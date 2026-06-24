using UnityEditor;
// Listens for prefab import changes and invalidates shared prefab-thumbnail cache entries.

namespace NoodleHammer.PreviewForge.Editor
{
    internal sealed class PrefabThumbnailPostprocessor : AssetPostprocessor
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
                PrefabThumbnailPersistentCache.PruneMissingAssets();
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

                PrefabThumbnailService.InvalidateSupportCacheForPath(path);
                PrefabThumbnailService.InvalidatePath(path, repaintProjectWindow: false);
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
