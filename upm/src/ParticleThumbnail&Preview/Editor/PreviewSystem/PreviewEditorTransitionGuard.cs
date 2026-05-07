using UnityEditor;
// Centralizes detection of unsafe editor transitions so preview hooks can fail open while inspector targets are rebuilding.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewEditorTransitionGuard
    {
        internal static bool IsUnsafeTransition()
        {
            return ComputeIsUnsafeTransitionForTests(
                EditorApplication.isCompiling,
                EditorApplication.isUpdating,
                EditorApplication.isPlayingOrWillChangePlaymode);
        }

        internal static bool ComputeIsUnsafeTransitionForTests(
            bool isCompiling,
            bool isUpdating,
            bool isPlayingOrWillChangePlaymode)
        {
            return isCompiling || isUpdating || isPlayingOrWillChangePlaymode;
        }
    }
}
