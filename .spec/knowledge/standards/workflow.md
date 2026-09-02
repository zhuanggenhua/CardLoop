---
name: workflow
description: 开发与规范治理流程：说明执行边界、提交限制、知识沉淀路由和外部工作流吸收方式。
metadata:
  type: doc
  status: 已交付
---

# 开发与规范治理流程

## 前提门禁

动手前提和多参考源设计裁决的唯一正文是 [`.spec/skills/before-you-code/SKILL.md`](../../skills/before-you-code/SKILL.md)。本文件只保留流程层路由：任何代码、配置、资源、规范、插件级或架构级改动，先进入 `before-you-code` 锁定问题对象、真相来源、目标入口/环境和验收口径。

## 文档先行门禁

框架、目录、职责归属或模块 owner 重构前，先按 [`framework-layering.md`](framework-layering.md) 写清 YokiFrame / GameCore / Gameplay 的唯一分层、特化落点和外部候选边界，再同步知识索引并通过 `spec-lint`。

文档未收口时，只允许继续清理文档入口、补自有项目事实、补外部候选裁决或删除真正垃圾；不得开始运行时代码、Prefab、场景或 Unity 配置重构。

## 外部参考候选门禁

CardLoop 是新项目。外部项目、旧工程、官方示例、教程和历史实现只能作为候选证据，不默认成为业务主线、框架 owner、目录规范、测试入口或完成标准。

若用户明确要求参考某个外部来源，必须先完成以下裁决，再允许实施：

1. **当前项目本体**：先写清 CardLoop 当前玩家目标、对象 owner、状态写入口、生命周期和验收入口。
2. **参考来源价值**：说明外部来源具体证明什么，只能证明到哪个范围。
3. **不采用内容**：明确来源工程的世界观、目录、旧单例、旧场景、旧资源路径、测试流水和业务数据是否排除。
4. **落点裁决**：判断吸收到 YokiFrame、GameCore、Gameplay、项目文档、任务记录，还是完全不采用。
5. **验证闭包**：回到 CardLoop 自有入口验证，不用外部来源能运行来证明本项目完成。

如果只是想要“像某项目一样”，但没有当前项目自己的职责裁决，结论必须停在候选分析，不写实现、不建文档矩阵、不把来源项目名称写成 active 主线。

## 执行边界

### Codex 宿主环境工具门禁

- 动手前必须先按当前会话宿主选择工具：Codex CLI / TUI 环境使用 `shell_command` 读取终端、文件状态、依赖探测和本地命令结果；Codex App / 桌面宿主环境才允许在确有需要时使用 App / MCP 类工具。
- 不得把调用失败当成环境探测方式。若已知当前处于 CLI / TUI，禁止调用动态工具发现、延迟加载 MCP / App 工具这类只适用于 App / 桌面宿主的入口。
- 如果某个工具返回当前宿主不支持动态工具，必须立即停止重复调用同类入口，记录为工具环境限制，并改用当前宿主可用的正式工具链。
- 需要 Unity 验证时仍先走 `.spec/tools/unity-verify.mjs` guard；不得因为 App 工具不可用就绕过 Unity 验证门禁。

### 项目结构入口

- 根 `AGENTS.md` 是入口，只放指针，不再堆项目细节。
- `.spec` 是规范、knowledge、skill 和 agent 的唯一权威源。
- `.spec/knowledge/features/project/` 是当前项目知识库正式入口；废弃目录不得作为规范入口继续引用。
- `openspec` 承载 proposal/change/spec，不混进 `.spec/decisions`。
- 项目 skill / agent 的落点、宿主发现适配入口和 `.codex/skills` 禁用口径，由 [`.spec/skills/spec-steward/SKILL.md`](../../skills/spec-steward/SKILL.md) 统一承载；本文件不重复写宿主目录规则。
- 参考工程接入或插件级抽象不得先写实现再补证据；如果外部参考路径尚未读取、关键入口尚未定位，必须停在“前提未锁定”，不能用“先做一个通用版本”代替参考裁决。

## 外部工作流吸收边界

采用：

- `.spec` 结构分层。
- `spec-steward` 维护规范落点。
- `lessons.md` 作为反复错误升级池。
- “完成声明必须有验证证据”的收口门禁。
- “主 Agent 调度、skill 是方法、`.md` 是规则”的结构思想。
- 宿主发现目录已按 Git symlink 收口；当前执行口径见 `spec-steward`。

不采用：

- 默认创建或使用 git worktree 的流程。
- 未经用户确认的分支、提交、发布、PR 流程。
- 所有生产代码都必须严格 TDD 的一刀切规则。
- “设计文档必须提交”的默认动作。
- 把旧项目或示例工程的文档体系整体搬进本项目。

## Git 与提交

- 不使用回滚/撤销历史操作。
- 不擅自创建、切换、重建或删除分支、tag、worktree。
- 不主动提交、不推送，除非用户当轮明确允许。
- 工作区已有无关改动时，只触碰本轮目标文件。

## 知识沉淀

新增规则、修改规范、新建/迁移 skill、任务卡和决策记录的落点治理，唯一正文是 [`.spec/skills/spec-steward/SKILL.md`](../../skills/spec-steward/SKILL.md)。本文件只保留流程要求：先判断落点，再同步索引；发现多个文档承载同一口径时，收口到一个权威文档，其它位置改成路由或证据说明。

## 结构校验

`.spec` 结构、索引、skill 或 agent 变化后，运行：

```powershell
node .spec/tools/spec-lint.mjs
```

该脚本只检查规范结构，不启动 Unity、不修改资产。
