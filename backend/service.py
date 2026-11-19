import hashlib
import json
import struct
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import threading
import time

from backend.art import image_cost, prices
from backend.prompts import ANTHRO_REVIEW_SUFFIX, compose, compose_anthro, get_settings
from backend.provider import ProviderRejected, QwenProvider
from backend.review import ReviewProvider, MockReviewProvider
from backend.image_workflow import new_attempt, run_images
from backend.store import Problem, now, uid
from scripts.verify_lux3d import inspect_glb

ACTIVE = {'queued', 'running'}


def find(items, item_id):
    item = next((i for i in items if i['id'] == item_id), None)
    if item is None:
        raise Problem('对应的版本、图片或任务不存在。', 404, 'NOT_FOUND')
    return item


def refresh_status(p):
    if any(t['status'] in ACTIVE for t in p['tasks']):
        p['status'] = 'generating'
    elif p.get('confirmedVersionId'):
        p['status'] = 'confirmed'
    elif any(v.get('designImageId') or v.get('model') for v in p['versions']):
        p['status'] = 'ready_for_review'
    else:
        p['status'] = 'draft'


class Studio:
    def __init__(self, store, provider, image_provider=None, poll_interval=15, review_provider=None):
        self.store, self.provider = store, provider
        self.image_provider = image_provider or QwenProvider()
        self.review_provider = review_provider or (MockReviewProvider() if self.image_provider.mode == 'mock' else ReviewProvider())
        self.poll_interval = poll_interval
        self.pool = ThreadPoolExecutor(max_workers=3, thread_name_prefix='studio-task')
        self.stop = threading.Event()
        self._scheduled = set()

    def migrate(self):
        for p in self.store.all():
            p.setdefault('activeAnthroVersionId', p.get('confirmedAnthroVersionId'))
            p.setdefault('confirmedAnthroVersionId', None)
            if p.get('schemaVersion', 1) >= 2:
                continue
            p['schemaVersion'] = 2
            p['activeImageIds'] = p.get('activeImageIds', [])[:3]
            for i, image in enumerate(p['sourceImages']):
                image.setdefault('purpose', ['物件正面', '侧面与结构', '风格参考'][min(i, 2)])
            for v in p['versions']:
                v['legacyAsset'] = True
                model_task = next((t for t in p['tasks'] if t.get('versionId') == v['id'] and t['kind'] == 'img-to-3d'), None)
                if model_task:
                    v.setdefault('modelTaskId', model_task['id'])
                design = next((i for i in p['sourceImages'] if i['id'] == v.get('designImageId')), None)
                if design and design.get('createdFromTaskId'):
                    v.setdefault('designTaskId', design['createdFromTaskId'])
                if v.get('model') and v.get('designImageId'):
                    v['model'].update(sourceDesignImageId=v['designImageId'], sourceDesignVersionId=v['id'])
            p.setdefault('activeVersionId', p.get('confirmedVersionId') or (p['versions'][-1]['id'] if p['versions'] else None))
            self.store.save(p)

    def recover(self):
        self.migrate()
        for p in self.store.all():
            for task in p['tasks']:
                if task['status'] not in ACTIVE:
                    continue
                has_result = task.get('_providerResult') if task['kind'] == 'image-generation' else task.get('providerTaskId')
                if task.get('_review'):
                    attempt = task['_review']['attempts'][-1]
                    if attempt['state'] == 'submitting' and not attempt.get('result'):
                        self.schedule(p['id'], task['id'])
                        continue
                    # The workflow resumes saved downloads/decisions and handles interrupted inspections.
                    self.schedule(p['id'], task['id'])
                    continue
                if task.get('stage') == 'submitting' and not has_result:
                    self.fail(p['id'], task['id'], 'SUBMISSION_UNKNOWN', '提交时服务中断，请先核对平台请求记录，避免重复扣费。', False)
                elif task['kind'] not in {'img-to-3d', 'image-generation'}:
                    self.fail(p['id'], task['id'], 'LEGACY_TASK', '这是旧版生图任务，请在平台核对结果；新设计统一使用 Qwen。', False)
                else:
                    self.schedule(p['id'], task['id'])

    def shutdown(self):
        self.stop.set()
        self.pool.shutdown(wait=True, cancel_futures=True)

    def busy(self, p):
        if any(t['status'] in ACTIVE for t in p['tasks']):
            raise Problem('此项目已有任务处理中，请等待或恢复现有任务。', 409, 'PROJECT_BUSY')

    def patch(self, project_id, body):
        def change(p):
            if 'title' in body:
                title = str(body['title']).strip()
                if not title or len(title) > 80:
                    raise Problem('项目名称需为 1–80 个字。')
                p['title'] = title
            if 'inputInstruction' in body:
                text = str(body['inputInstruction'])
                if len(text) > 1500:
                    raise Problem('想法最多 1500 个字。')
                p['inputInstruction'] = text
            if 'activeVersionId' in body:
                if body['activeVersionId'] is not None:
                    find(p['versions'], body['activeVersionId'])
                p['activeVersionId'] = body['activeVersionId']
        return self.store.update(project_id, change)

    def upload(self, project_id, data, name, activate=True, purpose=None):
        with self.store.lock:
            p = self.store.get(project_id)
            if activate and len(p['activeImageIds']) >= 3:
                raise Problem('初始设计最多使用 3 张参考图。', code='TOO_MANY_IMAGES')
            image = self.store.save_image(data, name)
            image.update(order=len(p['activeImageIds']), role='reference' if activate else 'auxiliary',
                         purpose=str(purpose or ['物件正面', '侧面与结构', '风格参考'][min(len(p['activeImageIds']), 2)])[:80])
            p['sourceImages'].append(image)
            if activate:
                p['activeImageIds'].append(image['id'])
            return self.store.save(p)

    def edit_image(self, project_id, image_id, body):
        def change(p):
            image = find(p['sourceImages'], image_id)
            purpose = str(body.get('purpose', '')).strip()
            if not purpose or len(purpose) > 80:
                raise Problem('请填写 1–80 字的参考用途。')
            image['purpose'] = purpose
        return self.store.update(project_id, change)

    def order_images(self, project_id, image_ids):
        def change(p):
            if not isinstance(image_ids, list) or len(image_ids) > 3 or len(set(image_ids)) != len(image_ids):
                raise Problem('最多选择 3 张不同的参考图。')
            for index, image_id in enumerate(image_ids):
                image = find(p['sourceImages'], image_id)
                if image['origin'] != 'upload':
                    raise Problem('初始参考图请从上传的照片中选择。')
                image.update(order=index, role='reference')
            p['activeImageIds'] = image_ids
        return self.store.update(project_id, change)

    def change_images(self, project_id, image_id, remove=False):
        with self.store.lock:
            p = self.store.get(project_id)
            ids = list(p['activeImageIds'])
            if image_id not in ids:
                raise Problem('该参考图已移除。', 404)
            ids.remove(image_id)
            if not remove:
                ids.insert(0, image_id)
            return self.order_images(project_id, ids)

    def references(self, p, refs, limit):
        if not isinstance(refs, list) or len(refs) > limit:
            raise Problem('本轮参考图数量超过限制。')
        result, seen = [], set()
        for ref in refs:
            if not isinstance(ref, dict):
                raise Problem('参考图格式不合法。')
            image = find(p['sourceImages'], ref.get('imageId'))
            if image['id'] in seen:
                raise Problem('同一张参考图不能重复选择。')
            purpose = str(ref.get('purpose') or image.get('purpose') or '物件参考').strip()
            if not purpose or len(purpose) > 80:
                raise Problem('每张参考图的用途最多 80 字。')
            result.append({'imageId': image['id'], 'purpose': purpose})
            seen.add(image['id'])
        return result

    def start(self, project_id, action, body):
        request_id = body.get('requestId', '')
        if not isinstance(request_id, str) or not 8 <= len(request_id) <= 100:
            raise Problem('缺少有效的请求标识。', code='MISSING_REQUEST_ID')
        fingerprint = hashlib.sha256(json.dumps({'action': action, 'body': body}, sort_keys=True).encode()).hexdigest()
        with self.store.lock:
            p = self.store.get(project_id)
            previous = p['_requests'].get(request_id)
            if previous:
                if previous != fingerprint:
                    raise Problem('相同请求标识不能用于不同操作。', 409, 'REQUEST_CONFLICT')
                return p
            self.busy(p)
            instruction = str(body.get('instruction', '')).strip()
            if len(instruction) > 1500:
                raise Problem('想法或修改意见最多 1500 个字。')
            if action == 'approve-design':
                if not self.provider.configured:
                    raise Problem('请配置服务端 AHOLO_API_KEY，重启后即可生成模型。', 503, 'AHOLO_NOT_CONFIGURED')
                v = find(p['versions'], body.get('versionId'))
                if not v.get('designImageId') or v['state'] in {'designing', 'discarded'}:
                    raise Problem('请先选择一张已生成的单主体设计图。', 409)
                if v.get('model'):
                    raise Problem('这张设计图已有对应模型，可直接切换查看。', 409, 'MODEL_EXISTS')
                if v.get('modelTaskId'):
                    raise Problem('该设计图已有模型任务，请使用现有任务的恢复或重试入口。', 409, 'MODEL_TASK_EXISTS')
                design = find(p['sourceImages'], v['designImageId'])
                if design.get('origin') != 'generated-design':
                    raise Problem('只能使用设计图生成模型，不能提交多视图排版页。')
                source_ids = [design['id']]
                kind, provider = 'img-to-3d', self.provider
                deps = [v.get('designTaskId') or v['createdFromTaskId']]
                v['state'] = 'generating'
                prompt, refs = None, [{'imageId': design['id'], 'purpose': '用户选中并确认的单主体设计图'}]
            else:
                if action not in {'generate', 'revise'}:
                    raise Problem('不支持的生成操作。')
                if not self.image_provider.configured:
                    raise Problem('请配置服务端 QWEN_API_KEY，重启后即可生成设计图。', 503, 'QWEN_NOT_CONFIGURED')
                base = None
                if action == 'revise':
                    if not instruction:
                        raise Problem('请先描述希望修改的内容。')
                    base = find(p['versions'], body.get('baseVersionId'))
                    if not base.get('designImageId'):
                        raise Problem('此旧版资产没有设计图，请先从左侧生成一张新设计。')
                    auxiliary = self.references(p, body.get('auxiliaryReferences', []), 2)
                    if any(ref['imageId'] == base['designImageId'] for ref in auxiliary):
                        raise Problem('当前设计图已作为图 1，无需再选为辅助图。')
                    refs = [{'imageId': base['designImageId'], 'purpose': '当前选中的设计图，作为本轮修改的主体基础'}] + auxiliary
                else:
                    default_refs = [{'imageId': i, 'purpose': find(p['sourceImages'], i).get('purpose', '物件参考')} for i in p['activeImageIds']]
                    refs = self.references(p, body.get('references', default_refs), 3)
                    if not instruction and not refs:
                        raise Problem('写下你的想法，或选择至少一张参考图。')
                source_ids = [ref['imageId'] for ref in refs]
                kind, provider = 'image-generation', self.image_provider
                settings = get_settings(self.store)
                prompt = compose(instruction, refs, revision=bool(base), fields=settings["fields"])
                v = {'id': uid(), 'version': len(p['versions']) + 1, 'sourceImageIds': source_ids,
                     'references': refs, 'userInstruction': instruction, 'actualPrompt': prompt,
                     'artRulesVersion': settings['baseVersion'], 'promptSettingsRevision': settings['revision'],
                     'negativePrompt': settings['fields']['negative'], 'state': 'designing', 'createdAt': now()}
                if base:
                    v['parentVersionId'] = base['id']
                p['versions'].append(v)
                deps = [base.get('designTaskId') or base['createdFromTaskId']] if base else []
            tid = uid()
            task = {'id': tid, 'kind': kind, 'provider': provider.name, 'providerVersion': provider.model,
                    'inputImageIds': source_ids, 'references': refs, 'prompt': prompt,
                    'userInstruction': instruction, 'dependsOnTaskIds': deps, 'status': 'queued', 'stage': 'queued',
                    'versionId': v['id'], 'clientRequestId': request_id, 'createdAt': now(), 'updatedAt': now()}
            if kind == 'img-to-3d':
                task.update(providerRegion=self.provider.region, sourceDesignImageId=v['designImageId'],
                            cost={'currency': 'CREDITS', 'amount': prices()['aholoCredits'], 'isEstimate': True, 'basis': 'estimate'})
                v['modelTaskId'] = tid
                task['requestRecord'] = {'method': 'POST', 'endpoint': provider.api_url, 'model': provider.model,
                    'inputImageIds': source_ids, 'parameters': {'faceCount': 100000, 'enablePbr': True, 'outputFormat': ['glb']}}
            else:
                task.update(promptSettingsSnapshot={k: value for k, value in settings.items() if k != 'agent'},
                            negativePrompt=settings['fields']['negative'])
                anthro_agent = {**settings['agent'], 'instruction': settings['agent']['instruction'] + '\n\n' + ANTHRO_REVIEW_SUFFIX,
                                'repairInstruction': settings['agent']['repairInstruction'] + '\n\n' + ANTHRO_REVIEW_SUFFIX}
                task['_review'] = {'config': anthro_agent, 'originalReferences': refs,
                    'attempts': [new_attempt(self, refs, prompt, task['negativePrompt'], 'initial')]}
                v['createdFromTaskId'] = v['designTaskId'] = tid
                task['cost'] = image_cost(len(source_ids))
                task['requestRecord'] = {'method': 'POST', 'endpoint': provider.api_url, 'model': provider.model,
                    'prompt': prompt, 'negative_prompt': task['negativePrompt'], 'n': 1, 'size': '1024x1024', 'prompt_extend': False, 'watermark': False,
                    'inputImages': [{'imageId': i, 'sha256': self.store.file(i)['sha256'], 'encoding': 'data-url'} for i in source_ids]}
            p['tasks'].append(task)
            p['activeVersionId'] = v['id']
            p['_requests'][request_id] = fingerprint
            refresh_status(p)
            self.store.save(p)
            self.schedule(project_id, tid)
            return p

    def anthro_context(self, project_id):
        p = self.store.get(project_id)
        object_id = p.get('confirmedVersionId')
        object_version = next((v for v in p['versions'] if v['id'] == object_id and v.get('model')), None)
        if not object_version:
            raise Problem('请先在原物件阶段定稿一个 3D 模型，才能进入拟人形象阶段。', 409, 'ANTHRO_LOCKED')
        design = find(p['sourceImages'], object_version.get('designImageId')) if object_version.get('designImageId') else None
        if not design or design.get('origin') != 'generated-design':
            raise Problem('原物件定稿缺少对应的单主体设计图，暂时不能进入拟人阶段。', 409, 'ANTHRO_SOURCE_MISSING')
        return {'project': p, 'objectVersion': object_version, 'objectDesign': design,
                'anthroVersions': [v for v in p['versions'] if v.get('stage') == 'anthro'],
                'unlocked': True}

    def start_anthro(self, project_id, action, body):
        request_id = body.get('requestId', '')
        if not isinstance(request_id, str) or not 8 <= len(request_id) <= 100:
            raise Problem('缺少有效的请求标识。', code='MISSING_REQUEST_ID')
        fingerprint = hashlib.sha256(json.dumps({'action': action, 'body': body}, sort_keys=True).encode()).hexdigest()
        with self.store.lock:
            p = self.store.get(project_id)
            previous = p['_requests'].get(request_id)
            if previous:
                if previous != fingerprint:
                    raise Problem('相同请求标识不能用于不同操作。', 409, 'REQUEST_CONFLICT')
                return p
            self.busy(p)
            context = self.anthro_context(project_id)
            object_version, object_design = context['objectVersion'], context['objectDesign']
            instruction = str(body.get('instruction', '')).strip()
            if len(instruction) > 1500:
                raise Problem('拟人要求最多 1500 个字。')
            concept = str(body.get('conceptOption', 'handle-ear'))
            if concept not in {'handle-ear', 'handle-tail', 'handle-equipment', 'custom'}:
                raise Problem('把手转化方案不合法。')
            settings = get_settings(self.store)
            if action == 'approve':
                version = find(p['versions'], body.get('versionId'))
                if version.get('stage') != 'anthro' or not version.get('designImageId'):
                    raise Problem('请先选择一张已生成的拟人设计图。', 409)
                if version.get('model') or version.get('modelTaskId'):
                    raise Problem('该拟人设计图已有模型任务或模型。', 409)
                kind, provider, refs, prompt, v = 'img-to-3d', self.provider, [{'imageId': version['designImageId'], 'purpose': '用户确认的单主体幼年拟人设计图'}], None, version
                deps = [version.get('designTaskId') or version.get('createdFromTaskId')]
                v['state'] = 'generating'
            else:
                if action not in {'generate', 'revise'}:
                    raise Problem('不支持的拟人生成操作。')
                base = None
                if action == 'revise':
                    if not instruction:
                        raise Problem('请先描述拟人设计的修改内容。')
                    base = find(p['versions'], body.get('baseVersionId'))
                    if base.get('stage') != 'anthro' or not base.get('designImageId'):
                        raise Problem('拟人修改必须基于已有拟人设计图。')
                    auxiliary = self.references(p, body.get('auxiliaryReferences', []), 2)
                    if any(ref['imageId'] == base['designImageId'] for ref in auxiliary):
                        raise Problem('当前拟人设计图已作为图 1，无需重复选择。')
                    refs = [{'imageId': base['designImageId'], 'purpose': '当前拟人设计图，作为本轮修改主体'}] + auxiliary
                else:
                    auxiliary = self.references(p, body.get('auxiliaryReferences', []), 2)
                    refs = [{'imageId': object_design['id'], 'purpose': '原物件确认设计图，保留材质、颜色与关键结构'}] + auxiliary
                source_ids = [r['imageId'] for r in refs]
                prompt = compose_anthro(instruction, refs, concept, revision=bool(base), fields=settings['anthro'])
                v = {'id': uid(), 'version': len(p['versions']) + 1, 'stage': 'anthro', 'stageName': 'juvenile',
                     'sourceObjectVersionId': object_version['id'], 'sourceObjectDesignImageId': object_design['id'],
                     'sourceImageIds': source_ids, 'references': refs, 'conceptOption': concept,
                     'userInstruction': instruction, 'actualPrompt': prompt, 'artRulesVersion': settings['baseVersion'],
                     'promptSettingsRevision': settings['revision'], 'negativePrompt': settings['anthro']['negative'],
                     'state': 'designing', 'rigStatus': 'not-tested', 'animationStatus': 'not-tested', 'createdAt': now()}
                if base:
                    v['parentVersionId'] = base['id']
                p['versions'].append(v)
                kind, provider, deps = 'image-generation', self.image_provider, ([base.get('designTaskId') or base.get('createdFromTaskId')] if base else [])
            source_ids = [r['imageId'] for r in refs] if kind == 'img-to-3d' else [r['imageId'] for r in refs]
            tid = uid()
            task = {'id': tid, 'kind': kind, 'stageKind': 'anthro', 'provider': provider.name, 'providerVersion': provider.model,
                    'inputImageIds': source_ids, 'references': refs, 'prompt': prompt, 'userInstruction': instruction,
                    'dependsOnTaskIds': deps, 'status': 'queued', 'stage': 'queued', 'versionId': v['id'],
                    'clientRequestId': request_id, 'createdAt': now(), 'updatedAt': now()}
            if kind == 'img-to-3d':
                task.update(providerRegion=self.provider.region, sourceDesignImageId=v['designImageId'], sourceObjectVersionId=object_version['id'],
                            cost={'currency': 'CREDITS', 'amount': prices()['aholoCredits'], 'isEstimate': True, 'basis': 'estimate'})
                v['modelTaskId'] = tid
                task['requestRecord'] = {'method': 'POST', 'endpoint': provider.api_url, 'model': provider.model,
                    'inputImageIds': source_ids, 'parameters': {'faceCount': 100000, 'enablePbr': True, 'outputFormat': ['glb']}}
            else:
                task.update(promptSettingsSnapshot={**{k: value for k, value in settings.items() if k != 'agent'}, 'fields': settings['anthro']},
                            negativePrompt=settings['anthro']['negative'])
                task['_review'] = {'config': settings['agent'], 'originalReferences': refs,
                    'attempts': [new_attempt(self, refs, prompt, task['negativePrompt'], 'initial')]}
                v['createdFromTaskId'] = v['designTaskId'] = tid
                task['cost'] = image_cost(len(source_ids))
                task['requestRecord'] = {'method': 'POST', 'endpoint': provider.api_url, 'model': provider.model,
                    'prompt': prompt, 'negative_prompt': task['negativePrompt'], 'n': 1, 'size': '1024x1024', 'prompt_extend': False, 'watermark': False,
                    'inputImages': [{'imageId': i, 'sha256': self.store.file(i)['sha256'], 'encoding': 'data-url'} for i in source_ids]}
            p['tasks'].append(task)
            p['activeAnthroVersionId'] = v['id']
            p['_requests'][request_id] = fingerprint
            refresh_status(p); self.store.save(p); self.schedule(project_id, tid); return p

    def confirm(self, project_id, version_id):
        def change(p):
            v = find(p['versions'], version_id)
            if not v.get('model'):
                raise Problem('只有已生成模型的版本才能定稿。', 409)
            for other in p['versions']:
                if other['state'] == 'confirmed':
                    other['state'] = 'ready'
            v['state'] = 'confirmed'
            p['confirmedVersionId'] = p['activeVersionId'] = version_id
            refresh_status(p)
        return self.store.update(project_id, change)

    def confirm_anthro(self, project_id, version_id):
        def change(p):
            v = find(p['versions'], version_id)
            if v.get('stage') != 'anthro' or not v.get('model'):
                raise Problem('只有已生成拟人模型的版本才能定稿。', 409)
            for other in p['versions']:
                if other.get('stage') == 'anthro' and other.get('state') == 'confirmed':
                    other['state'] = 'ready'
            v['state'] = 'confirmed'; p['confirmedAnthroVersionId'] = v['id']; p['activeAnthroVersionId'] = v['id']
            refresh_status(p)
        return self.store.update(project_id, change)

    def inspect_anthro_rig(self, project_id, version_id):
        def change(p):
            v = find(p['versions'], version_id)
            if v.get('stage') != 'anthro' or not v.get('model'):
                raise Problem('请先生成拟人模型。', 409)
            raw = self.store.file_path(v['model']['id']).read_bytes()
            if len(raw) < 20 or raw[:4] != b'glTF':
                raise Problem('拟人模型文件不是合法 GLB。', 422)
            chunk_size, chunk_kind = struct.unpack_from('<II', raw, 12)
            if chunk_kind != 0x4E4F534A or 20 + chunk_size > len(raw):
                raise Problem('拟人模型缺少合法 glTF 数据。', 422)
            document = json.loads(raw[20:20 + chunk_size])
            skins = len(document.get('skins', []))
            animations = len(document.get('animations', []))
            v['rigStatus'] = 'passed' if skins else 'failed'
            v['animationStatus'] = 'passed' if animations else 'failed'
            v['rigReport'] = {'checkedAt': now(), 'skins': skins, 'animations': animations,
                              'meshes': len(document.get('meshes', [])), 'materials': len(document.get('materials', [])),
                              'note': '当前 GLB 可展示，但需要额外骨骼绑定与动作制作。' if not skins else 'GLB 已包含骨骼数据。'}
        return self.store.update(project_id, change)

    def discard(self, project_id, version_id):
        def change(p):
            v = find(p['versions'], version_id)
            if v['state'] != 'awaiting_design_confirmation' or v.get('modelTaskId'):
                raise Problem('只能放弃尚未提交模型任务的设计图。', 409)
            v['state'] = 'discarded'
            refresh_status(p)
        return self.store.update(project_id, change)

    def retry(self, project_id, task_id, request_id):
        if not isinstance(request_id, str) or not 8 <= len(request_id) <= 100:
            raise Problem('缺少有效的请求标识。')
        with self.store.lock:
            p = self.store.get(project_id)
            task = find(p['tasks'], task_id)
            if task['kind'] not in {'image-generation', 'img-to-3d'}:
                raise Problem('此旧版生图任务不能在新服务中重试，请核对平台记录后新建设计。', 409, 'LEGACY_TASK')
            if task['status'] in ACTIVE or task.get('_lastRetryId') == request_id:
                return p
            self.busy(p)
            if not task.get('canRetry'):
                raise Problem('该任务不能直接重试，请先核对平台请求记录。', 409)
            if task['kind'] == 'image-generation' and find(p['versions'], task['versionId']).get('modelTaskId'):
                raise Problem('此设计已提交模型生成。请新建设计版本继续修改，以保留模型来源。', 409)
            task['_lastRetryId'] = request_id
            if task.get('_providerTerminal'):
                task.setdefault('previousProviderTaskIds', []).append(task['providerTaskId'])
                task.pop('providerTaskId', None)
                task.pop('_providerTerminal', None)
            task.update(status='queued', stage='queued', updatedAt=now())
            task.pop('error', None)
            v = find(p['versions'], task['versionId'])
            v['state'] = 'designing' if task['kind'] == 'image-generation' else 'generating'
            p['activeVersionId'] = v['id']
            refresh_status(p)
            self.store.save(p)
            self.schedule(project_id, task_id)
            return p

    def preview(self, project_id, version_id, data):
        with self.store.lock:
            p = self.store.get(project_id)
            v = find(p['versions'], version_id)
            if not v.get('model'):
                raise Problem('此版本还没有模型。')
            if not v.get('previewImage'):
                v['previewImage'] = self.store.save_image(data, 'model-preview.png', 'model-render')
                self.store.save(p)
            return p

    def schedule(self, project_id, task_id):
        with self.store.lock:
            if task_id in self._scheduled:
                return
            self._scheduled.add(task_id)
        self.pool.submit(self.run, project_id, task_id)

    def update_task(self, project_id, task_id, **values):
        def change(p):
            find(p['tasks'], task_id).update(values, updatedAt=now())
            refresh_status(p)
        return self.store.update(project_id, change)

    def fail(self, project_id, task_id, code, message, can_retry=True):
        def change(p):
            task = find(p['tasks'], task_id)
            task.update(status='failed', canRetry=can_retry, error={'code': code, 'message': message}, updatedAt=now())
            v = find(p['versions'], task['versionId'])
            if task.get('_review'):
                candidates = [a['file'] for a in task['_review']['attempts'] if a.get('file')]
                if candidates:
                    candidate = candidates[-1]
                    candidate.update(createdFromTaskId=task_id, purpose='单主体幼年拟人设计图' if task.get('stageKind') == 'anthro' else '单主体原物件设计图')
                    if not any(i['id'] == candidate['id'] for i in p['sourceImages']):
                        p['sourceImages'].append(candidate)
                    v['designImageId'] = candidate['id']
                    task['needsReview'] = True
            v['state'] = 'awaiting_design_confirmation' if v.get('designImageId') and not v.get('model') else 'failed'
            refresh_status(p)
        self.store.update(project_id, change)

    def complete(self, project_id, task_id, file):
        def change(p):
            t = find(p['tasks'], task_id)
            v = find(p['versions'], t['versionId'])
            t.update(status='succeeded', stage='complete', outputs=[file], canRetry=False, updatedAt=now())
            t.pop('error', None)
            if t['kind'] == 'img-to-3d':
                file.update(sourceDesignImageId=t.get('sourceDesignImageId'), sourceDesignVersionId=v['id'])
                file.update(stage=v.get('stage', 'object'), sourceObjectVersionId=t.get('sourceObjectVersionId'))
                v.update(model=file, state='ready')
            else:
                file.update(createdFromTaskId=task_id, purpose='单主体幼年拟人设计图' if t.get('stageKind') == 'anthro' else '单主体原物件设计图')
                if not any(i['id'] == file['id'] for i in p['sourceImages']):
                    p['sourceImages'].append(file)
                v.update(designImageId=file['id'], state='awaiting_design_confirmation')
            refresh_status(p)
        return self.store.update(project_id, change)

    def run(self, project_id, task_id):
        stage = 'preparing'
        try:
            task = find(self.store.get(project_id)['tasks'], task_id)
            if task['kind'] not in {'image-generation', 'img-to-3d'}:
                self.fail(project_id, task_id, 'LEGACY_TASK', '旧版任务不可使用新服务重新提交，请先核对平台记录。', False)
                return
            if task['kind'] == 'image-generation':
                if task.get('_review'):
                    run_images(self, project_id, task_id, task)
                    return
                result = task.get('_providerResult')
                if not result:
                    if self.stop.is_set():
                        return
                    stage = 'submitting'
                    self.update_task(project_id, task_id, status='running', stage=stage)
                    result = self.image_provider.generate([self.store.file_path(i) for i in task['inputImageIds']], task['prompt'], task.get('negativePrompt', ''))
                    usage = result.get('usage', {})
                    expiry = (datetime.now(timezone.utc) + timedelta(hours=24)).isoformat()
                    self.update_task(project_id, task_id, _providerResult=result, providerRequestId=result.get('requestId'),
                        responseStatus=result.get('responseStatus'), usage=usage, providerResultExpiresAt=expiry,
                        cost=image_cost(int(usage.get('input_image_count', len(task['inputImageIds']))),
                                        int(usage.get('output_image_count', 1)), 'usage'))
                elif task.get('providerResultExpiresAt') and datetime.fromisoformat(task['providerResultExpiresAt']) < datetime.now(timezone.utc):
                    self.fail(project_id, task_id, 'OUTPUT_EXPIRED', 'Qwen 结果链接已过期，无法恢复下载。请核对平台记录后另建生成。', False)
                    return
                stage = 'downloading'
                self.update_task(project_id, task_id, status='running', stage=stage)
                data, suffix = self.image_provider.download(result)
                file = self.store.save_image(data, 'design.' + suffix, 'generated-design')
                self.complete(project_id, task_id, file)
                return
            if not task.get('providerTaskId'):
                self.update_task(project_id, task_id, status='running', stage='uploading')
                urls = [self.provider.upload(self.store.file_path(i)) for i in task['inputImageIds']]
                if self.stop.is_set():
                    return
                stage = 'submitting'
                self.update_task(project_id, task_id, stage=stage)
                provider_id = self.provider.create('img-to-3d', urls)
                self.update_task(project_id, task_id, providerTaskId=provider_id, stage='polling')
            else:
                provider_id = task['providerTaskId']
                self.update_task(project_id, task_id, status='running', stage='polling')
            stage, failures = 'polling', 0
            deadline = time.monotonic() + 1800
            while not self.stop.is_set() and time.monotonic() < deadline:
                try:
                    result = self.provider.query(provider_id)
                    failures = 0
                except Exception:
                    failures += 1
                    if failures >= 3:
                        raise
                    self.stop.wait(self.poll_interval)
                    continue
                code = result['status']
                self.update_task(project_id, task_id, providerStatus=code)
                if code in {4, 6}:
                    self.update_task(project_id, task_id, _providerTerminal=True)
                    self.fail(project_id, task_id, 'PROVIDER_FAILED', 'Aholo 生成未成功；手动重新提交可能消耗积分。')
                    return
                if code == 3:
                    stage = 'downloading'
                    self.update_task(project_id, task_id, stage=stage)
                    data, _ = self.provider.download(result, 'img-to-3d')
                    file = self.store.save_file(data, 'glb', 'object.glb')
                    inspect_glb(self.store.file_path(file['id']))
                    self.complete(project_id, task_id, file)
                    return
                self.stop.wait(self.poll_interval)
            if not self.stop.is_set():
                self.fail(project_id, task_id, 'POLL_TIMEOUT', '等待到时，可恢复查询已有任务，无需重新生成。')
        except Exception as exc:
            name = type(exc).__name__
            if isinstance(exc, ProviderRejected):
                self.update_task(project_id, task_id, responseStatus=exc.status, providerRequestId=exc.request_id, providerErrorCode=exc.code)
                if exc.status >= 500 or exc.status == 408:
                    self.fail(project_id, task_id, 'SUBMISSION_UNKNOWN', 'Qwen 服务端响应异常，是否计费尚不明确；请先核对平台请求记录。', False)
                else:
                    message = ('Qwen 密钥或模型权限不可用，请检查独立的 Qwen 配置。' if exc.status in {401, 403}
                               else 'Qwen 请求被拒绝（HTTP %d，%s），请检查额度、请求参数或限流。' % (exc.status, exc.code))
                    self.fail(project_id, task_id, 'PROVIDER_REJECTED', message)
            elif stage == 'submitting' and name not in {'AuthenticationError', 'BusinessError', 'RateLimitError'}:
                self.fail(project_id, task_id, 'SUBMISSION_UNKNOWN', '生成提交响应中断，无法确定是否已计费；请先核对平台请求记录。', False)
            else:
                messages = {'AuthenticationError': 'Aholo 密钥无效或没有对应权限，请检查独立的 Aholo 配置。',
                            'RateLimitError': '平台暂时限流，请稍后重试。',
                            'BusinessError': '平台未接受请求，请检查额度、模型权限或平台历史。'}
                fallback = ('生成结果下载或校验中断；可恢复原任务结果下载，无需重新生成。' if stage == 'downloading'
                            else '任务处理中断，已保存记录；可重试上传或恢复已有任务查询。')
                self.fail(project_id, task_id, 'TASK_INTERRUPTED', messages.get(name, fallback))
            print('Task error: %s (%s)' % (task_id, name), flush=True)
        finally:
            with self.store.lock:
                self._scheduled.discard(task_id)
                current = find(self.store.get(project_id)['tasks'], task_id)
                if current['status'] == 'queued' and not self.stop.is_set():
                    self.schedule(project_id, task_id)
