using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Cysharp.Threading.Tasks;
using GameCore;
using Gameplay.Content;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 进程级单局编排入口，负责开始、推进和结束唯一活动剧本实例。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioDirector : AGameSystem
	{
		private const string YooAssetContentTag = "gameplay-content";
		private static readonly IReadOnlyCollection<Type> SystemStartupDependencies =
			new[] { typeof(SceneSystem) };

		private ResourceHandle<IList<ContentAsset>> m_contentHandle;

		private ScenarioRun m_activeRun;
		private bool m_scenarioChangeInProgress;
		private string m_returnSceneAddress = string.Empty;

		public override IReadOnlyCollection<Type> StartupDependencies => SystemStartupDependencies;

		public bool HasActiveScenario => m_activeRun != null;

		public ContentId ActiveScenarioId => m_activeRun?.ScenarioId ?? default(ContentId);

		public ScenarioRun ActiveRun => m_activeRun;

		public bool IsChangingScenario => m_scenarioChangeInProgress;

		private void Awake()
		{
			base.enabled = false;
		}

		public override void OnSystemStart()
		{
			if (base.enabled)
			{
				throw new InvalidOperationException("剧本导演已经启动，不能重复进入启动生命周期。");
			}
			base.enabled = true;
		}

		public override void OnSystemStop()
		{
			base.enabled = false;
			ReleaseActiveRun();
		}

		public override void OnSystemShutdown()
		{
			base.enabled = false;
			ReleaseActiveRun();
		}

		/// <summary>
		/// 从当前默认包和已启用 Mod 包的内容快照开始一个剧本单局；内容集合与资源句柄只存续到本次单局结束。
		/// </summary>
		public async UniTask StartScenarioAsync(ContentId scenarioId)
		{
			await StartScenarioAsync(scenarioId, CreateAuthoritativeRandomSeed());
		}

		/// <summary>
		/// 使用调用方提供的权威根种子开始单局。联机权威端、回放和确定性测试使用此入口；
		/// 所有地区牌桌的随机流都由本次单局继续派生，调用方不再逐牌桌初始化。
		/// </summary>
		public async UniTask StartScenarioAsync(ContentId scenarioId, uint authoritativeRandomSeed)
		{
			RequireRunningSystem();
			RequireNoScenarioChange();
			if (HasActiveScenario)
			{
				throw new InvalidOperationException($"剧本 {m_activeRun.ScenarioId} 仍在运行，不能同时开始剧本 {scenarioId}。");
			}
			if (!scenarioId.IsValid)
			{
				throw new InvalidOperationException("不能用无效内容 ID 开始剧本单局。");
			}
			if (authoritativeRandomSeed == 0u)
			{
				throw new ArgumentOutOfRangeException(
					nameof(authoritativeRandomSeed),
					"剧本单局的权威随机根种子不能为 0。");
			}

			m_scenarioChangeInProgress = true;
			ResourceHandle<IList<ContentAsset>> contentHandle =
				ResourceSystem.LoadAssetsByAssetTagAsync<ContentAsset>(YooAssetContentTag);
			bool handleTransferred = false;
			try
			{
				IList<ContentAsset> contentAssets = await contentHandle.ToUniTask();
				ContentIndex contentIndex = ContentIndex.Build(contentAssets);
				if (!contentIndex.TryGet(scenarioId, out ScenarioDefinition definition))
				{
					throw new InvalidOperationException($"内容 {scenarioId} 不存在或不是剧本定义。");
				}
				if (!contentIndex.TryGet(
					definition.InitialRegionId,
					out ScenarioRegionDefinition initialRegionDefinition))
				{
					throw new InvalidOperationException(
						$"剧本 {scenarioId} 的初始地区 {definition.InitialRegionId} 不存在或类型错误。");
				}

				string sourceSceneAddress = SceneManager.GetActiveScene().name;
				bool changesScene = !string.IsNullOrWhiteSpace(initialRegionDefinition.SceneAddress) &&
					!string.Equals(
						sourceSceneAddress,
						initialRegionDefinition.SceneAddress,
						StringComparison.Ordinal);
				if (changesScene)
				{
					await GameManager.SceneSystem.TransitionToAsync(initialRegionDefinition.SceneAddress);
					RequireRunningSystem();
				}

				ScenarioRun run = new ScenarioRun(
					definition,
					contentIndex,
					authoritativeRandomSeed);
				run.ActivateInitialQuests();
				m_contentHandle = contentHandle;
				handleTransferred = true;
				m_returnSceneAddress = changesScene ? sourceSceneAddress : string.Empty;
				m_activeRun = run;
				EventKit.Type.Send(new ScenarioRunChangedEvent(null, run));
			}
			finally
			{
				if (!handleTransferred)
				{
					contentHandle.Dispose();
				}
				m_scenarioChangeInProgress = false;
			}
		}

		private static uint CreateAuthoritativeRandomSeed()
		{
			byte[] bytes = new byte[sizeof(uint)];
			using RandomNumberGenerator generator = RandomNumberGenerator.Create();
			do
			{
				generator.GetBytes(bytes);
			}
			while (BitConverter.ToUInt32(bytes, 0) == 0u);
			return BitConverter.ToUInt32(bytes, 0);
		}

		public async UniTask EndScenarioAsync()
		{
			RequireRunningSystem();
			RequireNoScenarioChange();
			RequireActiveRun();

			m_scenarioChangeInProgress = true;
			string returnSceneAddress = m_returnSceneAddress;
			try
			{
				ReleaseActiveRun();
				if (!string.IsNullOrWhiteSpace(returnSceneAddress) &&
					!string.Equals(
						SceneManager.GetActiveScene().name,
						returnSceneAddress,
						StringComparison.Ordinal))
				{
					await GameManager.SceneSystem.TransitionToAsync(returnSceneAddress);
				}
			}
			finally
			{
				m_returnSceneAddress = string.Empty;
				m_scenarioChangeInProgress = false;
			}
		}

		public int ConfirmTurn()
		{
			RequireNoScenarioChange();
			return RequireActiveRun().ConfirmTurn();
		}

		/// <summary>
		/// 把当前活动单局作为独立模块写入 GameCore 的整数槽位容器。
		/// 同一槽位中其它领域模块会被保留，剧本导演不直接管理文件路径。
		/// </summary>
		public bool SaveActiveRunToSlot(int slotId)
		{
			RequireRunningSystem();
			RequireNoScenarioChange();
			ScenarioRun run = RequireActiveRun();
			ScenarioRunSnapshot snapshot = run.CreateSnapshot();
			SaveData container = SaveSystem.ExtractSaveContainerFromFile(slotId) ??
				SaveSystem.CreateSaveContainer();
			container.RegisterModule(snapshot);
			return SaveSystem.StoreSaveDataToFile(
				slotId,
				container,
				CreateSaveDisplayName(run));
		}

		/// <summary>
		/// 从 GameCore 整数槽位恢复整局。文件和内容解析完成后先构造完整候选单局，
		/// 目标地区场景成功切换后才替换当前活动单局。
		/// </summary>
		public async UniTask LoadRunFromSlotAsync(int slotId)
		{
			RequireRunningSystem();
			RequireNoScenarioChange();

			SaveData container = SaveSystem.ExtractSaveContainerFromFile(slotId);
			if (container == null)
			{
				throw new InvalidOperationException($"存档槽位 {slotId} 不存在或无法读取。");
			}

			m_scenarioChangeInProgress = true;
			ResourceHandle<IList<ContentAsset>> contentHandle =
				ResourceSystem.LoadAssetsByAssetTagAsync<ContentAsset>(YooAssetContentTag);
			bool handleTransferred = false;
			ScenarioRun restoredRun = null;
			try
			{
				IList<ContentAsset> contentAssets = await contentHandle.ToUniTask();
				ContentIndex contentIndex = ContentIndex.Build(contentAssets);
				restoredRun = CreateRunFromSaveContainer(container, contentIndex);
				string targetSceneAddress = GetActiveRegionSceneAddress(restoredRun);
				string currentSceneAddress = SceneManager.GetActiveScene().name;
				string returnSceneAddress = HasActiveScenario
					? m_returnSceneAddress
					: currentSceneAddress;
				bool changesScene = !string.IsNullOrWhiteSpace(targetSceneAddress) &&
					!string.Equals(currentSceneAddress, targetSceneAddress, StringComparison.Ordinal);

				if (changesScene)
				{
					await GameManager.SceneSystem.TransitionToAsync(targetSceneAddress);
					RequireRunningSystem();
				}

				ScenarioRun previousRun = ReplaceActiveRun(restoredRun);
				m_contentHandle = contentHandle;
				handleTransferred = true;
				m_returnSceneAddress = returnSceneAddress;
				EventKit.Type.Send(new ScenarioRunChangedEvent(previousRun, restoredRun));
			}
			finally
			{
				if (!handleTransferred)
				{
					restoredRun?.End();
					contentHandle.Dispose();
				}
				m_scenarioChangeInProgress = false;
			}
		}

		/// <summary>
		/// 把指定旅行卡牌迁移到目标地区，并通过正式 SceneSystem 切换地区的场景载体。
		/// </summary>
		public async UniTask TravelAsync(
			ContentId targetRegionId,
			IReadOnlyList<Gameplay.Tabletop.TabletopCardId> travelerCardIds)
		{
			RequireRunningSystem();
			RequireNoScenarioChange();
			ScenarioRun run = RequireActiveRun();
			ScenarioTravelPlan travel = run.BeginTravel(targetRegionId, travelerCardIds);

			m_scenarioChangeInProgress = true;
			try
			{
				await GameManager.SceneSystem.TransitionToAsync(
					travel.TargetSceneAddress,
					travel.Commit);
				RequireRunningSystem();
			}
			finally
			{
				if (!travel.IsCommitted && ReferenceEquals(m_activeRun, run))
				{
					run.CancelTravel(travel);
				}
				m_scenarioChangeInProgress = false;
			}
		}

		private void Update()
		{
			if (!m_scenarioChangeInProgress)
			{
				m_activeRun?.AdvanceRealTime(Time.deltaTime);
			}
		}

		private ScenarioRun RequireActiveRun()
		{
			RequireRunningSystem();
			if (m_activeRun == null)
			{
				throw new InvalidOperationException("当前没有活动剧本，不能执行单局操作。");
			}
			return m_activeRun;
		}

		private static string CreateSaveDisplayName(ScenarioRun run)
		{
			if (!run.ContentIndex.TryGet(run.ScenarioId, out ScenarioDefinition scenario))
			{
				throw new InvalidOperationException($"活动单局的剧本定义 {run.ScenarioId} 不存在。");
			}
			if (!run.ContentIndex.TryGet(run.ActiveRegion.Id, out ScenarioRegionDefinition region))
			{
				throw new InvalidOperationException($"活动单局的地区定义 {run.ActiveRegion.Id} 不存在。");
			}
			return $"{scenario.DisplayName} · {region.DisplayName} · 第 {run.CurrentDay} 天";
		}

		private static ScenarioRun CreateRunFromSaveContainer(
			SaveData container,
			ContentIndex contentIndex)
		{
			if (container == null)
			{
				throw new ArgumentNullException(nameof(container));
			}
			if (contentIndex == null)
			{
				throw new ArgumentNullException(nameof(contentIndex));
			}

			ScenarioRunSnapshot snapshot = container.GetModule<ScenarioRunSnapshot>();
			if (snapshot == null)
			{
				throw new InvalidOperationException("指定存档不包含剧本单局模块。");
			}
			if (!contentIndex.TryGet(snapshot.ScenarioId, out ScenarioDefinition definition))
			{
				throw new InvalidOperationException(
					$"存档引用的剧本 {snapshot.ScenarioId} 不存在或类型错误。");
			}
			return ScenarioRun.Restore(definition, contentIndex, snapshot);
		}

		private static string GetActiveRegionSceneAddress(ScenarioRun run)
		{
			if (!run.ContentIndex.TryGet(run.ActiveRegion.Id, out ScenarioRegionDefinition region))
			{
				throw new InvalidOperationException(
					$"恢复单局的当前地区 {run.ActiveRegion.Id} 不存在或类型错误。");
			}
			return region.SceneAddress;
		}

		private ScenarioRun ReplaceActiveRun(ScenarioRun run)
		{
			if (run == null)
			{
				throw new ArgumentNullException(nameof(run));
			}
			ScenarioRun previousRun = m_activeRun;
			m_activeRun = run;
			try
			{
				previousRun?.End();
			}
			catch
			{
				m_activeRun = previousRun;
				throw;
			}

			m_contentHandle.Dispose();
			m_contentHandle = default(ResourceHandle<IList<ContentAsset>>);
			m_returnSceneAddress = string.Empty;
			return previousRun;
		}

		private void RequireRunningSystem()
		{
			if (!base.enabled)
			{
				throw new InvalidOperationException("剧本导演尚未启动，不能开始或修改单局剧本。");
			}
		}

		private void RequireNoScenarioChange()
		{
			if (m_scenarioChangeInProgress)
			{
				throw new InvalidOperationException("剧本正在开始或结束，不能同时发起另一次剧本切换。");
			}
		}

		private void ReleaseActiveRun()
		{
			ScenarioRun run = m_activeRun;
			m_activeRun = null;
			try
			{
				run?.End();
			}
			finally
			{
				m_contentHandle.Dispose();
				m_contentHandle = default(ResourceHandle<IList<ContentAsset>>);
				m_returnSceneAddress = string.Empty;
			}
			if (run != null)
			{
				EventKit.Type.Send(new ScenarioRunChangedEvent(run, null));
			}
		}
	}
}
