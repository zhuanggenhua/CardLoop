using System;
using System.Collections.Generic;

namespace Gameplay.Tabletop
{
	/// <summary>单局牌桌内一场战斗的运行时身份，不是内容 ID 或作者字段。</summary>
	public readonly struct BattleId : IEquatable<BattleId>
	{
		public ulong Value { get; }

		public bool IsValid => Value != 0;

		internal BattleId(ulong value)
		{
			Value = value;
		}

		public bool Equals(BattleId other) => Value == other.Value;

		public override bool Equals(object obj) => obj is BattleId other && Equals(other);

		public override int GetHashCode() => Value.GetHashCode();

		public override string ToString() => Value.ToString();

		public static bool operator ==(BattleId left, BattleId right) => left.Equals(right);

		public static bool operator !=(BattleId left, BattleId right) => !left.Equals(right);
	}

	/// <summary>
	/// 一场战斗中的临时作战方。它只拥有本场分组，不复制角色的 GAS 阵营或关系标签。
	/// </summary>
	public sealed class BattleSide
	{
		private readonly List<TabletopCardId> m_cardIds;
		private readonly IReadOnlyList<TabletopCardId> m_readOnlyCardIds;

		internal BattleSide(IReadOnlyList<TabletopCardId> cardIds)
		{
			if (cardIds == null)
			{
				throw new ArgumentNullException(nameof(cardIds));
			}
			if (cardIds.Count == 0)
			{
				throw new InvalidOperationException("战斗方至少需要一张参战卡牌。");
			}

			m_cardIds = new List<TabletopCardId>(cardIds.Count);
			HashSet<TabletopCardId> uniqueCardIds = new HashSet<TabletopCardId>();
			for (int index = 0; index < cardIds.Count; index++)
			{
				TabletopCardId cardId = cardIds[index];
				if (!cardId.IsValid)
				{
					throw new InvalidOperationException($"战斗方的第 {index + 1} 张参战卡牌 ID 无效。");
				}
				if (!uniqueCardIds.Add(cardId))
				{
					throw new InvalidOperationException($"战斗方重复包含牌桌卡牌 {cardId}。");
				}
				m_cardIds.Add(cardId);
			}

			m_readOnlyCardIds = m_cardIds.AsReadOnly();
		}

		public IReadOnlyList<TabletopCardId> CardIds => m_readOnlyCardIds;

		public int ParticipantCount => m_cardIds.Count;

		public bool Contains(TabletopCardId cardId) => m_cardIds.Contains(cardId);

		internal void Remove(TabletopCardId cardId)
		{
			if (!m_cardIds.Remove(cardId))
			{
				throw new KeyNotFoundException($"战斗方中不存在牌桌卡牌 {cardId}。");
			}
		}

		internal void Clear()
		{
			m_cardIds.Clear();
		}
	}

	/// <summary>
	/// 牌桌上的即时战斗聚合，只拥有战斗方、参战关系和结束生命周期。
	/// 角色阵营与敌我关系由角色 GAS 状态和剧本关系规则解析，不复制进本对象。
	/// </summary>
	public sealed class Battle
	{
		private readonly List<BattleSide> m_sides;
		private readonly IReadOnlyList<BattleSide> m_readOnlySides;
		private Unity.Mathematics.Random m_authoritativeRandom;

		internal Battle(
			BattleId id,
			IReadOnlyList<IReadOnlyList<TabletopCardId>> sideRosters,
			uint authoritativeRandomSeed)
		{
			if (!id.IsValid)
			{
				throw new ArgumentException("战斗必须拥有有效的单局战斗 ID。", nameof(id));
			}
			if (sideRosters == null)
			{
				throw new ArgumentNullException(nameof(sideRosters));
			}
			if (sideRosters.Count < 2)
			{
				throw new InvalidOperationException("即时战斗至少需要两个战斗方。");
			}
			if (authoritativeRandomSeed == 0u)
			{
				throw new ArgumentOutOfRangeException(
					nameof(authoritativeRandomSeed),
					"战斗权威随机种子不能为 0。");
			}

			m_sides = new List<BattleSide>(sideRosters.Count);
			HashSet<TabletopCardId> allCardIds = new HashSet<TabletopCardId>();
			for (int sideIndex = 0; sideIndex < sideRosters.Count; sideIndex++)
			{
				BattleSide side = new BattleSide(sideRosters[sideIndex]);
				for (int cardIndex = 0; cardIndex < side.CardIds.Count; cardIndex++)
				{
					TabletopCardId cardId = side.CardIds[cardIndex];
					if (!allCardIds.Add(cardId))
					{
						throw new InvalidOperationException(
							$"牌桌卡牌 {cardId} 同时出现在多个战斗方中。");
					}
				}
				m_sides.Add(side);
			}

			m_readOnlySides = m_sides.AsReadOnly();
			m_authoritativeRandom = new Unity.Mathematics.Random(authoritativeRandomSeed);
			Id = id;
		}

		public BattleId Id { get; }

		public IReadOnlyList<BattleSide> Sides => m_readOnlySides;

		public int SideCount => m_sides.Count;

		public int ActiveSideCount
		{
			get
			{
				int count = 0;
				for (int index = 0; index < m_sides.Count; index++)
				{
					if (m_sides[index].ParticipantCount > 0)
					{
						count++;
					}
				}
				return count;
			}
		}

		public int ParticipantCount
		{
			get
			{
				int count = 0;
				for (int index = 0; index < m_sides.Count; index++)
				{
					count += m_sides[index].ParticipantCount;
				}
				return count;
			}
		}

		public bool IsEnded { get; private set; }

		public bool HasParticipant(TabletopCardId cardId) => TryGetSide(cardId, out _);

		/// <summary>为一次成功提交的 Ability 激活取得独立权威种子。</summary>
		internal uint TakeAbilityActivationSeed()
		{
			RequireActive();
			return m_authoritativeRandom.NextUInt(1u, uint.MaxValue);
		}

		public bool TryGetSide(TabletopCardId cardId, out BattleSide side)
		{
			for (int index = 0; index < m_sides.Count; index++)
			{
				if (m_sides[index].Contains(cardId))
				{
					side = m_sides[index];
					return true;
				}
			}
			side = null;
			return false;
		}

		internal void RemoveParticipant(TabletopCardId cardId)
		{
			RequireActive();
			if (!TryGetSide(cardId, out BattleSide side))
			{
				throw new KeyNotFoundException($"战斗中不存在牌桌卡牌 {cardId}。");
			}
			side.Remove(cardId);
		}

		internal void End()
		{
			RequireActive();
			IsEnded = true;
			for (int index = 0; index < m_sides.Count; index++)
			{
				m_sides[index].Clear();
			}
		}

		private void RequireActive()
		{
			if (IsEnded)
			{
				throw new InvalidOperationException("战斗已经结束，不能继续修改参战对象。");
			}
		}
	}
}
