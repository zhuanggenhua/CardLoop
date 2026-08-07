# EX-GAS GameplayTag

## 用途

GameplayTag 是 EX-GAS 用于描述能力、状态、阵营、事件和冷却等语义的整数码标签。运行时并不保存标签字符串，而是用整数码和预生成的父子关系判断“某个已持有标签是否包含查询标签”。

标签有两类使用位置：

- **内容静态标签**：表格、`AbilityConfig`、`GameplayEffect`、`GameplayCue`、`AbilitySystemCellConfig` 或 XParam 中保存的 `int` / `int[]`。它们描述内容配置。
- **角色动态标签**：角色的 `AbilitySystemCell` 运行时持有的固有标签和临时标签。固有标签在 Cell 初始化时加入；临时标签由 Ability 激活或持续 GE 加入，并记录来源 Entity，结束或移除时恢复。

不要把这两类标签混成一张项目侧字符串表。

## 正式入口

### 作者源

当前项目默认的 GameplayTag 作者表是 [`#exgas.gameplayTags.xlsx`](../../../../EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/%23exgas.gameplayTags.xlsx)。路径和文件名由 `GASSettingAsset.PathOfExcelTag` 与 `GASConstDefine.EXCEL_FILE_NAME_OF_TAG` 定义。标签行至少提供 ID、名称和描述，编辑器读取后的类型是 `TagInEditor`。

### 编辑器选择入口

打开 `EXTool/EX-GAS/GAS中心管理器`，进入 `GameplayTag标签`。该视图从生成 JSON 读取标签列表，并提供打开 Excel、打开 JSON、导出 JSON 和刷新操作。Ability、GE、Cue、MMC、ASC 等编辑页的标签下拉框也来自 EX-GAS 的 `GasXlsxChoice.Tags()` 或 `GeneralGasChoiceHelper.Tags()`，不要在业务层另建下拉数据源。

### 生成入口

1. `EXTool/EX-GAS/生成脚本/GAS表配置` 调用设置中的 `gen.bat`，把作者 Excel 导出到 `Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplaytags.json`，同时生成 Luban C# 表类。
2. `EXTool/EX-GAS/生成脚本/GameplayTag` 调用 `CodeGeneratorTagPart.GenerateTag()`，读取上述 JSON。
3. `CodeGeneratorTagPart.GenerateTag()` 根据标签名称的点号层级生成父列表和子列表，输出 `XTag.gen.cs`。生成文件包含 `XTag` 的 `public const int` 常量和 `InitTagList()`。

不要修改 [`XTag.gen.cs`](../../../Scripts/Gen/XTag.gen.cs)。它是生成物，作者源仍然是 Excel。

源码证据：`Editor/GameplayAbilitySystem/GASSettingAsset.cs` 的 `PathOfExcelTag`、`PathOfJsonTag`、`PathOfCodeTag`；`Editor/CodeGen/CodeGenerator.cs` 的 `GenerateTagCode`；`Editor/CodeGen/CodeGeneratorTagPart.cs` 的 `GenerateTag`；`Editor/GASCenterEditor/GASCenterViewTag.cs` 和 `GasJsonReader.cs`。

## 生成整数码和父子关系

`CodeGeneratorTagPart.GenerateTag()` 对标签名做标识符转换：名称中的 `.` 变成 `_`，再按下划线片段生成父节点名称。它把作者表的 ID 原样写成 `XTag` 常量，并把父码、子码写入 `new GameplayTag(code, parents, children)`。

例如作者表中的 `Ability.Gun.Shoot` 会生成类似 `XTag.Ability_Gun_Shoot`。运行时 `GameplayTag` 保存：

- `Code`：自身整数码。
- `Parents`：所有祖先整数码。
- `Children`：所有后代整数码。

运行时保存 `int` 的原因不是“字符串不重要”，而是当前正式数据和 ECS 结构都以整数 ID 为身份：`AbilityConfig`、GE 组件、Cue 配置、动态标签 buffer 和生成的 `XTag` 都使用整数；`GameplayTag` 只把父子关系数组挂在码上。字符串只保存在生成的反向名称字典中，用于编辑器和监测显示。

## 初始化时机和调用链

直接调用 `TagHelper.InitTagMap(...)` 的正式生成代码是 `XTag.InitTagList()`。`InitTagMap` 做两件事：

