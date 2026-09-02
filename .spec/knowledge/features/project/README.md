---
name: project
description: CardLoop 项目事实入口；登记 CardLoop 自有产品、框架分层、Gameplay 地基、联机和后续文档入口。
metadata:
  type: index
  status: 已交付
---

# Project Facts（项目事实入口）

本文件只登记 CardLoop 自有事实和后续文档入口。旧来源项目、外部示例和历史迁入证据不作为当前项目事实入口。

## 索引

| 文档 | 一句话 |
|---|---|
| [`card-survival-infinite.md`](card-survival-infinite.md) | 判断《卡牌生存：无限》的自有产品支柱、玩法流程、架构约束和后续拆需求边界时查。 |
| [`../../standards/framework-layering.md`](../../standards/framework-layering.md) | 判断 YokiFrame / GameCore / Gameplay 分层、GameCore 特化落点或代码重构前置文档门禁时查。 |
| [`gameplay-foundation-proposal.md`](gameplay-foundation-proposal.md) | 查看 Gameplay 地基总体方案、内容定义、作者源、资源/输入选择和分阶段吸收设计时查。 |
| [`gameplay-foundation-reaudit.md`](gameplay-foundation-reaudit.md) | 重审 Gameplay 地基模块的 OOP、生命周期、作者源、参考等价或 Mod/联机扩展边界时查。 |
| [`networking-protocol.md`](networking-protocol.md) | 判断玩家席位、命令、权威版本、可见快照和 Mirror 接入边界时查。 |

## 已知事实

- 框架总分层必须先查 [`../../standards/framework-layering.md`](../../standards/framework-layering.md)：YokiFrame 是底座，GameCore 是通用游戏解决方案，Gameplay 是 CardLoop 业务层；GameCore 的特化需求优先做扩展点 + Gameplay 实现，不默认下沉到 YokiFrame。
- Gameplay 地基对象模型和外部参考候选裁决必须再查 [`../../standards/gameplay-architecture.md`](../../standards/gameplay-architecture.md)：CardLoop 先按自有产品对象、生命周期和唯一 owner 建模；外部来源只能补证据，不能替代项目本体裁决。
- Unity 工程根目录：`C:\Gamedev\Unity\Project\CardLoop`。
- 当前阶段先完成文档结构、框架分层和自有项目事实收口；代码重构尚未进入。后续 GameCore 启用范围必须按 `framework-layering.md` 做文件级归属审查，不得把具体游戏规则下沉到 GameCore。
- 《卡牌生存：无限》当前作为 CardLoop 自有产品愿景摘要和架构扩展性约束记录，入口见 [`card-survival-infinite.md`](card-survival-infinite.md)；active 入口只保留后续框架重构需要读取的约束。
- 当前主线是打 CardLoop 自有 Gameplay 地基，不是把来源项目搬成当前业务。游戏愿景、联机、Mod、关卡编辑器和职业成长只作为架构裁决约束，未进入当轮实现授权。
- Gameplay 地基提案见 [`gameplay-foundation-proposal.md`](gameplay-foundation-proposal.md)：保留本项目框架，使用 YooAsset 与新 Input System，先建立内容定义 / 加载 / 作者源校验，再按 CardLoop 自有模块顺序推进。
- GameCore 与 EX-GAS 的正式集成边界见 `../gamecore-gas.md`；不得用未登记中转层替代 EX-GAS 正式使用入口。
- 联机后端裁决见 [`../../../decisions/0003-mirror-networking.md`](../../../decisions/0003-mirror-networking.md)：首版未来采用 Mirror，以 2-10 人 Host 房主局和低频权威命令同步为主。
- 联机薄协议骨架见 [`networking-protocol.md`](networking-protocol.md)，执行裁决见 [`../../../decisions/0004-networking-protocol-skeleton.md`](../../../decisions/0004-networking-protocol-skeleton.md)：当前只建立 Mirror 无关的玩家席位、牌桌命令、权威版本、可见快照和命令回执边界，尚未安装 Mirror 或接入真实 Host / Client。
- 历史迁入证据不作为当前入口；插件、本地 UPM 包、GameCore 候选和 AI workflow 是否正式启用，必须按当前仓库源码、文档和当轮验证逐项确认。
- 根目录旧流水账和空占位文档不再作为当前项目事实入口；当前框架分层看 `framework-layering.md`，在途任务看 `.spec/tasks/`。

## 后续文档入口

- 代码阶段的 GameCore / Gameplay 文件级归属审查结果。
- CardLoop 自己的 Unity 工程目录规范。
- 《卡牌生存：无限》的 MVP 用户故事、Mod 作者故事、关卡编辑器作者故事和联机故事。
