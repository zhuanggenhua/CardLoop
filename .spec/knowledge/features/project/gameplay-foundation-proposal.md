---
name: gameplay-foundation-proposal
description: Gameplay 地基提案摘要：查看当前地基方向、模块顺序、参考来源取舍和代码阶段前置条件时查。
metadata:
  type: feature
  status: 已交付
---

# Gameplay 地基提案摘要

本文件只保留当前仍有效的地基方向；历史提案不在工作区另设中间文档，追溯以 git 历史为准。

## 当前方向

- CardLoop 使用自有 `Gameplay -> GameCore -> YokiFrame` 分层；唯一分层口径见 [`../../standards/framework-layering.md`](../../standards/framework-layering.md)。
- 外部项目只作为候选参考，不作为 CardLoop 业务知识库、运行时框架或源码 owner。
- 当前优先目标是文档结构、框架分层和参考边界收口；代码重构尚未进入。
- 《卡牌生存：无限》记录产品愿景和扩展性压力，不是当前实现清单。
- 激进重构的前提是先列清旧合同、目标 owner、删除条件和验证入口；不得用长期 adapter 或兼容壳保留旧职责。

## 模块顺序

| 顺序 | 模块 | 当前用途 |
|---:|---|---|
| 1 | 内容作者源 / 内容发现 / 校验 | 定义稳定内容 ID、SO 作者源、资源引用、EX-GAS 标签边界和运行时索引。 |
| 2 | 启动流程 / 系统协作 / 单局状态 | 区分进程级 GameCore 服务和 Gameplay 单局聚合。 |
| 3 | Stackable Card Runtime / Card View | 承接卡牌、牌堆、拖拽、拆堆、合堆、放置和表现投影。 |
| 4 | 行动选择 / 配方条件 / 桌面行动进度 | 管理候选、请求、行动实例、进度和原子结果提交。 |
| 5 | Scenario / Quest / World Rules | 管理剧本、任务、回合、日期、遭遇和世界规则事实。 |
| 6 | Economy / Pack / Trading | 在需要时承接卡包、市场、付款和卖卡闭环。 |
| 7 | Combat / Stats / Equipment | EX-GAS 承接属性、效果和技能；Gameplay 只持有牌桌战斗生命周期和玩家效果。 |
| 8 | UI Framework / Authoring Tools | 只接入当前正式 UI 与作者工具，不照搬外部项目 UI 结构。 |
| 9 | Save / Runtime Restore | 由 GameCore 通用存档能力提供槽位和版本，Gameplay 提供具体单局 / 局外事实。 |
| 10 | Multiplayer Constraints | 约束控制权、同步、可见性、随机、秘密目标和重连协议，不提前接入真实网络运行时。 |

## 代码阶段前置

- 文档单一口径、索引和任务卡已收口。
- 待重构文件已经按 `framework-layering.md` 完成文件级归属审查。
- 旧参考职责、CardLoop 当前职责、目标 owner、删除 / 迁移条件和验证入口已经写清。
- `node .spec/tools/spec-lint.mjs` 通过。
