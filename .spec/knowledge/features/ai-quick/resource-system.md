---
name: resource-system
description: CardLoop 资源入口速查：复用 YooAsset 官方能力，并记录项目自己的资源包选择、资产句柄生命周期和 Mod 包边界。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: official-entry + project-source
  status: 已交付
  verified_at: 2026-08-04
  update_triggers: yooasset-version-change, resource-system-api-change, package-lifecycle-change, address-policy-change
---

# 资源系统项目入口

## 用途

运行时按明确地址加载资产、实例化 Prefab、释放句柄，并在默认 YooAsset 包和 Mod 独立包之间选择资源来源。资源地址只负责定位和加载，不是玩法内容稳定 ID。

## 官方文档入口

YooAsset 官方文档负责解释 `ResourcePackage`、资产句柄和包生命周期；项目直接复用这些能力：

- [YooAsset README](../../../../Packages/com.tuyoogame.yooasset/README.md)
- [YooAsset package.json](../../../../Packages/com.tuyoogame.yooasset/package.json)
- [UnitySkills YooAsset 初始化与生命周期](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/yooasset-design/INIT.md)
- [UnitySkills YooAsset 加载](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/yooasset-design/LOADING.md)
- [UnitySkills YooAsset 句柄](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/yooasset-design/HANDLES.md)

本卡不重复说明 YooAsset 的完整 API，只记录项目没有交给官方默认入口的部分。

## 项目正式入口

业务代码使用 `GameCore.ResourceSystem`、`ResourceHandle<T>`、`ResourceCache<TAsset>` 和 `SoftAssetReference<T>`。项目没有要求 Gameplay 直接调用 `YooAssets.LoadAssetAsync`。

| 现实需求 | 项目入口 | 项目特有规则 |
|---|---|---|
| 初始化默认包 | `ResourceSystem.InitializeAsync(CancellationToken)` | 由 `GameManager.Start` 等待完成；默认包来自 `YokiFrame.YooInit`。 |
| 加载资产 | `ResourceSystem.LoadAssetAsync<T>(string address)` | 只接受字符串地址；先查已加载 Mod 包，再查默认包。 |
| 场景包选择 | 内部 `ResourceSystemSceneLoaderPool` | 通过 ResKit 官方扩展点给 SceneKit 选择默认包 / Mod 包；业务层不直接调用。 |
| 批量发现内容资产 | `ResourceSystem.LoadAssetsByAssetTagAsync<T>(string assetTag)` | 按 YooAsset 构建清单标签读取默认包和已加载 Mod 包；标签只用于资源发现，不是 GAS 标签或内容 ID。 |
| 实例化 Prefab | `ResourceSystem.InstantiateAsync<T>(...)` | 使用 `ResourceHandle<T>`；释放实例句柄会销毁实例。 |
| 软引用 | `SoftAssetReference<T>.LoadAsync()` | 保存地址，不替代 `DatabaseEntryReference<T>` 的稳定身份。 |
| 地址缓存 | `ResourceCache<TAsset>` | 按地址持有句柄，可用版本号释放一批资源。 |
| 释放 | 句柄 `Dispose()`、`ReleaseAsset`、`ReleaseInstance` | 创建方负责在自己的生命周期结束时释放。 |

场景生命周期已经归 YokiFrame `SceneKit`，详见 [`scene-system.md`](scene-system.md)。`ResourceSystem` 只在 SceneKit 的 ResKit 后端选择资源包，不再公开第二个场景句柄或场景加载 API。测试代码仍可用 `SceneManager` 进入最初的测试入口场景，因为此时资源系统尚未启动。

`LoadAssetsAsync` 只支持明确地址集合。当前项目没有 Addressables 标签交集语义；`MergeMode.Intersection` 会抛出 `NotSupportedException`。

`LoadAssetsByAssetTagAsync` 是项目对 YooAsset `ResourcePackage.GetAssetInfos(tag)` 的正式复用入口。它会为标签命中的每个资源建立并持有句柄，调用方负责释放返回的合并句柄。Gameplay 当前由 `ScenarioDirector` 在开局时取得该句柄并构建 `ContentIndex`，内容索引随对应 `ScenarioRun` 存续；`ResourceSystem` 不保存玩法索引。

