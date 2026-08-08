# FantasyWord 插件与框架迁移清单

来源工程：`C:\Gamedev\Unity\Project\FantasyWord`  
目标工程：`C:\Gamedev\Unity\Project\CardLoop`  
迁移日期：2026-07-31  
本轮口径修正：2026-08-01

源工程 Unity 版本：`6000.3.10f1`  
目标工程 Unity 版本：`6000.5.4f1`

## 本轮目标

把 FantasyWord 中可复用的插件、框架、本地 UPM 包和 AI docs/agents 搬到 CardLoop。

本轮不按路径机械判断“是不是插件”。插件和框架可能位于：

- `Assets/Plugins`：传统 Unity 插件、第三方插件和外部框架。
- `Packages`：本地 UPM 插件/框架包。
- 项目根目录：例如 `puerts-unity-mcp` 这类 MCP / Editor 自动化框架。
- `.spec`、`.agents`、`.codex`：可复用 AI docs、agents 和 skills。

只剔除 FantasyWord 项目专有内容，例如旧场景、旧美术、旧预制体、旧业务数据、历史任务证据、旧世界观/旧玩法强绑定内容，以及不能直接作为 CardLoop 通用插件或框架复用的部分。

## 当前裁决

### 明确保留：插件 / 框架 / AI 自动化

| 来源 | 目标 | 裁决 | 说明 |
|------|------|------|------|
| `Packages/com.aibridge.unity` | `Packages/com.aibridge.unity` | 保留 | Unity AI Bridge，AI 控制 Unity Editor 的本地 UPM 工具包。 |
| `Packages/com.besty.unity-skills` | `Packages/com.besty.unity-skills` | 保留 | UnitySkills，Unity 自动化 skill / MCP 工具包。 |
| `puerts-unity-mcp` | `puerts-unity-mcp` | 保留 | PuerTS Unity MCP，AI / MCP 反射式 Unity 控制端点。 |
| `puerts-unity-mcp-extension` | `puerts-unity-mcp-extension` | 保留 | MCP 扩展配置。 |
| `Packages/com.tencent.puerts.core` | manifest `file:../puerts-unity-mcp/third_party/puerts/unity/upms/core` | 保留 | `puerts-unity-mcp` 依赖。 |
| `Packages/com.tencent.puerts.v8` | manifest `file:../puerts-unity-mcp/third_party/puerts/unity/upms/v8` | 保留 | `puerts-unity-mcp` 依赖。 |
| `Packages/com.ami.broaudio` | `Packages/com.ami.broaudio` | 保留 | BroAudio，重要音频中间件插件；不能因位于 `Packages` 被误判为旧包。 |
| `Packages/com.cysharp.unitask` | `Packages/com.cysharp.unitask` | 保留 | UniTask，通用异步框架。 |
| `Packages/com.tuyoogame.yooasset` | `Packages/com.tuyoogame.yooasset` | 保留 | YooAsset，资源管理框架。 |
| `Packages/com.liyingsong.foldertag` | `Packages/com.liyingsong.foldertag` | 保留 | Folder Tag，编辑器辅助插件。 |
| `Assets/Plugins` | `Assets/Plugins` | 保留主体 | 第三方插件集合；包含插件自带 Demo/Examples，Demo 不是 FantasyWord 旧业务，但可能后续按体积或编译噪音裁剪。 |
| `Assets/ProjectPlugins/ContextSteering2D` | `Assets/ProjectPlugins/ContextSteering2D` | 保留 | 自有可复用 2D 转向插件。 |
| `Assets/Scripts/Gen` | `Assets/Scripts/Gen` | 保留 | EX-GAS / GameCore 生成运行时胶水层，包含 `FantasyWord.GAS.GeneratedRuntime.asmdef`、`XAbility.gen.cs`、`XLuban.gen.cs`、`XLauncher.gen.cs` 等；不是手写业务代码，缺失会导致 GameCore Editor smoke / EditMode 测试无法编译。 |
| `.spec/skills`、`.spec/agents`、`.agents/skills`、`.codex/skills` | 同名目录 | 保留可复用部分 | AI workflow、agents 和专项 skills；旧项目任务事实不接管。 |
| `.unity-env` | `.unity-env` | 保留候选 | Unity 环境脚本候选，需后续验证是否仍适配 CardLoop。 |

### 保留但待验证：插件集合中的 Demo / Examples

`Assets/Plugins` 下当前共 18 个一级插件目录、约 12003 个文件。其中 12001 个来自 FantasyWord，另外 2 个是 CardLoop 为 DOTween 模块化编译补入的 `DOTween.Modules.asmdef` 与 `.meta`。以下是插件自带 Demo / Example 目录，不等同于 FantasyWord 旧业务包：

