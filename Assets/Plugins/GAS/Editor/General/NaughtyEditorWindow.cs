#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GAS.Editor
{
    /// <summary>
    /// EX-GAS 编辑器窗口的 NaughtyAttributes 承载基类；替代已移除的项目侧旧窗口基类。
    /// </summary>
    public class NaughtyEditorWindow : EditorWindow
    {
        protected virtual object GetTarget()
        {
            return this;
        }

        protected virtual void OnGUI()
        {
            DrawDefaultTargetInspector();
        }

        protected void OnImGUI()
        {
            DrawDefaultTargetInspector();
        }

        private void DrawDefaultTargetInspector()
        {
            object target = GetTarget();
            if (target is not Object unityObject)
            {
                return;
            }

            global::UnityEditor.Editor editor = global::UnityEditor.Editor.CreateEditor(unityObject);
            try
            {
                editor.OnInspectorGUI();
            }
            finally
            {
                if (editor != null)
                {
                    DestroyImmediate(editor);
                }
            }
        }
    }
}
#endif
