---
name: networking-protocol
description: CardLoop 联机薄协议骨架：判断玩家席位、命令、权威版本、可见快照和 Mirror 接入边界时查。
metadata:
  type: doc
  status: 已交付
---

# 联机薄协议骨架

CardLoop 首版联机后端采用 Mirror，但 Mirror 只承担连接、玩家对象、远程调用和状态同步。Gameplay 玩法层不直接依赖 Mirror，先通过项目侧薄协议表达玩家请求和房主 / 服务器确认。

## 当前代码入口

| 职责 | 当前入口 |
|---|---|
| 玩家席位 | `Assets/Scripts/Gameplay/Runtime/Networking/PlayerSeat.cs` |
| 权威状态版本 | `Assets/Scripts/Gameplay/Runtime/Networking/AuthorityRevision.cs` |
| 牌桌命令信封与预测口径 | `Assets/Scripts/Gameplay/Runtime/Networking/TabletopCommandProtocol.cs` |
| 权威快照可见范围 | `Assets/Scripts/Gameplay/Runtime/Networking/AuthoritySnapshotEnvelope.cs` |
| 命令确认 / 拒绝回执 | `Assets/Scripts/Gameplay/Runtime/Networking/TabletopCommandReceipt.cs` |
| 协议边界测试 | `Assets/Editor/Gameplay/Tests/NetworkingProtocolEditModeTests.cs` |

## 边界

- `Gameplay.Networking` 是项目协议层，不是 Mirror 适配层；当前不得引用 Mirror。
- Mirror 后续只在适配器中出现 `NetworkBehaviour`、`Command`、`SyncVar`、`ClientRpc`、`TargetRpc` 或自定义网络消息。
- `ScenarioRun`、`Tabletop`、行动实例、任务日志、战斗对象和内容 / Mod 版本边界继续拥有玩法规则。
- 玩家席位不是 Mirror 连接 ID；Mirror 连接只绑定到某个 `PlayerSeatId`。
- 权威状态版本用于拒绝过期命令和对齐客户端快照，不替代存档版本、内容版本或 Mod 包版本。
- 公开状态和指定席位私有状态必须分开发送；手牌、秘密目标、叛徒信息和隐藏检定不得放入公开快照。

## 预测口径

- 本地表现预测：允许拖拽预览、候选高亮和等待确认 UI，不提交权威状态。
- 确定性状态预测：只允许后续已证明不读随机、不碰隐藏信息且可回滚 / 重放的低频命令。
- 等待权威确认：抽牌、日终、随机结算、隐藏身份、秘密目标、战斗随机和内容 / Mod 版本失败默认走这个口径。

## 后续接入顺序

1. 先补玩家席位与控制权事实：谁能控制哪些卡、旁观者能做什么、队友共享到什么程度。
2. 再补 Mirror 最小适配器：Host 建房、Client 加入、连接绑定席位、发送一个测试命令、接收一个测试快照。
3. 再把真实 `Tabletop` 命令接到协议入口：移动牌堆、移动单张卡、启动行动、确认回合、处理日终。
4. 最后按体验需要补乐观预测：先表现级，再确定性状态级；随机和隐藏信息默认不预测。
