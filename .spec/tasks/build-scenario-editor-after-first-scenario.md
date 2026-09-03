---
status: pending
---

# 首个剧本完成后启动关卡编辑器

关卡编辑器必须等待首个最小可运行剧本完成后再开始，以真实剧本制作需求决定第一版工具范围。本卡不允许在 `complete-first-scenario-slice` 完成前进入实现。

## 涉及范围

- `Assets/Editor/Gameplay/Scenarios/ScenarioEditorWindow.cs`
- `Assets/Editor/Gameplay/Scenarios/ScenarioEditorWindow.uxml`
- `Assets/Editor/Gameplay/Scenarios/ScenarioEditorWindow.uss`
- `Assets/Editor/Gameplay/Scenarios/ScenarioEditorValidationPanel.cs`
- `Assets/Editor/Gameplay/Scenarios/ScenarioEditorPreviewRunner.cs`
- `Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioDefinition.cs`
- `Assets/Scripts/Gameplay/Runtime/Scenarios/ScenarioRegionDefinition.cs`

## 验收标准

- [ ] 启动前确认 `complete-first-scenario-slice` 已完成，并使用该剧本的字段清单裁剪第一版编辑器范围。
- [ ] 编辑器使用 UI Toolkit，服务 Unity Editor 内作者流程，不改变运行时 UI 技术栈。
- [ ] 编辑器只写正式作者源，运行时仍由 `ScenarioDirector -> ScenarioRun -> ScenarioRegion -> Tabletop` 消费。
- [ ] 第一版不接入 Lua 或其它脚本执行层，只保留强类型触发器和结果意图的作者入口。
- [ ] 内置剧本也通过该编辑器维护，不保留手工维护和编辑器维护两套长期流程。

## 依赖

- `complete-first-scenario-slice`

## 接口

Consumes: `complete-first-scenario-slice` 产出的首个内置剧本资产路径、真实启动入口、内容校验证据、关卡编辑器第一版字段清单。

Produces: UI Toolkit 关卡编辑器窗口、字段绑定、校验面板和首个剧本回读预览入口。
