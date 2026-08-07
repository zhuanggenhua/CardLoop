# EX-GAS 编辑器作者入口

## 用途

EX-GAS Editor 负责让作者编辑配置源、选择已注册类型、生成输出和观察运行时状态。它是配置作者入口，不是让 GamePlay 复制编辑器逻辑的理由。

## 正式入口

### GAS 中心管理器

菜单是 `EXTool/EX-GAS/GAS中心管理器`，由 `GASCenterWindow` 提供以下作者页：GameplayTag、Attribute、Attribute Set、GameplayCue、MMC、GameplayEffect、GameplayAbility、ASC。窗口打开时调用 `GasJsonReader.ReadAllAndCache()` 和 `GeneralGasChoiceHelper.LoadCache()`，各下拉项应复用 `GasXlsxChoice`/`GeneralGasChoiceHelper`。

### 生成脚本

- `EXTool/EX-GAS/生成脚本/更新Bean定义`
- `EXTool/EX-GAS/生成脚本/GAS表配置`
- `EXTool/EX-GAS/生成脚本/GameplayTag`
- `EXTool/EX-GAS/生成脚本/Attribute`
- `EXTool/EX-GAS/生成脚本/AttributeSet`
- `EXTool/EX-GAS/生成脚本/Ability`
- `EXTool/EX-GAS/生成脚本/GameplayCue`
- `EXTool/EX-GAS/生成脚本/ModMagnitudeCalculation`
- `EXTool/EX-GAS/生成脚本/LubanExtension`
- `EXTool/EX-GAS/生成脚本/Launcher`
- `EXTool/EX-GAS/生成脚本/生成所有`

### 时间轴和监测

`AbilityTimelineEditorWindow` 读取 `PathOfExcelTimelineAbility` 对应的时间轴表；`GASWatcher` 的菜单是 `EXTool/EX-GAS/监测台`，用于观察 ASC、属性、GE、标签和 Ability 状态。监测台是观察入口，不是运行时业务 API。

### Web 编辑器

Tag、Attribute、Attribute Set、ASC、Effect 等 Web 编辑器由 `GASWebEditorManager` 启动。它们仍然写入 `GASSettingAsset` 指向的作者 Excel；不能把 Web 编辑器产生的 JSON 当成新的作者源。

## 生命周期

1. 在 `GASSettingAsset` 中确认配置工程、表输出目录、表类输出目录和生成代码目录。
2. 修改作者 Excel 或 Bean 定义。
3. 有新 XParam/Ability/Cue/MMC/Task/TargetCatcher 时先更新 Bean，再生成类型代码。
4. 执行 Luban 表导出，再执行对应代码生成；生成物进入 `Assets/DataGenerated/Luban` 和 `Assets/Scripts/Gen`。
5. 运行时先初始化 `XLauncher`/GAS，再用监测台验证实际状态，不用编辑器缓存代替运行时验证。

## 最小示例

```csharp
#if UNITY_EDITOR
using GAS.Editor;

GASCenterWindow.OpenWindow();
GasJsonReader.ReadAllAndCache();
var tags = GasXlsxChoice.Tags();
#endif
```

这些是插件已有的 Editor API；正常作者操作优先使用菜单，不要在项目中包装出第二个配置编辑器。

## 常见错误

- 修改了 Excel 却只刷新窗口，没有重新导 JSON/生成代码，导致下拉、JSON、常量和运行时不一致。
- 把 `Assets/Scripts/Gen` 当手写代码目录。
- 把 `GASManager.Initialize`、`XLauncher.InitCache`、`XLuban.Init` 和 `XTag.InitTagList` 当成一个不可区分的“初始化”；它们分别负责 World、类型注册、表加载和标签图。
- 时间轴参数或自定义类型未进入 Bean 扫描，导致编辑器下拉为空或生成器无法拆解多态参数。
- 把项目侧 `GameCore` 的额外编辑器页或 2D Catcher，当成插件 Editor 已内置能力。

## 禁止做法

- 在 GamePlay/GameCore 侧复制 GAS 中心的表格读取、下拉选择、类型扫描和生成逻辑。
- 手动改生成 C#、Luban C#、JSON 或运行时缓存以“临时修复”作者数据。
- 用监测台截图或编辑器缓存状态代替运行时功能验收。

## 源码证据

- `Editor/GASCenterEditor/GASCenterWindow.cs`
- `Editor/GASCenterEditor/GASCenterViewTag.cs`
- `Editor/GASCenterEditor/GasXlsxChoice.cs`
- `Editor/GASCenterEditor/GasJsonReader.cs`
- `Editor/GameplayAbilitySystem/GASSettingAsset.cs`
- `Editor/CodeGen/BeanUpdater.cs`
- `Editor/CodeGen/CodeGenerator.cs`
- `Editor/Ability/AbilityTimelineEditor/EditorWindow/AbilityTimelineEditorWindow.cs`
- `Editor/Ability/AbilityTimelineEditor/DataClass/GasAbilityTimelineXlsxReadWrite.cs`
- `Editor/GameplayAbilitySystem/GASWatcher.cs`
- `Editor/WebEditor/GASWebEditorManger.cs`
