---
name: unity-ui-development
description: "CardLoop 的 Unity UI 开发入口，裁决 UGUI、UI Toolkit、TextMeshPro、Prefab/场景 UI、编辑器 UI 和上游 UnitySkills UI 资料的项目落点。"
---

# Unity UI Development

本 skill 是 CardLoop 的 UI 任务薄入口，只写会改变项目执行方式的规则。Unity 通用教程、API 细节和示例代码不在这里复制；需要时按“上游资料入口”读取 `Packages/com.besty.unity-skills/unity-skills~/` 的对应模块或当前 Unity 官方文档。

## 角色定位

- `canonical-source`：本文件只裁决 CardLoop UI 的职责边界、默认技术路线、验收口径和禁止项。
- `adapter`：Unity 官方 `ui` / `ui-ugui` / `ui-uitk` / `ui-imgui` / `optimize-text-mesh-pro` 只作为上游候选资料，不注册成项目平行 active skill。
- `reference`：旧版通用教程正文不保留在 active 项目文档；需要 API 细节时读取上游 UnitySkills 模块或当前 Unity 官方文档。

## 默认技术路线

| UI 对象 | 默认路线 | 说明 |
| --- | --- | --- |
| 运行时 HUD、菜单、弹窗、牌桌、背包、滚动列表 | UGUI | 使用 Canvas / RectTransform / Button / ScrollRect / TMP；优先局部修 Prefab 或场景实例。 |
| World Space 文字、血条、命中反馈、卡牌悬浮信息 | UGUI + TMP | 先确认 CanvasScaler、事件相机、排序层和字体 fallback，不新建第二套表现入口。 |
| 编辑器窗口、Inspector、PropertyDrawer、数据校验面板 | UI Toolkit | 新建编辑器 UI 默认 UI Toolkit；维护已有 IMGUI 时才沿用 IMGUI。 |
| UXML / USS / VisualElement / UIDocument | UI Toolkit | 需要 API 细节时读取 UnitySkills `uitoolkit` 模块和官方文档。 |
| Figma、截图或视觉稿复刻 | 现有组件优先 | 先读真实 Prefab / 场景 / 组件层级，再决定最小实现；Figma 自动导入不作为当前项目承诺。 |

## CardLoop 项目规则

- 修改现有 UI 前，必须先读取真实 Prefab 或场景实例、CanvasScaler、LayoutGroup、RectTransform 锚点、事件入口和字体资产。
- 用户只说“按钮能用”“菜单可点”“proper buttons”“working UI”时，默认只要求视觉层级和基础交互组件成立；只有明确要求业务逻辑时，才新增或修改 C# 业务脚本。
- UI 只消费正式 Gameplay / GameCore 状态，不保存第二套业务真相；临时显示缓存必须能从正式 owner 重建。
- 运行时 UI 归 Gameplay 业务层，通用 UI 基础设施归 GameCore，Unity 无业务 UI 原语和工具封装才可下沉 YokiFrame。
- 同一 UI 职责只能有一个正式 owner；不得用新面板、临时桥接组件或测试专用脚本绕过已有 UISettings、Prefab 或正式场景入口。
- 修改 Unity 序列化资产时必须遵守 `.spec/knowledge/standards/unity-serialization-safety.md`，保留 `.meta` 和 GUID，不手写 YAML 大片段。

## TextMeshPro / 中文显示

- CardLoop 默认使用 TextMeshPro，不新增 Unity 旧 `Text` 作为正式 UI。
- 主字体资产应稳定覆盖基础中文/CJK 字形；大字符集、多语言和符号用动态 fallback，并控制构建体积。
- 布局锁定后，计时器、计数器、动态名字等高频文本默认关闭 AutoSize，避免每帧触发布局和字体重算。
- World Space 高频文字优先用 `TextMeshPro`；不要把大量频繁更新的 `TextMeshProUGUI` 长期放进同一个 World Space Canvas。
- 多个粗体、描边、发光、斜体外观优先用 TMP Material Preset，不复制字体资产。

## 执行顺序

1. 锁定 UI 对象、真相来源、目标入口和验收口径。
2. 判断是现有 UGUI、现有 UI Toolkit、编辑器 UI，还是新 UI 入口。
3. 读取对应 Prefab / 场景 / UXML / USS / C# owner 和命中的项目规范。
4. 优先做局部属性或绑定修正；只有职责归属错误时才重构 owner。
5. 更新必要文档或索引；只把可复用规则写入 `.spec/knowledge/` 或项目 skill。
6. 用最低充分验证收口：静态检查、序列化引用检查、UI 组件检查；稳定交付候选或用户要求看效果时再截图验收。

## 上游资料入口

- 通用 Unity UI：`Packages/com.besty.unity-skills/unity-skills~/skills/ui/SKILL.md`
- UI Toolkit：`Packages/com.besty.unity-skills/unity-skills~/skills/uitoolkit/SKILL.md`
- TMP / 资源 / 导入：`Packages/com.besty.unity-skills/unity-skills~/skills/importer/SKILL.md` 与项目字体资产事实
- Prefab / Scene / 序列化：`Packages/com.besty.unity-skills/unity-skills~/skills/prefab/SKILL.md`、`Packages/com.besty.unity-skills/unity-skills~/skills/scene/SKILL.md`
- 需要 Unity 版本 API 细节时，按项目规则先查询当前官方文档，再回到本文件裁决项目落点。
