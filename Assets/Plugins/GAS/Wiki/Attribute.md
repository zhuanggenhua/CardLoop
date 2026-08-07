# EX-GAS Attribute / AttributeSet

## 用途

Attribute 是可被 GE Modifier 读取和修改的数值，AttributeSet 是一组属性的运行时容器。每个属性同时有 `BaseValue`、`CurrentValue`、可选最小/最大钳制和 Dirty 状态；CurrentValue 由基础值和当前激活的 GE Modifier 重算得出。

属性集和属性码属于 EX-GAS 的数据身份。GamePlay/GameCore 不应另建同名属性表、手工码表或独立的 CurrentValue 结算链。

## 正式入口

- 作者源：`GASSettingAsset.PathOfExcelAttr` 和 `PathOfExcelAttrSet` 指向配置工程的属性、属性集 Excel。
- 生成代码：`XAttribute` 提供属性常量，`XAttrSet` 提供属性集常量、嵌套属性常量和 `AttributeSetMap`。
- Runtime 配置：`AttrSetConfig` 包含属性集码和 `AttributeBaseSetting[]`；`AttributeBaseSetting` 包含属性码、初始值、钳制开关和边界。
- 角色入口：`AbilitySystemCell.GetAttrCurrentValue`、`GetAttrBaseValue`、`SetAttrBaseValue`；创建时把 `AttrSetConfig` 传给 `AbilitySystemCell.Init`。
- 重算入口：`AttributeHelper.RecalculateCurrentValue` 和 `SUpdateAttributeCurrentValue`。普通业务不直接操作 `CAttributeData` 或 `BEAttrSet`。

## 生命周期

1. 生成配置把属性集表行转换成 `AttrSetConfig`。
2. `AbilitySystemCell.Init` 调用 `AttrSetController.AddAttrSet`，用 `AttributeBaseSetting.InitValue` 同时初始化 BaseValue 和 CurrentValue。
3. GE 的激活/失活/移除改变 Modifier 集合，并给 ASC 加上 `CAttributeIsDirty`。
4. `SUpdateAttributeCurrentValue` 调用属性重算系统；重算从 BaseValue 开始，按目标 ASC 当前激活 GE 的 Modifier 顺序计算，再执行钳制并清除 Dirty。
5. `SetBaseValue` 会触发基础值事件并标记重算；`InitBaseValue` 是控制器层的无事件初始化入口。通过 Cell 的公开入口时应按业务需要选择现有方法，不要直接写 CurrentValue。

## 最小示例

```csharp
using GAS.Runtime;

var attrSet = new AttrSetConfig(
    XAttrSet.FightUnit,
    new[]
    {
        new AttributeBaseSetting(
            XAttrSet.AS_FightUnit.Hp,
            100f,
            true,
            true,
            0f,
            100f)
    });

var cell = new AbilitySystemCell();
cell.Init(
    System.Array.Empty<int>(),
    new[] { attrSet },
    System.Array.Empty<AbilityConfig>());

float current = cell.GetAttrCurrentValue(XAttrSet.FightUnit, XAttrSet.AS_FightUnit.Hp);
cell.SetAttrBaseValue(XAttrSet.FightUnit, XAttrSet.AS_FightUnit.Hp, 80f);
```

上例中的 `XAttrSet.FightUnit` 和 `XAttrSet.AS_FightUnit.Hp` 仅代表当前生成文件中存在的常量；如果作者表改变，应重新生成并使用新的 `XAttribute`/`XAttrSet` 常量，不要复制当前数字。

## 常见错误

- 只改 BaseValue 后立即假定 CurrentValue 已经包含全部 GE 结果；框架会通过 Dirty 和重算系统更新 CurrentValue。
- 以为 Modifier 直接永久写入 BaseValue；持续 GE 的 Modifier 参与 CurrentValue 重算，移除或失活后会退出计算。
- 把属性集码和属性码混用。GE `EffectModifier` 同时需要 `AttrSetCode` 和 `AttrCode`。
- 在属性尚未加入 ASC 时读取，控制器会返回 `CAttributeData.NULL` 的默认值语义，不会自动创建属性。
- 手写 `AttributeBaseSetting` 的边界但忘记同步作者表，导致编辑器下拉和运行时数据分叉。

## 禁止做法

- GamePlay 自建 `Stats`/属性 ID 表或另一套 Modifier 结算器。
- 直接修改 `CAttributeData.CurrentValue`，跳过 GE、Dirty、重算和事件。
- 手改 `XAttribute.gen.cs`、`XAttrSet.gen.cs`、Luban JSON 或生成的表类。
- 用属性名字符串作为运行时唯一身份，替代生成的整数码。

## 源码证据

- `Runtime/AttributeSet/AttrSetConfig.cs`
- `Runtime/AttributeSet/AttrSetController.cs`
- `Runtime/Attribute/Component/CAttributeData.cs`
- `Runtime/AttributeSet/Component/BEAttrSet.cs`
- `Runtime/General/Helper/AttributeHelper.cs`
- `Runtime/System/Attribute/SUpdateAttributeCurrentValue.cs`
- `Runtime/Effect/Component/Static/MCModifiers.cs`
- `Editor/GameplayAbilitySystem/GASSettingAsset.cs`
- `Editor/CodeGen/CodeGenerator.cs`
- `Assets/Scripts/Gen/XAttribute.gen.cs`
- `Assets/Scripts/Gen/XAttrSet.gen.cs`
