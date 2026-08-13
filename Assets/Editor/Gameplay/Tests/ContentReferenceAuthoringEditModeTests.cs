using System;
using System.Reflection;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Editor.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using Sirenix.OdinInspector;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证跨内容作者引用通过类型受限的资产选择入口维护唯一内容 ID。
	/// </summary>
	public sealed class ContentReferenceAuthoringEditModeTests
	{
		[Test]
		public void AuthorReferenceFields_DeclareExpectedSelectableContentType()
		{
			AssertReferenceField<ActionSlotDefinition>("m_allowedContentIds", typeof(ContentAsset), "允许的内容");
			AssertReferenceField<CreateCardsResultIntent>("m_contentId", typeof(CardDefinition), "产物卡牌");
			AssertReferenceField<QuestDefinition>("m_prerequisiteQuestIds", typeof(QuestDefinition), "前置任务");
			AssertReferenceField<ActionCompletionQuestTaskDefinition>("m_actionId", typeof(ActionDefinition), "要求行动");
			AssertReferenceField<ContentDiscoveryQuestTaskDefinition>("m_discoveredContentId", typeof(ContentAsset), "要求发现内容");
			AssertReferenceField<ScenarioDefinition>("m_questIds", typeof(QuestDefinition), "剧本任务");
		}

		[Test]
		public void AssignReference_WritesSelectedAssetContentIdWithoutObjectReference()
		{
			QuestDefinition quest = CreateContent<QuestDefinition>("test.authoring.quest");
			ScenarioDefinition scenario = CreateContent<ScenarioDefinition>("test.authoring.scenario");
			try
			{
				SerializedObject serializedScenario = new SerializedObject(scenario);
				SerializedProperty questIds = serializedScenario.FindProperty("m_questIds");
				questIds.arraySize = 1;
				SerializedProperty contentId = questIds
					.GetArrayElementAtIndex(0)
					.FindPropertyRelative("m_value");

				ContentIdReferenceDrawer.AssignReference(
					contentId,
					quest,
					typeof(QuestDefinition));
				serializedScenario.ApplyModifiedPropertiesWithoutUndo();

				Assert.That(scenario.QuestIds.Count, Is.EqualTo(1));
				Assert.That(scenario.QuestIds[0], Is.EqualTo(quest.ContentId));
				Assert.That(contentId.propertyType, Is.EqualTo(SerializedPropertyType.String));
			}
			finally
			{
				Object.DestroyImmediate(quest);
				Object.DestroyImmediate(scenario);
			}
		}

		[Test]
		public void AssignReference_RejectsContentOutsideDeclaredType()
		{
			CardDefinition card = CreateContent<CardDefinition>("test.authoring.card");
			ScenarioDefinition scenario = CreateContent<ScenarioDefinition>("test.authoring.scenario");
			try
			{
				SerializedObject serializedScenario = new SerializedObject(scenario);
				SerializedProperty questIds = serializedScenario.FindProperty("m_questIds");
				questIds.arraySize = 1;
				SerializedProperty contentId = questIds
					.GetArrayElementAtIndex(0)
					.FindPropertyRelative("m_value");

				Assert.Throws<InvalidOperationException>(() =>
					ContentIdReferenceDrawer.AssignReference(
						contentId,
						card,
						typeof(QuestDefinition)));
			}
			finally
			{
				Object.DestroyImmediate(card);
				Object.DestroyImmediate(scenario);
			}
		}

		private static TContent CreateContent<TContent>(string contentId)
			where TContent : ContentAsset
		{
			TContent content = ScriptableObject.CreateInstance<TContent>();
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}}}}",
				content);
			return content;
		}

		private static void AssertReferenceField<TDeclaring>(
			string fieldName,
			Type expectedType,
			string expectedLabel)
		{
			FieldInfo field = typeof(TDeclaring).GetField(
				fieldName,
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"{typeof(TDeclaring).Name}.{fieldName} 不存在。");
			ContentIdReferenceAttribute attribute =
				field.GetCustomAttribute<ContentIdReferenceAttribute>();
			Assert.That(attribute, Is.Not.Null, $"{typeof(TDeclaring).Name}.{fieldName} 仍要求手填内容 ID。");
			Assert.That(attribute.ContentType, Is.EqualTo(expectedType));
			LabelTextAttribute label = field.GetCustomAttribute<LabelTextAttribute>();
			Assert.That(label, Is.Not.Null, $"{typeof(TDeclaring).Name}.{fieldName} 缺少作者可读标签。");
			Assert.That(label.Text, Is.EqualTo(expectedLabel));
		}
	}
}
