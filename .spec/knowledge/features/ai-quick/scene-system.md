---
name: scene-system
description: CardLoop 的场景职责速查：SceneKit 生命周期、MapSystem 地图语义、ResourceSystem 多包选择和 TransitionSystem 视觉过渡边界。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-source
  status: 已交付
  verified_at: 2026-08-04
  update_triggers: yokiframe-version-change, scene-lifecycle-change, resource-package-change, map-transition-change
---

# 场景系统项目入口

## 唯一职责

| 现实职责 | 正式 owner | 直接入口 |
|---|---|---|
| 场景缓存、句柄、加载、卸载、活动场景和预加载 | YokiFrame `SceneKit` | `SceneKit.LoadSceneUniTaskAsync`、`GetSceneHandler` 等官方 API |
| 默认包 / Mod 包选择 | `GameCore.ResourceSystem` | 内部 `ResourceSystemSceneLoaderPool`，通过 `ResKit.SetSceneLoaderPool` 接入 |
| 当前地图地址、检查点、出生点和地图事件 | `GameCore.MapSystem` | `RequestTransitionAsync`、`GetCurrentSceneAddress` |
| 淡入淡出表现 | `GameCore.TransitionSystem` | 实现 `ISceneTransitionUniTask`，不拥有场景状态 |
| 事件派发 | YokiFrame `EventKit` | 地图与过场事实由 `MapSystem` 发送 |

`ResourceSystemSceneLoaderPool` 是 YokiFrame 官方 `ISceneResLoaderPool` 扩展点的项目实现，只负责在加载发生时选择资源包并复用官方 YooAsset 场景加载器。它不是第二个场景管理器，也不向业务层暴露场景句柄。

## 当前流程

`MapSystem` 串行执行：锁定过场 -> 淡出 -> 发送旧地图卸载 / 新地图加载事实 -> `SceneKit` 以 `Single` 模式加载目标场景 -> 更新地图状态 -> 淡入 -> 解锁过场。StackCraft 的旅行时序被吸收，固定场景名和直接 `SceneManager.LoadScene*` 没有进入正式链路。

空场景地址表示“继续使用当前编辑器 / Playtest 场景”，不会被解释成卸载最后一张场景。正式返回主菜单或切换世界时必须提供明确场景地址。

Mod 包卸载前必须先切离由该包提供的活动场景；`ResourceSystem` 会拒绝销毁仍被 SceneKit 使用的包，避免场景还在运行时资源包已经消失。

## YokiFrame owner 速查

| 模块 | CardLoop 当前状态 |
|---|---|
| `EventKit` | 正式事件派发 owner；事件类型仍归 `GameCore`。 |
| `SceneKit` | 正式场景生命周期 owner。 |
| `ResKit` | 场景加载后端扩展入口；普通资产仍由 `ResourceSystem` 统一选择默认包 / Mod 包。 |
| `UIKit` | 正式菜单面板栈 owner；`UISystem` / `UIManager` 承担项目 UI 语义和实例生命周期。 |
| `SaveKit` | 正式文件槽位、版本和元数据承载层；世界快照真相仍由 `SaveSystem` 聚合。 |
| `InputKit` | 正式重绑定、绑定显示和持久化工具；`GameCore.InputSystem` 仍拥有输入上下文和玩家命令语义。 |
| `AudioKit` | 当前未接管；音频正式入口是 `AudioSystem` + BroAudio，不能因为插件已安装就并行启用。 |

## 禁止旁路

- 不恢复 `SceneResourceHandle`、第二个场景注册表或 GamePlay 场景包装。
- 不在 GameCore / GamePlay 正式运行时代码直接调用 `SceneManager.LoadScene*` 或 `UnloadSceneAsync`。
- 不让 `TransitionSystem` 保存当前地图、资源包或场景句柄。
- 不用自动 `UnloadUnusedAssets` 的 fire-and-forget 钩子替代明确资源生命周期；该钩子会与资源系统关闭并发。

## 源码证据

- YokiFrame owner：[`SceneKit.cs`](../../../../Assets/Plugins/YokiFrame/Tools/SceneKit/Runtime/Public/SceneKit.cs)、[`SceneKit.Load.cs`](../../../../Assets/Plugins/YokiFrame/Tools/SceneKit/Runtime/Public/SceneKit.Load.cs)、[`ISceneResLoader.cs`](../../../../Assets/Plugins/YokiFrame/Core/Runtime/ResKit/Loader/Interface/ISceneResLoader.cs)。
- 项目接入：[`ResourceSystemSceneLoaderPool.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystemSceneLoaderPool.cs)、[`ResourceSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystem.cs)。
- 地图与过场：[`MapSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/MapSystem.cs)、[`TransitionSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/TransitionSystem.cs)。
- PlayMode 验证：[`GamePlayContentLoadingPlayModeTests.cs`](../../../../Assets/Tests/PlayMode/GamePlayContentLoadingPlayModeTests.cs)。
