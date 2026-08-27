---
name: unity-automation-tools
description: CardLoop Unity 自动化工具职责矩阵：裁决 UnitySkills、Unity CLI、AIBridge、puerts-unity-mcp 和 batchmode 的默认入口、专项场景、禁止场景和前置 guard。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-incident-evidence + unity-cli-docs
  status: 已交付
  verified_at: 2026-08-26
  update_triggers: automation-tool-change, unity-version-change, crash-evidence-change, guard-policy-change
---

# Unity 自动化工具职责矩阵

## 结论

CardLoop 当前确实安装了三套可操作 Unity 的工具入口：UnitySkills、AIBridge、puerts-unity-mcp；此外还有 Unity 官方 Editor batchmode 命令行。Unity 官方 Unity CLI 是后续可选入口，不是当前默认入口。它们不能平级混用，正式验证入口必须先经过项目 guard：

```powershell
node .spec/tools/unity-verify.mjs status
node .spec/tools/unity-verify.mjs preflight --mode batch
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool unityskills
```

默认裁决：

| 工具 | 当前定位 | 是否默认 | 主要用途 |
|---|---|---:|---|
| UnitySkills | Editor 结构化自动化主入口 | 是 | 场景 / 对象 / 资源 / Console / 测试 / 批处理 / 编译状态。 |
| batchmode | 无头编译、包解析、命令行测试和一次性 Editor 方法 | 是，限无头验证 | 不依赖普通 Editor 窗口；必须由 guard 生成或检查命令。 |
| Unity CLI | 绑定后的官方 CLI 候选入口 | 否，当前未启用 | 冷启动已绑定项目、无头测试、一次性 batch、构建；必须先确认本项目绑定和本机可执行文件。 |
| puerts-unity-mcp | 专项 MCP / JS / Runtime / Player / 截图 / Profiler 工具 | 否 | 只在 UnitySkills 或 batchmode 不适合时使用。 |
| AIBridge | 备用候选入口 | 否 | 只在 UnitySkills 不可用且文件式 IPC 更合适时使用。 |

当前本机事实：2026-08-26 检查 `Library/UnitySkills/cli_config.json` 不存在，`Get-Command unity` 未发现 PATH 上的 `unity` 命令。因此本项目当前不能直接切换到 Unity CLI；只能把它作为用户后续安装并在 UnitySkills 面板绑定后的候选入口。

## 工具分层

| 名称 | 现实含义 | 是否直接操作 Unity |
|---|---|---:|
| PuerTS | Unity 内运行 JavaScript 并桥接 C# 的底层运行时。 | 否，底层依赖。 |
| `com.tencent.puerts.core` / `com.tencent.puerts.v8` | PuerTS Core 和 V8 UPM 包。 | 否，给 puerts-unity-mcp 使用。 |
| puerts-unity-mcp | 基于 PuerTS 的 MCP 端点，提供 Editor / PlayMode / Player 工具。 | 是。 |
| UnitySkills | 本地 REST API + skill 系统，提供结构化 Unity Editor 自动化。 | 是。 |
| Unity CLI | Unity 官方 `unity` 命令行工具；当前项目必须先由 UnitySkills 写入 `Library/UnitySkills/cli_config.json` 且 `enabled:true` 才允许使用。 | 是，但只在项目已绑定且 Editor 关闭或确需 headless/one-shot 时使用。 |
| AIBridge | 文件式 IPC 的 Unity Editor AI Bridge。 | 是。 |
| Unity batchmode | Unity 官方命令行无头模式。 | 是，但不依赖普通 Editor UI。 |

## 能力对比

