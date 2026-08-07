# EX-GAS 2.0 AI 使用入口

## 文档角色

本文档是 EX-GAS 的 AI 工作流、正式入口、硬约束和文档路由。它不是完整 API 手册。具体 API、生命周期和示例以 [`Wiki/EX-GAS.md`](Wiki/EX-GAS.md) 的专项文档为准，源码优先于所有说明文字。

插件包信息、版本和官方仓库入口见 [`package.json`](package.json)。当前包版本为 `2.0.4`，官方文档地址为 `https://github.com/No78Vino/gameplay-ability-system-for-unity`。

## AI 先做什么

1. 先读 [`Wiki/EX-GAS.md`](Wiki/EX-GAS.md)，按任务进入专项章节。
2. 判断当前对象属于作者表格、生成代码、运行时对象，还是编辑器工具。不要把生成物当作者源，也不要在 GamePlay/GameCore 侧复制 EX-GAS 职责。
3. 查专项章节中的源码证据；文档与当前源码冲突时，以当前源码为准，并把冲突记为文档缺口。
4. 涉及运行时创建、查询或施加效果前，确认 `GASManager.Initialize()` 已完成；涉及标签层级查询前，确认生成的 `XTag.InitTagList()` 已完成；场景或进程关闭时由正式入口调用 `GASManager.Shutdown()`。
5. 涉及表格或新类型时，按 [`Wiki/XParam-Luban-CodeGen.md`](Wiki/XParam-Luban-CodeGen.md) 的正式生成链执行，不手改生成 C#、JSON 或 Luban 输出。

## 正式能力路由

| 任务 | 正式入口 |
|---|---|
| GameplayTag 作者源、层级查询、动态标签 | [`Wiki/GameplayTag.md`](Wiki/GameplayTag.md) |
| Ability、AbilityLogic、激活/取消/结束 | [`Wiki/Ability.md`](Wiki/Ability.md) |
| GameplayEffect、属性修改、标签条件 | [`Wiki/GameplayEffect.md`](Wiki/GameplayEffect.md) |
| GameplayCue、表现生命周期、CueUnit | [`Wiki/GameplayCue.md`](Wiki/GameplayCue.md) |
| Attribute、AttributeSet、数值重算 | [`Wiki/Attribute.md`](Wiki/Attribute.md) |
| AbilitySystemComponent、AbilitySystemCell | [`Wiki/AbilitySystemCell.md`](Wiki/AbilitySystemCell.md) |
| TargetCatcher、目标捕获 | [`Wiki/TargetCatcher.md`](Wiki/TargetCatcher.md) |
| AbilityTask、Timeline | [`Wiki/AbilityTask-Timeline.md`](Wiki/AbilityTask-Timeline.md) |
| MMC、属性来源和捕获 | [`Wiki/MMC.md`](Wiki/MMC.md) |
| XParam、Luban、Bean、代码生成 | [`Wiki/XParam-Luban-CodeGen.md`](Wiki/XParam-Luban-CodeGen.md) |
| GAS 中心、表格下拉、时间轴编辑器、监测台 | [`Wiki/Editor-Authoring.md`](Wiki/Editor-Authoring.md) |

## 不得重复建立的职责

- 标签语义、标签层级关系、标签码生成和运行时标签查询归 EX-GAS。不要在 GamePlay/GameCore 新建 `GamePlayTag` 类型、本地标签表、本地标签生成器或用整数相等替代层级查询。
- Ability、GameplayEffect、GameplayCue、Attribute/AttributeSet、AbilityTask、TargetCatcher 和 MMC 优先使用本插件已有基类、组件配置、Helper 和编辑器入口。
- 静态内容使用生成的 `XTag`、`XAttribute`、`XAttrSet` 等常量或表格 ID；角色运行时标签使用 `AbilitySystemCell` / `GameplayTagController` 管理的固有标签和临时标签。
- `Assets/DataGenerated`、`Assets/Scripts/Gen` 和 Luban 工具输出是生成物，不是作者入口。
- 本插件当前源码没有确认 Mod 动态合并 GameplayTag 表的正式 API。不要把 Mod 目标、项目侧临时扩展或自建合并逻辑写成 EX-GAS 已支持能力。

## 运行时初始化底线

生成的 `XLauncher.Launch()` 先调用 `InitCache()`，再调用 `GASManager.Initialize()`，最后调用 `XTag.InitTagList()`。`XTag.InitTagList()` 会调用 `TagHelper.InitTagMap()`，而 `InitTagMap` 会创建 ECS 的 `SingletonGameplayTagMap`，因此标签表初始化不能早于 `GASManager.Initialize()`。

配置表加载是另一条入口：生成的 `XLauncher.InitConfigTables(loader)` 调用 `XLuban.Init(loader)`。不要把“类型注册”“Luban 表加载”“GAS World 初始化”“标签图初始化”混为一个未验证的步骤。

源码证据：`Runtime/General/GASManager.cs` 的 `Initialize`、`Runtime/General/Helper/TagHelper.cs` 的 `InitTagMap`、`Editor/CodeGen/CodeGenerator.cs` 的 `GenerateLauncher`、生成文件 `Assets/Scripts/Gen/XLauncher.gen.cs` 和 `Assets/Scripts/Gen/XTag.gen.cs`。

## 新类型和配置变更

新建 AbilityLogic、GameplayCue、MMC、AbilityTask、TargetCatcher 或 XParam 前，先查专项文档和现有可复用类型。新类型需要走：

1. `EXTool/EX-GAS/生成脚本/更新Bean定义`
2. 配置表工程 `EX_GAS_Config/ProjectConfigTable/exgas_config/gen.bat`
3. `EXTool/EX-GAS/生成脚本/生成所有`
4. `EXTool/EX-GAS/生成脚本/GAS表配置`

这里的“生成所有”不会替代 Luban 的 `gen.bat`。完整原因、表格来源、输出路径和 XParam 约束见 [`Wiki/XParam-Luban-CodeGen.md`](Wiki/XParam-Luban-CodeGen.md)。

## 证据写法

专项文档的重要结论必须同时写出现实用途、代码符号和源码文件。例如不要只写“调用 TagHelper”；要说明它是“比较两个已知标签码的静态层级关系”，再引用 `TagHelper.HasTag` 和对应文件。
