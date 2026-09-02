---
name: unity-automation-tools
description: CardLoop Unity 自动化工具职责摘要：裁决 UnitySkills、Unity CLI、AIBridge、puerts-unity-mcp 和 batchmode 的默认入口、专项场景、禁止场景和前置 guard。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-incident-evidence + unity-cli-docs
  status: 已交付
  update_triggers: automation-tool-change, unity-version-change, crash-evidence-change, guard-policy-change
---

# Unity 自动化工具职责摘要

本文件只保留当前执行入口和禁止边界；完整事故记录、历史命令细节和本机旧检查结果不在 active 速查中维护，追溯以 git 历史为准。

## 默认裁决

CardLoop 不把 UnitySkills、AIBridge、puerts-unity-mcp、Unity CLI 和 batchmode 平级混用。进入 Unity 自动化前先走项目 guard：

```powershell
node .spec/tools/unity-verify.mjs status
node .spec/tools/unity-verify.mjs preflight --mode batch
node .spec/tools/unity-verify.mjs preflight --mode editor-automation --tool unityskills
```

| 工具 | 当前定位 | 默认性 | 用途边界 |
|---|---|---:|---|
| UnitySkills | 已打开 Editor 的结构化自动化主入口 | 默认 | 场景、对象、资源、Console、测试、批处理、编译状态。 |
| batchmode | 无头编译、包解析、命令行测试和一次性 Editor 方法 | 默认，限无头验证 | 不依赖普通 Editor 窗口；必须由 guard 生成或检查命令。 |
| Unity CLI | 官方 `unity` 命令行候选入口 | 非默认 | 只在项目已绑定、当前 help 证明目标命令存在、且场景适合冷启动 / 安全查询 / headless 时使用。 |
| puerts-unity-mcp | Runtime / Player / 手机 / 截图 / Profiler / JS eval 专项入口 | 非默认 | 只用于这些专项；不接管默认 Editor 结构化操作。 |
| AIBridge | 文件式 IPC fallback | 非默认 | 只在 UnitySkills 不可用，且文件式 IPC 比其它入口更合适时使用。 |

## 禁止场景

- 不得在同一轮验证里并发或轮换使用多套 Unity 自动化入口来绕过失败。
- 不得绕过 `.spec/tools/unity-verify.mjs` 手写新的 Unity 启动、测试或 Editor 自动化命令。
- 不得通过 Puerts / MCP / 反射 / 窗口信号关闭普通 Unity Editor。
- 不得在 UnitySkills 主线程队列卡住、导入未结束或 Shader 编译未结束时，继续压请求、重复测试、启动第二套工具或强关 Editor。
- 不得用截图 / E2E 代替源码、资源、配置和玩家可见参数对账。
- 不得自行安装、升级、卸载 Unity CLI、Unity Editor、Unity Pipeline 包或修改 UnitySkills CLI 配置来“让 CLI 可用”。

## Guard 摘要

- `unity-verify status` 先判断当前项目 Unity、锁文件、测试结果和自动化入口状态。
- `preflight --mode batch` 只为无头编译、包解析、命令行测试和一次性 Editor 方法服务。
- `preflight --mode editor-automation --tool unityskills` 是已打开 Editor 的默认自动化入口。
- `unityskills-ensure` 只恢复 UnitySkills REST 服务：Unity 已打开时等待插件自启；Unity 未打开时只在项目已启用 CLI coldStart 时启动当前项目。
- 截图 / 视觉证据类 PlayMode 用例不得走 batchmode；它们依赖真实 GameView、渲染帧和图片写入。
- `--testResults` 必须写到可持久回读的位置；Unity 退出码成功但结果 XML 缺失，只能判为验证基础设施失败。

## Unity CLI 候选条件

使用 Unity CLI 前必须重新读取当前本机状态，不沿用历史检查结果：

```powershell
if (Test-Path Library/UnitySkills/cli_config.json) { Get-Content -Raw Library/UnitySkills/cli_config.json }
Get-Command unity -ErrorAction SilentlyContinue
unity --help
node .spec/tools/unity-verify.mjs status
```

只有同时满足以下条件，才允许把 Unity CLI 作为本轮入口：项目 CLI 绑定存在且启用；可执行文件能回读；当前项目没有已打开 Editor 或本轮只是安全查询；目标属于冷启动、查询、headless test、one-shot batch 或 build；本机 help 证明目标子命令存在。

UPM 包管理不走 Unity CLI。用户说“装包”“UPM”“添加 com.unity.*”“改 manifest”时，先读项目 skill [`../../../skills/unity-package-management/SKILL.md`](../../../skills/unity-package-management/SKILL.md)。

## 阻塞处理

- Unity 已显示“打开中的场景被磁盘版本改变，是否重载”时，默认先停止当前实现 / 验证循环并汇报；只有用户明确授权后，才允许对唯一匹配窗口执行一次重载确认工具。
- UnitySkills PlayMode TestRunner job 在 Domain Reload 期间 REST 短暂拒绝连接属于预期瞬断；如果最终无法恢复原始 job、`totalTests = 0` 或缺少结果文件，结论是验证基础设施失败，不是玩法测试失败或通过。
- 自动化基础设施卡住时，只收集不加重阻塞的证据：`/health` 快路径、`/events`、项目日志、UnitySkills 日志、进程状态、窗口状态和磁盘静态校验。

## 官方入口

- UnitySkills：[`Packages/com.besty.unity-skills/unity-skills~/SKILL.md`](../../../../Packages/com.besty.unity-skills/unity-skills~/SKILL.md)。
- Unity CLI：[`../../../skills/unity-cli/SKILL.md`](../../../skills/unity-cli/SKILL.md)。
- Unity Package Management：[`../../../skills/unity-package-management/SKILL.md`](../../../skills/unity-package-management/SKILL.md)。
- AIBridge：[`Packages/com.aibridge.unity/README.md`](../../../../Packages/com.aibridge.unity/README.md)、[`Packages/com.aibridge.unity/package.json`](../../../../Packages/com.aibridge.unity/package.json)。
- puerts-unity-mcp：[`puerts-unity-mcp/README.md`](../../../../puerts-unity-mcp/README.md)、[`puerts-unity-mcp/Packages/puerts-unity-mcp/package.json`](../../../../puerts-unity-mcp/Packages/puerts-unity-mcp/package.json)。
- Unity Editor batchmode：以当前 Unity 官方文档和本地 Test Framework 源码为准；当前项目 Test Framework 1.7 不允许 `-runTests` 与 `-quit` 组合。