1. 把托管侧 `Dictionary<int, GameplayTag>` 和反向名称表保存到 `TagHelper`。
2. 创建 ECS `SingletonGameplayTagMap`，把同一份 Code/Parents/Children 转成 Burst 可读的 `NativeHashMap<int, ComGameplayTag>`。

生成的 `XLauncher.Launch()` 调用顺序是：

```csharp
XLauncher.InitCache();
GASManager.Initialize();
XTag.InitTagList();
```

当前 CardLoop 项目侧还有一条已确认的启动调用：`FormalAbilityRuntimeBootstrap.EnsureGeneratedGasCachesInitialized()` 通过反射调用 `XTag.InitTagList()`；它发生在 `FormalAbilityRuntimeBootstrap.EnsureInitialized()` 调用 `GASManager.Initialize()` 之后。这是项目侧启动适配，不是 EX-GAS 另建的标签作者入口。

如果在 `InitTagMap` 前查询：

- `TagHelper.HasTag` 会访问尚未创建的 `_tagMap`。
- `TagHelper.GetTagFullName` 会访问尚未创建的反向名称字典。
- `TagHelper.FilterInvalidTags` 在 map 为空时原样返回，但后续真正比较仍可能访问空 map。
- 依赖 ECS `SingletonGameplayTagMap` 的 GE 系统会因 `RequireForUpdate` / `GetSingleton` 没有可用单例而无法完成正常标签评估。

因此“GAS World 已创建”不等于“标签图已创建”。

源码证据：`Runtime/General/Helper/TagHelper.cs` 的 `InitTagMap`、`HasTag`、`GetTagFullName`、`FilterInvalidTags`；`Runtime/Tag/Component/SingletonGameplayTagMap.cs`；`Runtime/General/GASManager.cs` 的 `Initialize`；`Editor/CodeGen/CodeGenerator.cs` 的 `GenerateLauncher`；`Assets/Scripts/Gen/XLauncher.gen.cs`；项目侧 `Assets/Scripts/GameCore/Runtime/Game/FormalAbilityRuntimeBootstrap.cs`。

## `TagHelper.HasTag` 的参数方向

`TagHelper.HasTag(tagA, tagB)` 的准确含义是：**标签 A 自身等于 B，或 A 的父标签列表中包含 B**。换句话说，A 是实际持有/被描述的标签，B 是查询标签；这是“实际标签 A 是否包含查询标签 B”。

```csharp
bool same = TagHelper.HasTag(XTag.Ability_Gun, XTag.Ability_Gun);
bool childIncludesParent = TagHelper.HasTag(XTag.Ability_Gun_Shoot, XTag.Ability_Gun);
bool parentDoesNotIncludeChild = !TagHelper.HasTag(XTag.Ability_Gun, XTag.Ability_Gun_Shoot);
```

这里的代码使用当前生成文件中存在的常量。整数相等只能判断相同标签，不能替代第二行的层级判断。

在 `GameplayTag` 结构本身上，`HasTag` 判断自身或父标签，`HasParentTag` 只判断父列表，`HasChildTag` 只判断子列表。普通业务调用通常不直接拿到 `TagHelper` 的私有 map，而是使用 `AbilitySystemCell.HasTag`、`ASCHelper.HasAllTags` / `HasAnyTags` 或 `TagHelper.HasTag`。

## `AbilitySystemCell.HasTag` 与 `TagHelper.HasTag`

两者不是同一职责：

- `TagHelper.HasTag(tagA, tagB)` 只比较两个已知标签码的静态层级关系，不知道任何角色当前状态。
- `AbilitySystemCell.HasTag(tag)` 查询这个 Cell 的 `GameplayTagController`，遍历角色的固有标签和临时标签，再对每个已持有标签调用 `TagHelper.HasTag(heldTag, tag)`。
- ECS 系统需要无托管分配时使用 `ASCHelper` 或 `SingletonGameplayTagMapExtension`，它们执行同样的层级规则，但读取 ECS buffer / singleton。

```csharp
// 角色当前是否拥有 Ability.Gun 的语义标签，包含固有和临时标签。
bool canUseGunState = cell.HasTag(XTag.Ability_Gun);

// 只比较两个内容标签，不读取角色状态。
bool isGunAction = TagHelper.HasTag(XTag.Ability_Gun_Shoot, XTag.Ability_Gun);
```

