using System;

namespace Gameplay.Networking
{
	public enum SnapshotVisibilityScope
	{
		Public = 0,
		Seat = 10
	}

	/// <summary>房主 / 服务器发给客户端的权威快照外壳；快照内容仍由 ScenarioRun / Tabletop 等 owner 生成。</summary>
	public sealed class AuthoritySnapshotEnvelope<TSnapshot>
		where TSnapshot : class
	{
		public AuthorityRevision Revision { get; }

		public SnapshotVisibilityScope Visibility { get; }

		public PlayerSeatId RecipientSeatId { get; }

		public TSnapshot Snapshot { get; }

		public AuthoritySnapshotEnvelope(
			AuthorityRevision revision,
			SnapshotVisibilityScope visibility,
			PlayerSeatId recipientSeatId,
			TSnapshot snapshot)
		{
			if (!Enum.IsDefined(typeof(SnapshotVisibilityScope), visibility))
			{
				throw new ArgumentException($"权威快照可见范围无效：{visibility}。", nameof(visibility));
			}
			if (visibility == SnapshotVisibilityScope.Public && recipientSeatId.IsValid)
			{
				throw new ArgumentException("公开权威快照不能绑定单个接收席位。", nameof(recipientSeatId));
			}
			if (visibility == SnapshotVisibilityScope.Seat && !recipientSeatId.IsValid)
			{
				throw new ArgumentException("私有权威快照必须声明接收玩家席位。", nameof(recipientSeatId));
			}

			Revision = revision;
			Visibility = visibility;
			RecipientSeatId = recipientSeatId;
			Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
		}

		public bool IsVisibleTo(PlayerSeatId seatId)
		{
			return Visibility == SnapshotVisibilityScope.Public ||
				(Visibility == SnapshotVisibilityScope.Seat && RecipientSeatId == seatId);
		}
	}
}
