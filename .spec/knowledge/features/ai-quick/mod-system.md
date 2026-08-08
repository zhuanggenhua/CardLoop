---
name: mod-system
description: CardLoop 自有 Mod 入口速查：ModAPI、ModLoader、ModInfo、启停状态、独立 YooAsset 包和当前能力缺口。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: project-source + yooasset-official-entry
  status: 已交付
  verified_at: 2026-08-04
  update_triggers: mod-schema-change, mod-loader-change, resource-package-change, validation-policy-change
---

# Mod 系统项目入口

## 用途

发现 Mod 目录内容，读取描述文件，检查 API 版本和状态，按启用状态加载独立 YooAsset 资源包，并向 UI 提供 Mod 清单和刷新通知。

Mod 系统是 CardLoop 自有运行时能力，不是 YooAsset 或 EX-GAS 的官方能力；它不直接接管 Gameplay、EX-GAS、存档或数据库作者源。

## 官方资料入口

当前没有可引用的第三方“CardLoop Mod API”官方手册。Mod 扫描、校验、启停状态和内容契约以本项目源码为准；独立资源包的底层生命周期复用 [YooAsset 官方入口](../../../../Packages/com.tuyoogame.yooasset/README.md) 和 [YooAsset 初始化文档](../../../../Packages/com.besty.unity-skills/unity-skills~/skills/yooasset-design/INIT.md)。

## 项目正式入口

| 现实需求 | 项目入口 |
|---|---|
| Mod API | `GameCore.ModAPI` |
| 目录扫描和包加载 | `GameCore.ModLoader` |
| 描述清单 | `GameCore.ModInfo` |
| 启停状态持久化 | `GameCore.ModConfig` / `ModState` |
| 独立资源包 | `GameCore.ResourceSystem.LoadModPackageAsync` |

`ModInfo.packageName` 必须对应独立 YooAsset 包；`ModAPI.DefaultAPIVersion` 当前为 `0.1.0`；`loadOrder` 用于当前项目的 Mod 包解析顺序。

## 生命周期

`GameManager.Start` 调用 `await ModAPI.Initialize()`。随后 Mod 系统创建或读取 `ModConfig`，扫描 LoadingPath，解压 zip，读取目录内的 `*.cfg`，检查状态和 API 兼容性，再让 `ResourceSystem.LoadModPackageAsync` 初始化启用 Mod 的独立包。

初始化完成后通过 `ModAPI.CreateInfoSnapshot()` 读取清单，通过 `GetModState`、`SetModEnabled` 和 `DeleteMod` 修改持久化状态。`SetModEnabled` 当前只保存状态并触发刷新通知，不会在本次运行中自动卸载或重新加载已经初始化的包。

`GameManager.OnDestroy` 调用 `ModAPI.Shutdown()` 清理 Mod 状态，再由 `ResourceSystem.Shutdown()` 释放资源包。`ModAPI.Shutdown` 自身不销毁 YooAsset 包。

## 最小真实示例

正式游戏由 `GameManager` 初始化；读取已发现 Mod 使用公开快照：

```csharp
using Cysharp.Threading.Tasks;

private async UniTask<ModInfo[]> GetLoadedModsAsync()
{
    if (!ModAPI.Initialized)
    {
        await ModAPI.Initialize();
    }

    return ModAPI.CreateInfoSnapshot();
}
```

修改已有 Mod 的持久化启停状态：

```csharp
private void SetEnabled(ModInfo modInfo, bool enabled)
{
    if (modInfo != null && ModAPI.Initialized)
    {
        ModAPI.SetModEnabled(modInfo, enabled);
    }
}
```

## 常见错误

- 在 `ModAPI.Initialize` 完成前调用快照、状态或删除入口。
- 认为目录存在 `.cfg` 就一定会加载；禁用、删除、API 校验失败或缺少 `packageName` 都会阻止包进入运行时。
- 把 `packageName`、目录名、文件路径或资源地址当成玩法内容唯一 ID。
- 把 `ModAPI.Shutdown` 当成资源包释放，或直接在 Mod 侧调用 `YooAssets.Destroy`。
- 把 `SetModEnabled(false)` 当成本次运行立即卸载包的热切换 API。
- 让 Mod loader 写角色、技能、标签或数据库的第二套运行时状态。

## 禁止做法

- 不在 Gameplay 侧新建另一套 Mod 扫描器、配置路径、启停状态或资源包注册表。
- 不在 Mod 侧动态合并 EX-GAS GameplayTag 表、Ability 表或生成代码。
- 不把 `ModInfo.metaData` 当成无需定义作者源、版本和兼容性的任意业务数据库。
- 不把独立资源包加载扩大描述成“Mod 可以覆盖所有运行时系统”。

## 当前真实缺口

- 当前只确认 Mod 能初始化独立 YooAsset 包，未确认能运行时合并 EX-GAS GameplayTag 作者表、生成标签码或更新 `XTag` 常量。
- `ModAPI.Initialize` 没有取消令牌和中途回滚协议。
- 启停状态变化与已加载包之间没有自动热切换链路。
- Mod 内容如何映射到 Gameplay 稳定 ID、关卡作者源、存档依赖和联机校验仍未形成完整契约。

## 源码证据

- API：[`ModAPI.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModAPI.cs) 的 `Initialize`、`CreateInfoSnapshot`、`SetModEnabled`、`DeleteMod`、`Shutdown`。
- 扫描：[`ModLoader.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModLoader.cs) 的 `LoadAllModsAsync`、`LoadModAsync`；[`ModInfo.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModInfo.cs) 的清单字段。
- 状态：[`ModConfig.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModConfig.cs) 的 `LoadOrCreate`、`Save`、`SetModEnabled`、`DeleteMod`。
- 包边界：[`ResourceSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystem.cs) 的 `LoadModPackageAsync`、`UnloadModPackageAsync`、`ResolvePackage`。
- 启停顺序：[`GameManager.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/GameManager.cs) 的 `Start` 和 `OnDestroy`。