## 生命周期

`GameManager.Start` 的启动顺序是：等待 `ResourceSystem.InitializeAsync`，等待 `ModAPI.Initialize`，再初始化 GAS 和 GameCore 系统。

`GameManager.OnDestroy` 的资源收尾由 `ResourceSystem.Shutdown` 负责：释放活动资产操作，销毁已注册资源包，最后销毁 YooAsset 全局系统。Mod 资源包不能在业务侧自行销毁；如果 SceneKit 仍在使用该包提供的场景，卸载 Mod 包会明确失败，调用方必须先通过 `MapSystem` 切离场景。

未初始化时，`ResourceSystem` 的公开加载、检查和 Mod 包入口会进入 `EnsureInitialized` 并抛出异常。

## 最小真实示例

资产句柄必须在使用结果期间保持有效：

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;

private async UniTask LogPrefabNameAsync(string address)
{
    ResourceHandle<GameObject> handle =
        ResourceSystem.LoadAssetAsync<GameObject>(address);
    try
    {
        GameObject prefab = await handle.ToUniTask();
        Debug.Log(prefab.name);
    }
    finally
    {
        handle.Dispose();
    }
}
```

实例句柄释放会销毁实例，所以实例使用必须发生在释放之前：

```csharp
private async UniTask UseInstanceAsync(string address, Transform parent)
{
    ResourceHandle<GameObject> handle =
        ResourceSystem.InstantiateAsync<GameObject>(address, parent);
    try
    {
        GameObject instance = await handle.ToUniTask();
        instance.SetActive(true);
    }
    finally
    {
        ResourceSystem.ReleaseInstance(handle);
    }
}
```

## 常见错误

- 在 `GameManager` 启动完成前调用资源加载、软引用加载或 Mod 包加载。
- 使用 `ResourceHandle` 得到结果后忘记释放，或在仍使用结果时提前释放。
- 把 Unity GUID、文件路径、资源名和 YooAsset 地址并列当成同一个内容的多个稳定 ID。
- 把整数、标签对象或 Addressables 标签传给只接受字符串地址的项目入口。
- 让 Gameplay 自己维护默认包、Mod 包选择或第二套资源地址表。

## 禁止做法

- 不在 Gameplay / GameCore 新建第二套 YooAsset 加载器、包注册表或地址引用。
- 不恢复 `SceneResourceHandle` 或 `ResourceSystem.LoadSceneAsync`；场景生命周期直接归 SceneKit。
- 不把 `ResourceCache<TAsset>` 当成数据库内容索引；它只管理资源加载缓存。
- 不在场景切换时直接调用 `YooAssets.Destroy()`；完整销毁只由 `ResourceSystem.Shutdown()` 负责。
- 不让 UI、全局状态系统或旧主菜单脚本写死基础设施场景名并直接切场景；应用级主菜单流程要等真实 Director / 流程职责出现后再实现。
- 不把 Mod 的 `packageName`、资源地址或文件路径升级为玩法内容唯一 ID。

## 源码证据

- 项目资源入口：[`ResourceSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystem.cs) 的 `InitializeAsync`、`LoadAssetAsync`、`InstantiateAsync`、`LoadModPackageAsync`、`Shutdown`、`EnsureInitialized`。
- SceneKit 多包后端：[`ResourceSystemSceneLoaderPool.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystemSceneLoaderPool.cs)。
- 句柄释放：[`ResourceHandle.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceHandle.cs) 的 `ResourceHandle<T>`、`Dispose`、`AwaitResultAsync`。
- 缓存和软引用：[`ResourceCache.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceCache.cs)、[`SoftAssetReference.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/SoftAssetReference.cs)。
- 实例销毁行为：`ResourceSystem.cs` 内部 `InstantiateResourceOperationState.ReleaseCore`。
- 启动链：[`GameManager.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/GameManager.cs) 的 `Start` 和 `OnDestroy`。
