"""Persistent prompt revisions. A task snapshots the active revision at submission."""
from backend.store import Problem, now
from backend.review import DEFAULT_AGENT, validate_agent

ART_VERSION = 'object-spirit-v3'
DEFAULT_FIELDS = {
    'style': """卡通 3D 游戏道具的单物体渲染。圆润夸张的几何轮廓，简化细节，清晰配色，柔和的卡通立体着色。物件本身的比例、几何形状与材质呈现卡通感，保留原物件标志性结构。当前是原物件阶段，保持物件形态。""",
    'composition': """交付图像仅包含单个完整主体。主体占画面约 80%，居中，四分之三视角，顶部、底部及突出部件完整可见。主体之外全部为均匀纯白色 #FFFFFF，主体轮廓清晰，留白内没有任何内容。只有物件本身，保留原有组成部件；新增结构仅限用户明确要求。使用简洁的卡通 3D 道具渲染形式。""",
    'textDesign': '按用户描述创建一个独立的卡通 3D 游戏物件，呈现在纯白背景上。',
    'referenceDesign': '提取结构参考图中的一个主要物件，以卡通 3D 游戏道具风格重绘其本体。仅采纳参考用途指定的结构、配色或材质。输出纯白背景上的单个物件。',
    'revision': '以图 1 中的主要物件为基础，按用户意见修改。保留未要求修改的主体结构与视觉身份。辅助图按指定用途参考。仅保留主体本体，所有外围内容替换为纯白背景。输出单个完整物件。',
    'defaultIdea': '把参考物件重绘成圆润、简洁的卡通 3D 游戏道具，保留物件结构和配色。',
    'negative': '文字，汉字，字母，伪文字，标题，水印，商标，海报，商品详情页，UI，按钮，图例，标注，箭头，边框，拼图，多视角排版，多个物件，重复主体，图标，外围装饰，叶片，爱心，星星，闪光粒子，符文，光环，烟雾，背景道具，展示台，新增底座，杯碟，地面，投影，倒影，照片写实，水彩插画，卡通贴纸，笑脸，眼睛，人脸，手脚，裁切主体'
}

ANTHRO_DEFAULT_FIELDS = {
    'style': """早期桌面宠物游戏的通用卡通精灵美术：像独立的幼年生物而不是把物品贴上脸；头身比例明确但短小紧凑，大头、大眼、短肢、圆润夸张的外轮廓，少量高饱和主色块，粗而清晰的深色轮廓线，简洁的赛璐璐式明暗分区，表情鲜明、有活力、略带顽皮感。画面是干净的二维游戏角色立绘/精灵设定图质感，不是写实照片、潮玩产品渲染或复杂服装插画。只提炼通用特征，不复制任何具体作品、角色或 IP。""",
    'composition': """只生成一个完整的幼年拟人精灵，主体占画面约 75–85%，居中，四分之三视角，白色纯背景。原物件应被重新设计为角色的躯干、腹部或背部特征，头部与身体要有清楚的角色层次，不能只是原物件放大后长出脸。头、躯干、双臂、双腿、耳朵或尾巴（如有）必须轮廓清楚，根部和末端可见，四肢尽量分离，避免严重遮挡，保持站立或稳定悬浮姿态，适合后续骨骼绑定。不要生成三视图、动作表、成长阶段拼图或排版稿。""",
    'base': """以确认的原物件设计图和模型为身份来源，把它重新设计成一个可爱的幼年兽形小精灵。保留原物件的颜色、材质、轮廓和关键结构，但让角色拥有独立的头部、躯干和四肢：杯身应自然转化为躯干或腹部，不要把整只杯子直接当作头套在一个人形身体上。可以增加大眼、表情、短肢、耳朵、尾巴、简单装备和少量奇幻元素，但服装和装饰不能盖过原物件身份。""",
    'revision': """以图 1 的当前拟人设计为主体基础，按用户意见做局部修改。继续保留原物件身份、材质与关键结构，并维持独立的幼年生物轮廓、头身区分和短小精灵比例；辅助图只按用途参考。不得退回成人比例、写实人类、原物件长脸或复杂多角色构图。""",
    'defaultIdea': '把这个原物件变成老版端游卡通精灵风格的幼年小精灵，保留材质和关键特征，结构清楚并适合后续骨骼绑定。',
    'negative': '具体 IP 角色复制，写实人类，成人比例，长四肢，原物件直接长出脸和四肢，杯子头套在人形身体上，物件原样放大，复杂盔甲，厚重服装，多个角色，多状态拼图，三视图排版，动作表，文字，标题，水印，图标，场景，地面，底座，展示台，复杂背景，独立装饰物，漂浮道具，第三只耳朵，多余耳状结构，未要求的尾巴，严重遮挡，黏连四肢，手臂与身体融为一体，腿部不可见，尾巴断裂，耳朵悬空，切掉主体，纯物件无拟人结构'
}

