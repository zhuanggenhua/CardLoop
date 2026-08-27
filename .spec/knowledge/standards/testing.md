---
name: testing
description: 测试与验收规范：说明 CardLoop 的验证分层、TDD 适用范围、bug 验收和完成声明证据。
metadata:
  type: doc
  status: 已交付
---

# 测试与验收规范

## 验证分层

- 静态证据：文件、diff、配置、资源引用、GUID、脚本结构、索引链接。
- 轻量 smoke：能快速验证入口未断、脚本可运行、菜单/资源/引用存在。
- Unity Editor 自动化：默认通过 UnitySkills 读取场景、Console、对象、资源和测试状态；AIBridge 和 puerts-unity-mcp 的职责边界见项目职责矩阵。
- PlayMode / EditMode 测试：用于核心运行时逻辑、回归 bug、高风险合同。
- 截图或图面核验：必须先走安全读图门禁，不直接读取大图污染上下文。

## Unity 端到端验证标准流程

Unity 端到端验证是“当前项目真实入口能运行”的证据，不是参考工程吸收、源码等价或架构正确性的唯一证据。StackCraft 吸收类任务必须先完成参考源码对照、当前正式 owner 对照、参数 / 触发条件 / 结果映射，再选择最低足够的 Unity 验证层级。

进入 Unity 编辑器、batchmode、UnitySkills、AIBridge、puerts-unity-mcp 或 PlayMode / EditMode 前，必须先完成能脱离 Unity 的代码级预检；预检通过以前不得用编辑器验证替代逻辑审查。预检至少包括：

- 参考源码对照：模板玩家效果、触发条件、参数和结果已经映射到当前正式 owner，且明确哪些模板结构被排除。
- 当前源码扫描：没有恢复旧 Manager / 单例 / DTO / 第二套事件、第二套资源加载、第二套状态或直接依赖 `Assets/StackCraft` 旧路径。
- 配置与资源对照：新增或修改的 `.asset` / `.prefab` / `.meta` 中 GUID、地址、字段名和生成器常量一致。
- StackCraft 视觉 / 动画静态对账必须绑定到参考对象与当前对象的具体 Unity 对象、组件和字段；全文件 token 存在性只能作为 smoke 线索，不能证明卡牌标题、图标、材质、粒子、进度条或动画参数已经复刻到正确对象。
- StackCraft 吸收的静态期望值必须优先从参考源码、Prefab、Material、Shader、Mesh 或 Unity YAML 中解析得到；不得长期维护一份手写参数清单作为第二真相。确实无法解析时，必须在守卫或专项文档中说明来源位置和无法解析原因。
- 非 Unity 静态校验：相关文件的 `git diff --check`、`.spec` lint、规范测试、脚本级生成器 / 索引检查均已完成；如果没有 `.sln` / `.csproj` 或独立脚本测试入口，必须明确说明无法脱离 Unity 编译。

当前 StackCraft 吸收静态预检入口：

```powershell
node .spec/tools/gameplay-static-preflight.mjs
node .spec/tools/stackcraft-business-representative-audit.mjs
```

`gameplay-static-preflight` 只证明旧模板结构、旧命名、正式依赖入口和已登记测试资源没有明显静态回流；当脚本包含对象级字段对账时，也只能表述为“对应字段静态对账通过”。`stackcraft-business-representative-audit` 只证明 Starter / Beginning 代表性业务竖切仍能从 StackCraft 参考资产追溯到 CardLoop 作者源。它们都不证明 Unity 编译、Prefab / Scene 回读、PlayMode 行为、玩家画面、连续动画或 StackCraft 全量业务资产迁移已经一致。

只有预检无法覆盖的事实，才进入 Unity 阶段，例如 Unity C# 编译、Inspector 序列化回读、Prefab / Scene 实例引用、YooAsset 收集结果、真实输入链、PlayMode 运行结果和玩家可见画面。