| 插件目录 | 文件数 | Demo / Example 目录数 | 裁决 |
|----------|--------|------------------------|------|
| `AStar 2D Grid Pathfinding` | 89 | 3 | 保留插件主体；Demo 后续可按需要裁剪。 |
| `Backbone` | 20 | 1 | 保留插件主体；Sample Scene 后续可按需要裁剪。 |
| `Easy Transition` | 80 | 1 | 保留插件主体；Demo 后续可按需要裁剪。 |
| `FunkyCode` | 2132 | 17 | 保留 SmartLighting2D 主体；Demos 体积较大，后续可单独裁剪。 |
| `Sirenix` | 101 | 1 | 保留 Odin / Sirenix 插件主体；空 Demo 目录可后续清理。 |
| `TableForge` | 890 | 1 | 保留 TableForge 主体；Demo 后续可按需要裁剪。 |
| `TopDownEngine` | 4969 | 1 | 保留 TopDownEngine / MoreMountains 主体；Demos 后续可按需要裁剪。 |
| `YokiFrame` | 2280 | 2 | 保留 YokiFrame 主体；NodeKit/UIKit Examples 后续可按需要裁剪。 |

未经确认，不直接修改第三方插件源码、插件编辑器界面、插件示例文档或插件生成器本体。

## 待裁剪 / 待隔离：旧项目专有混入风险

这些内容有框架参考价值，但当前不够干净，不能直接称为“已作为 CardLoop 框架正式接入”。

| 来源 | 目标 | 当前状态 | 证据 | 裁决 |
|------|------|----------|------|------|
| `Assets/Scripts/GameCore` | `Assets/Scripts/GameCore` | 已进入活跃 `Assets`，但混有旧业务语义；旧本地 `Temporal*Effect` 第二套持续效果系统已按 EX-GAS 官方职责归属删除 | 仍命中 `FantasyWord` 命名空间、Equipment、WorldElement 等迁移语义 | 不删除整包；应继续按职责切片吸收或移到参考区，不能整包当成 CardLoop 正式框架。 |
| `Assets/Editor/GameCore` | `Assets/Editor/GameCore` | 已进入活跃 `Assets`，但包含旧验证器、旧测试和旧业务桥接 | 147 个文件；命中 `FantasyWord` 105 处、`Equipment` 139 处、`WorldElement` 71 处、`旧能力` 59 处 | 不删除；应先隔离旧工程验证器/测试，再抽取通用编辑器工具。 |
| `Assets/DataGenerated/Luban` | `Assets/DataGenerated/Luban` | 已进入活跃 `Assets`，仍包含 FantasyWord/GAS 生成配置和旧业务 Cue / 表数据风险；旧 `TaskApplyWorldElement` / `Flamethrower` 已按 GAS 表源删除并重新导表 | 生成配置仍需按 CardLoop 语义继续切片，不能直接成为 CardLoop 业务真相 | 不删除整套 GAS/Luban 链路；按官方文档和表源继续裁剪旧业务数据。 |
| `EX_GAS_Config` | `EX_GAS_Config` | 已复制配置源和 Luban 工具链 | 159 个文件；包含 EX-GAS 表、Luban 工具、生成脚本 | 保留为 GAS 配置源候选；其中具体表数据需确认是否可复用。 |
| `Assets/Settings/Renderer2D.asset` | `Assets/Settings/Renderer2D.asset` | 已被 FantasyWord 同名文件带入 | 与 FantasyWord 文件哈希一致；引用 `EquipmentSystem.HQ4xRendererFeature`、`EquipmentSystem.DepixelizeRendererFeature`、`EquipmentSystem.xBRZRendererFeature` | 旧项目设置风险；应恢复/重建为 CardLoop 自己的 Renderer2D 设置或先隔离。 |

## 已剔除或未接管：旧项目专有内容

| 来源 | 处理 | 当前证据 | 原因 |
|------|------|----------|------|
| `Assets/Art` | 未迁入 | 源 13639 个文件，目标不存在 | 旧美术资源，不属于插件/框架。 |
| `Assets/Prefabs` | 未迁入 | 源 117 个文件，目标不存在 | 旧预制体，不属于插件/框架。 |
| `Assets/Sprites` | 未迁入 | 源 28 个文件，目标不存在 | 旧业务 Sprite，不属于插件/框架。 |
| `Assets/GameData` | 未迁入 | 源 372 个文件，目标不存在 | 旧业务数据。 |
| `Assets/GameRes` | 未迁入 | 源 27 个文件，目标不存在 | 旧业务资源。 |
| `Assets/Scenes/ClickMoveTest.unity` | 未迁入 | 源存在，目标不存在 | FantasyWord 测试场景。 |
| `Assets/Scenes/EquipmentSystemDemo.unity` | 未迁入 | 源存在，目标不存在 | FantasyWord 装备系统 Demo 场景。 |
| `.codex/evidence` | 未迁入 | 源 72 个文件，目标不存在 | 旧截图、日志、测试结果，只证明 FantasyWord 历史任务。 |
| `.spec/tasks` | 未接管旧任务 | 目标仅保留占位 README | FantasyWord 的旧任务卡不是 CardLoop 当前任务事实。 |
| `.spec/decisions` | 未接管旧决策 | 目标仅保留占位 README | FantasyWord 历史架构决策不自动适用于 CardLoop。 |
| `.spec/knowledge/features/project` | 未接管旧业务知识库 | 目标仅保留占位 README | FantasyWord 业务知识库强项目绑定。 |
| `openspec/changes`、`openspec/specs` | 未迁入 | 源存在，目标不存在 | 旧项目提案和规格，不自动成为 CardLoop scope。 |
| `.codex/skills/safe-image-reading` | 未迁入 | 已排除 | 源项目中已暂停，且指向 FantasyWord 备份路径。 |
| `.spec/skills/equipment-system-workflow` | 未迁入 | 已排除 | 旧装备/坐骑专项流程，强项目绑定。 |

