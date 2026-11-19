"""Durable image → inspect → bounded correction workflow, one public version."""
import json
from datetime import datetime, timedelta, timezone
from decimal import Decimal

from backend.art import image_cost
from backend.prompts import compose, compose_anthro
from backend.provider import ProviderRejected
from backend.review import PROTOCOL, review_cost, validate_decision
from backend.store import now


def request_record(studio, refs, prompt, negative):
    return {'method': 'POST', 'endpoint': studio.image_provider.api_url, 'model': studio.image_provider.model,
            'prompt': prompt, 'negative_prompt': negative, 'n': 1, 'size': '1024x1024',
            'prompt_extend': False, 'watermark': False,
            'inputImages': [{'imageId': r['imageId'], 'purpose': r['purpose'],
                             'sha256': studio.store.file(r['imageId'])['sha256'], 'encoding': 'data-url'} for r in refs]}


def new_attempt(studio, refs, prompt, negative, action):
    return {'state': 'pending', 'action': action, 'references': refs, 'prompt': prompt,
            'createdAt': now(), 'requestRecord': request_record(studio, refs, prompt, negative)}


def checkpoint(studio, pid, tid, flow, **values):
    # Each response and next tool choice is committed before executing another request.
    attempts = flow['attempts']
    charged = [a for a in attempts if a.get('cost')]
    reviews = [a['review'] for a in attempts if a.get('review')]
    costs = [a['cost'] for a in charged] + [r['cost'] for r in reviews if r.get('cost')]
    if costs:
        total = float(sum((Decimal(str(c['amount'])) for c in costs), Decimal(0)))
        cost = {**charged[-1]['cost'], 'amount': total,
                'inputImageCount': sum(a['cost']['inputImageCount'] for a in charged),
                'outputImageCount': sum(a['cost']['outputImageCount'] for a in charged),
                'basis': 'usage', 'isEstimate': True}
        values['cost'] = cost
        values['costRecorded'] = True
    values['costIncomplete'] = (any(a.get('billingUnknown') for a in attempts)
                                or any(r.get('billingUnknown') or (r.get('result') and not r.get('cost')) for r in reviews))
    studio.update_task(pid, tid, _review=flow, **values)


def finish(studio, pid, tid, flow, status):
    flow['outcome'] = status
    checkpoint(studio, pid, tid, flow, stage='downloading',
               needsReview=status != 'accepted')
    studio.complete(pid, tid, flow['attempts'][-1]['file'])


