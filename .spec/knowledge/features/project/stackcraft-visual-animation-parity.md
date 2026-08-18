---
name: stackcraft-visual-animation-parity
description: StackCraft 表面与动画一致性清单：对账卡面、材质、进度、命中、粒子、投射物和拖拽 / 移动 / 受击动画是否由当前框架等效承接。
metadata:
  type: feature
  status: 设计中
---

# StackCraft 表面 / 动画一致性清单

## 当前结论

机制、源码映射、PlayMode 和代表性业务审计只能证明已覆盖的规则效果成立，不能证明玩家可见表面和动画已经与 StackCraft 一致。当前专项按“一比一复刻”处理：先以 StackCraft 源码 / Prefab / Material / Shader / Mesh / 资源闭包参数为主证据，截图只用于发现偏差和最终诊断。

当前状态必须表述为：

- 已有机制证据：Gameplay 自有框架接管了一批 StackCraft 机制效果，并完成 Starter / Beginning 代表性业务竖切审计。
- 静态表面正在订正：正确方向是自有 `卡牌.fbx` + 自有 `卡牌表面.shadergraph` + `卡牌表面_*.mat` + `_OverlayTex` 卡图覆盖；牌桌表现必须使用 StackCraft 的 XZ 桌面语义，Unity Y 只表达离桌高度；旧 SpriteRenderer 底板 / 独立插画层方案和 XY 平面适配方案都不能作为完成证据。
- 尚未完成：卡图比例和文字落点源码参数对账、外轮廓画面厚度、拖拽手感、移动补间、镜头动画、商贩 / 卡包表面和整体验收截图；已经通过静态守卫或目标测试的粒子、命中、投射物和受击项，只能按各自行覆盖范围声明。
- 禁止口径：在本表未收口前，不得说“和模板一致”“完整复刻”“可以删除模板”。

本表不是要求复制 StackCraft 旧 MonoBehaviour、旧单例、旧 `Resources`、旧输入或旧 UI 结构；它要求把模板承载玩家效果的表面和动画，用 CardLoop 正式对象、ResourceSystem、UIKit / 牌桌视图和 EX-GAS / GameCore 表现链等效实现，并给出来源证据。

## 审计口径

| 层级 | 说明 | 当前是否完成 |
|---|---|---|
| 机制一致 | 触发条件、规则结算、状态变化、业务参数、存档 / 任务事实。 | 部分已完成，有自动化和只读审计证据。 |
| 表面一致 | 玩家静止画面可见的卡面、材质、图标、文字、位置、比例、颜色、进度条、详情面板。 | 订正中。卡牌表面必须先通过 MeshRenderer / 材质链和测试 Prefab 回读，之后才进入截图诊断。 |
| 动画一致 | 玩家操作和结算过程中看到的拖拽、拆堆、合堆、移动、抬升、受击、命中、投射物、粒子、镜头。 | 未完成，需要逐项对照。 |
| 删除模板门槛 | 正式链路无依赖、代表性业务可追溯、表面 / 动画缺口已对账并处理、用户授权删除、删除后 Unity 验证。 | 未满足。 |

## 证据账本

