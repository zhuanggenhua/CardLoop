using System;
using DG.Tweening;
using GameCore;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 牌桌自动战斗的独立命中结果浮动 UI，只显示命中类型、伤害数字和克制图标。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class TabletopHitResultView : MonoBehaviour
	{
		[SerializeField]
		[LabelText("命中图片")]
		[Tooltip("对齐 参考模板命中图片：显示 Miss / Normal / Critical。")]
		private Image m_hitImage;

		[SerializeField]
		[LabelText("克制图片")]
		[Tooltip("对齐 参考模板克制图片：只在优势或劣势时显示。")]
		private Image m_effectivenessImage;

		[SerializeField]
		[LabelText("伤害文本")]
		[Tooltip("对齐 参考模板伤害文本：Miss 时为空，命中时显示伤害数字。")]
		private TextMeshProUGUI m_damageLabel;

		[SerializeField]
		[LabelText("未命中图片")]
		private Sprite m_missSprite;

		[SerializeField]
		[LabelText("普通命中图片")]
		private Sprite m_normalSprite;

		[SerializeField]
		[LabelText("暴击图片")]
		private Sprite m_criticalSprite;

		[SerializeField]
		[LabelText("优势图片")]
		private Sprite m_advantageSprite;

		[SerializeField]
		[LabelText("劣势图片")]
		private Sprite m_disadvantageSprite;

		[SerializeField]
		[LabelText("弹跳幅度")]
		[Tooltip("参考模板命中结果使用 DOPunchScale(new Vector3(0.15, 0.15), 1)。")]
		private float m_punchScale = 0.15f;

		[SerializeField]
		[LabelText("弹跳秒数")]
		[Tooltip("参考模板命中结果的 DOPunchScale 持续 1 秒，完成后销毁；本项目播放完成后由 TabletopView 释放实例。")]
		private float m_punchDurationSeconds = 1f;

		private bool m_isPlaying;
		private Vector3 m_baseScale = Vector3.one;
		private Tween m_punchTween;

		public bool IsPlaying => m_isPlaying;

		public string DisplayedDamageText => m_damageLabel == null ? string.Empty : m_damageLabel.text;

		public Sprite DisplayedHitSprite => m_hitImage == null ? null : m_hitImage.sprite;

		public bool DisplaysEffectivenessIcon =>
			m_effectivenessImage != null &&
			m_effectivenessImage.enabled &&
			m_effectivenessImage.gameObject.activeInHierarchy;

		public Sprite DisplayedEffectivenessSprite =>
			m_effectivenessImage == null ? null : m_effectivenessImage.sprite;

		public Vector3 DisplayedScale => transform.localScale;

		internal void Play(
			int appliedDamage,
			bool isMissed,
			bool isCriticalHit,
			DamageMatchupResult matchupResult,
			Vector3 localPosition,
			int sortingOrder)
		{
			if (appliedDamage < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(appliedDamage),
					appliedDamage,
					"命中结果显示的实际伤害不能为负数。");
			}
			if (m_hitImage == null || m_effectivenessImage == null || m_damageLabel == null)
			{
				throw new InvalidOperationException("命中结果视图缺少 参考模板对应的图片或伤害文本引用。");
			}
			if (!float.IsFinite(m_punchScale) || m_punchScale < 0f)
			{
				throw new InvalidOperationException("命中结果弹跳幅度必须是大于等于 0 的有限值。");
			}
			if (!float.IsFinite(m_punchDurationSeconds) || m_punchDurationSeconds <= 0f)
			{
				throw new InvalidOperationException("命中结果弹跳秒数必须是大于 0 的有限值。");
			}

			transform.localPosition = localPosition;
			transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
			m_baseScale = Vector3.one;
			transform.localScale = m_baseScale;
			ApplyCanvasSortingOrder(sortingOrder);
			ApplyHitResult(isMissed, isCriticalHit, appliedDamage);
			ApplyEffectiveness(matchupResult);
			gameObject.SetActive(true);
			PlayPunchTween();
		}

		private void ApplyHitResult(bool isMissed, bool isCriticalHit, int appliedDamage)
		{
			if (isMissed)
			{
				m_hitImage.sprite = RequireSprite(m_missSprite, "未命中图片");
				m_damageLabel.text = string.Empty;
				return;
			}

			m_hitImage.sprite = isCriticalHit
				? RequireSprite(m_criticalSprite, "暴击图片")
				: RequireSprite(m_normalSprite, "普通命中图片");
			m_damageLabel.text = appliedDamage.ToString();
			m_damageLabel.color = Color.white;
		}

		private void ApplyEffectiveness(DamageMatchupResult matchupResult)
		{
			Sprite sprite = matchupResult switch
			{
				DamageMatchupResult.Advantage => RequireSprite(m_advantageSprite, "优势图片"),
				DamageMatchupResult.Disadvantage => RequireSprite(m_disadvantageSprite, "劣势图片"),
				DamageMatchupResult.None => null,
				_ => throw new ArgumentOutOfRangeException(
					nameof(matchupResult),
					matchupResult,
					"未知的战斗克制表现结果。")
			};

			m_effectivenessImage.sprite = sprite;
			m_effectivenessImage.enabled = sprite != null;
		}

		private void ApplyCanvasSortingOrder(int sortingOrder)
		{
			if (TryGetComponent(out Canvas canvas))
			{
				canvas.renderMode = RenderMode.WorldSpace;
				canvas.overrideSorting = true;
				canvas.sortingOrder = sortingOrder;
			}
		}

		private static Sprite RequireSprite(Sprite sprite, string description)
		{
			if (sprite == null)
			{
				throw new InvalidOperationException($"命中结果视图缺少 {description}。");
			}
			return sprite;
		}

		private void PlayPunchTween()
		{
			if (m_punchTween != null)
			{
				m_punchTween.Kill();
				m_punchTween = null;
			}

			m_isPlaying = true;
			Tween punchTween = null;
			punchTween = transform
				.DOPunchScale(new Vector3(m_punchScale, m_punchScale, 0f), m_punchDurationSeconds)
				.SetUpdate(true)
				.SetTarget(this)
				.SetLink(gameObject, LinkBehaviour.KillOnDisable)
				.OnComplete(() =>
				{
					m_isPlaying = false;
					gameObject.SetActive(false);
				})
				.OnKill(() =>
				{
					if (!ReferenceEquals(m_punchTween, punchTween))
					{
						return;
					}
					m_punchTween = null;
				});
			m_punchTween = punchTween;
		}

		private void OnDestroy()
		{
			if (m_punchTween != null)
			{
				m_punchTween.Kill();
				m_punchTween = null;
				m_isPlaying = false;
			}
		}
	}
}