def run_images(studio, pid, tid, task):
    flow = task['_review']
    config = flow['config']
    fields = task['promptSettingsSnapshot']['fields']
    negative = task.get('negativePrompt', '')
    while not studio.stop.is_set():
        attempt = flow['attempts'][-1]
        # A process restart must never silently replay a POST whose outcome is unknown.
        if attempt['state'] == 'submitting' and not attempt.get('result'):
            attempt['billingUnknown'] = True
            checkpoint(studio, pid, tid, flow)
            studio.fail(pid, tid, 'SUBMISSION_UNKNOWN', '生成提交响应中断，是否计费尚不明确；请核对平台请求记录。', False)
            return
        if not attempt.get('result'):
            attempt['state'] = 'submitting'
            checkpoint(studio, pid, tid, flow, status='running', stage='submitting')
            try:
                result = studio.image_provider.generate(
                    [studio.store.file_path(r['imageId']) for r in attempt['references']], attempt['prompt'], negative)
            except Exception as exc:
                definite = isinstance(exc, ProviderRejected) and exc.status < 500 and exc.status != 408
                attempt['state'] = 'rejected' if definite else 'submitting'
                attempt['error'] = {'type': type(exc).__name__, 'at': now()}
                attempt['billingUnknown'] = not definite
                if isinstance(exc, ProviderRejected):
                    attempt['error'].update(responseStatus=exc.status, requestId=exc.request_id, code=exc.code)
                checkpoint(studio, pid, tid, flow)
                studio.fail(pid, tid, 'PROVIDER_REJECTED' if definite else 'SUBMISSION_UNKNOWN',
                    'Qwen 请求被拒绝，请检查密钥、权限、额度或请求参数。' if definite else
                    '生成提交响应中断，无法确定是否已计费；请先核对平台请求记录。', definite)
                return
            usage = result.get('usage', {})
            attempt.update(result=result, state='responded', usage=usage,
                           resultExpiresAt=(datetime.now(timezone.utc) + timedelta(hours=24)).isoformat(),
                           cost=image_cost(int(usage.get('input_image_count', len(attempt['references']))),
                                           int(usage.get('output_image_count', 1)), 'usage'))
            checkpoint(studio, pid, tid, flow, providerRequestId=result.get('requestId'),
                       responseStatus=result.get('responseStatus'), usage=usage,
                       providerResultExpiresAt=attempt['resultExpiresAt'])
        if studio.stop.is_set():
            return
        if not attempt.get('file'):
            if datetime.fromisoformat(attempt['resultExpiresAt']) < datetime.now(timezone.utc):
                studio.fail(pid, tid, 'OUTPUT_EXPIRED', 'Qwen 结果链接已过期，请核对平台记录后另建生成。', False)
                return
            checkpoint(studio, pid, tid, flow, status='running', stage='downloading')
            try:
                data, suffix = studio.image_provider.download(attempt['result'])
                file = studio.store.save_image(data, 'design.' + suffix, 'generated-design')
            except Exception:
                studio.fail(pid, tid, 'TASK_INTERRUPTED', '结果下载或校验中断，可恢复原任务下载，无需重新生成。')
                return
            attempt.update(file=file, state='downloaded')
            checkpoint(studio, pid, tid, flow)
        if studio.stop.is_set():
            return
        review = attempt.setdefault('review', {'state': 'pending'})
        if review['state'] == 'submitting' and not review.get('result'):
            review.update(state='failed', error={'code': 'REVIEW_INTERRUPTED', 'at': now()}, billingUnknown=True)
        if review['state'] == 'pending':
            candidate = attempt['file']['id']
            context = {'candidateImageId': candidate, 'originalReferences': flow['originalReferences'],
                       'userInstruction': task['userInstruction'], 'originalGenerationPrompt': task['prompt'],
                       'artRules': fields, 'previousIssues': [a['review']['decision']['issues']
                            for a in flow['attempts'][:-1] if a.get('review', {}).get('decision')]}
            ids = list(dict.fromkeys([r['imageId'] for r in flow['originalReferences']] + [candidate]))
            review.update(state='submitting', requestRecord={'method': 'POST', 'endpoint': config['apiUrl'],
                'model': config['model'], 'systemInstruction': config['instruction'], 'protocol': PROTOCOL,
                'context': context, 'inputImages': [{'imageId': i, 'sha256': studio.store.file(i)['sha256']} for i in ids],
                'response_format': {'type': 'json_object'}, 'enable_thinking': False, 'stream': False}, startedAt=now())
            checkpoint(studio, pid, tid, flow, status='running', stage='checking')
            try:
                if not studio.review_provider.configured:
                    raise ValueError('Agent not configured')
                result = studio.review_provider.inspect(config, context, [(i, studio.store.file_path(i)) for i in ids])
                review.update(result=result, cost=review_cost(result.get('usage', {}), config), state='responded')
                checkpoint(studio, pid, tid, flow)
            except Exception as exc:
                definite = isinstance(exc, ProviderRejected) and exc.status < 500 and exc.status != 408
                review.update(state='failed', error={'type': type(exc).__name__, 'at': now()},
                              billingUnknown=not definite and not isinstance(exc, ValueError))
                if isinstance(exc, ProviderRejected):
                    review['error'].update(responseStatus=exc.status, requestId=exc.request_id)
                checkpoint(studio, pid, tid, flow)
        if review['state'] == 'responded':
            try:
                result = review['result']
                if result.get('finishReason') not in {None, 'stop'}:
                    raise ValueError('Incomplete review')
                decision = validate_decision(json.loads(result['content']), attempt['file']['id'],
                                             [r['imageId'] for r in flow['originalReferences']])
                review.update(state='complete', decision=decision)
            except (ValueError, TypeError, KeyError):
                review.update(state='failed', error={'code': 'INVALID_DECISION', 'at': now()})
            checkpoint(studio, pid, tid, flow)
        if review['state'] == 'failed':
            finish(studio, pid, tid, flow, 'review_unavailable')
            return
        decision = review['decision']
        if decision['action'] == 'accept':
            finish(studio, pid, tid, flow, 'accepted')
            return
        if len(flow['attempts']) - 1 >= config['maxCorrections']:
            finish(studio, pid, tid, flow, 'needs_user_review')
            return
        purposes = {r['imageId']: r['purpose'] for r in flow['originalReferences']}
        purposes[attempt['file']['id']] = '当前候选设计图，本轮修改的主体基础'
        refs = [{'imageId': i, 'purpose': purposes[i]} for i in decision['referenceImageIds']]
        instructions = (config['repairInstruction'] + '\n【原始用户要求，后续修正不可丢失】\n' +
                        (task['userInstruction'] or fields['defaultIdea']) +
                        '\n【本轮修正意见】\n' + decision['instruction'])
        prompt = (compose_anthro(instructions, refs, task.get('conceptOption', 'custom'),
                                 revision=decision['action'] == 'edit', fields=fields)
                  if task.get('stageKind') == 'anthro' else
                  compose(instructions, refs, revision=decision['action'] == 'edit', fields=fields))
        flow['attempts'].append(new_attempt(studio, refs, prompt, negative, decision['action']))
        checkpoint(studio, pid, tid, flow, stage='queued')
