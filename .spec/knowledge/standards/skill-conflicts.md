---
name: skill-conflicts
description: LumioAgent skill 与现有 airule、AGENTS、项目 skills 的整合矩阵：说明保留、合并、改写和已收口项。
metadata:
  type: doc
  status: 已交付
---

# Skill 整合与冲突矩阵

本文件记录 LumioAgent 思想被吸收到 CardLoop 后，各 skill 与现有规范的关系。原则：**结构向 LumioAgent 学，执行红线以现有系统/项目规则为准**。

## 已合并为项目 `.spec` skill

| LumioAgent skill | 本项目落点 | 处理结果 |
|------------------|------------|----------|
| `spec-steward` | `.spec/skills/spec-steward` | 合并。吸收“放对位置、同步索引、反复错误升级”，并改成本项目五层落点规则。 |
| `before-you-code` | `.spec/skills/before-you-code` | 合并。改为四项前提锁定 + CardLoop 渐进披露入口。 |
| `systematic-debugging` | `.spec/skills/systematic-debugging` | 合并。叠加“原始 bug 描述不得改写”和“止血不等于修复”。 |
| `verification-before-completion` | `.spec/skills/verification-before-completion` | 合并。保留“证据先于声明”，适配 Unity/文档/资源验收。 |
| `task-breakdown` | `.spec/skills/task-breakdown` | 合并。保留任务拆解，移除自动 worktree 假设。 |
| `writing-plans` | `.spec/skills/writing-plans` | 改写为路由。长期计划仍以 `D:\codex-home\skills\planning-with-files\SKILL.md` 为准。 |
| `test-driven-development` | `.spec/skills/test-driven-development` | 改写。保留 TDD 方法，但不采用跨项目一刀切强制。 |
| `receiving-code-review` | `.spec/skills/receiving-code-review` | 合并。保留“先核实再改，不表演式认同”。 |
| `brainstorming` | `.spec/skills/brainstorming` | 合并。用于设计/需求未收敛场景，默认先方案后实施。 |
| `subagent-driven-development` | `.spec/skills/subagent-driven-development` | 安全改写。保留任务交接和审查思想；子 agent 模型配置与派发约束由该 skill 统一承载。 |
| `using-git-worktrees` | `.spec/skills/using-git-worktrees` | 安全改写。默认禁止自动创建/切换/删除 worktree，除非用户当轮明确许可。 |

## 已收口的现有项目 skill

| 现有 skill | 状态 | 原因 |
|------------|------|------|
| `aibridge` | 未迁入项目 skill | 源项目未提供可复用脚本入口；Unity 自动化优先查项目 skill `.spec/skills/unity-skills/SKILL.md` 和本项目本地包 `Packages/com.besty.unity-skills`、`Packages/com.aibridge.unity`。 |
| `gas-ability-authoring` | 已迁入 `.spec/skills/gas-ability-authoring` | CardLoop EX-GAS 制作和排查专项流程，强项目绑定。 |
| `safe-image-reading` | 未迁入项目 skill | 源项目中该 skill 已暂停且指向旧备份路径；图片展示和验收按系统 skill `D:\codex-home\skills\show-image-to-user\SKILL.md`。 |
| `unity-tilemap-2d` | 已迁入 `.spec/skills/unity-tilemap-2d` | 项目 2D Tilemap 专项流程，已接入新导航。 |
| `unity-timeline-signal-debug` | 已迁入 `.spec/skills/unity-timeline-signal-debug` | Timeline Signal 排查专项流程，项目内可复用。 |
| `D:\codex-home\skills\code-comments` | 保留为系统 skill | 中文注释和 Unity Inspector 说明专项能力；本项目当前没有对应项目 skill。 |
| `unity-ui-development` | 已迁入 `.spec/skills/unity-ui-development` | UGUI / UI Toolkit 专项能力，仍由项目文档路由。 |
| `unity-skills` | 已迁入 `.spec/skills/unity-skills` | 通过显式目标目录安装为项目 skill；根 `SKILL.md` 是唯一项目 skill，内部模块保留为 `MODULE.md` / `INDEX.md` 资料，不再注册成 71 个项目子 skill；本地包 `Packages/com.besty.unity-skills` 作为项目内上游资料，不再依赖系统层副本。 |
| `D:\codex-home\skills\planning-with-files` | 保留为长期计划真相源 | 用户已明确“长期计划”指向该 skill。 |
| `D:\codex-home\skills\self-evolving-skills` | 保留为系统 skill 生命周期能力 | `.spec/skills/spec-steward` 只管本仓 `.spec`，不替代系统 skill-lab 流程。 |

当前宿主适配结果见 `.spec/skills/spec-steward`：`.agents/skills`、`.claude/skills`、`.claude/agents` 已作为宿主发现入口收口，`.codex/skills` 不再作为项目 skill 来源。本文件只记录整合证据，不承载执行口径。

## 已按当前指令收口的原待决策项

| 原冲突点 | 当前处理 |
|----------|----------|
| `.spec/skills/spec-steward` 与 `self-evolving-skills` | 不再等待决策：`.spec/skills/spec-steward` 管本项目 `.spec`；`self-evolving-skills` 继续管系统/自有 skill 生命周期。 |
| LumioAgent 强 TDD 与项目务实测试策略 | 不再等待决策：项目采用务实 TDD；需要强制测试先行的模块以后直接写入对应模块规范。 |
| LumioAgent worktree 并行流程与项目禁止擅自 worktree | 不再等待决策：默认禁止自动 worktree，只有用户当轮明确授权才可走 `using-git-worktrees`。 |
| 项目知识库入口 | 不再等待决策：`.spec/knowledge/features/project/` 是 CardLoop 后续项目事实入口；FantasyWord 的同名业务知识库没有迁入。 |

