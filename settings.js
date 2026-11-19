const $ = id => document.getElementById(id);
const keys = ['style', 'composition', 'textDesign', 'referenceDesign', 'revision', 'defaultIdea', 'negative'];
const anthroKeys = ['style', 'base', 'revision', 'composition', 'defaultIdea', 'negative'];
let active, recommended, dirty = false, saving = false, previewTimer, previewSequence = 0;
const fields = () => Object.fromEntries(keys.map(key => [key, $(key).value]));
const anthro = () => Object.fromEntries(anthroKeys.map(key => [key, $('anthro' + key[0].toUpperCase() + key.slice(1)).value]));
const agentKeys = { model: 'agentModel', apiUrl: 'agentApiUrl', maxCorrections: 'agentMaxCorrections', timeoutSeconds: 'agentTimeoutSeconds', instruction: 'agentInstruction', repairInstruction: 'agentRepairInstruction', inputCnyPerMillion: 'agentInputPrice', outputCnyPerMillion: 'agentOutputPrice' };
const numeric = new Set(['maxCorrections', 'timeoutSeconds', 'inputCnyPerMillion', 'outputCnyPerMillion']);
const agent = () => Object.fromEntries(Object.entries(agentKeys).map(([key, id]) => [key, numeric.has(key) ? Number($(id).value) : $(id).value]));
function fillAgent(value) { Object.entries(agentKeys).forEach(([key, id]) => { $(id).value = value[key]; }); }
function notice(message = '') { $('settingsNotice').textContent = message; $('settingsNotice').classList.toggle('hidden', !message); }
async function api(path, method = 'GET', body) {
  const response = await fetch(path, { method, headers: body ? { 'Content-Type': 'application/json' } : {}, body: body ? JSON.stringify(body) : undefined });
  const data = await response.json();
  if (!response.ok) throw new Error(data.error?.message || '配置操作失败。');
  return data;
}
function status() {
  $('activeRevision').textContent = active?.revision ? `当前生效 · 配置 #${active.revision}` : '当前生效 · 推荐默认值';
  $('dirtyState').textContent = saving ? '正在保存…' : dirty ? '有未应用的修改' : '配置已应用';
  $('savePrompts').disabled = !active || saving || !dirty;
  $('resetPrompts').disabled = !active || saving;
  $('refreshPreview').disabled = !active || saving;
  $('promptFields').disabled = !active || saving;
  $('agentFields').disabled = !active || saving;
}
function fill(value) { keys.forEach(key => { $(key).value = value[key]; }); }
function fillAnthro(value) { anthroKeys.forEach(key => { $('anthro' + key[0].toUpperCase() + key.slice(1)).value = value[key]; }); }
function markDirty() { dirty = JSON.stringify(fields()) !== JSON.stringify(Object.fromEntries(keys.map(key => [key, active.fields[key]]))) || JSON.stringify(anthro()) !== JSON.stringify(active.anthro) || Object.keys(agentKeys).some(key => agent()[key] !== active.agent[key]); status(); }
async function preview() {
  clearTimeout(previewTimer);
  if (!active) return;
  const sequence = ++previewSequence, stage = $('previewStage').value;
  $('previewReferences').classList.toggle('hidden', stage === 'textDesign');
  $('previewStatus').textContent = '正在拼接…';
  try {
    const result = await api('/api/prompt-settings/preview', 'POST', { fields: fields(), anthro: anthro(), stage, instruction: $('previewIdea').value,
      purposes: $('previewPurposes').value.split('\n').map(s => s.trim()).filter(Boolean) });
    if (sequence !== previewSequence) return;
    $('composedPreview').textContent = result.prompt; $('negativePreview').textContent = result.negativePrompt || '（空）';
    $('previewStatus').textContent = '已按当前编辑更新 · 不产生费用';
  } catch (e) { if (sequence === previewSequence) $('previewStatus').textContent = '预览未更新：' + e.message; }
}
function schedulePreview() { ++previewSequence; clearTimeout(previewTimer); $('previewStatus').textContent = '编辑中，等待更新…'; previewTimer = setTimeout(preview, 450); }
keys.forEach(key => $(key).addEventListener('input', () => { markDirty(); schedulePreview(); }));
anthroKeys.forEach(key => $('anthro' + key[0].toUpperCase() + key.slice(1)).addEventListener('input', markDirty));
Object.values(agentKeys).forEach(id => $(id).addEventListener('input', markDirty));
['previewStage', 'previewIdea', 'previewPurposes'].forEach(key => $(key).addEventListener('input', schedulePreview));
$('refreshPreview').onclick = preview;
$('resetPrompts').onclick = () => { fill(recommended.fields); fillAnthro(recommended.anthro); fillAgent(recommended.agent); markDirty(); notice('已填入推荐默认值，点击“保存并应用”后生效。'); preview(); };
$('settingsForm').onsubmit = async event => {
  event.preventDefault();
  if (!active || saving || !dirty) return;
  saving = true; notice(); status();
  try {
    active = await api('/api/prompt-settings', 'PATCH', { expectedRevision: active.revision, fields: fields(), anthro: anthro(), agent: agent() });
    dirty = false; fill(active.fields); fillAnthro(active.anthro); fillAgent(active.agent); notice(`配置 #${active.revision} 已保存并应用，后续新任务立即使用。历史任务保留提交时的配置。`); await preview();
  } catch (e) { notice(e.message || '连接中断，请刷新检查配置是否已保存。'); }
  finally { saving = false; status(); }
};
window.addEventListener('beforeunload', event => { if (dirty) { event.preventDefault(); event.returnValue = ''; } });
(async () => {
  try { const result = await api('/api/prompt-settings'); active = result.active; recommended = result.defaults; fill(active.fields); fillAnthro(active.anthro); fillAgent(active.agent); $('agentConnection').textContent = result.agentConfigured ? '服务端 Agent 密钥已配置' : '服务端 Agent 密钥未配置'; status(); await preview(); }
  catch { notice('无法读取配置，请确认本地服务已启动后刷新。'); }
})();
