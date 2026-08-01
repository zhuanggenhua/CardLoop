using System;
using UnityEditor;
using UnityEngine;

namespace ContextSteering2D.Editor
{
    [CustomEditor(typeof(ContextSteeringProfile2D))]
    public sealed class ContextSteeringProfile2DEditor : UnityEditor.Editor
    {
        private SerializedProperty m_sampleCount;
        private SerializedProperty m_combineMode;
        private SerializedProperty m_selectionMode;
        private SerializedProperty m_agentRadius;
        private SerializedProperty m_maxSpeed;
        private SerializedProperty m_mass;
        private SerializedProperty m_avoidancePriority;
        private SerializedProperty m_obstacleProbeRadius;
        private SerializedProperty m_neighbourRadius;
        private SerializedProperty m_timeHorizon;
        private SerializedProperty m_maxNeighbours;
        private SerializedProperty m_contactStiffness;
        private SerializedProperty m_maxContactCorrection;
        private SerializedProperty m_defaultGroupId;
        private SerializedProperty m_behaviourGroups;
        private SerializedProperty m_drawDebug;

        private void OnEnable()
        {
            m_sampleCount = Find("m_sampleCount");
            m_combineMode = Find("m_combineMode");
            m_selectionMode = Find("m_selectionMode");
            m_agentRadius = Find("m_agentRadius");
            m_maxSpeed = Find("m_maxSpeed");
            m_mass = Find("m_mass");
            m_avoidancePriority = Find("m_avoidancePriority");
            m_obstacleProbeRadius = Find("m_obstacleProbeRadius");
            m_neighbourRadius = Find("m_neighbourRadius");
            m_timeHorizon = Find("m_timeHorizon");
            m_maxNeighbours = Find("m_maxNeighbours");
            m_contactStiffness = Find("m_contactStiffness");
            m_maxContactCorrection = Find("m_maxContactCorrection");
            m_defaultGroupId = Find("m_defaultGroupId");
            m_behaviourGroups = Find("m_behaviourGroups");
            m_drawDebug = Find("m_drawDebug");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawSection("Sampling", m_sampleCount, m_combineMode, m_selectionMode);
            DrawSection("Body", m_agentRadius, m_maxSpeed, m_mass, m_avoidancePriority);
            DrawSection("Detection", m_obstacleProbeRadius, m_neighbourRadius);
            DrawSection("Local Avoidance Participation", m_timeHorizon, m_maxNeighbours, m_contactStiffness, m_maxContactCorrection);

            EditorGUILayout.LabelField("Behaviour Groups", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_defaultGroupId);
            DrawGroups();
            if (GUILayout.Button("Add Behaviour Group")) AddGroup();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(m_drawDebug);
            serializedObject.ApplyModifiedProperties();

            try
            {
                ((ContextSteeringProfile2D)target).ValidateOrThrow();
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        public override bool HasPreviewGUI() => true;

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint) return;
            background.Draw(rect, false, false, false, false);

            SteeringDebugSnapshot2D snapshot;
            try
            {
                snapshot = ContextSteeringPreview2D.Evaluate((ContextSteeringProfile2D)target);
            }
            catch (Exception exception)
            {
                EditorGUI.HelpBox(rect, exception.Message, MessageType.Error);
                return;
            }

            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.33f;
            Handles.BeginGUI();
            for (int i = 0; i < snapshot.Directions.Length; i++)
            {
                Vector2 direction = snapshot.Directions[i];
                Handles.color = new Color(1.0f, 1.0f, 1.0f, 0.2f);
                Handles.DrawLine(center, center + direction * radius);

                for (int contributionIndex = 0; contributionIndex < snapshot.Contributions.Length; contributionIndex++)
                {
                    SteeringContributionSnapshot2D contribution = snapshot.Contributions[contributionIndex];
                    float interest = contribution.Interest[i];
                    float constraint = contribution.Constraint[i];
                    if (interest > 0.0f)
                    {
                        Handles.color = new Color(0.1f, 0.9f, 0.25f, 0.8f);
                        Handles.DrawLine(center + direction * radius, center + direction * (radius + interest * 18.0f));
                    }
                    if (constraint > 0.0f)
                    {
                        Handles.color = new Color(1.0f, 0.2f, 0.1f, 0.8f);
                        Handles.DrawLine(center + direction * (radius + 20.0f), center + direction * (radius + 20.0f + constraint * 18.0f));
                    }
                }
            }

            DrawPreviewVector(center, snapshot.Result.PreferredVelocity, radius + 30.0f, Color.yellow);
            DrawPreviewVector(center, snapshot.Result.FinalVelocity, radius + 42.0f, Color.cyan);
            Handles.EndGUI();
        }

