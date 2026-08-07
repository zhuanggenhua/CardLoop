---
name: knowledge
description: CardLoop 项目知识导航：查规范、迁移状态、项目事实、验收要求时先从这里定位。
metadata:
  type: index
  status: 已交付
---

# Knowledge（项目知识库导航）

## standards（长期规范）

| 文档 | 何时查 |
|------|--------|
| [`standards/workflow.md`](standards/workflow.md) | 做开发流程、验证、知识沉淀、规范维护时查。 |
| [`standards/testing.md`](standards/testing.md) | 做测试、验收、bug 修复、TDD 策略和验证证据时查。 |
| [`standards/code-style.md`](standards/code-style.md) | 写代码、写注释、建文档、命名和生成物处理时查。 |
| [`standards/skill-conflicts.md`](standards/skill-conflicts.md) | 查看从 FantasyWord 迁入的 skill 如何在 CardLoop 收口时查。 |

## migration（迁移事实）

| 文档 | 何时查 |
|------|--------|
| [`../../docs/FantasyWord-framework-migration.md`](../../docs/FantasyWord-framework-migration.md) | 查看 FantasyWord 插件、框架、AI docs/agents 的迁入范围、排除项和待验证项。 |

## features（项目/框架事实）

| 文档 | 何时查 |
|------|--------|
| [`features/gamecore-gas.md`](features/gamecore-gas.md) | 做 GameCore 与 EX-GAS 能力、Timeline、GameplayEffect、Cue、TargetCatcher 或正式集成边界时查。 |
| [`features/plugin-docs.md`](features/plugin-docs.md) | 接入、审查或清理第三方插件 / 本地 UPM 包时，先查官方文档入口和本项目适配边界。 |
| [`features/ai-quick/README.md`](features/ai-quick/README.md) | AI 按任务查询第三方插件、项目公共系统和自有工具类的正式入口、生命周期、真实示例和禁止旁路时查。 |
| [`features/project/card-survival-infinite.md`](features/project/card-survival-infinite.md) | 查看《卡牌生存：无限》的游戏愿景、核心交互、Mod/关卡编辑器扩展边界和视觉参考入口时查。 |
| [`features/project/stackcraft-template-study.md`](features/project/stackcraft-template-study.md) | 查看 StackCraft 模板导入后的设置恢复结论、框架吸收裁决、保留/不接管边界和后续底座设计约束时查。 |
| [`features/project/stackcraft-system-reference-matrix.md`](features/project/stackcraft-system-reference-matrix.md) | 查看 StackCraft 架构搬迁顺序、数据定义优先原则、UI 框架吸收、联机约束、可吸收职责和必须排除的旧职责时查。 |
| [`features/project/gameplay-foundation-proposal.md`](features/project/gameplay-foundation-proposal.md) | 查看 GamePlay 地基提案、YooAsset / 新 Input System 决策、内容定义优先、StackCraft 架构吸收阶段和未来业务边界时查。 |

## skills（工作流）

| Skill | 何时查 |
|-------|--------|
| `.spec/skills/before-you-code` | 改代码、配置、资源、规范或执行会改变结果的命令前查。 |
| `.spec/skills/systematic-debugging` | Bug、测试失败、异常行为排查时查。 |
| `.spec/skills/verification-before-completion` | 声称完成前查。 |
| `.spec/skills/spec-steward` | 新增或修改规范、skill、知识入口时查。 |
| `.spec/skills/task-breakdown` | 多步骤或多模块任务拆分时查。 |
| `.agents/skills/unity-ui-development` | 做 Unity UGUI 或 UI Toolkit 相关任务时查。 |
| `.codex/skills/gas-ability-authoring` | 做 EX-GAS 能力配置、制作或排查时查。 |
| `.codex/skills/unity-tilemap-2d` | 做 Unity 2D Tilemap 相关任务时查。 |

## agents（职能角色）

| Agent | 何时查 |
|-------|--------|
| [`.spec/agents/reviewer.agent.md`](../agents/reviewer.agent.md) | 需要隔离上下文做完整交付审查时查；普通小改动不用为了形式派审。 |
