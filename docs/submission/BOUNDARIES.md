# 提交范围与数据来源

提交版名称为《捡个星球慢慢造》，运行形式为 Unity WebGL 客户端和同源 Python 本地服务。

- 小禾真实服务：`backend/game/service.py` 通过 `LiveTools` 调用 Qwen，接入状态快照、有限动作、偏好和共同经历；模型输出经协议校验后才能形成建议。存档事实和奖励由游戏逻辑控制。
- 家园创作真实服务：`backend/home_creation/` 接入 TOAPIS `gpt-image-2`、Aholo 3D、可恢复任务与确认步骤。生成调用需要使用者自己的密钥和额度。
- 编辑范围：玩家首个相邻地块的现有目录资产复用、平面位置、Y 轴朝向、受限缩放与颜色；入口和地块边界受校验，预览经玩家确认才应用。完整家园展示保留天文台等已制作内容；未实现整张地图任意重塑、任意代码生成或任意游戏机制生成。地形连接只支持已有预设与柔化档位。
- 邻居是 NPC，各有本地进度和有限互动；没有真人在线联机服务。
- 故事演示、展示和录屏使用预设对白、时序或镜头。它们用于演示已制作内容，不能视为真实 Agent 在录制时实时规划、建模和完成所有动作的证据。
- `mock` 为明确标记的确定性演示服务；概念图/模型是测试素材，不调用网络。`live` 使用真实服务适配器，服务实际返回和额度决定可用性。
- 新生成 GLB 的归一化、LOD 和预览制作仍调用本机 Blender；编辑器装配、目录刷新及完整工程验收调用本机 Unity。仅浏览已构建 WebGL、基础游玩、小禾对话和现有物件布局不要求运行这些创作工具。

`seed/catalog.json` 来源于工程导出的 15 项预制件目录，只保存资源描述、工程依赖和内容哈希；预览位于 `seed/previews/`。`seed/garden-kit.json` 和 `seed/garden-kit/` 保存 4 项本地程序组合资源及预览，生成脚本为 `scripts/build_garden_detail_kit.py`。初始化仅注册这 19 项，不读取旧 SQLite，不包含旧任务、玩家档案、聊天、记忆或用户生成石灯。

打包时将三个目录校验所需的精确版本 Unity 包嵌入 `Packages/`：`com.unity.cloud.gltfast`、`com.unity.render-pipelines.universal`、`com.unity.shadergraph`。优先保留工程已有的嵌入包及本地改动，只有工程未嵌入时才从对应已解析 Unity 包补齐；不会用缓存覆盖工程版本。仅排除包示例、文档和测试，保留许可证及运行/编辑器源文件。目录依赖通过原始虚拟 `Packages/...` 路径和内容哈希检查。其他 Unity 包由 `manifest.json` / `packages-lock.json` 锁定，首次打开工程时由 Unity 解析。

`tools/unity` 与 `tools/blender` 是原工程的 macOS 工具包装，默认位置分别为 `/Applications/Unity Hub.app`、`/Applications/Blender.app`；其他平台或安装位置需调整包装脚本。原后端用 `fcntl` 锁定数据目录，当前启动流程适用于 macOS / Linux。历史素材重建脚本和桌面录制流程中存在旧 `artifacts/` 路径；WebGL 成品使用工程内已导入的资源。提交包不包含历史录制、旧 Mac 应用和全部历史生成素材，重新运行那些历史专用菜单需要补齐其相应源素材。

保留模型查看器及字体的现有许可证、Unity 包许可证和工程中的美术/音乐 provenance。提交包不构成所有第三方资源的额外授权声明。
