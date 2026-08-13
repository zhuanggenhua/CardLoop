using System;
using System.Collections.Generic;
using Gameplay.Actions;
using UnityEditor;
using UnityEngine;

namespace Gameplay.Editor.Actions
{
	/// <summary>
	/// 把行动内部槽位键绘制为当前行动的槽位选择器；显示名来自槽位定义，序列化仍只保存稳定键。
	/// </summary>
	[CustomPropertyDrawer(typeof(ActionSlotReferenceAttribute))]
	public sealed class ActionSlotReferenceDrawer : PropertyDrawer
	{
		private const float HelpSpacing = 2f;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			ActionDefinition action = ResolveAction(property);
			SlotOptions options = BuildOptions(property, action);
			Rect fieldRect = new Rect(
				position.x,
				position.y,
				position.width,
				EditorGUIUtility.singleLineHeight);

			using (new EditorGUI.DisabledScope(!options.CanSelect))
			{
				EditorGUI.BeginChangeCheck();
				int selectedIndex = EditorGUI.Popup(
					fieldRect,
					label,
					options.SelectedIndex,
					ToGuiContent(options.Labels));
				if (EditorGUI.EndChangeCheck())
				{
					AssignReference(property, action, options.Keys[selectedIndex]);
				}
			}

			if (!string.IsNullOrEmpty(options.ErrorMessage))
			{
				Rect helpRect = new Rect(
					position.x,
					fieldRect.yMax + HelpSpacing,
					position.width,
					EditorGUIUtility.singleLineHeight * 2f);
				EditorGUI.HelpBox(helpRect, options.ErrorMessage, MessageType.Error);
			}
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			SlotOptions options = BuildOptions(property, ResolveAction(property));
			return string.IsNullOrEmpty(options.ErrorMessage)
				? EditorGUIUtility.singleLineHeight
				: EditorGUIUtility.singleLineHeight * 3f + HelpSpacing;
		}

		/// <summary>
		/// 把选择结果写入既有字符串字段。空值只代表单槽位自动推导，其它值必须精确命中一个现有槽位。
		/// </summary>
		internal static void AssignReference(
			SerializedProperty slotKeyProperty,
			ActionDefinition action,
			string selectedKey)
		{
			if (slotKeyProperty == null || slotKeyProperty.propertyType != SerializedPropertyType.String)
			{
				throw new ArgumentException("行动槽位引用必须写入字符串字段。", nameof(slotKeyProperty));
			}
			if (action == null)
			{
				throw new ArgumentNullException(nameof(action));
			}

			IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
			if (string.IsNullOrWhiteSpace(selectedKey))
			{
				if (slots.Count != 1)
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 有 {slots.Count} 个参与槽位，不能自动推导结果槽位。");
				}
				slotKeyProperty.stringValue = string.Empty;
				return;
			}

			ActionSlotDefinition selectedSlot = null;
			for (int i = 0; i < slots.Count; i++)
			{
				ActionSlotDefinition slot = slots[i];
				if (slot == null || !StringComparer.Ordinal.Equals(slot.Key, selectedKey))
				{
					continue;
				}
				if (selectedSlot != null)
				{
					throw new InvalidOperationException(
						$"行动 {action.ContentId} 的参与槽位键重复：{selectedKey}。");
				}
				selectedSlot = slot;
			}

