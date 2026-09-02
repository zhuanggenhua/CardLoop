using System;
using System.IO;
using Gameplay.Networking;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Tests
{
	/// <summary>
	/// 保护首版联机协议骨架：玩法命令必须独立于 Mirror，并携带席位、版本、可见性和预测边界。
	/// </summary>
	public sealed class NetworkingProtocolEditModeTests
	{
		[Test]
		public void CommandEnvelope_CarriesSeatExpectedRevisionAndPredictableIntentWithoutMirrorReference()
		{
			PlayerSeatId seatId = new PlayerSeatId("seat.host");
			TabletopCommandIntent intent = TabletopCommandIntent.MoveCardStack(
				new TabletopCardId(12uL),
				new Vector2(2f, 3f),
				CommandPredictionMode.DeterministicState);

			TabletopCommandEnvelope envelope = new TabletopCommandEnvelope(
				seatId,
				3uL,
				new AuthorityRevision(7uL),
				intent);

			Assert.That(envelope.SenderSeatId, Is.EqualTo(seatId));
			Assert.That(envelope.ClientSequence, Is.EqualTo(3uL));
			Assert.That(envelope.ExpectedRevision.Value, Is.EqualTo(7uL));
			Assert.That(envelope.Intent.Kind, Is.EqualTo(TabletopCommandKind.MoveCardStack));
			Assert.That(envelope.Intent.CardId, Is.EqualTo(new TabletopCardId(12uL)));
			Assert.That(envelope.Intent.TablePosition, Is.EqualTo(new Vector2(2f, 3f)));
			Assert.That(TabletopCommandPredictionPolicy.CanShowBeforeAuthority(intent), Is.True);
			Assert.That(TabletopCommandPredictionPolicy.CanMutateLocalStateBeforeAuthority(intent), Is.True);

			string runtimeAsmdefPath = Path.Combine(
				Application.dataPath,
				"Scripts/Gameplay/Gameplay.Runtime.asmdef");
			string runtimeAsmdef = File.ReadAllText(runtimeAsmdefPath);
			StringAssert.DoesNotContain("Mirror", runtimeAsmdef);
		}

		[Test]
		public void PredictionPolicy_WaitsForAuthorityWhenCommandMayTouchRandomOrHiddenFacts()
		{
			ActionRequest request = new ActionRequest(
				"test.action.random-result",
				Array.Empty<ActionRequestBinding>());
			TabletopCommandIntent startAction = TabletopCommandIntent.StartAction(request);
			TabletopCommandIntent confirmTurn = TabletopCommandIntent.ConfirmTurn();
			TabletopCommandIntent confirmDayCycle = TabletopCommandIntent.ConfirmDayCycle();
			TabletopCommandIntent dragPreview = TabletopCommandIntent.MoveSingleCard(
				new TabletopCardId(2uL),
				Vector2.one);

			Assert.That(TabletopCommandPredictionPolicy.CanShowBeforeAuthority(startAction), Is.False);
			Assert.That(TabletopCommandPredictionPolicy.CanShowBeforeAuthority(confirmTurn), Is.False);
			Assert.That(TabletopCommandPredictionPolicy.CanShowBeforeAuthority(confirmDayCycle), Is.False);
			Assert.That(TabletopCommandPredictionPolicy.CanMutateLocalStateBeforeAuthority(dragPreview), Is.False);
			Assert.That(TabletopCommandPredictionPolicy.CanShowBeforeAuthority(dragPreview), Is.True);
		}

		[Test]
		public void SnapshotEnvelope_SeparatesPublicStateFromSeatPrivateState()
		{
			PlayerSeatId seatId = new PlayerSeatId("seat.player-1");

			AuthoritySnapshotEnvelope<string> publicSnapshot = new AuthoritySnapshotEnvelope<string>(
				new AuthorityRevision(2uL),
				SnapshotVisibilityScope.Public,
				default,
				"public-tabletop");
			AuthoritySnapshotEnvelope<string> privateSnapshot = new AuthoritySnapshotEnvelope<string>(
				new AuthorityRevision(2uL),
				SnapshotVisibilityScope.Seat,
				seatId,
				"private-hand");

			Assert.That(publicSnapshot.IsVisibleTo(new PlayerSeatId("seat.anyone")), Is.True);
			Assert.That(privateSnapshot.IsVisibleTo(seatId), Is.True);
			Assert.That(privateSnapshot.IsVisibleTo(new PlayerSeatId("seat.other")), Is.False);
			Assert.Throws<ArgumentException>(() => new AuthoritySnapshotEnvelope<string>(
				new AuthorityRevision(1uL),
				SnapshotVisibilityScope.Seat,
				default,
				"missing-recipient"));
			Assert.Throws<ArgumentException>(() => new AuthoritySnapshotEnvelope<string>(
				new AuthorityRevision(1uL),
				SnapshotVisibilityScope.Public,
				seatId,
				"public-with-recipient"));
		}

		[Test]
		public void SeatRosterRejectsDuplicatesAndObserversCannotSubmitGameplayCommands()
		{
			PlayerSeat host = new PlayerSeat(new PlayerSeatId("seat.host"), "Host", PlayerSeatKind.Player);
			PlayerSeat observer = new PlayerSeat(new PlayerSeatId("seat.observer"), "Observer", PlayerSeatKind.Observer);
			TabletopCommandEnvelope observerCommand = new TabletopCommandEnvelope(
				observer.Id,
				1uL,
				new AuthorityRevision(0uL),
				TabletopCommandIntent.MoveCardStack(new TabletopCardId(1uL), Vector2.zero));

			PlayerSeatRoster roster = new PlayerSeatRoster(new[] { host, observer });

			Assert.That(roster.Require(host.Id), Is.SameAs(host));
			Assert.Throws<InvalidOperationException>(() => new PlayerSeatRoster(new[] { host, host }));
			Assert.Throws<InvalidOperationException>(
				() => TabletopCommandAuthorization.RequireSeatCanSubmit(observer, observerCommand));
			Assert.Throws<InvalidOperationException>(
				() => TabletopCommandAuthorization.RequireSeatCanSubmit(host, observerCommand));
		}

		[Test]
		public void CommandReceiptDistinguishesAcceptedAndRejectedResults()
		{
			TabletopCommandReceipt accepted = TabletopCommandReceipt.Accept(
				11uL,
				new AuthorityRevision(9uL));
			TabletopCommandReceipt rejected = TabletopCommandReceipt.Reject(
				12uL,
				new AuthorityRevision(9uL),
				TabletopCommandRejectionReason.StaleRevision,
				"客户端基于过期牌桌状态提交命令。");

			Assert.That(accepted.Accepted, Is.True);
			Assert.That(accepted.RejectionReason, Is.EqualTo(TabletopCommandRejectionReason.None));
			Assert.That(rejected.Accepted, Is.False);
			Assert.That(rejected.RejectionReason, Is.EqualTo(TabletopCommandRejectionReason.StaleRevision));
			Assert.That(rejected.Message, Does.Contain("过期牌桌状态"));
			Assert.Throws<ArgumentException>(() => TabletopCommandReceipt.Reject(
				13uL,
				new AuthorityRevision(9uL),
				TabletopCommandRejectionReason.None,
				"missing-reason"));
		}
	}
}
