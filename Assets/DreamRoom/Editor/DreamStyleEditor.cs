using UnityEditor;

namespace DreamStudio.Editor
{
    /// <summary>Sets the illustration palette in the editor too, so scenes look right outside Play mode.</summary>
    [InitializeOnLoad]
    static class DreamStyleEditor
    {
        static DreamStyleEditor()
        {
            DreamStyle.Apply();
            EditorApplication.delayCall += DreamStyle.Apply;
        }
    }
}
