using System;
using System.Collections.Generic;
using Gameplay.Content;
using Gameplay.Tabletop;
using Gameplay.Tabletop.Actions;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 一次剧本运行中的地区对象，长期拥有该地区的牌桌状态，不随 Unity 场景卸载而销毁。
	/// </summary>
	public sealed class ScenarioRegion
	{
		public ContentId Id { get; }

		public string SceneAddress { get; }

		public UnityEngine.Vector2 ArrivalPosition { get; }

		public Gameplay.Tabletop.Tabletop Tabletop { get; }

		internal ScenarioRegion(
			ScenarioRegionDefinition definition,
			ContentIndex contentIndex,
			TabletopCardIdSequence cardIdSequence,
			Func<ContentId, bool> isContentDiscovered,
			Action<TabletopActionCompletion> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules,
			uint authoritativeRandomSeed)
		{
			if (definition == null)
			{
				throw new ArgumentNullException(nameof(definition));
			}
			if (definition.TabletopPlacement == null)
			{
				throw new InvalidOperationException($"剧本地区 {definition.ContentId} 缺少牌桌放置规则。");
			}

			Id = definition.ContentId;
			SceneAddress = definition.SceneAddress;
			ArrivalPosition = definition.ArrivalPosition;
			Tabletop = new Gameplay.Tabletop.Tabletop(
				contentIndex,
				definition.TabletopPlacement.CreateRuntime(),
				isContentDiscovered,
				actionCompleted,
				cardsDefeated,
				battleFormationRules,
				cardIdSequence);
			Tabletop.InitializeAuthoritativeRandom(authoritativeRandomSeed);
		}

		internal ScenarioRegion(
			ScenarioRegionDefinition definition,
			ContentIndex contentIndex,
			TabletopCardIdSequence cardIdSequence,
			Func<ContentId, bool> isContentDiscovered,
			Action<TabletopActionCompletion> actionCompleted,
			Action<IReadOnlyList<ContentId>> cardsDefeated,
			BattleFormationRules battleFormationRules,
			ScenarioRegionSnapshot snapshot)
		{
			if (definition == null)
			{
				throw new ArgumentNullException(nameof(definition));
			}
			if (snapshot == null || snapshot.RegionId != definition.ContentId)
			{
				throw new InvalidOperationException(
					$"地区快照 {snapshot?.RegionId} 与当前地区定义 {definition.ContentId} 不一致。");
			}
			if (definition.TabletopPlacement == null)
			{
				throw new InvalidOperationException($"剧本地区 {definition.ContentId} 缺少牌桌放置规则。");
			}
			if (snapshot.Tabletop == null)
			{
				throw new InvalidOperationException($"地区快照 {snapshot.RegionId} 缺少牌桌状态。");
			}

			Id = definition.ContentId;
			SceneAddress = definition.SceneAddress;
			ArrivalPosition = definition.ArrivalPosition;
			Tabletop = new Gameplay.Tabletop.Tabletop(
				contentIndex,
				snapshot.Tabletop.Cards,
				definition.TabletopPlacement.CreateRuntime(),
				snapshot.Tabletop.ActiveActions,
				isContentDiscovered,
				actionCompleted,
				cardsDefeated,
				battleFormationRules,
				cardIdSequence);
			Tabletop.RestoreAuthoritativeRandom(snapshot.Tabletop.AuthoritativeRandomState);
		}

		internal ScenarioRegionSnapshot CreateSnapshot()
		{
			return new ScenarioRegionSnapshot(Id, Tabletop.CreateSnapshot());
		}
	}
}
