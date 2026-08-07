# EX-GAS Ability

## 用途

Ability 表示可被角色尝试激活、持续运行、取消或正常结束的一段行为。EX-GAS 把“配置数据”和“行为代码”分开：`AbilityConfig` 只携带一组组件配置，`AbilityLogicBase` 的派生类实现行为，`AbilitySpec` 是运行时查询和操作单个技能的 OOP 入口。

不要在 GamePlay/GameCore 另建一套技能容器、技能激活标签判断或技能生命周期；先使用 ASC/Cell、`AbilityController`、`AbilitySpec` 和现有 AbilityLogic。

## 正式入口

- 行为代码：继承 `AbilityLogicBase` 或 `AbilityLogicBase<TParam>`，实现 `ActivateAbility(GlobalTimer)`、`CancelAbility(GlobalTimer)`、`EndAbility(GlobalTimer)` 和 `AbilityTick(GlobalTimer)`。
- 配置拼装：`AbilityConfig(AbilityComponentConfig[])`；常用组件配置包括 `MCConfAbilityLogic`、`ConfAbilityBaseInfo`、`ConfAbilityAssetTags`、`ConfAbilityActivationRequiredTags`、`ConfAbilityActivationBlockedTags`、`ConfAbilityActivationOwnedTags`、`ConfAbilityCost` 和 `ConfAbilityCooldown`。
- 运行时控制：`AbilitySystemCell.GetAbilitySpec(code)`、`TryActivateAbility(code, param)`、`TryEndAbility(code)`、`TryCancelAbility(code)`；也可以通过 `AbilitySpec.TryActivate/ TryEnd/ TryCancel` 操作单个规格。
- 类型创建：`AbilityHelper.TryCreateAbilityLogic` 由生成的类型注册表使用，不要在业务侧用另一套字符串到类型映射。

## 生命周期

1. `AbilityController.GrantAbility` 调用 `AbilityHelper.CreateAbilityEntity`，把每个 `AbilityComponentConfig` 加载到技能 Entity，再绑定 Owner 和 `AbilitySpec`。
2. `TryActivateAbility` 或 `AbilitySpec.TryActivate` 只加入“待尝试激活”标记；`STryActivateAbility` 在逻辑帧中检查已激活状态、标签、Cost、Cooldown 和其他能力的阻止关系。
3. 检查成功后，EX-GAS 加入激活标记，提交本次 `AbilityActivationContext`，调用 `ActivateAbility`。`ActivationOwnedTags` 也在此时按技能 Entity 作为来源加入临时标签。
4. 激活期间 `SAbilityTick` 调用 `AbilityTick`。Timeline Ability 在这里推进 `ALTimelinePlayer`。
5. 取消和结束分别由 `STryCancelAbility`、`STryEndAbility` 调用 `CancelAbility` 或 `EndAbility`，并通过 `ASCHelper.RestoreDynamicTags` 清理由该技能产生的动态标签。

## 最小示例

以下示例使用插件现有的 `ALDebugLog`、`XParamString`、`AbilityConfig` 和 `AbilitySystemCell`：

```csharp
using GAS.Runtime;

GASManager.Initialize();
XLauncher.InitCache();

var ability = new AbilityConfig(new AbilityComponentConfig[]
{
    new ConfAbilityBaseInfo { Code = 1001, Level = 1 },
    new MCConfAbilityLogic
    {
        AbilityLogicType = nameof(ALDebugLog),
        Param = new XParamString("attack")
    }
});

var cell = new AbilitySystemCell();
cell.Init(
    System.Array.Empty<int>(),
    System.Array.Empty<AttrSetConfig>(),
    new[] { ability });
cell.TryActivateAbility(1001);
```

真实项目通常由生成的 `XAbility`/Luban 配置返回 `AbilityConfig`，不应手动复制表读取逻辑。若需要检查失败原因，先拿到 `AbilitySpec`，使用 `CheckActivation()` 返回的 `AbilityActivationResult`。

## 标签、Cost 和 Cooldown

- `ActivationRequiredTags` 默认写入 `TagRequirementData.all`，要求所有条件标签满足。
- `ActivationBlockedTags` 默认写入 `TagRequirementData.none`，命中任一阻止标签就失败。
- `CBlockAbilityWithTags` 是已激活能力阻止其他能力的规则；`CCancelAbilityWithTags` 是激活当前能力时取消其他匹配技能的规则。
- `CanActivate` 是标签、Cost、Cooldown 和“是否已激活”的综合结果；`CheckActivation()` 用于获取具体失败枚举。
- Cost 和 Cooldown 的正式承载物是原型 GameplayEffect。通过 `AbilitySpec.GetCostEffectProto()`、`GetCooldownProtoGE()` 读取；不要在 GamePlay 自建资源扣除或冷却标签逻辑。

## 常见错误

- 把 `TryActivateAbility` 当成同步调用；它只是加入待处理标记，实际激活在 `STryActivateAbility` 的逻辑帧处理。
- 直接调用 `ActivateAbility` 绕过标签、Cost、Cooldown 和动态标签生命周期。
- 继承旧文档中的 `Ability`、`AbilitySpec` 体系或寻找不存在的 AbilityAsset 编辑器；当前 Runtime 的行为基类是 `AbilityLogicBase`，配置是组件数组。
- 复用同一个 `AbilitySpec` 时把一次性目标放进作者配置参数；一次激活的临时目标应使用 `AbilityActivationContext`，并从 `ActivationContext` 读取。
- 业务侧修改已 Apply 后的 Ability 组件结构；组件存在性和原生数组应在首次激活前配置。

## 禁止做法

- GamePlay 自建技能激活状态机、标签条件、Cost/Cooldown 或技能动态标签缓存。
- 手改 `Assets/Scripts/Gen/XAbility.gen.cs`、JSON 或 Luban C# 输出来修复技能配置。
- 用字符串技能名代替表格 ID 和生成的类型注册入口。
- 在 AbilityLogic 中把 Cue 当作数值结算或属性修改入口；数值应由 GameplayEffect 负责。

## 源码证据

- `Runtime/Ability/Component/AbilityLogic/AbilityLogicBase.cs`
- `Runtime/Ability/AbilityConfig.cs`
- `Runtime/Ability/AbilitySpec.cs`
- `Runtime/Ability/AbilityController.cs`
- `Runtime/Ability/AbilityHelper.cs`
- `Runtime/Ability/AbilityUtil.cs`
- `Runtime/Ability/Component/Static/MCAbilityLogic.cs`
- `Runtime/System/Ability/STryActivateAbility.cs`
- `Runtime/System/Ability/STryCancelAbility.cs`
- `Runtime/System/Ability/STryEndAbility.cs`
- `Runtime/System/Ability/SAbilityTick.cs`
