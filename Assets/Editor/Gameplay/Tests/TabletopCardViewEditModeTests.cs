using System;
using Gameplay.Content;
using Gameplay.Tabletop;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证卡牌视图只投影权威身份、尺寸和表现状态。
	/// </summary>
	public sealed class TabletopCardViewEditModeTests
	{
		[Test]
		public void Bind_ProjectsTheRuntimeCardAndContentIdentity()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			CardDefinition content = CreateContent("test.card");
			GameObject viewObject = CreateViewObject();
			try
			{
				TabletopCardView view = viewObject.GetComponent<TabletopCardView>();
				view.Bind(tabletopCard, content);
				Assert.That(view.TabletopCard, Is.SameAs(tabletopCard));
				Assert.That<TabletopCardId>(view.CardId, (IResolveConstraint)(object)Is.EqualTo((object)tabletopCard.Id));
				Assert.That<ContentId>(view.ContentId, (IResolveConstraint)(object)Is.EqualTo((object)content.ContentId));
				Assert.That(view.DisplaysCharacterStatus, Is.False);
				Assert.That(view.DisplayedHealthText, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)viewObject);
				Object.DestroyImmediate((Object)(object)content);
			}
		}

		[Test]
		public void Bind_RejectsContentWithDifferentIdentity()
		{
			TabletopCards state = new TabletopCards();
			TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
			CardDefinition content = CreateContent("test.other-card");
			GameObject viewObject = CreateViewObject();
			try
			{
				TabletopCardView view = viewObject.GetComponent<TabletopCardView>();
				Assert.Throws<ArgumentException>((TestDelegate)delegate
				{
					view.Bind(tabletopCard, content);
				});
			}
			finally
			{
				Object.DestroyImmediate((Object)(object)viewObject);
				Object.DestroyImmediate((Object)(object)content);
			}
		}

		private static GameObject CreateViewObject()
		{
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
				"Assets/Art/Prefabs/牌桌/卡牌视图.prefab");
			Assert.That(prefab, Is.Not.Null, "正式卡牌视图预制体不存在，不能用裸 GameObject 绕过生产表现依赖。");
			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			Assert.That(instance, Is.Not.Null, "正式卡牌视图预制体实例化失败。");
			return instance;
		}

		private static CardDefinition CreateContent(string contentId)
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			SerializedObject serializedContent = new SerializedObject((Object)(object)content);
			serializedContent.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serializedContent.ApplyModifiedPropertiesWithoutUndo();
			return content;
		}
	}
}
