using System;
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
		[LabelText("图片组件")]
		[Tooltip("可选的 SpriteRenderer。配置后会显示卡牌作者源选择的卡面图片。")]
		private SpriteRenderer m_artworkRenderer;

		[SerializeField]
		[LabelText("表面渲染器")]
		[Tooltip("可选的卡面 Renderer。配置纹理属性后，会将内容图片写入材质属性块，不复制材质实例。")]
		private Renderer m_surfaceRenderer;

		[SerializeField]
		[LabelText("表面纹理属性")]
		[Tooltip("表面材质用于接收卡面纹理的 Shader 属性名；留空表示不向表面材质写入纹理。")]
		private string m_surfaceTextureProperty = string.Empty;

		[SerializeField]
		[LabelText("高亮节点")]
		[Tooltip("可选的高亮子节点。拖拽命中空间候选时只切换其显隐，不创建材质或规则状态。")]
		private GameObject m_highlightRoot;

		[SerializeField]
		[LabelText("角色状态节点")]
		[Tooltip("角色卡显示当前生命时启用；普通卡牌保持隐藏。")]
		private GameObject m_characterStatusRoot;

		[SerializeField]
		[LabelText("生命文本")]
		[Tooltip("直接显示角色唯一 EX-GAS Health/MaxHealth 当前值。")]
		private TMP_Text m_healthLabel;

		private MaterialPropertyBlock m_propertyBlock;

		private Vector3 m_dragTargetLocalPosition;

		private float m_dragFollowSharpness;

		private bool m_isFollowingDragTarget;

		private SpriteRenderer[] m_spriteRenderers;

		private CharacterCard m_characterCard;

		private float m_displayedHealth = float.NaN;

		private float m_displayedMaxHealth = float.NaN;

		public TabletopCard TabletopCard { get; private set; }

		public TabletopCardId CardId => TabletopCard?.Id ?? default;

		public ContentId ContentId => TabletopCard?.ContentId ?? default;

		public bool IsHighlighted => m_highlightRoot != null && m_highlightRoot.activeSelf;

		public bool DisplaysCharacterStatus =>
			m_characterStatusRoot != null && m_characterStatusRoot.activeSelf;

		public string DisplayedHealthText => m_healthLabel == null ? string.Empty : m_healthLabel.text;

		/// <summary>当前卡牌表现的基础排序值，供附着在此卡牌上的纯表现元素对齐层级。</summary>
		public int SortingOrder { get; private set; }

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
			base.gameObject.name = "TabletopCard_" + contentAsset.DisplayName;
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
			m_healthLabel.text = $"{m_displayedHealth:0}/{m_displayedMaxHealth:0}";
		}

		private void OnDestroy()
		{
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
			base.transform.localScale = new Vector3(cardSize.x, cardSize.y, 1f);
		}

		public void ApplyPose(TabletopCardPose pose)
		{
			m_isFollowingDragTarget = false;
			base.transform.localPosition = pose.LocalPosition;
			ApplySortingOrder(pose.SortingOrder);
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
			if (m_artworkRenderer != null)
			{
				m_artworkRenderer.sprite = artwork;
			}
			if (m_surfaceRenderer == null || string.IsNullOrWhiteSpace(m_surfaceTextureProperty))
			{
				return;
			}
			Material material = m_surfaceRenderer.sharedMaterial;
			if (!(material == null) && material.HasProperty(m_surfaceTextureProperty))
			{
				if (m_propertyBlock == null)
				{
					m_propertyBlock = new MaterialPropertyBlock();
				}
				m_surfaceRenderer.GetPropertyBlock(m_propertyBlock);
				m_propertyBlock.SetTexture(Shader.PropertyToID(m_surfaceTextureProperty), (artwork == null) ? null : artwork.texture);
				m_surfaceRenderer.SetPropertyBlock(m_propertyBlock);
			}
		}

		public void SetHighlighted(bool highlighted)
		{
			if (m_highlightRoot != null)
			{
				m_highlightRoot.SetActive(highlighted);
			}
		}

		private void Update()
		{
			if (m_characterCard != null &&
				(!Mathf.Approximately(m_displayedHealth, m_characterCard.CurrentHealth) ||
				 !Mathf.Approximately(m_displayedMaxHealth, m_characterCard.MaxHealth)))
			{
				RefreshCharacterHealth();
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
			if (m_healthLabel != null)
			{
				m_healthLabel.GetComponent<Renderer>().sortingOrder = sortingOrder + 1;
			}
		}

		public void SetCharacterStatusVisible(bool visible)
		{
			if (m_characterStatusRoot != null)
			{
				m_characterStatusRoot.SetActive(m_characterCard != null && visible);
			}
		}
	}
}