## Manifest 变更

- `Packages/manifest.json` 已补入本地 UPM 插件/框架包、puerts MCP、YooAsset、Addressables、2D Animation、2D Tilemap、Cinemachine、Entities、Editor Coroutines、Newtonsoft Json 等依赖。
- `Packages` 下的本地 UPM 包不应被视为“旧包整搬”；它们是插件/框架的一种 Unity 分发形式。
- CardLoop 原有 Unity 包版本未降级，例如 Input System、URP、Test Framework、Timeline、UGUI、Visual Scripting 保持目标项目原版本。
- `packages-lock.json` 已随 Unity Package Manager 解析更新；本地 UPM 包以 `file:` / embedded 形式锁定，避免打开工程时依赖远程网络。

## 2026-08-01 插件更新记录

用户要求“有更新的都直接更新”后，本轮按官方仓库、OpenUPM 或包自身可确认来源更新；无法确认更新源的包不伪造新版。

| 包 / 框架 | 当前版本 | 处理 | 证据 / 说明 |
|-----------|----------|------|-------------|
| `com.aibridge.unity` | `1.0.0` | 保持 | GitHub latest 为 `v1.0.0`，本地已是最新版。 |
| `com.besty.unity-skills` | `2.4.2` | 已更新 | OpenUPM 当前查询版本为 `2.4.2`。 |
| `puerts-unity-mcp` | `0.1.0` | 保持 | 本地框架包；其依赖 `com.tencent.puerts.core` / `v8` 为 `3.0.2`，GitHub latest 为 `Unity_v3.0.2`。 |
| `com.ami.broaudio` | `3.2.2` | 已更新 | GitHub latest 为 `3.2.2`；BroAudio 是重要音频中间件，按必需插件保留。 |
| `com.tuyoogame.yooasset` | `3.0.5` | 已更新 | OpenUPM 当前查询版本为 `3.0.5`。 |
| `com.cysharp.unitask` | `2.5.11` | 保持 | GitHub latest 为 `2.5.11`。 |
| `com.cysharp.zstring` | `2.6.0` | 新增 | 新版 YokiFrame 依赖 `Cysharp.Text.ZString`，补入本地 UPM 包；OpenUPM 包元数据声明了不存在的 `Samples~/RequiredManagedDLLs`，已删除该错误 sample 声明，避免 Package Manager UI 读缺失目录时报错。 |
| `Assets/Plugins/YokiFrame` | `1.8.5` | 已更新 | GitHub latest 为 `v1.8.5`。 |
| `com.liyingsong.foldertag` | `1.0.2` | 保持 | GitHub releases/latest 返回 404，未找到可确认新版源。 |
| DOTween | 现有本地版本 | 未自动更新 | 免费版主要来自官网 / Asset Store 分发，不是可直接按 GitHub release 拉取的源码包。 |
| NuGetForUnity | `4.5.0` | 新增 | 从 `project-revive` 复用包管理器入口，用于管理 Unity 内 NuGet DLL；不替代 UPM / OpenUPM / GitHub release / Asset Store。 |
| ZLinq | `1.5.6` | 新增 | 按 Cysharp 官方 Unity 接入方式：NuGet `ZLinq` DLL 加 `ZLinq.Unity` UPM 包；`ZLinq.Unity` 已嵌入 `Packages/com.cysharp.zlinq`，避免打开工程时依赖 GitHub 网络。 |
| System.Runtime.CompilerServices.Unsafe | `6.1.2` | 新增 | ZLinq 在 Unity / netstandard2.1 下需要的 NuGet 依赖；采用稳定版，不采用 preview 包。 |

`C:\Gamedev\Unity\Project\project-revive` 中使用的开源包管理器是 `NuGetForUnity`，路径为 `Assets/ThirdParty/nugetforunity`，本地版本 `4.5.0`。它用于在 Unity Editor 内管理 `.NET/NuGet` 包，不能替代 Unity Package Manager、OpenUPM、GitHub release 或 Asset Store 来统一管理 BroAudio、YooAsset、YokiFrame、UnitySkills 这类插件。

## 2026-08-01 ZLinq 接入与集合查询规范

