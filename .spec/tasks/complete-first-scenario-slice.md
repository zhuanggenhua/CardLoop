---
status: pending
---

# 完成首个最小可运行剧本

先产出一个内置白盒剧本样本，用它验证核心玩法循环，并锁定关卡编辑器第一版必须服务的真实内容生产需求。本卡不实现关卡编辑器。

提案入口：`openspec/first-scenario-whitebox-flow/proposal.md`

## 涉及范围

- `Assets/Gameplay/Scenarios/首个内置剧本.asset`
- `Assets/Gameplay/Scenarios/首个内置地区.asset`
- `Assets/Gameplay/Scenarios/首个内置任务.asset`
- `Assets/Gameplay/Scenarios/首个内置行动.asset`
- `Assets/Gameplay/Scenarios/首个内置角色.asset`
- `Assets/Gameplay/Scenarios/首个内置地点.asset`
- `Assets/Gameplay/Scenarios/首个内置资源.asset`
- `Assets/Art/Textures/白盒卡面占位.png`
- `Assets/Art/Materials/白盒卡面占位.mat`
- `Assets/Gameplay/Scenarios/首个剧本编辑器需求记录.md`
- `Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDefinition.cs`
- `Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioRegionDefinition.cs`

## 验收标准

- [ ] 首个内置剧本为 `寒夜撤离`，包含一个初始地区、一个地区列表、至少一个任务目标和一组可启动的牌桌初始内容。
- [ ] 开局牌桌至少包含 2 名角色、1 个基地、2 个地点、3 类资源或事件卡，全部使用白盒卡面占位加文本。
- [ ] 玩家能完成一次“派角色探索或搜刮地点 -> 生成资源或事件 -> 推进任务”的闭环。
- [ ] 剧本作者资产通过现有内容校验，所有内容 ID、地区引用、任务引用和牌桌放置规则有效。
- [ ] 剧本能通过现有 `ScenarioDirector` 真实启动，进入初始地区并显示可交互牌桌。
- [ ] 记录关卡编辑器第一版必须覆盖的实际字段，字段来自本剧本的真实制作过程。

## 依赖

无

## 接口

Consumes: 无。

Produces: 首个内置剧本资产路径、真实启动入口、内容校验证据、关卡编辑器第一版字段清单。
