---
name: unity-tilemap-2d
description: "CardLoop Unity 2D Tilemap 工作流。用于 Grid/Tilemap/Tile Palette、Collider、RuleTile、SetTile/GetTile、瓦片导入和场景验收；适用于俯视角像素地图、地表、碰撞、装饰和运行时地图编辑。"
---

# Unity 2D Tilemap

本 skill 面向 CardLoop 的 Unity 6 俯视角像素开放世界地图制作。外部来源参考：

- https://www.skills.sh/gamedev-skills/awesome-gamedev-agent-skills/unity-tilemap-2d
- 原始安装命令：`npx skills add https://github.com/gamedev-skills/awesome-gamedev-agent-skills --skill unity-tilemap-2d`

本地安装时 GitHub 克隆连接被重置，因此这里沉淀为项目侧可用版本；后续若网络恢复，可再与上游 skill 对齐。

## 先锁定

动手前先明确四件事：

- **问题对象**：具体是哪一个 Scene、Grid、Tilemap 子对象、Tile 资源、Palette、RuleTile、碰撞层或运行时绘制脚本。
- **真相来源**：优先是当前 Unity 场景层级、`Assets/Scenes`、`Assets/Art` / `Assets/Sprites` 中的瓦片素材、Tile/RuleTile 资产、Tile Palette 资产、Package Manager 与项目已有测试证据。
- **目标入口**：默认是 Unity 原生 `Grid` + `Tilemap` + `Tile Palette`，以及项目正式场景或正式地图生成入口。
- **验收口径**：回到原始场景或原始地图入口验证：瓦片可见、排序正确、碰撞正确、运行时 SetTile/GetTile 逻辑正确，且不引入第二套地图真相源。

缺任一项时，只补证据；不要直接改场景、资源、脚本或包配置。

## 何时使用

使用本 skill 的典型场景：

- 创建或整理 2D 瓦片地图：`Grid`、`Tilemap`、Tile Palette、Tile 资产。
- 给地形、墙体、水体、障碍物等 Tilemap 增加 `TilemapCollider2D` 或 `CompositeCollider2D`。
- 使用 Rule Tile / Animated Tile / Brush 做自动拼接或动画瓦片。
- 从代码生成、读取或修改瓦片：`SetTile`、`GetTile`、`ClearAllTiles`、`RefreshTile`。
- 检查瓦片素材导入设置、像素密度、Filter Mode、压缩、Sprite 切片和 Sorting Layer。
- 排查瓦片不可见、碰撞不生效、Palette 不能画、运行时生成错位等问题。

不使用本 skill 的场景：

- 地图节奏、关卡路线、区域体验设计：优先走关卡/玩法设计文档。
- 角色移动、物理交互、战斗命中：优先走项目运行时、物理或 GAS 相关入口。
- UI、HUD、背包、工作台：优先走 Unity UI 相关 skill。
- 3D 网格或 ProBuilder：不归本 skill 管。

## 包与版本

- 项目当前 Unity 版本以根目录 `AGENTS.md` 为准。
- 核心 `Grid`、`Tilemap`、`Tile`、`TilemapCollider2D` 属于 Unity 2D Tilemap 内建能力。
- Rule Tile、Animated Tile、额外 Brush 通常来自 `2D Tilemap Extras` 包：`com.unity.2d.tilemap.extras`。
- 涉及包安装、版本、API 文档或迁移时，按项目规则先用 `ctx7` 查询当前文档，再改 `Packages/manifest.json` 或包配置。

## 推荐层级

常规场景层级：

```text
Grid
  Tilemap_Ground      地表，可走
  Tilemap_Decoration  装饰，无碰撞
  Tilemap_Water       水体，按玩法决定碰撞/触发
  Tilemap_Blocking    阻挡，带碰撞
```

约定：

- 每个 Tilemap 只承担一个明确职责，不把地表、装饰、阻挡混在同一层。
- 命名优先中文语义或项目已有命名；Unity 组件和代码符号保留英文。
- 排序优先使用项目现有 Sorting Layer / Order 规则，不临时另造一套。
- 正式地图不要同时保留“Tilemap 场景绘制”和“另一份并行地图数据”作为同职责真相源；若需要运行时数据化，必须明确谁是作者入口、谁是导出/生成产物。

## 制作流程

