using System;
using System.IO;
using UnityEditor;
using UnityEngine;
// Persists thumbnail textures and metadata to disk, restores cache entries, and handles best-effort cache cleanup.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class ParticleThumbnailPersistentCache
    {
        private const int CacheFormatVersion = 2;
        private const string CacheFolderName = "ParticleThumbnailCache";
        private static bool s_diskStatsInitialized;
        private static int s_cachedDiskFileCount;
        private static long s_cachedDiskBytes;
        private static string s_cacheDirectoryOverrideForTests;

        public static bool TryLoadTexture(ParticleThumbnailRequest request, string dependencyToken, out Texture2D texture)
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
                    name = $"ParticleThumb_{request.Guid}_{request.Surface}"
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

        public static void SaveTexture(ParticleThumbnailRequest request, string dependencyToken, Texture2D texture)
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
                long previousLength = 0L;
                if (hadExistingFile)
                    previousLength = new FileInfo(path).Length;

                WriteAllBytesAtomic(path, bytes);
                UpdateCachedDiskStatsForWrite(hadExistingFile, previousLength, bytes.LongLength);
            }
            catch (Exception)
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
            catch (Exception)
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
            catch (Exception)
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
            catch (Exception)
            {
            }
        }

        internal static void GetCachedDiskStats(out int fileCount, out long totalBytes)
        {
            EnsureCachedDiskStatsInitialized();
            fileCount = s_cachedDiskFileCount;
            totalBytes = s_cachedDiskBytes;
        }

        internal static string BuildCacheKey(ParticleThumbnailRequest request, string dependencyToken, string settingsToken)
        {
            return $"{request.Guid}_{(int)request.Surface}_{dependencyToken}_{settingsToken}_v{CacheFormatVersion}";
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
        }

        private static string GetCacheFilePath(ParticleThumbnailRequest request, string dependencyToken)
        {
            string settingsToken = ParticleThumbnailSettings.GetPersistentSettingsToken();
            string fileStem = BuildCacheKey(request, dependencyToken, settingsToken);
            return Path.Combine(GetCacheDirectory(), fileStem + ".png");
        }

        private static string GetCacheDirectory()
        {
            if (!string.IsNullOrEmpty(s_cacheDirectoryOverrideForTests))
                return s_cacheDirectoryOverrideForTests;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, "Library", CacheFolderName);
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
            catch
            {
                if (File.Exists(path))
                    TryDeleteFile(path, updateCachedStats: false);

                File.Move(tempPath, path);
            }
        }

        private static void EnsureCachedDiskStatsInitialized()
        {
            if (s_diskStatsInitialized)
                return;

            s_cachedDiskFileCount = 0;
            s_cachedDiskBytes = 0L;

            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
            {
                s_diskStatsInitialized = true;
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
                s_cachedDiskFileCount = files.Length;
                for (int i = 0; i < files.Length; i++)
                {
                    FileInfo fileInfo = new FileInfo(files[i]);
                    s_cachedDiskBytes += fileInfo.Length;
                }
            }
            catch (Exception)
            {
                s_cachedDiskFileCount = 0;
                s_cachedDiskBytes = 0L;
            }

            s_diskStatsInitialized = true;
        }

        private static void UpdateCachedDiskStatsForWrite(bool hadExistingFile, long previousLength, long newLength)
        {
            if (!s_diskStatsInitialized)
                return;

            if (!hadExistingFile)
            {
                s_cachedDiskFileCount++;
                s_cachedDiskBytes += newLength;
                return;
            }

            s_cachedDiskBytes += newLength - previousLength;
        }

        private static void ResetCachedDiskStats()
        {
            if (!s_diskStatsInitialized)
                return;

            s_cachedDiskFileCount = 0;
            s_cachedDiskBytes = 0L;
        }

        private static void TryDeleteFile(string path, bool updateCachedStats)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                long fileLength = 0L;
                if (updateCachedStats && s_diskStatsInitialized)
                    fileLength = new FileInfo(path).Length;

                File.Delete(path);
                if (updateCachedStats && s_diskStatsInitialized)
                {
                    s_cachedDiskFileCount = Math.Max(0, s_cachedDiskFileCount - 1);
                    s_cachedDiskBytes = Math.Max(0L, s_cachedDiskBytes - fileLength);
                }
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
