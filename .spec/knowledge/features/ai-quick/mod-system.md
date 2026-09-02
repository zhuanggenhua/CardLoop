---
name: mod-system
description: CardLoop 自有 Mod 入口速查：ModAPI、ModLoader、ModInfo、启停状态、独立 YooAsset 包和当前能力缺口。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: project-source + yooasset-official-entry
  status: 已交付
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

`ModInfo.modId` 是 Mod 的唯一稳定身份，版本升级不得改变它；`ModInfo.packageName` 只是 YooAsset 包定位，不能代替 Mod 身份或 Gameplay 内容 ID。`ModInfo.dependencies` 声明依赖 Mod ID 和可选的包含式最低 / 最高版本；依赖解析后按拓扑顺序加载，同层按 `modId` 排序。`ModAPI.DefaultAPIVersion` 当前为 `0.1.0`。Mod 清单不手填包哈希，加载器读取 YooAsset 构建产物中的官方 `.hash` 文件。每个 Mod 目录必须只有一个 `*.cfg` 清单；多个清单直接报错，不按文件系统顺序猜作者源。

## 生命周期

`GameManager.Start` 调用 `await ModAPI.Initialize(cancellationToken: destroyCancellationToken)`。随后 Mod 系统创建或读取 `ModConfig`，按规范化路径顺序扫描 LoadingPath，将每个 zip 解压到同名独立目录，再读取目录内唯一的 `*.cfg`，检查状态和 API 兼容性，最后让 `ResourceSystem.LoadModPackageAsync` 初始化启用 Mod 的独立包。取消令牌沿 `ModAPI -> IModLoader -> ResourceSystem` 传递；扫描、逐包加载和包初始化各阶段都会检查取消。

初始化完成后通过 `ModAPI.CreateInfoSnapshot()` 读取本次启动发现的清单，通过 `GetModState`、`SetModEnabled` 和 `DeleteMod` 修改下次启动意图。启停状态只按稳定 `modId` 保存，因此 Mod 升级不会重置用户选择。运行中禁用或标记删除不会卸载仍在使用的 YooAsset 包，也不会把清单从本次运行快照中伪装移除；当前实际加载集合由 `ResourceSystem` 唯一持有，新的单局包快照以该事实为准。

已有 Mod 配置文件属于玩家数据。文件不存在时才创建默认配置；文件存在但 JSON 损坏、内容为空、API 版本无效、状态为空、状态 ID 重复或状态枚举非法时直接拒绝初始化并保留原文件，不自动重建覆盖。保存使用同目录临时文件原子替换，不直接截断正式文件。暂时缺失的启用或禁用 Mod 状态继续保留；待删除 Mod 只有在启动扫描收齐全部清单、依赖闭包有效且全部删除路径都位于 Mod 根目录内后才会落盘删除，对应状态只在目录删除成功后消费。如果上一次已删除目录但在保存配置前中断，下次启动会确认该稳定 Mod ID 已不在安装目录后消费残留删除状态，不影响其它缺失 Mod 状态。启用 Mod 仍依赖目标时，删除请求会在改变配置前被拒绝。单个 Mod 清单使用同步结构化读取；空清单、无效 JSON 或反序列化为 `null` 会明确报出文件路径。

Mod 压缩包按路径确定顺序逐个处理，不并发写目录。每个 `example.zip` 只能解到同级 `example/`；目标目录已存在会拒绝覆盖。路径穿越、重复目标路径或解压失败会保留原 zip 并清理本轮残缺目录，不能让部分文件进入后续扫描。

启用 Mod 的实际包集合可由 `ModAPI.CreateActivePackageSetSnapshot()` 生成。它记录稳定 Mod ID、Mod 点分版本、YooAsset 官方包哈希和生效清单版本；`ScenarioRunSnapshot` 会冻结这份集合，读档要求当前集合严格一致。当前版本和依赖范围使用 .NET `System.Version`，支持 `1.2.3` 一类点分版本，不支持 SemVer 预发布后缀。缺失依赖、禁用依赖、版本不兼容、循环依赖和重复身份都会直接失败；缺少 YooAsset 官方 `.hash` 构建产物的启用包也会失败。