			if (selectedSlot == null)
			{
				throw new InvalidOperationException(
					$"行动 {action.ContentId} 不存在参与槽位：{selectedKey}。");
			}
			slotKeyProperty.stringValue = selectedSlot.Key;
		}

		private static ActionDefinition ResolveAction(SerializedProperty property)
		{
			if (property?.serializedObject == null || property.serializedObject.isEditingMultipleObjects)
			{
				return null;
			}
			return property.serializedObject.targetObject as ActionDefinition;
		}

		private static SlotOptions BuildOptions(
			SerializedProperty property,
			ActionDefinition action)
		{
			if (property == null || property.propertyType != SerializedPropertyType.String)
			{
				return SlotOptions.Invalid("行动槽位引用只能标记字符串字段。");
			}
			if (property.serializedObject.isEditingMultipleObjects)
			{
				return SlotOptions.Invalid("不能同时编辑多个行动的槽位引用，请单独选择一个行动资产。");
			}
			if (action == null)
			{
				return SlotOptions.Invalid("行动槽位引用必须属于 ActionDefinition 作者资产。");
			}

			IReadOnlyList<ActionSlotDefinition> slots = action.ParticipationSlots;
			if (slots.Count == 0)
			{
				return SlotOptions.Invalid("当前行动没有可引用的参与槽位。");
			}

			HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < slots.Count; i++)
			{
				ActionSlotDefinition slot = slots[i];
				if (slot == null || string.IsNullOrWhiteSpace(slot.Key))
				{
					return SlotOptions.Invalid("当前行动存在尚未生成内部键的参与槽位，请先完成行动资产校验。");
				}
				if (!seenKeys.Add(slot.Key))
				{
					return SlotOptions.Invalid($"当前行动的参与槽位键重复：{slot.Key}。");
				}
			}

			List<string> labels = new List<string>(slots.Count + 1);
			List<string> keys = new List<string>(slots.Count + 1);
			if (slots.Count == 1)
			{
				labels.Add($"自动推导（唯一槽位：{slots[0].DisplayName}）");
				keys.Add(string.Empty);
			}
			for (int i = 0; i < slots.Count; i++)
			{
				labels.Add($"{slots[i].DisplayName}（槽位 {i + 1}）");
				keys.Add(slots[i].Key);
			}

			string currentKey = string.IsNullOrWhiteSpace(property.stringValue)
				? string.Empty
				: property.stringValue;
			int selectedIndex;
			string errorMessage = null;
			if (slots.Count > 1 && string.IsNullOrEmpty(currentKey))
			{
				labels.Insert(0, "未指定（配置错误）");
				keys.Insert(0, string.Empty);
				selectedIndex = 0;
				errorMessage = "多槽位行动必须明确选择结果所引用的参与槽位。";
			}
			else
			{
				selectedIndex = FindKeyIndex(keys, currentKey);
				if (selectedIndex < 0)
				{
					labels.Insert(0, $"不存在的槽位：{currentKey}");
					keys.Insert(0, currentKey);
					selectedIndex = 0;
					errorMessage = $"当前引用的参与槽位不存在：{currentKey}。";
				}
			}

			return new SlotOptions(labels.ToArray(), keys.ToArray(), selectedIndex, errorMessage);
		}

		private static int FindKeyIndex(IReadOnlyList<string> keys, string key)
		{
			for (int i = 0; i < keys.Count; i++)
			{
				if (StringComparer.Ordinal.Equals(keys[i], key))
				{
					return i;
				}
			}
			return -1;
		}

		private static GUIContent[] ToGuiContent(IReadOnlyList<string> labels)
		{
			GUIContent[] contents = new GUIContent[labels.Count];
			for (int i = 0; i < labels.Count; i++)
			{
				contents[i] = new GUIContent(labels[i]);
			}
			return contents;
		}

		private readonly struct SlotOptions
		{
			internal string[] Labels { get; }
			internal string[] Keys { get; }
			internal int SelectedIndex { get; }
			internal string ErrorMessage { get; }
			internal bool CanSelect => Keys.Length > 0;

			internal SlotOptions(
				string[] labels,
				string[] keys,
				int selectedIndex,
				string errorMessage)
			{
				Labels = labels;
				Keys = keys;
				SelectedIndex = selectedIndex;
				ErrorMessage = errorMessage;
			}

			internal static SlotOptions Invalid(string message)
			{
				return new SlotOptions(
					new[] { "不可用" },
					Array.Empty<string>(),
					0,
					message);
			}
		}
	}
}
