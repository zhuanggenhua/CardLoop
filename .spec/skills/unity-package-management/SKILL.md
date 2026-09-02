---
name: unity-package-management
description: Unity Package Manager（UPM）包查询、安装、移除、升级和版本裁决的 CardLoop 项目入口；用户说装包、UPM、添加 com.unity.* 或改 manifest 时使用。
---

# Unity Package Management（项目适配）

本 skill 只管 Unity Package Manager（UPM）层面的包选择、查询、安装、移除和升级。它不负责插件业务接入，不替代 `.spec/knowledge/features/plugin-docs.md` 的插件真相源登记，也不直接修改第三方插件源码。

上游候选来源：`https://github.com/Unity-Technologies/skills/skills/unity-package-management/SKILL.md`。已吸收的有效增量是：Unity CLI 不负责 UPM、包变更应走 `UnityEditor.PackageManager.Client` 或 UnitySkills 包模块、不要手改 `Packages/manifest.json` 作为默认方案、安装/移除会触发包解析和 Domain Reload。

## 默认路径

CardLoop 已安装 UnitySkills，本项目默认优先用 UnitySkills 的包模块执行 UPM 操作：

- 查询已装包：`project_get_packages` 或 `package_list`
- 检查单个包：`package_check`
- 查询依赖或版本：`package_get_dependencies` / `package_get_versions`
- 安装包：`package_install`
- 移除包：`package_remove`
- 刷新包缓存：`package_refresh`

准确参数和返回值以 `.spec/skills/unity-skills/SKILL.md` 指向的 UnitySkills server schema / dryRun 为准；不要从本文猜参数。包模块参考：`Packages/com.besty.unity-skills/unity-skills~/skills/package/SKILL.md`。

## 前提锁定

准备安装、移除或升级包前，必须先锁定：

1. 现实目标：为什么要改这个包，解决哪个项目问题。
2. 真相来源：目标包的官方文档、package id、当前项目 `Packages/manifest.json` 和 `Packages/packages-lock.json`。
3. 目标入口：优先 UnitySkills 包模块；只有 Editor 不适合且用户明确需要无头流程时，才评估官方上游 headless C# Client API 模式。
4. 验收口径：包解析成功、manifest / lockfile 变化可解释、Unity 编译通过，必要时对应功能 smoke 通过。

## 禁止场景

- 不得因为官方 skill 推荐某包，就直接安装；先判断 CardLoop 是否真的需要。
- 不得手写修改 `Packages/manifest.json` 作为默认安装/移除方案。
- 不得绕过 `.spec/tools/unity-verify.mjs`、插件文档索引或 Unity 序列化安全规则。
- 不得安装商业化、广告、联机、云服务、语音、XR、平台发布相关包，除非用户当轮明确把目标切到对应领域。
- 不得把包安装成功说成业务接入完成；迁移、复制、接入和启用必须分开汇报。
- 不得在已打开 Editor 的情况下另起第二个 Editor 来跑 headless 包脚本。

## 允许的无头候选

只有在 UnitySkills 不适合、Editor 没有打开、用户明确需要非交互包变更，且本轮已经读过上游 `unity-package-management` 正文时，才允许评估 headless `UnityEditor.PackageManager.Client.AddAndRemove` 模式。

该模式的关键边界：

- 使用 Editor binary 的 `-batchmode -executeMethod`。
- 不加 `-quit`，因为 PackageManager request 需要后续 Editor update tick 才会完成。
- 临时 C# 脚本必须放在 `Assets/Editor/` 或 Editor-only assembly。
- 脚本必须在 request 完成后自行 `EditorApplication.Exit(code)`。
- 执行后必须验证 `Packages/manifest.json`、`Packages/packages-lock.json` 和 Unity 编译结果。

这个无头模式是备用方案，不是默认流程；能用 UnitySkills 包模块时不要新建 bootstrap 脚本。

## 汇报要求

包操作完成后必须说明：

- 改了哪个包，以及现实目的。
- 入口是 UnitySkills 包模块、Unity Editor batchmode，还是其它经用户确认的路径。
- `manifest.json` / `packages-lock.json` 的变化。
- Unity 包解析、编译或 smoke 验证结果。
- 若只是安装包，还不能称为功能接入完成。
