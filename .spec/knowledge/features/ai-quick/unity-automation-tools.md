---
name: unity-automation-tools
description: CardLoop Unity 自动化工具职责矩阵：裁决 UnitySkills、AIBridge、puerts-unity-mcp 和 batchmode 的默认入口、专项场景、禁止场景和前置 guard。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-incident-evidence
  status: 已交付
  verified_at: 2026-08-15
  update_triggers: automation-tool-change, unity-version-change, crash-evidence-change, guard-policy-change
---

# Unity 自动化工具职责矩阵

## 结论

CardLoop 当前确实安装了三套可操作 Unity 的工具入口：UnitySkills、AIBridge、puerts-unity-mcp；此外还有 Unity 官方 batchmode 命令行。它们不能平级混用，正式验证入口必须先经过项目 guard：

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
| puerts-unity-mcp | 专项 MCP / JS / Runtime / Player / 截图 / Profiler 工具 | 否 | 只在 UnitySkills 或 batchmode 不适合时使用。 |
| AIBridge | 备用候选入口 | 否 | 只在 UnitySkills 不可用且文件式 IPC 更合适时使用。 |

## 工具分层

| 名称 | 现实含义 | 是否直接操作 Unity |
|---|---|---:|
| PuerTS | Unity 内运行 JavaScript 并桥接 C# 的底层运行时。 | 否，底层依赖。 |
| `com.tencent.puerts.core` / `com.tencent.puerts.v8` | PuerTS Core 和 V8 UPM 包。 | 否，给 puerts-unity-mcp 使用。 |
| puerts-unity-mcp | 基于 PuerTS 的 MCP 端点，提供 Editor / PlayMode / Player 工具。 | 是。 |
| UnitySkills | 本地 REST API + skill 系统，提供结构化 Unity Editor 自动化。 | 是。 |
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

## 禁止场景

- 不得用 puerts-unity-mcp / 反射 / JS eval 调 `UnityEditor.EditorApplication.Exit` 关闭普通 Editor。
- 不得把 UnitySkills、AIBridge、puerts-unity-mcp 同时作为同一轮验证入口。
- 不得绕过 `.spec/tools/unity-verify.mjs` 手写新的 Unity 启动、测试或 Editor 自动化命令。
- 不得用 puerts-unity-mcp 做默认结构化 Editor 操作；只有 `runtime-player`、`screenshot`、`profiler`、`js-eval` 四类专项目的允许进入。
- 不得把 AIBridge 作为默认入口；除非 UnitySkills 不可用，且已说明文件式 IPC 为什么更合适。
- 不得用截图 / E2E 代替 StackCraft 源码等价证明；截图只证明用户可见表现。

## Guard 使用

batchmode 验证：

```powershell
node .spec/tools/unity-verify.mjs preflight --mode batch
node .spec/tools/unity-verify.mjs batch-test --unity "C:\Gamedev\Unity\Editor\6000.5.4f1\Editor\Unity.exe" --testPlatform PlayMode --testResults Temp\playmode-results.xml --logFile Temp\playmode.log
```

如果 batch preflight 只因 `Temp/UnityLockfile` 阻塞，先确认没有当前项目相关 Unity / 导入 / shader 编译进程，再用 guard 清理：

```powershell
node .spec/tools/unity-verify.mjs status
node .spec/tools/unity-verify.mjs clean-stale-lockfile --confirm-stale-lockfile
```

UnitySkills 默认 Editor 自动化：

```powershell
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool unityskills
```

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

## 事故归因

2026-08-15 最新 Unity 崩溃的直接证据指向 Puerts / Unity MCP 退出链路：通过 MCP 调用普通 Editor 退出后，PuerTS V8 原生模块 `PapiV8.dll` 在释放环境时崩溃。该证据不能证明 Gameplay / GameCore 代码导致 Unity 崩溃。

流程风险则是另一层问题：UnitySkills、Puerts MCP、batchmode 和普通 Editor 如果没有互斥入口，会造成重复启动、长导入、Domain Reload、TestRunner job 丢失和崩溃归因混乱。因此项目把 `.spec/tools/unity-verify.mjs` 设为统一前置 guard。

## 官方入口

- UnitySkills：[`Packages/com.besty.unity-skills/unity-skills~/SKILL.md`](../../../../Packages/com.besty.unity-skills/unity-skills~/SKILL.md)。
- AIBridge：[`Packages/com.aibridge.unity/README.md`](../../../../Packages/com.aibridge.unity/README.md)、[`Packages/com.aibridge.unity/package.json`](../../../../Packages/com.aibridge.unity/package.json)。
- puerts-unity-mcp：[`puerts-unity-mcp/README.md`](../../../../puerts-unity-mcp/README.md)、[`puerts-unity-mcp/Packages/puerts-unity-mcp/package.json`](../../../../puerts-unity-mcp/Packages/puerts-unity-mcp/package.json)。
- Unity 命令行：以当前 Unity 官方文档和本地 Test Framework 源码为准；当前项目 Test Framework 1.7 不允许 `-runTests` 与 `-quit` 组合。
