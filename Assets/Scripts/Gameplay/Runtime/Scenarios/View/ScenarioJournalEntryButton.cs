using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 剧本日志列表中的文字按钮，承接 StackCraft 菜单条目的悬浮下划线和信息请求。
	/// </summary>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(TMP_Text))]
	public sealed class ScenarioJournalEntryButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		private TMP_Text m_label;
		private Action<bool> m_hoverChanged;

		public string Text => Label.text;

		private TMP_Text Label
		{
			get
			{
				if (m_label == null)
				{
					m_label = GetComponent<TMP_Text>();
				}
				return m_label;
			}
		}

		public void Initialize(
			string text,
			float fontSize,
			Color color,
			Action<bool> hoverChanged)
		{
			Label.text = text;
			Label.fontSize = fontSize;
			Label.fontStyle = FontStyles.Normal;
			Label.color = color;
			Label.raycastTarget = true;
			m_hoverChanged = hoverChanged;
			gameObject.SetActive(true);
		}

		public void SetText(string text)
		{
			Label.text = text;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			Label.fontStyle = FontStyles.Underline;
			m_hoverChanged?.Invoke(true);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			ClearHover();
		}

		private void OnDisable()
		{
			ClearHover();
		}

		private void ClearHover()
		{
			Label.fontStyle = FontStyles.Normal;
			m_hoverChanged?.Invoke(false);
		}
	}
}
