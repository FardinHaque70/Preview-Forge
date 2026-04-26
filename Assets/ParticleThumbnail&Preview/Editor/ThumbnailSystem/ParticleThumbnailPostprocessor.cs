using UnityEditor;

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ParticleThumbnailPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            Invalidate(importedAssets);
            Invalidate(movedAssets);
            Invalidate(movedFromAssetPaths);
            Invalidate(deletedAssets);

            if ((deletedAssets?.Length ?? 0) > 0)
                ParticleThumbnailPersistentCache.PruneMissingAssets();

            EditorApplication.RepaintProjectWindow();
        }

        private static void Invalidate(string[] paths)
        {
            if (paths == null)
                return;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path))
                    continue;

                ParticleThumbnailService.InvalidateSupportCacheForPath(path);

                if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                ParticleThumbnailService.InvalidatePath(path, repaintProjectWindow: false);
            }
        }
    }
}
