---
name: spec-steward
description: 维护 CardLoop 的 .spec 规范结构、知识落点、skill 合并和反复错误升级；当新增或修改规则、知识、skill、任务卡、决策记录时使用。
---

# Spec Steward（规范管家）

用于保证规范放对层、索引同步、冲突可追踪。

## 落点判断

先分层，再写内容：

- 全局 AGENTS：跨项目通用红线、路径、路由原则。落点在 `D:\codex-home\AGENTS.md`。
- 项目 AGENTS：项目主入口指针和极少量必须常驻边界。当前根 `AGENTS.md` 只做 `.spec` 指针。
- 全局 skill：可复用任务类型的具体 SOP。落点在 `D:\codex-home\skills`。
- 项目 skill：只对 CardLoop 成立的专项 workflow。落点优先 `.spec/skills`；既有 `.codex/.agents/skills` 保留。
- 任务/用户故事/专项文档：一次性决定、局部特例、具体素材或当轮偏离。落点在 `openspec/`、`.spec/knowledge/features/project/` 对应专项或 `.spec/tasks`。

## 内容类型

| 内容 | 落点 |
|------|------|
| 硬红线、禁止项 | `.spec/rules/system.md` |
| 长期做法规范 | `.spec/knowledge/standards/` |
| 项目事实、系统设计、Unity 入口 | `.spec/knowledge/features/project/` 或 `.spec/knowledge/features/` |
| 反复错误候选 | `.spec/knowledge/lessons.md` |
| 架构/流程决策 | `.spec/decisions/` |
| 可复用流程 | `.spec/skills/<name>/SKILL.md` |
| 进行中工作拆解 | `.spec/tasks/<slug>.md` |

## Skill 合并规则

1. 先列出现有 skill 和外部 skill 是否同职责。
2. 如果同职责但本项目已有强红线，优先保留本项目语义，把外部 skill 的结构或流程吸收进来。
3. 如果冲突影响执行方式，必须列为用户决策项，不得擅自替换。
4. 第三方/bundled skill 默认只作为候选，不直接覆盖正式资产。
5. 自有项目 skill 可直接升级，但仍需说明变更文件、验证结果和剩余风险。

## 反复错误升级

- 第一次：当轮说明，不入长期规范。
- 第二次：记录到 `.spec/knowledge/lessons.md`。
- 第三次左右：升级为 `.spec/rules`、`.spec/knowledge/standards` 或 `.spec/skills`。
- 升级后在 lesson 中标注“已升格 -> <落点>”。

## 验证

- 新增/删除 `.spec` 文档后，同步 `.spec/knowledge/README.md`。
- 新增 skill 后确认 frontmatter 只有 `name` 和 `description`。
- 根 `AGENTS.md` 只能作为指针，不再堆详细 SOP。
- 同一规范口径只能有一个权威文档；入口、索引和冲突矩阵只能保留摘要和链接，不能重复承载会漂移的模型配置、命令顺序、参数或验收清单。
- 不作为正式入口保留旧 `.spec/knowledge/features/project` 或既有 skill，除非用户明确要求。

