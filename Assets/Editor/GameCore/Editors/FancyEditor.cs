using UnityEditor;
using UnityEngine;

namespace GameCore
{
    public abstract class FancyEditor<T> : UnityEditor.Editor where T : UnityEngine.Object
    {
        protected virtual bool m_drawScriptHeader => true;
        protected virtual bool m_drawdefaultInspector => true;

        private void DrawScriptHeader()
        {
            bool wasGUIEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            GUI.enabled = wasGUIEnabled;
        }

        private void DrawBaseInspector()
        {
            SerializedProperty property = serializedObject.GetIterator();
            property.NextVisible(true); // Move to the first property (skip "m_Script")

            // Start drawing the remaining properties
            while (property.NextVisible(false))
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (m_drawScriptHeader)
            {
                DrawScriptHeader();
            }

            DrawCustomInspector((T)target);

            if (m_drawdefaultInspector)
            {
                DrawBaseInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }

        protected abstract void DrawCustomInspector(T target);
    }
}