每次启动 Unity、进入 PlayMode、跑 UnitySkills / AIBridge / puerts-unity-mcp / batchmode 测试或做场景级验收前，必须先写清四项前提：

- 问题对象：本轮验证的具体功能、场景、测试或编辑器动作。
- 真相来源：参考源码、当前源码、序列化资源、测试文件、日志或用户指定入口中的哪一个。
- 目标入口 / 环境：使用已打开编辑器、batchmode、UnitySkills、AIBridge、Test Runner 还是人工打开的场景。
- 验收口径：本轮只证明编译、资源引用、测试断言、玩家链路、视觉表现，还是参考逻辑等价。

写清前提后，必须先跑项目 guard，而不是直接手写 Unity 命令：

```powershell
node .spec/tools/unity-verify.mjs status
node .spec/tools/unity-yaml-guard.mjs
node .spec/tools/unity-verify.mjs preflight --mode batch
```

`unity-verify` 的 `preflight` 会自动先跑 `unity-yaml-guard`。手工直接运行 `unity-yaml-guard` 只用于在不进入 Unity 的情况下确认 `.meta`、`.unity`、`.prefab` 和常见 Unity YAML 文件没有空文件、非法 GUID、损坏文件头或脚本替换残留。

如果 guard 只因 `Temp/UnityLockfile` 阻塞，且 `status` 已证明没有 Unity、导入进程或 shader 编译进程在处理当前项目，使用 guard 清理残留锁文件：

```powershell
node .spec/tools/unity-verify.mjs clean-stale-lockfile --confirm-stale-lockfile
```

需要使用已打开编辑器做默认 UnitySkills 自动化时，改用：

```powershell
node .spec/tools/unityskills-ensure.mjs --timeout-ms 600000
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool unityskills
```

`unityskills-ensure` 只恢复 UnitySkills REST 服务本身：Unity 已打开时等待插件自启；Unity 未打开时仅在本项目 `Library/UnitySkills/cli_config.json` 已启用 Unity CLI coldStart 时启动当前项目。它不是第二套自动化工具，不替代 `unity-verify`，也不得关闭 Unity 或清理进程。

其它工具的允许场景和 guard 参数以 [`../features/ai-quick/unity-automation-tools.md`](../features/ai-quick/unity-automation-tools.md) 为准。

需要跑 Unity Test Runner 命令行测试时，由 guard 生成命令；未加 `--execute` 时只 dry run，不启动 Unity：

```powershell
node .spec/tools/unity-verify.mjs batch-test --unity "C:\Gamedev\Unity\Editor\6000.5.4f1\Editor\Unity.exe" --testPlatform PlayMode --testResults Logs\playmode-results.xml --logFile Logs\playmode.log
```

验证层级按以下顺序裁决，能用低层级证明时不得升级到更重链路：

1. 源码 / 配置 / 序列化对照：用于证明职责、参数、GUID、fileID、引用和规则映射。
2. 编辑器静态查询：用于证明场景对象、组件、资源、Console 和编译状态。
3. EditMode / PlayMode 测试：用于证明可重复的运行时规则、回归 bug 和公开契约。
4. 统一测试场景 smoke：用于证明新正式链路在 `FoundationTest` 等真实入口可用。
5. 截图 / 录像 / 图面验收：只用于用户可见表现；不得替代源码等价和规则结算证据。

### Unity 进程与工具链纪律

