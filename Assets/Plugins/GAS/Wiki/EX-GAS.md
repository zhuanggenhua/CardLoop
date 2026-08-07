# EX-GAS 2.0 总导航

本文是插件的总览和 AI 导航入口。它只负责说明职责边界、作者源与生成物的关系、运行时主链路以及专项文档位置；具体 API 和生命周期见专项章节。

## 先判断对象属于哪一层

| 层 | 现实用途 | 当前正式入口 |
|---|---|---|
| 作者配置 | 人编辑的 GameplayTag、Attribute、AttributeSet、Ability、Cue、GE、MMC、ASC、Timeline 数据 | `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas/` 下的 `#exgas.*.xlsx` |
| 生成代码 | 将表格 ID、名称、类型映射成 C# 入口 | `Assets/Scripts/Gen/X*.gen.cs` |
| 生成配置 | Runtime 读取的 Luban JSON 和配置类 | `Assets/DataGenerated/Luban/Json/GAS/`、`Assets/DataGenerated/Luban/CSharp/` |
| 运行时对象 | ECS World、ASC、Cell、Ability、GE、Cue、标签和属性状态 | `Runtime/` 下的 `GAS.Runtime` 类型 |
| 编辑器作者工具 | 打开表格、选择 ID、编辑组件、编辑时间轴、监测运行时状态 | `Editor/` 下的菜单和窗口 |

作者配置是事实源。生成代码和 JSON 只能由工具生成，运行时缓存只能由初始化链创建。

## 按任务进入专项文档

| 任务 | 文档 |
|---|---|
| 标签作者源、整数码、父子层级、固有/临时标签、`TagRequirementData` | [`GameplayTag.md`](GameplayTag.md) |
| 技能逻辑、AbilitySpec、激活/取消/结束、Ability 标签 | [`Ability.md`](Ability.md) |
| 效果组件、即时/持续 GE、属性修改、效果标签和 Cue | [`GameplayEffect.md`](GameplayEffect.md) |
| 表现 Cue、CueUnit、Cue 生命周期和播放条件 | [`GameplayCue.md`](GameplayCue.md) |
| 属性集、属性码、基础值、当前值和 MMC 重算 | [`Attribute.md`](Attribute.md) |
| `AbilitySystemComponent` 与 `AbilitySystemCell` 的边界和初始化 | [`AbilitySystemCell.md`](AbilitySystemCell.md) |
| 目标捕获器和 `TaskApplyEffects` | [`TargetCatcher.md`](TargetCatcher.md) |
| `AbilityTask`、`ALTimeline`、Timeline 表和时间轴编辑器 | [`AbilityTask-Timeline.md`](AbilityTask-Timeline.md) |
| MMC、`MmcContext`、属性来源和 Track/SnapShot | [`MMC.md`](MMC.md) |
| XParam、Bean、Luban、生成脚本和输出 | [`XParam-Luban-CodeGen.md`](XParam-Luban-CodeGen.md) |
| GAS 中心管理器、下拉选择、时间轴作者入口和监测台 | [`Editor-Authoring.md`](Editor-Authoring.md) |

## 现有文档状态审查

| 文档 | 本轮审查结论 |
|---|---|
| `SKILL.md` | AI 工作流、硬约束和正式路由入口；不承担完整 API 手册。 |
| `EX-GAS.md` | 总导航、作者配置/生成物/运行时分层、初始化主链和能力缺口；不是专项 API 章节。 |
| `GameplayTag.md` | 本轮补齐后的正式标签专题，覆盖作者源、生成、整数码、层级查询、动态标签、需求协议和初始化后果。 |
| `Ability.md` | 原文主要是空的运行逻辑/编辑流程提纲；现已补成当前 `AbilityLogicBase`、`AbilitySpec`、Controller 和激活生命周期入口。 |
| `GameplayEffect.md` | 原文有正确的概念性 Apply/Activate 说明，但缺少当前 OOP/ECS API，且没有源码证据；现已改为组件配置和 `GameplayEffectSpec` 入口。 |
| `GameplayCue.md` | 原文包含表现职责边界，但使用了当前源码不存在的 Cue Spec 类型；现已改为 `GameplayCueBase`、`GameplayCueUnit` 和真实回调生命周期。 |
| `MMC.md` | 原文概念仍可表达线性/属性计算，但基类和内置实现类名已过时；现已按 `ModMagnitudeCalculationBase` 和当前内置类修订。 |
| `Attribute.md`、`AbilitySystemCell.md`、`TargetCatcher.md`、`AbilityTask-Timeline.md`、`XParam-Luban-CodeGen.md`、`Editor-Authoring.md` | 原 Wiki 没有对应的正式 AI 专题入口；本轮按源码新增，统一包含用途、入口、生命周期、示例、错误、禁止做法和证据。 |

