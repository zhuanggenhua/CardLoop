---
name: plugin-docs
description: 第三方插件、本地 UPM 包和外部框架的官方文档入口索引；项目侧只记录入口、适配和偏离，不替代官方建议。
metadata:
  type: doc
  status: 已交付
---

# 插件官方文档入口

本文件是 CardLoop 的插件文档索引，不是插件使用教程。接入、审查或清理插件相关代码时，默认先读插件自带 README / Wiki / SKILL / package.json / 官方仓库，再判断项目侧是否需要薄适配或偏离登记。

## 使用规则

- 插件官方文档和包内 README / Wiki 是用法真相源；项目 `.spec` 只记录当前项目入口、适配边界和偏离原因。
- `Assets/Plugins` 和 `Packages` 下的第三方源码、生成器、示例和文档未经当轮明确授权不直接修改。
- 如果插件已经提供官方扩展点，项目侧只实现扩展点；不得把迁移桥接、测试护栏或旧业务样例包装成新的项目框架 owner。
- 如果插件文档缺失、版本不明或官方仓库不可达，只能登记缺口和最小补证据动作，不用手写替代实现冒充官方建议。

## 当前入口

| 插件 / 包 | 本地官方文档入口 | 官方仓库 / 文档入口 |
|-----------|------------------|----------------------|
| EX-GAS | `Assets/Plugins/GAS/SKILL.md`、`Assets/Plugins/GAS/Wiki/*.md`、`Assets/Plugins/GAS/package.json` | `Assets/Plugins/GAS/package.json` 的 `documentationUrl` / `repository.url`。 |
| YokiFrame | `Assets/Plugins/YokiFrame/README.md`、`Assets/Plugins/YokiFrame/Core/Editor/AI_NAVIGATION.md`、`Assets/Plugins/YokiFrame/Core/Editor/Skills/*/SKILL.md` | `Assets/Plugins/YokiFrame/package.json` 的 `repository.url`。 |
| YooAsset | `Packages/com.tuyoogame.yooasset/README.md`、`Packages/com.tuyoogame.yooasset/CHANGELOG.md`、`Packages/com.besty.unity-skills/unity-skills~/skills/yooasset-design/*.md` | `Packages/com.tuyoogame.yooasset/package.json` 的 `repository.url`。 |
| BroAudio | `Packages/com.ami.broaudio/package.json` | `documentationUrl` 指向 BroAudio 官方 GitBook，`repository.url` 指向 GitHub。 |
| AIBridge Unity | `Packages/com.aibridge.unity/README.md`、`Packages/com.aibridge.unity/package.json` | `Packages/com.aibridge.unity/package.json` 的 `repository.url`。 |
| UnitySkills | `Packages/com.besty.unity-skills/unity-skills~/SKILL.md`、`Packages/com.besty.unity-skills/unity-skills~/skills/*/SKILL.md`、`references/*.md` | 以包内 skill / references 为本地官方入口。 |
| TopDownEngine | `Assets/Plugins/TopDownEngine/README.md` | 包内 README 和插件自带示例为入口；第三方源码默认不改。 |
| TableForge | `Assets/Plugins/TableForge/Demo/README.md` | 包内 Demo 文档为当前本地入口；正式接入前需补官方来源。 |
| Animation Sequencer | `Assets/Plugins/AnimationSequencer/README.md`、`CHANGELOG.MD` | 包内 README / CHANGELOG 为当前本地入口。 |
| Easy Transition | `Assets/Plugins/Easy Transition/Readme.md`、`Documentation/Documentation.md` | 包内 Documentation 为当前本地入口。 |
| LubanForConfig | `Assets/Plugins/ExOpenSource/LubanForConfig/README.md`、`README_zh.md` | 包内 README 为当前本地入口。 |

## 偏离登记要求

- 需要 Unity 版本兼容补丁时，先登记补丁目标和插件原始建议的差异，再最小修改。
- 需要项目侧扩展时，先写清它使用哪个官方扩展点、为什么不能直接配置官方能力、谁拥有运行时真相、如何验证。
- 需要删除迁移残留时，先证明它是旧业务样例、重复 owner、生成源漂移或无法按官方文档解释；不要按“未使用”单独删除。