1. **素材导入**
   - 确认 Sprite Mode、Pixels Per Unit、Filter Mode、Compression 与项目像素风规范一致。
   - 图集或 SpriteSheet 先确认切片网格、透明边、像素边界和 `.meta` 稳定性。
   - 不批量改第三方素材导入设置，除非已锁定影响范围和回退方式。

2. **Tile 与 Palette**
   - 为常用瓦片创建 `Tile` / `RuleTile` 资产，放入项目正式资源目录。
   - Tile Palette 只作为编辑入口，不作为运行时真相源。
   - RuleTile 使用前先确认 `com.unity.2d.tilemap.extras` 是否已安装。

3. **场景层级**
   - 在正式 Scene 中创建或复用 `Grid`。
   - 按职责拆分 Tilemap 子对象：地表、装饰、阻挡、水体、交互层等。
   - 设置 Tilemap Renderer 的 Sorting Layer / Order，保持角色、投影、装饰之间的遮挡关系可解释。

4. **碰撞**
   - 阻挡层优先使用 `TilemapCollider2D`。
   - 大面积静态碰撞需要合并时，再叠加 `CompositeCollider2D` 和 `Rigidbody2D` Static。
   - 不要把纯装饰层也加碰撞，除非玩法明确需要。

5. **运行时绘制**
   - 使用 `SetTile(Vector3Int cell, TileBase tile)` 写入瓦片。
   - 使用 `GetTile(Vector3Int cell)` 读取当前瓦片。
   - 世界坐标转格子坐标时，使用目标 Tilemap / Grid 的转换方法，不手写偏移猜坐标。
   - 批量绘制后按需调用刷新，不用每格都做昂贵刷新。

6. **验收**
   - 回到原始 Scene 或地图入口，检查可见层、碰撞层、运行时读写和排序。
   - 对碰撞问题，至少验证角色/测试体与阻挡层的真实接触结果。
   - 对运行时生成问题，至少验证一个左下/中心/右上或边界格。

## 常见排查

- **瓦片不可见**：先查 Tilemap Renderer、Sorting Layer、Order、相机 Culling Mask、瓦片资源是否为空。
- **Palette 画不上**：先查当前选中的 Active Tilemap、Grid 是否存在、Tile 资产是否有效。
- **碰撞不生效**：先查 `TilemapCollider2D` 所在层、Physics 2D Layer Collision Matrix、是否需要 `CompositeCollider2D` 和 Static Rigidbody2D。
- **运行时错位**：先查世界坐标到格子坐标的转换入口，确认使用的是同一个 Grid/Tilemap。
- **RuleTile 不工作**：先查 2D Tilemap Extras 是否安装、邻接规则是否覆盖当前格、是否需要刷新相邻格。
- **像素边缘异常**：先查素材导入的 Filter Mode、Compression、Pixels Per Unit、Sprite 切片边界和相机像素适配。

## 禁止事项

- 不因单个瓦片问题新造第二套地图编辑器或第二套地图数据源。
- 不擅自修改第三方素材包的原始资源、插件源码或导入设置；确需修改先锁定对象、影响和回退方式。
- 不把运行时缓存、生成产物或测试场景当成正式作者入口。
- 不在未确认 Unity 包状态时直接写依赖。
- 不用兜底空 Tile 或吞异常来假装地图生成成功；这只能算止血，不能算修复。

## 与项目其它入口的关系

- Unity Editor 自动化、场景读取、截图和 Console：优先查全局 `D:\codex-home\skills\unity-skills\SKILL.md`，再结合本项目本地包 `Packages/com.besty.unity-skills`、`Packages/com.aibridge.unity`。
- 通用 Unity 工程、包、Prefab、Scene、资源规范：先查 `.spec/knowledge/features/project/README.md`；若尚未沉淀项目事实，回到当前工程文件和 Unity 验证证据。
- 像素素材、SpriteSheet、导入设置：先查项目事实入口；没有正式规则时，只能基于当前资源导入设置和用户目标做最小改动。
- 图片/图集读取验收：不使用已剔除的 `safe-image-reading`；用户要看图时按全局 `D:\codex-home\skills\show-image-to-user\SKILL.md`。
- UI Tile/Grid 布局不是 Tilemap：走 `.agents/skills/unity-ui-development`。

