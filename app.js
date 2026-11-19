const $ = id => document.getElementById(id);
const state = { project: null, config: null, selected: null, pending: false, loading: false, mode: 'design', loaded: false,
  capturing: false, lastModel: '', draftTimer: null, draftDirty: false, auxiliary: [], revisionOpen: false, compareMode: 'design' };
const labels = { designing: '设计中', awaiting_design_confirmation: '设计图已保存', generating: '模型生成中', ready: '模型已保存', confirmed: '模型已定稿', failed: '未完成', discarded: '已放弃' };
const viewer = $('viewer');
const selected = () => state.project?.versions.find(v => v.id === state.selected);
const sourceImage = id => state.project?.sourceImages.find(i => i.id === id);
const endpoint = suffix => `/api/projects/${state.project.id}${suffix}`;
const isBusy = () => state.project?.tasks.some(t => ['queued', 'running'].includes(t.status));
const money = value => `¥${Number(value).toFixed(2)}`;
function notice(text) { $('notice').textContent = text; $('notice').classList.toggle('hidden', !text); }
function saved(text = '所有更改已保存') { $('saveStatus').textContent = text; }
async function api(path, method = 'GET', body) {
  let response;
  try { response = await fetch(path, { method, headers: body === undefined ? {} : { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) }); }
  catch { throw new Error('连接中断，已有任务会保留。恢复服务后请刷新并检查任务记录，再决定是否提交。'); }
  const data = await response.json();
  if (!response.ok) throw new Error(data.error?.message || '操作失败，请重试。');
  return data;
}
function applyProject(project, focusLast = false) {
  if (state.project && state.project.id !== project.id) return;
  const hadModel = !!selected()?.model;
  state.project = project;
  if (focusLast || !project.versions.some(v => v.id === state.selected)) {
    state.selected = project.activeVersionId || project.versions.at(-1)?.id || null;
    state.mode = 'design'; state.auxiliary = []; state.revisionOpen = false;
  } else if (!hadModel && selected()?.model) state.mode = 'model';
  render();
}
async function action(fn, focusLast = false) {
  if (state.pending || state.capturing || !state.project) return;
  state.pending = true; notice(''); render();
  try { applyProject(await fn(), focusLast); saved(); await refreshProjects(); }
  catch (e) { notice(e.message); }
  finally { state.pending = false; render(); }
}
async function refreshProjects() {
  const projects = await api('/api/projects');
  $('projectSelect').replaceChildren();
  projects.forEach(p => $('projectSelect').add(new Option(`${p.title} · ${p.versionCount} 版`, p.id)));
  if (state.project) $('projectSelect').value = state.project.id;
}
async function flushDraft() {
  clearTimeout(state.draftTimer);
  if (!state.draftDirty || !state.project) return;
  state.draftDirty = false;
  const projectId = state.project.id;
  try {
    const p = await api(`/api/projects/${projectId}`, 'PATCH', { title: $('projectTitle').value.trim() || '未命名物件', inputInstruction: $('prompt').value });
    if (state.project?.id === projectId) { applyProject(p); saved(); }
  } catch (e) { state.draftDirty = true; notice(e.message); saved('保存未完成'); }
}
async function openProject(id) {
  if (state.pending || state.capturing) return;
  const token = crypto.randomUUID(); state.openToken = token;
  state.loading = true; render();
  try {
    await flushDraft();
    const p = await api(`/api/projects/${id}`);
    if (state.openToken !== token) return;
    state.project = p; state.selected = p.activeVersionId || p.confirmedVersionId || p.versions.at(-1)?.id || null;
    state.mode = selected()?.designImageId ? 'design' : 'model';
    state.auxiliary = []; state.revisionOpen = false; state.draftDirty = false;
    localStorage.setItem('object-studio-project', id);
    $('projectTitle').value = p.title; $('prompt').value = p.inputInstruction || ''; $('revisionPrompt').value = '';
    $('thumbGrid').dataset.key = ''; notice(''); saved();
    await refreshProjects();
  } finally { if (state.openToken === token) { state.loading = false; render(); } }
}
function priceForImages(count) {
  const p = state.config?.prices || { qwenInputCny: .02, qwenOutputCny: .18 };
  return p.qwenOutputCny + p.qwenInputCny * count;
}
function renderCosts() {
  const n = state.project.activeImageIds.length, credits = state.config?.prices.aholoCredits ?? 1;
  $('generationCost').textContent = `Qwen · 首次生图预计 ${money(priceForImages(n))}（${n} 张输入），最终按实际用量计费`;
  $('revisionCost').textContent = `修改首图预计 ${money(priceForImages(1 + state.auxiliary.length))} · 最终按实际用量计费`;
  $('modelCost').textContent = selected()?.model ? '已有对应模型，查看与下载不产生费用。' : `Aholo G1-Turbo · 预计 ${credits} 积分 / 次 · 点击后才开始生成`;
  const tasks = state.project.tasks.filter(t => t.cost && (t.costRecorded || t.status === 'succeeded'));
  const sum = currency => tasks.filter(t => t.cost.currency === currency).reduce((v, t) => v + t.cost.amount, 0);
  $('projectQwenCost').textContent = `Qwen ${money(sum('CNY'))}`;
  $('projectAholoCost').textContent = `Aholo ${sum('CREDITS')} 积分`;
  const uncertain = state.project.tasks.some(t => t.costIncomplete || t.error?.code === 'SUBMISSION_UNKNOWN');
  const legacy = state.project.tasks.some(t => !t.cost);
  $('costFootnote').textContent = `Qwen 合计包含已记录的生图与检查用量估价；实际扣费以平台账单为准。${uncertain ? '另有计费待核对的请求，当前合计可能不完整。' : ''}${legacy ? '旧版任务没有费用记录，未计入。' : ''}`;
}
function renderImages() {
  const ids = state.project.activeImageIds;
  $('imageCount').textContent = `${ids.length} / 3`;
  const key = JSON.stringify(ids.map(id => [id, sourceImage(id)?.purpose])) + state.pending;
  const grid = $('thumbGrid');
  if (grid.dataset.key === key || grid.contains(document.activeElement)) return;
  grid.dataset.key = key; grid.replaceChildren();
  ids.forEach((id, index) => {
    const image = sourceImage(id), card = document.createElement('div'); card.className = 'reference-card';
    const img = document.createElement('img'); img.src = image.url; img.alt = `图 ${index + 1}：${image.purpose}`;
    const body = document.createElement('div'); body.className = 'reference-card-body';
    const line = document.createElement('div'); line.className = 'reference-card-title';
    const label = document.createElement('b'); label.textContent = `图 ${index + 1}`;
    const tools = document.createElement('span');
    for (const [symbol, title, disabled, callback] of [
      ['↑', `图 ${index + 1}向前`, index === 0, () => { const next = [...ids]; [next[index - 1], next[index]] = [next[index], next[index - 1]]; return api(endpoint('/images/order'), 'POST', { imageIds: next }); }],
      ['↓', `图 ${index + 1}向后`, index === ids.length - 1, () => { const next = [...ids]; [next[index + 1], next[index]] = [next[index], next[index + 1]]; return api(endpoint('/images/order'), 'POST', { imageIds: next }); }],
      ['×', `移除图 ${index + 1}`, false, () => api(endpoint(`/images/${id}`), 'DELETE')]
    ]) { const b = document.createElement('button'); b.textContent = symbol; b.setAttribute('aria-label', title); b.disabled = state.pending || disabled; b.onclick = () => action(callback); tools.append(b); }
    line.append(label, tools);
    const purpose = document.createElement('input'); purpose.value = image.purpose || '物件参考'; purpose.maxLength = 80; purpose.setAttribute('aria-label', `图 ${index + 1}用途`); purpose.placeholder = '例如：侧面结构、风格参考'; purpose.disabled = state.pending;
    purpose.onchange = () => action(() => api(endpoint(`/images/${id}`), 'PATCH', { purpose: purpose.value.trim() }));
    body.append(line, purpose); card.append(img, body); grid.append(card);
  });
}
function renderTask() {
  const p = state.project, active = p.tasks.find(t => ['queued', 'running'].includes(t.status));
  const failed = [...p.tasks].reverse().find(t => t.status === 'failed' && t.versionId === state.selected);
  const task = active || failed;
  $('taskBar').classList.toggle('hidden', !task); $('taskBar').classList.toggle('task-error', !active && !!failed);
  $('taskBar').querySelector('.spinner').classList.toggle('hidden', !active); $('retryBtn').classList.add('hidden');
  if (!task) return;
  const imageTask = task.kind === 'image-generation';
  const stages = { queued: '等待开始', uploading: '上传确认的设计图', submitting: imageTask ? '正在生成设计图' : '提交 3D 生成', checking: '正在整理设计图', polling: '正在生成 3D 模型', downloading: '保存结果文件', complete: '已保存' };
  $('taskTitle').textContent = active ? `v${p.versions.find(v => v.id === task.versionId)?.version} · ${imageTask ? 'Qwen' : 'Aholo'} · ${stages[task.stage] || '处理中'}` : '本次请求未完成';
  $('taskDetail').textContent = active ? (imageTask ? '仅生成一张设计图；完成后由你决定下一步。可刷新或暂时离开。' : '以选中的单主体设计图生成模型，其他版本会保留。') : task.error?.message || '请检查请求记录。';
  if (!active && failed?.canRetry) {
    const recover = failed.providerResultExpiresAt || failed.providerTaskId && ![4, 6].includes(failed.providerStatus);
    $('retryBtn').classList.remove('hidden'); $('retryBtn').disabled = state.pending;
    $('retryBtn').textContent = recover ? '恢复结果下载' : '重新提交';
    $('retryBtn').onclick = () => action(() => api(endpoint(`/tasks/${failed.id}/retry`), 'POST', { requestId: crypto.randomUUID() }));
  }
}
function chooseVersion(id) {
  state.selected = id; state.mode = selected()?.designImageId ? 'design' : 'model';
  state.revisionOpen = false; state.auxiliary = []; $('revisionPrompt').value = ''; render();
}
function renderVersions() {
  const list = $('versionList'); list.replaceChildren();
  $('versionCount').textContent = `${state.project.versions.length} 个版本`;
  if (!state.project.versions.length) { const p = document.createElement('p'); p.className = 'subtle'; p.textContent = '你的设计图、修改与对应模型会保留在这里。'; list.append(p); return; }
  for (const v of state.project.versions) {
    const b = document.createElement('button'); b.className = 'version-card' + (v.id === state.selected ? ' current' : ''); b.disabled = state.capturing || state.pending;
    const image = sourceImage(v.designImageId) || v.previewImage || sourceImage(v.sourceImageIds[0]);
    if (image) { const img = document.createElement('img'); img.src = image.url; img.alt = ''; b.append(img); }
    const span = document.createElement('span'), title = document.createElement('b'), detail = document.createElement('small');
    title.textContent = `v${v.version}${v.id === state.project.activeVersionId ? ' · 当前' : ''}`;
    detail.textContent = labels[v.state] || v.state; span.append(title, detail); b.append(span);
    b.title = v.userInstruction || '原物件设计'; b.onclick = () => chooseVersion(v.id); list.append(b);
  }
}
function renderRecord(v) {
  $('requestDetails').classList.toggle('hidden', !v);
  if (!v) return;
  const box = $('versionMeta'); box.replaceChildren();
  const entries = [`v${v.version}${v.parentVersionId ? ' · 由 v' + state.project.versions.find(i => i.id === v.parentVersionId)?.version + ' 继续修改' : ''}`];
  (v.references || []).forEach((r, i) => entries.push(`图 ${i + 1}：${r.purpose}`));
  state.project.tasks.filter(t => t.versionId === v.id).forEach(t => {
    const cost = t.cost ? (t.cost.currency === 'CNY' ? money(t.cost.amount) : `${t.cost.amount} 积分`) + '（估算）' : '无费用记录';
    entries.push(`${t.provider} / ${t.providerVersion || '旧版模型'} · ${t.status} · ${cost}`);
    if (t.providerRequestId || t.providerTaskId) entries.push(`平台请求：${t.providerRequestId || t.providerTaskId}`);
  });
  entries.forEach(text => { const p = document.createElement('p'); p.textContent = text; box.append(p); });
  $('actualPrompt').textContent = (v.actualPrompt || v.userInstruction || '此早期资产没有统一美术指令记录。')
    + (v.negativePrompt ? '\n\n【负向提示词】\n' + v.negativePrompt : '');
  if (v.promptSettingsRevision !== undefined) { const p = document.createElement('p'); p.textContent = `提示词配置：${v.promptSettingsRevision ? '#' + v.promptSettingsRevision : '推荐默认值'} · ${v.artRulesVersion}`; box.append(p); }
}
function renderAuxiliarySummary() {
  const box = $('auxiliarySummary'); box.replaceChildren();
  state.auxiliary.forEach((ref, i) => {
    const chip = document.createElement('span'), img = document.createElement('img'), text = document.createElement('small');
    img.src = sourceImage(ref.imageId).url; img.alt = ''; text.textContent = `图 ${i + 2} · ${ref.purpose}`; chip.append(img, text); box.append(chip);
  });
  $('auxiliaryCount').textContent = `${state.auxiliary.length} / 2`;
}
function render() {
  if (!state.project) return;
  const v = selected(), busy = isBusy(), locked = state.pending || state.capturing || state.loading, design = sourceImage(v?.designImageId), model = v?.model;
  $('promptSettingsBtn').setAttribute('aria-disabled', String(locked));
  $('newProject').disabled = locked; $('projectSelect').disabled = locked;
  $('projectTitle').disabled = locked; $('prompt').disabled = locked; $('revisionPrompt').disabled = locked;
  $('generateBtn').disabled = locked || busy || (!$('prompt').value.trim() && !state.project.activeImageIds.length);
  $('fileInput').disabled = locked || state.project.activeImageIds.length >= 3;
  $('connectionHint').textContent = state.config?.provider === 'mock' ? '模拟模式 · 不调用付费接口' : `Qwen ${state.config?.imageService.configured ? '已配置' : '待配置'} · Aholo ${state.config?.modelService.configured ? '已配置' : '待配置'}`;
  $('compareBtn').disabled = locked || (state.project.versions.filter(v => v.designImageId).length < 2 && state.project.versions.filter(v => v.model).length < 2);
  $('restoreBtn').disabled = locked || !v || v.id === state.project.activeVersionId || (!design && !model);
  $('confirmBtn').disabled = locked || !model || state.project.confirmedVersionId === v?.id;
  $('confirmBtn').textContent = model && state.project.confirmedVersionId === v?.id ? '已定稿 ✓' : '定稿模型';
  renderImages(); renderTask(); renderVersions(); renderCosts(); renderRecord(v); renderAuxiliarySummary();
  if (!design && model) state.mode = 'model'; if (!model && design) state.mode = 'design';
  const showModel = !!model && state.mode === 'model', showDesign = !!design && state.mode === 'design';
  $('modelTab').disabled = !model || locked; $('designTab').disabled = !design || locked;
  $('modelTab').classList.toggle('active', state.mode === 'model'); $('designTab').classList.toggle('active', state.mode === 'design');
  $('versionLabel').textContent = v ? `v${v.version} · ${labels[v.state]}` : '';
  viewer.classList.toggle('hidden', !showModel); $('designZoomTarget').classList.toggle('hidden', !showDesign); $('emptyState').classList.toggle('hidden', showModel || showDesign);
  if (showDesign && $('designImage').getAttribute('src') !== design.url) $('designImage').src = design.url;
  if (showModel && state.lastModel !== model.url) {
    state.lastModel = model.url; state.loaded = false; viewer.dataset.loaded = 'false'; viewer.setAttribute('src', model.url); viewer.cameraOrbit = '45deg 65deg auto';
  }
  $('emptyTitle').textContent = busy ? '正在让想法成形' : '先让一个想法成形';
  $('emptyDetail').textContent = busy ? '任务与输入已保存。完成后会显示结果，你可以刷新或暂时离开。' : '写下灵感，或上传一件喜欢的物品。这里会出现它的第一张设计图。';
  $('assetBadge').classList.toggle('hidden', !showModel && !showDesign); $('assetBadge').textContent = showDesign ? '单主体设计图' : '设计图对应的 3D';
  $('modelTools').classList.toggle('hidden', !showModel);
  document.querySelectorAll('[data-orbit],#resetView,#fourViewsBtn').forEach(b => { b.disabled = !showModel || !state.loaded || locked; });
  $('downloadModel').classList.toggle('hidden', !model);
  if (model) { $('downloadModel').href = model.url; $('downloadModel').download = `${state.project.title}-v${v.version}.glb`; }
  $('downloadDesign').classList.toggle('hidden', !design);
  if (design) { $('downloadDesign').href = design.url; $('downloadDesign').download = `${state.project.title}-v${v.version}-design.${design.format}`; }
  $('modelHint').textContent = showDesign ? '点击图片放大查看 · 旧版始终保留' : showModel ? (state.loaded ? '拖动旋转 · 滚轮缩放 · 视角基于模型坐标系' : '正在加载 3D 模型…') : '';
  $('modelOrigin').classList.toggle('hidden', !showModel);
  $('modelOriginText').textContent = design ? `模型来源：v${v.version} 的设计图 · ${design.id.slice(0, 8)}` : '早期照片生成资产，没有对应的设计图记录。';
  $('showSourceDesign').classList.toggle('hidden', !design);
  $('designApproval').classList.toggle('hidden', !design || v?.state === 'discarded');
  $('designActionTitle').textContent = model ? '这张设计图，已有对应模型。' : '满意这张设计了吗？';
  const needsReview = state.project.tasks.some(t => t.id === v?.designTaskId && t.needsReview);
  $('designActionNote').textContent = model ? `v${v.version} 的设计图与模型已关联保存，也可以继续创作新设计版本。` : needsReview ? '请仔细检查主体、风格与画面内容，仍可能需要调整。可继续修改，或确认这张图生成 3D。' : '确认图中只有一个完整主体。继续打磨，或将这张图生成 3D。';
  $('continueBtn').disabled = locked || busy;
  $('approveDesignBtn').textContent = model ? '查看对应 3D' : v?.modelTaskId ? '模型任务未完成' : '用这张图生成 3D';
  $('approveDesignBtn').disabled = locked || busy || (!model && !!v?.modelTaskId);
  $('revisionBox').classList.toggle('hidden', !design || !state.revisionOpen);
  $('revisionBase').textContent = v ? `图 1：当前选中的 v${v.version} 设计图，是本轮修改的主体基础。` : '';
  $('reviseBtn').disabled = locked || busy || !$('revisionPrompt').value.trim(); $('auxiliaryBtn').disabled = locked || busy;
  $('handoff').classList.toggle('hidden', !state.project.confirmedVersionId);
  if (state.project.confirmedVersionId) $('handoffNext').href = `/anthro.html?projectId=${encodeURIComponent(state.project.id)}`;
  $('anthroPhaseLink').href = `/anthro.html?projectId=${encodeURIComponent(state.project.id)}`;
  const final = state.project.versions.find(v => v.id === state.project.confirmedVersionId);
  $('handoffText').textContent = final ? `已保存「${state.project.title}」v${final.version} 的${final.designImageId ? '设计图与模型' : '早期模型'}，仍可恢复历史版本继续创作。` : '';
}
function fileBase64(file) {
  return new Promise((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(reader.result.split(',')[1]); reader.onerror = () => reject(new Error('读取图片失败。')); reader.readAsDataURL(file); });
}
async function addFiles(files, auxiliary = false) {
  if (state.pending || !state.project) return;
  const picked = Array.from(files);
  if (!picked.length) return;
  if ((auxiliary ? state.auxiliary.length : state.project.activeImageIds.length) + picked.length > (auxiliary ? 2 : 3)) { notice(auxiliary ? '最多额外选择 2 张辅助图。' : '最多 3 张参考图。'); return; }
  for (const file of picked) {
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) { notice('仅支持 JPG、PNG 或 WebP。'); return; }
    if (file.size > 10485760) { notice('每张照片最多 10 MB。'); return; }
  }
  await action(async () => {
    let p = state.project;
    for (const file of picked) {
      p = await api(`/api/projects/${p.id}/images`, 'POST', { fileName: file.name, data: await fileBase64(file), scope: auxiliary ? 'auxiliary' : 'initial', purpose: auxiliary ? '辅助参考' : undefined });
      if (auxiliary) state.auxiliary.push({ imageId: p.sourceImages.at(-1).id, purpose: '辅助参考' });
    }
    return p;
  });
  $('fileInput').value = ''; $('auxiliaryInput').value = '';
  if (auxiliary) renderReferencePicker();
}
$('fileInput').onchange = e => addFiles(e.target.files);
$('auxiliaryInput').onchange = e => addFiles(e.target.files, true);
for (const event of ['dragover', 'dragenter']) $('dropzone').addEventListener(event, e => { e.preventDefault(); $('dropzone').classList.add('dragover'); });
for (const event of ['dragleave', 'drop']) $('dropzone').addEventListener(event, e => { e.preventDefault(); $('dropzone').classList.remove('dragover'); });
$('dropzone').addEventListener('drop', e => addFiles(e.dataTransfer.files));
$('generateBtn').onclick = () => action(() => api(endpoint('/generate'), 'POST', {
  requestId: crypto.randomUUID(), instruction: $('prompt').value.trim(),
  references: state.project.activeImageIds.map(id => ({ imageId: id, purpose: sourceImage(id).purpose }))
}), true);
$('continueBtn').onclick = () => { state.revisionOpen = true; render(); $('revisionBox').scrollIntoView({ behavior: 'smooth', block: 'center' }); $('revisionPrompt').focus({ preventScroll: true }); };
$('approveDesignBtn').onclick = () => {
  if (selected()?.model) { state.mode = 'model'; render(); return; }
  action(() => api(endpoint(`/versions/${state.selected}/generate`), 'POST', { requestId: crypto.randomUUID() }));
};
$('reviseBtn').onclick = () => action(async () => {
  const p = await api(endpoint('/revisions'), 'POST', { requestId: crypto.randomUUID(), baseVersionId: state.selected, instruction: $('revisionPrompt').value.trim(), auxiliaryReferences: state.auxiliary });
  $('revisionPrompt').value = ''; return p;
}, true);
$('revisionPrompt').oninput = () => { $('reviseBtn').disabled = state.pending || isBusy() || !$('revisionPrompt').value.trim(); };
 $('confirmBtn').onclick = async () => {
  await action(() => api(endpoint(`/versions/${state.selected}/confirm`), 'POST', {}));
  if (state.project?.confirmedVersionId === state.selected) location.href = `/anthro.html?projectId=${encodeURIComponent(state.project.id)}`;
 };
