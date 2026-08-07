using UnityEditor;
using UnityEngine;

namespace GameCore
{
    [CustomEditor(typeof(TerrainNavigationMap))]
    public sealed class TerrainNavigationMapEditor : UnityEditor.Editor
    {
        private SerializedProperty m_showPreviewProperty;
        private SerializedProperty m_previewStartProperty;
        private SerializedProperty m_previewDestinationProperty;

        private void OnEnable()
        {
            m_showPreviewProperty = serializedObject.FindProperty("m_showEditorNavigationPreview");
            m_previewStartProperty = serializedObject.FindProperty("m_editorPreviewStart");
            m_previewDestinationProperty = serializedObject.FindProperty("m_editorPreviewDestination");
            EditorApplication.delayCall += RebuildPreview;
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RebuildPreview;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (m_showPreviewProperty == null || !m_showPreviewProperty.boolValue)
            {
                return;
            }

            EditorGUILayout.Space();
            GUIContent refreshContent = EditorGUIUtility.IconContent("Refresh");
            refreshContent.text = " 重新计算编辑器路径";
            refreshContent.tooltip = "重新读取规则 Tilemap，并按当前预览起点和点击点计算路径。";
            if (GUILayout.Button(refreshContent))
            {
                RebuildPreview();
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            TerrainNavigationMap navigationMap = (TerrainNavigationMap)target;
            if (!navigationMap.ShowEditorNavigationPreview || Application.isPlaying)
            {
                return;
            }

            serializedObject.Update();
            Vector3 start = ToHandlePosition(m_previewStartProperty.vector2Value);
            Vector3 destination = ToHandlePosition(m_previewDestinationProperty.vector2Value);

            EditorGUI.BeginChangeCheck();
            start = Handles.PositionHandle(start, Quaternion.identity);
            destination = Handles.PositionHandle(destination, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                m_previewStartProperty.vector2Value = start;
                m_previewDestinationProperty.vector2Value = destination;
                serializedObject.ApplyModifiedProperties();
                RebuildPreview();
            }

            Handles.color = Color.white;
            Handles.Label(start + Vector3.up * 0.35f, "路径起点");
            Handles.color = new Color(1.0f, 0.25f, 0.2f, 1.0f);
            Handles.Label(destination + Vector3.down * 0.35f, "点击点");
        }

        private void RebuildPreview()
        {
            if (target is not TerrainNavigationMap navigationMap ||
                navigationMap == null ||
                Application.isPlaying ||
                !navigationMap.ShowEditorNavigationPreview)
            {
                return;
            }

            navigationMap.RefreshNavigationData();
            navigationMap.TryBuildWorldPath(
                navigationMap.EditorPreviewStart,
                navigationMap.EditorPreviewDestination,
                out _);
            SceneView.RepaintAll();
        }

        private static Vector3 ToHandlePosition(Vector2 position)
        {
            return new Vector3(position.x, position.y, 0.0f);
        }
    }
}
