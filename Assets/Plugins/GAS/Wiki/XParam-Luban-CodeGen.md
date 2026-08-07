# EX-GAS XParam / Luban / 代码生成

## 用途

`XParam` 是 AbilityLogic、AbilityTask、GameplayCue、MMC、TargetCatcher 等泛型运行时类型之间传递作者参数的统一协议。它同时承担编辑器 Bean 字段和 Luban 流式表参数的读写，不是项目侧随意定义的 DTO。

## 正式入口

- 参数协议：实现 `XParam`；编辑器侧实现 `DecodeExcelData(List<object>)` 和 `EncodeExcelData()`。空值必须转换成明确的默认占位，不要让流式表解析依赖空字段。
- 普通字段：用 `BeanFieldAttribute` 指定 Setter、字段名、Luban 类型和顺序。
- 多态字段：用 `BeanPolymorphicFieldAttribute` 连接类型名、参数字段和对应 Helper，例如 `XParamCue` 的 CueLogic、`XParamApplyEffects` 的 TargetCatcher。
- Bean 作者源：`EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/__beans__.xlsx`；`BeanUpdater.UpdateBeans()` 扫描当前程序集的 XParam、Cue、MMC、AbilityLogic、AbilityTask、TargetCatcher 并更新定义。
- 表格生成：`EXTool/EX-GAS/生成脚本/GAS表配置` 调用配置工程的 `gen.bat`，输出 Luban JSON 和 C# 表类。
- 代码生成：`EXTool/EX-GAS/生成脚本/生成所有` 依次更新 Bean、生成 Tag/Attribute/AttributeSet/Ability/Cue/MMC、LubanExtension 和 Launcher；也可使用各专项菜单。

## 生命周期

1. 作者在 Excel/Bean 中选择类型名并填写参数。
2. Editor 通过 `EditorAbilityHelper`、`EditorCueHelper`、`EditorMmcHelper`、`EditorTargetCatcherHelper` 创建参数并调用 Decode/Encode。
3. Luban 输出表类和 JSON；生成的 `XLuban` 将配置行转换成 `AbilityConfig`、`GameplayEffectConfig`、`GameplayCueConfig`、`MMCConfig`、`AbilitySystemCellConfig` 等 Runtime 对象。
4. `XLauncher.InitCache()` 加载 Ability/MMC/Cue 类型注册；`XLauncher.InitConfigTables(loader)` 调用 `XLuban.Init(loader)` 并注册 GE 配置 ID 查询。
5. Runtime 根据注册名和参数创建泛型逻辑；生成代码、JSON 和表类均属于输出物。

## 最小示例

```csharp
using System.Collections.Generic;
using GAS.Runtime;

public sealed class XParamDamage : XParam
{
    public int Value { get; private set; }

    public void SetValue(int value) => Value = value;

#if UNITY_EDITOR
    public void DecodeExcelData(List<object> data)
    {
        Value = data != null && data.Count > 0 ? System.Convert.ToInt32(data[0]) : 0;
    }

    public List<object> EncodeExcelData() => new() { Value };
#endif
}
```

该示例使用真实 `XParam` 契约；如果它作为 `AbilityLogicBase<XParamDamage>` 或其他多态字段参数使用，还必须通过 `BeanUpdater` 和对应生成器进入正式类型注册。不要只创建类而跳过 Bean/注册生成。

## 常见错误

- 直接编辑 `Assets/DataGenerated/Luban` 或 `Assets/Scripts/Gen` 解决配置问题，下一次生成会覆盖这些输出。
- 只运行 Luban `gen.bat`，却没有更新 Bean 或生成类型注册，导致运行时按名称找不到类型。
- `DecodeExcelData` 和 `EncodeExcelData` 的槽位顺序不一致；多态参数还要注意类型名槽位和参数槽位的拆分。
- 让空参数返回 `null` 而不是默认对象/默认值，触发流式配置的空占位问题。
- 将当前项目的 `GameCore` 类型注册误写成 EX-GAS 插件内置能力。

## 禁止做法

- 在 GamePlay/GameCore 新建另一套参数序列化协议、Bean 继承树或类型注册表。
- 手改生成的 `X*.gen.cs`、`XLuban.gen.cs`、Luban JSON、表类或 `__beans__.xlsx` 的生成结果。
- 未经源码证据把 Mod 运行时表合并、动态注册或热更新写成当前 EX-GAS 能力。

## 源码证据

- `Runtime/General/XParam/XParam.cs`
- `Runtime/General/XParam/BeanFieldAttribute.cs`
- `Runtime/General/XParam/BeanPolymorphicFieldAttribute.cs`
- `Runtime/General/XParam/Ability/XParamApplyEffects.cs`
- `Runtime/General/XParam/Cue/XParamCue.cs`
- `Runtime/Effect/Modifier/MMCConfig.cs`
- `Editor/CodeGen/BeanUpdater.cs`
- `Editor/CodeGen/CodeGenerator.cs`
- `Editor/CodeGen/CodeGeneratorLubanPart.cs`
- `Editor/CodeGen/CodeGeneratorAbilityPart.cs`
- `Assets/Scripts/Gen/XLauncher.gen.cs`
- `Assets/Scripts/Gen/XLuban.gen.cs`
