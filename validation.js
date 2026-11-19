const viewer = document.getElementById('viewer');
const statusText = document.getElementById('modelStatus');
let currentUrl = '';
let loaded = false;
viewer.addEventListener('load', () => {
  loaded = true;
  statusText.textContent = '真实 GLB 已加载。拖动旋转、滚轮缩放，或切换检查视角。';
  viewer.dataset.verifiedLoaded = 'true';
});
viewer.addEventListener('error', () => {
  statusText.textContent = '模型加载失败，请检查模型格式、浏览器控制台或 WebGL 支持。';
  viewer.dataset.verifiedLoaded = 'false';
});
function selectModel(file, run) {
  currentUrl = file.url;
  loaded = false;
  viewer.dataset.verifiedLoaded = 'false';
  viewer.src = file.url;
  document.getElementById('modelTitle').textContent = run.label || `真实生成 · 任务 ${run.taskId}`;
  statusText.textContent = '正在加载本地 GLB…';
}
async function refresh() {
  try {
    const response = await fetch('/api/validation');
    if (!response.ok) throw new Error('status');
    const runs = await response.json();
    const list = document.getElementById('taskList');
    list.replaceChildren();
    for (const run of runs) {
      const row = document.createElement('div');
      const names = {'img-to-3d':'图转 3D','multimodal-to-image':'文字改图'};
      const states = {uploading:'上传中',submitting:'提交中',running:'生成中',succeeded:'已保存',failed:'失败',cancelled:'取消'};
      row.textContent = `${names[run.kind] || run.kind} · ${run.taskId || '待提交'} · ${states[run.status] || run.status}`;
      list.appendChild(row);
    }
    const models = runs.flatMap(run => run.files.filter(f => f.format === 'glb').map(file => ({run,file})));
    if (!currentUrl && models.length) selectModel(models[0].file, models[0].run);
    const choices = document.getElementById('modelChoices');
    choices.replaceChildren();
    for (const {run,file} of models) {
      const button = document.createElement('button');
      button.textContent = run.label || `查看任务 ${run.taskId}`;
      button.onclick = () => selectModel(file,run);
      choices.appendChild(button);
    }
    const imagePanel = document.getElementById('imageResults');
    const images = runs.flatMap(run => run.files.filter(f => ['png','jpg','jpeg','webp'].includes(f.format)));
    for (const file of images) {
      if (imagePanel.querySelector(`img[src="${file.url}"]`)) continue;
      const label = document.createElement('p');
      label.textContent = '文字修改生成的新设计图（不是模型渲染图）';
      const img = document.createElement('img');
      img.src = file.url;
      img.alt = '按文字要求生成的新设计图';
      imagePanel.append(label,img);
    }
  } catch {
    if (!loaded) statusText.textContent = '无法读取本地任务记录，请确认预览服务正在运行。';
  }
}
document.querySelectorAll('[data-orbit]').forEach(button => button.onclick = () => {viewer.cameraOrbit = button.dataset.orbit;});
document.getElementById('rotate').onclick = () => {viewer.autoRotate = !viewer.autoRotate;};
refresh();
setInterval(refresh, 5000);
