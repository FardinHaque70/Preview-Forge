using System;
using System.IO;
using UnityEditor;
using UnityEngine;
// Persists prefab thumbnail textures and metadata to disk, restores cache entries, and handles best-effort cleanup.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PrefabThumbnailPersistentCache
    {
        private const int CacheFormatVersion = 3;
        private const string LegacyCacheFolderName = "ParticleThumbnailCache";
        private const string StudioFolderName = "Noodle Hammer";
        private const string ProductFolderName = "Preview Forge";
        private static bool s_diskStatsInitialized;
        private static int s_cachedDiskFileCount;
        private static long s_cachedDiskBytes;
        private static bool s_legacyCacheMigrationAttempted;
        private static string s_cacheDirectoryOverrideForTests;

        public static bool TryLoadTexture(PrefabThumbnailRequest request, string dependencyToken, out Texture2D texture)
        {
            texture = null;
            string path = GetCacheFilePath(request, dependencyToken);
            if (!File.Exists(path))
                return false;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length == 0)
                {
                    TryDeleteFile(path, updateCachedStats: true);
                    return false;
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    name = $"PrefabThumb_{request.Guid}_{request.AssetKind}_{request.Surface}"
                };

                if (!texture.LoadImage(bytes, false))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    texture = null;
                    TryDeleteFile(path, updateCachedStats: true);
                    return false;
                }

                return true;
            }
            catch
            {
                TryDeleteFile(path, updateCachedStats: true);
                return false;
            }
        }

        public static void SaveTexture(PrefabThumbnailRequest request, string dependencyToken, Texture2D texture)
        {
            if (texture == null)
                return;

            try
            {
                byte[] bytes = texture.EncodeToPNG();
                if (bytes == null || bytes.Length == 0)
                    return;

                string path = GetCacheFilePath(request, dependencyToken);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                bool hadExistingFile = File.Exists(path);
                long previousLength = hadExistingFile ? new FileInfo(path).Length : 0L;
                WriteAllBytesAtomic(path, bytes);
                UpdateCachedDiskStatsForWrite(hadExistingFile, previousLength, bytes.LongLength);
            }
            catch
            {
            }
        }

        public static void InvalidateGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return;

            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
                return;

            try
            {
                string[] files = Directory.GetFiles(directory, $"{guid}_*.png", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                    TryDeleteFile(files[i], updateCachedStats: true);
            }
            catch
            {
            }
        }

        public static void PruneMissingAssets()
        {
            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
                return;

            try
            {
                string[] files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < files.Length; i++)
                {
                    string fileName = Path.GetFileNameWithoutExtension(files[i]);
                    int separatorIndex = fileName.IndexOf('_');
                    if (separatorIndex <= 0)
                        continue;

                    string guid = fileName.Substring(0, separatorIndex);
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                        continue;

                    TryDeleteFile(files[i], updateCachedStats: true);
                }
            }
            catch
            {
            }
        }

        public static void ClearAll()
        {
            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
                return;

            try
            {
                Directory.Delete(directory, true);
                ResetCachedDiskStats();
            }
            catch
            {
            }
        }

        internal static void GetCachedDiskStats(out int fileCount, out long totalBytes)
        {
            EnsureCachedDiskStatsInitialized();
            fileCount = s_cachedDiskFileCount;
            totalBytes = s_cachedDiskBytes;
        }

        internal static string BuildCacheKey(PrefabThumbnailRequest request, string dependencyToken, string settingsToken)
        {
            return $"{request.Guid}_{(int)request.AssetKind}_{(int)request.Surface}_{dependencyToken}_{settingsToken}_v{CacheFormatVersion}";
        }

        internal static string GetCacheDirectoryPathForTests()
        {
            return GetCacheDirectory();
        }

        internal static void SetCacheDirectoryOverrideForTests(string directory)
        {
            s_cacheDirectoryOverrideForTests = directory;
            ResetCachedDiskStatsForTests();
        }

        internal static void ResetCachedDiskStatsForTests()
        {
            s_diskStatsInitialized = false;
            s_cachedDiskFileCount = 0;
            s_cachedDiskBytes = 0L;
            s_legacyCacheMigrationAttempted = false;
        }

        private static string GetCacheFilePath(PrefabThumbnailRequest request, string dependencyToken)
        {
            string settingsToken = PrefabThumbnailSettings.GetPersistentSettingsToken();
            string fileStem = BuildCacheKey(request, dependencyToken, settingsToken);
            return Path.Combine(GetCacheDirectory(), fileStem + ".png");
        }

        private static string GetCacheDirectory()
        {
            if (!string.IsNullOrEmpty(s_cacheDirectoryOverrideForTests))
                return s_cacheDirectoryOverrideForTests;

            string projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            string libraryDirectory = Path.Combine(projectRoot, "Library");
            string cacheDirectory = Path.Combine(libraryDirectory, StudioFolderName, ProductFolderName, LegacyCacheFolderName);
            TryMigrateLegacyCacheDirectory(libraryDirectory, cacheDirectory);
            return cacheDirectory;
        }

        private static void TryMigrateLegacyCacheDirectory(string libraryDirectory, string cacheDirectory)
        {
            if (s_legacyCacheMigrationAttempted)
                return;

            s_legacyCacheMigrationAttempted = true;
            string legacyCacheDirectory = Path.Combine(libraryDirectory, LegacyCacheFolderName);
            if (!Directory.Exists(legacyCacheDirectory) || string.Equals(legacyCacheDirectory, cacheDirectory, StringComparison.Ordinal))
                return;

            try
            {
                string parentDirectory = Path.GetDirectoryName(cacheDirectory);
                if (string.IsNullOrEmpty(parentDirectory))
                    return;

                Directory.CreateDirectory(parentDirectory);
                if (!Directory.Exists(cacheDirectory))
                {
                    Directory.Move(legacyCacheDirectory, cacheDirectory);
                    return;
                }

                string[] legacyFiles = Directory.GetFiles(legacyCacheDirectory, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < legacyFiles.Length; i++)
                {
                    string destinationPath = Path.Combine(cacheDirectory, Path.GetFileName(legacyFiles[i]));
                    if (File.Exists(destinationPath))
                        continue;

                    File.Move(legacyFiles[i], destinationPath);
                }

                if (Directory.GetFileSystemEntries(legacyCacheDirectory).Length == 0)
                    Directory.Delete(legacyCacheDirectory, false);
            }
            catch
            {
            }
        }

        private static void WriteAllBytesAtomic(string path, byte[] bytes)
        {
            string tempPath = path + ".tmp";
            TryDeleteFile(tempPath, updateCachedStats: false);

            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }

            try
            {
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            finally
            {
                TryDeleteFile(tempPath, updateCachedStats: false);
            }
        }

        private static void EnsureCachedDiskStatsInitialized()
        {
            if (s_diskStatsInitialized)
                return;

            s_diskStatsInitialized = true;
            s_cachedDiskFileCount = 0;
            s_cachedDiskBytes = 0L;

            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
                return;

            try
            {
                string[] files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
                s_cachedDiskFileCount = files.Length;
                for (int i = 0; i < files.Length; i++)
                    s_cachedDiskBytes += new FileInfo(files[i]).Length;
            }
            catch
            {
                s_cachedDiskFileCount = 0;
                s_cachedDiskBytes = 0L;
            }
        }

        private static void UpdateCachedDiskStatsForWrite(bool hadExistingFile, long previousLength, long newLength)
        {
            EnsureCachedDiskStatsInitialized();
            if (!hadExistingFile)
                s_cachedDiskFileCount++;

            s_cachedDiskBytes = Math.Max(0L, s_cachedDiskBytes - previousLength + newLength);
        }

        private static void TryDeleteFile(string path, bool updateCachedStats)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                long length = updateCachedStats ? new FileInfo(path).Length : 0L;
                File.Delete(path);
                if (updateCachedStats)
                {
                    EnsureCachedDiskStatsInitialized();
                    s_cachedDiskFileCount = Math.Max(0, s_cachedDiskFileCount - 1);
                    s_cachedDiskBytes = Math.Max(0L, s_cachedDiskBytes - length);
                }
            }
            catch
            {
            }
        }

        private static void ResetCachedDiskStats()
        {
            s_diskStatsInitialized = true;
            s_cachedDiskFileCount = 0;
            s_cachedDiskBytes = 0L;
        }
    }

    internal static class ParticleThumbnailPersistentCache
    {
        public static bool TryLoadTexture(PrefabThumbnailRequest request, string dependencyToken, out Texture2D texture)
            => PrefabThumbnailPersistentCache.TryLoadTexture(request, dependencyToken, out texture);

        public static void SaveTexture(PrefabThumbnailRequest request, string dependencyToken, Texture2D texture)
            => PrefabThumbnailPersistentCache.SaveTexture(request, dependencyToken, texture);

        public static void InvalidateGuid(string guid)
            => PrefabThumbnailPersistentCache.InvalidateGuid(guid);

        public static void PruneMissingAssets()
            => PrefabThumbnailPersistentCache.PruneMissingAssets();

        public static void ClearAll()
            => PrefabThumbnailPersistentCache.ClearAll();

        internal static void GetCachedDiskStats(out int fileCount, out long totalBytes)
            => PrefabThumbnailPersistentCache.GetCachedDiskStats(out fileCount, out totalBytes);

        internal static string BuildCacheKey(PrefabThumbnailRequest request, string dependencyToken, string settingsToken)
            => PrefabThumbnailPersistentCache.BuildCacheKey(request, dependencyToken, settingsToken);
    }
}
