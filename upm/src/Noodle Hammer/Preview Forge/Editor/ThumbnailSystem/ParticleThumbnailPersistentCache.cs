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
        private const string CurrentCacheFolderName = "PrefabThumbnailCache";
        private const string LegacyCacheFolderName = "ParticleThumbnailCache";
        private const string StudioFolderName = "Noodle Hammer";
        private const string ProductFolderName = "Preview Forge";
        private static bool s_diskStatsInitialized;
        private static int s_cachedDiskFileCount;
        private static long s_cachedDiskBytes;
        private static bool s_cacheMigrationAttempted;
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
            s_cacheMigrationAttempted = false;
        }

        internal static string BuildCurrentCacheDirectoryPathForTests(string libraryDirectory)
        {
            return BuildProductCacheDirectory(libraryDirectory, CurrentCacheFolderName);
        }

        internal static string BuildNestedLegacyCacheDirectoryPathForTests(string libraryDirectory)
        {
            return BuildProductCacheDirectory(libraryDirectory, LegacyCacheFolderName);
        }

        internal static string BuildFlatLegacyCacheDirectoryPathForTests(string libraryDirectory)
        {
            return Path.Combine(libraryDirectory, LegacyCacheFolderName);
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
            string cacheDirectory = BuildCurrentCacheDirectoryPathForTests(libraryDirectory);
            TryMigrateLegacyCacheDirectories(libraryDirectory, cacheDirectory);
            return cacheDirectory;
        }

        private static void TryMigrateLegacyCacheDirectories(string libraryDirectory, string cacheDirectory)
        {
            if (s_cacheMigrationAttempted)
                return;

            s_cacheMigrationAttempted = true;
            string[] legacyDirectories =
            {
                BuildNestedLegacyCacheDirectoryPathForTests(libraryDirectory),
                BuildFlatLegacyCacheDirectoryPathForTests(libraryDirectory),
            };

            for (int i = 0; i < legacyDirectories.Length; i++)
            {
                TryMigrateCacheDirectory(legacyDirectories[i], cacheDirectory);
            }
        }

        private static void TryMigrateCacheDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (string.IsNullOrEmpty(sourceDirectory)
                || string.IsNullOrEmpty(destinationDirectory)
                || string.Equals(sourceDirectory, destinationDirectory, StringComparison.Ordinal)
                || !Directory.Exists(sourceDirectory))
            {
                return;
            }

            try
            {
                string parentDirectory = Path.GetDirectoryName(destinationDirectory);
                if (string.IsNullOrEmpty(parentDirectory))
                    return;

                Directory.CreateDirectory(parentDirectory);
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.Move(sourceDirectory, destinationDirectory);
                    return;
                }

                string[] sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < sourceFiles.Length; i++)
                {
                    string destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourceFiles[i]));
                    if (File.Exists(destinationPath))
                        continue;

                    File.Move(sourceFiles[i], destinationPath);
                }

                if (Directory.GetFileSystemEntries(sourceDirectory).Length == 0)
                    Directory.Delete(sourceDirectory, false);
            }
            catch
            {
            }
        }

        private static string BuildProductCacheDirectory(string libraryDirectory, string cacheFolderName)
        {
            return Path.Combine(libraryDirectory, StudioFolderName, ProductFolderName, cacheFolderName);
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
