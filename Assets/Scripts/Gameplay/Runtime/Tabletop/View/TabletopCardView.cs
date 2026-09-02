using System;
using System.Collections.Generic;
using DG.Tweening;
using GAS.Runtime;
using GameCore;
using Gameplay.Content;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 单张牌桌卡牌的 Unity 表现组件，只投影身份、卡面、姿态、高亮和角色可见状态。
	/// </summary>
	public sealed class TabletopCardView : MonoBehaviour
	{
		private static readonly Vector3 PackVendorTitleLocalPosition = new Vector3(0f, 0f, 0f);
		private static readonly Vector2 PackVendorTitleSize = new Vector2(0.7f, 0.4f);
		private const float PackVendorTitleFontSize = 1.2f;
		private static readonly Vector3 PackVendorTrackerLocalPosition = new Vector3(0f, 0f, 0.35f);
		private static readonly Vector2 PackVendorTrackerSize = new Vector2(0.8f, 0.3f);
		private const float PackVendorTrackerFontSize = 1.1f;
		private static readonly Vector3 PackVendorPriceLocalPosition = new Vector3(0f, 0f, -0.35f);
		private static readonly Vector2 PackVendorPriceSize = new Vector2(0.8f, 0.3f);
		private const float PackVendorPriceFontSize = 1.1f;
		private static readonly Vector2 StackCraftTextAnchoredPosition = new Vector2(0f, 0.001f);
		private const float StackCraftVendorTextFontSizeMin = 18f;
		private const float StackCraftVendorTextFontSizeMax = 72f;
		private static readonly Vector3 PackInstanceTitleLocalPosition = new Vector3(0f, 0f, -0.43f);
		private static readonly Vector2 PackInstanceTitleSize = new Vector2(0.8f, 0.2f);
		private static readonly Vector4 PackInstanceTitleMargin = new Vector4(0.05f, 0.025f, 0.05f, 0.025f);
		private static readonly Color PackInstanceTitleColor = new Color(1f, 1f, 1f, 0.6f);
		private const float PackInstanceTitleFontSize = 1f;
		private const float PackInstanceTitleMinFontSize = 0.4f;
		private const float PackInstanceTitleMaxFontSize = 1f;
		private static readonly Vector3 DefaultCardColliderSize = new Vector3(0.8f, 0f, 1.0000002f);
		private static readonly Vector3 PackInstanceColliderSize = new Vector3(0.9f, 0f, 1.3000002f);

		[Header("表现组件")]
		[SerializeField]
		[LabelText("表面渲染器")]
		[Tooltip("卡牌表面的 MeshRenderer。StackCraft 卡面由同一个材质承载底板、插画覆盖层、颜色和受击闪光。")]
		private Renderer m_surfaceRenderer;

		[SerializeField]
		[LabelText("表面网格")]
		[Tooltip("卡牌表面的 MeshFilter。普通卡牌和卡包共用同一个视图组件，但必须按内容类型切换 StackCraft 对应 FBX。")]
		private MeshFilter m_surfaceMeshFilter;

		[SerializeField]
		[LabelText("普通卡牌网格")]
		[Tooltip("StackCraft Card.fbx 的自有副本，用于普通卡、商贩和收购点。")]
		private Mesh m_defaultSurfaceMesh;

		[SerializeField]
		[LabelText("卡包网格")]
		[Tooltip("StackCraft Pack.fbx 的自有副本，用于 CardPackDefinition，不能用普通卡牌网格缩放模拟。")]
		private Mesh m_packSurfaceMesh;

		[SerializeField]
		[LabelText("高亮网格")]
		[Tooltip("候选高亮的 MeshFilter。卡包切换 Pack.fbx 时，高亮轮廓必须同步切换。")]
		private MeshFilter m_highlightMeshFilter;

		[SerializeField]
		[LabelText("表面纹理属性")]
		[Tooltip("StackCraft Card shadergraph 接收卡面插画的纹理属性名。")]
		private string m_surfaceTextureProperty = "_OverlayTex";

		[SerializeField]
		[LabelText("受击闪光属性")]
		[Tooltip("StackCraft Card shadergraph 接收受击闪白强度的浮点属性名。")]
		private string m_surfaceFlashProperty = "_FlashAmount";

		[SerializeField]
		[LabelText("高亮节点")]
		[Tooltip("可选的高亮子节点。拖拽命中空间候选时只切换其显隐，不创建材质或规则状态。")]
		private GameObject m_highlightRoot;

		[SerializeField]
		[LabelText("收购点货币图标")]
		[Tooltip("StackCraft CardBuyer 子物体 Icon 的 MeshRenderer，只在收购点卡牌上显示。")]
		private MeshRenderer m_cardBuyerCurrencyIconRenderer;

		[SerializeField]
		[LabelText("收购点图标纹理属性")]
		[Tooltip("StackCraft CurrencyIcon 材质接收货币图片的纹理属性名。")]
		private string m_cardBuyerCurrencyTextureProperty = "_MainTex";

		[SerializeField]
		[LabelText("标题文本")]
		[Tooltip("显示卡牌作者源名称。它属于卡牌本体表面，不替代右侧详情面板。")]
		private TMP_Text m_titleLabel;

		[SerializeField]
		[LabelText("价格文本")]
		[Tooltip("显示 StackCraft 卡面底部价格数字；复杂价格说明仍放到详情面板。")]
		private TMP_Text m_priceLabel;

		[SerializeField]
		[LabelText("营养文本")]
		[Tooltip("显示 StackCraft 食物卡底部营养数字；非食物卡保持隐藏。")]
		private TMP_Text m_nutritionLabel;

		[SerializeField]
		[LabelText("角色状态节点")]
		[Tooltip("角色卡显示当前生命时启用；普通卡牌保持隐藏。")]
		private GameObject m_characterStatusRoot;

		[SerializeField]
		[LabelText("装备面板节点")]
		[Tooltip("StackCraft 只有玩家角色卡 Prefab 带装备面板；当前合成卡牌 Prefab 必须按卡牌类别标签显隐，避免普通卡和怪物卡额外参与物理命中。")]
		private GameObject m_equipmentPanelRoot;

		[SerializeField]
		[LabelText("生命文本")]
		[Tooltip("直接显示角色唯一 EX-GAS Health/MaxHealth 当前值。")]
		private TMP_Text m_healthLabel;

		[SerializeField]
		[LabelText("受击闪白延迟")]
		[Tooltip("StackCraft 受击闪白 Tween 的 SetDelay 参数。")]
		private float m_hurtFlashDelaySeconds = 0.05f;

		[SerializeField]
		[LabelText("受击闪白单段秒数")]
		[Tooltip("StackCraft 受击闪白 DOFloat(1, _FlashAmount, 0.1) 的单段时长。")]
		private float m_hurtFlashTweenSeconds = 0.1f;

		[SerializeField]
		[LabelText("受击闪白循环次数")]
		[Tooltip("StackCraft 受击闪白 SetLoops(2, Yoyo) 的循环次数。")]
		private int m_hurtFlashLoopCount = 2;

		[SerializeField]
		[LabelText("受击摇晃角度")]
		[Tooltip("卡牌本体受击时绕 Unity Y 轴的最大摇晃角度；对齐 StackCraft DOPunchRotation(0, 15, 0)。")]
		private float m_hurtPunchRotationDegrees = 15f;

		[SerializeField]
		[LabelText("受击摇晃秒数")]
		[Tooltip("StackCraft 受击 DOPunchRotation 的 duration 参数。")]
		private float m_hurtPunchDurationSeconds = 0.25f;

		[SerializeField]
		[LabelText("受击摇晃振动次数")]
		[Tooltip("StackCraft 受击 DOPunchRotation 的 vibrato 参数。")]
		private int m_hurtPunchVibrato = 25;

		private MaterialPropertyBlock m_propertyBlock;

		private Vector3 m_dragTargetLocalPosition;

		private float m_dragFollowSharpness;

		private bool m_isFollowingDragTarget;

		private bool m_isInteractionHighlighted;

		private float m_presentationHighlightRemainingSeconds;

		private SpriteRenderer[] m_spriteRenderers;

		private Renderer[] m_characterStatusRenderers;

		private CardDefinition m_contentAsset;

		private CharacterCard m_characterCard;

		private int m_displayedPrice = int.MinValue;

		private int m_displayedNutrition = int.MinValue;

		private float m_displayedHealth = float.NaN;

		private float m_displayedMaxHealth = float.NaN;

		private Vector2 m_appliedCardSize = Vector2.one;

		private Vector2 m_unscaledViewFootprint;

		private Vector3 m_unscaledColliderCenter;

		private bool m_hasUnscaledViewFootprint;

		private Quaternion m_hurtBaseRotation = Quaternion.identity;

		private Tween m_moveTween;

		private Tween m_hurtTween;

		private float m_surfaceFlashAmount;

		private Texture2D m_displayedArtwork;

		private MaterialPropertyBlock m_cardBuyerCurrencyIconPropertyBlock;

		public TabletopCard TabletopCard { get; private set; }

		public TabletopCardId CardId => TabletopCard?.Id ?? default;

		public ContentId ContentId => TabletopCard?.ContentId ?? default;

		public bool IsHighlighted => m_highlightRoot != null && m_highlightRoot.activeSelf;

		public bool DisplaysCharacterStatus =>
			m_characterStatusRoot != null && m_characterStatusRoot.activeSelf;

		public bool DisplaysEquipmentPanel =>
			m_equipmentPanelRoot != null && m_equipmentPanelRoot.activeSelf;

		public string DisplayedHealthText => m_healthLabel == null ? string.Empty : m_healthLabel.text;

		public Texture2D DisplayedArtwork => m_displayedArtwork;

		public bool DisplaysArtwork => DisplayedArtwork != null;

		public Material DisplayedSurfaceMaterial =>
			m_surfaceRenderer == null ? null : m_surfaceRenderer.sharedMaterial;

		public string DisplayedTitleText => m_titleLabel == null ? string.Empty : m_titleLabel.text;

		public string DisplayedPriceText => m_priceLabel == null ? string.Empty : m_priceLabel.text;

		public string DisplayedNutritionText => m_nutritionLabel == null ? string.Empty : m_nutritionLabel.text;

		public bool IsHurtFeedbackActive => m_hurtTween != null && m_hurtTween.active;

		public Vector2 AppliedCardSize => m_appliedCardSize;

		/// <summary>当前卡牌表现的基础排序值，供附着在此卡牌上的纯表现元素对齐层级。</summary>
		public int SortingOrder { get; private set; }

		private void Awake()
		{
			ApplyHighlightVisibility();
		}

		public void Bind(TabletopCard tabletopCard, CardDefinition contentAsset)
		{
			if (TabletopCard != null)
			{
				throw new InvalidOperationException("卡牌视图尚未解绑，不能覆盖上一张局内卡牌。");
			}
			if (tabletopCard == null)
			{
				throw new ArgumentNullException("tabletopCard");
			}
			if (contentAsset == null)
			{
				throw new ArgumentNullException("contentAsset");
			}
			if ((string)tabletopCard.ContentId != (string)contentAsset.ContentId)
			{
				throw new ArgumentException("牌桌卡牌和卡牌作者源的内容 ID 不一致，拒绝创建错误投影。", "contentAsset");
			}
			TabletopCard = tabletopCard;
			m_contentAsset = contentAsset;
			base.gameObject.name = "TabletopCard_" + contentAsset.DisplayName;
			m_isInteractionHighlighted = false;
			m_presentationHighlightRemainingSeconds = 0f;
			ApplyHighlightVisibility();
			HideCardBuyerCurrencyIcon();
			ApplyEquipmentPanelVisibility(contentAsset);
			ApplySurfaceMeshForContent(contentAsset);
			RefreshSurfaceText();
			ApplyCardPackInstanceSurface();
			BindCharacterStatus(tabletopCard as CharacterCard);
		}

		private void ApplyEquipmentPanelVisibility(CardDefinition contentAsset)
		{
			bool shouldShowEquipmentPanel = HasExactContentTagCode(
				contentAsset,
				XTag.Card_Category_Character);
			if (m_equipmentPanelRoot == null)
			{
				if (shouldShowEquipmentPanel)
				{
					throw new InvalidOperationException("卡牌视图预制体缺少角色装备面板节点，无法对齐 StackCraft 玩家角色卡。");
				}
				return;
			}

			m_equipmentPanelRoot.SetActive(shouldShowEquipmentPanel);
		}

		private static bool HasExactContentTagCode(ContentAsset contentAsset, int tagCode)
		{
			IReadOnlyList<int> tagCodes = contentAsset.TagCodes;
			for (int tagIndex = 0; tagIndex < tagCodes.Count; tagIndex++)
			{
				if (tagCodes[tagIndex] == tagCode)
				{
					return true;
				}
			}
			return false;
		}

		private void ApplySurfaceMeshForContent(CardDefinition contentAsset)
		{
			if (m_surfaceMeshFilter == null)
			{
				throw new InvalidOperationException("卡牌视图预制体缺少表面 MeshFilter，无法按 StackCraft Card / Pack 独立 FBX 投影。");
			}
			if (m_defaultSurfaceMesh == null)
			{
				throw new InvalidOperationException("卡牌视图预制体缺少 StackCraft Card.fbx 自有副本网格引用。");
			}

			Mesh targetMesh = m_defaultSurfaceMesh;
			Vector3 targetColliderSize = DefaultCardColliderSize;
			if (contentAsset is CardPackDefinition)
			{
				if (m_packSurfaceMesh == null)
				{
					throw new InvalidOperationException("卡牌视图预制体缺少 StackCraft Pack.fbx 自有副本网格引用，不能把卡包显示成普通卡牌。");
				}

				targetMesh = m_packSurfaceMesh;
				targetColliderSize = PackInstanceColliderSize;
			}

			m_surfaceMeshFilter.sharedMesh = targetMesh;
			if (m_highlightMeshFilter != null)
			{
				m_highlightMeshFilter.sharedMesh = targetMesh;
			}

			ApplyUnscaledColliderFootprint(targetColliderSize);
		}

		private void ApplyUnscaledColliderFootprint(Vector3 colliderSize)
		{
			if (!TryGetComponent(out BoxCollider cardCollider))
			{
				throw new InvalidOperationException("卡牌视图预制体缺少 BoxCollider，无法对齐 StackCraft Card / Pack 可点击尺寸。");
			}

			cardCollider.size = colliderSize;
			cardCollider.center = Vector3.zero;
			m_unscaledViewFootprint = new Vector2(colliderSize.x, colliderSize.z);
			m_unscaledColliderCenter = cardCollider.center;
			m_hasUnscaledViewFootprint = true;
		}

		private void BindCharacterStatus(CharacterCard characterCard)
		{
			if (characterCard == null)
			{
				m_characterCard = null;
				m_displayedHealth = float.NaN;
				m_displayedMaxHealth = float.NaN;
				if (m_healthLabel != null)
				{
					m_healthLabel.text = string.Empty;
				}
				if (m_characterStatusRoot != null)
				{
					m_characterStatusRoot.SetActive(false);
				}
				return;
			}
			if (m_characterStatusRoot == null || m_healthLabel == null)
			{
				throw new InvalidOperationException("角色卡视图预制体缺少角色状态节点或生命文本。");
			}

			m_characterCard = characterCard;
			m_characterStatusRoot.SetActive(true);
			RefreshCharacterHealth();
		}

		private void RefreshCharacterHealth()
		{
			if (m_characterCard == null)
			{
				throw new InvalidOperationException("普通卡牌不能刷新角色生命投影。");
			}

			m_displayedHealth = m_characterCard.CurrentHealth;
			m_displayedMaxHealth = m_characterCard.MaxHealth;
			m_healthLabel.text = $"{m_displayedHealth:0}";
		}

		private void RefreshSurfaceText()
		{
			if (TabletopCard == null || m_contentAsset == null)
			{
				return;
			}
			if (m_contentAsset is CardBuyerDefinition)
			{
				ApplyCardBuyerTextSurface();
				return;
			}
			if (m_titleLabel != null)
			{
				m_titleLabel.text = m_contentAsset.DisplayName;
			}
			RefreshPriceText();
			RefreshNutritionText();
		}

		private void RefreshPriceText()
		{
			if (m_priceLabel == null)
			{
				return;
			}

			int price = ResolvePriceValue(TabletopCard, m_contentAsset);
			m_displayedPrice = price;
			m_priceLabel.text = price > 0 ? price.ToString() : string.Empty;
			m_priceLabel.gameObject.SetActive(price > 0);
		}

		private void RefreshNutritionText()
		{
			if (m_nutritionLabel == null)
			{
				return;
			}

			int nutrition = ResolveNutritionValue(m_contentAsset);
			m_displayedNutrition = nutrition;
			m_nutritionLabel.text = nutrition > 0 ? nutrition.ToString() : string.Empty;
			m_nutritionLabel.gameObject.SetActive(nutrition > 0);
		}

		/// <summary>
		/// 用 StackCraft 卡包商贩表面语义覆盖普通卡面文字：标题显示卡包名，底部显示剩余价格，中部显示收藏进度。
		/// </summary>
		public void ApplyPackVendorSurface(
			string offeredPackName,
			int remainingPrice,
			CardPackCollectionProgress progress)
		{
			if (string.IsNullOrWhiteSpace(offeredPackName))
			{
				throw new ArgumentException("卡包商贩表面必须显示被出售卡包的名称。", nameof(offeredPackName));
			}
			if (remainingPrice <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(remainingPrice), remainingPrice, "卡包商贩表面剩余价格必须大于 0。");
			}

			if (m_titleLabel != null)
			{
				ApplyPackVendorTextLayout(
					m_titleLabel,
					PackVendorTitleLocalPosition,
					PackVendorTitleSize,
					PackVendorTitleFontSize,
					VerticalAlignmentOptions.Middle);
				m_titleLabel.text = offeredPackName;
			}
			if (m_priceLabel != null)
			{
				ApplyPackVendorTextLayout(
					m_priceLabel,
					PackVendorPriceLocalPosition,
					PackVendorPriceSize,
					PackVendorPriceFontSize,
					VerticalAlignmentOptions.Middle);
				m_priceLabel.text = "价格：" + remainingPrice;
				m_priceLabel.gameObject.SetActive(true);
			}
			if (m_nutritionLabel != null)
			{
				ApplyPackVendorTextLayout(
					m_nutritionLabel,
					PackVendorTrackerLocalPosition,
					PackVendorTrackerSize,
					PackVendorTrackerFontSize,
					VerticalAlignmentOptions.Top);
				string trackerText = BuildPackVendorTrackerText(progress);
				m_nutritionLabel.text = trackerText;
				m_nutritionLabel.gameObject.SetActive(trackerText.Length > 0);
			}

			m_displayedPrice = remainingPrice;
			m_displayedNutrition = 0;
		}

		/// <summary>
		/// 用 StackCraft CardBuyer 表面语义覆盖普通卡面：根材质显示交易区，标题显示出售，子图标显示货币。
		/// </summary>
		public void ApplyCardBuyerSurface(Texture2D currencyArtwork)
		{
			if (currencyArtwork == null)
			{
				throw new ArgumentNullException(nameof(currencyArtwork));
			}
			if (m_contentAsset is not CardBuyerDefinition)
			{
				throw new InvalidOperationException("只有收购点卡牌才能应用 StackCraft CardBuyer 表面。");
			}
			if (m_cardBuyerCurrencyIconRenderer == null)
			{
				throw new InvalidOperationException("卡牌视图预制体缺少 StackCraft CardBuyer 货币图标渲染器。");
			}
			if (string.IsNullOrWhiteSpace(m_cardBuyerCurrencyTextureProperty))
			{
				throw new InvalidOperationException("卡牌视图缺少 StackCraft CardBuyer 货币图标纹理属性名。");
			}
			ApplyCardBuyerTextSurface();
			if (m_cardBuyerCurrencyIconPropertyBlock == null)
			{
				m_cardBuyerCurrencyIconPropertyBlock = new MaterialPropertyBlock();
			}
			m_cardBuyerCurrencyIconRenderer.GetPropertyBlock(m_cardBuyerCurrencyIconPropertyBlock);
			m_cardBuyerCurrencyIconPropertyBlock.SetTexture(
				Shader.PropertyToID(m_cardBuyerCurrencyTextureProperty),
				currencyArtwork);
			m_cardBuyerCurrencyIconRenderer.SetPropertyBlock(m_cardBuyerCurrencyIconPropertyBlock);
			m_cardBuyerCurrencyIconRenderer.gameObject.SetActive(true);
		}

		private void ApplyCardBuyerTextSurface()
		{
			if (m_titleLabel != null)
			{
				Transform titleTransform = m_titleLabel.transform;
				titleTransform.localPosition = new Vector3(0f, 0f, -0.35f);
				if (titleTransform is RectTransform rectTransform)
				{
					rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
					rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
					rectTransform.pivot = new Vector2(0.5f, 0.5f);
					rectTransform.anchoredPosition = StackCraftTextAnchoredPosition;
					rectTransform.sizeDelta = new Vector2(0.8f, 0.3f);
				}
				m_titleLabel.text = "出售";
				m_titleLabel.fontSize = 1.5f;
				m_titleLabel.enableAutoSizing = false;
				m_titleLabel.alignment = TextAlignmentOptions.Center;
				m_titleLabel.margin = Vector4.zero;
				m_titleLabel.color = Color.white;
			}

			ClearSurfaceText(m_priceLabel);
			ClearSurfaceText(m_nutritionLabel);
			m_displayedPrice = 0;
			m_displayedNutrition = 0;
		}

		private void HideCardBuyerCurrencyIcon()
		{
			if (m_cardBuyerCurrencyIconRenderer != null)
			{
				m_cardBuyerCurrencyIconRenderer.gameObject.SetActive(false);
			}
		}

		private void ApplyCardPackInstanceSurface()
		{
			if (m_contentAsset is not CardPackDefinition)
			{
				return;
			}

			if (m_titleLabel != null)
			{
				Transform titleTransform = m_titleLabel.transform;
				titleTransform.localPosition = PackInstanceTitleLocalPosition;
				if (titleTransform is RectTransform rectTransform)
				{
					rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
					rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
					rectTransform.pivot = new Vector2(0.5f, 0.5f);
					rectTransform.anchoredPosition = StackCraftTextAnchoredPosition;
					rectTransform.sizeDelta = PackInstanceTitleSize;
				}
				m_titleLabel.fontSize = PackInstanceTitleFontSize;
				m_titleLabel.fontSizeMin = PackInstanceTitleMinFontSize;
				m_titleLabel.fontSizeMax = PackInstanceTitleMaxFontSize;
				m_titleLabel.enableAutoSizing = true;
				m_titleLabel.alignment = TextAlignmentOptions.Center;
				m_titleLabel.margin = PackInstanceTitleMargin;
				m_titleLabel.color = PackInstanceTitleColor;
			}

			ClearSurfaceText(m_priceLabel);
			ClearSurfaceText(m_nutritionLabel);
			m_displayedPrice = 0;
			m_displayedNutrition = 0;
		}

		private static void ApplyPackVendorTextLayout(
			TMP_Text label,
			Vector3 localPosition,
			Vector2 size,
			float fontSize,
			VerticalAlignmentOptions verticalAlignment)
		{
			Transform textTransform = label.transform;
			textTransform.localPosition = localPosition;
			if (textTransform is RectTransform rectTransform)
			{
				rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
				rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
				rectTransform.pivot = new Vector2(0.5f, 0.5f);
				rectTransform.anchoredPosition = StackCraftTextAnchoredPosition;
				rectTransform.sizeDelta = size;
			}
			label.fontSize = fontSize;
			label.fontSizeMin = StackCraftVendorTextFontSizeMin;
			label.fontSizeMax = StackCraftVendorTextFontSizeMax;
			label.enableAutoSizing = false;
			label.horizontalAlignment = HorizontalAlignmentOptions.Center;
			label.verticalAlignment = verticalAlignment;
			label.margin = Vector4.zero;
			label.color = Color.white;
		}

		private static void ClearSurfaceText(TMP_Text label)
		{
			if (label == null)
			{
				return;
			}

			label.text = string.Empty;
			label.gameObject.SetActive(false);
		}

		private static string BuildPackVendorTrackerText(CardPackCollectionProgress progress)
		{
			if (progress.TotalCount == 0)
			{
				return string.Empty;
			}
			if (progress.IsComplete)
			{
				return "<color=#FFD700>已完成</color>";
			}
			return "已发现：\n" + progress.DiscoveredCount + "/" + progress.TotalCount;
		}

		private static int ResolvePriceValue(TabletopCard card, CardDefinition contentAsset)
		{
			if (card is PackVendorCard vendorCard)
			{
				return vendorCard.RemainingPrice;
			}
			if (contentAsset is PackVendorDefinition vendorDefinition)
			{
				return vendorDefinition.Price;
			}
			return contentAsset.SellValue;
		}

		private static int ResolveNutritionValue(CardDefinition contentAsset)
		{
			return contentAsset is FoodCardDefinition food ? food.NutritionPerUse : 0;
		}

		private void OnDestroy()
		{
			KillHurtFeedback(resetVisuals: false);
			if (m_characterCard == null)
			{
				return;
			}

			m_characterCard = null;
		}

		public void ApplySize(Vector2 cardSize)
		{
			if (!float.IsFinite(cardSize.x) || !float.IsFinite(cardSize.y) || cardSize.x <= 0f || cardSize.y <= 0f)
			{
				throw new ArgumentException("卡牌视图尺寸必须具有有限坐标和正数宽高。", "cardSize");
			}
			TryGetComponent(out BoxCollider cardCollider);
			Vector2 unscaledViewFootprint = GetUnscaledViewFootprint(cardCollider);
			m_appliedCardSize = cardSize;
			base.transform.localScale = new Vector3(
				cardSize.x / unscaledViewFootprint.x,
				1f,
				cardSize.y / unscaledViewFootprint.y);
			if (cardCollider != null)
			{
				cardCollider.size = new Vector3(unscaledViewFootprint.x, 0f, unscaledViewFootprint.y);
				cardCollider.center = m_unscaledColliderCenter;
			}
			ApplySurfaceLayout();
		}

		private Vector2 GetUnscaledViewFootprint(BoxCollider cardCollider)
		{
			if (m_hasUnscaledViewFootprint)
			{
				return m_unscaledViewFootprint;
			}
			if (cardCollider != null)
			{
				Vector3 colliderSize = cardCollider.size;
				if (float.IsFinite(colliderSize.x) &&
					float.IsFinite(colliderSize.z) &&
					colliderSize.x > 0f &&
					colliderSize.z > 0f)
				{
					m_unscaledViewFootprint = new Vector2(colliderSize.x, colliderSize.z);
					m_unscaledColliderCenter = cardCollider.center;
					m_hasUnscaledViewFootprint = true;
					return m_unscaledViewFootprint;
				}
			}
			if (m_surfaceRenderer != null &&
				m_surfaceRenderer.TryGetComponent(out MeshFilter surfaceMeshFilter) &&
				surfaceMeshFilter.sharedMesh != null)
			{
				Vector3 meshSize = surfaceMeshFilter.sharedMesh.bounds.size;
				if (float.IsFinite(meshSize.x) &&
					float.IsFinite(meshSize.z) &&
					meshSize.x > 0f &&
					meshSize.z > 0f)
				{
					m_unscaledViewFootprint = new Vector2(meshSize.x, meshSize.z);
					m_unscaledColliderCenter = Vector3.zero;
					m_hasUnscaledViewFootprint = true;
					return m_unscaledViewFootprint;
				}
			}
			throw new InvalidOperationException("卡牌视图缺少可回读的未缩放 StackCraft 卡牌本体尺寸，无法把作者源尺寸投影到可见表面。");
		}

		public void ApplyPose(TabletopCardPose pose, float durationSeconds, Ease moveEase)
		{
			m_isFollowingDragTarget = false;
			ApplySortingOrder(pose.SortingOrder);
			if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
			{
				CancelMoveTween();
				base.transform.localPosition = pose.LocalPosition;
				SyncPhysicsTransformsAfterImmediatePose();
				return;
			}

			CancelMoveTween();
			Tween moveTween = transform
				.DOLocalMove(pose.LocalPosition, durationSeconds)
				.SetEase(moveEase)
				.SetUpdate(true)
				.SetTarget(this)
				.SetLink(gameObject, LinkBehaviour.KillOnDisable);
			if (Time.timeScale == 0f)
			{
				moveTween.OnUpdate(Physics.SyncTransforms);
			}
			moveTween.OnKill(() =>
			{
				if (ReferenceEquals(m_moveTween, moveTween))
				{
					m_moveTween = null;
				}
			});
			m_moveTween = moveTween;
		}

		public void ApplyDragPose(TabletopCardPose pose, bool immediate, float followSharpness)
		{
			if (!float.IsFinite(followSharpness) || followSharpness <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(followSharpness),
					"拖拽跟随锐度必须是有限正数。");
			}
			ApplySortingOrder(pose.SortingOrder);
			CancelMoveTween();
			if (immediate)
			{
				m_isFollowingDragTarget = false;
				base.transform.localPosition = pose.LocalPosition;
				SyncPhysicsTransformsAfterImmediatePose();
			}
			else
			{
				m_dragTargetLocalPosition = pose.LocalPosition;
				m_dragFollowSharpness = followSharpness;
				m_isFollowingDragTarget = true;
			}
		}

		public void SetArtwork(Texture2D artwork)
		{
			m_displayedArtwork = artwork;
			if (artwork == null)
			{
				return;
			}
			if (m_surfaceRenderer == null)
			{
				throw new InvalidOperationException("卡牌视图缺少表面渲染器，无法按 StackCraft 材质覆盖层显示卡面插画。");
			}
			if (string.IsNullOrWhiteSpace(m_surfaceTextureProperty))
			{
				throw new InvalidOperationException("卡牌视图缺少 StackCraft 覆盖层纹理属性名。");
			}
			Material material = m_surfaceRenderer.sharedMaterial;
			if (!(material == null) && material.HasProperty(m_surfaceTextureProperty))
			{
				SetSurfaceTexture(m_surfaceTextureProperty, artwork);
				return;
			}
			throw new InvalidOperationException(
				$"卡牌表面材质 {material?.name ?? "(空)"} 不包含 StackCraft 覆盖层纹理属性 {m_surfaceTextureProperty}。");
		}

		public void SetSurfaceMaterial(Material surfaceMaterial)
		{
			if (surfaceMaterial == null)
			{
				throw new ArgumentNullException(nameof(surfaceMaterial));
			}
			if (m_surfaceRenderer == null)
			{
				throw new InvalidOperationException("卡牌视图缺少表面渲染器，无法应用 StackCraft 卡面材质。");
			}
			if (m_surfaceRenderer is SpriteRenderer)
			{
				throw new InvalidOperationException("StackCraft 卡面复刻必须使用 MeshRenderer 材质链，不能把表面材质应用到 SpriteRenderer。");
			}

			m_surfaceRenderer.sharedMaterial = surfaceMaterial;
			if (m_displayedArtwork != null)
			{
				SetArtwork(m_displayedArtwork);
			}
			SetSurfaceFlash(0f);
			ApplySurfaceLayout();
		}

		private void ApplySurfaceLayout()
		{
			if (m_surfaceRenderer == null)
			{
				return;
			}
			if (m_surfaceRenderer is SpriteRenderer)
			{
				throw new InvalidOperationException("StackCraft 卡面复刻不能使用 SpriteRenderer 缩放模拟卡牌底板。");
			}
			if (m_surfaceRenderer.transform == transform)
			{
				return;
			}
			m_surfaceRenderer.transform.localScale = Vector3.one;
			m_surfaceRenderer.transform.localPosition = Vector3.zero;
		}

		public void SetHighlighted(bool highlighted)
		{
			m_isInteractionHighlighted = highlighted;
			ApplyHighlightVisibility();
		}

		public void ShowPresentationHighlight(float durationSeconds)
		{
			if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(durationSeconds),
					durationSeconds,
					"卡牌提示高亮持续时间必须是大于 0 的有限秒数。");
			}
			m_presentationHighlightRemainingSeconds = Math.Max(
				m_presentationHighlightRemainingSeconds,
				durationSeconds);
			ApplyHighlightVisibility();
		}

		private void ApplyHighlightVisibility()
		{
			if (m_highlightRoot != null)
			{
				m_highlightRoot.SetActive(
					m_isInteractionHighlighted || m_presentationHighlightRemainingSeconds > 0f);
			}
		}

		private static void SyncPhysicsTransformsAfterImmediatePose()
		{
			// 当前项目关闭了 Physics auto sync；即时写入卡牌 Transform 后，后续射线和吸附必须命中当前画面位置。
			Physics.SyncTransforms();
		}

		public void PlayHurtFeedback()
		{
			if (!float.IsFinite(m_hurtFlashDelaySeconds) || m_hurtFlashDelaySeconds < 0f)
			{
				throw new InvalidOperationException("受击闪白延迟必须是大于等于 0 的有限值。");
			}
			if (!float.IsFinite(m_hurtFlashTweenSeconds) || m_hurtFlashTweenSeconds <= 0f)
			{
				throw new InvalidOperationException("受击闪白单段秒数必须是大于 0 的有限值。");
			}
			if (m_hurtFlashLoopCount <= 0)
			{
				throw new InvalidOperationException("受击闪白循环次数必须是正整数。");
			}
			if (!float.IsFinite(m_hurtPunchRotationDegrees) || m_hurtPunchRotationDegrees < 0f)
			{
				throw new InvalidOperationException("受击摇晃角度必须是大于等于 0 的有限值。");
			}
			if (!float.IsFinite(m_hurtPunchDurationSeconds) || m_hurtPunchDurationSeconds <= 0f)
			{
				throw new InvalidOperationException("受击摇晃秒数必须是大于 0 的有限值。");
			}
			if (m_hurtPunchVibrato <= 0)
			{
				throw new InvalidOperationException("受击摇晃振动次数必须是正整数。");
			}

			KillHurtFeedback(resetVisuals: false);
			m_hurtBaseRotation = transform.localRotation;
			Tween flashTween = DOTween.To(
					() => m_surfaceFlashAmount,
					SetSurfaceFlash,
					1f,
					m_hurtFlashTweenSeconds)
				.SetDelay(m_hurtFlashDelaySeconds)
				.SetLoops(m_hurtFlashLoopCount, LoopType.Yoyo)
				.SetTarget(this);
			Tween shakeTween = transform.DOPunchRotation(
				new Vector3(0f, m_hurtPunchRotationDegrees, 0f),
				m_hurtPunchDurationSeconds,
				m_hurtPunchVibrato);
			Sequence hurtSequence = DOTween.Sequence();
			hurtSequence.Join(flashTween)
				.Join(shakeTween)
				.SetUpdate(true)
				.SetLink(gameObject, LinkBehaviour.KillOnDisable);
			hurtSequence.OnKill(() =>
			{
				if (ReferenceEquals(m_hurtTween, hurtSequence))
				{
					m_hurtTween = null;
				}
			});
			m_hurtTween = hurtSequence;
		}

		private void Update()
		{
			if (m_characterCard != null &&
				(!Mathf.Approximately(m_displayedHealth, m_characterCard.CurrentHealth) ||
				 !Mathf.Approximately(m_displayedMaxHealth, m_characterCard.MaxHealth)))
			{
				RefreshCharacterHealth();
			}
			if (TabletopCard != null &&
				m_contentAsset != null &&
				(m_displayedPrice != ResolvePriceValue(TabletopCard, m_contentAsset) ||
				 m_displayedNutrition != ResolveNutritionValue(m_contentAsset)))
			{
				RefreshSurfaceText();
			}
			if (m_isFollowingDragTarget)
			{
				float interpolation = 1f - Mathf.Exp((0f - m_dragFollowSharpness) * Time.unscaledDeltaTime);
				base.transform.localPosition = Vector3.LerpUnclamped(
					base.transform.localPosition,
					m_dragTargetLocalPosition,
					interpolation);
				if ((base.transform.localPosition - m_dragTargetLocalPosition).sqrMagnitude < 0.0001f)
				{
					base.transform.localPosition = m_dragTargetLocalPosition;
					m_isFollowingDragTarget = false;
				}
			}
			if (m_presentationHighlightRemainingSeconds > 0f)
			{
				m_presentationHighlightRemainingSeconds -= Time.unscaledDeltaTime;
				if (m_presentationHighlightRemainingSeconds <= 0f)
				{
					m_presentationHighlightRemainingSeconds = 0f;
					ApplyHighlightVisibility();
				}
			}
		}

		private void ApplySortingOrder(int sortingOrder)
		{
			SortingOrder = sortingOrder;
			if (m_spriteRenderers == null)
			{
				m_spriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
			}
			for (int i = 0; i < m_spriteRenderers.Length; i++)
			{
				m_spriteRenderers[i].sortingOrder = sortingOrder;
			}
			if (m_surfaceRenderer != null)
			{
				m_surfaceRenderer.sortingOrder = sortingOrder;
			}
			if (m_characterStatusRoot != null)
			{
				m_characterStatusRenderers ??=
					m_characterStatusRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < m_characterStatusRenderers.Length; i++)
				{
					if (m_characterStatusRenderers[i] != null)
					{
						m_characterStatusRenderers[i].sortingOrder = sortingOrder;
					}
				}
			}
			if (m_cardBuyerCurrencyIconRenderer != null)
			{
				m_cardBuyerCurrencyIconRenderer.sortingOrder = sortingOrder;
			}
			if (m_healthLabel != null)
			{
				m_healthLabel.GetComponent<Renderer>().sortingOrder = sortingOrder;
			}
			if (m_titleLabel != null)
			{
				m_titleLabel.GetComponent<Renderer>().sortingOrder = sortingOrder;
			}
			if (m_priceLabel != null)
			{
				m_priceLabel.GetComponent<Renderer>().sortingOrder = sortingOrder;
			}
			if (m_nutritionLabel != null)
			{
				m_nutritionLabel.GetComponent<Renderer>().sortingOrder = sortingOrder;
			}
		}

		private void CancelMoveTween()
		{
			if (m_moveTween != null)
			{
				m_moveTween.Kill();
				m_moveTween = null;
			}
		}

		public void SetCharacterStatusVisible(bool visible)
		{
			if (m_characterStatusRoot != null)
			{
				m_characterStatusRoot.SetActive(m_characterCard != null && visible);
			}
		}

		private void KillHurtFeedback(bool resetVisuals)
		{
			if (m_hurtTween != null)
			{
				m_hurtTween.Kill();
				m_hurtTween = null;
			}
			if (!resetVisuals)
			{
				return;
			}
			transform.localRotation = m_hurtBaseRotation;
			SetSurfaceFlash(0f);
		}

		private void SetSurfaceTexture(string propertyName, Texture texture)
		{
			if (m_propertyBlock == null)
			{
				m_propertyBlock = new MaterialPropertyBlock();
			}
			m_surfaceRenderer.GetPropertyBlock(m_propertyBlock);
			m_propertyBlock.SetTexture(Shader.PropertyToID(propertyName), texture);
			m_surfaceRenderer.SetPropertyBlock(m_propertyBlock);
		}

		private void SetSurfaceFlash(float amount)
		{
			m_surfaceFlashAmount = amount;
			if (m_surfaceRenderer == null || string.IsNullOrWhiteSpace(m_surfaceFlashProperty))
			{
				return;
			}
			Material material = m_surfaceRenderer.sharedMaterial;
			if (material == null || !material.HasProperty(m_surfaceFlashProperty))
			{
				return;
			}
			if (m_propertyBlock == null)
			{
				m_propertyBlock = new MaterialPropertyBlock();
			}
			m_surfaceRenderer.GetPropertyBlock(m_propertyBlock);
			m_propertyBlock.SetFloat(Shader.PropertyToID(m_surfaceFlashProperty), amount);
			m_surfaceRenderer.SetPropertyBlock(m_propertyBlock);
		}

	}
}