| 场景 | 默认工具 | 备用 / 专项 | 裁决 |
|---|---|---|---|
| 编辑器结构化操作 | UnitySkills | AIBridge | UnitySkills 有 schema、dry run、permission、batch/job；作为默认入口。 |
| 测试运行 | batchmode 或 UnitySkills Test | 不用 Puerts | 命令行测试先走 guard；已打开 Editor 的测试可用 UnitySkills，但失败要按 TestRunner job 分类。 |
| 场景 / 资源查询 | UnitySkills | AIBridge | 结构化查询优先 UnitySkills；静态文件可直接读 YAML / 源码。 |
| PlayMode / Runtime | UnitySkills Test 或 puerts-unity-mcp | AIBridge 备用 | Runtime/Player/手机目标才优先考虑 puerts-unity-mcp。 |
| 手机 / Player 调试 | puerts-unity-mcp | 无 | Puerts MCP 明确支持 Player / 手机端点，这是它的专项优势。 |
| 截图 | puerts-unity-mcp | UnitySkills / 其它截图流程 | EditorWindow、Player、手机截图可用 Puerts；用户验收图仍按看图规范。 |
| Profiler | puerts-unity-mcp | Unity Profiler 手动流程 | Puerts MCP 文档明确提供 Profiler 报告。 |
| JS eval | puerts-unity-mcp | 无 | 仅限需要 JS eval 且不应生成 C# / 触发 domain reload 的场景。 |
| 批处理 / 多对象编辑 | UnitySkills batch | AIBridge | UnitySkills 有 preview、confirmToken、job、报告；优先使用。 |
| 编译 / 包解析 smoke | batchmode | UnitySkills debug | 无头验证优先 batchmode；普通 Editor 不为纯编译打开。 |
| Editor 未打开时冷启动 | UnitySkills ensure + 已绑定 Unity CLI | 普通手动打开 Editor | 只有本项目 `Library/UnitySkills/cli_config.json` 存在且 `enabled:true`、`features.coldStart:true` 时，才允许 CLI cold-start。 |
| Editor 关闭的一次性回归测试 | Unity CLI 或 batchmode | 无 | Unity CLI 只在项目绑定、CLI 可执行文件可回读、Editor 未打开时使用；未绑定时继续使用 guard 生成的 batchmode。 |

## 禁止场景

- 不得用 puerts-unity-mcp / 反射 / JS eval 调 `UnityEditor.EditorApplication.Exit` 关闭普通 Editor。
- 不得把 UnitySkills、AIBridge、puerts-unity-mcp 同时作为同一轮验证入口。
- 不得绕过 `.spec/tools/unity-verify.mjs` 手写新的 Unity 启动、测试或 Editor 自动化命令。
- 不得用 puerts-unity-mcp 做默认结构化 Editor 操作；只有 `runtime-player`、`screenshot`、`profiler`、`js-eval` 四类专项目的允许进入。
- 不得把 AIBridge 作为默认入口；除非 UnitySkills 不可用，且已说明文件式 IPC 为什么更合适。
- 不得用截图 / E2E 代替 StackCraft 源码等价证明；截图只证明用户可见表现。
- 不得在 `Library/UnitySkills/cli_config.json` 缺失、`enabled:false`、`unity` 命令不可回读或 Editor 已经打开时，把 Unity CLI 当成默认编辑器入口。
- 不得自行安装 Unity CLI、Unity Editor、Unity Pipeline 包或修改 UnitySkills CLI 配置来“让 CLI 可用”；这些属于用户选择或项目显式启用动作。

## Guard 使用

进入任何 Unity 自动化或 batchmode 前，`unity-verify` 会自动运行 Unity 序列化结构守卫：

```powershell
node .spec/tools/unity-yaml-guard.mjs
```

该守卫用于提前拦截空 `.meta`、非法 GUID、损坏 `.unity` / `.prefab` 文件头和脚本替换残留，防止把坏序列化文件交给 Unity 导入 / 编译。

batchmode 验证：

```powershell
node .spec/tools/unity-verify.mjs preflight --mode batch
node .spec/tools/unity-verify.mjs batch-test --unity "C:\Gamedev\Unity\Editor\6000.5.4f1\Editor\Unity.exe" --testPlatform PlayMode --testResults Logs\playmode-results.xml --logFile Logs\playmode.log
```

`batch-test --execute` 必须同时满足 Unity 退出码成功和 `--testResults` 指定的 XML 文件真实生成且非空。若 Unity 日志显示测试结束但结果 XML 缺失，只能判定为验证基础设施失败；不得把退出码 0 单独解释成“测试完整通过”。

