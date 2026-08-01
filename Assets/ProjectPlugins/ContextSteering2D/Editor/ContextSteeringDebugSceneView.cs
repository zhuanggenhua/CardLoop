using UnityEditor;
using UnityEngine;

namespace ContextSteering2D.Editor
{
    [InitializeOnLoad]
    internal static class ContextSteeringDebugSceneView
    {
        private const float OverlayWidth = 230.0f;

        private static readonly Color[] ContributionColors =
        {
            new(0.15f, 0.9f, 0.3f, 0.95f),
            new(0.2f, 0.75f, 1.0f, 0.95f),
            new(1.0f, 0.65f, 0.1f, 0.95f),
            new(0.9f, 0.25f, 0.85f, 0.95f),
            new(0.65f, 1.0f, 0.2f, 0.95f),
        };

        static ContextSteeringDebugSceneView()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            ContextSteeringDebugProbe2D selectedProbe = ResolveSelectedProbe();
            ContextSteeringDebugProbe2D[] probes = UnityEngine.Object.FindObjectsByType<ContextSteeringDebugProbe2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < probes.Length; i++)
            {
                ContextSteeringDebugProbe2D probe = probes[i];
                if (!CanDraw(probe))
                {
                    continue;
                }

                if (probe != selectedProbe && !probe.DrawWhenNotSelected)
                {
                    continue;
                }

                DrawWorld(probe, probe.Snapshot);
            }

