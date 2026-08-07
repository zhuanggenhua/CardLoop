# EX-GAS MMC（Modifier Magnitude Calculation）

## 用途

MMC 负责把 GE Modifier 的基础 `Magnitude` 转换成最终修改量。它只计算数值，不负责选择目标、不负责创建 GE，也不应直接承担技能或标签规则。

当前真实基类是 `ModMagnitudeCalculationBase` / `ModMagnitudeCalculationBase<TParam>`。旧文档中的 `ModifierMagnitudeCalculation`、`ScalableFloatModCalculation` 和 `AttributeBasedModCalculation` 不是当前类型名；当前内置实现是 `MMCScalableFloat`、`MMCAttributeBased` 和 `MMCNone`。

## 正式入口

- GE Modifier：`ModifierSetting.Magnitude` 和 `ModifierSetting.MMC` 组成 `EffectModifier`。
- MMC 参数：`MmcParaFloatScale`、`AttributeBasedMmcParam` 等 `XParam`。
- 创建/注册：`MMCConfig.CreateMmc()` -> `MmcHelper.TryCreateMmc`；生成的 `XMmc.LoadMmcType()` 通过 `MmcHelper.RegisterMmc` 注册类型和参数类型。
- 自定义 MMC：继承 `ModMagnitudeCalculationBase<TParam>`，实现 `CalculateMagnitude(MmcContext, float)`；如需 Track 依赖监听，覆写 `OnAdded`/`OnRemoved`。

## 生命周期

1. GE 配置加载时，`MCConfModifiers.LoadToGameplayEffectEntity` 为每个 `ModifierSetting` 创建 `MMC` 实例，并调用其参数初始化。
2. GE 激活或即时结算时，框架通过 `MmcHelper.Calculate` 构造 `MmcContext`，再调用 `EffectModifier.Apply` 和 `CalculateMagnitude`。
3. 持续 GE 激活 Modifier 时，框架调用 `OnAddMmc`；GE 移除时调用 `OnRemoveMmc`。自定义 MMC 必须在 `OnRemoved` 中注销自己注册的监听。

## 内置计算

- `MMCScalableFloat`：最终值为 `magnitude * k + b`。
- `MMCAttributeBased`：从 Source 或 Target 的 `AbilitySystemCell` 解析属性，计算 `attributeValue * K + B`。`SnapShot` 第一次计算后缓存，`Track` 每次读取，并在被依赖属性基础值变化时触发目标属性重算。
- `MMCNone`：返回输入 Magnitude 的基础行为。

## 最小示例

```csharp
using GAS.Runtime;

public sealed class DoubleMagnitude : ModMagnitudeCalculationBase<MmcParaFloatScale>
{
    public override float CalculateMagnitude(MmcContext context, float magnitude)
    {
        return magnitude * Parameter.K + Parameter.B;
    }
}

var mmc = MmcHelper.TryCreateMmc(
    typeof(MMCScalableFloat),
    new MmcParaFloatScale(2f, 1f));
```

`DoubleMagnitude` 是自定义 MMC 的真实基类和方法签名示例；它还必须被编辑器扫描并通过 `XMmc.LoadMmcType()` 注册，才能从表格的类型字段创建。普通 GE 应优先复用 `MMCScalableFloat` 或 `MMCAttributeBased`，而不是为简单线性计算新建类。

## 常见错误

- 在 MMC 中缓存错误的 Source/Target；应从 `MmcContext.Source`、`MmcContext.Target` 和 `MmcContext.EffectSpec` 读取。
- 把 `SnapShot` 误认为每帧实时取值，把 `Track` 误认为只在首次应用时取值。
- 使用 `MMCAttributeBased` 却未提供有效的属性集码和属性码；默认解析器调用 `AbilitySystemCell.GetAttrCurrentValue`。
- 自定义 MMC 在 `OnAdded` 注册监听却不在 `OnRemoved` 注销，导致移除 GE 后仍保留回调。
- 把 `MMCConfig.MmcType` 写成未由 `MmcHelper.RegisterMmc` 注册的类型。

## 禁止做法

- 在 GamePlay 重写 Modifier 结算、属性来源解析或 Track 监听协议。
- 在 MMC 中修改标签、添加/移除 GE 或驱动 Ability 生命周期。
- 手改 `XMmc.gen.cs` 或 Luban 生成的 MMC 配置代码。

## 源码证据

- `Runtime/Effect/Modifier/ModMagnitudeCalculationBase.cs`
- `Runtime/Effect/Modifier/MmcContext.cs`
- `Runtime/Effect/Modifier/MMCConfig.cs`
- `Runtime/Effect/Modifier/CommonUsage/MMCScalableFloat.cs`
- `Runtime/Effect/Modifier/CommonUsage/MMCAttributeBased.cs`
- `Runtime/Effect/Modifier/CommonUsage/MMCNone.cs`
- `Runtime/Effect/Modifier/MmcParameter/MmcParaFloatScale.cs`
- `Runtime/Effect/Modifier/MmcParameter/AttributeBasedMmcParam.cs`
- `Runtime/General/Helper/MmcHelper.cs`
- `Runtime/Effect/Component/Static/MCModifiers.cs`
- `Editor/Helper/EditorMmcHelper.cs`
- `Editor/CodeGen/CodeGenerator.cs`
