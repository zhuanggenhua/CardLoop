# 0001 · ai-rules 吸收裁决

- 日期:2026-08-14
- 状态:生效

## 背景

外部来源 `C:\Users\zhuagenbao\Desktop\tesmp\ai-rules` 准备吸收到 CardLoop `.spec`。本轮已读 `README.md`、`00-sources-and-routing.md`、`10-engineering-principles.md` 全章节、`20-unity-serialization-safety.md`、`30-workflow-and-verification.md`、`40-git-workspace-and-docs.md`、`50-terminology-and-bans.md`、`60-skill-rules.md`。

CardLoop 只吸收可复用工程原则、规范分层方法和可验证工作流;旧项目路径、Phase、MiniFrame、Legacy Input、ChineseLabel、旧测试约束和还原专项事实不作为 CardLoop 正式事实。

## 决策

| 来源 | CardLoop 落点 | 裁决 |
|---|---|---|
| `00-sources-and-routing.md` | `.spec/AGENTS.md`、[`knowledge/README.md`](../knowledge/README.md)、`.spec/skills/spec-steward` | 改写吸收“入口轻、知识路由、规则分层、经验归类”。明确 `knowledge/README.md` 是知识导航,旧项目专用入口、旧阶段计划口径不接管。 |
| `10-engineering-principles.md` 的设计原则、SOLID、设计模式、反模式 | [`knowledge/standards/code-design.md`](../knowledge/standards/code-design.md) | 改写吸收为 CardLoop 的代码设计原则入口,接入 Gameplay 架构、Unity patterns、architecture、scriptdesign 和 improve-codebase-architecture skill。 |
| `10-engineering-principles.md` 的配置真相、依赖入口、运行时边界、UI、资源、性能、禁止补丁式实现 | [`knowledge/standards/runtime-implementation-boundaries.md`](../knowledge/standards/runtime-implementation-boundaries.md) | 改写吸收单一真相、正式依赖入口、缺失暴露、UI/资源/性能边界,剔除旧项目具体组件和路径。 |
| `10-engineering-principles.md` 的证据链、日志、资源本体、视觉证据、链路分段 | [`knowledge/standards/debugging-evidence.md`](../knowledge/standards/debugging-evidence.md)、`.spec/skills/systematic-debugging` | 改写吸收。证据分层和链路分段放入长期规范,排查 skill 显式路由。 |
| `10-engineering-principles.md` 的注释、编码、文档命名、工具链调研 | [`knowledge/standards/code-style.md`](../knowledge/standards/code-style.md)、[`knowledge/standards/debugging-evidence.md`](../knowledge/standards/debugging-evidence.md) | 既有规范大体覆盖;本轮只吸收“先静态证据后动态取证”“不凭工具便利切场景”等缺口到排查证据链,旧中文特性方案不接管。 |
| `20-unity-serialization-safety.md` | [`knowledge/standards/unity-serialization-safety.md`](../knowledge/standards/unity-serialization-safety.md) | 改写吸收为 Unity 序列化安全标准,覆盖 YAML 结构守卫、Prefab/场景 override、资源本体核对和写入后回读。 |
| `30-workflow-and-verification.md` | [`knowledge/standards/debugging-evidence.md`](../knowledge/standards/debugging-evidence.md)、`.spec/skills/verification-before-completion` | 改写吸收“静态/动态证据能力匹配”“知识正文必须读取”的缺口;既有 testing 规范继续承载测试分层。 |
| `40-git-workspace-and-docs.md` | `.spec/rules/system.md`、[`knowledge/standards/unity-serialization-safety.md`](../knowledge/standards/unity-serialization-safety.md) | 只吸收与当前项目不冲突的写入前回读、保护他人改动、编码/脚本谨慎原则。原文中“批量写入前提交”与本项目“未经授权不提交”冲突,未接管。 |
| `50-terminology-and-bans.md` | `.spec/rules/system.md`、[`knowledge/standards/code-style.md`](../knowledge/standards/code-style.md) | 只吸收“先解释现实含义再提内部符号”的通用沟通原则;旧 Inspector 中文特性方案与 CardLoop Odin 口径冲突,未接管。 |
| `60-skill-rules.md` | `.spec/skills/spec-steward` | 已由 spec-steward 的 skill 落点、frontmatter、索引同步和单一权威规则承接;不复制正文。 |

## 后果

未接管内容:

- 旧项目的 MiniFrame、Legacy Input、Phase 1、旧项目专用规则入口、旧 Inspector 中文特性方案、旧测试框架限制、旧工具包装脚本和旧目录命名。
- 绑定旧项目路径、场景、Prefab、资源、平台 SDK 或还原阶段的事实。
- 与 CardLoop 当前硬红线冲突的 Git 提交流程、编辑器桥接工具默认选择和项目专用验证入口。

禁止口径:

- 不把外部 `ai-rules` 目录称为 CardLoop 的正式事实来源;它只是本轮用户指定的参考源。
- 不把旧项目的 MiniFrame、Legacy Input、Phase 1、旧项目专用规则入口、ChineseLabel 或旧测试框架限制迁入 CardLoop。
- 不整段覆盖现有 `.spec`;所有吸收必须先按当前项目职责改写,并同步索引。
