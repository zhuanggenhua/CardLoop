---
name: ai-quick-reference
description: CardLoop AI 速查索引：先定位插件官方文档，再定位项目实际使用的公共入口、生命周期和职责边界。
metadata:
  type: index
  role: index
  source: official-entry-index + project-source
  status: 已交付
  verified_at: 2026-08-04
  update_triggers: package-version-change, public-entry-change, lifecycle-change, generator-change
---

# AI 速查入口

本目录只解决两个查询问题：插件官方能力在哪里，以及 CardLoop 当前应该调用哪个项目正式入口。它不是第三方插件 API 手册，也不替项目源码创造新的包装层。

## 查询顺序

1. 先按任务进入下表对应的官方入口或项目入口。
2. 第三方插件先读 [`plugin-docs.md`](../plugin-docs.md) 指向的 README、Wiki、SKILL、`package.json` 和官方仓库。
3. 只有需要确认 CardLoop 自己的所有权、生命周期、启动顺序、项目约束或偏离时，才读对应项目卡。
4. 项目卡与源码冲突时，以当前源码为准；官方文档与插件源码冲突时，以当前插件版本源码为准。

## 任务路由

| 现实任务 | 官方能力入口 | CardLoop 项目入口 | 项目卡状态 |
|---|---|---|---|
| GameplayTag、Ability、Effect、Attribute、Cue、ASC、Timeline、MMC、生成器 | [`EX-GAS SKILL`](../../../../Assets/Plugins/GAS/SKILL.md) → [`EX-GAS 总导航`](../../../../Assets/Plugins/GAS/Wiki/EX-GAS.md) | [`ex-gas-runtime.md`](ex-gas-runtime.md)：本地 2.0.4 项目校准 | 已按官方 2.0 README 与本地源码校准 |
| 资源地址加载、Prefab 实例化、句柄释放、Mod 资源包 | [YooAsset 官方入口](../../../../Packages/com.tuyoogame.yooasset/README.md) | [`resource-system.md`](resource-system.md)：`GameCore.ResourceSystem` | 已补项目事实 |
| 场景加载、地图切换、活动场景、过场表现 | [YokiFrame SceneKit 官方入口](../../../../Assets/Plugins/YokiFrame/Tools/SceneKit/Editor/Documentation/SceneKitDocData.cs) | [`scene-system.md`](scene-system.md)：`SceneKit` + `MapSystem` + `TransitionSystem` | 已补项目事实 |
| Unity 异步、取消、帧等待、并发解压 | [UniTask 官方入口](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/unitask-design/SKILL.md) | [`async-runtime.md`](async-runtime.md)：当前项目实际调用 | 已补项目事实 |
| 强类型事件、UI 请求、表现通知 | [YokiFrame EventKit 官方入口](../../../../Assets/Plugins/YokiFrame/Core/Editor/Documentation/Core/EventKit/EventKitDocData.cs) | [`event-system.md`](event-system.md)：`GameCore` 事件类型 + 直接 `EventKit.Type` | 已补项目事实 |
| 音效、BGM、通道、暂停、停止、BroAudio SoundID | [BroAudio 官方入口](../../../../Packages/com.ami.broaudio/Documentation~/Documentation.txt) | [`audio-system.md`](audio-system.md)：`AudioSystem` / `AudioClipResolver` / `AudioChannel` | 已补项目事实 |
| Mod 扫描、版本校验、启停状态、内容包加载 | [YooAsset 官方入口](../../../../Packages/com.tuyoogame.yooasset/README.md) | [`mod-system.md`](mod-system.md)：`ModAPI` / `ModLoader` | 项目自有能力 |
| ScriptableObject 数据、稳定引用、可序列化字典、目标冷却存档 | [本地包入口](../../../../Assets/Plugins/azixMcAze.SerializableDictionary/package.json) | [`data-serialization.md`](data-serialization.md)：`DatabaseRegistry` / `PerTargetCooldown<T>` | 项目自有能力 |
| YokiFrame UIKit 面板栈 | [YokiFrame 官方入口](../../../../Assets/Plugins/YokiFrame/Core/Editor/AI_NAVIGATION.md) | `GameCore.UIManager` / `UIKit` | 正式 owner 已确认 |
| 输入上下文、重绑定和绑定持久化 | [Unity Input System / YokiFrame InputKit 入口](../plugin-docs.md) | `GameCore.InputSystem` + `InputKit` | 正式 owner 已确认 |
| 存档文件槽位、版本和世界快照 | [YokiFrame SaveKit 入口](../../../../Assets/Plugins/YokiFrame/Core/Editor/AI_NAVIGATION.md) | `SaveSystem` 聚合世界快照，`SaveKit` 负责文件层 | 正式 owner 已确认 |
| Cinemachine、DOTween、Tilemap、Addressables | [UnitySkills / 包内官方入口](../plugin-docs.md) | 先确认当前业务是否有正式调用 | 已装或被工具覆盖，未确认统一项目入口 |
| UnitySkills、AIBridge、puerts-unity-mcp、batchmode 等 Unity 自动化工具 | [插件入口索引](../plugin-docs.md) | [`unity-automation-tools.md`](unity-automation-tools.md)：默认入口、专项场景、禁止场景和 guard | 已补项目职责矩阵 |

## 文档分层

- **插件官方文档**：解释插件本身的 API、能力和通用生命周期。
- **项目速查卡**：只记录 CardLoop 的正式入口、项目拥有的事件/数据契约、启动与释放顺序、项目禁止旁路和源码证据。
- **项目规范**：硬红线仍以 [`.spec/rules/system.md`](../../../rules/system.md) 为准；速查卡只引用并说明当前入口，不复制硬规则。
- **源码**：所有示例和结论的最终事实源。

如果官方文档已经完整覆盖某个能力，项目只在总索引中保留链接；只有出现项目专属入口、项目专属生命周期或职责冲突时，才建立项目卡。

## 自有工具纳入标准

只有跨模块、公开、具有生命周期或身份所有权的入口才单独建卡。当前纳入资源句柄、资源缓存、软资源引用、数据库稳定引用、Mod API、音频通道、事件定义和目标冷却存档。

私有 helper、纯转发方法、临时测试工具和没有稳定职责的迁移残留不创建第二套文档入口，直接回到拥有它的源码。

## 更新协议

每张卡的 frontmatter 记录 `verified_at` 和 `update_triggers`。发生以下变化时，只更新受影响的项目事实，并重新检查官方入口链接和源码符号：

- 包版本、官方仓库、本地 README 或插件内置文档变化。
- 项目公开类、公开方法、初始化顺序、释放规则或事件/数据契约变化。
- 资源地址、稳定 ID、Mod 包、生成器或作者源变化。
- 发现项目卡重复了官方手册，或卡片描述的入口已不再是实际调用入口。

## 当前真实缺口

- EX-GAS 当前没有源码证据证明支持 Mod 在运行时动态合并 GameplayTag 作者表；详见 [`GameplayTag.md`](../../../../Assets/Plugins/GAS/Wiki/GameplayTag.md)。
- `ResourceSystem`、`ModAPI` 和 `FormalAbilityRuntimeBootstrap` 已有启动顺序，但部分公开入口在未初始化时直接抛异常，没有统一的查询前保护协议。
- UniTask 包当前是 `2.5.11`，迁入的 `unitask-design` 仍标注 `2.5.10`，涉及版本特有行为时需要回到当前包源码。
- Cinemachine、DOTween 等已有官方入口，但尚未全部形成项目事实卡；安装事实不等于正式业务能力。
