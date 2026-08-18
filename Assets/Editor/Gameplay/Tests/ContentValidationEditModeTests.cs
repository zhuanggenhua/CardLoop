using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Quests;
using Gameplay.Scenarios;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证内容派生类型能通过正式入口补充自己的作者规则。
	/// </summary>
	public sealed class ContentValidationEditModeTests
	{
		private sealed class ValidationProbeContent : ContentAsset
		{
			protected override void ValidateContent(ContentValidationContext context)
			{
				context.AddError(
					"TEST_DERIVED_CONTENT_VALIDATION",
					"派生内容校验入口已执行。",
					this);
			}
		}

		private sealed class ValidationCollectionProbeContent : ContentAsset
		{
			internal bool AssetsAreReadOnly { get; private set; }

			protected override void ValidateContent(ContentValidationContext context)
			{
				IList<ContentAsset> assets = context.Assets as IList<ContentAsset>;
				AssetsAreReadOnly = assets != null && assets.IsReadOnly;
				if (assets != null)
				{
					Assert.Throws<NotSupportedException>(() => assets.Clear());
				}
			}
		}

		[Test]
		public void ValidateContentAssets_InvokesDerivedContentValidation()
		{
			ValidationProbeContent content = ScriptableObject.CreateInstance<ValidationProbeContent>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.validation-probe\"}}",
					content);
				ContentValidationReport report = ContentValidator.ValidateContentAssets(
					new ContentAsset[] { content });

				bool found = false;
				for (int i = 0; i < report.Issues.Count; i++)
				{
					if (report.Issues[i].Code == "TEST_DERIVED_CONTENT_VALIDATION")
					{
						found = true;
						break;
					}
				}
				Assert.That(found, Is.True);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void Build_PublishesAssetCollectionThatCannotDivergeFromIdIndex()
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.read-only-index\"}}",
					content);
				ContentIndex index = ContentIndex.Build(new ContentAsset[] { content });
				IList<ContentAsset> assetList = index.AllAssets as IList<ContentAsset>;

				Assert.That(assetList, Is.Not.Null);
				Assert.That(assetList.IsReadOnly, Is.True);
				Assert.Throws<NotSupportedException>(() => assetList.Clear());
				Assert.That(index.Count, Is.EqualTo(1));
				Assert.That(index.TryGet(content.ContentId, out ContentAsset indexed), Is.True);
				Assert.That(indexed, Is.SameAs(content));
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void ValidateContentAssets_PublishesIssuesThatCannotBeRemovedByCallers()
		{
			ValidationProbeContent content = ScriptableObject.CreateInstance<ValidationProbeContent>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.read-only-report\"}}",
					content);
				ContentValidationReport report = ContentValidator.ValidateContentAssets(
					new ContentAsset[] { content });
				IList<ContentValidationIssue> issues = report.Issues as IList<ContentValidationIssue>;

				Assert.That(issues, Is.Not.Null);
				Assert.That(issues.IsReadOnly, Is.True);
				Assert.Throws<NotSupportedException>(() => issues.Clear());
				Assert.That(report.HasErrors, Is.True);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void ValidateContentAssets_GivesDerivedContentAnImmutableAssetCollection()
		{
			ValidationCollectionProbeContent content =
				ScriptableObject.CreateInstance<ValidationCollectionProbeContent>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.read-only-validation-assets\"}}",
					content);

				ContentValidationReport report = ContentValidator.ValidateContentAssets(
					new ContentAsset[] { content });

				Assert.That(report.HasErrors, Is.False);
				Assert.That(content.AssetsAreReadOnly, Is.True);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void Build_RejectsDuplicateAssetReferenceThroughContentValidation()
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			try
			{
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.duplicate-reference\"}}",
					content);

				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
					() => ContentIndex.Build(new ContentAsset[] { content, content }));

				StringAssert.Contains("CONTENT_ASSET_DUPLICATE_REFERENCE", exception.Message);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void ContentSetSnapshot_RequiresSavedContentButAllowsAdditionalInstalledContent()
		{
			CardDefinition first = CreateContent("test.snapshot.first");
			CardDefinition second = CreateContent("test.snapshot.second");
			CardDefinition addedLater = CreateContent("test.snapshot.added-later");
			try
			{
				ContentIndex savedIndex = ContentIndex.Build(new ContentAsset[] { second, first });
				ContentSetSnapshot snapshot = savedIndex.CreateSnapshot();

				Assert.That(
					snapshot.ContentIds,
					Is.EqualTo(new[] { first.ContentId, second.ContentId }),
					"内容集合快照必须使用稳定顺序，不能依赖 YooAsset 或 Mod 的枚举顺序。");

				string json = JsonUtility.ToJson(snapshot);
				ContentSetSnapshot serialized = JsonUtility.FromJson<ContentSetSnapshot>(json);
				ContentIndex compatibleIndex = ContentIndex.Build(
					new ContentAsset[] { addedLater, second, first });

				Assert.DoesNotThrow(() => compatibleIndex.RequireContentSet(serialized));

				ContentIndex missingIndex = ContentIndex.Build(new ContentAsset[] { addedLater });
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
					() => missingIndex.RequireContentSet(serialized));
				StringAssert.Contains(first.ContentId.Value, exception.Message);
				StringAssert.Contains(second.ContentId.Value, exception.Message);
			}
			finally
			{
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
				Object.DestroyImmediate(addedLater);
			}
		}

		[Test]
		public void ContentAuthoringBoundary_DoesNotForceDisplayFieldsOntoContentIdentity()
		{
			Assert.That(
				typeof(ContentAsset).GetProperty(nameof(DisplayableContentAsset.DisplayName)),
				Is.Null,
				"内容身份基类不能强迫纯规则参数提供玩家展示字段。");
			Assert.That(typeof(DisplayableContentAsset).IsAssignableFrom(typeof(CardDefinition)), Is.True);
			Assert.That(typeof(DisplayableContentAsset).IsAssignableFrom(typeof(ActionDefinition)), Is.True);
			Assert.That(typeof(DisplayableContentAsset).IsAssignableFrom(typeof(ScenarioDefinition)), Is.True);
			Assert.That(typeof(DisplayableContentAsset).IsAssignableFrom(typeof(QuestDefinition)), Is.True);
		}

		[Test]
		public void ContentDefinitions_RemainOpenForCodeModAuthoring()
		{
			Assert.That(
				typeof(CardDefinition).IsSealed,
				Is.False,
				"卡牌定义是代码 Mod 的正式作者扩展点，不能被 sealed 封死。");
			Assert.That(
				typeof(ActionDefinition).IsSealed,
				Is.False,
				"行动定义是代码 Mod 的正式作者扩展点，不能被 sealed 封死。");
			Assert.That(
				typeof(ScenarioDefinition).IsSealed,
				Is.False,
				"剧本定义是代码 Mod 的正式作者扩展点，不能被 sealed 封死。");
			Assert.That(
				typeof(QuestDefinition).IsSealed,
				Is.False,
				"任务定义已经提供受保护的作者校验钩子，不能再用 sealed 封死代码 Mod 的派生入口。");
		}

		[Test]
		public void CardDefinition_RejectsAutomaticMovementWithoutPositiveRadiusAndAttempts()
		{
			CardDefinition content = CreateContent("test.automatic-movement.validation");
			JsonUtility.FromJsonOverwrite(
				"{\"m_automaticMovementIntervalSeconds\":1.0}",
				content);
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(
					new ContentAsset[] { content });

				Assert.That(ContainsIssue(report, "CARD_AUTOMATIC_MOVEMENT_RADIUS_INVALID"), Is.True);
				Assert.That(ContainsIssue(report, "CARD_AUTOMATIC_MOVEMENT_ATTEMPTS_INVALID"), Is.True);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void CardDefinition_RejectsNegativeAutomaticMovementRetentionCapacity()
		{
			CardDefinition content = CreateContent("test.automatic-movement.retention.validation");
			JsonUtility.FromJsonOverwrite(
				"{\"m_automaticMovementRetentionCapacity\":-1}",
				content);
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(
					new ContentAsset[] { content });

				Assert.That(
					ContainsIssue(report, "CARD_AUTOMATIC_MOVEMENT_RETENTION_CAPACITY_INVALID"),
					Is.True);
			}
			finally
			{
				Object.DestroyImmediate(content);
			}
		}

		[Test]
		public void CardDefinition_RejectsPeriodicProductionWithoutPositiveInterval()
		{
			CardDefinition producer = CreateContent("test.periodic.validation.producer");
			CardDefinition product = CreateContent("test.periodic.validation.product");
			JsonUtility.FromJsonOverwrite(
				"{\"m_periodicProductionCardId\":{\"m_value\":\"test.periodic.validation.product\"}}",
				producer);
			try
			{
				ContentValidationReport report = ContentValidator.ValidateContentAssets(
					new ContentAsset[] { producer, product });

				bool found = false;
				for (int i = 0; i < report.Issues.Count; i++)
				{
					if (report.Issues[i].Code == "CARD_PERIODIC_PRODUCTION_INTERVAL_INVALID")
					{
						found = true;
						break;
					}
				}
				Assert.That(found, Is.True);
			}
			finally
			{
				Object.DestroyImmediate(producer);
				Object.DestroyImmediate(product);
			}
		}

		private static bool ContainsIssue(ContentValidationReport report, string code)
		{
			for (int i = 0; i < report.Issues.Count; i++)
			{
				if (report.Issues[i].Code == code)
				{
					return true;
				}
			}
			return false;
		}

		private static CardDefinition CreateContent(string contentId)
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				"{\"m_contentId\":{\"m_value\":\"" + contentId + "\"}}",
				content);
			return content;
		}
	}
}
