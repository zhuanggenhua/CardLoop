# EX-GAS AbilitySystemComponent / AbilitySystemCell

## 用途

`AbilitySystemCell` 是一个角色或单位的 EX-GAS OOP 控制门面，统一持有基础数据、属性集、GameplayTag、GameplayEffect 和 Ability 控制器。`AbilitySystemComponent` 是把这个 Cell 挂到 Unity `GameObject` 上的 MonoBehaviour 适配器。

两者不是两套 ASC：Component 的 `Cell` 属性返回它创建的同一个 `AbilitySystemCell`。GamePlay 不应再建第三个角色能力容器。

## 正式入口

- 全局运行时：`GASManager.Initialize()` 创建 `EX_GAS_World`、系统组和全局计时器；`GASManager.Run/Stop` 控制运行状态，`GASManager.Shutdown()` 完整释放 World、标签图、绑定 Cell 和静态状态。
- 纯代码单位：`new AbilitySystemCell()` -> `Init(baseTags, attrSets, baseAbilities, level)` -> 使用 Cell API -> `Dispose()`。
- Unity 单位：在 GameObject 上挂 `AbilitySystemComponent`；`Awake` 创建 Cell，`OnEnable` 绑定 Entity，`Init(AbilitySystemCellConfig)` 写入配置，`OnDestroy` 释放。
- 主要操作：标签查询/添加、属性读写、GE Apply/Remove、Ability Grant/Activate/Cancel/End、获取 `AbilitySpec`。

## 生命周期

1. 先初始化 `GASManager`，否则 Cell 构造时无法创建 EX-GAS Entity。
2. Cell 构造建立 Entity 并创建 `BasicDataController`、`AttrSetController`、`GameplayTagController`、`GameplayEffectController`、`AbilityController`。
3. `Init` 按顺序加入固有标签、属性集、基础技能并设置等级；它不会替换已有内容，也不会自动确认标签图是否已初始化。
4. Component 启用/禁用时只负责 GameObject 与 Entity 绑定；Entity 的真正释放在 `OnDestroy`/`Dispose`。
5. 进程或测试场景关闭时调用 `GASManager.Shutdown()`；它会先完成已跟踪任务，再释放绑定 Cell、标签图、PlayerLoop 和 World。纯代码创建的 Cell 仍须由创建者先调用 `Dispose()`。

## 最小示例

```csharp
using GAS.Runtime;

GASManager.Initialize();
XTag.InitTagList();

var cell = new AbilitySystemCell();
cell.Init(
    new[] { XTag.Faction_Player },
    System.Array.Empty<AttrSetConfig>(),
    System.Array.Empty<AbilityConfig>(),
    level: 1);

bool isFaction = cell.HasTag(XTag.Faction);
cell.TryActivateAbility(1001);
cell.Dispose();
GASManager.Shutdown();
```

如果使用 Unity 组件，调用 `component.Init(new AbilitySystemCellConfig(tags, attrSets, abilities))`，不要自己在 `Awake` 再创建一个 Cell。

## 与其他入口的边界

- `AbilitySystemCell.HasTag` 读取当前角色的固有/临时标签；静态标签码的父子比较用 `TagHelper.HasTag`，详见 [`GameplayTag.md`](GameplayTag.md)。
- `AbilitySystemComponent.HasTag` 只是转发到 Cell。
- `Cell.GetAbilitySpec` 返回单个技能的 OOP 包装；技能逻辑仍由 AbilityLogic 承担。
- `Cell.ApplyGameplayEffectTo` 是对目标 Cell 施加已有 `GameplayEffectSpec` 的门面；GE 的具体创建和组件配置见 [`GameplayEffect.md`](GameplayEffect.md)。

## 常见错误

- 只调用 `GASManager.Initialize()` 就查询标签；它不会创建 `TagHelper` 的标签字典，必须随后完成生成的 `XTag.InitTagList()`。
- 在 `Dispose()` 后继续保存和使用 Cell 或 `AbilitySpec`，底层 Entity 已失效。
- 让同一个 GameObject 同时挂多个会各自创建 Cell 的 ASC 组件。
- 把 `AbilitySystemComponent` 当作所有 GAS 逻辑实现位置；它只是 Unity 生命周期和公开转发入口。
- 直接读取 ECS Entity、Buffer 作为业务 API，绕过 Cell 的 OOP 门面。

## 禁止做法

- GamePlay 自建 ASC、属性控制器、标签控制器或 GE 容器。
- 在项目层重写 `AbilitySystemComponent` 以抢占插件初始化和生命周期职责。
- 在未完成 GAS World 和标签图初始化时创建依赖标签的 GE/Ability/Cue。

## 源码证据

- `Runtime/AbilitySystemCell/AbilitySystemCell.cs`
- `Runtime/AbilitySystemCell/AbilitySystemComponent.cs`
- `Runtime/AbilitySystemCell/AbilitySystemCellConfig.cs`
- `Runtime/AbilitySystemCell/BasicDataController.cs`
- `Runtime/General/GASManager.cs`
- `Runtime/Tag/GameplayTagController.cs`
- `Runtime/Ability/AbilityController.cs`
- `Runtime/Effect/GameplayEffectController.cs`
