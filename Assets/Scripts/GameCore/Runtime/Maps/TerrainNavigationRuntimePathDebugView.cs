using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyWord.GameCore
{
    internal readonly struct TerrainNavigationRuntimePathDebugSnapshot
    {
        public TerrainNavigationRuntimePathDebugSnapshot(
            Transform ownerTransform,
            string ownerName,
            float z,
            bool showDetails,
            float markerRadius,
            float lineWidth,
            float waypointRadius,
            int markerSegments,
            Color pathColor,
            Color failureColor,
            Color resolvedColor,
            Vector2 start,
            Vector2 click,
            Vector2 finalDestination,
            Vector2 resolvedCell,
            IReadOnlyList<Vector2> worldPath,
            bool pathSucceeded,
            string status)
        {
            OwnerTransform = ownerTransform;
            OwnerName = ownerName;
            Z = z;
            ShowDetails = showDetails;
            MarkerRadius = markerRadius;
            LineWidth = lineWidth;
            WaypointRadius = waypointRadius;
            MarkerSegments = markerSegments;
            PathColor = pathColor;
            FailureColor = failureColor;
            ResolvedColor = resolvedColor;
            Start = start;
            Click = click;
            FinalDestination = finalDestination;
            ResolvedCell = resolvedCell;
            WorldPath = worldPath ?? Array.Empty<Vector2>();
            PathSucceeded = pathSucceeded;
            Status = status ?? string.Empty;
        }

        public Transform OwnerTransform { get; }
        public string OwnerName { get; }
        public float Z { get; }
        public bool ShowDetails { get; }
        public float MarkerRadius { get; }
        public float LineWidth { get; }
        public float WaypointRadius { get; }
        public int MarkerSegments { get; }
        public Color PathColor { get; }
        public Color FailureColor { get; }
        public Color ResolvedColor { get; }
        public Vector2 Start { get; }
        public Vector2 Click { get; }
        public Vector2 FinalDestination { get; }
        public Vector2 ResolvedCell { get; }
        public IReadOnlyList<Vector2> WorldPath { get; }
        public bool PathSucceeded { get; }
        public string Status { get; }
    }

    /// <summary>
    /// 只负责把地形导航最近一次路径结果绘制成运行时调试提示。
    /// 导航规则、路径搜索和地表运行时状态仍归 TerrainNavigationMap。
    /// </summary>
    internal sealed class TerrainNavigationRuntimePathDebugView
    {
        private Transform m_runtimePathRoot;
        private LineRenderer m_runtimePathLine;
        private LineRenderer m_runtimeStartRing;
        private LineRenderer m_runtimeClickRing;
        private LineRenderer m_runtimeFinalDestinationRing;
        private LineRenderer m_runtimeResolvedCellRing;
        private LineRenderer m_runtimeFailureCross;
        private LineRenderer m_runtimeClickToResolvedLine;
        private LineRenderer m_runtimeResolvedToEndLine;
        private TextMesh m_runtimeStartLabel;
        private TextMesh m_runtimeClickLabel;
        private TextMesh m_runtimeFinalDestinationLabel;
        private TextMesh m_runtimeResolvedCellLabel;
        private TextMesh m_runtimeStatusLabel;
        private readonly List<LineRenderer> m_runtimeWaypointRings = new();
        private Material m_runtimePathMaterial;

        public void Sync(in TerrainNavigationRuntimePathDebugSnapshot snapshot)
        {
            EnsureRuntimeObjects(snapshot);
            Vector3 start = ToDebugPosition(snapshot.Start, snapshot.Z);
            Vector3 click = ToDebugPosition(snapshot.Click, snapshot.Z);
            Vector3 finalDestination = ToDebugPosition(snapshot.FinalDestination, snapshot.Z);
            Vector3 resolvedCell = ToDebugPosition(snapshot.ResolvedCell, snapshot.Z);

            if (snapshot.ShowDetails)
            {
                SyncRingRenderer(
                    m_runtimeStartRing,
                    start,
                    snapshot.MarkerRadius * 0.75f,
                    Color.white,
                    snapshot.LineWidth * 0.72f,
                    snapshot.MarkerSegments);
                SyncRingRenderer(
                    m_runtimeClickRing,
                    click,
                    snapshot.MarkerRadius * 0.72f,
                    snapshot.FailureColor,
                    snapshot.LineWidth * 0.58f,
                    snapshot.MarkerSegments);
                SyncRuntimeLabel(
                    m_runtimeStartLabel,
                    start + Vector3.up * snapshot.MarkerRadius * 1.45f,
                    "起点",
                    Color.white);
                SyncRuntimeLabel(
                    m_runtimeClickLabel,
                    click + Vector3.down * snapshot.MarkerRadius * 1.45f,
                    "点击",
                    snapshot.FailureColor);
                SyncRuntimeLabel(
                    m_runtimeStatusLabel,
                    start + Vector3.up * snapshot.MarkerRadius * 2.35f,
                    snapshot.Status,
                    Color.white);
            }
            else
            {
                ClearRuntimeNavigationDebugDetails(snapshot);
            }

            if (!snapshot.PathSucceeded)
            {
                SetLineRendererPositions(m_runtimePathLine, Array.Empty<Vector3>(), snapshot.PathColor, snapshot.LineWidth);
                SetLineRendererPositions(
                    m_runtimeFinalDestinationRing,
                    Array.Empty<Vector3>(),
                    snapshot.PathColor,
                    snapshot.LineWidth);
                SetLineRendererPositions(
                    m_runtimeResolvedCellRing,
                    Array.Empty<Vector3>(),
                    snapshot.ResolvedColor,
                    snapshot.LineWidth);
                SetLineRendererPositions(
                    m_runtimeClickToResolvedLine,
                    Array.Empty<Vector3>(),
                    snapshot.ResolvedColor,
                    snapshot.LineWidth);
                SetLineRendererPositions(
                    m_runtimeResolvedToEndLine,
                    Array.Empty<Vector3>(),
                    snapshot.PathColor,
                    snapshot.LineWidth);
                ClearRuntimeLabel(m_runtimeFinalDestinationLabel);
                ClearRuntimeLabel(m_runtimeResolvedCellLabel);
                ClearRuntimeWaypointRings(snapshot);
                SyncFailureCrossRenderer(click, snapshot);
                return;
            }

            SyncRingRenderer(
                m_runtimeFinalDestinationRing,
                finalDestination,
                snapshot.MarkerRadius,
                snapshot.PathColor,
                snapshot.LineWidth,
                snapshot.MarkerSegments);

            if (snapshot.ShowDetails)
            {
                SyncRuntimeLabel(
                    m_runtimeFinalDestinationLabel,
                    finalDestination + Vector3.up * snapshot.MarkerRadius * 1.45f,
                    "终点",
                    snapshot.PathColor);

                if ((snapshot.ResolvedCell - snapshot.FinalDestination).sqrMagnitude > 0.0001f)
                {
                    SyncRingRenderer(
                        m_runtimeResolvedCellRing,
                        resolvedCell,
                        snapshot.MarkerRadius * 0.54f,
                        snapshot.ResolvedColor,
                        snapshot.LineWidth * 0.48f,
                        snapshot.MarkerSegments);
                    SyncRuntimeLabel(
                        m_runtimeResolvedCellLabel,
                        resolvedCell + Vector3.down * snapshot.MarkerRadius * 2.35f,
                        "吸附格",
                        snapshot.ResolvedColor);
                }
                else
                {
                    SetLineRendererPositions(
                        m_runtimeResolvedCellRing,
                        Array.Empty<Vector3>(),
                        snapshot.ResolvedColor,
                        snapshot.LineWidth);
                    ClearRuntimeLabel(m_runtimeResolvedCellLabel);
                }

                SyncRuntimeSegment(
                    m_runtimeClickToResolvedLine,
                    click,
                    resolvedCell,
                    snapshot.ResolvedColor,
                    snapshot.LineWidth * 0.38f);
                SyncRuntimeSegment(
                    m_runtimeResolvedToEndLine,
                    resolvedCell,
                    finalDestination,
                    snapshot.PathColor,
                    snapshot.LineWidth * 0.38f);
            }
            else
            {
                ClearRuntimeNavigationDebugDetails(snapshot);
            }

            Vector3[] pathPositions = new Vector3[snapshot.WorldPath.Count + 1];
            pathPositions[0] = start;
            for (int i = 0; i < snapshot.WorldPath.Count; i++)
            {
                pathPositions[i + 1] = ToDebugPosition(snapshot.WorldPath[i], snapshot.Z);
            }

            SetLineRendererPositions(m_runtimePathLine, pathPositions, snapshot.PathColor, snapshot.LineWidth);
            if (snapshot.ShowDetails)
            {
                SyncRuntimeWaypointRings(snapshot);
            }
            SetLineRendererPositions(
                m_runtimeFailureCross,
                Array.Empty<Vector3>(),
                snapshot.FailureColor,
                snapshot.LineWidth);
        }

        public void Clear(Color pathColor, Color failureColor, Color resolvedColor, float lineWidth)
        {
            SetLineRendererPositions(m_runtimePathLine, Array.Empty<Vector3>(), pathColor, lineWidth);
            SetLineRendererPositions(m_runtimeFinalDestinationRing, Array.Empty<Vector3>(), pathColor, lineWidth);
            SetLineRendererPositions(m_runtimeFailureCross, Array.Empty<Vector3>(), failureColor, lineWidth);
            SetLineRendererPositions(m_runtimeStartRing, Array.Empty<Vector3>(), Color.white, lineWidth);
            SetLineRendererPositions(m_runtimeClickRing, Array.Empty<Vector3>(), failureColor, lineWidth);
            SetLineRendererPositions(m_runtimeResolvedCellRing, Array.Empty<Vector3>(), resolvedColor, lineWidth);
            SetLineRendererPositions(m_runtimeClickToResolvedLine, Array.Empty<Vector3>(), resolvedColor, lineWidth);
            SetLineRendererPositions(m_runtimeResolvedToEndLine, Array.Empty<Vector3>(), pathColor, lineWidth);
            ClearRuntimeLabel(m_runtimeStartLabel);
            ClearRuntimeLabel(m_runtimeClickLabel);
            ClearRuntimeLabel(m_runtimeFinalDestinationLabel);
            ClearRuntimeLabel(m_runtimeResolvedCellLabel);
            ClearRuntimeLabel(m_runtimeStatusLabel);
            ClearRuntimeWaypointRings(pathColor, lineWidth);
        }

        private void EnsureRuntimeObjects(in TerrainNavigationRuntimePathDebugSnapshot snapshot)
        {
            if (m_runtimePathRoot == null)
            {
                GameObject root = new($"{snapshot.OwnerName} Runtime Navigation Path");
                root.hideFlags = HideFlags.DontSave;
                root.transform.SetParent(snapshot.OwnerTransform, worldPositionStays: false);
                m_runtimePathRoot = root.transform;
            }

            m_runtimePathMaterial ??= CreateRuntimeDebugMaterial(snapshot.OwnerName);
            m_runtimePathLine = EnsureRuntimeLineRenderer(m_runtimePathLine, "Path", loop: false);
            m_runtimeStartRing = EnsureRuntimeLineRenderer(m_runtimeStartRing, "Start", loop: true);
            m_runtimeClickRing = EnsureRuntimeLineRenderer(m_runtimeClickRing, "Click", loop: true);
            m_runtimeFinalDestinationRing = EnsureRuntimeLineRenderer(m_runtimeFinalDestinationRing, "End", loop: true);
            m_runtimeResolvedCellRing = EnsureRuntimeLineRenderer(
                m_runtimeResolvedCellRing,
                "Resolved Cell",
                loop: true);
            m_runtimeFailureCross = EnsureRuntimeLineRenderer(m_runtimeFailureCross, "Failure", loop: false);
            m_runtimeClickToResolvedLine = EnsureRuntimeLineRenderer(
                m_runtimeClickToResolvedLine,
                "Click To Resolved Cell",
                loop: false);
            m_runtimeResolvedToEndLine = EnsureRuntimeLineRenderer(
                m_runtimeResolvedToEndLine,
                "Resolved Cell To End",
                loop: false);
            m_runtimeStartLabel = EnsureRuntimeTextMesh(m_runtimeStartLabel, "Start Label");
            m_runtimeClickLabel = EnsureRuntimeTextMesh(m_runtimeClickLabel, "Click Label");
            m_runtimeFinalDestinationLabel = EnsureRuntimeTextMesh(m_runtimeFinalDestinationLabel, "End Label");
            m_runtimeResolvedCellLabel = EnsureRuntimeTextMesh(
                m_runtimeResolvedCellLabel,
                "Resolved Cell Label");
            m_runtimeStatusLabel = EnsureRuntimeTextMesh(m_runtimeStatusLabel, "Status Label");
        }

        private LineRenderer EnsureRuntimeLineRenderer(
            LineRenderer lineRenderer,
            string objectName,
            bool loop)
        {
            if (lineRenderer != null)
            {
                return lineRenderer;
            }

            GameObject lineObject = new(objectName);
            lineObject.transform.SetParent(m_runtimePathRoot, worldPositionStays: false);
            lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineObject.hideFlags = HideFlags.DontSave;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = loop;
            lineRenderer.material = m_runtimePathMaterial;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sortingOrder = 32760;
            return lineRenderer;
        }

        private TextMesh EnsureRuntimeTextMesh(TextMesh textMesh, string objectName)
        {
            if (textMesh != null)
            {
                return textMesh;
            }

            GameObject textObject = new(objectName);
            textObject.transform.SetParent(m_runtimePathRoot, worldPositionStays: false);
            textObject.hideFlags = HideFlags.DontSave;
            textMesh = textObject.AddComponent<TextMesh>();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.045f;
            textMesh.richText = false;

            MeshRenderer meshRenderer = textObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 32761;
            }

            return textMesh;
        }

        private static Material CreateRuntimeDebugMaterial(string ownerName)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored");
            }

            return new Material(shader)
            {
                name = $"{ownerName} Runtime Navigation Path Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static void SyncRingRenderer(
            LineRenderer lineRenderer,
            Vector3 center,
            float radius,
            Color color,
            float width,
            int markerSegments)
        {
            int segmentCount = Mathf.Clamp(markerSegments, 12, 96);
            Vector3[] positions = new Vector3[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                float angle = i / (float)segmentCount * Mathf.PI * 2.0f;
                positions[i] = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0.0f);
            }

            SetLineRendererPositions(lineRenderer, positions, color, width);
        }

        private void SyncFailureCrossRenderer(
            Vector3 destination,
            in TerrainNavigationRuntimePathDebugSnapshot snapshot)
        {
            float radius = snapshot.MarkerRadius * 0.9f;
            Vector3[] positions =
            {
                destination + new Vector3(-radius, -radius, 0.0f),
                destination + new Vector3(radius, radius, 0.0f),
                destination,
                destination + new Vector3(-radius, radius, 0.0f),
                destination + new Vector3(radius, -radius, 0.0f)
            };

            SetLineRendererPositions(
                m_runtimeFailureCross,
                positions,
                snapshot.FailureColor,
                snapshot.LineWidth);
        }

        private static void SyncRuntimeSegment(
            LineRenderer lineRenderer,
            Vector3 from,
            Vector3 to,
            Color color,
            float width)
        {
            if ((from - to).sqrMagnitude <= 0.0001f)
            {
                SetLineRendererPositions(lineRenderer, Array.Empty<Vector3>(), color, width);
                return;
            }

            SetLineRendererPositions(
                lineRenderer,
                new[] { from, to },
                color,
                width);
        }

        private static void SyncRuntimeLabel(
            TextMesh textMesh,
            Vector3 position,
            string text,
            Color color)
        {
            if (textMesh == null)
            {
                return;
            }

            textMesh.gameObject.SetActive(true);
            textMesh.transform.position = position;
            textMesh.text = text;
            textMesh.color = color;
        }

        private static void ClearRuntimeLabel(TextMesh textMesh)
        {
            if (textMesh == null)
            {
                return;
            }

            textMesh.text = string.Empty;
            textMesh.gameObject.SetActive(false);
        }

        private void SyncRuntimeWaypointRings(in TerrainNavigationRuntimePathDebugSnapshot snapshot)
        {
            ClearRuntimeWaypointRings(snapshot);
            if (snapshot.WorldPath.Count == 0)
            {
                return;
            }

            for (int i = 0; i < snapshot.WorldPath.Count - 1; i++)
            {
                LineRenderer waypointRing = GetOrCreateRuntimeWaypointRing(i);
                SyncRingRenderer(
                    waypointRing,
                    ToDebugPosition(snapshot.WorldPath[i], snapshot.Z),
                    snapshot.WaypointRadius,
                    snapshot.PathColor,
                    snapshot.LineWidth * 0.55f,
                    snapshot.MarkerSegments);
            }
        }

        private LineRenderer GetOrCreateRuntimeWaypointRing(int index)
        {
            while (m_runtimeWaypointRings.Count <= index)
            {
                m_runtimeWaypointRings.Add(EnsureRuntimeLineRenderer(
                    null,
                    $"Waypoint {m_runtimeWaypointRings.Count:00}",
                    loop: true));
            }

            return m_runtimeWaypointRings[index];
        }

        private void ClearRuntimeWaypointRings(in TerrainNavigationRuntimePathDebugSnapshot snapshot)
        {
            ClearRuntimeWaypointRings(snapshot.PathColor, snapshot.LineWidth);
        }

        private void ClearRuntimeWaypointRings(Color pathColor, float lineWidth)
        {
            for (int i = 0; i < m_runtimeWaypointRings.Count; i++)
            {
                SetLineRendererPositions(
                    m_runtimeWaypointRings[i],
                    Array.Empty<Vector3>(),
                    pathColor,
                    lineWidth * 0.55f);
            }
        }

        private void ClearRuntimeNavigationDebugDetails(in TerrainNavigationRuntimePathDebugSnapshot snapshot)
        {
            SetLineRendererPositions(m_runtimeStartRing, Array.Empty<Vector3>(), Color.white, snapshot.LineWidth);
            SetLineRendererPositions(
                m_runtimeClickRing,
                Array.Empty<Vector3>(),
                snapshot.FailureColor,
                snapshot.LineWidth);
            SetLineRendererPositions(
                m_runtimeResolvedCellRing,
                Array.Empty<Vector3>(),
                snapshot.ResolvedColor,
                snapshot.LineWidth);
            SetLineRendererPositions(
                m_runtimeClickToResolvedLine,
                Array.Empty<Vector3>(),
                snapshot.ResolvedColor,
                snapshot.LineWidth);
            SetLineRendererPositions(
                m_runtimeResolvedToEndLine,
                Array.Empty<Vector3>(),
                snapshot.PathColor,
                snapshot.LineWidth);
            ClearRuntimeLabel(m_runtimeStartLabel);
            ClearRuntimeLabel(m_runtimeClickLabel);
            ClearRuntimeLabel(m_runtimeFinalDestinationLabel);
            ClearRuntimeLabel(m_runtimeResolvedCellLabel);
            ClearRuntimeLabel(m_runtimeStatusLabel);
            ClearRuntimeWaypointRings(snapshot);
        }

        private static void SetLineRendererPositions(
            LineRenderer lineRenderer,
            IReadOnlyList<Vector3> positions,
            Color color,
            float width)
        {
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            lineRenderer.positionCount = positions?.Count ?? 0;
            for (int i = 0; positions != null && i < positions.Count; i++)
            {
                lineRenderer.SetPosition(i, positions[i]);
            }
        }

        private static Vector3 ToDebugPosition(Vector2 position, float z)
        {
            return new Vector3(position.x, position.y, z);
        }
    }
}
