# 捡个星球慢慢造

一款 Unity 小世界原型：照料家园、领取一次性宝箱、挑战土地守卫、访问 NPC 邻居，并与向导“小禾”讨论和布置新土地。提交版为 **Unity WebGL + Python 本地服务**，通过同一地址提供网页、游戏与 API。

## 解压后启动

需要 macOS / Linux、Python 3.10+ 和支持 WebGL 2 的桌面浏览器。浏览已构建的游戏不需要安装 Unity。请在解压后的 `SmallPlanetSubmission` 目录执行：

```sh
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements-submission.txt
.venv/bin/python tools/start_submission.py
```

打开 **http://127.0.0.1:8791/**，点击入口进入 `/play/`。不要直接双击 WebGL 的 `index.html`。启动器会从随包的干净素材建立 19 项资产目录；不会导入作者的存档、聊天或创作记录。默认使用 `live` 真实服务模式，入口会显示密钥配置状态；未配置密钥时，相应真实对话和生成不可用。基础游戏资源已在 WebGL 内。

端口可用 `--port 8792` 修改；在服务器终端按 `Ctrl+C` 关闭。无浏览器自动弹出可加 `--no-browser`。游戏状态存在该浏览器的本地存储，后端档案、聊天和新资产写入 `data/submission-live/`。换浏览器不会自动同步本地游戏进度。

仅需要不调用付费接口的测试时，显式运行 `.venv/bin/python tools/start_submission.py --mode mock`。此模式使用确定性对话、测试概念图和测试模型，入口会标记模拟模式，不能当作真实共创能力展示。

快速验收路线：先进入完整家园展示，和小禾约定“仅用已有物件摆放长凳或白花”，查看预览并应用，再刷新页面检查保存结果。第一次新旅程与完整展示使用独立存档，可以分别体验开拓流程和现有家园。真实小禾对话需要配置对话密钥。

页面顶部的“录屏”可保存实际游戏画面，单段最长 5 分钟、无音轨；停止后点击“下载录屏”。刷新前请先下载，视频不会自动上传。游戏标题中的“玩法说明”介绍操作与服务配置，不会跳转为预录演示。

## 使用真实小禾与生成服务

```sh
cp .env.example .env.local
# 在本机编辑 .env.local，填入自己账号的密钥，再运行：
.venv/bin/python tools/start_submission.py --mode live
```

| 配置 | 实际用途 |
| --- | --- |
| `QWEN_AGENT_API_KEY` | 小禾对话、需求整理、资产拆分、布局建议和视觉检查；默认 `qwen3.8-flash` |
| `TOAPIS_API_KEY`、`TOAPIS_BASE_URL` | 家园概念图和资产设计图，实际调用 `gpt-image-2` |
| `AHOLO_API_KEY`、`AHOLO_REGION` | Aholo 图生 3D；国内账号 `cn`，国际账号 `com` |
| `QWEN_API_KEY` | 旧物件设计工作台的可选配置；也是未配置 Agent 独立密钥时的回退值 |

Agent 模型、接口、预算及提示词可在 `/home-creation-settings.html` 设置；小禾设置在 `/game-settings.html`。服务端同时支持同名环境变量，优先于 `.env.local`。密钥仅保存在 Python 服务端，不应放入 Unity、WebGL、网页或提交包。提交包中的模板没有任何可用密钥。

`live` 使用独立的 `data/submission-live/`，不和 `mock` 任务混用。发送真实小禾消息会调用模型并可能计费；概念图和新 3D 资产需按界面确认。已有资源摆放可以复用本地目录。人民币、美元和 Aholo 积分分别记录，估算与实际结算可能不同，实际可用性取决于账号权限、余额和服务返回。初始化、打包和干净目录验收均不调用这些付费接口。

新生成 GLB 的归一化、LOD 和预览制作需要本机 Blender。编辑器装配、目录刷新及完整工程验收需要 Unity 编辑器和对应工具桥接；这些作者工具不会在浏览器内执行。原包装脚本 `tools/blender`、`tools/unity` 使用 macOS 的 `/Applications` 安装位置，换平台或安装位置需调整。

## 实现范围

小禾真实服务接入游戏状态快照、有限动作协议、显式偏好与共同经历；任务系统支持概念确认、制作批次确认、进度恢复及最终应用确认。模型建议经后端约束校验，不能直接发放奖励或改写游戏事实。

