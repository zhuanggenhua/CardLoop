# CardLoop AI 规范中心

本目录是 CardLoop 项目的 AI 规范主入口。根目录 `AGENTS.md` 只负责进入这里；实际规则、知识、skill 和任务组织收口到 `.spec/`。

## 项目定位

- Unity 工程根目录：`C:\Gamedev\Unity\Project\CardLoop`。
- 当前迁移状态：已静态迁入 FantasyWord 的插件、本地包、GameCore 运行时候选和 AI workflow。
- 当前验证状态：2026-08-01 已用 Unity `6000.5.4f1` 新鲜 batchmode 完成 Package Resolve 与脚本编译验证，关键错误筛选为空；场景运行和业务启用仍需后续单独验证。
- 当前边界：不得把 FantasyWord 的业务内容、任务进度、旧世界观、截图证据或历史决策当成 CardLoop 已确认事实。

## 每轮必读核心

1. `AGENTS.md`：根目录入口，只负责指向这里。
2. `.spec/AGENTS.md`：本文件，说明调度结构和项目边界。
3. `.spec/rules/system.md`：硬红线，任何任务都不得绕过。
4. `.spec/knowledge/README.md`：知识导航，决定任务应该继续读哪些规范。

## 结构分工

| 位置 | 职责 |
|------|------|
| `.spec/rules/` | 强制红线，只写必须做、不得做、只能做什么。 |
| `.spec/knowledge/standards/` | 长期规范和做法，回答“这类事该怎么做”。 |
| `.spec/skills/` | 项目内可复用工作流。 |
| `.spec/agents/` | 需要隔离上下文才有价值的职能 agent。 |
| `.agents/skills/` | 从 FantasyWord 迁入的可复用任务型 skill。 |
| `.codex/skills/` | 从 FantasyWord 迁入的专项任务型 skill。 |
| `docs/FantasyWord-framework-migration.md` | 本轮迁移清单、裁决、排除项和后续验证入口。 |

## 调度核心

- 小而清楚的任务：读直接相关规范后实施。
- Bug、测试失败、异常行为：先用 `.spec/skills/systematic-debugging` 找到能解释原始症状的证据。
- 新增或修改规则、知识、skill：先用 `.spec/skills/spec-steward` 判断落点。
- 多步骤或多模块任务：用 `.spec/skills/task-breakdown` 拆任务；同一文件集重叠的任务串行。
- 收口前：用 `.spec/skills/verification-before-completion`，必须有新鲜验证证据。

## 项目验收口径

- 文档或规范任务：至少验证 `.spec` 链接、索引和入口一致。
- `.spec` 结构改动后运行：`node .spec/tools/spec-lint.mjs`。
- 插件或框架迁移：至少做静态文件数对账；进入 Unity 后还要做 Package Resolve、脚本编译、Console 错误检查。
- GameCore 当前只是迁入候选；已通过 Unity 编译验证，但没有 CardLoop 语义切片和运行验证前，不得声称“已正式业务接入完成”。
