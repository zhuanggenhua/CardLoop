using System;
using System.Collections.Generic;
using Gameplay.Content;
using Gameplay.Tabletop;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Quests
{
	/// <summary>累计购买卡包次数的任务子项，可限定指定卡包或统计任意卡包。</summary>
	[Serializable]
	public sealed class CardPackPurchaseQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardPackDefinition)), LabelText("要求购买卡包")]
		[Tooltip("留空时统计任意已购买卡包；填写时只统计指定卡包内容。")]
		private ContentId m_packId;

		[SerializeField, Min(1), LabelText("购买次数")]
		private int m_requiredPurchaseCount = 1;

		public ContentId PackId => m_packId;

		public int RequiredPurchaseCount => m_requiredPurchaseCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (PackId.IsValid && !context.Content.TryGet(PackId, out CardPackDefinition _))
			{
				context.AddError("QUEST_PACK_PURCHASE_PACK_INVALID", $"任务 {context.Quest.ContentId} 引用了无效购买卡包 {PackId}。");
			}
			if (RequiredPurchaseCount <= 0)
			{
				context.AddError("QUEST_PACK_PURCHASE_COUNT_INVALID", $"任务 {context.Quest.ContentId} 的卡包购买次数必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardPackPurchaseQuestTaskDefinition m_definition;
			private int m_purchaseCount;

			internal RuntimeState(CardPackPurchaseQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_purchaseCount, m_definition.RequiredPurchaseCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not CardPackPurchasedQuestTaskFact purchase ||
					(m_definition.PackId.IsValid && purchase.PackId != m_definition.PackId))
				{
					return false;
				}
				m_purchaseCount = checked(m_purchaseCount + 1);
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_purchaseCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredPurchaseCount)
				{
					throw new InvalidOperationException("卡包购买任务子项的存档进度无效。");
				}
				m_purchaseCount = amount.CurrentAmount;
			}
		}
	}

	/// <summary>根据当前牌桌状态统计指定卡牌拥有数量的任务子项。</summary>
	[Serializable]
	public sealed class CardPossessionQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("要求拥有卡牌")]
		private ContentId m_cardId;

		[SerializeField, Min(1), LabelText("拥有数量")]
		private int m_requiredCardCount = 1;

		public ContentId CardId => m_cardId;

		public int RequiredCardCount => m_requiredCardCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!CardId.IsValid || !context.Content.TryGet(CardId, out CardDefinition _))
			{
				context.AddError(
					"QUEST_CARD_POSSESSION_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效拥有卡牌 {CardId}。");
			}
			if (RequiredCardCount <= 0)
			{
				context.AddError(
					"QUEST_CARD_POSSESSION_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的拥有数量必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardPossessionQuestTaskDefinition m_definition;
			private int m_currentCount;

			internal RuntimeState(CardPossessionQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_currentCount, m_definition.RequiredCardCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not TabletopStateQuestTaskFact state)
				{
					return false;
				}

				int nextCount = Math.Min(
					m_definition.RequiredCardCount,
					state.CountCards(m_definition.CardId));
				if (nextCount == m_currentCount)
				{
					return false;
				}

				m_currentCount = nextCount;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_currentCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredCardCount)
				{
					throw new InvalidOperationException("拥有卡牌任务子项的存档进度无效。");
				}
				m_currentCount = amount.CurrentAmount;
			}
		}
	}

	/// <summary>根据当前牌桌食物卡剩余使用次数统计总营养的任务子项。</summary>
	[Serializable]
	public sealed class FoodNutritionQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, Min(1), LabelText("要求食物营养")]
		private int m_requiredNutrition = 1;

		public int RequiredNutrition => m_requiredNutrition;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (RequiredNutrition <= 0)
			{
				context.AddError(
					"QUEST_FOOD_NUTRITION_AMOUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的食物营养要求必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly FoodNutritionQuestTaskDefinition m_definition;
			private int m_currentNutrition;

			internal RuntimeState(FoodNutritionQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_currentNutrition, m_definition.RequiredNutrition);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not TabletopStateQuestTaskFact state)
				{
					return false;
				}

				int nextNutrition = Math.Min(
					m_definition.RequiredNutrition,
					state.TotalFoodNutrition);
				if (nextNutrition == m_currentNutrition)
				{
					return false;
				}

				m_currentNutrition = nextNutrition;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_currentNutrition);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredNutrition)
				{
					throw new InvalidOperationException("食物营养任务子项的存档进度无效。");
				}
				m_currentNutrition = amount.CurrentAmount;
			}
		}
	}

	/// <summary>根据当前牌桌和箱内存币统计指定货币数量的任务子项。</summary>
	[Serializable]
	public sealed class CurrencyAmountQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("要求货币卡")]
		private ContentId m_currencyCardId;

		[SerializeField, Min(1), LabelText("货币数量")]
		private int m_requiredAmount = 1;

		public ContentId CurrencyCardId => m_currencyCardId;

		public int RequiredAmount => m_requiredAmount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!CurrencyCardId.IsValid || !context.Content.TryGet(CurrencyCardId, out CardDefinition _))
			{
				context.AddError(
					"QUEST_CURRENCY_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效货币卡 {CurrencyCardId}。");
			}
			if (RequiredAmount <= 0)
			{
				context.AddError(
					"QUEST_CURRENCY_AMOUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的货币数量必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CurrencyAmountQuestTaskDefinition m_definition;
			private int m_currentAmount;

			internal RuntimeState(CurrencyAmountQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_currentAmount, m_definition.RequiredAmount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not TabletopStateQuestTaskFact state)
				{
					return false;
				}

				int nextAmount = Math.Min(
					m_definition.RequiredAmount,
					state.GetCurrencyAmount(m_definition.CurrencyCardId));
				if (nextAmount == m_currentAmount)
				{
					return false;
				}

				m_currentAmount = nextAmount;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_currentAmount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredAmount)
				{
					throw new InvalidOperationException("货币数量任务子项的存档进度无效。");
				}
				m_currentAmount = amount.CurrentAmount;
			}
		}
	}

	/// <summary>根据当前剧本日终规则和牌桌上限加成统计卡牌容量的任务子项。</summary>
	[Serializable]
	public sealed class CardCapacityQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, Min(1), LabelText("要求卡牌容量")]
		private int m_requiredCapacity = 1;

		public int RequiredCapacity => m_requiredCapacity;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (RequiredCapacity <= 0)
			{
				context.AddError(
					"QUEST_CARD_CAPACITY_AMOUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的卡牌容量要求必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardCapacityQuestTaskDefinition m_definition;
			private int m_currentCapacity;

			internal RuntimeState(CardCapacityQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_currentCapacity, m_definition.RequiredCapacity);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not TabletopStateQuestTaskFact state)
				{
					return false;
				}

				int nextCapacity = Math.Min(
					m_definition.RequiredCapacity,
					state.CardCapacity);
				if (nextCapacity == m_currentCapacity)
				{
					return false;
				}

				m_currentCapacity = nextCapacity;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_currentCapacity);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredCapacity)
				{
					throw new InvalidOperationException("卡牌容量任务子项的存档进度无效。");
				}
				m_currentCapacity = amount.CurrentAmount;
			}
		}
	}

	/// <summary>累计成功出售卡牌数量的任务子项，可限定指定卡牌或统计任意卡牌。</summary>
	[Serializable]
	public sealed class CardSaleQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("要求出售卡牌")]
		[Tooltip("留空时统计任意已售卡牌；填写时只统计指定卡牌内容。")]
		private ContentId m_cardId;

		[SerializeField, Min(1), LabelText("出售数量")]
		private int m_requiredSoldCount = 1;

		public ContentId CardId => m_cardId;

		public int RequiredSoldCount => m_requiredSoldCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (CardId.IsValid && !context.Content.TryGet(CardId, out CardDefinition _))
			{
				context.AddError(
					"QUEST_CARD_SALE_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效出售卡牌 {CardId}。");
			}
			if (RequiredSoldCount <= 0)
			{
				context.AddError(
					"QUEST_CARD_SALE_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的出售数量必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardSaleQuestTaskDefinition m_definition;
			private int m_soldCount;

			internal RuntimeState(CardSaleQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_soldCount, m_definition.RequiredSoldCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not CardsSoldQuestTaskFact sale)
				{
					return false;
				}

				int matchedCount = CountMatchingSoldCards(sale.SoldCardIds);
				if (matchedCount == 0)
				{
					return false;
				}
				m_soldCount = Math.Min(
					m_definition.RequiredSoldCount,
					checked(m_soldCount + matchedCount));
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_soldCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredSoldCount)
				{
					throw new InvalidOperationException("出售任务子项的存档进度无效。");
				}
				m_soldCount = amount.CurrentAmount;
			}

			private int CountMatchingSoldCards(IReadOnlyList<ContentId> soldCardIds)
			{
				int count = 0;
				for (int i = 0; i < soldCardIds.Count; i++)
				{
					if (!m_definition.CardId.IsValid || soldCardIds[i] == m_definition.CardId)
					{
						count++;
					}
				}
				return count;
			}
		}
	}

	/// <summary>累计行动成功生成指定卡牌数量的任务子项。</summary>
	[Serializable]
	public sealed class CardCreationQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("要求生成卡牌")]
		private ContentId m_cardId;

		[SerializeField, Min(1), LabelText("生成数量")]
		private int m_requiredCreatedCount = 1;

		public ContentId CardId => m_cardId;

		public int RequiredCreatedCount => m_requiredCreatedCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!CardId.IsValid || !context.Content.TryGet(CardId, out CardDefinition _))
			{
				context.AddError(
					"QUEST_CARD_CREATION_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效生成卡牌 {CardId}。");
			}
			if (RequiredCreatedCount <= 0)
			{
				context.AddError(
					"QUEST_CARD_CREATION_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的生成数量必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardCreationQuestTaskDefinition m_definition;
			private int m_createdCount;

			internal RuntimeState(CardCreationQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_createdCount, m_definition.RequiredCreatedCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not CardsCreatedQuestTaskFact creation)
				{
					return false;
				}

				int matchedCount = CountMatchingCreatedCards(creation.CreatedCardIds);
				if (matchedCount == 0)
				{
					return false;
				}
				m_createdCount = Math.Min(
					m_definition.RequiredCreatedCount,
					checked(m_createdCount + matchedCount));
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_createdCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredCreatedCount)
				{
					throw new InvalidOperationException("生成卡牌任务子项的存档进度无效。");
				}
				m_createdCount = amount.CurrentAmount;
			}

			private int CountMatchingCreatedCards(IReadOnlyList<ContentId> createdCardIds)
			{
				int count = 0;
				for (int i = 0; i < createdCardIds.Count; i++)
				{
					if (createdCardIds[i] == m_definition.CardId)
					{
						count++;
					}
				}
				return count;
			}
		}
	}

	/// <summary>累计战斗中正式击败指定卡牌数量的任务子项。</summary>
	[Serializable]
	public sealed class CardDefeatQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("要求击败卡牌")]
		[Tooltip("只统计战斗死亡清理链正式移除的指定卡牌内容；普通移除、出售或旅行不算击败。")]
		private ContentId m_cardId;

		[SerializeField, Min(1), LabelText("击败数量")]
		[Tooltip("本任务子项需要累计的指定卡牌击败次数。必须大于 0。")]
		private int m_requiredDefeatCount = 1;

		public ContentId CardId => m_cardId;

		public int RequiredDefeatCount => m_requiredDefeatCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!CardId.IsValid || !context.Content.TryGet(CardId, out CardDefinition _))
			{
				context.AddError(
					"QUEST_CARD_DEFEAT_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效击败卡牌 {CardId}。");
			}
			if (RequiredDefeatCount <= 0)
			{
				context.AddError(
					"QUEST_CARD_DEFEAT_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的击败数量必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardDefeatQuestTaskDefinition m_definition;
			private int m_defeatedCount;

			internal RuntimeState(CardDefeatQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_defeatedCount, m_definition.RequiredDefeatCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not CardsDefeatedQuestTaskFact defeat)
				{
					return false;
				}

				int matchedCount = CountMatchingDefeatedCards(defeat.DefeatedCardIds);
				if (matchedCount == 0)
				{
					return false;
				}
				m_defeatedCount = Math.Min(
					m_definition.RequiredDefeatCount,
					checked(m_defeatedCount + matchedCount));
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_defeatedCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredDefeatCount)
				{
					throw new InvalidOperationException("击败任务子项的存档进度无效。");
				}
				m_defeatedCount = amount.CurrentAmount;
			}

			private int CountMatchingDefeatedCards(IReadOnlyList<ContentId> defeatedCardIds)
			{
				int count = 0;
				for (int i = 0; i < defeatedCardIds.Count; i++)
				{
					if (defeatedCardIds[i] == m_definition.CardId)
					{
						count++;
					}
				}
				return count;
			}
		}
	}

	/// <summary>累计成功探索指定区域或地点卡牌次数的任务子项。</summary>
	[Serializable]
	public sealed class CardExplorationQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(CardDefinition)), LabelText("要求探索卡牌")]
		[Tooltip("只统计行动结果明确声明为已探索的指定区域或地点卡牌内容。")]
		private ContentId m_cardId;

		[SerializeField, Min(1), LabelText("探索次数")]
		[Tooltip("本任务子项需要累计的指定卡牌探索次数。必须大于 0。")]
		private int m_requiredExplorationCount = 1;

		public ContentId CardId => m_cardId;

		public int RequiredExplorationCount => m_requiredExplorationCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!CardId.IsValid || !context.Content.TryGet(CardId, out CardDefinition _))
			{
				context.AddError(
					"QUEST_CARD_EXPLORATION_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效探索卡牌 {CardId}。");
			}
			if (RequiredExplorationCount <= 0)
			{
				context.AddError(
					"QUEST_CARD_EXPLORATION_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的探索次数必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardExplorationQuestTaskDefinition m_definition;
			private int m_explorationCount;

			internal RuntimeState(CardExplorationQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_explorationCount, m_definition.RequiredExplorationCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not CardsExploredQuestTaskFact exploration)
				{
					return false;
				}

				int matchedCount = CountMatchingExploredCards(exploration.ExploredCardIds);
				if (matchedCount == 0)
				{
					return false;
				}
				m_explorationCount = Math.Min(
					m_definition.RequiredExplorationCount,
					checked(m_explorationCount + matchedCount));
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_explorationCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredExplorationCount)
				{
					throw new InvalidOperationException("探索任务子项的存档进度无效。");
				}
				m_explorationCount = amount.CurrentAmount;
			}

			private int CountMatchingExploredCards(IReadOnlyList<ContentId> exploredCardIds)
			{
				int count = 0;
				for (int i = 0; i < exploredCardIds.Count; i++)
				{
					if (exploredCardIds[i] == m_definition.CardId)
					{
						count++;
					}
				}
				return count;
			}
		}
	}

	/// <summary>在玩家把当前单局普通行动推进模式切到指定状态后完成的任务子项。</summary>
	[Serializable]
	public sealed class ProgressionModeQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, LabelText("目标推进模式")]
		[Tooltip("玩家主动切换到该推进模式时完成任务；不会因为单局初始默认模式自动完成。")]
		private ActionProgressionMode m_targetMode = ActionProgressionMode.RealTime;

		public ActionProgressionMode TargetMode => m_targetMode;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!Enum.IsDefined(typeof(ActionProgressionMode), TargetMode))
			{
				context.AddError(
					"QUEST_PROGRESSION_MODE_INVALID",
					$"任务 {context.Quest.ContentId} 的目标推进模式无效：{TargetMode}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly ProgressionModeQuestTaskDefinition m_definition;
			private bool m_isReached;

			internal RuntimeState(ProgressionModeQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_isReached ? 1 : 0, 1);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not ProgressionModeChangedQuestTaskFact progression ||
					progression.Mode != m_definition.TargetMode)
				{
					return false;
				}

				m_isReached = true;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_isReached ? 1 : 0);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					(amount.CurrentAmount != 0 && amount.CurrentAmount != 1))
				{
					throw new InvalidOperationException("推进模式任务子项的存档进度无效。");
				}
				m_isReached = amount.CurrentAmount == 1;
			}
		}
	}

	/// <summary>累计装备指定装备卡次数的任务子项。</summary>
	[Serializable]
	public sealed class CardEquipQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField, ContentIdReference(typeof(EquipmentCardDefinition)), LabelText("要求装备卡牌")]
		private ContentId m_equipmentCardId;

		[SerializeField, Min(1), LabelText("装备次数")]
		private int m_requiredEquipCount = 1;

		public ContentId EquipmentCardId => m_equipmentCardId;

		public int RequiredEquipCount => m_requiredEquipCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!EquipmentCardId.IsValid || !context.Content.TryGet(EquipmentCardId, out EquipmentCardDefinition _))
			{
				context.AddError(
					"QUEST_CARD_EQUIP_CARD_INVALID",
					$"任务 {context.Quest.ContentId} 引用了无效装备卡 {EquipmentCardId}。");
			}
			if (RequiredEquipCount <= 0)
			{
				context.AddError(
					"QUEST_CARD_EQUIP_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的装备次数必须大于 0。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new RuntimeState(this);
		}

		private sealed class RuntimeState : QuestTaskRuntimeState
		{
			private readonly CardEquipQuestTaskDefinition m_definition;
			private int m_equipCount;

			internal RuntimeState(CardEquipQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress =>
				new QuestTaskProgressSnapshot(m_equipCount, m_definition.RequiredEquipCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not CardEquippedQuestTaskFact equipped ||
					equipped.EquipmentCardId != m_definition.EquipmentCardId)
				{
					return false;
				}
				m_equipCount = Math.Min(
					m_definition.RequiredEquipCount,
					checked(m_equipCount + 1));
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_equipCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 || amount.CurrentAmount > m_definition.RequiredEquipCount)
				{
					throw new InvalidOperationException("装备任务子项的存档进度无效。");
				}
				m_equipCount = amount.CurrentAmount;
			}
		}
	}

	/// <summary>
	/// 任务子项的作者校验上下文，提供所属任务和活动内容查询。
	/// </summary>
	public sealed class QuestTaskValidationContext
	{
		public QuestDefinition Quest { get; }

		public ContentValidationContext Content { get; }

		internal QuestTaskValidationContext(
			QuestDefinition quest,
			ContentValidationContext content)
		{
			Quest = quest ?? throw new ArgumentNullException(nameof(quest));
			Content = content ?? throw new ArgumentNullException(nameof(content));
		}

		public void AddError(string code, string message)
		{
			Content.AddError(code, message, Quest);
		}
	}

	/// <summary>
	/// 任务子项作者声明的多态基类。
	/// </summary>
	[Serializable]
	public abstract class QuestTaskDefinition
	{
		internal void ValidateTask(QuestTaskValidationContext context)
		{
			ValidateDefinition(context ?? throw new ArgumentNullException(nameof(context)));
		}

		internal QuestTaskRuntimeState CreateRuntimeStateForQuestLog()
		{
			QuestTaskRuntimeState state = CreateRuntimeState();
			if (state == null)
			{
				throw new InvalidOperationException(
					$"任务子项类型 {GetType().FullName} 没有创建运行时进度状态。");
			}

			return state;
		}

		/// <summary>校验当前任务子项的作者数据；Mod 子项可覆盖该入口。</summary>
		protected virtual void ValidateDefinition(QuestTaskValidationContext context)
		{
		}

		/// <summary>创建当前单局使用的进度状态；Mod 子项必须通过该入口接入任务日志。</summary>
		protected abstract QuestTaskRuntimeState CreateRuntimeState();
	}

	/// <summary>
	/// 通过已提交的行动完成事实累计进度的任务子项。
	/// </summary>
	[Serializable]
	public sealed class ActionCompletionQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField]
		[ContentIdReference(typeof(Gameplay.Actions.ActionDefinition))]
		[LabelText("要求行动")]
		[Tooltip("需要成功完成的具体行动。编辑器自动保存其唯一内容 ID；它不是行动类型枚举，也不替代行动自身的 EX-GAS 标签。")]
		private ContentId m_actionId;

		[SerializeField]
		[Min(1f)]
		[LabelText("完成次数")]
		[Tooltip("本任务子项需要累计的成功行动次数。必须大于 0。")]
		private int m_requiredCompletionCount = 1;

		public ContentId ActionId => m_actionId;

		public int RequiredCompletionCount => m_requiredCompletionCount;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!ActionId.IsValid)
			{
				context.AddError(
					"QUEST_ACTION_TASK_ACTION_INVALID",
					$"任务 {context.Quest.ContentId} 的行动完成子项引用了无效行动 ID：{ActionId}。");
			}
			else if (!context.Content.TryGet(ActionId, out ContentAsset actionAsset))
			{
				context.AddError(
					"QUEST_ACTION_TASK_ACTION_UNKNOWN",
					$"任务 {context.Quest.ContentId} 的行动完成子项引用了不存在的行动 {ActionId}。");
			}
			else if (actionAsset is not Gameplay.Actions.ActionDefinition)
			{
				context.AddError(
					"QUEST_ACTION_TASK_ACTION_TYPE_INVALID",
					$"任务 {context.Quest.ContentId} 的行动完成子项引用的内容 {ActionId} 不是行动定义。");
			}

			if (RequiredCompletionCount <= 0)
			{
				context.AddError(
					"QUEST_ACTION_TASK_COUNT_INVALID",
					$"任务 {context.Quest.ContentId} 的行动完成次数必须大于 0，当前值为 {RequiredCompletionCount}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new ActionCompletionQuestTaskRuntimeState(this);
		}

		private sealed class ActionCompletionQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly ActionCompletionQuestTaskDefinition m_definition;

			private int m_completedCount;

			internal ActionCompletionQuestTaskRuntimeState(ActionCompletionQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_completedCount,
				m_definition.RequiredCompletionCount);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not ActionCompletedQuestTaskFact actionCompleted ||
					actionCompleted.ActionId != m_definition.ActionId)
				{
					return false;
				}

				checked
				{
					m_completedCount++;
					return true;
				}
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_completedCount);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					amount.CurrentAmount < 0 ||
					amount.CurrentAmount > m_definition.RequiredCompletionCount)
				{
					throw new InvalidOperationException("行动完成任务子项的存档进度无效。");
				}
				m_completedCount = amount.CurrentAmount;
			}
		}
	}

	/// <summary>
	/// 在剧本单局到达指定游戏日后完成的任务子项。
	/// </summary>
	[Serializable]
	public sealed class DayReachedQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField]
		[Min(1f)]
		[LabelText("要求到达天数")]
		[Tooltip("当前单局首次到达这个游戏日时完成子项。游戏日由剧本的每日确认回合数和总确认回合推导。")]
		private int m_requiredDay = 1;

		public int RequiredDay => m_requiredDay;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (RequiredDay <= 0)
			{
				context.AddError(
					"QUEST_DAY_TASK_DAY_INVALID",
					$"任务 {context.Quest.ContentId} 的要求到达天数必须大于 0，当前值为 {RequiredDay}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new DayReachedQuestTaskRuntimeState(this);
		}

		private sealed class DayReachedQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly DayReachedQuestTaskDefinition m_definition;

			private int m_currentDay;

			internal DayReachedQuestTaskRuntimeState(DayReachedQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_currentDay,
				m_definition.RequiredDay);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not DayReachedQuestTaskFact dayReached)
				{
					return false;
				}

				bool reachedRequiredDay = dayReached.CurrentDay >= m_definition.RequiredDay;
				m_currentDay = dayReached.CurrentDay;
				return reachedRequiredDay;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_currentDay);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount || amount.CurrentAmount < 0)
				{
					throw new InvalidOperationException("到达天数任务子项的存档进度无效。");
				}
				m_currentDay = amount.CurrentAmount;
			}
		}
	}

	/// <summary>
	/// 在当前剧本单局发现指定内容后完成的任务子项。
	/// </summary>
	[Serializable]
	public sealed class ContentDiscoveryQuestTaskDefinition : QuestTaskDefinition
	{
		[SerializeField]
		[ContentIdReference]
		[LabelText("要求发现内容")]
		[Tooltip("当前剧本单局必须已经发现的具体内容。选择器只保存唯一内容 ID；发现状态由剧本单局持有。")]
		private ContentId m_discoveredContentId;

		public ContentId DiscoveredContentId => m_discoveredContentId;

		protected override void ValidateDefinition(QuestTaskValidationContext context)
		{
			if (!DiscoveredContentId.IsValid)
			{
				context.AddError(
					"QUEST_DISCOVERY_TASK_CONTENT_INVALID",
					$"任务 {context.Quest.ContentId} 的发现内容子项引用了无效内容 ID：{DiscoveredContentId}。");
			}
			else if (!context.Content.TryGet(DiscoveredContentId, out ContentAsset _))
			{
				context.AddError(
					"QUEST_DISCOVERY_TASK_CONTENT_UNKNOWN",
					$"任务 {context.Quest.ContentId} 的发现内容子项引用了不存在的内容 {DiscoveredContentId}。");
			}
		}

		protected override QuestTaskRuntimeState CreateRuntimeState()
		{
			return new ContentDiscoveryQuestTaskRuntimeState(this);
		}

		private sealed class ContentDiscoveryQuestTaskRuntimeState : QuestTaskRuntimeState
		{
			private readonly ContentDiscoveryQuestTaskDefinition m_definition;

			private bool m_hasDiscoveredContent;

			internal ContentDiscoveryQuestTaskRuntimeState(ContentDiscoveryQuestTaskDefinition definition)
			{
				m_definition = definition ?? throw new ArgumentNullException(nameof(definition));
			}

			public override QuestTaskProgressSnapshot Progress => new QuestTaskProgressSnapshot(
				m_hasDiscoveredContent ? 1 : 0,
				1);

			protected override bool RecordFact(QuestTaskFact fact)
			{
				if (IsCompleted || fact is not ContentDiscoveredQuestTaskFact contentDiscovered ||
					contentDiscovered.ContentId != m_definition.DiscoveredContentId)
				{
					return false;
				}

				m_hasDiscoveredContent = true;
				return true;
			}

			protected override QuestTaskStateSnapshot CreateStateSnapshot()
			{
				return new QuestTaskAmountStateSnapshot(m_hasDiscoveredContent ? 1 : 0);
			}

			protected override void RestoreStateSnapshot(QuestTaskStateSnapshot snapshot)
			{
				if (snapshot is not QuestTaskAmountStateSnapshot amount ||
					(amount.CurrentAmount != 0 && amount.CurrentAmount != 1))
				{
					throw new InvalidOperationException("内容发现任务子项的存档进度无效。");
				}
				m_hasDiscoveredContent = amount.CurrentAmount == 1;
			}
		}
	}
}