$('restoreBtn').onclick = () => action(() => api(endpoint(''), 'PATCH', { activeVersionId: state.selected }));
$('newProject').onclick = async () => {
  if (state.pending || state.capturing) return;
  await flushDraft(); state.pending = true; render();
  try { const p = await api('/api/projects', 'POST', { title: '新的物件' }); state.pending = false; await openProject(p.id); }
  catch (e) { notice(e.message); } finally { state.pending = false; render(); }
};
$('projectSelect').onchange = e => openProject(e.target.value).catch(e => notice(e.message));
for (const id of ['projectTitle', 'prompt']) $(id).addEventListener('input', () => {
  state.draftDirty = true; saved('正在保存…');
  if (id === 'prompt') $('generateBtn').disabled = state.pending || isBusy() || (!$('prompt').value.trim() && !state.project.activeImageIds.length);
  clearTimeout(state.draftTimer); state.draftTimer = setTimeout(flushDraft, 500);
});
$('modelTab').onclick = () => { state.mode = 'model'; render(); };
$('designTab').onclick = $('showSourceDesign').onclick = () => { state.mode = 'design'; render(); };
document.querySelectorAll('[data-orbit]').forEach(b => b.onclick = () => { viewer.cameraOrbit = b.dataset.orbit; });
$('resetView').onclick = () => { viewer.cameraOrbit = '45deg 65deg auto'; viewer.cameraTarget = 'auto auto auto'; viewer.fieldOfView = 'auto'; };
viewer.addEventListener('load', async () => {
  const v = selected(), projectId = state.project?.id;
  if (!v || viewer.getAttribute('src') !== v.model?.url) return;
  state.loaded = true; viewer.dataset.loaded = 'true'; render();
  if (v.previewImage) return;
  try {
    await new Promise(resolve => setTimeout(resolve, 500));
    if (state.project?.id !== projectId || state.selected !== v.id || state.capturing) return;
    const blob = await viewer.toBlob({ mimeType: 'image/png', idealAspect: true });
    const p = await api(`/api/projects/${projectId}/versions/${v.id}/preview`, 'POST', { data: await fileBase64(blob) });
    if (state.project?.id === projectId) applyProject(p);
  } catch { /* An optional thumbnail must never block a valid model. */ }
});
viewer.addEventListener('error', () => { state.loaded = false; viewer.dataset.loaded = 'false'; notice('模型加载失败，文件已保存。可下载检查或刷新。'); });
function renderReferencePicker() {
  const grid = $('referencePicker'); grid.replaceChildren();
  const candidates = state.project.sourceImages.filter(i => i.id !== selected()?.designImageId && ['upload', 'generated-design'].includes(i.origin));
  candidates.forEach(image => {
    const card = document.createElement('div'); card.className = 'reference-choice';
    const label = document.createElement('label'), checkbox = document.createElement('input'), img = document.createElement('img'), name = document.createElement('span');
    checkbox.type = 'checkbox'; checkbox.value = image.id; checkbox.checked = state.auxiliary.some(r => r.imageId === image.id); checkbox.disabled = !checkbox.checked && state.auxiliary.length >= 2;
    checkbox.setAttribute('aria-label', `选择辅助图 ${image.fileName}`); img.src = image.url; img.alt = ''; name.textContent = image.fileName;
    const purpose = document.createElement('input'); purpose.type = 'text'; purpose.maxLength = 80; purpose.value = state.auxiliary.find(r => r.imageId === image.id)?.purpose || image.purpose || '辅助参考'; purpose.setAttribute('aria-label', `辅助用途 ${image.fileName}`);
    checkbox.onchange = () => {
      if (checkbox.checked && state.auxiliary.length < 2) state.auxiliary.push({ imageId: image.id, purpose: purpose.value.trim() || '辅助参考' });
      else state.auxiliary = state.auxiliary.filter(r => r.imageId !== image.id);
      renderReferencePicker(); renderAuxiliarySummary(); renderCosts();
    };
    purpose.oninput = () => { const ref = state.auxiliary.find(r => r.imageId === image.id); if (ref) { ref.purpose = purpose.value.trim() || '辅助参考'; renderAuxiliarySummary(); } };
    label.append(checkbox, img, name); card.append(label, purpose); grid.append(card);
  });
  if (!candidates.length) { const p = document.createElement('p'); p.className = 'subtle'; p.textContent = '还没有其他参考图，可以上传辅助图，也可以直接修改。'; grid.append(p); }
  $('auxiliaryInput').disabled = state.pending || state.auxiliary.length >= 2;
}
$('auxiliaryBtn').onclick = () => { renderReferencePicker(); $('referencesDialog').showModal(); };
$('doneReferences').onclick = () => { $('referencesDialog').close(); render(); };
function compareRender() {
  for (const side of ['Left', 'Right']) {
    const v = state.project.versions.find(v => v.id === $('compare' + side).value), model = $('compareViewer' + side), img = $('compareImage' + side);
    const showModel = state.compareMode === 'model' && !!v?.model;
    model.classList.toggle('hidden', !showModel); img.classList.toggle('hidden', showModel || !v?.designImageId);
    if (showModel) { model.setAttribute('src', v.model.url); model.cameraOrbit = '45deg 65deg auto'; }
    else if (v?.designImageId) img.src = sourceImage(v.designImageId).url;
  }
}
function setupComparison(mode) {
  state.compareMode = mode;
  const versions = state.project.versions.filter(v => mode === 'model' ? v.model : v.designImageId);
  $('compareDesignMode').classList.toggle('active', mode === 'design'); $('compareModelMode').classList.toggle('active', mode === 'model');
  for (const id of ['compareLeft', 'compareRight']) { $(id).replaceChildren(); versions.forEach(v => $(id).add(new Option(`v${v.version} · ${labels[v.state]}`, v.id))); }
  const current = versions.find(v => v.id === state.selected) || versions.at(-1);
  if (current) { $('compareLeft').value = versions.find(v => v.id !== current.id)?.id || versions[0].id; $('compareRight').value = current.id; }
  compareRender();
}
$('compareBtn').onclick = () => {
  const p = state.project;
  $('compareDesignMode').disabled = p.versions.filter(v => v.designImageId).length < 2;
  $('compareModelMode').disabled = p.versions.filter(v => v.model).length < 2;
  setupComparison($('compareDesignMode').disabled ? 'model' : 'design'); $('compareDialog').showModal();
};
$('compareDesignMode').onclick = () => setupComparison('design'); $('compareModelMode').onclick = () => setupComparison('model');
$('compareLeft').onchange = $('compareRight').onchange = compareRender;
$('designZoomTarget').onclick = () => {
  $('zoomImage').src = sourceImage(selected().designImageId).url;
  $('zoomTitle').textContent = `v${selected().version} · 设计图细节`; $('zoomDialog').showModal(); setZoom(1);
};
function setZoom(scale) {
  const area = document.querySelector('.zoom-scroll');
  $('zoomImage').style.width = `${Math.floor(Math.min(area.clientWidth - 16, area.clientHeight - 16) * scale)}px`;
}
document.querySelectorAll('[data-zoom]').forEach(b => b.onclick = () => setZoom(Number(b.dataset.zoom)));
document.querySelectorAll('[data-close]').forEach(b => b.onclick = () => $(b.dataset.close).close());
$('fourViewsBtn').onclick = async () => {
  if (!state.loaded || state.capturing) return;
  state.capturing = true; render(); const original = viewer.cameraOrbit, decay = viewer.interpolationDecay;
  try {
    $('fourViewsGrid').replaceChildren(); viewer.interpolationDecay = 0;
    for (const [name, orbit] of [['正面', '0deg 75deg auto'], ['侧面', '90deg 75deg auto'], ['背面', '180deg 75deg auto'], ['四分之三', '45deg 65deg auto']]) {
      viewer.cameraOrbit = orbit; viewer.jumpCameraToGoal(); await new Promise(resolve => setTimeout(resolve, 180));
      const blob = await viewer.toBlob({ mimeType: 'image/png', idealAspect: true });
      const fig = document.createElement('figure'), img = document.createElement('img'), cap = document.createElement('figcaption');
      img.src = 'data:image/png;base64,' + await fileBase64(blob); img.alt = name; cap.textContent = name; fig.append(img, cap); $('fourViewsGrid').append(fig);
    }
    $('fourViewsDialog').showModal();
  } catch { notice('视图渲染失败，请确认模型加载后重试。'); }
  finally { viewer.cameraOrbit = original; viewer.interpolationDecay = decay; state.capturing = false; render(); }
};
$('promptSettingsBtn').onclick = async event => {
  event.preventDefault();
  if (state.pending || state.capturing || state.loading) return;
  await flushDraft();
  if (state.draftDirty) return;
  if (state.project) sessionStorage.setItem('object-studio-settings-return', JSON.stringify({ projectId: state.project.id, versionId: state.selected,
    revisionText: $('revisionPrompt').value, revisionOpen: state.revisionOpen, auxiliary: state.auxiliary }));
  location.href = '/settings.html';
};
async function boot() {
  try {
    await customElements.whenDefined('model-viewer');
    state.config = await api('/api/config'); $('modeBadge').textContent = state.config.provider === 'mock' ? '模拟测试模式' : 'Qwen 设计 / Aholo 3D';
    const projects = await api('/api/projects'), last = localStorage.getItem('object-studio-project');
    const p = projects.find(p => p.id === last) || projects[0] || await api('/api/projects', 'POST', { title: '我的第一个物件' });
    await openProject(p.id);
    const back = JSON.parse(sessionStorage.getItem('object-studio-settings-return') || 'null');
    if (back?.projectId === p.id) {
      if (state.project.versions.some(v => v.id === back.versionId)) chooseVersion(back.versionId);
      $('revisionPrompt').value = back.revisionText || ''; state.revisionOpen = !!back.revisionOpen;
      state.auxiliary = (back.auxiliary || []).filter(r => sourceImage(r.imageId)); render();
    }
    sessionStorage.removeItem('object-studio-settings-return');
    document.body.dataset.ready = 'true';
  } catch (e) { notice(e.message); saved('连接未完成'); }
}
setInterval(async () => {
  if (!state.project || state.pending || state.capturing) return;
  const id = state.project.id;
  try { const p = await api(`/api/projects/${id}`); if (state.project?.id === id && !state.pending) applyProject(p); }
  catch { saved('暂时离线 · 已保存的任务仍保留'); }
}, 2000);
boot();
