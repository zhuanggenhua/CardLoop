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

- Unity 工程根目录：`C:\Gamedev\Unity\Project\CardLoop`。
- 当前阶段是 GameCore 通用框架搭建，不是具体游戏玩法落地；GameCore 的默认入口和默认持久化名不得绑定当前 Unity 工程名。
- 《卡牌生存：无限》当前作为 CardLoop 的项目愿景草案记录，入口见 [`card-survival-infinite.md`](card-survival-infinite.md)。
- GameCore 与 EX-GAS 的正式集成边界见 `../gamecore-gas.md`；不得用未登记桥接替代 EX-GAS 正式使用入口。
- 已静态迁入 FantasyWord 的插件、本地 UPM 包、GameCore 候选和 AI workflow。
- 迁移清单见 `../../../../docs/FantasyWord-framework-migration.md`。

## 待沉淀

- CardLoop 自己的 Unity 工程目录规范。
- CardLoop 自己的 GameCore 启用范围。
- CardLoop 自己的输入、场景、资源、测试和验收入口。
- 《卡牌生存：无限》的 MVP 用户故事、Mod 作者故事、关卡编辑器作者故事和联机故事。