ANTHRO_REVIEW_SUFFIX = """你现在检查的是拟人化幼年精灵，不是原物件阶段。必须检查：
1. 角色是否像一个独立的幼年端游精灵：头身层次明确、短小紧凑、大头大眼、短肢、圆润轮廓、清晰色块和简洁赛璐璐明暗。
2. 原物件是否被重新设计为躯干/腹部/背部等角色结构，而不是原杯子放大后直接长出脸和四肢；透明玻璃材质、淡蓝色、杯沿和竖纹仍应可识别。
3. 若结构方案是 handle-ear，必须正好两只耳朵：由杯把转化的两只黄色耳朵；禁止头部两侧再出现人类耳朵、动物小耳朵、第三只耳朵或任何耳状突起，也禁止未要求的尾巴；四肢根部、末端和关节区要清楚分离，适合后续绑定。
4. 画面必须只有一个完整角色、白色背景、四分之三视角，无文字、场景、排版、多角色或复杂服装。局部问题用 edit，整体仍是原物件长脸或风格偏离时用 regenerate。"""

LIMITS = {key: (500 if key == 'negative' else 2000) for key in DEFAULT_FIELDS}


def defaults():
    return {'revision': 0, 'baseVersion': ART_VERSION, 'updatedAt': None, 'fields': dict(DEFAULT_FIELDS),
            'anthro': dict(ANTHRO_DEFAULT_FIELDS), 'agent': dict(DEFAULT_AGENT)}


def validate_fields(fields):
    if not isinstance(fields, dict) or set(fields) != set(DEFAULT_FIELDS):
        raise Problem('配置字段不完整，请重新读取配置。')
    clean = {}
    for key, value in fields.items():
        if not isinstance(value, str) or len(value) > LIMITS[key] or (key != 'negative' and not value.strip()):
            raise Problem('提示词不能为空，普通项最多 2000 字，负向提示词最多 500 字。')
        clean[key] = value.strip()
    if sum(len(v) for k, v in clean.items() if k != 'negative') > 6500:
        raise Problem('正向提示词配置合计最多 6500 字，请精简后保存。')
    return clean


def get_settings(store):
    with store.lock:
        row = store.db.execute('SELECT data FROM prompt_settings ORDER BY revision DESC LIMIT 1').fetchone()
    if row:
        import json
        saved = json.loads(row[0])
        saved['fields'] = {**DEFAULT_FIELDS, **saved.get('fields', {})}
        saved['anthro'] = {**ANTHRO_DEFAULT_FIELDS, **saved.get('anthro', {})}
        saved['agent'] = {**DEFAULT_AGENT, **saved.get('agent', {})}
        return saved
    return defaults()


def save_settings(store, body):
    fields = validate_fields(body.get('fields'))
    with store.lock, store.db:
        current = get_settings(store)
        agent = validate_agent(body.get('agent', current['agent']))
        expected = body.get('expectedRevision')
        if type(expected) is not int or expected != current['revision']:
            raise Problem('配置已在其他页面更新。请复制你的修改，重新读取后再保存。', 409, 'PROMPT_CONFLICT')
        anthro = body.get('anthro', current.get('anthro', ANTHRO_DEFAULT_FIELDS))
        if not isinstance(anthro, dict) or set(anthro) != set(ANTHRO_DEFAULT_FIELDS):
            raise Problem('拟人阶段提示词配置字段不完整，请重新读取配置。')
        clean_anthro = {}
        for key, value in anthro.items():
            if not isinstance(value, str) or len(value) > (500 if key == 'negative' else 2400) or (key != 'negative' and not value.strip()):
                raise Problem('拟人阶段提示词不能为空或超过长度限制。')
            clean_anthro[key] = value.strip()
        result = {'revision': current['revision'] + 1, 'baseVersion': ART_VERSION,
                  'updatedAt': now(), 'fields': fields, 'anthro': clean_anthro, 'agent': agent}
        import json
        store.db.execute('INSERT INTO prompt_settings VALUES (?, ?)',
                         (result['revision'], json.dumps(result, ensure_ascii=False)))
        return result


def compose(instruction, references, revision=False, fields=None):
    f = fields or DEFAULT_FIELDS
    stage = 'revision' if revision else ('referenceDesign' if references else 'textDesign')
    refs = '\n'.join('图 %d：%s' % (i + 1, ref['purpose']) for i, ref in enumerate(references))
    prompt = '\n\n'.join([
        '【统一美术规则】\n' + f['style'], '【本轮任务】\n' + f[stage],
        '【参考图及用途，顺序必须对应】\n' + (refs or '无参考图，根据文字创作。'),
        '【用户想法或修改意见】\n' + (instruction or f['defaultIdea']),
        '【最终画面与交付约束】\n' + f['composition']])
    return prompt


def compose_anthro(instruction, references, concept, revision=False, fields=None):
    f = fields or ANTHRO_DEFAULT_FIELDS
    refs = '\n'.join('图 %d：%s' % (i + 1, ref['purpose']) for i, ref in enumerate(references))
    concept_text = {'handle-ear': '把手转化为一对对称、连接自然的耳朵',
                    'handle-tail': '把手转化为有清晰根部和末端的尾巴，不遮挡双腿',
                    'handle-equipment': '把手转化为明显但不过度复杂的标志性装备',
                    'custom': '把手转化方式按用户说明设计'} .get(concept, '把手转化方式按用户说明设计')
    return '\n\n'.join([
        '【统一幼年精灵美术规则】\n' + f['style'],
        '【拟人化任务】\n' + (f['revision'] if revision else f['base']),
        '【原物件结构转化】\n' + concept_text,
        '【参考图及用途，顺序必须对应】\n' + (refs or '无参考图，根据原物件定稿信息创作。'),
        '【用户想法或修改意见】\n' + (instruction or f['defaultIdea']),
        '【最终画面与绑定约束】\n' + f['composition']])
