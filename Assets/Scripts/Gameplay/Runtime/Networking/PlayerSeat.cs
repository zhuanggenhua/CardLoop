using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gameplay.Networking
{
	/// <summary>联机局中的稳定玩家席位；它不是 Mirror 连接 ID，也不是内容 ID。</summary>
	[Serializable]
	public struct PlayerSeatId : IEquatable<PlayerSeatId>
	{
		[SerializeField]
		private string m_value;

		public string Value => m_value ?? string.Empty;

		public bool IsValid => NetworkProtocolKeyRules.IsValidKey(Value);

		public PlayerSeatId(string value)
		{
			if (!NetworkProtocolKeyRules.IsValidKey(value))
			{
				throw new ArgumentException("玩家席位 ID 必须是非空稳定 key，且只能包含字母、数字、点、下划线或短横线。", nameof(value));
			}
			m_value = value;
		}

		public bool Equals(PlayerSeatId other)
		{
			return string.Equals(Value, other.Value, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is PlayerSeatId other && Equals(other);
		}

		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(Value);
		}

		public override string ToString()
		{
			return Value;
		}

		public static bool operator ==(PlayerSeatId left, PlayerSeatId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(PlayerSeatId left, PlayerSeatId right)
		{
			return !left.Equals(right);
		}
	}

	public enum PlayerSeatKind
	{
		Player = 0,
		Observer = 10
	}

	/// <summary>当前联机局里一个参与者的席位事实；控制权细则后续由玩法协议继续收敛。</summary>
	public sealed class PlayerSeat
	{
		public PlayerSeatId Id { get; }

		public string DisplayName { get; }

		public PlayerSeatKind Kind { get; }

		public bool CanSubmitGameplayCommands => Kind == PlayerSeatKind.Player;

		public PlayerSeat(PlayerSeatId id, string displayName, PlayerSeatKind kind)
		{
			if (!id.IsValid)
			{
				throw new ArgumentException("玩家席位必须拥有有效 ID。", nameof(id));
			}
			if (string.IsNullOrWhiteSpace(displayName))
			{
				throw new ArgumentException("玩家席位显示名不能为空。", nameof(displayName));
			}
			if (!Enum.IsDefined(typeof(PlayerSeatKind), kind))
			{
				throw new ArgumentException($"玩家席位类型无效：{kind}。", nameof(kind));
			}

			Id = id;
			DisplayName = displayName;
			Kind = kind;
		}
	}

	/// <summary>一局联机的席位表；只保存席位事实，不保存网络连接对象。</summary>
	public sealed class PlayerSeatRoster
	{
		private readonly ReadOnlyCollection<PlayerSeat> m_seats;
		private readonly Dictionary<PlayerSeatId, PlayerSeat> m_byId;

		public IReadOnlyList<PlayerSeat> Seats => m_seats;

		public PlayerSeatRoster(IReadOnlyList<PlayerSeat> seats)
		{
			if (seats == null)
			{
				throw new ArgumentNullException(nameof(seats));
			}

			List<PlayerSeat> copiedSeats = new List<PlayerSeat>(seats.Count);
			m_byId = new Dictionary<PlayerSeatId, PlayerSeat>();
			for (int i = 0; i < seats.Count; i++)
			{
				PlayerSeat seat = seats[i] ?? throw new ArgumentException(
					$"玩家席位表的第 {i + 1} 项为空。",
					nameof(seats));
				if (!m_byId.TryAdd(seat.Id, seat))
				{
					throw new InvalidOperationException($"玩家席位表重复包含席位 {seat.Id}。");
				}
				copiedSeats.Add(seat);
			}
			m_seats = copiedSeats.AsReadOnly();
		}

		public bool TryGet(PlayerSeatId id, out PlayerSeat seat)
		{
			if (!id.IsValid)
			{
				seat = null;
				return false;
			}
			return m_byId.TryGetValue(id, out seat);
		}

		public PlayerSeat Require(PlayerSeatId id)
		{
			if (!TryGet(id, out PlayerSeat seat))
			{
				throw new InvalidOperationException($"联机局不存在玩家席位 {id}。");
			}
			return seat;
		}
	}

	internal static class NetworkProtocolKeyRules
	{
		internal static bool IsValidKey(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
			{
				return false;
			}
			foreach (char c in value)
			{
				if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}
	}
}
