using System;
using System.Collections.Generic;
using Gameplay.Actions;
using Gameplay.Content;

namespace Gameplay.Tabletop.Actions
{
	/// <summary>
	/// 从当前冻结内容集合推导 StackCraft 货币卡身份；不新增枚举、标签或第二份作者真相。
	/// </summary>
	internal static class CurrencyCardQuery
	{
		internal static bool IsCurrencyCard(ContentIndex contentIndex, ContentId contentId)
		{
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}
			if (!contentId.IsValid)
			{
				return false;
			}

			IReadOnlyList<ContentAsset> assets = contentIndex.AllAssets;
			for (int i = 0; i < assets.Count; i++)
			{
				if (DeclaresCurrencyCard(assets[i], contentId))
				{
					return true;
				}
			}
			return false;
		}

		internal static HashSet<ContentId> BuildCurrencyCardIds(ContentIndex contentIndex)
		{
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}

			HashSet<ContentId> currencyCardIds = new HashSet<ContentId>();
			IReadOnlyList<ContentAsset> assets = contentIndex.AllAssets;
			for (int i = 0; i < assets.Count; i++)
			{
				AddCurrencyCardIds(assets[i], currencyCardIds);
			}
			return currencyCardIds;
		}

		private static bool DeclaresCurrencyCard(ContentAsset asset, ContentId contentId)
		{
			switch (asset)
			{
				case CardBuyerDefinition buyer:
					return buyer.CurrencyCardId == contentId;
				case ChestCardDefinition chest:
					return chest.CurrencyCardId == contentId;
				case ActionDefinition action:
					return ContainsCurrencyCardId(action.ResultIntents, contentId) ||
						ContainsCurrencyCardId(action.ResultBranches, contentId);
				default:
					return false;
			}
		}

		private static void AddCurrencyCardIds(ContentAsset asset, ISet<ContentId> currencyCardIds)
		{
			switch (asset)
			{
				case CardBuyerDefinition buyer:
					AddCurrencyCardId(currencyCardIds, buyer.CurrencyCardId);
					break;
				case ChestCardDefinition chest:
					AddCurrencyCardId(currencyCardIds, chest.CurrencyCardId);
					break;
				case ActionDefinition action:
					AddCurrencyCardIds(action.ResultIntents, currencyCardIds);
					IReadOnlyList<ActionResultBranchDefinition> branches = action.ResultBranches;
					for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
					{
						if (branches[branchIndex] != null)
						{
							AddCurrencyCardIds(branches[branchIndex].ResultIntents, currencyCardIds);
						}
					}
					break;
			}
		}

		private static bool ContainsCurrencyCardId(
			IReadOnlyList<ActionResultIntent> resultIntents,
			ContentId contentId)
		{
			for (int intentIndex = 0; intentIndex < resultIntents.Count; intentIndex++)
			{
				if (resultIntents[intentIndex] is SellCardsResultIntent sellIntent &&
					sellIntent.CurrencyCardId == contentId)
				{
					return true;
				}
			}
			return false;
		}

		private static bool ContainsCurrencyCardId(
			IReadOnlyList<ActionResultBranchDefinition> branches,
			ContentId contentId)
		{
			for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
			{
				ActionResultBranchDefinition branch = branches[branchIndex];
				if (branch != null && ContainsCurrencyCardId(branch.ResultIntents, contentId))
				{
					return true;
				}
			}
			return false;
		}

		private static void AddCurrencyCardIds(
			IReadOnlyList<ActionResultIntent> resultIntents,
			ISet<ContentId> currencyCardIds)
		{
			for (int intentIndex = 0; intentIndex < resultIntents.Count; intentIndex++)
			{
				if (resultIntents[intentIndex] is SellCardsResultIntent sellIntent)
				{
					AddCurrencyCardId(currencyCardIds, sellIntent.CurrencyCardId);
				}
			}
		}

		private static void AddCurrencyCardId(ISet<ContentId> currencyCardIds, ContentId contentId)
		{
			if (contentId.IsValid)
			{
				currencyCardIds.Add(contentId);
			}
		}
	}
}
