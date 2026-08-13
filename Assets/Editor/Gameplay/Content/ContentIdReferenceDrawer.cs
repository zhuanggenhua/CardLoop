using System;
using System.Collections.Generic;
using Gameplay.Content;
using UnityEditor;
using UnityEngine;

namespace Gameplay.Editor.Content
{
	/// <summary>
	/// 把 ContentId 作者引用绘制为内容资产选择器；序列化结果仍只有唯一内容 ID。
	/// </summary>
	[CustomPropertyDrawer(typeof(ContentIdReferenceAttribute))]
	public sealed class ContentIdReferenceDrawer : PropertyDrawer
	{
		private const float HelpSpacing = 2f;

		private static Dictionary<string, ContentAsset> s_assetsById;

		[InitializeOnLoadMethod]
		private static void RegisterProjectChangeInvalidation()
		{
			EditorApplication.projectChanged -= InvalidateAssetIndex;
			EditorApplication.projectChanged += InvalidateAssetIndex;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			SerializedProperty valueProperty = FindValueProperty(property);
			if (valueProperty == null)
			{
				EditorGUI.LabelField(position, label.text, "字段不是 ContentId。");
				EditorGUI.EndProperty();
				return;
			}

			ContentIdReferenceAttribute reference = (ContentIdReferenceAttribute)attribute;
			string contentId = valueProperty.stringValue;
			ContentAsset resolvedAsset = Resolve(contentId);
			ContentAsset displayedAsset = resolvedAsset != null &&
				reference.ContentType.IsInstanceOfType(resolvedAsset)
					? resolvedAsset
					: null;

			Rect fieldRect = new Rect(
				position.x,
				position.y,
				position.width,
				EditorGUIUtility.singleLineHeight);
			EditorGUI.BeginChangeCheck();
			ContentAsset selectedAsset = EditorGUI.ObjectField(
				fieldRect,
				label,
				displayedAsset,
				reference.ContentType,
				allowSceneObjects: false) as ContentAsset;
			if (EditorGUI.EndChangeCheck())
			{
				AssignReference(valueProperty, selectedAsset, reference.ContentType);
				InvalidateAssetIndex();
			}

			if (ContentIdRules.IsValidKey(contentId) && displayedAsset == null)
			{
				Rect helpRect = new Rect(
					position.x,
					fieldRect.yMax + HelpSpacing,
					position.width,
					EditorGUIUtility.singleLineHeight * 2f);
				string message = resolvedAsset == null
					? $"找不到内容：{contentId}"
					: $"内容类型不符合 {reference.ContentType.Name}：{contentId}";
				EditorGUI.HelpBox(helpRect, message, MessageType.Error);
			}
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			SerializedProperty valueProperty = FindValueProperty(property);
			if (valueProperty == null || !ContentIdRules.IsValidKey(valueProperty.stringValue))
			{
				return EditorGUIUtility.singleLineHeight;
			}
			ContentIdReferenceAttribute reference = (ContentIdReferenceAttribute)attribute;
			ContentAsset resolvedAsset = Resolve(valueProperty.stringValue);
			if (resolvedAsset != null && reference.ContentType.IsInstanceOfType(resolvedAsset))
			{
				return EditorGUIUtility.singleLineHeight;
			}
			return EditorGUIUtility.singleLineHeight * 3f + HelpSpacing;
		}

		internal static void AssignReference(
			SerializedProperty valueProperty,
			ContentAsset selectedAsset,
			Type expectedType)
		{
			if (valueProperty == null || valueProperty.propertyType != SerializedPropertyType.String)
			{
				throw new ArgumentException("内容引用必须写入 ContentId.m_value 字符串字段。", nameof(valueProperty));
			}
			if (expectedType == null || !typeof(ContentAsset).IsAssignableFrom(expectedType))
			{
				throw new ArgumentException("内容引用限制类型必须继承 ContentAsset。", nameof(expectedType));
			}
			if (selectedAsset == null)
			{
				valueProperty.stringValue = string.Empty;
				return;
			}
			if (!expectedType.IsInstanceOfType(selectedAsset))
			{
				throw new InvalidOperationException(
					$"内容 {selectedAsset.name} 不是允许的 {expectedType.Name} 类型。");
			}
			selectedAsset.EnsureGeneratedContentIdForEditor();
			if (!selectedAsset.ContentId.IsValid)
			{
				throw new InvalidOperationException($"内容 {selectedAsset.name} 尚未生成有效的唯一内容 ID。");
			}
			valueProperty.stringValue = selectedAsset.ContentId.Value;
		}

		private static SerializedProperty FindValueProperty(SerializedProperty property)
		{
			return property?.FindPropertyRelative("m_value");
		}

		private static ContentAsset Resolve(string contentId)
		{
			if (!ContentIdRules.IsValidKey(contentId))
			{
				return null;
			}
			EnsureAssetIndex();
			s_assetsById.TryGetValue(contentId, out ContentAsset contentAsset);
			return contentAsset;
		}

		private static void EnsureAssetIndex()
		{
			if (s_assetsById != null)
			{
				return;
			}
			s_assetsById = new Dictionary<string, ContentAsset>(StringComparer.Ordinal);
			string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				ContentAsset contentAsset = AssetDatabase.LoadAssetAtPath<ContentAsset>(path);
				if (contentAsset == null || !contentAsset.ContentId.IsValid)
				{
					continue;
				}
				if (!s_assetsById.TryAdd(contentAsset.ContentId.Value, contentAsset))
				{
					s_assetsById[contentAsset.ContentId.Value] = null;
				}
			}
		}

		private static void InvalidateAssetIndex()
		{
			s_assetsById = null;
		}
	}
}