- 已接入 NuGetForUnity `4.5.0`：`Assets/ThirdParty/nugetforunity`，NuGet 源配置在 `Assets/NuGet.config`，NuGet 包清单在 `Assets/packages.config`。
- 已接入 NuGet 包 `ZLinq` `1.5.6`：`Assets/Packages/ZLinq.1.5.6`；已接入依赖 `System.Runtime.CompilerServices.Unsafe` `6.1.2`：`Assets/Packages/System.Runtime.CompilerServices.Unsafe.6.1.2`。
- 已在 `Packages/manifest.json` 加入 `com.cysharp.zlinq`，指向本地嵌入包 `file:com.cysharp.zlinq`，用于 Unity GameObject / Transform / UI Toolkit 查询扩展；本地包来源为已解析成功的 `ZLinq.Unity` `1.5.6`。
- 集合查询的项目规范归属是 `.spec/knowledge/standards/code-style.md`：运行时代码新增集合查询必须使用 ZLinq；热路径必须使用 ZLinq 或手写无分配循环；不默认启用 DropInGenerator。
- 当前本地解析证据：`Packages/com.cysharp.zlinq/package.json` 标记 `version` 为 `1.5.6`；`Assets/Packages/ZLinq.1.5.6/ZLinq.nuspec` 标记 NuGet 版本为 `1.5.6`，并声明 `System.Runtime.CompilerServices.Unsafe` `6.1.2` 依赖。
- 当前 `Packages/packages-lock.json` 已写入 `com.cysharp.zlinq`，来源为本地 embedded 包；不再需要启动时访问 GitHub 拉取 `ZLinq.Unity`。

## Unity 6000.5 兼容修复记录

以下修复只服务于让迁入插件/框架在 CardLoop 的 Unity `6000.5.4f1` 下继续编译；不是删插件，也不是裁掉 `Packages`。

| 区域 | 修复 | 验证 |
|------|------|------|
| AIBridge | 增加并复用 `ObjectIdCompat`，避开 Unity 6000.5 中直接 `GetInstanceID()`、`InstanceIDToObject(int)` 和 `EntityId -> int` 转换导致的编译错误；`Selection` 工具对外仍返回原 `instanceID` 数字。 | `AiBridge.Unity.Runtime.rsp`、`AiBridge.Unity.Editor.rsp` 静态编译通过。 |
| puerts-unity-mcp | 增加并复用 `UnityObjectIdCompat`，让运行时 / Editor 反射工具继续输出对象 `instanceId`。 | `PuertsUnityMcp.Runtime.rsp`、`PuertsUnityMcp.Editor.rsp` 静态编译通过。 |
| Luban 生成代码 | 将生成物和 `cs-simple-json` 模板里的 `using SimpleJSON;` 改成 `JSONNode` / `JSONNodeType` 别名，避免与其它 `SimpleJSON` 类型名冲突。 | `FantasyWord.GAS.GeneratedConfig.rsp` 静态编译通过；模板已同步，避免重新生成后复发。 |
| EX-GAS / GameCore 生成运行时 | 从源工程补入 `Assets/Scripts/Gen`，保留 20 个文件 / `.meta`，补齐 `XAbility.gen.cs`、`XLuban.gen.cs`、`XLauncher.gen.cs` 等生成入口。 | 手动构造 `FantasyWord.GAS.GeneratedRuntime` 等价响应文件后静态编译通过。 |
| YokiFrame / GAS Editor | 修正 Unity 6000.5 UIElements / NodeKit / IMGUI TreeView API 兼容点。 | `YokiFrame.UIKit.Editor.rsp`、`YokiFrame.NodeKit.Editor.rsp`、`com.exhard.exgas.editor.rsp` 静态编译通过。 |
| TableForge / YooAsset / BroAudio / UnitySkills | 未裁剪，按迁入插件保留并验证编译。 | 对应 Editor / Runtime 响应文件静态编译通过。 |
| Manifest / 本地 UPM | 删除 Unity `6000.5.4f1` 解析不到的旧模块依赖 `com.unity.modules.vr`；保留 AIBridge、UnitySkills、puerts、BroAudio、UniTask、YooAsset、FolderTag 等本地包。 | 新鲜 batchmode Package Resolve 通过；`packages-lock.json` 中必需插件均存在。 |
| EX-GAS / GameCore 生成运行时 asmdef | 将 `Assets/Scripts/Gen/FantasyWord.GAS.GeneratedRuntime.asmdef` 改为 `overrideReferences: true`，避免额外吃进 Plastic SCM / TextMateSharp 的 `JSONNode` 类型冲突。 | 新鲜 batchmode 中 `FantasyWord.GAS.GeneratedRuntime` 不再出现 JSONNode 编译错误。 |
| Sirenix / Odin Editor 初始化钩子 | 对 Sirenix 两个 Unity 旧 UIElements 初始化入口做最小 DLL 兼容补丁，只禁用已失效的初始化钩子，保留 Odin 主体。 | `Temp/UnityBridge/backups/sirenix-unity6000-hook-patch-20260801-013438` 有补丁前备份；新鲜 batchmode 未再出现对应 `MissingFieldException`。 |
| GameCore Editor 数据缓存 | `FormalDataAssetCache.RebuildCache()` 只扫描当前项目真实存在的目录；`Assets/GameData` 未迁入时清空缓存并返回。 | 新鲜 batchmode 未再出现 `AssetDatabase.FindAssets: Folder not found: 'Assets/GameData'`。 |
| DOTween / Animation Sequencer | 补入 `DOTween.Modules.asmdef` 这类 DOTween setup 产物，让 Animation Sequencer 能识别 DOTween；Unity 为 Standalone 写入 `DOTWEEN_ENABLED`；同时将其 Unity 6000.5 已过时的 `AdvancedDropdownItem.children` 改为 `childList`。 | 新鲜 batchmode 未再出现 `No DOTween found` warning，也未出现 Animation Sequencer 编译错误。 |
| YokiFrame UIKit / DOTween UI 扩展 | `YokiFrame.UIKit.asmdef` 增加 `DOTween.Modules` 引用，让 `CanvasGroup.DOFade`、`RectTransform.DOAnchorPos`、`RectTransform.DOSizeDelta` 等 DOTween UI 扩展进入 UIKit 编译输入。 | 当前打开的 Unity Editor 通过 AI Bridge `assets-refresh` 重新编译；`YokiFrame.UIKit.rsp` 已包含 `DOTween.Modules.ref.dll`，刷新后关键错误筛选为空。 |
| YokiFrame 1.8.5 / GameCore 旧 API 兼容 | 补回 `GameObjectPoolService` / `PooledGameObject` 的 GameObject 预制体池入口、`CoroutineKit` 的等待入口，并给新版 `InputKit` 补回 `SetActionAsset(InputActionAsset)`；这些入口是 GameCore 迁入候选仍在调用的通用框架能力。 | 2026-08-01 09:36 临时新鲜工程 batchmode 编译通过；更新后未再出现 `GameObjectPoolService`、`PooledGameObject`、`CoroutineKit` 或 `SetActionAsset` 缺失错误。 |
| YokiFrame / YooAsset 软依赖宏 | 给当前 Standalone 平台写入 `YOKIFRAME_YOOASSET_SUPPORT`，让 YokiFrame 的 YooAsset 接口文件和 V3 Provider 实现同时进入编译；`IYooAssetRawFileProvider`、`IYooAssetResProvider`、`IYooAssetSceneProvider` 文件本身存在，不是插件缺文件。 | `ProjectSettings/ProjectSettings.asset` 与 `Library/EditorOnlyScriptingSettings.json` 已包含该宏；`Library/Bee/1900b0aEDbg-inputdata.json` 同时命中 `YOKIFRAME_YOOASSET_SUPPORT` 与 `YOOASSET_3_0_OR_NEWER`；最新成功编译后未再出现这三个接口缺失错误。 |
| StackCraft URP 后处理 | 将项目侧 `CustomPostProcessFeature` 从旧 `ScriptableRenderPass.Execute(...)` 改为 Unity 6 URP RenderGraph 的 `RecordRenderGraph(...)`；这是项目模板脚本兼容问题，不是 YokiFrame / YooAsset 插件缺失。 | `Assets/StackCraft/Scripts/PostProcess/CustomPostProcessFeature.cs` 已使用 `RecordRenderGraph` / `RenderGraphUtils`；最新成功编译后未再出现 `CS0115`。 |

