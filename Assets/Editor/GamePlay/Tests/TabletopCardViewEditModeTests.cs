using NUnit.Framework;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Tests
{
    /// <summary>
    /// 验证卡牌视图只绑定身份一致的局内卡牌与卡牌作者源。
    /// </summary>
    public sealed class TabletopCardViewEditModeTests
    {
        [Test]
        public void Bind_ProjectsTheRuntimeCardAndContentIdentity()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            CardDefinition content = CreateContent("test.card");
            GameObject viewObject = new("TabletopCardViewTest");

            try
            {
                TabletopCardView view = viewObject.AddComponent<TabletopCardView>();
                view.Bind(tabletopCard, content);

                Assert.That(view.CardId, Is.EqualTo(tabletopCard.Id));
                Assert.That(view.ContentId, Is.EqualTo(content.ContentId));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(content);
            }
        }

        [Test]
        public void Bind_RejectsContentWithDifferentIdentity()
        {
            var state = new TabletopCardState();
            TabletopCard tabletopCard = state.CreateCard("test.card", Vector2.zero);
            CardDefinition content = CreateContent("test.other-card");
            GameObject viewObject = new("TabletopCardViewTest");

            try
            {
                TabletopCardView view = viewObject.AddComponent<TabletopCardView>();

                Assert.Throws<System.ArgumentException>(
                    () => view.Bind(tabletopCard, content));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(content);
            }
        }

        private static CardDefinition CreateContent(string contentId)
        {
            CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
            var serializedContent = new UnityEditor.SerializedObject(content);
            serializedContent.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
            serializedContent.ApplyModifiedPropertiesWithoutUndo();
            return content;
        }
    }
}
