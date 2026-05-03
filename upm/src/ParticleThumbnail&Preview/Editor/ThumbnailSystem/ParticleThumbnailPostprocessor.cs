using UnityEditor;
// Listens for relevant asset import changes and invalidates thumbnail cache entries to keep project thumbnails up to date.

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
            bool changed = false;
            changed |= Invalidate(importedAssets);
            changed |= Invalidate(movedAssets);
            changed |= Invalidate(movedFromAssetPaths);
            changed |= Invalidate(deletedAssets);

            if (ContainsPrefabPath(deletedAssets))
            {
                ParticleThumbnailPersistentCache.PruneMissingAssets();
                changed = true;
            }

            if (changed)
                EditorApplication.RepaintProjectWindow();
        }

        private static bool Invalidate(string[] paths)
        {
            bool changed = false;
            if (paths == null)
                return false;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                ParticleThumbnailService.InvalidateSupportCacheForPath(path);
                ParticleThumbnailService.InvalidatePath(path, repaintProjectWindow: false);
                changed = true;
            }

            return changed;
        }

        private static bool ContainsPrefabPath(string[] paths)
        {
            if (paths == null)
                return false;

            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (!string.IsNullOrEmpty(path)
                    && path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