| 范围 | StackCraft 证据 | 当前 CardLoop 证据 | 当前判断 |
|---|---|---|---|
| 卡牌 Prefab 分族 | `Assets/StackCraft/Prefabs/Cards/Card_*.prefab`；每类卡用 `MeshFilter` + `MeshRenderer` + 类别材质，角色卡还有 `EquipmentPanel`、`Health`；根 Collider 为 `0.8 × 0 × 1.0000002`，文本局部旋转 `x = 90`。 | 正确链路是 `CardDefinition.m_cardSurface` 声明类别材质；`TabletopView` 通过 `ResourceSystem.LoadAssetAsync<Material>` 加载；`TabletopCardView.SetSurfaceMaterial` 写入 `MeshRenderer`。旧 `SpriteRenderer` 表面方案和 XY 牌桌姿态已判定错误，测试 Prefab / SO 仍需重建和静态预检确认。 | 订正中。必须按源码参数复刻，不能按截图调成“类似”。 |
| 卡牌材质分类 | `Assets/StackCraft/Materials/Cards/*.mat`；角色、材料、配方、资源、敌对等有不同材质，材质引用 `Card.shadergraph` 并包含 `_BaseTex`、`_OverlayTex`、`_OverlayScale`、`_OverlayOffset`、`_OverlayTint`、`_FlashAmount`。 | 类别材质已迁入 `Assets/Art/Materials/卡牌表面_*.mat`，卡牌分族贴图保留在 `Assets/Art/Sprites/StackCraft/Cards/*.png`，测试作者源的表面地址应使用 `卡牌表面_角色` / `卡牌表面_生物` 等材质地址。 | 订正中。材质参数和 YooAsset 收集项由静态预检守卫。 |
| 卡面文字落点 | StackCraft 卡牌 Prefab 中 `Title` 位于 `z = 0.4`，价格 / 营养 / 生命在卡牌不同局部位置；角色生命显示当前生命数字，不显示 `当前/上限`。 | `牌桌测试卡牌视图.prefab` 现在有标题、价格、营养、使用次数、角色生命和战斗结果文本；`TabletopCardView` 不再缩放根对象，文字落点按模板局部坐标保留，Villager 生命按 StackCraft 原始 15 投影。 | 静态字段和源码几何已订正；仍需真实截图确认落点 / 字号观感。 |
| 牌桌坐标语义 | `CardController.GetMouseWorldPosition()` 使用 `Plane(Vector3.up, Vector3.zero)`；拖拽时 `mousePos.y = DragHeight`；`CardStack.stackStep = (0, 0.002, -0.18)`；`Board` 按 X/Z 判断边界。 | `TabletopCoordinateSpace` 是当前唯一映射：二维桌面坐标 `(x, y)` 映射到 Unity 本地 `(x, height, y)`；`TabletopCardLayout`、拖拽投影、镜头、烟雾、投射物和战斗区域必须走该映射。 | 订正中。任何回到 `new Vector3(x, y, 0)` 的牌桌表现都视为 XY 适配回流。 |
| 卡图承载 | `CardInstance.SetDefinition` 把定义的 ArtTexture 写入材质 `_OverlayTex`，角色材质 `_OverlayScale = 0.8`、`_OverlayOffset.x = 0.1`、`_OverlayTint` 为深蓝灰。 | 正确链路是 `TabletopCardView.SetArtwork(Sprite)` 把卡图纹理写入当前表面材质属性块 `_OverlayTex`；不得保留独立 `m_artworkRenderer`、`m_artworkPadding`、黑底透明材质或 SpriteRenderer 插画层。 | 订正中。代码路径已改，测试 Prefab 序列化残留必须清除后才能进入截图诊断。 |
| 候选高亮 | `CardInstance.SetHighlighted` / `Highlight` 使用原卡 Mesh 和 `Default_Card_Settings.outlineMaterial`，该材质引用 `CardOutline.shadergraph` 子资源。 | 当前应由自有 `Assets/Art/Shaders/卡牌轮廓.shadergraph` + 测试卡牌 Prefab 内的 MeshFilter / MeshRenderer 高亮节点承接；四条 Sprite 线框属于已排除的近似实现。 | 订正中。静态守卫已禁止线框模拟，仍需重建 Prefab 后回读 Mesh / 材质引用。 |
| 牌堆拖拽 | `CardController` 在按下时拆堆、抬高到 `DragHeight`、随鼠标移动，落下时播放拿起 / 放下音效并重新放置；尾随卡牌使用 `precedingCard.transform.position + stackStep` 与 `swaySharpness = 100` 链式跟随。 | `TabletopCardDragInput` 走新输入系统；`TabletopView` 拖拽预览应使用上一张卡当前表现位置加 StackCraft 步进，测试视图设置应写 `m_dragFollowSharpness = 100`。 | 订正中。链式跟随源码已对齐，仍需重建资产并回读设置值。 |
| 牌堆移动补间 | StackCraft `CardSettings.moveDuration = 0.1`、`moveEase = Ease.OutQuad`；`CardInstance.SetTargetAnimated` 使用 `DOMove(...).SetEase(...).SetUpdate(true)`，新建牌堆首次落位 `instant: true`。 | `TabletopCardView.ApplyPose` 由 `TabletopViewSettings.m_moveDurationSeconds = 0.1` 驱动 DOTween `DOLocalMove` + `Ease.OutQuad` + `SetUpdate(true)`；`TabletopView` 新实例首次落位传 0 秒，后续权威姿态变化传 0.1 秒；测试资产已回读 `m_moveDurationSeconds: 0.1`。 | 当前子项已通过覆盖范围验证：静态预检、Unity 编译、中间牌拖出尾段后边界修订投影、空白释放整堆重叠分离两个 PlayMode 用例通过；最终连续动画观感仍留到整体验收截图。 |
| 行动进度条 | `ProgressUI.prefab` 是 Canvas UI，灰底 + 黄色填充，`CraftingManager` 世界锚点实例化。 | `牌桌测试行动进度.prefab` 仍由牌桌视图实例化，但静态颜色已改成 StackCraft 灰底 `{0.25,0.25,0.25,0.8}` 与黄色填充 `{1,0.7974138,0,1}`；尺寸按当前 SpriteRenderer 约束换算。 | 静态表面完成，动画 / 锚点观感待验收。 |
| 烟雾粒子 | `PuffParticle.prefab` + `Puff.mat` + `Puff.png` + `Puff.wav`；`PuffParticle.Awake` 播放 `AudioId.Puff`，并在 `main.duration + 0.1f` 后销毁；`CardInstance.PlayPuffParticle`、制作产出、交易和击杀等位置实例化。 | `Assets/Art/Prefabs/卡牌烟雾粒子.prefab` 由参考 `PuffParticle.prefab` 克隆参数后替换为 `TabletopCardSmokeEffectView`；材质 / 贴图 / 音效迁入 `Assets/Art` / `Assets/Audio`；`TabletopPresentationCueKind.CardSmoke` 由牌桌视图在对应卡牌坐标实例化，释放时长按 `main.duration + 0.1f`。 | 当前子项已通过覆盖范围验证：Prefab 静态参数、释放时长、静态预检、Unity 编译和目标 EditMode 粒子触发测试通过；最终画面仍留到整体验收截图。 |
| 受击反馈 | `CardInstance.TakeDamage`：材质 `_FlashAmount` 延迟 `0.05` 秒后用 `DOFloat(..., 0.1)`，`SetLoops(2, Yoyo)`；同时 `DOPunchRotation(0, 15, 0)`，duration `0.25`，vibrato `25`，`SetUpdate(true)`。 | `TabletopCardView` 已在命中且实际伤害大于 0 时触发卡牌本体闪白和 Unity Y 轴摇晃；闪白用 DOTween.To 驱动 `_FlashAmount` 的 MaterialPropertyBlock 等价投影，摇晃直接用 `DOPunchRotation(new Vector3(0, 15, 0), 0.25, vibrato: 25)`；测试 Prefab 必须序列化 `m_hurtFlashDelaySeconds = 0.05`、`m_hurtFlashTweenSeconds = 0.1`、`m_hurtFlashLoopCount = 2`、`m_hurtPunchRotationDegrees = 15`、`m_hurtPunchDurationSeconds = 0.25`、`m_hurtPunchVibrato = 25`。 | 当前子项已通过覆盖范围验证：删除手写 `sin` / `EaseOutQuad` 近似算法，`gameplay-static-preflight`、`spec-lint`、Unity 编译和目标 PlayMode 命中反馈测试通过；最终画面仍留到整体验收截图。 |
| 命中 UI | `HitUI.prefab` + `HitUI.Initialize`：Miss / Normal / Critical 图标、优势 / 劣势图标、伤害文字、`DOPunchScale(0.15, 1s)` 后销毁。 | `TabletopHitResultView` 作为独立浮动 UI 承接模板命中结果；测试设置通过 `m_hitResultViewPrefab` 引用 `牌桌测试命中结果.prefab`，卡牌本体只保留受击闪白 / 摇晃。 | 当前子项已通过覆盖范围验证：独立 Prefab、设置引用、静态预检、Unity 编译和目标 PlayMode 命中反馈测试通过；最终屏幕位置和 punch 观感留到整体验收截图。 |
| 投射物 | `Projectile_Arrow.prefab` / `Projectile_Magic.prefab`，SpriteRenderer 尺寸 `1.28 × 1.28`；Prefab 实际序列化 `duration: 0.5`，运行时 `CombatProjectile.Fire` 用 `LookRotation(direction.Flatten()) * Euler(90,0,0)` 朝向目标并线性飞行。 | `牌桌测试投射物.prefab` 接入 `箭矢投射物.png` / `魔法投射物.png` 自有副本；`TabletopProjectileView` 按 `Combat_Ranged` / `Combat_Magic` 选择图片，尺寸回写 `1.28 × 1.28`，朝向公式与飞行时长按 Prefab 实际参数对齐。 | 当前子项已通过覆盖范围验证：Prefab 尺寸、朝向公式、飞行时长、静态预检、Unity 编译和目标 EditMode 投射物延迟结算测试通过；最终飞行动画截图留到整体验收。 |
| 镜头聚焦 | `CameraController.MoveTo`，商贩解锁和遭遇生成会移动镜头。 | `TabletopPresentationCueKind.CameraFocus` + `TabletopCameraController`。 | 部分。功能入口有，运动曲线和时序未验收。 |
| 商贩 / 卡包表面 | `PackVendor.prefab`、`PackInstance.prefab`、交易区高亮和烟雾。 | 当前以卡牌 / 行动链表达购买与卡包打开，具体表面需查当前 Prefab。 | 未判定。需要纳入后续静态对照。 |

