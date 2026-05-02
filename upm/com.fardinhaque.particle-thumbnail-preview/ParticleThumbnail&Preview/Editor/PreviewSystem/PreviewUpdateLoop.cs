using UnityEditor;

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewUpdateLoop
    {
        internal static void EnsureRegistered(ref bool isRegistered, EditorApplication.CallbackFunction updateAction)
        {
            if (isRegistered)
                return;

            EditorApplication.update += updateAction;
            isRegistered = true;
        }

        internal static void EnsureUnregistered(ref bool isRegistered, EditorApplication.CallbackFunction updateAction)
        {
            if (!isRegistered)
                return;

            EditorApplication.update -= updateAction;
            isRegistered = false;
        }
    }
}
