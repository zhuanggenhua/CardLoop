# EX-GAS TargetCatcher

## 用途

TargetCatcher 把一个 Ability 的主目标、施法者或空间查询转换成 `AbilitySystemCell` 列表，供 `TaskApplyEffects` 等任务逐个施加 GE。它只负责找目标，不负责伤害、标签条件或效果结算。

## 正式入口

- 基类：`TargetCatcherBase` 或 `TargetCatcherBase<TParam>`。
- 初始化：`Init(owner, activationContext)` 保存施法者和本次激活上下文；参数用 `InitParameters(XParam)`。
- 查询：推荐复用结果列表并调用 `CatchTargetsNonAllocSafe(mainTarget, ref results)`；旧的 `CatchTargets` 已标记 `Obsolete`，会产生新列表。
- 创建/注册：生成的 `XAbility.LoadAbilityCode()` 调用 `TargetCatcherHelper.RegisterTargetCatcher`；运行时通过 `TryCreateTargetCatcher` 按生成注册名创建。
- 现有插件 Runtime 类型：`CatchSelf`、`CatchTarget`、`CatchAreaBox3D`。`CatchAreaBox2D.cs` 和 `CatchAreaCircle2D.cs` 当前是注释代码，不能作为插件正式能力。

## 生命周期

1. 类型注册完成后，`XParamApplyEffects.CatcherType` 保存类型名，`Param` 保存对应 XParam。
2. `TaskApplyEffects.InitParameters` 创建 Catcher 并注入参数。
3. 任务开始时以 Ability Owner 和 `ActivationContext` 初始化 Catcher，再以主目标调用查询。
4. 任务遍历结果，并用 `GameplayEffectHelper.GetConfigByID` 创建 GE、以 Owner 为来源施加到每个目标。
5. Catcher 本身没有自动销毁阶段；自定义 Catcher 的临时缓存应在任务或自定义逻辑的生命周期中清理。

## 最小示例

```csharp
using System.Collections.Generic;
using GAS.Runtime;

var catcher = TargetCatcherHelper.TryCreateTargetCatcher(nameof(CatchSelf));
var results = new List<AbilitySystemCell>();
catcher.Init(ownerCell, null);
catcher.InitParameters(new XParamNone());
catcher.CatchTargetsNonAllocSafe(ownerCell, ref results);
```

应用效果的正式组合入口是 `TaskApplyEffects` + `XParamApplyEffects`：`IDs` 指向 GE 配置 ID，`CatcherType` 指向注册名，`Param` 是对应 Catcher 参数。自定义 Catcher 需要无参构造函数、泛型参数类型和生成注册。

## 常见错误

- 把 `mainTarget` 当作 Owner；`CatchSelf` 返回 Owner，`CatchTarget` 返回传入的 mainTarget。
- 忘记先 `Init`，导致自定义 Catcher 没有 Owner 或激活上下文。
- 使用 `CatchTargets` 作为高频查询，造成额外 GC；应复用列表调用 NonAlloc 入口。
- 把注释中的 2D Catcher 当作当前插件 Runtime API。项目侧如果有额外 2D 注册，应单独标为项目能力。
- 在 Catcher 中直接修改属性、标签或 GE，破坏“只捕获目标”的职责边界。

## 禁止做法

- GamePlay 自建目标捕获协议、目标列表缓存和区域碰撞转 Cell 逻辑。
- 用 Catcher 取代 `GameplayEffect` 或 `AbilityTask` 的执行职责。
- 手改生成的 `XAbility.gen.cs` 添加注册项；应由类型扫描和生成流程更新。

## 源码证据

- `Runtime/Ability/TargetCatcher/TargetCatcherBase.cs`
- `Runtime/Ability/TargetCatcher/TargetCatcherHelper.cs`
- `Runtime/Ability/TargetCatcher/CatchSelf.cs`
- `Runtime/Ability/TargetCatcher/CatchTarget.cs`
- `Runtime/Ability/TargetCatcher/CatchAreaBox3D.cs`
- `Runtime/Ability/AbilityTask/CommonTask/TaskApplyEffects.cs`
- `Runtime/General/XParam/Ability/XParamApplyEffects.cs`
- `Editor/CodeGen/CodeGeneratorAbilityPart.cs`
- `Assets/Scripts/Gen/XAbility.gen.cs`
