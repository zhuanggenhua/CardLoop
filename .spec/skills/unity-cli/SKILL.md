---
name: unity-cli
description: Unity 官方 unity 命令行工具的 CardLoop 项目入口；用于查询/管理 Editor、打开当前项目、检查 CLI 能力、冷启动辅助和明确授权后的无头测试/构建候选，不替代 UnitySkills。
---

# Unity CLI（项目适配）

本 skill 只管 Unity 官方 `unity` 命令行工具。它不是 UnitySkills REST，也不是 Unity Editor 的传统 `Unity.exe -batchmode` 参数。当前项目默认仍用 `.spec/skills/unity-skills` 做已打开 Editor 的结构化自动化，用 `.spec/tools/unity-verify.mjs` 管 batchmode 验证。

上游候选来源：`https://github.com/Unity-Technologies/skills/skills/unity-cli/SKILL.md`。上游正文只作为能力参考；CardLoop 执行时必须按本文件和 `unity-automation-tools.md` 的项目边界裁决。

## 先查本机真实能力

每次准备用 Unity CLI 前，先查当前 CLI 版本和实际帮助，不按上游最新版假设命令存在：

```powershell
Get-Command unity -ErrorAction SilentlyContinue
unity --version --format json
unity --help
```

如果要使用子命令，先读对应 help：

```powershell
unity open --help
unity editors --help
unity projects --help
```

历史检查线索：曾确认本机 PATH 上存在 `unity.exe` 且当时版本 help 未显示上游 skill 中的 `status`、`command`、`run`、`test`、`build`、`doctor`、`pipeline` 等新版命令。该线索不能替代本轮重查；使用前必须以当前 `unity --version --format json` 和 `unity --help` 为准。

## 允许用途

优先用于这些外层任务：

- 查询 CLI、Editor 安装、默认 Editor、Hub 项目注册表。
- 在当前项目没有已打开 Editor 时，用已绑定 Editor 路径打开 CardLoop。
- 读取或验证 UnitySkills CLI 绑定配置。
- 明确需要 Unity CLI，且本机 help 证明命令存在时，作为 headless test / build / one-shot batch 的候选入口。

当前项目绑定配置检查：

```powershell
if (Test-Path Library/UnitySkills/cli_config.json) { Get-Content -Raw Library/UnitySkills/cli_config.json }
node .spec/tools/unity-verify.mjs status
```

只有同时满足以下条件，才能把 Unity CLI 作为本轮 Unity 入口：

1. `Library/UnitySkills/cli_config.json` 存在且 `enabled:true`。
2. `cliPath` 或 PATH 上的 `unity` 可执行文件能回读。
3. 当前项目没有已经打开的 Editor，或本轮目标只是安全查询，不会改变 Editor / 项目状态。
4. 目标属于冷启动、查询、明确授权的无头测试/构建候选，且 `unity --help` / 子命令 help 证明命令存在。

## 禁止用途

- 不得用 Unity CLI 抢 `.spec/skills/unity-skills` 的默认 Editor 自动化入口。
- 当前项目已有 Editor 或 `Temp/UnityLockfile` 被持有时，不得用 Unity CLI 另起一个 Editor 来绕过 UnitySkills。
- 不得自行安装、升级、卸载 Unity CLI、Unity Editor、Editor modules 或 Unity Pipeline 包；这些属于用户明确授权动作。
- 不得为了使用上游 skill 里的 `status` / `command` / `test` / `build`，擅自升级本机 CLI。
- 不得用 Unity CLI 直接增删 UPM 包来绕过项目插件索引和包管理裁决。
- 不得把 Unity CLI 的成功退出码单独解释成测试完整通过；测试必须有可回读结果文件或明确机器输出。

## 与其它入口分工

| 场景 | 使用入口 |
|---|---|
| 已打开 Editor 的场景、对象、资源、Console、批量操作 | `.spec/skills/unity-skills` |
| 无头编译、包解析、测试前置检查 | `.spec/tools/unity-verify.mjs` |
| 查询/打开 Unity 项目、列 Editor、检查 CLI 能力 | 本 skill |
| UnitySkills 不可用且文件式 IPC 更合适 | AIBridge fallback，先按项目 guard 说明原因 |
| Player / 手机 / 截图 / Profiler / JS eval 专项 | puerts-unity-mcp，先按项目 guard 说明目的 |

## 命令规则

- 需要机器读取输出时，优先使用 `--format json` 或命令支持的 JSON 参数。
- 需要非交互执行时，必须显式使用 `--non-interactive` / `--yes` 等 help 中存在的非交互参数。
- 命令失败时先读 stdout / help / 项目日志，不猜测命令语义，不换第二套 Unity 工具冒充成功。
- 如本机 CLI 版本低于上游 skill 描述，按本机 help 收窄能力；上游只作为升级候选，不作为当前可执行事实。

## 需重验示例

```powershell
unity --version --format json
unity editors --installed --json
unity open "C:\Gamedev\Unity\Project\CardLoop" --editor-path "C:\Gamedev\Unity\Editor\6000.5.4f1\Editor\Unity.exe"
```

如果这些命令的 help 或实际输出与本文不一致，以本机 help 和项目 guard 输出为准，并更新本 skill 或 `unity-automation-tools.md`。
