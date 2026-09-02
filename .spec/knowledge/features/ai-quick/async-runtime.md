---
name: async-runtime
description: CardLoop 异步入口速查：引用 UniTask 官方文档，并记录资源、Mod 和 Unity 生命周期中的实际异步边界。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-source
  status: 已交付
  update_triggers: unitask-version-change, cancellation-policy-change, async-entry-change
---

# UniTask 项目使用

## 用途

为资源初始化、Mod 扫描、压缩包解压、资源句柄等待和命令延时提供异步流程，并明确谁等待、谁观察异常、谁释放资源。

## 官方文档入口

UniTask 的通用 API、PlayerLoop、取消、组合和线程切换直接复用官方资料：

- [UniTask 官方仓库](https://github.com/Cysharp/UniTask)
- [当前包 package.json](../../../../Packages/com.cysharp.unitask/package.json)
- [UnitySkills async 设计入口](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/async/SKILL.md)
- [UnitySkills unitask-design](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/unitask-design/SKILL.md)

当前工程包版本是 `2.5.11`；迁入的 `unitask-design` 标注 `2.5.10`，版本特有行为不能只看后者。

## 项目正式入口

项目不新增 `TaskRunner`、`AsyncHelper` 或全局异步包装层。当前实际使用入口如下：

| 项目流程 | 实际调用 |
|---|---|
| 资源启动 | `ResourceSystem.InitializeAsync(..., CancellationToken)` |
| 资源句柄等待 | `ResourceHandle<T>.ToUniTask()` |
| Mod 启动 | `ModAPI.Initialize(..., CancellationToken)`、`ModLoader.LoadAllModsAsync` |
| 压缩包并行解压 | `ModAPI.UnZipAllAsync` 中的 `UniTask.RunOnThreadPool`、`UniTask.WhenAll` |
| Unity 对象销毁取消 | `GameManager.Start` 将 `destroyCancellationToken` 传给资源和 Mod 启动 |
| 命令延时 | `Wait` 命令使用 `UniTask.WaitForSeconds` |

## 生命周期

资源句柄的拥有者必须在 `finally` 中释放。`ResourceSystem.Shutdown` 会释放活动资源操作，但不能替代业务对象自己的句柄管理。

`ResourceSystem.InitializeAsync` 的取消只撤销本次项目资源启动结果。YokiFrame 的底层初始化没有完整的中途回滚入口，因此项目入口会等待它收敛到可释放状态，再统一回滚，不能把“中断等待”误说成“第三方资源已立即清理”。

`ModAPI.Initialize` 接受 `CancellationToken`，并把同一令牌传给 `IModLoader` 和 `ResourceSystem.LoadModPackageAsync`。目录扫描、逐 Mod 处理、逐包加载和包初始化各阶段都会检查取消；已经开始的 YooAsset 单次操作没有立即中断入口，因此会自然结束后销毁未发布包，并停止加载后续包。取消结果不会提交为已初始化状态，资源包的最终释放仍由 `ResourceSystem` 统一负责。

使用 `UniTaskVoid` 或 `.Forget()` 时，必须沿用项目已有的异常观察方式；不能让异步异常无人等待、无人记录。

## 最小真实示例

这是项目资源句柄和取消检查的组合，不是重新包装 UniTask：

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

private async UniTask LogTextAsync(
    string address,
    CancellationToken cancellationToken)
{
    ResourceHandle<TextAsset> handle =
        ResourceSystem.LoadAssetAsync<TextAsset>(address);
    try
    {
        TextAsset asset = await handle.ToUniTask();
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Log(asset.text);
    }
    finally
    {
        handle.Dispose();
    }
}
```

## 常见错误

- 把 UniTask 通用 API 复制到项目卡，造成与官方资料的第二份真相。
- 创建资源句柄却不等待、不释放，或异常路径没有释放。
- 在线程池任务中访问 UnityEngine 对象；当前项目线程池调用只用于明确的压缩包文件处理。
- 把 Mod 取消令牌理解成能立即中断 `IModLoader` 的所有文件和资源包操作，或把 `ModAPI.Shutdown` 当作资源包回收。
- 使用 `.Forget()` 却没有异常记录。
- 混合 Task、协程和 UniTask，却没有说明线程、帧时序和取消边界。

## 禁止做法

- 不新建只转发 UniTask 的项目工具类。
- 不把线程池任务当作 Unity 主线程调度器。
- 不因为官方示例存在某个 API 就绕过项目的资源句柄和 Mod 生命周期。
- 不把 `unitask-design` 的 `2.5.10` 标记当成当前 `2.5.11` 包的最终事实。

## 源码证据

- 当前版本：[`Packages/com.cysharp.unitask/package.json`](../../../../Packages/com.cysharp.unitask/package.json)。
- 资源异步：[`ResourceSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystem.cs) 的 `InitializeAsync`、`ToUniTask` 和各类 `AwaitResultCore`。
- Mod 异步：[`ModAPI.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModAPI.cs) 的 `Initialize`、`UnZipAllAsync`、`LoadModInfo`；[`ModLoader.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModLoader.cs) 的 `LoadAllModsAsync`。
- 启动取消：[`GameManager.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/GameManager.cs) 的 `Start` 与 `OnDestroy`。