## 运行时主链路

1. `GASManager.Initialize()` 创建 `EX_GAS_World`、系统组和全局计时器。
2. `AbilitySystemComponent.Awake()` 创建对应的 `AbilitySystemCell`；Cell 创建 ECS Entity，并建立基础数据、属性集、标签、GE 和 Ability 控制器。
3. `AbilitySystemComponent.Init(config)` 或 `AbilitySystemCell.Init(...)` 写入初始标签、属性集、基础技能和等级。
4. 生成配置将表格行转换成 `AbilityConfig`、`GameplayEffectConfig`、`GameplayCueConfig` 等 Runtime 配置。
5. Ability 通过 `TryActivateAbility` 进入 ECS 激活系统；Timeline 和 Task 在激活期间推帧；GE 通过 Apply/Activate/Deactivate/Remove 管线改变属性或触发 Cue。
6. 关闭进程内运行时调用 `GASManager.Shutdown()`，由插件统一完成任务、Cell、标签图、PlayerLoop、World 和静态状态释放。

标签图必须在使用标签比较和依赖标签的 GE 系统前完成初始化。`GASManager.Initialize()` 只创建 World 和系统，不会自动替 `TagHelper` 填充标签图。

## 当前能力与明确缺口

- 当前已确认有 GameplayTag 的静态表生成、父子关系缓存、固有标签和按来源记录的临时标签。
- 当前已确认的 Runtime TargetCatcher 是 `CatchSelf`、`CatchTarget` 和 `CatchAreaBox3D`。`CatchAreaBox2D.cs`、`CatchAreaCircle2D.cs` 当前是整文件注释，不是可用插件能力。
- 当前没有源码证据证明 EX-GAS 支持 Mod 在运行时动态合并 GameplayTag 作者表。现阶段只能写“当前未确认/未支持”。
- 标签查询仍必须发生在 `InitTagMap` 完成后；初始化入口现在会拒绝重复初始化、缺少 EX-GAS World 或已有标签图单例，查询发生在初始化前仍属于调用错误。
- 旧 Wiki 中的 `GameplayCueInstantSpec`、`GameplayCueDurationalSpec`、`GameplayCueSpec` 不是当前 Runtime 基类层级；当前入口是 `GameplayCueBase` / `GameplayCueBase<T>`。

## 源码证据入口

- 标签：`Runtime/Tag/GameplayTag.cs`、`Runtime/Tag/GameplayTagController.cs`、`Runtime/General/Helper/TagHelper.cs`、`Runtime/Tag/TagRequirementData.cs`。
- ASC：`Runtime/AbilitySystemCell/AbilitySystemCell.cs`、`Runtime/AbilitySystemCell/AbilitySystemComponent.cs`。
- Ability/Task/Timeline：`Runtime/Ability/`。
- GE/Attribute/MMC：`Runtime/Effect/`、`Runtime/Attribute/`、`Runtime/AttributeSet/`。
- Cue：`Runtime/Cue/`。
- 生成与编辑器：`Editor/CodeGen/`、`Editor/GASCenterEditor/`、`Editor/Ability/AbilityTimelineEditor/`、`Editor/GameplayAbilitySystem/GASSettingAsset.cs`。
