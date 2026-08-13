using Gameplay.Content;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证内容唯一身份的稳定生成与已生成身份不可自动漂移。
	/// </summary>
	public sealed class ContentIdentityEditModeTests
	{
		[Test]
		public void GeneratedContentIds_AreStableAndDistinctForDifferentAssetSeeds()
		{
			string first = ContentIdRules.CreateGeneratedContentId("示例内容", "guid-first");
			string repeated = ContentIdRules.CreateGeneratedContentId("示例内容", "guid-first");
			string second = ContentIdRules.CreateGeneratedContentId("示例内容", "guid-second");

			Assert.That(first, Is.EqualTo(repeated));
			Assert.That(second, Is.Not.EqualTo(first));
			Assert.That(ContentIdRules.IsValidKey(first), Is.True);
			Assert.That(ContentIdRules.IsValidKey(second), Is.True);
		}

		[Test]
		public void EnsureGeneratedContentIdForEditor_DoesNotReplaceAnExistingValidId()
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.stable-content-id\"}}",
					content);

				bool changed = content.EnsureGeneratedContentIdForEditor();

				Assert.That(changed, Is.False);
				Assert.That(content.ContentId.Value, Is.EqualTo("test.stable-content-id"));
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void ReadingOptionalPresentationReferences_DoesNotModifyAuthorAsset()
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			try
			{
				Assert.That(content.Icon, Is.Null);
				Assert.That(content.CardArt, Is.Null);
				Assert.That(content.Artwork, Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}
	}
}