## Unity 新鲜验证

2026-08-01 02:08 已使用 CardLoop 的 Unity `6000.5.4f1` 跑新鲜 batchmode：

- 日志：`Temp/UnityBridge/results/unity-batchmode-framework-migration-20260801-020815.log`
- 退出码：`0`
- 项目关键错误筛选：`error CS`、`Script Compilation Error`、`Scripts have compiler errors`、`Exception while executing InitializeOnLoad`、`MissingFieldException`、`AssetDatabase.FindAssets: Folder not found`、`Package [`、`No DOTween found` 均未命中。
- 宽泛日志筛选只剩 Unity 授权客户端握手重连和 batchmode 退出后的 `Curl error 42: Callback aborted`；授权随后解析为 Unity Personal，退出码仍为 `0`，不属于迁入插件/框架脚本错误。
- `Packages/packages-lock.json` 保留必需插件 / 框架：`com.aibridge.unity`、`com.ami.broaudio`、`com.besty.unity-skills`、`puerts-unity-mcp`、`com.tencent.puerts.core`、`com.tencent.puerts.v8`、`com.cysharp.unitask`、`com.tuyoogame.yooasset`、`com.liyingsong.foldertag`。
- `Packages/packages-lock.json` 未解析旧 2D 辅助包：`com.unity.2d.pixel-perfect`、`com.unity.2d.tilemap.extras`、`com.unity.2d.tooling`。
- `.spec` 链接和 frontmatter 验证通过：`node .spec/tools/spec-lint.mjs` 输出 `spec-lint passed`。

2026-08-01 08:13 针对用户当前打开的 Unity Editor 又出现的 UIKit / DOTween 编译错误做新鲜验证：