`--testResults` 禁止写到项目 `Temp\` 目录；正式测试结果写到 `Logs\*.xml` 或其它可持久回读路径，防止测试日志完成但指定 XML 无法作为证据。

截图 / 视觉证据类 PlayMode 用例不得走 batchmode。它们依赖真实 GameView、渲染帧和图片写入，应在 Unity 编辑器健康后走 UnitySkills 编辑器自动化，或按 `puerts-mcp --purpose screenshot` 等截图专用链路执行；batchmode 只用于不依赖最终画面截图的编译、规则和运行时断言。

如果 batch preflight 只因 `Temp/UnityLockfile` 阻塞，先确认没有当前项目相关 Unity / 导入 / shader 编译进程，再用 guard 清理：

```powershell
node .spec/tools/unity-verify.mjs status
node .spec/tools/unity-verify.mjs clean-stale-lockfile --confirm-stale-lockfile
```

UnitySkills 默认 Editor 自动化：

```powershell
node .spec/tools/unityskills-ensure.mjs --timeout-ms 600000
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool unityskills
```

`unityskills-ensure` 只负责恢复 UnitySkills REST 服务入口：先扫 `8090-8100` 的 `/health`；如果当前项目 Unity 已打开，就等待插件侧自启，不启动第二个 Editor；如果 Unity 未打开，则只有在 `Library/UnitySkills/cli_config.json` 已启用 Unity CLI 且允许 coldStart 时，才用 `-unityskills-coldstart` 启动当前项目。它不得关闭 Unity、不得杀进程、不得绕过 `unity-verify`。

Unity CLI 候选流程：

```powershell
if (Test-Path Library/UnitySkills/cli_config.json) { Get-Content -Raw Library/UnitySkills/cli_config.json }
Get-Command unity -ErrorAction SilentlyContinue
node .spec/tools/unity-verify.mjs status
```

只有同时满足以下条件，才允许把 Unity CLI 作为本轮 Unity 入口：本项目 `Library/UnitySkills/cli_config.json` 存在且 `enabled:true`；配置里的 `cliPath` 或 PATH 上的 `unity` 可执行文件能回读；当前项目没有已经打开的 Editor 或被 `Temp/UnityLockfile` 持有；目标属于冷启动、headless test、one-shot batch 或 build。已打开 Editor 的结构化查询、场景对象读取、Console、资源和交互式测试仍走 UnitySkills；未绑定项目继续走 guard 生成的 batchmode。

Unity CLI 命令必须使用结构化和非交互参数，例如 `--format json --non-interactive`；失败时先读取 `--help` / `doctor` 输出和项目日志，不修改 UnitySkills 配置、不安装 CLI、不绕过 guard。

puerts-unity-mcp 专项用途：

```powershell
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool puerts-mcp --purpose runtime-player
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool puerts-mcp --purpose screenshot
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool puerts-mcp --purpose profiler
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool puerts-mcp --purpose js-eval
```

AIBridge fallback：

```powershell
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool aibridge --fallback
```

场景重载确认弹窗：

```powershell
node .spec/tools/unity-confirm-scene-reload.mjs --project CardLoop --scene FoundationTest --confirm-reload-dialog
```

该工具只处理 Unity 已经显示“打开中的场景被磁盘版本改变，是否重载”的确认框。重载弹窗本身是当前验证链路的阻塞信号：默认先停止当前实现 / 验证循环并汇报，不继续排队 UnitySkills 请求、重复测试或改走第二套工具。只有用户明确授权代为确认后，才允许用本工具定位标题包含项目名和场景名的唯一 Unity 主窗口并发送一次确认；不能作为通用点击器、不能替代 UnitySkills，也不能在没有弹窗证据时使用。

## TestRunner job 恢复失败

当前项目已确认 UnitySkills 通过已打开 Editor 运行 PlayMode 测试时，Unity Test Runner 会在 Domain Reload 期间短暂断开 REST 服务；这时轮询可能出现连接拒绝，属于预期瞬断。UnitySkills 在 `AsyncJobService` 中只允许恢复原始 PlayMode Test Runner job：PlayMode 原始 job 没有离开 Play Mode 前绝不重启；离开 Play Mode 后仍取不到原始结果时，也不能自动重启同一 PlayMode 测试，因为原测试可能已经生成截图、写文件或提交玩法副作用。只有最终返回 `failed_runner_not_restored`、`totalTests = 0`、`The original Unity Test Runner job ... was not restored after domain reload.`，或只有中间截图但没有 TestRunner 结果，才说明 UnitySkills / TestRunner 的异步 job 恢复链路失败，不能证明玩法测试失败，也不能证明玩法测试通过。EditMode 在未接受任何结果时仍可按 UnitySkills 内部规则保守重启一次。

处理顺序：

1. 先允许 UnitySkills 内置恢复完成；轮询期间只把 REST 连接拒绝视为 Domain Reload 瞬断，不改走第二套 Unity 工具，也不启动第二个 PlayMode test job。
2. 如果测试最终 `completed` 且有真实 `totalTests / passedTests / failedTests`，该结果可作为本次 TestRunner 证据。
3. 如果内置恢复后仍返回 `failed_runner_not_restored`、`totalTests = 0` 或没有测试结果文件，立即停止用同一 UnitySkills Test 路径反复重试。
4. 读取 `debug_check_compilation`、`debug_get_errors` 和 Console，确认是否存在真实编译或运行错误。
5. 若 Console / 编译干净，继续源码、资源、Prefab、静态预检和只读审计；需要测试结果时，等待项目无已打开 Editor 后改走 guard 生成的 batchmode 测试，或在用户明确要求时用人工 Test Runner 结果作为证据。
6. 汇报时必须把未恢复情况称为“验证基础设施失败”，不得写成“PlayMode 失败”或“业务功能失败”。

## 事故归因

2026-08-15 最新 Unity 崩溃的直接证据指向 Puerts / Unity MCP 退出链路：通过 MCP 调用普通 Editor 退出后，PuerTS V8 原生模块 `PapiV8.dll` 在释放环境时崩溃。该证据不能证明 Gameplay / GameCore 代码导致 Unity 崩溃。

流程风险则是另一层问题：UnitySkills、Puerts MCP、batchmode 和普通 Editor 如果没有互斥入口，会造成重复启动、长导入、Domain Reload、TestRunner job 丢失和崩溃归因混乱。因此项目把 `.spec/tools/unity-verify.mjs` 设为统一前置 guard。

2026-08-20 事故追加：一次普通 Editor 关闭请求发生在 Unity 自动化主线程队列已经异常、且 Unity 退出清理阶段仍触发编译管线 tick 的状态下，Windows 事件显示 `Unity.dll` 访问冲突 `0xc0000005`，Editor log 显示 `s_EditorUserBuildSettings != NULL` 断言失败并崩溃。该证据同样不能证明 Gameplay 运行时代码崩溃；它证明 Agent 在编辑器状态不健康时继续尝试自动关闭 Editor 是错误流程。此后 `unity-verify` 必须在 UnitySkills 健康状态证明 Unity 正在编译 / 导入、无法确认空闲，或 UnitySkills 主线程长时间不 tick 时阻断自动化，不允许改用关闭信号、第二套工具或重复压队列。

2026-08-20 恢复策略追加：UnitySkills 服务停止不再靠手动猜端口、重复调用主线程 skill、改走 AIBridge / Puerts 或关闭重开 Unity。插件源码默认启用 Editor 启动自启，并在 Unity 编译 / 导入中延后开服；项目脚本 `.spec/tools/unityskills-ensure.mjs` 是唯一外部恢复入口。

## 官方入口

- UnitySkills：[`Packages/com.besty.unity-skills/unity-skills~/SKILL.md`](../../../../Packages/com.besty.unity-skills/unity-skills~/SKILL.md)。
- AIBridge：[`Packages/com.aibridge.unity/README.md`](../../../../Packages/com.aibridge.unity/README.md)、[`Packages/com.aibridge.unity/package.json`](../../../../Packages/com.aibridge.unity/package.json)。
- puerts-unity-mcp：[`puerts-unity-mcp/README.md`](../../../../puerts-unity-mcp/README.md)、[`puerts-unity-mcp/Packages/puerts-unity-mcp/package.json`](../../../../puerts-unity-mcp/Packages/puerts-unity-mcp/package.json)。
- Unity 命令行：以当前 Unity 官方文档和本地 Test Framework 源码为准；当前项目 Test Framework 1.7 不允许 `-runTests` 与 `-quit` 组合。
