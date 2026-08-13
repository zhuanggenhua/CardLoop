---
name: scene-system
description: CardLoop 的场景职责速查：SceneSystem 技术切换、SceneKit 生命周期、剧本场景组合、ResourceSystem 多包选择和旧 MapSystem 边界。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-source
  status: 已交付
  verified_at: 2026-08-11
  update_triggers: yokiframe-version-change, scene-lifecycle-change, resource-package-change, map-transition-change
---

# 场景系统项目入口

## 唯一职责

| 现实职责 | 正式 owner | 直接入口 |
|---|---|---|
| 场景缓存、句柄、加载、卸载、活动场景和预加载 | YokiFrame `SceneKit` | `SceneKit.LoadSceneUniTaskAsync`、`GetSceneHandler` 等官方 API |
| 默认包 / Mod 包选择和 YooAsset 场景句柄 | `GameCore.ResourceSystem` | 内部 `ResourceSystemSceneLoaderPool`，通过 `SceneKit.SetLoaderPool` 直接接入 |
| 技术场景切换和场景生命周期事件 | `GameCore.SceneSystem` | `TransitionToAsync` |
| 剧本初始场景和开局 / 结束返回顺序 | `ScenarioDefinition` + `ScenarioDirector` | `InitialSceneAddress`、`StartScenarioAsync`、`EndScenarioAsync` |
| 旧 2D 地图的检查点、出生点、重生和地图存档 | `GameCore.MapSystem` | 需要换场时调用 `SceneSystem`，不拥有技术场景状态 |
| 淡入淡出表现 | `GameCore.TransitionSystem` | 由 `SceneSystem` 调用，不拥有场景或剧本状态 |
| 事件派发 | YokiFrame `EventKit` | 技术场景事实由 `SceneSystem` 发送 |

`ResourceSystemSceneLoaderPool` 是 YokiFrame 官方 `ISceneLoaderPool` 扩展点的项目实现，只负责在加载发生时选择资源包并持有 YooAsset 场景句柄。它不是第二个场景管理器，也不向 Gameplay 暴露场景句柄。

## 当前流程

`SceneSystem` 串行执行：开始过场 -> 淡出 -> 发送场景卸载 / 加载事实 -> `SceneKit` 以 `Single` 模式加载目标场景 -> 淡入 -> 发布成功并结束过场。StackCraft 的切换时序被吸收，固定场景名和直接 `SceneManager.LoadScene*` 没有进入正式链路。

开始剧本时，`ScenarioDirector` 先解析内容和剧本定义；若 `InitialSceneAddress` 非空，则等待 `SceneSystem` 完成切换，之后才创建并发布 `ScenarioRun`。结束剧本时先让旧局失效并释放本局内容句柄，再通过同一个 `SceneSystem` 返回来源场景。剧本内旅行仍是后续模块，不由 `MapSystem` 或另一个 Gameplay 场景包装提前接管。

剧本的空初始场景地址表示“在当前场景运行”，不会被解释成卸载最后一张场景。场景地址只是 YooAsset 资源定位，不是 Gameplay 内容 ID。

`GameManager` 是 `DontDestroyOnLoad` 的唯一进程宿主。由 `SceneSystem` 加载的普通剧本场景和返回场景不得再配置第二个 `GameManager`；进程宿主场景也不能被当作普通剧本场景重复装入。

Mod 包卸载前必须先切离由该包提供的活动场景；`ResourceSystem` 会拒绝销毁仍被 SceneKit 使用的包，避免场景还在运行时资源包已经消失。

## YokiFrame owner 速查

| 模块 | CardLoop 当前状态 |
|---|---|
| `EventKit` | 正式事件派发 owner；事件类型仍归 `GameCore`。 |
| `SceneKit` | 正式场景生命周期 owner。 |
| `ResKit` | 不再位于正式场景加载链；普通资产仍由 `ResourceSystem` 统一选择默认包 / Mod 包。 |
| `UIKit` | 当前正式面板栈 owner；旧 `UISystem` / `UIManager` 仍是待后续真实消费者裁决的候选。 |
| `SaveKit` | 正式文件槽位、版本和元数据承载层；世界快照真相仍由 `SaveSystem` 聚合。 |
| `InputKit` | 正式重绑定、绑定显示和持久化工具；`GameCore.InputSystem` 仍拥有输入上下文和玩家命令语义。 |
| `AudioKit` | 当前未接管；音频正式入口是 `AudioSystem` + BroAudio，不能因为插件已安装就并行启用。 |

## 禁止旁路

- 不恢复 `SceneResourceHandle`、第二个场景注册表或 Gameplay 场景包装。
- 不在 GameCore / Gameplay 正式运行时代码直接调用 `SceneManager.LoadScene*` 或 `UnloadSceneAsync`。
- 不让 `TransitionSystem` 保存当前地图、资源包或场景句柄。
- 不用自动 `UnloadUnusedAssets` 的 fire-and-forget 钩子替代明确资源生命周期；该钩子会与资源系统关闭并发。

## 源码证据

- YokiFrame owner：[`SceneKit.cs`](../../../../Assets/Plugins/YokiFrame/Tools/SceneKit/Runtime/Public/SceneKit.cs)、[`SceneKit.Load.cs`](../../../../Assets/Plugins/YokiFrame/Tools/SceneKit/Runtime/Public/SceneKit.Load.cs)、[`ISceneLoaderPool.cs`](../../../../Assets/Plugins/YokiFrame/Tools/SceneKit/Runtime/Contracts/Interface/ISceneLoaderPool.cs)。
- 项目接入：[`ResourceSystemSceneLoaderPool.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystemSceneLoaderPool.cs)、[`ResourceSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystem.cs)。
- 技术切换与过场：[`SceneSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/SceneSystem.cs)、[`TransitionSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/TransitionSystem.cs)。
- 剧本组合：[`ScenarioDefinition.cs`](../../../../Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDefinition.cs)、[`ScenarioDirector.cs`](../../../../Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDirector.cs)。
- PlayMode 验证：[`ScenarioContentPlayModeTests.cs`](../../../../Assets/Tests/PlayMode/ScenarioContentPlayModeTests.cs)。
