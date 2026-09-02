# 0002 · 联机后端裁决:FishNet

- 日期:2026-08-13
- 状态:被 0003 取代

## 背景

CardLoop 未来需要合作生存、叛徒、资源和行动结算的权威状态,同时保留单机优先、Mod 和关卡内容高度扩展的产品边界。候选方案包括 FishNet、Unity Netcode for GameObjects 和 Photon Fusion。

选择依据:

- FishNet 官方能力覆盖服务器权威、监听服务器、独立服务器、场景同步以及 `Replicate` / `Reconcile` 客户端预测与服务器校正。
- CardLoop 需要主机优先的早期形态,并保留后续专用服务器形态。
- 项目不是以高频移动为核心的竞技游戏,不需要为了牌桌行动引入全量预测或 ECS 网络模型。
- FishNet 不把项目绑定到 Photon 云服务和 CCU 计费模型。

## 决策

CardLoop 未来的正式 Unity 联机后端采用 FishNet。保留 FishNet 的客户端预测能力,但预测不是所有玩法的默认执行方式。

权威边界:

- 牌桌卡牌位置、工位归属、行动计划、回合确认、行动进度和结算结果由主机 / 服务器确认。
- 资源、角色状态、EX-GAS 能力和 GameplayEffect 结果由主机 / 服务器确认。
- 随机种子、掷骰结果、叛徒秘密状态、可见性和 Mod / 内容包版本由主机 / 服务器确认。
- 客户端可以生成拖拽预览、行动候选和本地表现,但不能直接写上述权威状态。

客户端预测边界:

- 未来即时战斗中确实需要高频同步的移动、瞄准等表现时,按需使用 FishNet 的 `Replicate` / `Reconcile`。
- 预测只负责临时表现和输入执行,不复制一套伤害、资源、命中或技能结算规则。
- 卡牌拖拽的本地预览可以是表现预测,但最终放置仍提交给牌桌权威对象复核。

## 后果

当前不做:

- 不立即安装或接入 FishNet。
- 不提前创建 `NetworkBehaviour`、RPC、`NetworkVariable`、`PlayerId`、权限表或网络 DTO。
- 不把现有 `GameCommandContext.RemotePlayer` 当作联机协议。

接入前提:

- 只有在玩家席位、可控制角色集合、队长授权、叛徒可见性、主机 / 服务器模式和断线策略形成正式事实后,才开始接入 FishNet。
- 接入时先让 FishNet 承担连接、生成、同步和生命周期,再把权威命令接入现有 `ScenarioRun` / `Tabletop` 聚合;网络组件不能接管玩法规则。

资料依据:

- FishNet 官方文档:服务器权威、监听服务器、独立服务器,以及 `Replicate` / `Reconcile` 客户端预测与服务器校正。
- Unity Netcode for GameObjects 官方文档:服务器、主机、客户端、连接审批和网络场景管理。
- Photon Fusion 官方资料:候选对比来源;不采用其云服务绑定作为 CardLoop 当前正式后端。
