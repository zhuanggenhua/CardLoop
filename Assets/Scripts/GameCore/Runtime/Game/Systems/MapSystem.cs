using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 地图、检查点和传送的运行时真相源。
    /// 它统一负责地图状态、出生点、检查点顺序和重生节奏。
    /// </summary>
    public class MapSystem : AGameSystem, IDataBlockHandler<MapDataBlock>
    {
        private Stack<ICheckpoint> m_checkpointStack;
        private MapInfo m_activeMapInfo;
        private readonly List<MapInfo> m_registeredMapInfos = new();
        private bool m_hasOrderedCheckpoint;
        private int m_currentCheckpointOrder = int.MinValue;
        private Coroutine m_respawnCoroutine;

        public override void OnSystemInit()
        {
            m_checkpointStack ??= new Stack<ICheckpoint>();
        }

        public override void OnSystemStart()
        {
            EventKit.Type.Register<SceneLoadedEvent>(OnSceneLoaded);
            RefreshActiveMapInfoFromRegisteredInfos();
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<SceneLoadedEvent>(OnSceneLoaded);
            StopRespawnCoroutine();
        }

        public override void OnSystemShutdown()
        {
            StopRespawnCoroutine();
            m_activeMapInfo = null;
        }

        private void OnDisable()
        {
            StopRespawnCoroutine();
        }

        private void OnDestroy()
        {
            StopRespawnCoroutine();
        }

        private void OnSceneLoaded(SceneLoadedEvent _)
        {
            RefreshActiveMapInfoFromRegisteredInfos();
        }

        /// <summary>
        /// 正式登记当前场景可用的 MapInfo。
        /// 地图配置真相仍然属于场景里的 MapInfo 组件，但“当前活动地图配置是哪一个”必须由 MapSystem 统一缓存。
        /// </summary>
        public void RegisterActiveMapInfo(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return;
            }

            if (!m_registeredMapInfos.Contains(mapInfo))
            {
                m_registeredMapInfos.Add(mapInfo);
            }

            RefreshActiveMapInfoFromRegisteredInfos();
        }

        public void UnregisterActiveMapInfo(MapInfo mapInfo)
        {
            if (mapInfo == null)
            {
                return;
            }

            m_registeredMapInfos.Remove(mapInfo);

            if (ReferenceEquals(m_activeMapInfo, mapInfo))
            {
                m_activeMapInfo = null;
            }

            RefreshActiveMapInfoFromRegisteredInfos();
        }

        public void SaveCheckpoint(ICheckpoint checkpoint)
        {
            SaveCheckpoint(checkpoint, int.MinValue, true);
        }

        /// <summary>
        /// 保存当前重生点。带顺序的入口用于场景触发器，保留 TopDown CheckPoint 的“只向前推进”体验。
        /// </summary>
        public void SaveCheckpoint(ICheckpoint checkpoint, int checkpointOrder, bool forceAssignation = false)
        {
            EnsureValidCheckpoint(checkpoint, nameof(SaveCheckpoint));

            if (!forceAssignation && m_hasOrderedCheckpoint && checkpointOrder < m_currentCheckpointOrder)
            {
                Debug.Log($"Skipping checkpoint order {checkpointOrder}; current checkpoint order is {m_currentCheckpointOrder}.");
                return;
            }

            checkpoint.UpdateSceneAddress();
            Debug.Log($"Saving checkpoint from scene address '{checkpoint.sceneAddress}' at position: {checkpoint.position}...");
            m_checkpointStack.Push(checkpoint);
            m_hasOrderedCheckpoint = true;
            m_currentCheckpointOrder = checkpointOrder;
        }

        public void RespawnPlayer()
        {
            if (m_respawnCoroutine != null)
            {
                return;
            }

            m_respawnCoroutine = StartCoroutine(RespawnPlayerCoroutine());
        }

        private void StopRespawnCoroutine()
        {
            if (m_respawnCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_respawnCoroutine);
            m_respawnCoroutine = null;
        }

        internal ICheckpoint FindValidCheckpoint()
        {
            while (m_checkpointStack.Count > 0)
            {
                ICheckpoint checkpoint = m_checkpointStack.Peek();

                if (checkpoint.IsValid())
                {
                    return checkpoint;
                }

                Debug.LogWarning("Invalid checkpoint data found! Skipping...");
                m_checkpointStack.Pop();
            }

            return null;
        }

        internal ICheckpoint FindPlaytestCheckpoint()
        {
            MapInfo mapInfo = ResolveActiveMapInfo();
            if (mapInfo == null)
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] Playtest spawn requires an active {nameof(MapInfo)} registered for the current map.");
            }

            if (!mapInfo.TryGetPlaytestCheckpoint(out ICheckpoint checkpoint))
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] Playtest spawn requires a valid playtest checkpoint on the active {nameof(MapInfo)}.");
            }

            if (!string.IsNullOrEmpty(checkpoint.sceneAddress))
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] Playtest checkpoint must leave its scene address empty so the current scene can be used.");
            }

            return checkpoint;
        }

        internal ICheckpoint FindInitialSpawnCheckpoint()
        {
            MapInfo mapInfo = ResolveActiveMapInfo();
            return mapInfo != null && mapInfo.TryGetInitialSpawnCheckpoint(out ICheckpoint checkpoint) ? checkpoint : null;
        }

        /// <summary>
        /// 当前活动地图配置只允许从正式登记过的 MapInfo 集合里选出，
        /// 不再靠场景树扫描补真相。
        /// </summary>
        internal void RefreshActiveMapInfoFromRegisteredInfos()
        {
            Scene trackedScene = ResolveTrackedScene();
            if (!trackedScene.IsValid() || !trackedScene.isLoaded)
            {
                m_activeMapInfo = null;
                return;
            }

            m_registeredMapInfos.RemoveAll(static mapInfo => mapInfo == null);
            m_activeMapInfo = m_registeredMapInfos.Find(mapInfo => mapInfo.gameObject.scene == trackedScene);
        }

        internal MapInfo ResolveActiveMapInfo()
        {
            if (m_activeMapInfo == null)
            {
                RefreshActiveMapInfoFromRegisteredInfos();
            }

            return m_activeMapInfo;
        }

        internal bool TryGetActiveTerrainNavigationMap(out TerrainNavigationMap terrainNavigationMap)
        {
            terrainNavigationMap = null;
            MapInfo mapInfo = ResolveActiveMapInfo();
            return mapInfo != null && mapInfo.TryGetTerrainNavigationMap(out terrainNavigationMap);
        }

        private Scene ResolveTrackedScene()
        {
            SceneHandler currentSceneHandler = SceneKit.GetActiveSceneHandler();
            if (currentSceneHandler != null)
            {
                Scene currentScene = currentSceneHandler.Scene;
                if (currentScene.IsValid() && currentScene.isLoaded)
                {
                    return currentScene;
                }
            }

            return SceneManager.GetActiveScene();
        }

        internal float GetRespawnDelay()
        {
            return m_activeMapInfo != null ? m_activeMapInfo.respawnDelay : 0f;
        }

        /// <summary>
        /// 吸收 uMMORPG `Database.CharacterLoad()` 的出生点健壮性规则：
        /// 读档进入当前地图后，如果保存位置对当前 2D 碰撞闭包已不合法，就回退到本地图正式初始出生点，
        /// 而不是依赖后续角色更新去碰运气脱墙。
        /// </summary>
        internal void EnsureTraversalCharacterValidSpawnOnActiveMap()
        {
            CharacterActor traversalCharacter = GetRequiredTraversalCharacter(nameof(EnsureTraversalCharacterValidSpawnOnActiveMap));

            if (traversalCharacter.IsValidSpawnPoint(traversalCharacter.transform.position))
            {
                return;
            }

            ICheckpoint checkpoint = FindRequiredInitialSpawnCheckpoint(nameof(EnsureTraversalCharacterValidSpawnOnActiveMap));

            SaveCheckpoint(checkpoint);
            traversalCharacter.TeleportTo(checkpoint.position);
        }

        /// <summary>
        /// 当前地图传送与重生绑定玩家存档主角色。
        /// 在队伍控制和世界角色实体彻底拆开前，不能让“谁触发世界穿越”与“谁被传送/复活”分别落在两套真相上。
        /// </summary>
        internal CharacterActor GetTraversalCharacter()
        {
            return GameManager.PlayerSystem.GetPrimaryPlayerCharacter();
        }

        public void TeleportTo(ICheckpoint checkpoint, Action onMapLoaded = null, Action onCompletion = null)
        {
            EnsureValidCheckpoint(checkpoint, nameof(TeleportTo));

            CharacterActor traversalCharacter = GetRequiredTraversalCharacter(nameof(TeleportTo));

            GetRequiredSceneSystem().TransitionTo(checkpoint.sceneAddress, () =>
            {
                traversalCharacter.TeleportTo(checkpoint.position);
                onMapLoaded?.Invoke();
            }, onCompletion);
        }

        public void TeleportToInitialSpawnPosition(string sceneAddress, Action onCompletion = null)
        {
            GetRequiredSceneSystem().TransitionTo(sceneAddress, () =>
            {
                ICheckpoint checkpoint = FindRequiredInitialSpawnCheckpoint(nameof(TeleportToInitialSpawnPosition));
                CharacterActor traversalCharacter = GetRequiredTraversalCharacter(nameof(TeleportToInitialSpawnPosition));

                SaveCheckpoint(checkpoint);
                traversalCharacter.TeleportTo(checkpoint.position);
            }, onCompletion);
        }

        public void TeleportToPlaytestStartPosition(string sceneAddress, Action onCompletion = null)
        {
            GetRequiredSceneSystem().TransitionTo(sceneAddress, () =>
            {
                ICheckpoint checkpoint = FindPlaytestCheckpoint();
                CharacterActor traversalCharacter = GetRequiredTraversalCharacter(nameof(TeleportToPlaytestStartPosition));

                SaveCheckpoint(checkpoint);
                traversalCharacter.TeleportTo(checkpoint.position);
            }, onCompletion);
        }

        public MapDataBlock CreateDataBlock()
        {
            return new MapDataBlock
            {
                currentSceneAddress = GameManager.SceneSystem.CurrentSceneAddress,
                checkpoints = m_checkpointStack.ToArray(),
                hasOrderedCheckpoint = m_hasOrderedCheckpoint,
                currentCheckpointOrder = m_currentCheckpointOrder,
            };
        }

        public void LoadDataBlock(MapDataBlock block)
        {
            LoadDataBlock(block, null);
        }

        /// <summary>
        /// 恢复地图快照，并在过场、地图生命周期和落点校验全部完成后通知调用方。
        /// </summary>
        internal void LoadDataBlock(MapDataBlock block, Action onCompletion)
        {
            if (block == null)
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] Loading a save file requires a map data block.");
            }

            ICheckpoint[] checkpoints = block.checkpoints ?? Array.Empty<ICheckpoint>();
            m_checkpointStack = new Stack<ICheckpoint>(checkpoints.Reverse());
            m_hasOrderedCheckpoint = block.hasOrderedCheckpoint;
            m_currentCheckpointOrder = m_hasOrderedCheckpoint ? block.currentCheckpointOrder : int.MinValue;
            string savedSceneAddress = block.currentSceneAddress ?? string.Empty;

            if (block.playtest)
            {
                TeleportToPlaytestStartPosition(savedSceneAddress, onCompletion);
            }
            else
            {
                bool isFirstTimePlaying = string.IsNullOrEmpty(savedSceneAddress);

                if (isFirstTimePlaying)
                {
                    ICheckpoint checkpoint = FindValidCheckpoint();
                    Debug.Assert(checkpoint != null, "No valid checkpoint set in the save file! Did you forget to add one or specify a valid map & identifier?");
                    TeleportTo(checkpoint, onCompletion: onCompletion);
                }
                else
                {
                    GetRequiredSceneSystem().TransitionTo(
                        savedSceneAddress,
                        EnsureTraversalCharacterValidSpawnOnActiveMap,
                        onCompletion);
                }
            }
        }

        private IEnumerator RespawnPlayerCoroutine()
        {
            try
            {
                ICheckpoint checkpoint = FindRequiredRespawnCheckpoint();

                float delay = GetRespawnDelay();
                if (delay > 0f)
                {
                    yield return new WaitForSeconds(delay);
                }

                CharacterActor traversalCharacter = GetRequiredTraversalCharacter(nameof(RespawnPlayer));
                TeleportTo(checkpoint, traversalCharacter.Revive);
            }
            finally
            {
                m_respawnCoroutine = null;
            }
        }

        private static SceneSystem GetRequiredSceneSystem()
        {
            SceneSystem sceneSystem = GameManager.SceneSystem;
            if (sceneSystem == null || !sceneSystem.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] 跨场景地图操作需要一个启用的 {nameof(SceneSystem)}。");
            }

            return sceneSystem;
        }

        private static void EnsureValidCheckpoint(ICheckpoint checkpoint, string operationName)
        {
            if (checkpoint == null || !checkpoint.IsValid())
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] {operationName} requires a valid checkpoint and cannot silently skip the map result.");
            }
        }

        private ICheckpoint FindRequiredInitialSpawnCheckpoint(string operationName)
        {
            ICheckpoint checkpoint = FindInitialSpawnCheckpoint();
            EnsureValidCheckpoint(checkpoint, operationName);
            return checkpoint;
        }

        private ICheckpoint FindRequiredRespawnCheckpoint()
        {
            ICheckpoint checkpoint = FindValidCheckpoint();
            EnsureValidCheckpoint(checkpoint, nameof(RespawnPlayer));
            return checkpoint;
        }

        private CharacterActor GetRequiredTraversalCharacter(string operationName)
        {
            CharacterActor traversalCharacter = GetTraversalCharacter();
            if (traversalCharacter == null)
            {
                throw new InvalidOperationException(
                    $"[{nameof(MapSystem)}] {operationName} requires {nameof(PlayerSystem)} to provide a primary traversal character.");
            }

            return traversalCharacter;
        }
    }
}
