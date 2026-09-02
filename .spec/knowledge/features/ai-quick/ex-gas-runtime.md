---
name: ex-gas-runtime
description: CardLoop 对 EX-GAS 2.0.4 的运行时接口校准、项目使用边界和已确认缺口。
metadata:
  type: doc
  role: adapter
  source: EX-GAS-2.0 README + local 2.0.4 source
  status: 已交付
  update_triggers: package-version-change, public-api-change, generator-change
---

# EX-GAS 2.0.4 项目运行时速查

本文按“CardLoop 要完成什么”列出当前本地 `2.0.4` 可直接调用的正式接口。它是官方 `EX-GAS-2.0` README 的项目版本校准，不属于插件作者随包文档，也不把 ECS 内部 Buffer 当成项目业务 API。

## 版本与证据规则

- 官方设计基线：`EX-GAS-2.0` 分支 README，提交 `779b37a525ceccdaaa751c26c68a0a15836d0161`。
- 官方 README：`https://github.com/No78Vino/gameplay-ability-system-for-unity/blob/EX-GAS-2.0/README.md`。
- 当前行为真相：本地 `Assets/Plugins/GAS/Runtime` 与生成的 `Assets/Scripts/Gen`。
- 官方 README 与源码不一致时，以本地源码为准。
- `#if UNITY_EDITOR` 内的方法只供编辑器调试，不能作为玩家构建、存档或联机接口。

官方 2.0 的关键架构事实是：底层从旧 Mono 实现转为 DOTS/ECS，并把数据层与逻辑层分离；官方 Excel/Luban/JSON 是推荐作者工作流，不是强制数据源。项目可以提供自己的 SO 或配置来源，但最终必须构造插件的 `AbilitySystemCellConfig`、`AbilityConfig`、`GameplayEffectConfig` 等正式运行时配置，不能复制一套 Ability、Tag、Effect 运行时。

## 初始化与关闭

| 现实任务 | 当前接口 | 说明 |
|---|---|---|
| 创建 EX-GAS World | `GASManager.Initialize()` | 只创建 World、系统组、逻辑计时器，不加载配置表，也不初始化标签图。 |
| 加载 Luban 配置 | `XLauncher.InitConfigTables(loader)` | 调用生成的 `XLuban.Init(loader)`；加载器由项目资源系统提供。 |
| 初始化生成缓存和标签图 | `XLauncher.Launch()` | 当前生成顺序是类型缓存、`GASManager.Initialize()`、`XTag.InitTagList()`。项目已有正式启动 owner 时，不应再调用第二遍。 |
| 运行或暂停 GAS 更新 | `GASManager.Run()` / `Stop()` | 只控制插件 PlayerLoop 更新，不等于创建或销毁 World。 |
| 完整释放 | `GASManager.Shutdown()` | 释放 Cell、标签图、原生容器、PlayerLoop 和 World。 |
| 读取逻辑时间 | `GASManager.CurrentFrame` / `CurrentTurn` | 插件同时提供 `TurnController`；游戏回合规则仍由拥有剧本生命周期的模块决定。 |

## 从作者数据取得运行时配置

| 内容 | 生成入口 | 交给谁使用 |
|---|---|---|
| ASC 预设 | `XLuban.GetAscConfig(id)` | `AbilitySystemComponent.Init` 或 `AbilitySystemCell.Init` |
| Ability | `XLuban.GetAbilityConfig(id)` | `AbilitySystemCell.GrantAbility` |
| GameplayEffect | `XLuban.GetGameplayEffectConfig(id)` | 用 `ComponentConfigs` 构造 `GameplayEffectSpec` 后 Apply |
| GameplayCue | `XLuban.GetGameplayCueConfig(id)` | Cue 配置或 GE Cue 组件 |
| MMC | `XLuban.GetMmcConfig(id)` | GE Modifier 配置 |

官方 2.0 允许替换 Excel/Luban 数据方案。替换的是“如何产生这些 Config”，不是另建 Ability、Tag、GE 或 ASC 运行时。

## 创建和拥有角色能力状态

纯代码对象由创建者负责释放：

```csharp
var cell = new AbilitySystemCell();
cell.Init(config.BaseTags, config.AttrSets, config.BaseAbilities, config.Level);
// 使用 cell
cell.Dispose();
```

Unity 对象使用 `AbilitySystemComponent`，由组件在 `Awake` 创建唯一 Cell：

```csharp
abilitySystemComponent.Init(XLuban.GetAscConfig(ascId));
AbilitySystemCell cell = abilitySystemComponent.Cell;
```

`AbilitySystemComponent` 是 GameObject 生命周期适配器，`AbilitySystemCell` 是 OOP 运行时门面；两者指向同一份 ASC 状态。

## 标签

| 现实任务 | 正式接口 |
|---|---|
| 查询角色是否拥有某标签语义 | `cell.HasTag(tagCode)` |
| 查询全部或任一标签 | `cell.HasAllTags(codes)` / `HasAnyTags(codes)` |
| 添加或移除固有标签 | `AddFixedTag(s)` / `KillFixedTag(s)` |
| 比较两个静态标签的父子包含关系 | `TagHelper.HasTag(actualTag, queryTag)` |
| 使用生成标签码 | `XTag.*` |

