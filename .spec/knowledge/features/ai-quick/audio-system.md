---
name: audio-system
description: CardLoop 音频项目入口速查：复用 BroAudio 的声音库和 SoundID，并记录项目通道、解析器、暂停停止和回调生命周期。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-source
  status: 已交付
  update_triggers: broaudio-version-change, sound-library-change, audio-channel-change, playback-lifecycle-change
---

# 音频系统项目入口

## 用途

统一处理 BGM、界面音效、战斗音效和其它声音的通道、音量、暂停、停止、位置播放和完成回调。业务侧提供项目数据库中的 `AudioClipResolver`，不直接管理底层播放器。

## 官方文档入口

BroAudio 官方资料负责声音库、`SoundID`、编辑器和底层播放 API；当前包版本是 `3.2.2`：

- [BroAudio package.json](../../../../Packages/com.ami.broaudio/package.json)
- [BroAudio 包内 Documentation.txt](../../../../Packages/com.ami.broaudio/Documentation~/Documentation.txt)
- [BroAudio 官方在线文档](https://man572142s-organization.gitbook.io/broaudio/)

项目只在本卡说明“业务应该从哪个项目入口进入”，不复制 BroAudio 的声音库制作手册。

## 项目正式入口

BroAudio 是底层能力；CardLoop 业务直接使用 `AudioSystem`、`AudioClipResolver` 和 `AudioChannel`。

| 现实需求 | 项目入口 | 责任 |
|---|---|---|
| 定义可播放声音 | `AudioClipResolver` | 保存有效 `SoundID`、传统 `AudioClip[]` 兜底、选择策略和目标通道。 |
| 普通播放 | `AudioSystem.Play` | 根据 `targetChannel` 找到项目通道。 |
| 位置 / 跟随播放 | `AudioSystem.PlayAt` / `PlayAttached` | 交给目标 `AudioChannel`。 |
| 跨模块播放请求 | `EventKit.Type.Send(new AudioPlaybackRequestedEvent(resolver))` | `AudioSystem` 负责监听并路由。 |
| 通道状态 | `AudioSystem.SetChannelVolumeScale`、`StopChannel`、`PauseChannel`、`ResumeChannel` | 保存并控制项目通道状态。 |

`AudioChannel` 内部优先使用有效 `SoundID` 调用 `BroAudio.Play`；无效时回退到 `AudioClipResolver.GetClip()`。这是项目已有行为，不是要求业务层直接操作 BroAudio。

## 生命周期

`AudioSystem.OnSystemStart` 校验通道、加载音量设置并注册 `AudioPlaybackRequestedEvent`；`OnSystemStop` 注销监听并保存设置。

`AudioChannel.Awake` 确认必需的 `AudioSource` 并初始化 fallback 播放池；禁用或销毁时停止播放并回收内部播放器。请求发生前必须确保 `targetChannel` 已绑定有效通道。

## 最小真实示例

跨模块播放使用项目已有的强类型事件：

```csharp
using YokiFrame;

private void RequestPlayback(AudioClipResolver resolver)
{
    if (resolver != null)
    {
        EventKit.Type.Send(new AudioPlaybackRequestedEvent(resolver));
    }
}
```

需要直接调用正式系统时：

```csharp
private void PlayAt(
    AudioSystem audioSystem,
    AudioClipResolver resolver,
    Vector3 position)
{
    audioSystem.PlayAt(resolver, position);
}
```

## 常见错误

- 直接在 Gameplay 调 `BroAudio.Play`，绕过项目通道、音量、暂停和 fallback 策略。
- `SoundID` 无效且没有可用的 `AudioClip` 兜底。
- `AudioClipResolver.targetChannel` 没有绑定到 `AudioSystem` 的通道字典。
- `AudioChannel` 缺少必需的 `AudioSource`。
- 通过多个 AudioSystem 或静态播放器重复保存同一通道音量状态。

## 禁止做法

- 不新建 `AudioManager` 取代 `AudioSystem`。
- 不把 `SoundID` 当成游戏内容唯一 ID，它只是 BroAudio 的播放选择入口。
- 不把内部 `PlaybackRuntime`、fallback 池和 `IAudioPlayer` 当成业务公开 API。
- 不把播放事件写成伤害、属性或死亡结算的作者源。

## 源码证据

- 项目系统：[`AudioSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/Systems/AudioSystem.cs) 的 `OnSystemStart`、`OnSystemStop`、`Play`、`PlayAt`、`PlayAttached`。
- 项目通道：[`AudioChannel.cs`](../../../../Assets/Scripts/GameCore/Runtime/Audio/AudioChannel.cs) 的 `PlayInternal`、`Pause`、`Resume`、`Stop`。
- 项目解析器：[`AudioClipResolver.cs`](../../../../Assets/Scripts/GameCore/Runtime/Database/Audio/AudioClipResolver.cs) 的 `TryGetSoundId`、`GetClip` 和 `targetChannel`。
- 底层调用位置：[`AudioChannel.PlaybackRuntime.cs`](../../../../Assets/Scripts/GameCore/Runtime/Audio/AudioChannel.PlaybackRuntime.cs) 的 `PlayBroAudio`。
- 项目事件：[`GameCorePresentationEvents.cs`](../../../../Assets/Scripts/GameCore/Runtime/Events/GameCorePresentationEvents.cs) 的 `AudioPlaybackRequestedEvent`。
