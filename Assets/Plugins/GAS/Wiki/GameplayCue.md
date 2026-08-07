# EX-GAS GameplayCue

## 用途

GameplayCue 是表现层能力：播放特效、音效、动画、日志或其他非结算反馈。当前 Runtime 的作者基类是 `GameplayCueBase` / `GameplayCueBase<T>`，不是旧 Wiki 中的 `GameplayCueSpec`、`GameplayCueInstantSpec` 或 `GameplayCueDurationalSpec`。

Cue 不应成为属性、Buff、伤害、位移判定或其他必须影响战斗结果的唯一入口；这些结果应由 Ability/GameplayEffect 负责。

## 正式入口

- Cue 逻辑：继承 `GameplayCueBase` 或 `GameplayCueBase<TParam>`，实现 `InitParameters`（泛型基类通常已实现）以及 `OnAdd`、`OnRemove`、`OnActivate`、`OnDeactivate`、`OnTick`、`OnDestroy`。
- Cue 参数：实现 `XParam` 的参数类，例如 `XParamLogging`、`XParamPlaySound`、`XParamAnimator`、`XParamMountPrefab`。
- 类型注册：生成的 `XCue.LoadCueType()` 调用 `CueHelper.RegisterCue`；运行时使用 `CueHelper.TryCreateCue`，不要自行维护字符串到类型字典。
- GE 触发：`ConfCueBase`/`GameplayCueConfig` 将 Cue 组件加载到 GE；系统根据 Apply/Add/Activate/Deactivate/Remove/Tick 阶段触发。
- 独立使用：`GameplayCueUnit` 提供 `Create`、`AddToAsc`、`Play`、`Stop`、`RemoveFromAsc`、`Destroy`。

## 生命周期

### GE 来源

`ConfCueBase.CreateCueEntityArray` 为每个配置创建 Cue Entity，设置 `CueSourceType.GameplayEffect` 和来源 GE。播放前会用 `CPlayRequiredTags` 与 `CPlayImmunitedTags` 检查目标 ASC；`CueHelper.TryPlayCueOnAsc` 重置逻辑、绑定目标并播放。

### 独立 CueUnit

调用顺序是 `new GameplayCueUnit(config)` -> `Create()` -> `AddToAsc(cell)` -> `Play()` -> `Stop()` -> `RemoveFromAsc()` -> `Destroy()`。`AddToAsc` 返回 `false` 表示目标标签条件或免疫条件不满足；不能忽略这个结果后假设 Cue 已挂载。

### 自定义逻辑回调

- `OnAdd`：Cue 加入目标 ASC。
- `OnActivate` / `OnDeactivate`：所属持续 GE 生效/失活。
- `OnTick`：Cue 的更新阶段。
- `OnRemove`：Cue 从目标 ASC 移除。
- `OnDestroy`：Cue Entity 被销毁前的清理。

## 最小示例

```csharp
using GAS.Runtime;

public sealed class DamageLogCue : GameplayCueBase<XParamString>
{
    public override void OnActivate(float time)
    {
        UnityEngine.Debug.Log(Parameter.Value);
    }
}

var config = new GameplayCueConfig(
    typeof(DamageLogCue),
    new XParamString("damage"),
    new[] { XTag.State },
    new[] { XTag.State_Debuff });

var cue = new GameplayCueUnit(config);
cue.Create();
if (cue.AddToAsc(targetCell))
    cue.Play();
// 结束时：cue.Stop(); cue.RemoveFromAsc(); cue.Destroy();
```

自定义 Cue 必须通过项目实际生成的 `XCue.LoadCueType()` 注册后才能用字符串配置创建；直接传 `Type` 的 `GameplayCueConfig` 示例仍要求 `CueHelper.TryCreateCue` 能创建该类型。

## 标签条件

`GameplayCueConfig` 支持 `RequiredAllTags`、`RequiredAnyTags`、`RequiredNoneTags` 以及对应的免疫三槽位。空槽位通过；整体仍是 `all && any && none`。旧式构造函数参数 `requiredTags` 映射到 `RequiredAllTags`，`immunityTags` 映射到 `ImmunityNoneTags`。

## 常见错误

- 继续继承旧的 `GameplayCueSpec`、`GameplayCueInstantSpec` 或 `GameplayCueDurationalSpec`，这些类型在当前 Runtime 中不存在。
- 忘记 `Create()` 就调用 `Play()`；`GameplayCueUnit` 会报 Cue 实例不存在。
- 忽略 `AddToAsc` 的布尔返回值，导致需求标签不满足时仍认为表现已经播放。
- 在 `OnRemove` 中只停表现不释放业务资源，或独立 Cue 结束时遗漏 `Destroy()`。
- 把 `OnTick` 当作属性结算和状态机入口。

## 禁止做法

- 在 GamePlay 自建 Cue 注册表、Cue 参数序列化协议或 Cue 生命周期容器。
- 用 Cue 修改属性、授予技能、决定命中或替代 GE。
- 手改 `XCue.gen.cs`；新增 Cue 后应让编辑器扫描派生类并重新生成类型注册。

## 源码证据

- `Runtime/Cue/Base/GameplayCueBase.cs`
- `Runtime/Cue/Base/GameplayCueParameters.cs`
- `Runtime/Cue/GameplayCueUnit.cs`
- `Runtime/Cue/CueConfig.cs`
- `Runtime/Cue/ConfCueBase.cs`
- `Runtime/General/Helper/CueHelper.cs`
- `Runtime/Cue/Component/MCCue.cs`
- `Runtime/System/Cue/SCueStart.cs`
- `Runtime/System/Cue/SCueTick.cs`
- `Runtime/System/Cue/SCueEnd.cs`
- `Runtime/System/Cue/SCueDestroy.cs`
- `Editor/CodeGen/CodeGenerator.cs`
