---
name: project
description: CardLoop 项目事实入口；当前只记录迁移后待沉淀的项目事实，不接管来源项目业务知识库。
metadata:
  type: index
  status: 已交付
---

# Project Facts（项目事实入口）

当前只登记迁移后的事实入口，不承载 FantasyWord 的旧业务知识库。

## 已知事实

- Gameplay 地基对象模型和参考吸收裁决必须先查 ../../standards/gameplay-architecture.md：StackCraft 主要证明卡牌/牌堆/牌桌交互对象模型，2DRPGEngine 主要证明 RPG 数据、地图、存档、任务、对话、命令和技能族闭包，不能混用成一套平铺系统。
- Unity 工程根目录：`C:\Gamedev\Unity\Project\CardLoop`。
- 当前阶段是 GameCore 通用框架搭建，不是具体游戏玩法落地；GameCore 的默认入口和默认持久化名不得绑定当前 Unity 工程名。
- 《卡牌生存：无限》当前作为 CardLoop 的项目愿景草案和架构扩展性约束记录，入口见 [`card-survival-infinite.md`](card-survival-infinite.md)；2026-08-02 已以最新附件为主，同步局外准备、局内生存、成长带出、多世界、联机、Mod、跑团工具、职业经历、混合回合/即时制、UI 和荒岛流程补充。
- StackCraft 模板当前作为参考模板和候选底座保留；设置恢复、吸收裁决与后续约束见 [`stackcraft-template-study.md`](stackcraft-template-study.md)。
- 当前主线是打 Gameplay 地基与 StackCraft 架构搬迁 / 吸收审查，不是先实现《卡牌生存：无限》的原创业务内容；游戏愿景、联机、Mod、关卡编辑器和职业成长只作为架构裁决约束。
- StackCraft 模块吸收审查表见 [`stackcraft-system-reference-matrix.md`](stackcraft-system-reference-matrix.md)：按依赖顺序记录数据定义优先、逐模块重构裁决、UI 框架吸收、联机适配约束、可吸收职责、必须排除的旧职责和临时适配删除条件。
- Gameplay 地基提案见 [`gameplay-foundation-proposal.md`](gameplay-foundation-proposal.md)：保留本项目框架，使用 YooAsset 与新 Input System，先建立内容定义 / 加载 / 作者源校验，再按 StackCraft 架构搬迁顺序逐块吸收。
- GameCore 与 EX-GAS 的正式集成边界见 `../gamecore-gas.md`；不得用未登记中转层替代 EX-GAS 正式使用入口。
- 已静态迁入 FantasyWord 的插件、本地 UPM 包、GameCore 候选和 AI workflow。
- 迁移清单见 `../../../../docs/FantasyWord-framework-migration.md`。

## 待沉淀

- CardLoop 自己的 Unity 工程目录规范。
- CardLoop 自己的 GameCore 启用范围。
- CardLoop 自己的输入、场景、资源、测试和验收入口。
- 《卡牌生存：无限》的 MVP 用户故事、Mod 作者故事、关卡编辑器作者故事和联机故事。