如果 Mod 启动已成功，`GameManager.OnDestroy` 调用 `ModAPI.Shutdown()` 清理 Mod 状态，再由 `ResourceSystem.Shutdown()` 释放资源包；启动尚未成功时，销毁取消令牌只阻止 ModAPI 提交迟到结果。`ModAPI.Shutdown` 自身不销毁 YooAsset 包。

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
- 认为目录存在 `.cfg` 就一定会加载；禁用、删除、API 校验失败、依赖不满足、缺少 `packageName` 或缺少 YooAsset 官方 `.hash` 构建产物都会阻止包进入运行时。
- 手工损坏 Mod 配置后期待系统自动恢复；系统会保留原文件并拒绝启动，避免覆盖玩家启停状态。
- 把多个 Mod 压缩包直接解到同一个目录，或用新压缩包覆盖已有同名 Mod 目录；每个压缩包必须拥有独立目录，升级流程尚未开放。
- 把 `packageName`、目录名、文件路径或资源地址当成玩法内容唯一 ID。
- 把 `ModAPI.Shutdown` 当成资源包释放，或直接在 Mod 侧调用 `YooAssets.Destroy`。
- 把 `SetModEnabled(false)` 或 `DeleteMod` 当成本次运行立即卸载包的热切换 API；它们只记录下次启动意图。
- 绕过 `ModAPI.DeleteMod` 直接删除安装目录；磁盘删除只属于启动加载事务。
- 让 Mod loader 写角色、技能、标签或数据库的第二套运行时状态。

## 禁止做法

- 不在 Gameplay 侧新建另一套 Mod 扫描器、配置路径、启停状态或资源包注册表。
- 不在 Mod 侧动态合并 EX-GAS GameplayTag 表、Ability 表或生成代码。
- 不在 Mod 清单中增加任意业务字典；玩法扩展必须进入统一 `ContentAsset` 作者源、内容校验和当前单局 `ContentIndex`。
- 不把独立资源包加载扩大描述成“Mod 可以覆盖所有运行时系统”。

## 当前真实缺口

- 当前只确认 Mod 能初始化独立 YooAsset 包，未确认能运行时合并 EX-GAS GameplayTag 作者表、生成标签码或更新 `XTag` 常量。
- YooAsset 已经开始的单次包操作没有项目可用的立即中断入口；取消后会等待当前操作自然结束、销毁未发布包并停止加载后续包，不能承诺立即停止底层文件 I/O。
- 启停状态变化与已加载包之间没有自动热切换链路。
- 数据型 Mod 内容已经通过独立 YooAsset 包中的 `ContentAsset` 进入统一稳定内容 ID、内容校验和当前单局 `ContentIndex`；不存在第二套 Mod 内容注册表。代码型扩展、EX-GAS 动态作者表、游戏内关卡编辑和联机可见性 / 权限校验仍未形成正式契约；单局存档所需的 Mod 包版本事实已接入。
- 当前依赖版本只支持 `System.Version` 点分格式；SemVer 预发布标识和更丰富的版本范围语法尚未接入。项目运行时没有可直接复用的正式 SemVer 库，不能引用 Editor-only 的 NuGetForUnity 或 Unity Visual Scripting 内部实现，也不手写半套解析器。
- `GameCore.GameCommandContext.RemotePlayer` 不是 Mod 或牌桌联机入口；它属于旧 `CharacterBase` 实体命令链，仅记录来源枚举和字符串标识，不能用于验证远端玩家权限、同步或可见性。

## 源码证据

- API：[`ModAPI.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModAPI.cs) 的 `Initialize`、`CreateInfoSnapshot`、`SetModEnabled`、`DeleteMod`、`Shutdown`。
- 扫描：[`ModLoader.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModLoader.cs) 的 `LoadAllModsAsync`；依赖：[`ModDependencyResolver.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModDependencyResolver.cs)；清单：[`ModInfo.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModInfo.cs)。
- 状态：[`ModConfig.cs`](../../../../Assets/Scripts/GameCore/Runtime/Mods/ModConfig.cs) 的 `LoadOrCreate`、`Save`、`SetModEnabled`、`DeleteMod`。
- 包边界：[`ResourceSystem.cs`](../../../../Assets/Scripts/GameCore/Runtime/Resources/ResourceSystem.cs) 的 `LoadModPackageAsync`、`UnloadModPackageAsync`、`ResolvePackage`。
- 启停顺序：[`GameManager.cs`](../../../../Assets/Scripts/GameCore/Runtime/Game/GameManager.cs) 的 `Start` 和 `OnDestroy`。