源码证据：`Runtime/AbilitySystemCell/AbilitySystemCell.cs` 的 GameplayTag 区域；`Runtime/Tag/GameplayTagController.cs` 的 `HasFixedTag`、`HasAnyTemporaryTag`、`HasTag`；`Runtime/General/Helper/ASCHelper.cs`；`Runtime/Tag/Component/SingletonGameplayTagMap.cs`。

## `TagRequirementData` 语义和正式使用入口

`TagRequirementData` 是三个 `NativeArray<int>` 的组合：

| 成员 | 现实语义 |
|---|---|
| `all` | 所有列出的查询标签都必须满足。空数组视为通过。 |
| `any` | 列出的查询标签中至少一个满足。空数组视为通过。 |
| `none` | 列出的查询标签一个都不能满足。空数组视为通过。 |

每个单标签匹配仍然使用层级包含，而不是整数相等。Runtime 的 `EvaluateAscTagRequirement`、`GameplayCueUnit.EvaluateTagRequirement` 和 `SingletonGameplayTagMapExtension.Asc/EffectEvaluateTagRequirement` 都按 `passAll && passAny && passNone` 组合。

正式使用入口和默认映射如下：

| 内容 | 默认使用的槽位 | Runtime 入口 |
|---|---|---|
| Ability 激活需要的标签 | `all` | `AbilityUtil.CheckGameplayTagsValidTpActivate`、`CAbilityActivationRequiredTags` |
| Ability 激活阻止的标签 | `none` | `CAbilityActivationBlockedTags` 的旧式 `tags` 映射、`AbilityUtil.CheckGameplayTagsValidTpActivate` |
| GE 应用需要的标签 | `all` | `SCheckApplicationRequiredTags`、`CApplicationRequiredTags` |
| GE 持续激活需要的标签 | `all` | `SCheckEffectActive`、`COngoingRequiredTags` |
| 移除带匹配标签的 GE | `any` | `SRemoveEffectWithTags`、`CRemoveEffectWithTags` |
| GE 免疫标签 | `any` | `SCheckImmunityTags`、`CEffectImmunityTags` |
| 直接创建 `GameplayCueUnit` 的需求/免疫 | 需求 `all`，免疫 `none` | `GameplayCueConfig` 与 `GameplayCueUnit` 的简化构造函数 |

编辑器 GE 页的正式协议在 `EditorEffectHelper.TagRequirementProtocolFields`：应用和持续需求用 `All`，移除和免疫用 `Any`。编辑器也支持 `GEEditTagRequirement` 的 All/Any/None 三列；不要把“某个组件的默认映射”误写成 `TagRequirementData` 本身只能有一种模式。

需要注意：Ability 的 `ActivationBlockedTags` 和 GE 的 `ImmunityTags` 有反向业务结果。前者在检测到阻止标签时不允许激活，后者在检测到免疫规则匹配时销毁正在应用的效果；判断时应使用正式系统入口，不要自行把 `TagRequirementData` 再包一层。

## 辅助方法

- `FilterInvalidTags(int[])` / `FilterInvalidTags(List<int>)`：在当前 `_tagMap` 已存在时移除注册表中没有的整数码；map 尚未初始化时原样返回。它不会把字符串转换成标签，也不会修复作者表中的重复 ID。
- `GetTagFullName(int)`：从 `tagCode -> 原始标签全名` 的反向表取显示名称。编辑器监测台用它显示 ASC 的固有/临时标签和能力冷却标签；未知码在编辑器下记录错误并返回 `null`。
- `GameplayTagController.AddFixedTag` / `AddTemporaryTag`：分别管理 Cell 的固有标签和按来源记录的临时标签。激活 Ability 或激活持续 GE 的系统会加入临时标签，取消/结束/失活会由 `ASCHelper.RestoreDynamicTags` 清理。

## 生命周期

1. 作者在 GameplayTag Excel 中维护标签 ID、名称和描述。
2. Luban 导出 JSON；`CodeGeneratorTagPart.GenerateTag()` 读取 JSON，生成 `XTag` 常量、父子关系和 `InitTagList()`。
3. `XLauncher.Launch()` 在 `GASManager.Initialize()` 之后调用 `XTag.InitTagList()`；这一步把托管字典和 ECS 单例标签图都建立起来。
4. Cell 初始化时写入固有标签；Ability 激活和持续 GE 激活期间写入按来源记录的临时标签，取消/结束/失活时由框架恢复。
5. 内容配置使用整数码，查询通过 Cell、`TagHelper` 或 ECS 标签查询辅助完成。
6. 关闭 EX-GAS 时调用 `GASManager.Shutdown()`，由标签图正式释放入口清理父子数组、原生哈希表、ECS 单例和托管状态；不得手动销毁其中任何一部分。

