using System;
using System.IO;
using UnityEditor;
using UnityEngine;
// Persists thumbnail textures and metadata to disk, restores cache entries, and handles best-effort cache cleanup.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class ParticleThumbnailPersistentCache
    {
        private const int CacheFormatVersion = 2;
        private const string CacheFolderName = "ParticleThumbnailCache";

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
                    TryDeleteFile(path);
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
                    TryDeleteFile(path);
                    return false;
                }

                return true;
            }
            catch
            {
                TryDeleteFile(path);
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

                WriteAllBytesAtomic(path, bytes);
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
                    TryDeleteFile(files[i]);
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

                    TryDeleteFile(files[i]);
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
            }
            catch (Exception)
            {
            }
        }

        internal static void GetCachedDiskStats(out int fileCount, out long totalBytes)
        {
            fileCount = 0;
            totalBytes = 0L;

            string directory = GetCacheDirectory();
            if (!Directory.Exists(directory))
                return;

            try
            {
                string[] files = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly);
                fileCount = files.Length;
                for (int i = 0; i < files.Length; i++)
                {
                    FileInfo fileInfo = new FileInfo(files[i]);
                    totalBytes += fileInfo.Length;
                }
            }
            catch (Exception)
            {
                fileCount = 0;
                totalBytes = 0L;
            }
        }

        internal static string BuildCacheKey(ParticleThumbnailRequest request, string dependencyToken, string settingsToken)
        {
            return $"{request.Guid}_{(int)request.Surface}_{dependencyToken}_{settingsToken}_v{CacheFormatVersion}";
        }

        private static string GetCacheFilePath(ParticleThumbnailRequest request, string dependencyToken)
        {
            string settingsToken = ParticleThumbnailSettings.GetPersistentSettingsToken();
            string fileStem = BuildCacheKey(request, dependencyToken, settingsToken);
            return Path.Combine(GetCacheDirectory(), fileStem + ".png");
        }

        private static string GetCacheDirectory()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot ?? string.Empty, "Library", CacheFolderName);
        }

        private static void WriteAllBytesAtomic(string path, byte[] bytes)
        {
            string tempPath = path + ".tmp";
            TryDeleteFile(tempPath);

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
                    TryDeleteFile(path);

                File.Move(tempPath, path);
            }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best effort cleanup.
            }
        }
    }
}