- 当前 Editor：`C:\Gamedev\Unity\Editor\6000.5.4f1\Editor\Unity.exe`，工程窗口为 CardLoop / SampleScene。
- 现实症状：`YokiFrame.UIKit.dll` 编译时报 `CanvasGroup` / `RectTransform` 找不到 DOTween UI 扩展方法；这些扩展来自 `DOTween.Modules`，不是单独的 `DOTween.dll`。
- 修复点：`Assets/Plugins/YokiFrame/Tools/UIKit/Runtime/YokiFrame.UIKit.asmdef` 已引用 `DOTween.Modules`。
- 刷新方式：通过 AI Bridge 文件桥执行 `assets-refresh`，命令 `refresh-20260801-081311-733` 返回 `success`。
- 编译输入证据：`Library/Bee/artifacts/1900b0aEDbg.dag/YokiFrame.UIKit.rsp` 于 08:13:21 重新生成，并包含 `DOTween.Modules.ref.dll`、`DOTween.dll`、`UnityEngine.UI.ref.dll`。
- 当前 Editor 状态：`editor-application-get-state` 返回 `isCompiling=false`、`isUpdating=false`。
- 刷新后日志筛选：从 `assets-refresh` 之后到 08:13:58，`error CS`、`Script Compilation Error`、`Scripts have compiler errors`、`Exception while executing InitializeOnLoad`、`MissingFieldException`、`No DOTween found`、`Tundra build failed`、`Compilation failed` 均未命中。

2026-08-01 09:36 针对“有更新的都直接更新”后的包体状态做新鲜临时工程验证：

- 临时工程：`C:\Gamedev\Unity\Project\CardLoop_UpdateVerify_20260801_0916`。
- 日志：`C:\Gamedev\Unity\Project\CardLoop_UpdateVerify_20260801_0930.log`。
- 退出码：`0`，日志末尾为 `Exiting batchmode successfully now!` / `Application will terminate with return code 0`。
- 编译证据：`Tundra build success (7.63 seconds)`，`AssetDatabase: script compilation time: 18.508882s`。
- 关键错误筛选：`error CS`、`Script Compilation Error`、`Scripts have compiler errors`、`Compilation failed`、`Tundra build failed`、`Exception while executing InitializeOnLoad`、`MissingFieldException`、`No DOTween found`、`Package [` 均未命中。
- 当前已打开的主 Unity Editor 曾出现无响应；本条验证刻意使用临时新鲜工程，不把卡住的 Editor 作为完成证据。

2026-08-01 13:00 针对 ZLinq 接入和集合查询规范做当前打开 Editor 验证：

- 当前打开的 Unity Editor 通过 AI Bridge 触发 `AssetDatabase.Refresh(ForceUpdate | ForceSynchronousImport)` 与 `CompilationPipeline.RequestScriptCompilation()`。
- 编译证据：`Logs/Editor.log` 最新脚本编译结果出现 `Tundra build success (4.90 seconds), 34 items updated, 2343 evaluated`。
- 关键错误筛选：从最后一次 `Tundra build success` 之后继续筛 `error CS`、`Script Compilation Error`、`Compilation failed`、`Tundra build failed`、`DirectoryNotFoundException`、`MissingFieldException`、`Exception while executing InitializeOnLoad`、`Package [`，均未命中。
- 当前 Editor 后续仍可能处于资源导入 / Domain Reload busy 状态；本条只证明脚本编译和 ZLinq 接入没有继续产生关键编译错误，不等于 GameCore/GAS 业务正式启用。

2026-08-01 13:40 针对“打开就闪退”做 ZLinq 包来源修复和新鲜验证：

- 现实症状证据：`Logs/Editor-prev.log` 显示 Unity 在 Package Resolve 阶段因 `com.cysharp.zlinq` GitHub 443 连接失败退出，返回码 1；这是启动阶段依赖远程 Git 包失败，不是 Unity 原生崩溃栈。
- 修复点：将 `com.cysharp.zlinq` 从远程 Git URL 改为本地嵌入包 `Packages/com.cysharp.zlinq`，`Packages/manifest.json` 和 `Packages/packages-lock.json` 均指向 `file:com.cysharp.zlinq`。
- 包解析证据：`Library/PackageManager/projectResolution.json` 中 `com.cysharp.zlinq` 的 `resolvedPath` 为 `C:\Gamedev\Unity\Project\CardLoop\Packages\com.cysharp.zlinq`，`source` 为 `embedded`。
- 验证命令：Unity `6000.5.4f1` batchmode 打开 CardLoop，日志写入 `Logs/OpenCheck-ZLinqLocal-20260801-2.log`。
- 验证结果：进程退出码 `0`，日志中 `com.cysharp.zlinq@file:C:\Gamedev\Unity\Project\CardLoop\Packages\com.cysharp.zlinq`、`Tundra build success (21.35 seconds)`、`Application will terminate with return code 0` 均命中；关键错误筛选未命中包解析失败、`DirectoryNotFoundException`、脚本编译失败或 `return code 1`。