## 下一步顺序

1. **卡牌表面优先**：先对齐卡牌类别材质 / 卡图比例 / 标题、价格、营养、生命文字的可见承载，避免还没静止画面就讨论动画。
2. **必要反馈第二**：再对齐候选高亮、行动进度、命中 UI、烟雾粒子、投射物素材和镜头聚焦。
3. **动作生命周期第三**：最后对齐拖拽抬升、移动补间、受击闪烁 / 摇晃、投射物飞行、命中结果 punch 和销毁时序。
4. **验证只按覆盖范围声明**：静态源码 / Prefab 对照证明结构差异；Unity 编译证明资源可用；PlayMode 证明链路可跑；截图只能证明当前画面，不能证明未截图动画段。

## 当前阻塞

没有工程级阻塞；当前阻塞是证据缺口：静态表面只完成了一部分，动画和若干表面项尚未完成逐项对照和补齐。完成前不能删除 `Assets/StackCraft`，也不能宣称模板玩家效果整体一致。

## 静态守卫

`node .spec/tools/gameplay-static-preflight.mjs` 当前额外检查：

- `Assets/Art/Sprites/StackCraft` 必须由 YooAsset 以 `AddressByFolderAndFileName` 收集，确保 `Cards/Character.png` 等覆盖图来源可加载。
- StackCraft 12 张卡牌分族贴图和 12 个自有类别材质必须存在并保留 `.meta`。
- 测试作者源里的 `m_cardSurface` 地址必须落在 `卡牌表面_*` 材质地址白名单内，不得再使用 `Cards_*` 图片地址。
- 卡牌视图 Prefab 必须使用 `MeshFilter` + `MeshRenderer` + 自有 `卡牌.fbx` + 自有 `卡牌表面_角色.mat`；候选高亮必须引用自有 `卡牌轮廓.shadergraph` 子资源；不得保留独立 SpriteRenderer 插画层、旧 `m_artworkRenderer`、旧 `m_artworkPadding`、旧“表面详情”混合文本节点、整块绿色候选遮罩或四条 Sprite 线框高亮。
- 行动进度 Prefab 必须保持 StackCraft 灰底和黄色填充。
- 投射物 Prefab 必须引用箭矢 / 魔法图片，不能退回旧占位缩放。
