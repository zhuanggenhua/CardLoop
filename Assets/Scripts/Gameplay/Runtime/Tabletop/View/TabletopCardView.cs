using System;
using DG.Tweening;
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
		[Header("表现组件")]
		[SerializeField]
		[LabelText("表面渲染器")]
		[Tooltip("卡牌表面的 MeshRenderer。StackCraft 卡面由同一个材质承载底板、插画覆盖层、颜色和受击闪光。")]
		private Renderer m_surfaceRenderer;

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
		[LabelText("使用次数文本")]
		[Tooltip("显示当前剩余使用次数；一次性卡牌不显示，避免无意义噪声。")]
		private TMP_Text m_usesLabel;

		[SerializeField]
		[LabelText("角色状态节点")]
		[Tooltip("角色卡显示当前生命时启用；普通卡牌保持隐藏。")]
		private GameObject m_characterStatusRoot;

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

		private CardDefinition m_contentAsset;

		private CharacterCard m_characterCard;

		private int m_displayedRemainingUses = -1;

		private float m_displayedHealth = float.NaN;

		private float m_displayedMaxHealth = float.NaN;

		private Vector2 m_appliedCardSize = Vector2.one;

		private Quaternion m_hurtBaseRotation = Quaternion.identity;

		private Tween m_moveTween;

		private Tween m_hurtTween;

		private float m_surfaceFlashAmount;

		private Sprite m_displayedArtwork;

		public TabletopCard TabletopCard { get; private set; }

		public TabletopCardId CardId => TabletopCard?.Id ?? default;

		public ContentId ContentId => TabletopCard?.ContentId ?? default;

		public bool IsHighlighted => m_highlightRoot != null && m_highlightRoot.activeSelf;

		public bool DisplaysCharacterStatus =>
			m_characterStatusRoot != null && m_characterStatusRoot.activeSelf;

		public string DisplayedHealthText => m_healthLabel == null ? string.Empty : m_healthLabel.text;

		public Sprite DisplayedArtwork => m_displayedArtwork;

		public bool DisplaysArtwork => DisplayedArtwork != null;

		public Material DisplayedSurfaceMaterial =>
			m_surfaceRenderer == null ? null : m_surfaceRenderer.sharedMaterial;

		public string DisplayedTitleText => m_titleLabel == null ? string.Empty : m_titleLabel.text;

		public string DisplayedPriceText => m_priceLabel == null ? string.Empty : m_priceLabel.text;

		public string DisplayedNutritionText => m_nutritionLabel == null ? string.Empty : m_nutritionLabel.text;

		public string DisplayedUsesText => m_usesLabel == null ? string.Empty : m_usesLabel.text;

		public bool IsHurtFeedbackActive => m_hurtTween != null && m_hurtTween.active;

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
			RefreshSurfaceText();
			BindCharacterStatus(tabletopCard as CharacterCard);
		}

		private void BindCharacterStatus(CharacterCard characterCard)
		{
			if (characterCard == null)
			{
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
			if (m_titleLabel != null)
			{
				m_titleLabel.text = m_contentAsset.DisplayName;
			}
			RefreshPriceText();
			RefreshNutritionText();
			if (m_usesLabel != null)
			{
				m_usesLabel.text = TabletopCard.RemainingUses > 1
					? "x" + TabletopCard.RemainingUses.ToString()
					: string.Empty;
			}
			m_displayedRemainingUses = TabletopCard.RemainingUses;
		}

		private void RefreshPriceText()
		{
			if (m_priceLabel == null)
			{
				return;
			}

			int price = ResolvePriceValue(TabletopCard, m_contentAsset);
			m_priceLabel.text = price > 0 ? price.ToString() : string.Empty;
			m_priceLabel.gameObject.SetActive(price > 0);
		}

		private void RefreshNutritionText()
		{
			if (m_nutritionLabel == null)
			{
				return;
			}

			int nutrition = m_contentAsset is FoodCardDefinition food
				? food.NutritionPerUse
				: 0;
			m_nutritionLabel.text = nutrition > 0 ? nutrition.ToString() : string.Empty;
			m_nutritionLabel.gameObject.SetActive(nutrition > 0);
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
			m_appliedCardSize = cardSize;
			base.transform.localScale = Vector3.one;
			if (TryGetComponent(out BoxCollider cardCollider))
			{
				cardCollider.size = new Vector3(cardSize.x, 0f, cardSize.y);
				cardCollider.center = Vector3.zero;
			}
			ApplySurfaceLayout();
		}

		public void ApplyPose(TabletopCardPose pose, float durationSeconds)
		{
			m_isFollowingDragTarget = false;
			ApplySortingOrder(pose.SortingOrder);
			if (!float.IsFinite(durationSeconds) || durationSeconds <= 0f)
			{
				CancelMoveTween();
				base.transform.localPosition = pose.LocalPosition;
				return;
			}

			CancelMoveTween();
			Tween moveTween = transform
				.DOLocalMove(pose.LocalPosition, durationSeconds)
				.SetEase(Ease.OutQuad)
				.SetUpdate(true)
				.SetTarget(this)
				.SetLink(gameObject, LinkBehaviour.KillOnDisable);
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
			}
			else
			{
				m_dragTargetLocalPosition = pose.LocalPosition;
				m_dragFollowSharpness = followSharpness;
				m_isFollowingDragTarget = true;
			}
		}

		public void SetArtwork(Sprite artwork)
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
				SetSurfaceTexture(m_surfaceTextureProperty, artwork.texture);
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
			if (TabletopCard != null && m_displayedRemainingUses != TabletopCard.RemainingUses)
			{
				RefreshSurfaceText();
			}
			if (m_isFollowingDragTarget)
			{
				float interpolation = 1f - Mathf.Exp((0f - m_dragFollowSharpness) * Time.unscaledDeltaTime);
				base.transform.localPosition = Vector3.Lerp(base.transform.localPosition, m_dragTargetLocalPosition, interpolation);
				if ((base.transform.localPosition - m_dragTargetLocalPosition).sqrMagnitude <= 1E-06f)
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
			if (m_healthLabel != null)
			{
				m_healthLabel.GetComponent<Renderer>().sortingOrder = sortingOrder + 1;
			}
			if (m_titleLabel != null)
			{
				m_titleLabel.GetComponent<Renderer>().sortingOrder = sortingOrder + 2;
			}
			if (m_priceLabel != null)
			{
				m_priceLabel.GetComponent<Renderer>().sortingOrder = sortingOrder + 2;
			}
			if (m_nutritionLabel != null)
			{
				m_nutritionLabel.GetComponent<Renderer>().sortingOrder = sortingOrder + 2;
			}
			if (m_usesLabel != null)
			{
				m_usesLabel.GetComponent<Renderer>().sortingOrder = sortingOrder + 2;
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
