using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 地表元素运行时表现桥接器。
    /// 它只同步临时火焰/蒸汽 Tile 和草覆盖层显隐，不修改规则 Tilemap，也不把燃尽结果写成新的作者地形。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainSurfacePresentation : MonoBehaviour
    {
        [Header("状态来源")]
        [InspectorName("地形导航地图")]
        [Tooltip("地表运行时状态的真相源。这里订阅它的格子状态变化，不直接扫描视觉 Tilemap 推断玩法。")]
        [SerializeField] private TerrainNavigationMap m_navigationMap = null;

        [InspectorName("地表表现配置")]
        [Tooltip("运行时状态、一次性表现信号到 Tile 的映射表。缺映射时会报警，不会静默用占位 Tile 顶替。")]
        [SerializeField] private TerrainSurfacePresentationConfig m_config = null;

        [Header("表现层")]
        [InspectorName("临时效果 Tilemap")]
        [Tooltip("只承载燃烧、蒸汽等短期元素效果。永久地貌变化必须回到世界地形变更链路。")]
        [SerializeField] private Tilemap m_temporaryEffectTilemap = null;

        private readonly List<Vector3Int> m_runtimeCells = new();
        private readonly Dictionary<Vector3Int, Coroutine> m_signalCoroutines = new();

        // 覆盖层被燃尽时只临时隐藏原 Tile，保留作者 Tile 数据，便于退出 PlayMode 或状态清空时恢复。
        private readonly Dictionary<int, HiddenSurfaceCoverSource> m_hiddenSurfaceCoverSources =
            new();

        private readonly HashSet<string> m_reportedMissingMappings = new();

        private void OnEnable()
        {
            if (!ValidateReferences())
            {
                return;
            }

            m_navigationMap.CellStateChanged += OnCellStateChanged;
            m_navigationMap.RuntimeSurfaceStatesCleared += OnRuntimeSurfaceStatesCleared;
            RefreshAllRuntimeCells();
        }

        private void OnDisable()
        {
            if (m_navigationMap != null)
            {
                m_navigationMap.CellStateChanged -= OnCellStateChanged;
                m_navigationMap.RuntimeSurfaceStatesCleared -= OnRuntimeSurfaceStatesCleared;
            }

            StopAllSignalCoroutines();
            RestoreAllSurfaceCoverCells();
        }

        private bool ValidateReferences()
        {
            bool valid = true;
            if (m_navigationMap == null)
            {
                Debug.LogError(
                    "TerrainSurfacePresentation 缺少 TerrainNavigationMap 引用。",
                    this);
                valid = false;
            }

            if (m_config == null)
            {
                Debug.LogError(
                    "TerrainSurfacePresentation 缺少地表元素表现配置。",
                    this);
                valid = false;
            }

            if (m_temporaryEffectTilemap == null)
            {
                Debug.LogError(
                    "TerrainSurfacePresentation 缺少临时效果 Tilemap。",
                    this);
                valid = false;
            }

            return valid;
        }

        private void RefreshAllRuntimeCells()
        {
            m_temporaryEffectTilemap.ClearAllTiles();
            RestoreAllSurfaceCoverCells();
            m_navigationMap.CollectRuntimeStateCells(m_runtimeCells);
            for (int i = 0; i < m_runtimeCells.Count; i++)
            {
                if (m_navigationMap.TryGetSurfaceSample(
                        m_runtimeCells[i],
                        out TerrainSurfaceSample sample))
                {
                    RefreshCell(sample);
                }
            }
        }

        private void OnCellStateChanged(TerrainCellStateChange change)
        {
            RefreshCell(change.Current);

            if (change.PresentationSignal != EElementPresentationSignal.None)
            {
                PlaySignal(change.Cell, change.PresentationSignal);
            }
        }

        private void OnRuntimeSurfaceStatesCleared()
        {
            StopAllSignalCoroutines();
            m_temporaryEffectTilemap.ClearAllTiles();
            RestoreAllSurfaceCoverCells();
        }

        private void RefreshCell(in TerrainSurfaceSample sample)
        {
            RefreshSurfaceCoverCell(sample);

            if (m_config.TryGetTemporaryStateTile(
                    sample.RuntimeStateSnapshot,
                    out TileBase temporaryTile))
            {
                m_temporaryEffectTilemap.SetTile(sample.Cell, temporaryTile);
            }
            else
            {
                m_temporaryEffectTilemap.SetTile(sample.Cell, null);
                if (sample.ActiveStates.Count > 0)
                {
                    ReportMissingMappingOnce($"state:{sample.RuntimeState}");
                }
            }

            // 燃尽后的“露出土壤”不是盖一张结果 Tile，而是由正式地图层移除草覆盖后露出底层土壤。
            // 这里仍只负责临时元素效果；永久/持久地貌变化必须走后续世界地形变更链路。
        }

        private void RefreshSurfaceCoverCell(in TerrainSurfaceSample sample)
        {
            if (sample.BaseSurfaceCover == ETerrainSurfaceCoverKind.None ||
                !sample.SurfaceCoverSource.IsValid)
            {
                return;
            }

            if (!m_navigationMap.TryGetSurfaceCoverTilemap(
                    sample.SurfaceCoverSource,
                    out Tilemap surfaceTilemap))
            {
                ReportMissingMappingOnce($"surface-source:{sample.SurfaceCoverSource.SourceId}");
                return;
            }

            bool shouldHide =
                sample.EffectiveSurfaceCover == ETerrainSurfaceCoverKind.None ||
                sample.SurfaceCoverLifecycle == ETerrainSurfaceCoverLifecycle.Removed;
            if (shouldHide)
            {
                HideMappedSurfaceCoverCells(sample.Cell);
            }
            else
            {
                RestoreMappedSurfaceCoverCells(sample.Cell);
            }
        }

        private void HideMappedSurfaceCoverCells(Vector3Int cell)
        {
            IReadOnlyList<TerrainSurfaceLayerSource> sources =
                m_navigationMap.SurfaceLayerSources;
            for (int i = 0; i < sources.Count; i++)
            {
                TerrainSurfaceLayerSource source = sources[i];
                if (source == null ||
                    !source.IsValid ||
                    !source.TryResolveSurfaceCover(
                        cell,
                        out ETerrainSurfaceCoverKind coverKind,
                        out _) ||
                    coverKind == ETerrainSurfaceCoverKind.None)
                {
                    continue;
                }

                HideSurfaceCoverCell(
                    new TerrainSurfaceCoverSourceReference(
                        source.SourceId,
                        source.Role),
                    source.Tilemap,
                    cell);
            }
        }

        private void RestoreMappedSurfaceCoverCells(Vector3Int cell)
        {
            IReadOnlyList<TerrainSurfaceLayerSource> sources =
                m_navigationMap.SurfaceLayerSources;
            for (int i = 0; i < sources.Count; i++)
            {
                TerrainSurfaceLayerSource source = sources[i];
                if (source == null ||
                    !source.IsValid ||
                    !source.TryResolveSurfaceCover(
                        cell,
                        out ETerrainSurfaceCoverKind coverKind,
                        out _) ||
                    coverKind == ETerrainSurfaceCoverKind.None)
                {
                    continue;
                }

                RestoreSurfaceCoverCell(
                    new TerrainSurfaceCoverSourceReference(
                        source.SourceId,
                        source.Role),
                    source.Tilemap,
                    cell);
            }
        }

        private void HideSurfaceCoverCell(
            in TerrainSurfaceCoverSourceReference sourceReference,
            Tilemap surfaceTilemap,
            Vector3Int cell)
        {
            if (!m_hiddenSurfaceCoverSources.TryGetValue(
                    sourceReference.SourceId,
                    out HiddenSurfaceCoverSource hiddenSource))
            {
                hiddenSource = new HiddenSurfaceCoverSource(sourceReference);
                m_hiddenSurfaceCoverSources.Add(sourceReference.SourceId, hiddenSource);
            }

            if (hiddenSource.Cells.Add(cell))
            {
                surfaceTilemap.SetTileFlags(cell, TileFlags.None);
            }

            // 用透明度隐藏覆盖层，而不是 SetTile(null)，这样作者原 Tile、GUID 和 Palette 来源仍可恢复。
            Color color = surfaceTilemap.GetColor(cell);
            if (!Mathf.Approximately(color.a, 0.0f))
            {
                color.a = 0.0f;
                surfaceTilemap.SetColor(cell, color);
            }
        }

        private void RestoreSurfaceCoverCell(
            in TerrainSurfaceCoverSourceReference sourceReference,
            Tilemap surfaceTilemap,
            Vector3Int cell)
        {
            if (!m_hiddenSurfaceCoverSources.TryGetValue(
                    sourceReference.SourceId,
                    out HiddenSurfaceCoverSource hiddenSource) ||
                !hiddenSource.Cells.Remove(cell))
            {
                return;
            }

            Color color = surfaceTilemap.GetColor(cell);
            if (!Mathf.Approximately(color.a, 1.0f))
            {
                color.a = 1.0f;
                surfaceTilemap.SetColor(cell, color);
            }

            if (hiddenSource.Cells.Count == 0)
            {
                m_hiddenSurfaceCoverSources.Remove(sourceReference.SourceId);
            }
        }

        private void RestoreAllSurfaceCoverCells()
        {
            foreach (HiddenSurfaceCoverSource hiddenSource in m_hiddenSurfaceCoverSources.Values)
            {
                if (!m_navigationMap.TryGetSurfaceCoverTilemap(
                        hiddenSource.SourceReference,
                        out Tilemap surfaceTilemap))
                {
                    continue;
                }

                foreach (Vector3Int cell in hiddenSource.Cells)
                {
                    Color color = surfaceTilemap.GetColor(cell);
                    color.a = 1.0f;
                    surfaceTilemap.SetColor(cell, color);
                }
            }

            m_hiddenSurfaceCoverSources.Clear();
        }

        private void PlaySignal(
            Vector3Int cell,
            EElementPresentationSignal signal)
        {
            if (!m_config.TryGetSignalTile(signal, out TileBase tile, out float duration))
            {
                ReportMissingMappingOnce($"signal:{signal}");
                if (m_navigationMap.TryGetSurfaceSample(cell, out TerrainSurfaceSample sample))
                {
                    RefreshCell(sample);
                }

                return;
            }

            if (m_signalCoroutines.TryGetValue(cell, out Coroutine activeCoroutine))
            {
                StopCoroutine(activeCoroutine);
            }

            m_temporaryEffectTilemap.SetTile(cell, tile);
            m_signalCoroutines[cell] = StartCoroutine(
                RestoreTemporaryStateAfterSignal(cell, duration));
        }

        /// <summary>
        /// 一次性表现信号结束后回到当前运行时状态，而不是盲目清空 Tile。
        /// 这样同一格仍在 Burning 等状态中时，不会被 Steam 之类的瞬时信号误清。
        /// </summary>
        private IEnumerator RestoreTemporaryStateAfterSignal(
            Vector3Int cell,
            float duration)
        {
            yield return new WaitForSeconds(duration);
            m_signalCoroutines.Remove(cell);
            if (m_navigationMap.TryGetSurfaceSample(cell, out TerrainSurfaceSample sample))
            {
                RefreshCell(sample);
            }
            else
            {
                m_temporaryEffectTilemap.SetTile(cell, null);
            }
        }

        private void StopAllSignalCoroutines()
        {
            foreach (Coroutine coroutine in m_signalCoroutines.Values)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }

            m_signalCoroutines.Clear();
        }

        private void ReportMissingMappingOnce(string key)
        {
            if (m_reportedMissingMappings.Add(key))
            {
                Debug.LogWarning(
                    $"地表元素状态有效，但表现配置缺少映射：{key}。",
                    this);
            }
        }

        private sealed class HiddenSurfaceCoverSource
        {
            public HiddenSurfaceCoverSource(
                in TerrainSurfaceCoverSourceReference sourceReference)
            {
                SourceReference = sourceReference;
            }

            public TerrainSurfaceCoverSourceReference SourceReference { get; }
            public HashSet<Vector3Int> Cells { get; } = new();
        }
    }
}