## 最小示例

内容资产保存标签码即可，不需要保存第二份字符串。初始化角色后，用 Cell 查询角色状态；比较内容标签本身时用 `TagHelper`：

```csharp
using GAS.Runtime;

// 这些值来自表格生成的常量，内容配置实际保存的是 int。
int[] baseTags = { XTag.Faction_Player };
var cellConfig = new AbilitySystemCellConfig(
    baseTags,
    System.Array.Empty<AttrSetConfig>(),
    System.Array.Empty<AbilityConfig>());

// GASManager.Initialize() 且 XTag.InitTagList() 完成后再创建和初始化 Cell。
var cell = new AbilitySystemCell();
cell.Init(cellConfig.BaseTags, cellConfig.AttrSets, cellConfig.BaseAbilities);

bool hasFaction = cell.HasTag(XTag.Faction);
bool isGunAction = TagHelper.HasTag(XTag.Ability_Gun_Shoot, XTag.Ability_Gun);
```

`hasFaction` 会把 `Faction_Player` 作为子标签匹配到 `Faction`；`isGunAction` 不读取 Cell，只判断两个静态码的层级。GE、Ability 和 Cue 配置中的标签数组也走同一个 `TagHelper` / `SingletonGameplayTagMap` 体系。

## 常见错误

- 直接把 `XTag` 常量值用 `==` 比较并声称支持父标签。
- 在 `GASManager.Initialize()` 或 `XTag.InitTagList()` 前创建依赖标签判断的 Cell、GE、Cue 或 Ability。
- 修改 `XTag.gen.cs` 解决标签问题；正确动作是修改 Excel，重新导 JSON，再重新生成 GameplayTag 代码。
- 把 `GetTagFullName` 当作运行时标签查询。它只负责码到名称的显示映射。
- 把 `FilterInvalidTags` 当作初始化保护。map 未初始化时它会原样返回输入。
- 把 GE 的 AssetTags、GrantedTags、ApplicationRequiredTags、OngoingRequiredTags 混成同一种业务含义。

## 禁止做法

- GamePlay 自建标签类型、本地标签表、本地标签生成器或本地 `HasTag`。
- 用字符串作为运行时标签身份，或要求作者同时维护字符串和 EX-GAS 整数码两份事实。
- 用整数相等冒充父子层级判断。
- 未经源码证据把 Mod 动态合并标签写成现有能力。当前 `TagHelper.InitTagMap` 只接收一份完整 map 并创建一次 ECS singleton，源码中没有 Mod 表发现、校验和合并入口；当前结论是“当前未确认/未支持”。

## 源码证据

- `Runtime/Tag/GameplayTag.cs`
- `Runtime/General/Helper/TagHelper.cs`
- `Runtime/Tag/GameplayTagController.cs`
- `Runtime/Tag/TagRequirementData.cs`
- `Runtime/Tag/Component/SingletonGameplayTagMap.cs`
- `Runtime/AbilitySystemCell/AbilitySystemCell.cs`
- `Runtime/AbilitySystemCell/AbilitySystemComponent.cs`
- `Runtime/Ability/AbilityUtil.cs`
- `Runtime/Ability/AbilitySpec.cs`
- `Runtime/Effect/GameplayEffectSpec.cs`
- `Runtime/System/GameplayEffect/Operation/CheckApply/SCheckApplicationRequiredTags.cs`
- `Runtime/System/GameplayEffect/Operation/CheckApply/SCheckImmunityTags.cs`
- `Runtime/System/GameplayEffect/Operation/CheckActive/SCheckEffectActive.cs`
- `Runtime/System/GameplayEffect/Operation/Apply/SRemoveEffectWithTags.cs`
- `Editor/CodeGen/CodeGeneratorTagPart.cs`
- `Editor/CodeGen/CodeGenerator.cs`
- `Editor/GameplayAbilitySystem/GASSettingAsset.cs`
- `Editor/GASCenterEditor/GASCenterWindow.cs`
- `Editor/GASCenterEditor/GASCenterViewTag.cs`
- `Editor/GASCenterEditor/GasJsonReader.cs`
- `Editor/Helper/EditorEffectHelper.cs`
- `Assets/Scripts/Gen/XTag.gen.cs`
- `Assets/Scripts/Gen/XLauncher.gen.cs`