2026-08-01 针对 YokiFrame / YooAsset Provider 接口缺失报错做当前打开 Editor 验证：

- 现实症状证据：`Logs/Editor.log` 旧编译记录中出现 `IYooAssetRawFileProvider`、`IYooAssetResProvider`、`IYooAssetSceneProvider` 缺失的 `CS0246`。
- 根因证据：接口文件实际位于 `Assets/Plugins/YokiFrame/Core/Runtime/ResKit/Loader/YooAsset/Internal/`，但接口受 `YOKIFRAME_YOOASSET_SUPPORT` 条件编译控制；V3 Provider 受 `YOOASSET_3_0_OR_NEWER` 控制。当前平台只启用后者时，就会出现“V3 实现参与编译、接口被裁掉”的错配。
- 修复点：当前 Standalone 平台宏已包含 `YOKIFRAME_YOOASSET_SUPPORT`；这是启用 YokiFrame 对 YooAsset 的软依赖，不是删除或裁剪插件。
- 追加暴露并修复的项目侧问题：宏修正后继续编译暴露 `Assets/StackCraft/Scripts/PostProcess/CustomPostProcessFeature.cs` 的旧 URP `Execute(...)` 覆写错误，已改为 Unity 6 URP RenderGraph 入口。
- 验证结果：`Logs/Editor.log` 连续出现 `Tundra build success (4.65 seconds), 9 items updated, 2363 evaluated`、`Tundra build success (4.66 seconds), 5 items updated, 2363 evaluated` 和后续 `Tundra build success (0.94 seconds), 1 items updated, 2363 evaluated`；从最新成功记录之后继续筛 `error CS`、`Script Compilation Error`、`Tundra build failed`、`CS0246`、`CS0115` 和三个 `IYooAsset*Provider` 名称均未命中。

## 后续业务启用边界

以下事项不阻塞本轮“插件 / 框架 / AI docs/agents 已迁入并可编译”的验收，但会影响后续把 GameCore/GAS 当作 CardLoop 正式业务职责入口使用：

1. 后续正式启用前，需要收口旧项目混入风险：重点处理 `Assets/Scripts/GameCore`、`Assets/Editor/GameCore`、`Assets/DataGenerated/Luban`、`EX_GAS_Config` 和 `Assets/Settings/Renderer2D.asset`。
2. 若 GameCore/GAS 需要正式启用，必须先完成 CardLoop 语义切片和旧业务剥离，再处理命名空间、输入资产、菜单路径、资源地址和业务配置。
3. 当前 `Assets/InputSystem_Actions.inputactions` 没有被 FantasyWord 版本覆盖；如果要启用 GameCore 输入链，需要单独确认是否替换或合并输入动作。

## 2026-08-01 GameCore / EX-GAS 官方职责清理

- 真相源：`Assets/Plugins/GAS/package.json` 指向的官方仓库、`Assets/Plugins/GAS/SKILL.md`、`Assets/Plugins/GAS/Wiki/GameplayEffect.md`、`GameplayCue.md` 和 `Ability.md`。
- 已删除旧 `Flamethrower / 持续喷火` 表源与桥接：`TaskApplyWorldElement`、`XParamApplyWorldElement`、`FlamethrowerCueVisual` 及其迁移测试。
- 已删除 GameCore 本地 `Temporal*Effect` 状态系统：持续伤害、持续治疗、回蓝、属性修正、移速修正、控制、能力授予/压制/替换、`ITemporalEffect`、`ATemporalEffect`、角色本地 Temporal 注册表、Temporal 存档字段、净化道具、效果栏 UI 和 Temporal 浮字事件。
- 裁决依据：EX-GAS 官方 GameplayEffect 已负责持续时间、周期执行、授予能力、属性修改、标签条件、移除匹配标签效果和 Cue；项目侧本地 Temporal 另做这些职责属于重复职责入口，不是通用框架阶段应保留的薄适配。
- 保留边界：地图 / TerrainNavigation / 地表元素反应没有按“未使用”删除；这些需要按地图、地表语义和导航职责单独审查。
- 静态验证：`FantasyWord.GameCore.rsp`、`FantasyWord.GameCore.Editor.rsp`、`FantasyWord.GameCore.EditModeTests.rsp` 经 Unity `6000.5.4f1` Bee 响应文件过滤已删除源后编译通过，退出码 0、错误数 0；`node .spec/tools/spec-lint.mjs` 通过。

## 本轮静态验证

