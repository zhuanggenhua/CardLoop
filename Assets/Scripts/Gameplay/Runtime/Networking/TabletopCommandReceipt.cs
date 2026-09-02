using System;

namespace Gameplay.Networking
{
	public enum TabletopCommandRejectionReason
	{
		None = 0,
		StaleRevision = 10,
		UnauthorizedSeat = 20,
		InvalidCommand = 30,
		RuleRejected = 40,
		ContentMismatch = 50
	}

	/// <summary>房主 / 服务器对客户端牌桌命令的确认或拒绝。</summary>
	public sealed class TabletopCommandReceipt
	{
		public ulong ClientSequence { get; }

		public AuthorityRevision Revision { get; }

		public bool Accepted { get; }

		public TabletopCommandRejectionReason RejectionReason { get; }

		public string Message { get; }

		private TabletopCommandReceipt(
			ulong clientSequence,
			AuthorityRevision revision,
			bool accepted,
			TabletopCommandRejectionReason rejectionReason,
			string message)
		{
			if (clientSequence == 0uL)
			{
				throw new ArgumentOutOfRangeException(nameof(clientSequence), "客户端命令序号必须从 1 开始。");
			}
			if (!Enum.IsDefined(typeof(TabletopCommandRejectionReason), rejectionReason))
			{
				throw new ArgumentException($"牌桌命令拒绝原因无效：{rejectionReason}。", nameof(rejectionReason));
			}
			if (accepted && rejectionReason != TabletopCommandRejectionReason.None)
			{
				throw new ArgumentException("已接受命令不能同时携带拒绝原因。", nameof(rejectionReason));
			}
			if (!accepted && rejectionReason == TabletopCommandRejectionReason.None)
			{
				throw new ArgumentException("被拒绝命令必须携带明确拒绝原因。", nameof(rejectionReason));
			}

			ClientSequence = clientSequence;
			Revision = revision;
			Accepted = accepted;
			RejectionReason = rejectionReason;
			Message = message ?? string.Empty;
		}

		public static TabletopCommandReceipt Accept(ulong clientSequence, AuthorityRevision revision)
		{
			return new TabletopCommandReceipt(
				clientSequence,
				revision,
				accepted: true,
				TabletopCommandRejectionReason.None,
				string.Empty);
		}

		public static TabletopCommandReceipt Reject(
			ulong clientSequence,
			AuthorityRevision revision,
			TabletopCommandRejectionReason reason,
			string message)
		{
			if (string.IsNullOrWhiteSpace(message))
			{
				throw new ArgumentException("拒绝牌桌命令时必须提供可展示或可记录的原因。", nameof(message));
			}
			return new TabletopCommandReceipt(
				clientSequence,
				revision,
				accepted: false,
				reason,
				message);
		}
	}
}
