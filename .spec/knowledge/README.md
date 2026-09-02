---
name: knowledge
description: 项目知识库导航——查“某事怎么做”(standards)或“某功能怎么设计的”(features)时，从这里找到对应 .md。
metadata:
  type: index
---

# Knowledge(项目知识库 · 导航)

本文件是 `knowledge/` 下所有 .md 的导航 meta:一行描述 + 路径,按需下钻。

> **导航行与各文档 frontmatter `description` 同一句话口径,只写「是什么 + 何时查」。** 交付历史在 git,不进文档;长度 / status 枚举 / 登记覆盖 / 链接可达由 `node .spec/tools/spec-lint.mjs` 机械校验。

## standards/(开发规范 · 要遵守的「怎么做」)

| 文档 | 一句话 |
|------|--------|
| [`standards/gameplay-architecture.md`](standards/gameplay-architecture.md) | Gameplay 地基架构规范:裁决自有对象模型、系统边界、外部参考候选、Mod 和联机扩展时查 |
| [`standards/framework-layering.md`](standards/framework-layering.md) | 框架分层规范:裁决 YokiFrame、GameCore、Gameplay 的职责边界、依赖方向和特化落点时查 |
| [`standards/workflow.md`](standards/workflow.md) | 开发与规范治理流程:处理任务前提、执行边界、提交限制、知识沉淀和外部工作流吸收时查 |
| [`standards/testing.md`](standards/testing.md) | 测试与验收规范:实现功能、修 bug、选择 TDD / 回归 / Unity 验证和声明完成前查 |
| [`standards/code-design.md`](standards/code-design.md) | 代码设计原则、设计模式选择和反模式预防规范:写业务代码、拆职责、加抽象或审查反模式时查 |
| [`standards/code-style.md`](standards/code-style.md) | 代码与文档风格:写代码、注释、文档、命名、生成物或维护 skill/frontmatter 时查 |
| [`standards/runtime-implementation-boundaries.md`](standards/runtime-implementation-boundaries.md) | 运行时实现边界:写运行时逻辑、配置真相、依赖入口、UI、资源或性能热路径时查 |
| [`standards/unity-serialization-safety.md`](standards/unity-serialization-safety.md) | Unity 序列化安全:修改场景、Prefab、资源、动画、材质、`.meta` 或 ProjectSettings 前查 |
| [`standards/debugging-evidence.md`](standards/debugging-evidence.md) | 排查证据规范:排查 bug、回归、资源缺失、表现异常、日志和链路分段时查 |
| [`standards/skill-conflicts.md`](standards/skill-conflicts.md) | 外部 skill 整合矩阵:查看官方候选 skill 如何在 CardLoop 收口、合并或排除时查 |

## features/(功能设计与记录 · 供了解)

| 文档 | 一句话 |
|------|--------|
| [`features/gamecore-gas.md`](features/gamecore-gas.md) | GAS 官方文档入口、GameCore 与 EX-GAS 的正式集成边界:做 Timeline、GameplayEffect/Cue、TargetCatcher 或正式集成边界时查 |
| [`features/plugin-docs.md`](features/plugin-docs.md) | 第三方插件、本地 UPM 包和外部框架的官方文档入口索引:接入、审查或清理插件 / 本地包时查 |
| [`features/ai-quick/README.md`](features/ai-quick/README.md) | CardLoop AI 速查索引:查询第三方插件、项目公共系统和自有工具类的正式入口、生命周期和职责边界时查 |
| [`features/project/README.md`](features/project/README.md) | CardLoop 项目事实入口:涉及产品愿景、Gameplay 地基、Mod、联机或项目阶段事实时查 |

## lessons(经验教训 · 复发问题暂存区)

| 文档 | 一句话 |
|------|--------|
| [`lessons.md`](lessons.md) | CardLoop 反复经验升级池:同类错误再次发生、判断经验是否应升级为正式规范或 skill 时查 |

---

新增 / 修改 / 维护知识文档(放哪、frontmatter、同步本导航) -> 用 `spec-steward` 技能;决策记录(唯一落点) -> [`../decisions/`](../decisions/README.md)。
