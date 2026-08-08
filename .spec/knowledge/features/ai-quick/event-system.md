---
name: event-system
description: CardLoop 的 YokiFrame EventKit 项目用法：事件类型归属、直接调用入口、生命周期和禁止重复事件总线。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-source
  status: 已交付
  verified_at: 2026-08-04
  update_triggers: yokiframe-version-change, event-contract-change, lifecycle-change
---

# YokiFrame EventKit 项目用法

## 用途

项目模块需要发布跨模块事实、表现通知或确实需要多方观察的语义时，直接使用 YokiFrame `EventKit`。事件只传递语义，不拥有属性、存档、能力、资源或 UI 的领域真相。

只有一个明确执行者的一对一必需协作使用正式方法调用；不要为了隔离调用方，把可变回调塞进请求事件。当前地图切换由 `MapSystem` 直接调用 `TransitionSystem` 的 SceneKit 过渡接口，地图和整段过场事实都由真正拥有流程的 `MapSystem` 通过 EventKit 广播。

## 官方文档入口

YokiFrame 已提供 EventKit 官方文档，不在本卡复制完整 API：

- 文档总注册：[`EventKitDocData.cs`](../../../../Assets/Plugins/YokiFrame/Core/Editor/Documentation/Core/EventKit/EventKitDocData.cs)。
- 类型事件：[`EventKitDocType.cs`](../../../../Assets/Plugins/YokiFrame/Core/Editor/Documentation/Core/EventKit/EventKitDocType.cs)。
- 枚举、字符串、EasyEvent、通道和高级用法：同目录下的 `EventKitDocEnum.cs`、`EventKitDocString.cs`、`EventKitDocEasyEvent.cs`、`EventKitDocChannel.cs`、`EventKitDocAdvanced.cs`。
- Runtime API：[`EventKit.cs`](../../../../Assets/Plugins/YokiFrame/Core/Runtime/EventKit/EventKit.cs)、[`TypeEvent.cs`](../../../../Assets/Plugins/YokiFrame/Core/Runtime/EventKit/EventSystem/TypeEvent.cs)。
- 编辑器监控和扫描：`Core/Editor/ToolsWindow/Pages/Kits/EventKit/` 下的 `EventKitToolPage`。

## 项目正式入口

这不是“项目适配器”：业务代码直接调用 `YokiFrame.EventKit.Type`。

| 现实职责 | 项目所有者 | 直接入口 |
|---|---|---|
| 事件数据类型 | `GameCore` | `Runtime/Events/GameCore*Events.cs` |
| 发送 | 发送方 | `EventKit.Type.Send(new TEvent(...))` |
| 监听 | 消费方 | `EventKit.Type.Register<TEvent>(handler)` |
| 注销 | 消费方 | `EventKit.Type.UnRegister<TEvent>(handler)` |
| 类型选择 | CardLoop 约束 | 新项目代码优先使用类型事件；不要新增字符串事件。 |

地图和读档生命周期同样直接使用该入口：`MapSystem` 发送地图加载 / 卸载与过场开始 / 完成事件，`SaveSystem` 在完整恢复后发送 `SaveFileLoadedEvent`；`TransitionSystem` 只实现视觉过渡，不重复发布地图流程事件。

菜单打开、关闭和返回主菜单是单一 UI / 游戏状态执行者的直接调用，不再通过携带 `TaskCompletionSource` 或请求事件绕路。音频请求仍是表现层广播，因为多个世界对象可以独立发起而不需要等待音频系统结果。

项目事件类型不是 YokiFrame 的插件类型，而是 `GameCore` 自己拥有的领域契约。YokiFrame 只提供派发机制。

## 生命周期

监听方在启用时注册，在禁用或销毁时注销。`AudioSystem.OnSystemStart` / `OnSystemStop`、`InputSystem` 和各种场景表现组件按此方式管理监听；`TransitionSystem` 不监听请求事件，由 `MapSystem` 直接调用。

发送方只在请求确实发起或结果确实成立时发送。事件监听不能替代正式状态写入、权限判断、存档或属性结算。

## 最小真实示例

下面的事件和调用均来自当前项目：

```csharp
using Cysharp.Threading.Tasks;
using YokiFrame;

private void OnEnable()
{
    EventKit.Type.Register<MapLoadedEvent>(OnMapLoaded);
}

private void OnDisable()
{
    EventKit.Type.UnRegister<MapLoadedEvent>(OnMapLoaded);
}

private void OnMapLoaded(MapLoadedEvent _)
{
    // 只响应地图已经稳定的事实，不在事件里保存另一份地图状态。
}

private async UniTask RequestMapTransition(MapSystem mapSystem)
{
    await mapSystem.RequestTransitionAsync("FoundationMapTest");
}
```

## 常见错误

- 注册后没有注销，导致对象销毁后仍被全局 EventKit 调用。
- 新增代码使用 `EventKit.String` 或自建字符串 key；YokiFrame Runtime 已将字符串事件标记为过时。
- 让事件承载可变领域状态，导致事件、数据库和运行时对象各自保存一份真相。
- 把只有一个执行者的必需协作伪装成事件，尤其是把 `Action`、`TaskCompletionSource` 或其它可变回调放进事件对象。
- 用事件替代 `GameManager.UISystem`、`GameStateSystem` 或其它正式系统已经拥有的明确方法入口。

## 禁止做法

- 不新增 `GameEventBus`、`EventCenter` 或只转发 EventKit 的项目包装层。
- 不在 Gameplay 重新声明与 `GameCore/Runtime/Events/` 相同语义的事件类型。
- 不把 YokiFrame 通用示例中的 `PlayerDiedEvent` 等示例类型写成项目正式事件。

## 源码证据

- YokiFrame 直接入口：[`EventKit.cs`](../../../../Assets/Plugins/YokiFrame/Core/Runtime/EventKit/EventKit.cs)、[`TypeEvent.cs`](../../../../Assets/Plugins/YokiFrame/Core/Runtime/EventKit/EventSystem/TypeEvent.cs)。
- 项目事件所有权：[`GameCoreLifecycleEvents.cs`](../../../../Assets/Scripts/GameCore/Runtime/Events/GameCoreLifecycleEvents.cs)、[`GameCorePresentationEvents.cs`](../../../../Assets/Scripts/GameCore/Runtime/Events/GameCorePresentationEvents.cs)、[`GameCoreProgressionEvents.cs`](../../../../Assets/Scripts/GameCore/Runtime/Events/GameCoreProgressionEvents.cs)。
- 真实发送/监听：[`AudioSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/AudioSystem.cs)、[`MapSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/MapSystem.cs)、`InputSystem.cs` 和 `AudioRegion.cs`。
- 项目规范：[`system.md`](../../../rules/system.md) 中关于普通强类型事件直接使用 YokiFrame `EventKit`、禁止只转发包装的规则。
