using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>一个卡包在当前单局中的可发现内容进度。</summary>
	public readonly struct CardPackCollectionProgress
	{
		public int DiscoveredCount { get; }

		public int TotalCount { get; }

		public bool IsComplete => TotalCount > 0 && DiscoveredCount >= TotalCount;

		internal CardPackCollectionProgress(int discoveredCount, int totalCount)
		{
			DiscoveredCount = discoveredCount;
			TotalCount = totalCount;
		}
	}

	/// <summary>卡包普通抽取池中的一个加权卡牌条目。</summary>
	[Serializable]
	public sealed class CardPackEntry
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("卡牌")]
		private ContentId m_cardId;

		[SerializeField, Min(1), LabelText("权重")]
		private int m_weight = 1;

		public ContentId CardId => m_cardId;

		public int Weight => m_weight;
	}

	/// <summary>卡包可发现的行动及其在牌桌上生成的配方卡。</summary>
	[Serializable]
	public sealed class CardPackRecipeEntry
	{
		[SerializeField, ContentIdReference(typeof(ActionDefinition)), LabelText("解锁行动")]
		private ContentId m_actionId;

		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("配方卡")]
		private ContentId m_recipeCardId;

		public ContentId ActionId => m_actionId;

		public ContentId RecipeCardId => m_recipeCardId;
	}

	/// <summary>卡包一次打开所使用的普通卡池和未发现配方池。</summary>
	[Serializable]
	public sealed class CardPackSlotDefinition
	{
		[SerializeField, LabelText("普通卡池")]
		private CardPackEntry[] m_entries = Array.Empty<CardPackEntry>();

		[SerializeField, LabelText("配方候选")]
		private CardPackRecipeEntry[] m_recipeEntries = Array.Empty<CardPackRecipeEntry>();

		[SerializeField, Range(0f, 1f), LabelText("配方概率")]
		[Tooltip("命中后只从当前单局尚未发现的配方中等概率抽取；没有可用配方时回退普通卡池。")]
		private float m_recipeChance;

		public IReadOnlyList<CardPackEntry> Entries => m_entries ?? Array.Empty<CardPackEntry>();

		public IReadOnlyList<CardPackRecipeEntry> RecipeEntries =>
			m_recipeEntries ?? Array.Empty<CardPackRecipeEntry>();

		public float RecipeChance => m_recipeChance;
	}

	/// <summary>可实例化到牌桌的卡包；每个槽位对应一次打开。</summary>
	[CreateAssetMenu(menuName = "Gameplay/内容/卡包", fileName = "卡包_")]
	public class CardPackDefinition : CardDefinition
	{
		[SerializeField, LabelText("抽取槽位")]
		[Tooltip("按列表顺序逐次抽取；每个槽位只结算一次。")]
		private CardPackSlotDefinition[] m_slots = Array.Empty<CardPackSlotDefinition>();

		public IReadOnlyList<CardPackSlotDefinition> Slots =>
			m_slots ?? Array.Empty<CardPackSlotDefinition>();

		public override int InitialUses => Slots.Count;

		protected override bool HasDerivedInitialUses => true;

		/// <summary>按唯一内容 ID 统计卡包所有普通卡和可发现行动，不把 UI 计数保存成第二份状态。</summary>
		public CardPackCollectionProgress GetCollectionProgress(Func<ContentId, bool> isDiscovered)
		{
			if (isDiscovered == null)
			{
				throw new ArgumentNullException(nameof(isDiscovered));
			}
			HashSet<ContentId> contents = new HashSet<ContentId>();
			for (int slotIndex = 0; slotIndex < Slots.Count; slotIndex++)
			{
				CardPackSlotDefinition slot = Slots[slotIndex] ??
					throw new InvalidOperationException($"卡包 {ContentId} 的第 {slotIndex + 1} 个槽位为空。");
				for (int entryIndex = 0; entryIndex < slot.Entries.Count; entryIndex++)
				{
					CardPackEntry entry = slot.Entries[entryIndex] ??
						throw new InvalidOperationException($"卡包 {ContentId} 的普通卡池包含空条目。");
					contents.Add(entry.CardId);
				}
				for (int recipeIndex = 0; recipeIndex < slot.RecipeEntries.Count; recipeIndex++)
				{
					CardPackRecipeEntry recipe = slot.RecipeEntries[recipeIndex] ??
						throw new InvalidOperationException($"卡包 {ContentId} 的配方池包含空条目。");
					contents.Add(recipe.ActionId);
				}
			}

			int discoveredCount = 0;
			foreach (ContentId contentId in contents)
			{
				if (isDiscovered(contentId))
				{
					discoveredCount++;
				}
			}
			return new CardPackCollectionProgress(discoveredCount, contents.Count);
		}

		protected override void ValidateContent(ContentValidationContext context)
		{
			base.ValidateContent(context);
			if (Slots.Count == 0)
			{
				context.AddError("CARD_PACK_SLOTS_EMPTY", $"卡包 {ContentId} 至少需要一个抽取槽位。", this);
				return;
			}

			for (int slotIndex = 0; slotIndex < Slots.Count; slotIndex++)
			{
				ValidateSlot(context, Slots[slotIndex], slotIndex);
			}
		}

		private void ValidateSlot(ContentValidationContext context, CardPackSlotDefinition slot, int slotIndex)
		{
			if (slot == null)
			{
				context.AddError("CARD_PACK_SLOT_NULL", $"卡包 {ContentId} 的第 {slotIndex + 1} 个抽取槽位为空。", this);
				return;
			}
			if (!float.IsFinite(slot.RecipeChance) || slot.RecipeChance < 0f || slot.RecipeChance > 1f)
			{
				context.AddError("CARD_PACK_RECIPE_CHANCE_INVALID", $"卡包 {ContentId} 的第 {slotIndex + 1} 个槽位配方概率必须位于 0 到 1。", this);
			}
			if (slot.Entries.Count == 0)
			{
				context.AddError("CARD_PACK_ENTRIES_EMPTY", $"卡包 {ContentId} 的第 {slotIndex + 1} 个槽位缺少普通卡池。", this);
			}

			for (int entryIndex = 0; entryIndex < slot.Entries.Count; entryIndex++)
			{
				CardPackEntry entry = slot.Entries[entryIndex];
				if (entry == null || entry.Weight <= 0 || !context.TryGet(entry.CardId, out CardDefinition _))
				{
					context.AddError("CARD_PACK_ENTRY_INVALID", $"卡包 {ContentId} 的第 {slotIndex + 1} 个槽位包含无效普通卡牌或权重。", this);
				}
			}

			HashSet<ContentId> recipeActionIds = new HashSet<ContentId>();
			for (int recipeIndex = 0; recipeIndex < slot.RecipeEntries.Count; recipeIndex++)
			{
				CardPackRecipeEntry recipe = slot.RecipeEntries[recipeIndex];
				if (recipe == null ||
					!context.TryGet(recipe.ActionId, out ActionDefinition _) ||
					!context.TryGet(recipe.RecipeCardId, out CardDefinition _) ||
					!recipeActionIds.Add(recipe.ActionId))
				{
					context.AddError("CARD_PACK_RECIPE_ENTRY_INVALID", $"卡包 {ContentId} 的第 {slotIndex + 1} 个槽位包含无效或重复的配方候选。", this);
				}
			}
			if (slot.RecipeChance > 0f && slot.RecipeEntries.Count == 0)
			{
				context.AddError("CARD_PACK_RECIPE_POOL_EMPTY", $"卡包 {ContentId} 的第 {slotIndex + 1} 个槽位配置了配方概率但没有配方候选。", this);
			}
		}
	}
}