- 文件数对账通过但存在已解释差异：`Packages/com.aibridge.unity` 比来源多 2 个 Unity 6000.5 对象 ID 兼容文件，`puerts-unity-mcp` 比来源多 2 个 Unity 对象 ID 兼容文件，`Assets/Plugins` 比来源多 2 个 DOTween 模块 asmdef setup 文件；`.spec/skills` 少 1 个旧装备 workflow，`.codex/skills` 少 1 个已暂停的 `safe-image-reading`，均符合本清单裁决。
- 其余关键目录与来源文件数一致：`Packages/com.besty.unity-skills`、`Packages/com.ami.broaudio`、`Packages/com.cysharp.unitask`、`Packages/com.tuyoogame.yooasset`、`Packages/com.liyingsong.foldertag`、`puerts-unity-mcp-extension`、`Assets/ProjectPlugins/ContextSteering2D`、`Assets/Scripts/Gen`、`.spec/agents`、`.agents/skills`、`.unity-env`。
- `Assets/Scripts/Gen` 已补入并对账通过：源工程 20 个文件 / `.meta`，目标工程 20 个文件 / `.meta`。
- `Packages/manifest.json` JSON 解析通过。
- manifest 中所有本地 `file:` 包路径均存在。
- `Packages/packages-lock.json` 中确认保留本地插件 / 框架：`com.aibridge.unity`、`com.ami.broaudio`、`com.besty.unity-skills`、`puerts-unity-mcp`、`com.tencent.puerts.core`、`com.tencent.puerts.v8`、`com.cysharp.unitask`、`com.tuyoogame.yooasset`、`com.liyingsong.foldertag`。
- `Packages/packages-lock.json` 中确认旧 2D 辅助包不再解析：`com.unity.2d.pixel-perfect`、`com.unity.2d.tilemap.extras`、`com.unity.2d.tooling` 均未命中。
- 旧业务大资源目录未迁入：`Assets/Art`、`Assets/Prefabs`、`Assets/Sprites`、`Assets/GameData`、`Assets/GameRes` 在目标工程不存在。
- 使用 Unity `6000.5.4f1` 自带 Roslyn / Bee 响应文件完成迁入插件/框架静态编译扫尾：85 个非 Unity 官方程序集响应文件通过，失败数 0。
- 已单独验证官方 2D 相关包：`Unity.2D.Animation.Runtime`、`Unity.2D.Animation.Editor`、`Unity.2D.SpriteShape.Runtime`、`Unity.2D.SpriteShape.Editor`、`Unity.2D.Psdimporter.Editor` 静态编译通过。
- 2026-08-01 01:03 复查关键程序集：`YokiFrame.UIKit.Editor.rsp`、`AiBridge.Unity.Editor.rsp`、`UnitySkills.Editor.rsp`、`PuertsUnityMcp.Editor.rsp`、`BroAudioEditor.rsp`、`FantasyWord.GAS.GeneratedRuntime.static.rsp`、`FantasyWord.GameCore.Editor.static.rsp`、`FantasyWord.GameCore.EditModeTests.static.rsp` 均静态编译通过。
- 2026-08-01 02:08 已完成新鲜 Unity batchmode Package Resolve 与脚本编译验证；静态编译结果已被正式 Unity 编译验证覆盖，但不等于 GameCore/GAS 业务正式启用。

## 2026-08-04 Gameplay 地基测试场景与启动验收

- 新增统一测试场景 `Assets/Scenes/FoundationTest.unity` 和测试配置 `Assets/Scenes/FoundationTestConfig.asset`，并加入 `ProjectSettings/EditorBuildSettings.asset`。场景只验证 GameManager、资源包、ModAPI 和 EX-GAS 启动，不承载原创玩法。
- 新增编辑器生成入口 `Gameplay/地基/重建测试场景`。生成器保存场景后重新打开并核对 `GameConfig` 引用，避免出现场景文件存在但入口配置为空的假成功。
- 删除 StackCraft 的 `PlayModeStartScene` 编辑器脚本：该脚本会全局把 Play Mode 场景切到 `Assets/StackCraft/Scenes/Title.unity`，不适合作为 CardLoop 的正式测试入口。
- 清理 `Assets/BundleCollectorSetting.asset` 中从 FantasyWord 迁入但目标不存在的 `Assets/GameRes/UI/Panels`、`Assets/GameRes/Localization` 收集规则；保留 `DefaultPackage`，当前只收集测试场景以满足 YooAsset `EditorSimulateMode` 的非空资源包要求。
- `GameCore.ResourceSystem` 不再在资源包初始化阶段硬编码加载 FantasyWord 的 `localization` 地址；本地化业务资源未迁入，当前不以空资源伪造该依赖，后续由正式本地化职责接入。
- 新鲜编译证据：`Temp/codex-gameplay-foundation-fresh-compile-20260804.log`，Unity `6000.5.4f1` 退出码 `0`。
- 新鲜运行证据：`Temp/codex-gameplay-foundation-final-clean-playmode-20260804.log`。Play Mode 中活动场景为测试场景，启动状态为 `Ready`，异常为空，`GameManager` 数量为 `1`；资源系统、YooAsset、ModAPI 和 GAS 均完成初始化，退出后资源系统、YooAsset、ModAPI 释放，GAS 停止运行。该日志未命中启动失败、YooAsset 构建失败、资源地址异常或脚本编译错误。
- 这只是 2.1 基础设施的运行验收；StackCraft `GameDirector` 的新局、读档、存档、场景旅行和单局状态职责仍待后续小模块裁决。
