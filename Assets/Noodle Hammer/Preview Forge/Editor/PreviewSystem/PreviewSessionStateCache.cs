using System;
using System.Collections.Generic;
// Caches lightweight per-asset preview state so short editor refreshes can restore interaction context without full reinitialization.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PreviewSessionStateCache
    {
        internal static bool TryRestore<TSnapshot>(
            Dictionary<string, TSnapshot> cache,
            string assetPath,
            double now,
            double windowSeconds,
            Func<TSnapshot, double> getSavedAt,
            out TSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (!cache.TryGetValue(assetPath, out snapshot))
                return false;

            if (now - getSavedAt(snapshot) > windowSeconds)
            {
                cache.Remove(assetPath);
                return false;
            }

            return true;
        }

        internal static void SaveAndTrim<TSnapshot>(
            Dictionary<string, TSnapshot> cache,
            string assetPath,
            TSnapshot snapshot,
            int maxEntries,
            Func<TSnapshot, double> getSavedAt)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            cache[assetPath] = snapshot;
            if (cache.Count <= maxEntries)
                return;

            string oldestKey = null;
            double oldestTime = double.MaxValue;
            foreach (KeyValuePair<string, TSnapshot> pair in cache)
            {
                double savedAt = getSavedAt(pair.Value);
                if (savedAt >= oldestTime)
                    continue;

                oldestTime = savedAt;
                oldestKey = pair.Key;
            }

            if (!string.IsNullOrEmpty(oldestKey))
                cache.Remove(oldestKey);
        }
    }
}
