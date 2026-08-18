---
name: plugin-docs
description: 第三方插件、本地 UPM 包和外部框架的官方文档入口索引；项目侧只记录入口、项目特殊约束和偏离，不替代官方建议。
metadata:
  type: doc
  status: 已交付
---

# 插件官方文档入口

本文件是 CardLoop 的插件文档索引，不是插件使用教程。接入、审查或清理插件相关代码时，默认先读插件自带 README / Wiki / SKILL / package.json / 官方仓库，再判断项目侧是否只需引用，或确实存在项目特殊约束和偏离需要登记。

## 使用规则

- 插件官方文档和包内 README / Wiki 是插件用法真相源；项目 `.spec` 只记录当前项目入口、项目特殊约束和偏离原因。
- `Assets/Plugins` 和 `Packages` 下的第三方源码、生成器、示例和文档未经当轮明确授权不直接修改。
- 如果插件已经提供官方扩展点，项目侧只实现扩展点；不得把迁移中转层、测试护栏或旧业务样例包装成新的项目框架职责。
- 如果插件文档缺失、版本不明或官方仓库不可达，只能登记缺口和最小补证据动作，不用手写替代实现冒充官方建议。

项目已经确认的运行时入口、项目特殊约束和公共工具速查见 [`ai-quick/README.md`](ai-quick/README.md)。本文件仍只负责官方文档入口，不复制这些项目卡的 API 正文。

## 目录边界

- `Assets/Plugins` 默认只放第三方插件、外部框架、第三方依赖和它们的项目级配置资源。
- 自有可复用插件或库应放在 `Assets/ProjectPlugins` 或本地 UPM 包；不得长期伪装成第三方插件。
- 第三方 Demo、Example、Editor UI、内置样式和生成器本体只能作为插件随包内容或参考素材处理；要接管为项目正式能力时，必须先登记项目入口、职责边界和偏离原因。

## 当前入口