        private SerializedProperty Find(string name) => serializedObject.FindProperty(name);

        private static void DrawSection(string title, params SerializedProperty[] properties)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int i = 0; i < properties.Length; i++) EditorGUILayout.PropertyField(properties[i]);
            EditorGUILayout.Space();
        }

        private void DrawGroups()
        {
            for (int groupIndex = 0; groupIndex < m_behaviourGroups.arraySize; groupIndex++)
            {
                SerializedProperty group = m_behaviourGroups.GetArrayElementAtIndex(groupIndex);
                SerializedProperty stableId = group.FindPropertyRelative("m_stableId");
                SerializedProperty displayName = group.FindPropertyRelative("m_displayName");
                SerializedProperty behaviours = group.FindPropertyRelative("m_behaviours");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(displayName.stringValue) ? $"Group {groupIndex}" : displayName.stringValue, EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(64.0f)))
                {
                    m_behaviourGroups.DeleteArrayElementAtIndex(groupIndex);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(stableId);
                EditorGUILayout.PropertyField(displayName);

                for (int behaviourIndex = 0; behaviourIndex < behaviours.arraySize; behaviourIndex++)
                {
                    SerializedProperty behaviour = behaviours.GetArrayElementAtIndex(behaviourIndex);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(behaviour, GUIContent.none, true);
                    if (GUILayout.Button("X", GUILayout.Width(24.0f)))
                    {
                        behaviours.DeleteArrayElementAtIndex(behaviourIndex);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Behaviour")) ShowAddBehaviourMenu(behaviours);
                EditorGUILayout.EndVertical();
            }
        }

        private void AddGroup()
        {
            int index = m_behaviourGroups.arraySize;
            m_behaviourGroups.InsertArrayElementAtIndex(index);
            SerializedProperty group = m_behaviourGroups.GetArrayElementAtIndex(index);
            group.FindPropertyRelative("m_stableId").stringValue = $"group-{index + 1}";
            group.FindPropertyRelative("m_displayName").stringValue = $"Group {index + 1}";
            group.FindPropertyRelative("m_behaviours").ClearArray();
        }

        private void ShowAddBehaviourMenu(SerializedProperty behaviours)
        {
            GenericMenu menu = new();
            AddMenuItem<SeekSteeringBehaviour2D>(menu, behaviours, "Seek");
            AddMenuItem<ArriveSteeringBehaviour2D>(menu, behaviours, "Arrive");
            AddMenuItem<PursuitSteeringBehaviour2D>(menu, behaviours, "Pursuit");
            AddMenuItem<ObstacleAvoidanceSteeringBehaviour2D>(menu, behaviours, "Obstacle Avoidance");
            AddMenuItem<SeparationSteeringBehaviour2D>(menu, behaviours, "Separation");
            AddMenuItem<SideStepSteeringBehaviour2D>(menu, behaviours, "Side Step");
            AddMenuItem<AlignmentSteeringBehaviour2D>(menu, behaviours, "Alignment");
            AddMenuItem<CohesionSteeringBehaviour2D>(menu, behaviours, "Cohesion");
            AddMenuItem<OrbitSteeringBehaviour2D>(menu, behaviours, "Orbit");
            AddMenuItem<CombatWanderSteeringBehaviour2D>(menu, behaviours, "Combat Wander");
            menu.ShowAsContext();
        }

        private void AddMenuItem<T>(GenericMenu menu, SerializedProperty behaviours, string label) where T : SteeringBehaviour2D, new()
        {
            string path = behaviours.propertyPath;
            menu.AddItem(new GUIContent(label), false, () =>
            {
                serializedObject.Update();
                SerializedProperty targetList = serializedObject.FindProperty(path);
                int index = targetList.arraySize;
                targetList.InsertArrayElementAtIndex(index);
                targetList.GetArrayElementAtIndex(index).managedReferenceValue = new T();
                serializedObject.ApplyModifiedProperties();
            });
        }

        private static void DrawPreviewVector(Vector2 center, Vector2 value, float length, Color color)
        {
            if (value.sqrMagnitude <= 0.0001f) return;
            Handles.color = color;
            Handles.DrawAAPolyLine(4.0f, center, center + value.normalized * length);
        }
    }
}
