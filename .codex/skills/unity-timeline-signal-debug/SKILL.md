---
name: unity-timeline-signal-debug
description: "排查 Unity Timeline Signal 链路。用于 PlayableDirector、SignalTrack、SignalEmitter、SignalReceiver、场景 binding、SignalAsset、listener 未触发、marker 到点无反应或业务未执行。"
---

# Unity Timeline Signal 排查

## 作用

把 `Timeline Signal -> SignalReceiver -> UnityEvent listener -> 业务入口` 这条链一次查通，不靠猜。

这个技能适合：
- `Timeline` 播到了，但 `Signal` 没触发
- `SignalReceiver` 看起来有绑定，但业务方法没进
- `PlayableDirector`、`SignalTrack`、`SignalEmitter`、`SignalAsset`、`listener` 之间有一处断了
- 改了 `.playable` 或 `.unity` 文本后，不确定 Unity 现场有没有吃到新值

## 参考依据

优先参考以下来源：
- Unity Timeline 包源码注释（官方包本地缓存）
  - `Library/PackageCache/com.unity.timeline@1.6.5/Runtime/Events/Signals/SignalReceiver.cs`
  - `Library/PackageCache/com.unity.timeline@1.6.5/Runtime/Events/Signals/SignalEmitter.cs`
  - `Library/PackageCache/com.unity.timeline@1.6.5/Runtime/Events/SignalTrack.cs`
- Unity 自动化入口
  - 全局 `D:\codex-home\skills\unity-skills\SKILL.md`
  - 本项目本地包 `Packages/com.besty.unity-skills`、`Packages/com.aibridge.unity`

从这些依据吸收的关键结论：
- `SignalTrack` 的 binding 类型就是 `SignalReceiver`
- `SignalEmitter` 发出的通知只有在对应 `SignalTrack` 真正绑定到某个 `SignalReceiver` 时才会被消费
- `SignalReceiver` 收到通知后，会按 `SignalAsset` 查到对应 `UnityEvent`，再调用 persistent listener

## 开始前必做

如果在当前项目里使用，先读：
- `AGENTS.md`
- `.spec/AGENTS.md`
- `.spec/rules/system.md`

如果问题绑定具体场景，再补读：
- 目标场景 `.unity`
- 目标 Timeline `.playable`
- 业务消费脚本

## 核心原则

1. 先查静态链路，再补最小日志。
2. 不把“Inspector 里看起来有东西”等同于“Timeline 真能打到”。
3. `SignalAsset`、`SignalTrack binding`、`listener target/method` 三者必须同时成立。
4. 同一条链路日志只用一个前缀，一次搜索拿全链。
5. 修改 `.playable`/`.unity` 磁盘真相后，必须区分“文件改对了”和“Unity 已重载生效”。

## 标准工作流

### 1. 先收窄问题

至少明确：
- 哪个场景
- 哪个 `PlayableDirector`
- 哪个 `.playable`
- 哪个 `SignalTrack`
- 期望打到哪个业务方法

### 2. 查 Timeline 资产真相

先在 `.playable` 里确认：
- 是否真的有 `SignalTrack`
- 对应 `SignalEmitter` 的 `m_Time`
- `m_Asset` 指向哪个 `SignalAsset`
- `retroactive / emitOnce` 当前值

常见搜索词：
- `Signal Emitter`
- `m_Asset:`
- `m_Time:`
- 轨道名

### 3. 查 PlayableDirector 场景 binding

这是最容易漏掉的一跳。

在目标场景 `.unity` 的 `PlayableDirector.m_SceneBindings` 里确认：
- `key` 是否存在目标 `SignalTrack`
- `value` 是否指向 `SignalReceiver` 组件的 `fileID`

结论规则：
- `key` 在、`value: {fileID: 0}`：这条 `SignalTrack` 实际没绑任何 `SignalReceiver`
- `key` 不在：Timeline 资产改了，但场景里的 `PlayableDirector` 没重新建立 binding
- `value` 指错组件：信号会打到错误对象

### 4. 查 SignalReceiver 真相

在场景 `.unity` 里确认目标 `SignalReceiver` 组件：
- `m_Signals` 里是否有目标 `SignalAsset`
- `m_Events` 里是否有对应 `UnityEvent`
- persistent listener 的：
  - `m_Target`
  - `m_TargetAssemblyTypeName`
  - `m_MethodName`

这一步要防两个误判：
- `SignalReceiver` 存在，但注册的是另一个 `SignalAsset`
- `SignalAsset` 对了，但 listener 还绑着旧方法名

### 5. 查业务入口

确认 listener 指向的方法：
- 是否公开可调用
- 是否只是桥接方法，真正业务是否继续走下去
- 有没有早退条件

如果用户已明确上游条件成立，就不要擅自退回去重查一大圈前置业务。

### 6. 仍未锁定时补最小日志

沿同一前缀补三类日志就够：
- `PlayableDirector` 已播放且越过 signal 时间点
- 当前 `SignalReceiver` 里注册了哪些 `SignalAsset -> listener`
- 目标业务方法是否真的被调用

推荐日志内容：
- `director.name`
- `director.time`
- `SignalReceiver.Count()`
- 每个 `SignalAsset.name`
- 每个 persistent listener 的 `target.method`
- 业务入口调用次数

## 当前项目的工具用法

只读检查优先用：
- `rg`
- `Get-Content`
- Unity Console 读取需走当前可用的 UnitySkills/AIBridge 正式入口；本项目没有迁入 `.codex/skills/aibridge/bridge.py` 脚本。

如果要改 Unity 编辑器现场，必须走当前项目已有的安全包装入口或正式写锁入口。

不要裸调会写现场的 AIBridge 命令。

## 常见根因清单

### 1. SignalTrack 没绑到 SignalReceiver

表现：
- Timeline 时间越过了 marker
- `SignalReceiver` 本身也有注册
- 业务方法就是没进

定位：
- 看 `PlayableDirector.m_SceneBindings`
- 目标 `SignalTrack` 的 `value` 是不是 `fileID: 0`

### 2. SignalAsset 不是同一个

表现：
- `.playable` 里有 signal
- `SignalReceiver` 里也有 signal
- 但名字相同不代表资源相同

定位：
- 直接比 `guid`
- 运行时可补 `GetInstanceID()` 日志

### 3. listener 还绑着旧方法

表现：
- `SignalReceiver` 能收到
- 但打到的是旧桥接方法、空方法或错误对象

定位：
- 看 `m_TargetAssemblyTypeName`
- 看 `m_MethodName`

### 4. 磁盘改了，Unity 现场没刷新

表现：
- 文本 diff 正确
- 运行时仍像旧配置

处理：
- 先 `AssetDatabaseCommand_Refresh`
- 必要时重载场景
- 再进播放态验证

## 输出方式

修完后至少说明：
- 根因落在哪一跳
- 改的是 `.playable`、`.unity`，还是代码
- 是否还需要 Unity 现场刷新/重载
- 用的日志前缀是什么

## 典型结论模板

- `SignalTrack` 存在，但 `PlayableDirector.m_SceneBindings` 对应 value 为 `fileID: 0`，因此 Timeline 到点不会把通知送到 `SignalReceiver`
- `SignalReceiver` 已注册目标 `SignalAsset`，listener 也正确，因此若业务没进，优先查 track binding，而不是继续怀疑业务方法

