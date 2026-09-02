using System;
using Gameplay.Content;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using UnityEngine;

namespace Gameplay.Networking
{
	public enum TabletopCommandKind
	{
		None = 0,
		MoveCardStack = 10,
		MoveSingleCard = 20,
		StartAction = 30,
		ConfirmTurn = 40,
		ConfirmDayCycle = 50
	}

	public enum CommandPredictionMode
	{
		WaitForAuthority = 0,
		LocalPresentation = 10,
		DeterministicState = 20
	}

	/// <summary>一次玩家牌桌意图；Mirror 只负责运送它，不直接拥有玩法规则。</summary>
	public sealed class TabletopCommandIntent
	{
		public TabletopCommandKind Kind { get; }

		public TabletopCardId CardId { get; }

		public Vector2 TablePosition { get; }

		public ActionRequest ActionRequest { get; }

		public CommandPredictionMode PredictionMode { get; }

		public bool RequiresAuthoritativeRandom { get; }

		public bool MayRevealHiddenInformation { get; }

		private TabletopCommandIntent(
			TabletopCommandKind kind,
			TabletopCardId cardId,
			Vector2 tablePosition,
			ActionRequest actionRequest,
			CommandPredictionMode predictionMode,
			bool requiresAuthoritativeRandom,
			bool mayRevealHiddenInformation)
		{
			if (!Enum.IsDefined(typeof(TabletopCommandKind), kind) || kind == TabletopCommandKind.None)
			{
				throw new ArgumentException($"牌桌命令类型无效：{kind}。", nameof(kind));
			}
			RequirePredictionMode(predictionMode);
			Kind = kind;
			CardId = cardId;
			TablePosition = tablePosition;
			ActionRequest = actionRequest;
			PredictionMode = predictionMode;
			RequiresAuthoritativeRandom = requiresAuthoritativeRandom;
			MayRevealHiddenInformation = mayRevealHiddenInformation;
		}

		public static TabletopCommandIntent MoveCardStack(
			TabletopCardId cardId,
			Vector2 tablePosition,
			CommandPredictionMode predictionMode = CommandPredictionMode.LocalPresentation)
		{
			RequireCardId(cardId, "移动牌堆");
			RequireFinite(tablePosition, nameof(tablePosition));
			RequirePredictionMode(predictionMode);
			return new TabletopCommandIntent(
				TabletopCommandKind.MoveCardStack,
				cardId,
				tablePosition,
				null,
				predictionMode,
				requiresAuthoritativeRandom: false,
				mayRevealHiddenInformation: false);
		}

		public static TabletopCommandIntent MoveSingleCard(
			TabletopCardId cardId,
			Vector2 tablePosition,
			CommandPredictionMode predictionMode = CommandPredictionMode.LocalPresentation)
		{
			RequireCardId(cardId, "移动单张卡牌");
			RequireFinite(tablePosition, nameof(tablePosition));
			RequirePredictionMode(predictionMode);
			return new TabletopCommandIntent(
				TabletopCommandKind.MoveSingleCard,
				cardId,
				tablePosition,
				null,
				predictionMode,
				requiresAuthoritativeRandom: false,
				mayRevealHiddenInformation: false);
		}

		public static TabletopCommandIntent StartAction(ActionRequest request)
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}
			return new TabletopCommandIntent(
				TabletopCommandKind.StartAction,
				default,
				default,
				request,
				CommandPredictionMode.WaitForAuthority,
				requiresAuthoritativeRandom: true,
				mayRevealHiddenInformation: false);
		}

		public static TabletopCommandIntent ConfirmTurn()
		{
			return new TabletopCommandIntent(
				TabletopCommandKind.ConfirmTurn,
				default,
				default,
				null,
				CommandPredictionMode.WaitForAuthority,
				requiresAuthoritativeRandom: true,
				mayRevealHiddenInformation: false);
		}

		public static TabletopCommandIntent ConfirmDayCycle()
		{
			return new TabletopCommandIntent(
				TabletopCommandKind.ConfirmDayCycle,
				default,
				default,
				null,
				CommandPredictionMode.WaitForAuthority,
				requiresAuthoritativeRandom: true,
				mayRevealHiddenInformation: true);
		}

		private static void RequireCardId(TabletopCardId cardId, string operation)
		{
			if (!cardId.IsValid)
			{
				throw new ArgumentException($"{operation}命令必须引用有效的局内卡牌。", nameof(cardId));
			}
		}

		private static void RequireFinite(Vector2 position, string parameterName)
		{
			if (!float.IsFinite(position.x) || !float.IsFinite(position.y))
			{
				throw new ArgumentException("牌桌命令坐标必须是有限值。", parameterName);
			}
		}

		private static void RequirePredictionMode(CommandPredictionMode predictionMode)
		{
			if (!Enum.IsDefined(typeof(CommandPredictionMode), predictionMode))
			{
				throw new ArgumentException($"牌桌命令预测模式无效：{predictionMode}。", nameof(predictionMode));
			}
		}
	}

	/// <summary>客户端提交到房主 / 服务器的牌桌命令外壳。</summary>
	public sealed class TabletopCommandEnvelope
	{
		public PlayerSeatId SenderSeatId { get; }

		public ulong ClientSequence { get; }

		public AuthorityRevision ExpectedRevision { get; }

		public TabletopCommandIntent Intent { get; }

		public TabletopCommandEnvelope(
			PlayerSeatId senderSeatId,
			ulong clientSequence,
			AuthorityRevision expectedRevision,
			TabletopCommandIntent intent)
		{
			if (!senderSeatId.IsValid)
			{
				throw new ArgumentException("牌桌命令必须声明有效的发起玩家席位。", nameof(senderSeatId));
			}
			if (clientSequence == 0uL)
			{
				throw new ArgumentOutOfRangeException(nameof(clientSequence), "客户端命令序号必须从 1 开始。");
			}

			SenderSeatId = senderSeatId;
			ClientSequence = clientSequence;
			ExpectedRevision = expectedRevision;
			Intent = intent ?? throw new ArgumentNullException(nameof(intent));
		}
	}

	public static class TabletopCommandPredictionPolicy
	{
		public static bool CanShowBeforeAuthority(TabletopCommandIntent intent)
		{
			return intent != null &&
				intent.PredictionMode != CommandPredictionMode.WaitForAuthority &&
				!intent.RequiresAuthoritativeRandom &&
				!intent.MayRevealHiddenInformation;
		}

		public static bool CanMutateLocalStateBeforeAuthority(TabletopCommandIntent intent)
		{
			return intent != null &&
				intent.PredictionMode == CommandPredictionMode.DeterministicState &&
				!intent.RequiresAuthoritativeRandom &&
				!intent.MayRevealHiddenInformation;
		}
	}

	public static class TabletopCommandAuthorization
	{
		public static void RequireSeatCanSubmit(PlayerSeat seat, TabletopCommandEnvelope command)
		{
			if (seat == null)
			{
				throw new ArgumentNullException(nameof(seat));
			}
			if (command == null)
			{
				throw new ArgumentNullException(nameof(command));
			}
			if (seat.Id != command.SenderSeatId)
			{
				throw new InvalidOperationException(
					$"命令发起席位 {command.SenderSeatId} 与当前连接绑定席位 {seat.Id} 不一致。");
			}
			if (!seat.CanSubmitGameplayCommands)
			{
				throw new InvalidOperationException($"旁观席位 {seat.Id} 不能提交牌桌玩法命令。");
			}
		}
	}
}
