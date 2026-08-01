using UnityEditor;
using UnityEngine;

namespace ContextSteering2D.Editor
{
    [CustomEditor(typeof(ContextSteeringDebugProbe2D))]
    public sealed class ContextSteeringDebugProbe2DEditor : UnityEditor.Editor
    {
        private SerializedProperty m_contributionFilter;

        private void OnEnable()
        {
            m_contributionFilter = serializedObject.FindProperty("m_contributionFilter");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            ContextSteeringDebugProbe2D probe = (ContextSteeringDebugProbe2D)target;
            SteeringDebugSnapshot2D snapshot = probe.Snapshot;
            DrawContributionFilter(snapshot);
            serializedObject.ApplyModifiedProperties();

            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("运行后由世界级 ContextSteeringSimulation2D 发布调试快照。", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulation Snapshot", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Profile", snapshot.ProfileName);
            EditorGUILayout.TextField("Behaviour Group", snapshot.BehaviourGroupId);
            EditorGUILayout.Vector2Field("Preferred Velocity", snapshot.Result.PreferredVelocity);
            EditorGUILayout.Vector2Field("Safe Velocity", snapshot.Result.SafeVelocity);
            EditorGUILayout.Vector2Field("Push Correction", snapshot.Result.PushCorrection);
            EditorGUILayout.Vector2Field("Final Velocity", snapshot.Result.FinalVelocity);
            EditorGUILayout.FloatField("Speed Scale", snapshot.Result.SpeedScale);
            EditorGUILayout.IntField("Obstacles", snapshot.Obstacles.Length);
            EditorGUILayout.IntField("Neighbours", snapshot.Neighbours.Length);
            EditorGUILayout.IntField("Semantic Colliders", snapshot.DetectedColliderCount);
            EditorGUILayout.IntField("Contributions", snapshot.Contributions.Length);
        }

        private void DrawContributionFilter(SteeringDebugSnapshot2D snapshot)
        {
            if (snapshot == null || snapshot.Contributions.Length == 0)
            {
                return;
            }

            string[] labels = new string[snapshot.Contributions.Length + 1];
            labels[0] = "All Behaviours";
            int selectedIndex = 0;
            for (int i = 0; i < snapshot.Contributions.Length; i++)
            {
                SteeringContributionSnapshot2D contribution = snapshot.Contributions[i];
                labels[i + 1] = contribution.DisplayName;
                if (contribution.StableId == m_contributionFilter.stringValue)
                {
                    selectedIndex = i + 1;
                }
            }

            int nextIndex = EditorGUILayout.Popup("Contribution Layer", selectedIndex, labels);
            m_contributionFilter.stringValue = nextIndex == 0
                ? string.Empty
                : snapshot.Contributions[nextIndex - 1].StableId;
        }
    }
}