| 插件 / 包 | 本地官方文档入口 | 官方仓库 / 文档入口 |
|-----------|------------------|----------------------|
| EX-GAS | `Assets/Plugins/GAS/SKILL.md`、`Assets/Plugins/GAS/Wiki/*.md`、`Assets/Plugins/GAS/package.json` | 官方 `EX-GAS-2.0` 分支 README：`https://github.com/No78Vino/gameplay-ability-system-for-unity/blob/EX-GAS-2.0/README.md`；本地 2.0.4 的项目校准见 [`ai-quick/ex-gas-runtime.md`](ai-quick/ex-gas-runtime.md)。默认分支 1.x README 不适用于当前版本。 |
| YokiFrame | `Assets/Plugins/YokiFrame/README.md`、`Assets/Plugins/YokiFrame/Core/Editor/AI_NAVIGATION.md`、`Assets/Plugins/YokiFrame/Core/Editor/Skills/*/SKILL.md` | `Assets/Plugins/YokiFrame/package.json` 的 `repository.url`。 |
| YooAsset | `Packages/com.tuyoogame.yooasset/README.md`、`Packages/com.tuyoogame.yooasset/CHANGELOG.md`、`Packages/com.besty.unity-skills/unity-skills~/skills/yooasset-design/*.md` | `Packages/com.tuyoogame.yooasset/package.json` 的 `repository.url`。 |
| UniTask | `Packages/com.cysharp.unitask/package.json`、`Packages/com.besty.unity-skills/unity-skills~/skills/unitask-design/*.md` | 官方仓库为 `https://github.com/Cysharp/UniTask`；当前项目包版本见本地 `package.json`。 |
| BroAudio | `Packages/com.ami.broaudio/package.json` | `documentationUrl` 指向 BroAudio 官方 GitBook，`repository.url` 指向 GitHub。 |
| SerializableDictionary | `Assets/Plugins/azixMcAze.SerializableDictionary/package.json` | 当前是本地插件包；项目使用边界见 `ai-quick/data-serialization.md`。 |
| Unity Input System | `Packages/manifest.json` 中的 `com.unity.inputsystem`；项目入口见 `ai-quick/README.md` | Unity 官方包文档：`https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/index.html`。 |
| Unity Cinemachine | `Packages/manifest.json` 中的 `com.unity.cinemachine` | Unity 官方包文档：`https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html`；当前项目专项卡待补。 |
| Unity Timeline | `Packages/manifest.json` 中的 `com.unity.timeline`、EX-GAS Timeline 专项文档 | Unity 官方包文档：`https://docs.unity3d.com/Packages/com.unity.timeline@1.8/manual/index.html`；EX-GAS 时间轴先读插件文档。 |
| AIBridge Unity | `Packages/com.aibridge.unity/README.md`、`Packages/com.aibridge.unity/package.json` | `Packages/com.aibridge.unity/package.json` 的 `repository.url`。 |
| PuerTS Unity MCP | `puerts-unity-mcp/README.md`、`puerts-unity-mcp/README-zh.md`、`puerts-unity-mcp/Packages/puerts-unity-mcp/package.json` | 本地 README 和 package.json；项目职责矩阵见 [`ai-quick/unity-automation-tools.md`](ai-quick/unity-automation-tools.md)。 |
| PuerTS Core / V8 | `puerts-unity-mcp/third_party/puerts/unity/upms/core/package.json`、`puerts-unity-mcp/third_party/puerts/unity/upms/v8/package.json` | 作为 PuerTS Unity MCP 的底层依赖处理，不作为独立 Unity 自动化入口。 |
| Odin Inspector | `Assets/Plugins/Sirenix/Odin Inspector/`、`Assets/Plugins/Sirenix/Assemblies/Sirenix.OdinInspector.Attributes.dll` | 官方属性文档为 `https://odininspector.com/attributes/label-text-attribute`；当前项目的 ScriptableObject 实际由 `OdinEditor` 绘制，普通字段中文标签统一使用 `[LabelText]`，不修改插件源码。 |
| UnitySkills | `Packages/com.besty.unity-skills/unity-skills~/SKILL.md`、`Packages/com.besty.unity-skills/unity-skills~/skills/*/SKILL.md`、`references/*.md` | 以包内 skill / references 为本地官方入口。 |
| NuGetForUnity | `Assets/ThirdParty/nugetforunity/package.json`、`Assets/ThirdParty/nugetforunity/README.pdf`、`Assets/NuGet.config`、`Assets/packages.config` | `package.json` 的 `documentationUrl` / `changelogUrl` 指向 NuGetForUnity 官方仓库；本项目用它管理 Unity 内的 NuGet DLL。 |
| ZLinq / ZLinq.Unity | `Assets/Packages/ZLinq.1.5.6/README.md`、`Assets/Packages/ZLinq.1.5.6/ZLinq.nuspec`、`Packages/com.cysharp.zlinq/package.json` | 官方仓库为 `ZLinq.nuspec` 的 `projectUrl`；Unity 集成按官方建议由 NuGet `ZLinq` 加 `ZLinq.Unity` UPM 包组成，本项目将 `ZLinq.Unity` 嵌入到 `Packages/com.cysharp.zlinq`，避免打开工程时依赖 GitHub 网络。 |
| System.Runtime.CompilerServices.Unsafe | `Assets/Packages/System.Runtime.CompilerServices.Unsafe.6.1.2/PACKAGE.md`、`Assets/Packages/System.Runtime.CompilerServices.Unsafe.6.1.2/System.Runtime.CompilerServices.Unsafe.nuspec` | ZLinq 的 NuGet 依赖；官方来源见 nuspec 的 `projectUrl` / `repository`。 |
| TopDownEngine | `Assets/Plugins/TopDownEngine/README.md` | 包内 README 和插件自带示例为入口；第三方源码默认不改。 |
| TableForge | `Assets/Plugins/TableForge/Demo/README.md` | 包内 Demo 文档为当前本地入口；正式接入前需补官方来源。 |
| Animation Sequencer | `Assets/Plugins/AnimationSequencer/README.md`、`CHANGELOG.MD` | 包内 README / CHANGELOG 为当前本地入口。 |
| Easy Transition | `Assets/Plugins/Easy Transition/Readme.md`、`Documentation/Documentation.md` | 包内 Documentation 为当前本地入口。 |
| LubanForConfig | `Assets/Plugins/ExOpenSource/LubanForConfig/README.md`、`README_zh.md` | 包内 README 为当前本地入口。 |

## 偏离登记要求

- 需要 Unity 版本兼容补丁时，先登记补丁目标和插件原始建议的差异，再最小修改。
- 需要项目侧扩展时，先写清它使用哪个官方扩展点、为什么不能直接配置官方能力、谁拥有运行时真相、如何验证。
- 需要删除迁移残留时，先证明它是旧业务样例、重复职责、生成源漂移或无法按官方文档解释；不要按“未使用”单独删除。