真实家园编辑针对玩家首个相邻地块，支持目录资产复用、平面移动、Y 轴旋转、受限缩放和颜色，校验土地边界和入口通路，并保留预览与应用步骤。完整家园展示保留天文台等已制作内容；地形连接使用已有预设和柔化档位。当前没有任意代码生成、整张地图任意重塑或任意新增玩法。

邻居是 **NPC**，有本地进度和有限互动，尚未接入真人在线多人服务。工程中的故事演示、展示和录屏流程采用预设对白、时序与镜头；录屏不能代表真实 Agent 当时实时完成了规划、建模和全部游戏动作。`mock` 演示也不能视为真实模型能力验收。详细边界和素材来源见 [docs/submission/BOUNDARIES.md](docs/submission/BOUNDARIES.md)。

## 干净目录验收

建议在刚解压、尚未创建 `.env.local` 和本地存档时运行：

```sh
.venv/bin/python tools/check_submission.py
```

脚本核对随包清单与哈希、排除文件、19 项种子资产及其源文件依赖，在临时目录创建全新数据库，并启动 `mock` 服务检查 API、正式入口、`/play/`、封面、构建元数据及四类 WebGL 资源的 HTTP 头。它不会读取原作者数据库，也不会调用付费服务。这里只验证文件、资产完整性和本地 HTTP 连通性；WebGL 实际渲染、操作和实时服务效果需用浏览器体验。

若只需要后端，可手动初始化并运行：

```sh
.venv/bin/python tools/init_submission_data.py --data-dir data/custom-mock
.venv/bin/python home_server.py --mode mock --port 8791 --data-dir data/custom-mock
```

初始化拒绝覆盖已存在数据库；换 `--data-dir` 即可得到新的本地档案。当前服务是本地原型，绑定 `127.0.0.1`，设置页尚未提供面向公网的管理员鉴权。

## Unity 源码与重新构建

Unity 工程位于 `unity/DawnGarden/`，版本 **6000.3.27f1**。随包保留 `Assets/`、`Packages/`、`ProjectSettings/`，排除 `Library/`、日志、自动恢复及用户设置。使用 Unity Hub 安装该编辑器和 WebGL Build Support 后打开工程，等待包解析与导入。

三个资产目录校验需要的精确版本包已嵌入 `Packages/`，其他依赖由包清单和锁文件解析。WebGL 构建入口为编辑器方法 `DawnGarden.Editor.SmallWorldWebGLBuilder.BuildWebGL`；默认输出 `artifacts/submission/WebGL/`，邻接 `webgl-build-manifest.json` 记录结果。也可调用 Unity 批处理：

```sh
"$UNITY_EDITOR" -batchmode -nographics -projectPath "$PWD/unity/DawnGarden" \
  -executeMethod DawnGarden.Editor.SmallWorldWebGLBuilder.BuildWebGL \
  -logFile /tmp/small-planet-webgl.log -quit
```

`UNITY_EDITOR` 应指向本机 Unity 可执行文件。构建读取随包的 `artifacts/game-v5/build-manifest.json` 场景清单，运行时保留 `artifacts/game-v1/build-manifest.json` 的源 `buildId`；这两个小文件用于兼容性校验，不包含旧 Mac 应用。完整源码含历史场景和作者工具；历史素材重建/录制专用菜单可能还依赖未随包提供的旧素材，不是本次 WebGL 构建入口。

## 生成提交包

```sh
# 先查看允许清单；不会创建压缩包：
python3 tools/package_submission.py --dry-run --manifest /tmp/submission-inventory.json
# WebGL 构建齐备后生成：
python3 tools/package_submission.py
```

默认输出 `artifacts/submission/SmallPlanetSubmission.zip`。打包器只收录明确允许的源码、导入资源、干净种子和 WebGL，检查已知本机密钥是否混入所有文件；排除 `.env.local`、用户 `data/`、虚拟环境、Unity 缓存、历史构建、录屏、测试输出与日志。压缩包内有 `submission-manifest.json` 可供复核。

主要目录：`backend/game/` 为小禾和运行时家园逻辑；`backend/home_creation/` 为生成任务与资产目录；`home_server.py` 为同源本地服务；`web/submission/` 为提交入口；`tools/` 为启动、初始化、验收和打包；`docs/submission/seed/` 为公开种子素材。旧版物件设计工作台仍保留在 `server.py` 与根目录网页中，它与 WebGL 入口使用不同服务。
