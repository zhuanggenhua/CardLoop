#if UNITY_EDITOR
namespace GAS.Editor
{
    public class NaughtyEditorWithoutHeader: NaughtyAttributes.Editor.NaughtyInspector
    {
        protected override void OnHeaderGUI()
        {
        }
    }
}
#endif
