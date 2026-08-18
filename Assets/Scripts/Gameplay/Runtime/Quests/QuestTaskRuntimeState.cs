using System;
using System.Collections.Generic;
using Gameplay.Content;
using Gameplay.Tabletop;

namespace Gameplay.Quests
{
	/// <summary>
	/// 任务子项对外可读的数值进度；复杂任务可以用多个子项表达，不把任务规则集中到这里。
	/// </summary>
	public readonly struct QuestTaskProgressSnapshot
	{
		public int CurrentAmount { get; }

		public int RequiredAmount { get; }

		public bool IsCompleted => CurrentAmount >= RequiredAmount;

		public QuestTaskProgressSnapshot(int currentAmount, int requiredAmount)
		{
			if (currentAmount < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(currentAmount));
			}
			if (requiredAmount <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(requiredAmount));
			}
			CurrentAmount = currentAmount;
			RequiredAmount = requiredAmount;
		}
	}

	/// <summary>
	/// 单局任务日志交给任务子项解释的已提交事实；它不是全局事件总线或第二份玩法状态。
	/// </summary>
	public abstract class QuestTaskFact
	{
	}

	/// <summary>
	/// 当前单局牌桌状态的快照事实，用于解释“拥有 / 食物 / 货币 / 容量”这类状态型任务。
	/// </summary>
	public sealed class TabletopStateQuestTaskFact : QuestTaskFact
	{
		private readonly ContentId[] m_cardContentIds;
		private readonly CurrencyStock[] m_storedCurrencyStocks;

		public TabletopStateQuestTaskFact(
			IReadOnlyList<ContentId> cardContentIds,
			int totalFoodNutrition,
			IReadOnlyList<CurrencyStock> storedCurrencyStocks,
			int cardCapacity)
		{
			if (cardContentIds == null)
			{
				throw new ArgumentNullException(nameof(cardContentIds));
			}
			if (totalFoodNutrition < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(totalFoodNutrition));
			}
			if (storedCurrencyStocks == null)
			{
				throw new ArgumentNullException(nameof(storedCurrencyStocks));
			}
			if (cardCapacity < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(cardCapacity));
			}

			m_cardContentIds = new ContentId[cardContentIds.Count];
			for (int i = 0; i < cardContentIds.Count; i++)
			{
				if (!cardContentIds[i].IsValid)
				{
					throw new ArgumentException(
						$"牌桌状态事实的第 {i + 1} 张卡牌内容 ID 无效。",
						nameof(cardContentIds));
				}
				m_cardContentIds[i] = cardContentIds[i];
			}

			m_storedCurrencyStocks = new CurrencyStock[storedCurrencyStocks.Count];
			for (int i = 0; i < storedCurrencyStocks.Count; i++)
			{
				CurrencyStock stock = storedCurrencyStocks[i];
				if (!stock.CurrencyCardId.IsValid || stock.Amount <= 0)
				{
					throw new ArgumentException(
						$"牌桌状态事实的第 {i + 1} 个箱内货币数量无效。",
						nameof(storedCurrencyStocks));
				}
				m_storedCurrencyStocks[i] = stock;
			}

			TotalFoodNutrition = totalFoodNutrition;
			CardCapacity = cardCapacity;
		}

		public int TotalFoodNutrition { get; }

		public int CardCapacity { get; }

		public int CountCards(ContentId cardId)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("统计牌桌卡牌数量必须使用有效内容 ID。", nameof(cardId));
			}

			int count = 0;
			for (int i = 0; i < m_cardContentIds.Length; i++)
			{
				if (m_cardContentIds[i] == cardId)
				{
					count++;
				}
			}
			return count;
		}

		public int GetCurrencyAmount(ContentId currencyCardId)
		{
			if (!currencyCardId.IsValid)
			{
				throw new ArgumentException("统计货币数量必须使用有效内容 ID。", nameof(currencyCardId));
			}

			int amount = CountCards(currencyCardId);
			for (int i = 0; i < m_storedCurrencyStocks.Length; i++)
			{
				if (m_storedCurrencyStocks[i].CurrencyCardId == currencyCardId)
				{
					amount = checked(amount + m_storedCurrencyStocks[i].Amount);
				}
			}
			return amount;
		}

		public readonly struct CurrencyStock
		{
			public ContentId CurrencyCardId { get; }

			public int Amount { get; }

			public CurrencyStock(ContentId currencyCardId, int amount)
			{
				if (!currencyCardId.IsValid)
				{
					throw new ArgumentException("箱内货币必须引用有效货币卡内容 ID。", nameof(currencyCardId));
				}
				if (amount <= 0)
				{
					throw new ArgumentOutOfRangeException(nameof(amount));
				}

				CurrencyCardId = currencyCardId;
				Amount = amount;
			}
		}
	}

	/// <summary>
	/// 普通牌桌行动成功结算后，所属剧本单局交给任务日志的行动完成事实。
	/// </summary>
	public sealed class ActionCompletedQuestTaskFact : QuestTaskFact
	{
		public ActionCompletedQuestTaskFact(ContentId actionId)
		{
			if (!actionId.IsValid)
			{
				throw new ArgumentException("行动完成事实必须引用有效内容 ID。", nameof(actionId));
			}

			ActionId = actionId;
		}

		public ContentId ActionId { get; }
	}

	/// <summary>
	/// 剧本单局跨入指定游戏日后交给任务日志的日期事实。
	/// </summary>
	public sealed class DayReachedQuestTaskFact : QuestTaskFact
	{
		public DayReachedQuestTaskFact(int currentDay)
		{
			if (currentDay <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(currentDay),
					currentDay,
					"到达天数必须大于 0。");
			}

			CurrentDay = currentDay;
		}

		public int CurrentDay { get; }
	}

	/// <summary>
	/// 剧本单局已确认某个内容进入当前发现集合后的事实。
	/// </summary>
	public sealed class ContentDiscoveredQuestTaskFact : QuestTaskFact
	{
		public ContentDiscoveredQuestTaskFact(ContentId contentId)
		{
			if (!contentId.IsValid)
			{
				throw new ArgumentException("内容发现事实必须引用有效内容 ID。", nameof(contentId));
			}

			ContentId = contentId;
		}

		public ContentId ContentId { get; }
	}

	/// <summary>卡包商贩完成一笔购买后产生的单局事实。</summary>
	public sealed class CardPackPurchasedQuestTaskFact : QuestTaskFact
	{
		public CardPackPurchasedQuestTaskFact(ContentId packId)
		{
			if (!packId.IsValid)
			{
				throw new ArgumentException("卡包购买事实必须引用有效内容 ID。", nameof(packId));
			}
			PackId = packId;
		}

		public ContentId PackId { get; }
	}

	/// <summary>一次出售行动成功提交后，记录被售卡牌内容 ID 的单局事实。</summary>
	public sealed class CardsSoldQuestTaskFact : QuestTaskFact
	{
		private readonly ContentId[] m_soldCardIds;

		public CardsSoldQuestTaskFact(IReadOnlyList<ContentId> soldCardIds)
		{
			if (soldCardIds == null)
			{
				throw new ArgumentNullException(nameof(soldCardIds));
			}
			if (soldCardIds.Count == 0)
			{
				throw new ArgumentException("出售事实必须至少包含一张已售卡牌。", nameof(soldCardIds));
			}

			m_soldCardIds = new ContentId[soldCardIds.Count];
			for (int i = 0; i < soldCardIds.Count; i++)
			{
				if (!soldCardIds[i].IsValid)
				{
					throw new ArgumentException(
						$"出售事实的第 {i + 1} 个卡牌内容 ID 无效。",
						nameof(soldCardIds));
				}
				m_soldCardIds[i] = soldCardIds[i];
			}
		}

		public IReadOnlyList<ContentId> SoldCardIds => m_soldCardIds;
	}

	/// <summary>一次行动成功提交后，记录由该行动生成的卡牌内容 ID。</summary>
	public sealed class CardsCreatedQuestTaskFact : QuestTaskFact
	{
		private readonly ContentId[] m_createdCardIds;

		public CardsCreatedQuestTaskFact(IReadOnlyList<ContentId> createdCardIds)
		{
			if (createdCardIds == null)
			{
				throw new ArgumentNullException(nameof(createdCardIds));
			}
			if (createdCardIds.Count == 0)
			{
				throw new ArgumentException("卡牌生成事实必须至少包含一张已生成卡牌。", nameof(createdCardIds));
			}

			m_createdCardIds = new ContentId[createdCardIds.Count];
			for (int i = 0; i < createdCardIds.Count; i++)
			{
				if (!createdCardIds[i].IsValid)
				{
					throw new ArgumentException(
						$"卡牌生成事实的第 {i + 1} 个内容 ID 无效。",
						nameof(createdCardIds));
				}
				m_createdCardIds[i] = createdCardIds[i];
			}
		}

		public IReadOnlyList<ContentId> CreatedCardIds => m_createdCardIds;
	}

	/// <summary>战斗死亡清理正式移除角色卡后，记录被击败卡牌内容 ID 的单局事实。</summary>
	public sealed class CardsDefeatedQuestTaskFact : QuestTaskFact
	{
		private readonly ContentId[] m_defeatedCardIds;

		public CardsDefeatedQuestTaskFact(IReadOnlyList<ContentId> defeatedCardIds)
		{
			if (defeatedCardIds == null)
			{
				throw new ArgumentNullException(nameof(defeatedCardIds));
			}
			if (defeatedCardIds.Count == 0)
			{
				throw new ArgumentException("击败事实必须至少包含一张被击败卡牌。", nameof(defeatedCardIds));
			}

			m_defeatedCardIds = new ContentId[defeatedCardIds.Count];
			for (int i = 0; i < defeatedCardIds.Count; i++)
			{
				if (!defeatedCardIds[i].IsValid)
				{
					throw new ArgumentException(
						$"击败事实的第 {i + 1} 个内容 ID 无效。",
						nameof(defeatedCardIds));
				}
				m_defeatedCardIds[i] = defeatedCardIds[i];
			}
		}

		public IReadOnlyList<ContentId> DefeatedCardIds => m_defeatedCardIds;
	}

	/// <summary>一次探索行动成功提交后，记录已探索区域或地点卡牌内容 ID 的单局事实。</summary>
	public sealed class CardsExploredQuestTaskFact : QuestTaskFact
	{
		private readonly ContentId[] m_exploredCardIds;

		public CardsExploredQuestTaskFact(IReadOnlyList<ContentId> exploredCardIds)
		{
			if (exploredCardIds == null)
			{
				throw new ArgumentNullException(nameof(exploredCardIds));
			}
			if (exploredCardIds.Count == 0)
			{
				throw new ArgumentException("探索事实必须至少包含一张已探索卡牌。", nameof(exploredCardIds));
			}

			m_exploredCardIds = new ContentId[exploredCardIds.Count];
			for (int i = 0; i < exploredCardIds.Count; i++)
			{
				if (!exploredCardIds[i].IsValid)
				{
					throw new ArgumentException(
						$"探索事实的第 {i + 1} 个内容 ID 无效。",
						nameof(exploredCardIds));
				}
				m_exploredCardIds[i] = exploredCardIds[i];
			}
		}

		public IReadOnlyList<ContentId> ExploredCardIds => m_exploredCardIds;
	}

	/// <summary>玩家切换当前单局普通行动推进模式后，交给任务日志的事实。</summary>
	public sealed class ProgressionModeChangedQuestTaskFact : QuestTaskFact
	{
		public ProgressionModeChangedQuestTaskFact(ActionProgressionMode mode)
		{
			if (!Enum.IsDefined(typeof(ActionProgressionMode), mode))
			{
				throw new ArgumentOutOfRangeException(nameof(mode), mode, "推进模式事实包含无效模式。");
			}

			Mode = mode;
		}

		public ActionProgressionMode Mode { get; }
	}

	/// <summary>装备行动成功提交后，记录被穿戴装备内容 ID 的单局事实。</summary>
	public sealed class CardEquippedQuestTaskFact : QuestTaskFact
	{
		public CardEquippedQuestTaskFact(ContentId equipmentCardId)
		{
			if (!equipmentCardId.IsValid)
			{
				throw new ArgumentException("装备事实必须引用有效装备卡内容 ID。", nameof(equipmentCardId));
			}

			EquipmentCardId = equipmentCardId;
		}

		public ContentId EquipmentCardId { get; }
	}

	/// <summary>
	/// 一个任务子项在当前单局中的可写进度。任务日志是唯一创建者和事实分发者。
	/// </summary>
	public abstract class QuestTaskRuntimeState
	{
		public bool IsCompleted => Progress.IsCompleted;

		public abstract QuestTaskProgressSnapshot Progress { get; }

		internal bool RecordFactFromQuestLog(QuestTaskFact fact)
		{
			return RecordFact(fact ?? throw new ArgumentNullException(nameof(fact)));
		}

		internal QuestTaskStateSnapshot CreateSnapshotForQuestLog()
		{
			QuestTaskStateSnapshot snapshot = CreateStateSnapshot();
			return snapshot ?? throw new InvalidOperationException(
				$"任务子项 {GetType().FullName} 返回了空存档状态。");
		}

		internal void RestoreSnapshotForQuestLog(QuestTaskStateSnapshot snapshot)
		{
			RestoreStateSnapshot(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
		}

		/// <summary>为自定义任务子项生成自己的可序列化状态。</summary>
		protected virtual QuestTaskStateSnapshot CreateStateSnapshot()
		{
			throw new InvalidOperationException(
				$"任务子项 {GetType().FullName} 没有实现存档状态创建，不能生成不完整任务快照。");
		}

		/// <summary>从自定义任务子项自己的状态恢复；类型不匹配时应直接报错。</summary>
		protected virtual void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
		{
			throw new InvalidOperationException(
				$"任务子项 {GetType().FullName} 没有实现存档状态恢复，不能静默丢失任务进度。");
		}

		/// <summary>解释所属单局已提交的任务事实；Mod 任务状态可覆盖这个入口。</summary>
		protected abstract bool RecordFact(QuestTaskFact fact);
	}
}
