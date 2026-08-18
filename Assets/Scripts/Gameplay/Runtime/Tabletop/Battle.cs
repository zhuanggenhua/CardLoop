using System;
using System.Collections.Generic;
using GAS.Runtime;
using UnityEngine;

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

		internal void Add(TabletopCardId cardId)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException("加入战斗方的牌桌卡牌 ID 无效。", nameof(cardId));
			}
			if (m_cardIds.Contains(cardId))
			{
				throw new InvalidOperationException($"战斗方已经包含牌桌卡牌 {cardId}。");
			}

			m_cardIds.Add(cardId);
		}

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
	/// 牌桌上的即时战斗聚合，拥有战斗方、参战关系、自动行动进度和结束生命周期。
	/// 角色阵营与敌我关系由角色 GAS 状态和剧本关系规则解析，不复制进本对象。
	/// </summary>
	public sealed class Battle
	{
		private const float TurnIntervalSeconds = 1f;

		private readonly List<BattleSide> m_sides;
		private readonly IReadOnlyList<BattleSide> m_readOnlySides;
		private readonly Dictionary<TabletopCardId, float> m_actionProgress =
			new Dictionary<TabletopCardId, float>();
		private Unity.Mathematics.Random m_authoritativeRandom;
		private bool m_hasAreaCenter;
		private float m_turnTimer = TurnIntervalSeconds;
		private TabletopCardId m_executingActorId;
		private TabletopCardId m_executingTargetId;
		private AbilitySpec m_executingAbility;
		private int m_executingPresentationTagCode;
		private float m_pendingActivationRemainingSeconds;
		private float m_pendingActivationDurationSeconds;
		private ulong m_attackPresentationSequence;
		private bool m_hasUnconsumedAttackStartedPresentation;
		private BattleAttackPresentation m_unconsumedAttackStartedPresentation;

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
					m_actionProgress.Add(cardId, 0f);
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

		internal bool HasExecutingTurn => m_executingAbility != null;

		/// <summary>本场战斗区域的权威中心；区域尺寸由当前战斗方人数和剧本规则派生。</summary>
		public Vector2 AreaCenter
		{
			get
			{
				if (!m_hasAreaCenter)
				{
					throw new InvalidOperationException("战斗区域中心尚未由所属牌桌初始化。");
				}
				return m_areaCenter;
			}
			private set => m_areaCenter = value;
		}

		private Vector2 m_areaCenter;

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
			m_actionProgress.Remove(cardId);
			if (m_executingActorId == cardId)
			{
				FinishExecutingTurn(resetActorProgress: false);
			}
			else if (m_executingTargetId == cardId)
			{
				FinishExecutingTurn(resetActorProgress: false);
			}
		}

		internal void AddParticipant(int sideIndex, TabletopCardId cardId)
		{
			RequireActive();
			if (sideIndex < 0 || sideIndex >= m_sides.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(sideIndex),
					sideIndex,
					$"战斗方索引必须位于 0 到 {m_sides.Count - 1} 之间。");
			}
			if (HasParticipant(cardId))
			{
				throw new InvalidOperationException($"战斗已经包含牌桌卡牌 {cardId}。");
			}

			m_sides[sideIndex].Add(cardId);
			m_actionProgress.Add(cardId, 0f);
		}

		internal void AddActionProgress(TabletopCardId cardId, float amount)
		{
			RequireActive();
			if (!float.IsFinite(amount) || amount < 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(amount), amount, "战斗行动进度增量必须是大于或等于 0 的有限值。");
			}
			if (!m_actionProgress.TryGetValue(cardId, out float current))
			{
				throw new KeyNotFoundException($"战斗中不存在牌桌卡牌 {cardId} 的行动进度。");
			}
			m_actionProgress[cardId] = current + amount;
		}

		internal float GetActionProgress(TabletopCardId cardId)
		{
			RequireActive();
			return m_actionProgress.TryGetValue(cardId, out float progress)
				? progress
				: throw new KeyNotFoundException($"战斗中不存在牌桌卡牌 {cardId} 的行动进度。");
		}

		internal bool ConsumeTurnInterval(float deltaSeconds)
		{
			RequireActive();
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"战斗推进秒数必须是大于或等于 0 的有限值。");
			}
			if (HasExecutingTurn)
			{
				return false;
			}

			m_turnTimer -= deltaSeconds;
			if (m_turnTimer > 0f)
			{
				return false;
			}
			m_turnTimer += TurnIntervalSeconds;
			return true;
		}

		internal TabletopCardId TakeRandomOpponent(TabletopCardId actorId)
		{
			RequireActive();
			if (!TryGetSide(actorId, out BattleSide actorSide))
			{
				throw new InvalidOperationException($"自动行动角色 {actorId} 不属于当前战斗。");
			}

			int opponentCount = ParticipantCount - actorSide.ParticipantCount;
			if (opponentCount <= 0)
			{
				throw new InvalidOperationException($"战斗 {Id} 没有可供角色 {actorId} 选择的对方目标。");
			}
			int targetIndex = m_authoritativeRandom.NextInt(opponentCount);
			for (int sideIndex = 0; sideIndex < m_sides.Count; sideIndex++)
			{
				BattleSide side = m_sides[sideIndex];
				if (ReferenceEquals(side, actorSide))
				{
					continue;
				}
				if (targetIndex < side.ParticipantCount)
				{
					return side.CardIds[targetIndex];
				}
				targetIndex -= side.ParticipantCount;
			}
			throw new InvalidOperationException($"战斗 {Id} 的对方目标索引计算失败。");
		}

		internal void BeginTurn(
			TabletopCardId actorId,
			TabletopCardId targetId,
			AbilitySpec ability,
			int presentationTagCode,
			float preActivationSeconds)
		{
			RequireActive();
			if (HasExecutingTurn)
			{
				throw new InvalidOperationException($"战斗 {Id} 已有正在执行的自动行动。");
			}
			if (!m_actionProgress.ContainsKey(actorId))
			{
				throw new InvalidOperationException($"自动行动角色 {actorId} 不属于当前战斗。");
			}
			if (!HasParticipant(targetId))
			{
				throw new InvalidOperationException($"自动行动目标 {targetId} 不属于当前战斗。");
			}
			if (ability == null || !ability.IsValid)
			{
				throw new ArgumentException("自动战斗必须提交有效的 EX-GAS Ability。", nameof(ability));
			}
			if (!float.IsFinite(preActivationSeconds) || preActivationSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(preActivationSeconds),
					preActivationSeconds,
					"战斗攻击前摇秒数必须是大于或等于 0 的有限值。");
			}

			m_executingActorId = actorId;
			m_executingTargetId = targetId;
			m_executingAbility = ability;
			m_executingPresentationTagCode = presentationTagCode;
			m_pendingActivationDurationSeconds = preActivationSeconds;
			m_pendingActivationRemainingSeconds = preActivationSeconds;
			checked
			{
				m_attackPresentationSequence++;
			}
			m_unconsumedAttackStartedPresentation =
				CreateExecutingAttackPresentation(preActivationSeconds);
			m_hasUnconsumedAttackStartedPresentation = true;
			ability.RegisterOnActivateResult(OnExecutingAbilityActivationResult);
			ability.RegisterOnEndAbility(OnExecutingAbilityFinished);
			ability.RegisterOnCancelAbility(OnExecutingAbilityFinished);
		}

		internal bool TryConsumeAttackStartedPresentation(out BattleAttackPresentation presentation)
		{
			if (!m_hasUnconsumedAttackStartedPresentation)
			{
				presentation = default;
				return false;
			}

			presentation = m_unconsumedAttackStartedPresentation;
			m_hasUnconsumedAttackStartedPresentation = false;
			m_unconsumedAttackStartedPresentation = default;
			return true;
		}

		internal bool TryGetExecutingAttackPresentation(out BattleAttackPresentation presentation)
		{
			if (m_executingAbility == null)
			{
				presentation = default;
				return false;
			}

			presentation = CreateExecutingAttackPresentation(m_pendingActivationRemainingSeconds);
			return true;
		}

		internal bool TryGetPendingAttackPresentation(out BattleAttackPresentation presentation)
		{
			if (m_executingAbility == null || m_pendingActivationRemainingSeconds <= 0f)
			{
				presentation = default;
				return false;
			}

			presentation = CreateExecutingAttackPresentation(m_pendingActivationRemainingSeconds);
			return true;
		}

		private BattleAttackPresentation CreateExecutingAttackPresentation(float remainingSeconds)
		{
			return new BattleAttackPresentation(
				m_attackPresentationSequence,
				m_executingActorId,
				m_executingTargetId,
				m_executingPresentationTagCode,
				m_pendingActivationDurationSeconds,
				remainingSeconds);
		}

		internal bool TryConsumePendingActivation(
			float deltaSeconds,
			out BattlePendingAbilityActivation activation)
		{
			RequireActive();
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"战斗攻击前摇推进秒数必须是大于或等于 0 的有限值。");
			}
			if (m_executingAbility == null || m_pendingActivationRemainingSeconds <= 0f)
			{
				activation = default;
				return false;
			}

			m_pendingActivationRemainingSeconds -= deltaSeconds;
			if (m_pendingActivationRemainingSeconds > 0f)
			{
				activation = default;
				return false;
			}

			m_pendingActivationRemainingSeconds = 0f;
			activation = new BattlePendingAbilityActivation(
				m_executingActorId,
				m_executingTargetId,
				m_executingAbility);
			return true;
		}

		internal void AbortTurn()
		{
			RequireActive();
			FinishExecutingTurn(resetActorProgress: false);
		}

		internal void InitializeAreaCenter(Vector2 center)
		{
			RequireActive();
			if (m_hasAreaCenter)
			{
				throw new InvalidOperationException("战斗区域中心已经初始化，不能重复设置。");
			}
			if (!float.IsFinite(center.x) || !float.IsFinite(center.y))
			{
				throw new ArgumentException("战斗区域中心必须是有限二维坐标。", nameof(center));
			}

			AreaCenter = center;
			m_hasAreaCenter = true;
		}

		internal void AbsorbAreaCenter(Battle source)
		{
			RequireActive();
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}
			source.RequireActive();
			int destinationCount = ParticipantCount;
			int sourceCount = source.ParticipantCount;
			AreaCenter = (AreaCenter * destinationCount + source.AreaCenter * sourceCount) /
				(destinationCount + sourceCount);
		}

		internal void End()
		{
			RequireActive();
			FinishExecutingTurn(resetActorProgress: false);
			IsEnded = true;
			for (int index = 0; index < m_sides.Count; index++)
			{
				m_sides[index].Clear();
			}
			m_actionProgress.Clear();
		}

		private void OnExecutingAbilityActivationResult(AbilityActivationResult result)
		{
			if (result != AbilityActivationResult.Success)
			{
				FinishExecutingTurn(resetActorProgress: false);
			}
		}

		private void OnExecutingAbilityFinished()
		{
			FinishExecutingTurn(resetActorProgress: true);
		}

		private void FinishExecutingTurn(bool resetActorProgress)
		{
			AbilitySpec ability = m_executingAbility;
			if (ability == null)
			{
				return;
			}

			// Ability 的真实完成时刻归 EX-GAS；战斗只在回调后重置行动者进度，避免另存一份技能时长。
			ability.UnRegisterOnActivateResult(OnExecutingAbilityActivationResult);
			ability.UnRegisterOnEndAbility(OnExecutingAbilityFinished);
			ability.UnRegisterOnCancelAbility(OnExecutingAbilityFinished);
			TabletopCardId actorId = m_executingActorId;
			m_executingAbility = null;
			m_executingActorId = default;
			m_executingTargetId = default;
			m_executingPresentationTagCode = 0;
			m_pendingActivationRemainingSeconds = 0f;
			m_pendingActivationDurationSeconds = 0f;
			if (resetActorProgress && m_actionProgress.ContainsKey(actorId))
			{
				m_actionProgress[actorId] = 0f;
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

	/// <summary>一次自动战斗攻击在规则结算前的只读表现事实；规则仍由 Battle / EX-GAS 提交。</summary>
	public readonly struct BattleAttackPresentation
	{
		internal BattleAttackPresentation(
			ulong sequence,
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			int presentationTagCode,
			float durationSeconds,
			float remainingSeconds)
		{
			Sequence = sequence;
			SourceCardId = sourceCardId;
			TargetCardId = targetCardId;
			PresentationTagCode = presentationTagCode;
			DurationSeconds = durationSeconds;
			RemainingSeconds = remainingSeconds;
		}

		public ulong Sequence { get; }
		public TabletopCardId SourceCardId { get; }
		public TabletopCardId TargetCardId { get; }
		public int PresentationTagCode { get; }
		public float DurationSeconds { get; }
		public float RemainingSeconds { get; }
	}

	/// <summary>攻击前摇结束后交给所属牌桌激活 EX-GAS Ability 的内部请求。</summary>
	internal readonly struct BattlePendingAbilityActivation
	{
		internal BattlePendingAbilityActivation(
			TabletopCardId sourceCardId,
			TabletopCardId targetCardId,
			AbilitySpec ability)
		{
			SourceCardId = sourceCardId;
			TargetCardId = targetCardId;
			Ability = ability;
		}

		internal TabletopCardId SourceCardId { get; }
		internal TabletopCardId TargetCardId { get; }
		internal AbilitySpec Ability { get; }
	}
}
