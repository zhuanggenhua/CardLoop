# EX-GAS AbilityTask / Timeline

## 用途

`AbilityTaskBase` 把 Ability 的时间段工作封装成 Begin、Tick、Finish；`ALTimeline` 用 `XParamTimeline` 的轨道和片段，在 Ability 激活期间按逻辑帧创建并推进任务。它适合把“某个时间段施加效果、播放 Cue、扣 Cost 或调试输出”配置化。

## 正式入口

- Task 基类：`AbilityTaskBase` 或 `AbilityTaskBase<TParam>`；实现 `InitParameters`，按需覆写 `OnBegin`、`OnTick`、`OnFinish` 和 `Dispose`。
- 内置 Task：`TaskApplyEffects`、`TaskPlayCue`、`TaskDoCooldown`、`TaskDoCost`、`TaskDoNothing`、`TaskDebug`。
- Timeline 逻辑：`ALTimeline` 使用 `XParamALTimelineID` 找到并缓存 `XParamTimeline`，由 `ALTimelinePlayer` 读取 `Tracks` 和 `TaskClipData`。
- 注册：生成的 `XAbility.LoadAbilityCode()` 注册 AbilityLogic 和 AbilityTask；`AbilityHelper.TryCreateAbilityTask` 按类型名创建。
- 作者工具：`EXTool/EX-GAS/Ability时间轴编辑器`（实际菜单名见 `AbilityTimelineEditorWindow` 的 EditorWindow 入口），数据读写由 `GasAbilityTimelineXlsxReadWrite` 完成。

## 生命周期

1. Timeline 表读取为 `XParamTimeline -> Track -> TaskClipData`；每个片段保存 `StartTime`、`EndTime`、`TaskType` 和 `Parameter`。
2. `ALTimeline.SetParam` 创建 Timeline 参数并调用 Player 的 `InitData`，为每个片段实例化 Task。
3. Ability 激活调用 `ALTimelinePlayer.Play`，从第 0 帧开始推进。
4. 到达片段起始帧调用 `Begin`，区间内每个逻辑帧调用 `Tick`，结束帧调用 `Finish`。
5. 播放到 `LifeTime` 后，如果 `ManualEndAbility` 为 false，Player 调用 `ALTimeline.TryEndSelf()`；否则只停止播放，等待 Ability 外部结束。
6. 取消/结束都会调用 Player.Stop，并让已缓存片段执行 `Finish`。

## 最小示例

```csharp
using GAS.Runtime;

var task = new TaskDebug(abilityLogic);
task.InitParameters(new XParamString("timeline task"));
task.Begin(0);
task.Tick(0);
task.Finish(1);

var timeline = new XParamTimeline();
timeline.SetLifeTime(30);
timeline.SetManualEndAbility(false);
timeline.Tracks.Add(new Track
{
    Name = "Main",
    TaskClips = new System.Collections.Generic.List<TaskClipData>
    {
        new TaskClipData
        {
            StartTime = 0,
            EndTime = 10,
            TaskType = nameof(TaskDebug),
            Parameter = new XParamString("debug")
        }
    }
});
```

片段真正运行时由 `TaskClipData.InstantiateTask` 调用 `AbilityHelper.TryCreateAbilityTask`；示例不能绕过类型注册把 Task 直接塞进 Timeline。

## 常见错误

- 把 Timeline 的时间当 Unity 秒；`ALTimelinePlayer` 使用 `GASTimer.FrameRate` 和逻辑帧，片段字段是帧索引。
- 自定义 Task 未实现匹配的 `XParam` 类型，泛型基类会在编辑器报告参数类型不匹配。
- 任务创建后没有保证 `Finish`，导致 Cue、临时目标或其他资源残留；Player.Stop 会 Finish 已缓存片段，但自定义资源仍需正确清理。
- 在 Task 中直接实现标签层级或效果结算，绕过 `TagHelper` 和 `GameplayEffect`。
- 把 Timeline 表或生成的 `XLuban` 解析代码当成手写业务入口。

## 禁止做法

- GamePlay 自建时间轴调度器、Task 注册表或片段序列化格式。
- 让 Task 取代 Ability 的激活/取消/结束状态机。
- 手改 `XAbility.gen.cs`、`XLuban.gen.cs` 或 Timeline JSON。

## 源码证据

- `Runtime/Ability/AbilityTask/AbilityTaskBase.cs`
- `Runtime/Ability/AbilityTask/CommonTask/TaskApplyEffects.cs`
- `Runtime/Ability/AbilityTask/CommonTask/TaskPlayCue.cs`
- `Runtime/Ability/TimelineAbility/ALTimeline.cs`
- `Runtime/Ability/TimelineAbility/ALTimelinePlayer.cs`
- `Runtime/Ability/TimelineAbility/Data/XParamTimeline.cs`
- `Runtime/Ability/TimelineAbility/Data/Track.cs`
- `Runtime/Ability/TimelineAbility/Data/TaskClipData.cs`
- `Runtime/Ability/AbilityHelper.cs`
- `Editor/Ability/AbilityTimelineEditor/EditorWindow/AbilityTimelineEditorWindow.cs`
- `Editor/Ability/AbilityTimelineEditor/DataClass/GasAbilityTimelineXlsxReadWrite.cs`