当前玩家构建没有公开的“枚举 Cell 全部固有标签”接口。`AbilitySystemCell.FixedTags()` 被 `UNITY_EDITOR` 包围，只能用于编辑器监测。

## 属性与属性集

| 现实任务 | 正式接口 |
|---|---|
| 读当前计算值 | `cell.GetAttrCurrentValue(attrSetCode, attributeCode)` |
| 读基础值 | `cell.GetAttrBaseValue(attrSetCode, attributeCode)` |
| 修改基础值 | `cell.SetAttrBaseValue(attrSetCode, attributeCode, value)` |
| 使用生成码 | `XAttrSet.*`、`XAttribute.*` |

`CurrentValue` 是 EX-GAS 根据基础值与活动效果计算出的运行值，不应作为另一份作者真相写回。当前玩家构建不能从 Cell 枚举全部属性集和属性；`AbilitySystemCell.AttrSets()` 仅存在于编辑器条件编译中。

## Ability

| 现实任务 | 正式接口 |
|---|---|
| 授予或移除 | `cell.GrantAbility(config)` / `RemoveAbility(code)` |
| 激活、结束、取消 | `TryActivateAbility` / `TryEndAbility` / `TryCancelAbility` |
| 查询是否激活 | `cell.IsAbilityActive(code)` |
| 取得单个技能实例 | `cell.GetAbilitySpec(code)` |
| 读取或设置技能等级 | `AbilitySpec.Level` / `SetLevel(level)` |
| 检查能否激活及原因 | `AbilitySpec.CanActivate` / `CheckActivation()` |
| 触发 Cost 或 Cooldown | `AbilitySpec.DoCost()` / `DoCooldown()` |

`AbilityController.GetAllAbilitySpecs()` 虽是 `public`，但 Cell 不公开 Controller；项目不能通过 Cell 枚举已授予技能。不要用反射或 ECS Buffer 绕过这一边界。

## GameplayEffect

```csharp
GameplayEffectConfig config = XLuban.GetGameplayEffectConfig(effectId);
GameplayEffectSpec effect = new GameplayEffectSpec(config.ComponentConfigs);
effect.ApplyTo(targetCell, sourceCell);
```

也可由 `sourceCell.ApplyGameplayEffectTo(effect, targetCell)` 施加。具体组件语义回到插件 [`GameplayEffect.md`](../../../../Assets/Plugins/GAS/Wiki/GameplayEffect.md)。

## 其它能力入口

- Cue：使用 `GameplayCueConfig`、`GameplayCueUnit`、`CueHelper`；表现逻辑继承 `GameplayCueBase`。
- Timeline：Ability 配置使用 `ALTimeline` 与生成的 Timeline 数据。
- 目标捕获：实现 `TargetCatcherBase`。
- 插件事件：使用 `GASEventCenter`；项目领域事件不因此迁入 GAS。
- 运行参数：使用 `XParam` 及其强类型子类，不复制无约束参数协议。

## 调试接口不是业务接口

官方 `GASWatcher` 可以在编辑器中读取 ASC 等级、属性、固有/临时标签、Ability 和 GE。这证明底层状态存在，不代表玩家构建已经有稳定的 OOP 导出契约。存档、联机和 Mod API 必须基于公开运行时契约重新裁决，不能直接依赖 Watcher 或 ECS 内部布局。

## 当前已确认边界

- 有 OOP 门面，可以在项目玩法层避免直接操作 ECS。
- 数据来源可替换，但插件运行时职责不可在项目侧复制。
- 没有完整 ASC 快照 API，也没有非 Editor 的全状态枚举 API；这只是接口事实，不自动构成项目需求或插件缺陷。
- 官方把 RPC/网络同步列为 3.0 后续计划；2.0.4 的 GAS 状态复制需由项目另行设计并明确权威边界。
- 未确认支持 Mod 在运行时动态合并 GameplayTag 表。

## 源码入口

- 生命周期：`Assets/Plugins/GAS/Runtime/General/GASManager.cs`
- ASC：`Assets/Plugins/GAS/Runtime/AbilitySystemCell/AbilitySystemCell.cs`、`AbilitySystemComponent.cs`
- Ability：`Assets/Plugins/GAS/Runtime/Ability/AbilitySpec.cs`、`AbilityController.cs`
- GE：`Assets/Plugins/GAS/Runtime/Effect/GameplayEffectSpec.cs`、`GameplayEffectConfig.cs`
- 标签：`Assets/Plugins/GAS/Runtime/Tag/GameplayTagController.cs`、`Assets/Plugins/GAS/Runtime/General/Helper/TagHelper.cs`
- 属性：`Assets/Plugins/GAS/Runtime/AttributeSet/AttrSetController.cs`
- 配置转换：`Assets/Scripts/Gen/XLuban.gen.cs`
