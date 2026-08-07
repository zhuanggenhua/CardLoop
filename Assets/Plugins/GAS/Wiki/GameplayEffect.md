# EX-GAS GameplayEffect

## 用途

GameplayEffect（GE）是 EX-GAS 修改属性、授予/移除标签和技能、执行周期子效果、触发 Cue 的正式数据与运行时管线。作者侧组合 `GameplayEffectComponentConfig[]`，运行时由 `GameplayEffectHelper.CreateGameplayEffectEntity` 创建 GE Entity，再通过 Apply 管线进入目标 ASC。

属性修改应优先放进 GE 的 Modifier；不要在 GamePlay/GameCore 复制一套 Buff 容器或直接改 CurrentValue。

## 正式入口

- 作者/生成配置：`GameplayEffectConfig` 和派生的 `GameplayEffectComponentConfig`，包括 `ConfEffectBasicInfo`、`ConfDuration`、`MCConfModifiers`、`ConfAssetTags`、`ConfEffectGrantedTags`、四类标签条件、Cue、Period、Stacking 和 `MCConfGrantedAbility`。
- 创建：`GameplayEffectConfig.CreateGameplayEffectEntity()` 或 `GameplayEffectHelper.CreateGameplayEffectEntity(config.ComponentConfigs)`。
- 施加：`GameplayEffectSpec.ApplyTo(target, source)`、`ApplyToSelf(target)`，或 `GameplayEffectHelper.ApplyGameplayEffectTo`。
- 移除：`GameplayEffectSpec.Remove()`、`AbilitySystemCell.RemoveGameplayEffect`；实际销毁由 ECS 系统延后处理。
- OOP 读取和运行时改值：`GameplayEffectSpec`。组件增删应在 Apply 前完成；Apply 后只使用明确允许的 Set/运行时状态入口。

## 生命周期

1. 创建原型：组件配置的 `LoadToGameplayEffectEntity` 把静态组件装到 Entity。此时还没有 Source/Target，也不是已应用实例。
2. Apply：`GameplayEffectHelper.ApplyGameplayEffectTo` 写入 `CEffectInUsage`，加入 `WipInstantiateEffect`，随后进入 `Instantiate -> CheckApply -> Apply`。
3. CheckApply：检查 `ApplicationRequiredTags`、`ImmunityTags` 和其他应用条件。失败时不会成为目标的有效效果。
4. Apply：即时 GE 执行 Modifier 后结束；有 `CDuration` 的 GE 加入目标 ASC 的 `BGameplayEffect`，并继续检查激活条件。
5. Activate/Deactivate：`OngoingRequiredTags` 变化会使持续 GE 失活或重新激活；激活时加入 Modifier、GrantedTags、GrantedAbility 和 Cue，失活时撤销这些运行时作用。
6. Remove/Destroy：`Remove()` 只标记销毁并触发撤销和 Cue 清理，最终由 `SDestroyEffects` 等系统回收。

## 最小示例

```csharp
using GAS.Runtime;

var effect = new GameplayEffectConfig(new GameplayEffectComponentConfig[]
{
    new ConfEffectBasicInfo { Name = "Damage" },
    new ConfDuration
    {
        duration = 30,
        timeUnit = TimeUnit.Frame,
        ResetStartTimeWhenActivated = false,
        StopTickWhenDeactivated = false
    }
});

var spec = new GameplayEffectSpec(effect.ComponentConfigs);
spec.ApplyTo(targetCell, sourceCell);
// 结束时：spec.Remove();
```

实际产生数值变化时，向配置数组加入 `MCConfModifiers`，其中每个 `ModifierSetting` 使用属性集码、属性码、`GEOperation`、基础 Magnitude 和 `MMCConfig`。具体 MMC 规则见 [`MMC.md`](MMC.md)，属性身份见 [`Attribute.md`](Attribute.md)。

## 标签条件语义

`TagRequirementData` 本身同时有 `all`、`any`、`none` 三个槽位，组合结果是 `passAll && passAny && passNone`。编辑器协议的默认映射是：

| GE 组件 | 默认槽位 | 现实用途 |
|---|---|---|
| `ApplicationRequiredTags` | `all` | 目标必须包含全部标签才允许施加 |
| `OngoingRequiredTags` | `all` | 持续期间必须包含全部标签才保持激活 |
| `RemoveGameplayEffectsWithTags` | `any` | 移除目标当前持有的任一匹配效果 |
| `ImmunityTags` | `any` | 目标匹配任一免疫标签时阻止应用 |

`AssetTags` 只是描述 GE；`GrantedTags` 是持续 GE 激活期间由 GE 来源提供的动态标签。不要把四种条件标签与描述/授予标签混成同一字段。

## 常见错误

- 把 Apply 和 Activate 当成同一步；持续 GE 可以已施加但因持续需求标签不满足而失活。
- 以为 `duration = 0` 是无效果；`CDuration` 的源码语义是小于等于 0 表示无限，具体作者协议应以当前表格/编辑器读取逻辑为准。
- Apply 后动态增删 GE 组件，破坏 ECS 系统查询；组件结构应在 Apply 前完成。
- 用 `GameplayEffectHelper.ApplyGameplayEffectImmediate` 代替普通 GE 管线。这个接口只适合明确的即时 BaseValue 变更场景，不创建 GE 实例，也不走完整生命周期。
- 直接修改属性 CurrentValue，导致 Modifier、重算、钳制和事件链失去统一来源。

## 禁止做法

- GamePlay 自建 Buff/Effect 容器、标签条件协议或数值修改器。
- 直接在业务侧操作 GE Entity 的 ECS 组件，代替 `GameplayEffectSpec`、`AbilitySystemCell` 和 Helper 的正式入口。
- 把 `GameplayCue` 当作属性或玩法规则执行器；GE 负责结算，Cue 负责表现。
- 修改生成的 GE/类型映射代码以绕过配置缺失。

## 源码证据

- `Runtime/Effect/GameplayEffectConfig.cs`
- `Runtime/Effect/GameplayEffectSpec.cs`
- `Runtime/Effect/GameplayEffectController.cs`
- `Runtime/Effect/Component/GameplayEffectComponentConfig.cs`
- `Runtime/General/Helper/GameplayEffectHelper.cs`
- `Runtime/System/GameplayEffect/Operation/CheckApply/SCheckApplicationRequiredTags.cs`
- `Runtime/System/GameplayEffect/Operation/CheckApply/SCheckImmunityTags.cs`
- `Runtime/System/GameplayEffect/Operation/Apply/SRemoveEffectWithTags.cs`
- `Runtime/System/GameplayEffect/Operation/CheckActive/SCheckEffectActive.cs`
- `Runtime/System/GameplayEffect/Operation/Activate/SAddModifiers.cs`
- `Runtime/System/GameplayEffect/Operation/Deactivate/SRemoveModifiers.cs`
- `Editor/Helper/EditorEffectHelper.cs`
