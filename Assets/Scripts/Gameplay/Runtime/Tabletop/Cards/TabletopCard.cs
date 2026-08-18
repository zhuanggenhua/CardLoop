using System;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Tabletop
{
	/// <summary>
	/// 一次剧本运行共享的卡牌实例号序列，让不同地区中的卡牌仍拥有单局唯一身份。
	/// </summary>
	internal sealed class TabletopCardIdSequence
	{
		private ulong m_nextValue;

		internal ulong NextValue => m_nextValue;

		internal TabletopCardIdSequence(ulong nextValue = 1uL)
		{
			if (nextValue == 0)
			{
				throw new ArgumentOutOfRangeException(nameof(nextValue), "下一卡牌实例号不能为 0。");
			}
			m_nextValue = nextValue;
		}

		internal TabletopCardId PeekNext()
		{
			if (m_nextValue == ulong.MaxValue)
			{
				throw new InvalidOperationException("本次剧本运行的卡牌实例号已耗尽。");
			}
			return new TabletopCardId(m_nextValue);
		}

		internal void EnsureAvailable(int count)
		{
			if (count < 0 || (count > 0 && m_nextValue > ulong.MaxValue - (ulong)count))
			{
				throw new InvalidOperationException("本次剧本运行的卡牌实例号不足以提交当前变更。");
			}
		}

		internal void Commit(TabletopCardId cardId)
		{
			if (cardId.Value != m_nextValue)
			{
				throw new InvalidOperationException(
					$"卡牌实例号提交顺序错误：预期 {m_nextValue}，实际 {cardId}。");
			}
			m_nextValue++;
		}
	}

	/// <summary>
	/// 牌桌单局内的卡牌实例身份；它不是内容 ID，也不作为作者字段。
	/// </summary>
	public readonly struct TabletopCardId : IEquatable<TabletopCardId>
	{
		public ulong Value { get; }

		public bool IsValid => Value != 0;

		internal TabletopCardId(ulong value)
		{
			Value = value;
		}

		public bool Equals(TabletopCardId other)
		{
			return Value == other.Value;
		}

		public override bool Equals(object obj)
		{
			return obj is TabletopCardId other && Equals(other);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public override string ToString()
		{
			return Value.ToString();
		}

		public static bool operator ==(TabletopCardId left, TabletopCardId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(TabletopCardId left, TabletopCardId right)
		{
			return !left.Equals(right);
		}
	}

	/// <summary>
	/// 牌桌中的卡牌实例，引用唯一内容 ID，并由所属牌堆决定空间位置。
	/// </summary>
	public class TabletopCard
	{
		public TabletopCardId Id { get; }

		public ContentId ContentId { get; }

		public int RemainingUses { get; private set; }

		public float PeriodicProductionElapsedSeconds { get; private set; }

		public float AutomaticMovementElapsedSeconds { get; private set; }

		/// <summary>当前直接拥有本卡牌空间位置和堆叠顺序的牌堆；移出牌桌后为空。</summary>
		public TabletopCardStack Stack { get; private set; }

		/// <summary>卡牌当前在牌桌上的逻辑位置；堆叠成员共享所属牌堆的锚点。</summary>
		public Vector2 Position => Stack != null
			? Stack.Position
			: throw new InvalidOperationException($"牌桌卡牌 {Id} 当前不属于任何牌堆，没有牌桌位置。");

		public bool IsPlacementLocked => Stack?.IsPlacementLocked ?? false;

		protected internal TabletopCard(
			TabletopCardId id,
			ContentId contentId,
			int remainingUses = 1,
			float periodicProductionElapsedSeconds = 0f,
			float automaticMovementElapsedSeconds = 0f)
		{
			if (!id.IsValid)
			{
				throw new ArgumentException("牌桌卡牌必须拥有有效的局内 ID。", nameof(id));
			}
			if (!contentId.IsValid)
			{
				throw new ArgumentException("牌桌卡牌必须引用有效的 Gameplay 内容 ID。", nameof(contentId));
			}
			if (remainingUses <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(remainingUses),
					remainingUses,
					"牌桌卡牌剩余使用次数必须大于 0。");
			}
			if (!float.IsFinite(periodicProductionElapsedSeconds) || periodicProductionElapsedSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(periodicProductionElapsedSeconds),
					periodicProductionElapsedSeconds,
					"牌桌卡牌周期产出已累计秒数必须是大于或等于 0 的有限值。");
			}
			if (!float.IsFinite(automaticMovementElapsedSeconds) || automaticMovementElapsedSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(automaticMovementElapsedSeconds),
					automaticMovementElapsedSeconds,
					"牌桌卡牌自动移动已累计秒数必须是大于或等于 0 的有限值。");
			}

			Id = id;
			ContentId = contentId;
			RemainingUses = remainingUses;
			PeriodicProductionElapsedSeconds = periodicProductionElapsedSeconds;
			AutomaticMovementElapsedSeconds = automaticMovementElapsedSeconds;
		}

		/// <summary>创建本卡牌派生类型自己的运行状态快照；普通卡牌没有额外状态。</summary>
		protected internal virtual TabletopCardRuntimeStateSnapshot CreateRuntimeStateSnapshot()
		{
			return null;
		}

		internal TabletopCardSnapshot CreateSnapshot()
		{
			return new TabletopCardSnapshot(
				Id,
				ContentId,
				RemainingUses,
				PeriodicProductionElapsedSeconds,
				AutomaticMovementElapsedSeconds,
				CreateRuntimeStateSnapshot());
		}

		internal int AdvancePeriodicProduction(float deltaSeconds, float intervalSeconds)
		{
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"周期产出推进秒数必须是大于或等于 0 的有限值。");
			}
			if (!float.IsFinite(intervalSeconds) || intervalSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(intervalSeconds),
					intervalSeconds,
					"周期产出间隔必须是大于 0 的有限秒数。");
			}
			if (deltaSeconds == 0f)
			{
				return 0;
			}

			PeriodicProductionElapsedSeconds += deltaSeconds;
			if (PeriodicProductionElapsedSeconds < intervalSeconds)
			{
				return 0;
			}

			int productionCount = Mathf.FloorToInt(PeriodicProductionElapsedSeconds / intervalSeconds);
			PeriodicProductionElapsedSeconds -= productionCount * intervalSeconds;
			return productionCount;
		}

		internal bool AdvanceAutomaticMovement(float deltaSeconds, float intervalSeconds)
		{
			if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(deltaSeconds),
					deltaSeconds,
					"自动移动推进秒数必须是大于或等于 0 的有限值。");
			}
			if (!float.IsFinite(intervalSeconds) || intervalSeconds <= 0f)
			{
				throw new ArgumentOutOfRangeException(
					nameof(intervalSeconds),
					intervalSeconds,
					"自动移动间隔必须是大于 0 的有限秒数。");
			}
			if (deltaSeconds == 0f)
			{
				return false;
			}

			AutomaticMovementElapsedSeconds += deltaSeconds;
			if (AutomaticMovementElapsedSeconds < intervalSeconds)
			{
				return false;
			}

			int elapsedIntervals = Mathf.FloorToInt(AutomaticMovementElapsedSeconds / intervalSeconds);
			AutomaticMovementElapsedSeconds -= elapsedIntervals * intervalSeconds;
			return true;
		}

		internal void ConsumeUse()
		{
			if (RemainingUses <= 1)
			{
				throw new InvalidOperationException(
					$"牌桌卡牌 {Id} 的最后一次使用必须由所属牌桌直接移除，不能留下零次数实例。");
			}

			RemainingUses--;
		}

		internal void AttachToStack(TabletopCardStack stack)
		{
			if (stack == null)
			{
				throw new ArgumentNullException(nameof(stack));
			}
			if (Stack != null)
			{
				throw new InvalidOperationException($"牌桌卡牌 {Id} 已属于一个牌堆，不能重复加入。");
			}

			Stack = stack;
		}

		internal void TransferToStack(TabletopCardStack source, TabletopCardStack target)
		{
			if (!ReferenceEquals(Stack, source))
			{
				throw new InvalidOperationException($"牌桌卡牌 {Id} 的实际所属牌堆与迁移来源不一致。");
			}
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			Stack = target;
		}

		internal void DetachFromStack(TabletopCardStack stack)
		{
			if (!ReferenceEquals(Stack, stack))
			{
				throw new InvalidOperationException($"牌桌卡牌 {Id} 的实际所属牌堆与移除来源不一致。");
			}

			Stack = null;
		}
	}
}