            if (selectedProbe != null && CanDraw(selectedProbe) && selectedProbe.DrawOverlay)
            {
                DrawOverlay(sceneView, selectedProbe, selectedProbe.Snapshot);
            }
        }

        private static ContextSteeringDebugProbe2D ResolveSelectedProbe()
        {
            GameObject selected = Selection.activeGameObject;
            return selected != null
                ? selected.GetComponentInParent<ContextSteeringDebugProbe2D>()
                : null;
        }

        private static bool CanDraw(ContextSteeringDebugProbe2D probe)
        {
            return probe != null && probe.DrawSceneView && probe.Snapshot != null;
        }

        private static void DrawWorld(ContextSteeringDebugProbe2D probe, SteeringDebugSnapshot2D snapshot)
        {
            Vector3 origin = ToScene(snapshot.Position);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = new Color(0.15f, 0.65f, 1.0f, 0.2f);
            Handles.DrawSolidDisc(origin, Vector3.forward, snapshot.AgentRadius);
            Handles.color = new Color(0.2f, 0.75f, 1.0f, 0.95f);
            Handles.DrawWireDisc(origin, Vector3.forward, snapshot.AgentRadius, 2.0f);
            DrawArrow(origin, snapshot.Forward, 0.65f, Color.white, "Forward");

            if (probe.DrawTarget && snapshot.TargetPosition.HasValue)
            {
                Vector3 target = ToScene(snapshot.TargetPosition.Value);
                Handles.color = new Color(1.0f, 0.2f, 0.85f, 0.9f);
                Handles.DrawDottedLine(origin, target, 5.0f);
                Handles.DrawWireDisc(target, Vector3.forward, 0.15f, 2.0f);
            }

            if (probe.DrawDetection)
            {
                DrawDetection(snapshot);
            }

            DrawContextChart(origin, probe, snapshot);

            float arrowBase = Mathf.Max(snapshot.AgentRadius + 0.5f, 0.85f);
            if (probe.DrawPreferredVelocity)
            {
                DrawArrow(origin, snapshot.Result.PreferredVelocity, arrowBase, new Color(1.0f, 0.65f, 0.0f), "Preferred");
            }
            if (probe.DrawSafeVelocity)
            {
                DrawArrow(origin, snapshot.Result.SafeVelocity, arrowBase + 0.3f, Color.green, "Safe");
            }
            if (probe.DrawPushCorrection)
            {
                DrawArrow(origin, snapshot.Result.PushCorrection, arrowBase * 0.75f, Color.cyan, "Push");
            }
        }

        private static void DrawDetection(SteeringDebugSnapshot2D snapshot)
        {
            for (int i = 0; i < snapshot.Obstacles.Length; i++)
            {
                Handles.color = new Color(1.0f, 0.2f, 0.1f, 0.95f);
                Handles.DrawWireDisc(ToScene(snapshot.Obstacles[i].ClosestPoint), Vector3.forward, 0.1f, 2.0f);
            }

            for (int i = 0; i < snapshot.Neighbours.Length; i++)
            {
                SteeringBody2D neighbour = snapshot.Neighbours[i];
                Handles.color = new Color(0.2f, 0.55f, 1.0f, 0.9f);
                Handles.DrawWireDisc(ToScene(neighbour.Position), Vector3.forward, neighbour.Radius, 2.0f);
            }
        }

        private static void DrawOverlay(SceneView sceneView, ContextSteeringDebugProbe2D probe, SteeringDebugSnapshot2D snapshot)
        {
            Handles.BeginGUI();
            int rowCount = VisibleLegendRowCount(probe, snapshot);
            float overlayHeight = 58.0f + rowCount * 17.0f;
            float x = Mathf.Max(12.0f, sceneView.position.width - OverlayWidth - 12.0f);
            Rect panel = new(x, 12.0f, OverlayWidth, overlayHeight);
            EditorGUI.DrawRect(panel, new Color(0.055f, 0.065f, 0.08f, 0.92f));
            GUI.Box(panel, GUIContent.none, EditorStyles.helpBox);

            Rect header = new(panel.x + 12.0f, panel.y + 8.0f, panel.width - 24.0f, 38.0f);
            GUI.Label(header, $"Context Steering  |  {probe.name}", EditorStyles.boldLabel);
            GUI.Label(new Rect(header.x, header.y + 19.0f, header.width, 18.0f),
                $"Obstacles {snapshot.Obstacles.Length}    Neighbours {snapshot.Neighbours.Length}    Speed {snapshot.Result.SafeVelocity.magnitude:F2}",
                EditorStyles.miniLabel);

            DrawLegend(new Rect(panel.x + 12.0f, panel.y + 52.0f, panel.width - 24.0f, overlayHeight - 58.0f), probe, snapshot);
            Handles.EndGUI();
        }

        private static void DrawContextChart(Vector3 origin, ContextSteeringDebugProbe2D probe, SteeringDebugSnapshot2D snapshot)
        {
            float baseRadius = Mathf.Max(snapshot.AgentRadius + 0.22f, 0.58f);
            float valueScale = 0.48f;
            Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.16f);
            Handles.DrawWireDisc(origin, Vector3.forward, baseRadius, 1.0f);
            Handles.DrawWireDisc(origin, Vector3.forward, baseRadius + valueScale, 1.0f);
            for (int i = 0; i < snapshot.Directions.Length; i++)
            {
                Vector3 direction = ToScene(snapshot.Directions[i]);
                Handles.DrawLine(origin + direction * baseRadius, origin + direction * (baseRadius + valueScale));
            }

            if (probe.DrawContributions)
            {
                for (int i = 0; i < snapshot.Contributions.Length; i++)
                {
                    SteeringContributionSnapshot2D contribution = snapshot.Contributions[i];
                    if (!string.IsNullOrEmpty(probe.ContributionFilter) && contribution.StableId != probe.ContributionFilter)
                    {
                        continue;
                    }

                    DrawMapPolyline(origin, baseRadius, valueScale, snapshot.Directions, contribution.Interest,
                        ContributionColors[i % ContributionColors.Length], 2.0f);
                }
            }

            if (probe.DrawConstraints)
            {
                DrawMapPolyline(origin, baseRadius, valueScale, snapshot.Directions, snapshot.Constraint, new Color(1.0f, 0.2f, 0.1f, 0.95f), 2.5f);
            }
            if (probe.DrawCombined)
            {
                DrawMapPolyline(origin, baseRadius, valueScale, snapshot.Directions, snapshot.Combined, Color.yellow, 4.0f);
            }
        }

        private static void DrawLegend(Rect rect, ContextSteeringDebugProbe2D probe, SteeringDebugSnapshot2D snapshot)
        {
            GUILayout.BeginArea(rect);
            if (probe.DrawContributions)
            {
                for (int i = 0; i < snapshot.Contributions.Length; i++)
                {
                    SteeringContributionSnapshot2D contribution = snapshot.Contributions[i];
                    if (!string.IsNullOrEmpty(probe.ContributionFilter) && contribution.StableId != probe.ContributionFilter)
                    {
                        continue;
                    }
                    DrawLegendRow(ContributionColors[i % ContributionColors.Length], contribution.DisplayName);
                }
            }
            if (probe.DrawConstraints) DrawLegendRow(Color.red, "Constraints");
            if (probe.DrawCombined) DrawLegendRow(Color.yellow, "Combined");
            if (probe.DrawPreferredVelocity) DrawLegendRow(new Color(1.0f, 0.65f, 0.0f), "Preferred");
            if (probe.DrawSafeVelocity) DrawLegendRow(Color.green, "Safe velocity");
            if (probe.DrawPushCorrection) DrawLegendRow(Color.cyan, "Push correction");
            GUILayout.EndArea();
        }

        private static void DrawMapPolyline(
            Vector3 origin,
            float baseRadius,
            float valueScale,
            Vector2[] directions,
            float[] values,
            Color color,
            float width)
        {
            Vector3[] points = new Vector3[directions.Length + 1];
            for (int i = 0; i < directions.Length; i++)
            {
                float value = Mathf.Clamp01(values[i]);
                points[i] = origin + ToScene(directions[i]) * (baseRadius + value * valueScale);
            }
            points[^1] = points[0];
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
        }

        private static int VisibleLegendRowCount(ContextSteeringDebugProbe2D probe, SteeringDebugSnapshot2D snapshot)
        {
            int count = 0;
            if (probe.DrawContributions)
            {
                count += string.IsNullOrEmpty(probe.ContributionFilter) ? snapshot.Contributions.Length : 1;
            }
            if (probe.DrawConstraints) count++;
            if (probe.DrawCombined) count++;
            if (probe.DrawPreferredVelocity) count++;
            if (probe.DrawSafeVelocity) count++;
            if (probe.DrawPushCorrection) count++;
            return count;
        }

        private static void DrawLegendRow(Color color, string label)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(17.0f));
            Rect swatch = GUILayoutUtility.GetRect(12.0f, 9.0f, GUILayout.Width(12.0f), GUILayout.Height(9.0f));
            EditorGUI.DrawRect(swatch, color);
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.EndHorizontal();
        }

        private static void DrawArrow(Vector3 origin, Vector2 vector, float length, Color color, string label)
        {
            if (vector.sqrMagnitude <= 0.0001f) return;
            Vector2 direction = vector.normalized;
            Vector3 end = origin + ToScene(direction * length);
            Handles.color = color;
            Handles.DrawAAPolyLine(4.0f, origin, end);
            Handles.ConeHandleCap(0, end, Quaternion.LookRotation(Vector3.forward, direction), 0.1f, EventType.Repaint);
            Handles.Label(end + ToScene(direction * 0.1f), label, EditorStyles.miniBoldLabel);
        }

        private static Vector3 ToScene(Vector2 value) => new(value.x, value.y, 0.0f);
    }
}