- 启动 Unity 前先检查当前项目是否已有 `Unity.exe`、`AssetImportWorker`、`UnityShaderCompiler` 进程和 `Temp/UnityLockfile`；已有可用编辑器时优先复用，不为单个逻辑切片重复开普通 Editor。
- 不得并发启动多条 Unity 验证路径；batchmode、普通 Editor、UnitySkills、AIBridge、puerts-unity-mcp 同一时间只能有一个主验证入口。`unity-verify` 如果 UnitySkills `/health` 证明当前正在编译 / 导入，或无法通过正式健康入口确认空闲，必须停止验证并等待，不得改走另一套工具。
- 普通 Editor 只用于确实需要编辑器状态、场景对象或人工观察的验证；纯编译、纯测试、纯源码对照优先不用普通 Editor。
- 不得通过 Puerts / Unity MCP / 反射调用 `UnityEditor.EditorApplication.Exit` 关闭普通 Editor。Agent 也不得在 UnitySkills / AIBridge 主线程队列卡住、导入未结束或 Shader 编译未结束时，对普通 Editor 发送 `CloseMainWindow`、`WM_CLOSE`、`Alt+F4`、`taskkill` 或等价关闭信号；此时只能停下汇报“编辑器自动化基础设施卡住 / Unity 正在导入或编译”，等待用户人工处理或等待状态恢复。
- UnitySkills 是默认 Editor 自动化入口；AIBridge 只能 fallback，puerts-unity-mcp 只能用于 Runtime / Player、截图、Profiler 或 JS eval 等专项场景。
- 不得绕过 `.spec/tools/unity-verify.mjs` 手写新的 Unity 启动 / 测试命令，除非 guard 本身损坏；绕过时必须先说明损坏证据和等价的手动检查结果。
- 如果本轮外部场景生成器或磁盘写入导致 Unity 弹出“已打开场景在磁盘上改变，是否重载”的确认框，或 UnitySkills / 编辑器自动化被该弹窗挡住，必须立即停止当前实现与验证循环并向用户汇报阻塞；不得继续压 UnitySkills 请求、重复运行测试、启动第二套 Unity 工具、无目标按键模拟或自行点击重载。汇报时写清：现实后果是编辑器等待用户选择、证据是哪个场景被磁盘版本改变、为什么它会阻止本轮验证继续、最小补救动作是用户手动选择重载 / 不重载，或用户明确授权后只执行一次 `node .spec/tools/unity-confirm-scene-reload.mjs --project CardLoop --scene <SceneName> --confirm-reload-dialog`。
- UnitySkills 的 `/health` 快路径如果持续显示 `mainThreadIdleMs` 很大、`queuedRequests` / `pendingRequests` 增长，且 `isCompiling=false`、`isUpdating=false`，必须先判定为编辑器自动化基础设施卡住；此时不得继续调用普通 UnitySkills skill、`/health?live=1`、`/analytics` 或其它会进入主线程队列的接口，也不得反复刷新资源、重跑测试、启动第二套验证链路或尝试自动关闭 Editor。只能使用不进入主线程队列的证据：`/health` 快路径、`/events`、项目 `Logs/Editor.log`、`Library/UnitySkills/*.jsonl`、进程状态、窗口截图 / 窗口枚举和磁盘文件静态校验。若证据指向 Unity 或插件机制不明确，先查项目规范、插件自带文档、Unity 官方文档或本地源码，再决定最小补救动作，不得靠猜测点按钮、清缓存、杀进程或继续压队列。
- 不得为了“让测试继续”直接删除 crash dump、Library 缓存或进程残留；`Temp/UnityLockfile` 只能通过 `clean-stale-lockfile --confirm-stale-lockfile` 清理，且 guard 必须先确认无 Unity / 导入 / shader 编译进程。
- Unity Test Runner 参数必须以当前项目本地包源码或官方文档为准。当前项目的 Test Framework 1.7 本地源码明确提示命令行跑测试时不能同时指定 `-quit`；该版本下 `-runTests` 命令不得再带 `-quit`。
- UnitySkills PlayMode 测试只允许恢复原始 Test Runner job，不允许在 Domain Reload 后自动重启同一 PlayMode 测试。REST 短暂拒绝连接属于预期瞬断；若原 job 最终仍未恢复、`totalTests = 0`、没有结果文件或只生成了中间截图，结论都是“验证基础设施失败”，不得说成玩法测试失败或通过，也不得反复用同一失败路径重试。EditMode 在未接受任何结果时可按 UnitySkills 内部规则保守重启一次；AIBridge 若出现同类未恢复，仍按验证基础设施失败处理。
- batchmode 测试必须同时检查 Unity 退出码和 `--testResults` 文件。退出码为 0 但 XML 缺失或为空时，只能判定为验证基础设施失败；不得把日志里的“Test run completed”单独当成完整测试通过证据。
- `--testResults` 不得写到项目 `Temp\` 目录；该目录下的结果文件在当前项目曾出现“Unity 日志显示完成但指定 XML 不保留”的误判风险。正式测试结果写到 `Logs\*.xml` 或其它可持久回读路径。
- 截图 / 视觉证据类 PlayMode 用例不得通过无头 batchmode 运行；这类用例依赖真实 GameView / 渲染帧和图片写入，应在 Unity 编辑器健康后走 UnitySkills 编辑器自动化，或按专项目的走截图专用工具链。batchmode 只用于不依赖最终画面截图的编译、规则和运行时断言。
- StackCraft 参考画面截图只有一个允许入口：`Assets/Editor/Gameplay/Automation/StackCraftReferenceCaptureMenu.cs` 的 `Gameplay/Automation/Capture StackCraft Reference Main` 菜单。该入口只用于加载参考模板场景、注入干净参考存档状态并采集对照图；它不得被正式 Gameplay 或测试支撑代码调用，不得在截图流程中修改 `Assets/StackCraft` 源码，不得删除、移动或读写用户真实 `Application.persistentDataPath` 存档。其它正式代码和测试仍禁止直接引用 `Assets/StackCraft` 路径、`CryingSnow` 命名空间或模板 Manager / DTO / UI 类型。经用户当轮明确授权的参考模板可运行性补丁是例外，但只能隔离模板自身旧假设，例如存档文件名过滤或 Editor 参考场景加载，并必须登记到 StackCraft 吸收矩阵；这类补丁不得成为正式 Gameplay 职责入口。

### 崩溃、卡死与长导入中止条件

- Unity 窗口无响应、长时间 Opening Project、Hold on、导入资源或 shader 编译时，先判断是导入 / 编译耗时还是崩溃，不得继续叠加测试命令。
- “卡死 / 阻塞”必须基于现实证据判定，例如窗口无响应、导入 / 编译长时间无进度、UnitySkills 主线程队列持续增长、重载确认框挡住编辑器、测试结果文件长期不生成、Crash Handler 或编辑器进程异常退出。单次命令失败、测试失败、截图不一致、普通编译错误、CLI/TUI 不支持某个 App / MCP 动态工具，都不等同于 Unity 卡死。
- 判定为卡死 / 阻塞后，必须立即停止同一路径无效尝试：不得继续压 UnitySkills 请求、重复运行同一测试、启动第二套 Unity 工具、模拟乱点按钮、强关编辑器、清缓存或改走旁路冒充验证完成。只能继续收集不加重阻塞的证据，例如进程状态、日志、已有 health 快照、磁盘文件和窗口状态。
- 如果不是卡死 / 阻塞，而是普通可定位失败，应继续按静态证据、错误日志或测试断言修复原任务；不得把“停止无效重试”误用成停止任务、跳过修复或回避验证。
- 一旦出现 Unity 崩溃、Crash Handler、Windows Error Reporting、crash dump 或编辑器进程异常退出，必须立即停止玩法实现和验证链路，转入 `debugging-evidence.md` 的崩溃取证；未分类前不得继续开 Unity。
- 崩溃汇报必须区分现实故障、触发条件、工具链责任和玩法代码责任。只有崩溃栈或运行证据指向 Gameplay / GameCore 代码时，才允许称为玩法代码导致崩溃。
- 因工具链、Unity 插件、MCP、Puerts、Test Runner 或编辑器退出路径导致的失败，必须标为验证流程故障；不能拿它证明业务功能失败，也不能拿业务测试缺口掩盖工具链事故。

## TDD 适用范围

通用 red-green 流程、公开边界、独立预期值和测试分类以系统 skill `D:\codex-home\skills\tdd\SKILL.md` 为准；本节只规定 CardLoop 的选择和验收口径。

进入 TDD 前，必须锁定真实业务行为、当前公开入口和可观察结果。只有框架形状、程序集结构、未来接口或没有消费者的抽象时，使用静态检查、架构守卫或 smoke，不把它们当成业务 TDD。

CardLoop 的 AI 驱动开发不采用“所有生产代码都严格 TDD”的一刀切方式。玩法、UI 手感、职业、剧本、关卡编辑器、联机 / Mod API 形状仍在变化时，先用设计记录、功能裁决、原型场景、截图 / 日志 / playtest 和统一测试场景验收约束方向；等规则成为正式行为、接入长期边界或出现复现 bug 后，再提升为行为测试或回归测试。

如果代码已经先于测试完成，后补测试必须按真实用途归类为回归测试、公开契约测试、架构守卫或验收测试，不得在汇报中倒称为 TDD。严格 TDD 必须有 RED 证据，并确认失败来自目标行为缺失。

当前 Gameplay 地基阶段优先保护的不变量包括：唯一内容 ID、EX-GAS 标签职责、ResourceSystem / YooAsset 入口、行动请求复核、权威随机、原子结果结算、统一测试场景玩家可见功能验收，以及不得出现第二套真相。这些不变量变化时，优先补行为测试、公开契约测试或架构守卫。

必须优先补失败测试或最小复现：

- 新增核心运行时逻辑。
- 修复已复现 bug。
- 修改无测试保护但风险高的规则结算、数据加载、保存、生成、同步、UI 状态机。
- 第三次复发的同类错误。

可以不机械补同粒度测试：

- 纯文档或规范重构。
- 小范围注释、索引、路由调整。
- 一次性取证脚本。
- 只改变说明文字、不改变行为的内容。
- 仍在验证方向的玩法探索、交互手感、UI 原型、关卡规则草案和 Mod / 联机 API 草案。

即使不写测试，也必须给出对应的验证证据或说明为什么本轮只需要静态验证。

## 测试分类

分类定义和 TDD 质量规则见系统 skill；CardLoop 只规定各类验证的落点：

- 行为、回归和已确认的公开契约：优先使用 EditMode / PlayMode 测试。
- 程序集、依赖、类型形状和职责边界：使用静态校验或 Editor 架构守卫，不计入业务行为覆盖。
- Unity 入口、资源、场景、编译链路和编辑器状态：使用 smoke / Editor 自动化，不替代业务测试。

反射检查方法或类型是否存在、检查私有实现、用 Stub 自证框架行为时，按系统 skill 的分类归入架构守卫或辅助验证。

## PlayMode 状态隔离

- PlayMode 测试触碰 SaveSystem、SaveKit、PlayerPrefs、Application.persistentDataPath、临时资源包、全局配置或其它跨用例持久状态时，必须在 UnitySetUp 中配置独立临时目录或独立命名空间，并在 UnityTearDown 中重置入口和删除本轮临时数据。
- 不得让测试默认读写真正运行目录的存档槽位。若测试目标就是验证正式持久目录，必须在用例名和验证说明中写清，并只操作本轮创建的槽位。
- 因历史运行残留导致的槽位满、配置残留或全局状态污染，属于测试隔离缺陷；修复应优先收口测试入口，不用清空用户真实数据来让测试转绿。
## Bug 验收

- 先写清用户原始症状的保真版，保留关键限定词和数量范围。
- 再写清当前证据命中的症状，两者不一致时不得冒充已经修复原 bug。
- 修复后必须回到原始位点验证。
- 如果改动只是止血、跳过、兜底或降噪，必须标为“缓解”，不能叫“修复”。

## 完成声明

声称完成前必须回答：

- 哪个命令、截图、场景、日志、数据或链接证明这件事？
- 是否是本轮新鲜证据？
- 是否覆盖用户原始目标，而不是只覆盖相邻问题？
- 有哪些 known gaps 或未验证项？
