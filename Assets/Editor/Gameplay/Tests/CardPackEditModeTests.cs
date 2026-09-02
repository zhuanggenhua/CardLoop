using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>验证卡包通过正式牌桌行动按槽抽取，并复用单局发现与权威随机。</summary>
	public sealed class CardPackEditModeTests
	{
		[Test]
		public void OpenPackAction_DrawsSlotsInOrderAndRemovesPackWhenExhausted()
		{
			using PackTestContext context = PackTestContext.Create(
				12345u,
				new[]
				{
					PackSlot(CardEntry("test.pack.product.first", 1)),
					PackSlot(CardEntry("test.pack.product.second", 1))
				});

			context.OpenPack();

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(context.PackCardId, out TabletopCard pack), Is.True);
			Assert.That(pack.RemainingUses, Is.EqualTo(1));
			Assert.That(context.CountCards("test.pack.product.first"), Is.EqualTo(1));
			Assert.That(context.CountCards("test.pack.product.second"), Is.Zero);

			context.OpenPack();

			Assert.That(context.Run.Tabletop.Cards.TryGetCard(context.PackCardId, out _), Is.False);
			Assert.That(context.CountCards("test.pack.product.first"), Is.EqualTo(1));
			Assert.That(context.CountCards("test.pack.product.second"), Is.EqualTo(1));
		}

		[Test]
		public void OpenPackAction_WithSameSeedProducesSameWeightedDraw()
		{
			CardPackSlotDefinition[] slots =
			{
				PackSlot(
					CardEntry("test.pack.product.first", 1),
					CardEntry("test.pack.product.second", 5))
			};
			using PackTestContext first = PackTestContext.Create(9981u, slots);
			using PackTestContext replay = PackTestContext.Create(9981u, slots);

			first.OpenPack();
			replay.OpenPack();

			Assert.That(first.DrawnProductIds, Is.EqualTo(replay.DrawnProductIds));
		}

		[Test]
		public void OpenPackAction_SpawnsDrawnCardAtStackCraftLiftedPackHeight()
		{
			using PackTestContext context = PackTestContext.Create(
				12345u,
				new[] { PackSlot(CardEntry("test.pack.product.first", 1)) });
			List<TabletopPresentationCue> cues = new List<TabletopPresentationCue>();
			context.Run.Tabletop.PresentationCueRequested += cues.Add;
			try
			{
				context.OpenPack();

				Assert.That(cues, Has.Some.Matches<TabletopPresentationCue>(cue =>
					cue.Kind == TabletopPresentationCueKind.CardSpawn &&
					cue.UsesDragHeight &&
					cue.HasSpawnOriginCardId &&
					cue.SpawnOriginCardId == context.PackCardId &&
					Mathf.Approximately(cue.SpawnHeightOffset, 0.1f)));
			}
			finally
			{
				context.Run.Tabletop.PresentationCueRequested -= cues.Add;
			}
		}

		[Test]
		public void OpenPackAction_SpawnsRecipeCardAtStackCraftLiftedPackHeight()
		{
			using PackTestContext context = PackTestContext.Create(
				12345u,
				new[]
				{
					PackSlot(
						1f,
						new[] { CardEntry("test.pack.product.first", 1) },
						RecipeEntry("test.pack.recipe.first", "test.pack.recipe-card.first"))
				});
			List<TabletopPresentationCue> cues = new List<TabletopPresentationCue>();
			context.Run.Tabletop.PresentationCueRequested += cues.Add;
			try
			{
				context.OpenPack();

				Assert.That(context.CountCards("test.pack.recipe-card.first"), Is.EqualTo(1));
				Assert.That(context.CountCards("test.pack.product.first"), Is.Zero);
				Assert.That(cues, Has.Some.Matches<TabletopPresentationCue>(cue =>
					cue.Kind == TabletopPresentationCueKind.CardSpawn &&
					cue.UsesDragHeight &&
					cue.HasSpawnOriginCardId &&
					cue.SpawnOriginCardId == context.PackCardId &&
					Mathf.Approximately(cue.SpawnHeightOffset, 0.1f)));
			}
			finally
			{
				context.Run.Tabletop.PresentationCueRequested -= cues.Add;
			}
		}

		[Test]
		public void OpenPackAction_DiscoversOnlyUndiscoveredRecipeBeforeFallingBackToCards()
		{
			using PackTestContext context = PackTestContext.Create(
				12345u,
				new[]
				{
					PackSlot(
						1f,
						new[] { CardEntry("test.pack.product.first", 1) },
						RecipeEntry("test.pack.recipe.first", "test.pack.recipe-card.first"),
						RecipeEntry("test.pack.recipe.second", "test.pack.recipe-card.second"))
				});
			context.Run.DiscoverContent(new ContentId("test.pack.recipe.first"));

			context.OpenPack();

			Assert.That(context.Run.IsContentDiscovered(new ContentId("test.pack.recipe.second")), Is.True);
			Assert.That(context.CountCards("test.pack.recipe-card.first"), Is.Zero);
			Assert.That(context.CountCards("test.pack.recipe-card.second"), Is.EqualTo(1));
			Assert.That(context.CountCards("test.pack.product.first"), Is.Zero);
		}

		private static CardPackSlotDefinition PackSlot(params CardPackEntry[] entries)
		{
			return PackSlot(0f, entries);
		}

		private static CardPackSlotDefinition PackSlot(
			float recipeChance,
			IReadOnlyList<CardPackEntry> entries,
			params CardPackRecipeEntry[] recipes)
		{
			CardPackSlotDefinition slot = new CardPackSlotDefinition();
			SetField(slot, "m_entries", entries.ToArray());
			SetField(slot, "m_recipeEntries", recipes);
			SetField(slot, "m_recipeChance", recipeChance);
			return slot;
		}

		private static CardPackEntry CardEntry(string contentId, int weight)
		{
			CardPackEntry entry = new CardPackEntry();
			SetField(entry, "m_cardId", new ContentId(contentId));
			SetField(entry, "m_weight", weight);
			return entry;
		}

		private static CardPackRecipeEntry RecipeEntry(string actionId, string recipeCardId)
		{
			CardPackRecipeEntry entry = new CardPackRecipeEntry();
			SetField(entry, "m_actionId", new ContentId(actionId));
			SetField(entry, "m_recipeCardId", new ContentId(recipeCardId));
			return entry;
		}

		private static void SetField(object target, string fieldName, object value)
		{
			Type type = target.GetType();
			while (type != null)
			{
				System.Reflection.FieldInfo field = type.GetField(
					fieldName,
					System.Reflection.BindingFlags.Instance |
					System.Reflection.BindingFlags.NonPublic);
				if (field != null)
				{
					field.SetValue(target, value);
					return;
				}
				type = type.BaseType;
			}

			throw new MissingFieldException(target.GetType().FullName, fieldName);
		}

		private sealed class PackTestContext : IDisposable
		{
			private readonly List<Object> m_assets;
			private readonly HashSet<ContentId> m_nonProductIds;

			internal ScenarioRun Run { get; }

			internal TabletopCardId PackCardId { get; }

			internal IReadOnlyList<ContentId> DrawnProductIds =>
				Run.Tabletop.Cards.Stacks
					.SelectMany(stack => stack.Cards)
					.Select(card => card.ContentId)
					.Where(contentId => !m_nonProductIds.Contains(contentId))
					.OrderBy(contentId => contentId.Value, StringComparer.Ordinal)
					.ToArray();

			private PackTestContext(
				List<Object> assets,
				HashSet<ContentId> nonProductIds,
				ScenarioRun run,
				TabletopCardId packCardId)
			{
				m_assets = assets;
				m_nonProductIds = nonProductIds;
				Run = run;
				PackCardId = packCardId;
			}

			internal static PackTestContext Create(uint seed, CardPackSlotDefinition[] slots)
			{
				List<Object> assets = new List<Object>();
				CardPackDefinition pack = ScriptableObject.CreateInstance<CardPackDefinition>();
				SetContentId(pack, "test.pack.card");
				SetField(pack, "m_slots", slots);
				SetField(pack, "m_countsTowardCardLimit", false);
				assets.Add(pack);

				HashSet<string> referencedCardIds = new HashSet<string>(StringComparer.Ordinal);
				HashSet<string> referencedActionIds = new HashSet<string>(StringComparer.Ordinal);
				foreach (CardPackSlotDefinition slot in slots)
				{
					foreach (CardPackEntry entry in slot.Entries)
					{
						referencedCardIds.Add(entry.CardId.Value);
					}
					foreach (CardPackRecipeEntry recipe in slot.RecipeEntries)
					{
						referencedActionIds.Add(recipe.ActionId.Value);
						referencedCardIds.Add(recipe.RecipeCardId.Value);
					}
				}
				foreach (string cardId in referencedCardIds)
				{
					CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
					SetContentId(card, cardId);
					assets.Add(card);
				}
				foreach (string actionId in referencedActionIds)
				{
					ActionDefinition recipe = ScriptableObject.CreateInstance<ActionDefinition>();
					SetContentId(recipe, actionId);
					assets.Add(recipe);
				}

				ActionDefinition openAction = ScriptableObject.CreateInstance<ActionDefinition>();
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.pack.open\"},\"m_turnCost\":0,\"m_canStartFromClick\":true," +
					"\"m_participationSlots\":[{\"m_key\":\"pack\",\"m_minimumParticipants\":1," +
					"\"m_maximumParticipants\":1,\"m_allowedContentIds\":[{\"m_value\":\"test.pack.card\"}]}]}",
					openAction);
				SerializedObject serializedAction = new SerializedObject(openAction);
				SerializedProperty intents = serializedAction.FindProperty("m_resultIntents");
				intents.arraySize = 1;
				OpenCardPackResultIntent openIntent = new OpenCardPackResultIntent();
				JsonUtility.FromJsonOverwrite("{\"m_packSlotKey\":\"pack\"}", openIntent);
				intents.GetArrayElementAtIndex(0).managedReferenceValue = openIntent;
				serializedAction.ApplyModifiedPropertiesWithoutUndo();
				assets.Add(openAction);

				ScenarioRegionDefinition region = ScriptableObject.CreateInstance<ScenarioRegionDefinition>();
				SetContentId(region, "test.pack.region");
				assets.Add(region);
				ScenarioDefinition scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
				JsonUtility.FromJsonOverwrite(
					"{\"m_contentId\":{\"m_value\":\"test.pack.scenario\"}," +
					"\"m_initialRegionId\":{\"m_value\":\"test.pack.region\"}," +
					"\"m_regionIds\":[{\"m_value\":\"test.pack.region\"}]}",
					scenario);
				assets.Add(scenario);

				ContentIndex content = ContentIndex.Build(assets.Cast<ContentAsset>());
				ScenarioRun run = new ScenarioRun(scenario, content, seed);
				run.DiscoverContent(openAction.ContentId);
				TabletopCard packCard = run.Tabletop.CreateCard(pack.ContentId, Vector2.zero);
				HashSet<ContentId> nonProducts = new HashSet<ContentId>
				{
					pack.ContentId,
					openAction.ContentId,
					region.ContentId,
					scenario.ContentId
				};
				foreach (string actionId in referencedActionIds)
				{
					nonProducts.Add(new ContentId(actionId));
				}
				return new PackTestContext(assets, nonProducts, run, packCard.Id);
			}

			internal void OpenPack()
			{
				Vector2 position = Run.Tabletop.Cards.GetStackContaining(PackCardId).Position;
				TabletopCardPointerReleaseIntent click = new TabletopCardPointerReleaseIntent(
					PackCardId,
					position,
					position,
					position,
					isDrag: false);
				ActionCandidate[] candidates = Run.FindActionCandidates(click);
				Assert.That(candidates, Has.Length.EqualTo(1));
				Run.StartAction(ActionRequest.FromCandidate(candidates[0]));
			}

			internal int CountCards(string contentId)
			{
				ContentId expected = new ContentId(contentId);
				return Run.Tabletop.Cards.Stacks
					.SelectMany(stack => stack.Cards)
					.Count(card => card.ContentId == expected);
			}

			public void Dispose()
			{
				Run.End();
				for (int i = m_assets.Count - 1; i >= 0; i--)
				{
					Object.DestroyImmediate(m_assets[i]);
				}
			}
		}

		private static void SetContentId(ContentAsset asset, string contentId)
		{
			SerializedObject serializedAsset = new SerializedObject(asset);
			serializedAsset.FindProperty("m_contentId").FindPropertyRelative("m_value").stringValue = contentId;
			serializedAsset.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
