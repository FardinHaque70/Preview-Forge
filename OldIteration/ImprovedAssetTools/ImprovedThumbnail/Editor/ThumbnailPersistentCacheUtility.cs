using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ThumbnailPersistentCacheUtility
{
    private const int CacheFormatVersion = 2;
    private const string CacheFolderName = "ImprovedThumbnailCache";
    private static int s_cachedFileCount = -1;
    private static long s_cachedDiskBytes = 0L;
    private static double s_diskBytesTimestamp = double.MinValue;

    public static bool TryLoadRecord(ThumbnailRequest request, long dependencyHash, out ThumbnailCacheRecord record)
    {
        record = null;
        string posterPath = GetPosterFilePath(request, dependencyHash);
        if (!File.Exists(posterPath))
            return false;

        try
        {
            Texture2D posterTexture = LoadTextureFromPng(posterPath, $"ImprovedThumbnail_{request.Guid}_Poster");
            if (posterTexture == null)
                return false;

            record = new ThumbnailCacheRecord
            {
                Guid = request.Guid,
                AssetPath = request.AssetPath,
                DependencyHash = dependencyHash,
                AssetKind = request.AssetKind,
                Frames = new ThumbnailFrameSet
                {
                    StaticFrame = posterTexture,
                },
            };
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to load cached thumbnail for '{request.AssetPath}': {e.Message}");
            return false;
        }
    }

    public static void SaveRecord(ThumbnailRequest request, ThumbnailCacheRecord record)
    {
        if (record?.Frames?.StaticFrame == null)
            return;

        try
        {
            byte[] pngBytes = record.Frames.StaticFrame.EncodeToPNG();
            if (pngBytes == null || pngBytes.Length == 0)
                return;

            string posterPath = GetPosterFilePath(request, record.DependencyHash);
            Directory.CreateDirectory(Path.GetDirectoryName(posterPath));
            bool existed = File.Exists(posterPath);
            WriteAllBytesAtomic(posterPath, pngBytes);
            if (!existed && s_cachedFileCount >= 0)
                s_cachedFileCount++;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to persist thumbnail for '{request.AssetPath}': {e.Message}");
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
            s_cachedFileCount = 0;
            s_cachedDiskBytes = 0L;
            s_diskBytesTimestamp = double.MinValue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to clear persistent thumbnail cache: {e.Message}");
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

            if (s_cachedFileCount >= 0)
                s_cachedFileCount = Mathf.Max(0, s_cachedFileCount - files.Length);
            if (files.Length > 0)
                s_diskBytesTimestamp = double.MinValue;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to invalidate persistent cache for guid '{guid}': {e.Message}");
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
            int removedCount = 0;

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
                removedCount++;
            }

            if (removedCount > 0)
            {
                s_cachedFileCount = -1;
                s_diskBytesTimestamp = double.MinValue;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to prune orphaned thumbnail cache entries: {e.Message}");
        }
    }

    public static int GetCachedFileCount()
    {
        if (s_cachedFileCount >= 0)
            return s_cachedFileCount;

        string directory = GetCacheDirectory();
        if (!Directory.Exists(directory))
        {
            s_cachedFileCount = 0;
            return s_cachedFileCount;
        }

        try
        {
            s_cachedFileCount = Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly).Length;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to count persistent cache files: {e.Message}");
            s_cachedFileCount = 0;
        }

        return s_cachedFileCount;
    }

    public static long GetCachedDiskBytes()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - s_diskBytesTimestamp < 5.0)
            return s_cachedDiskBytes;

        s_diskBytesTimestamp = now;
        string directory = GetCacheDirectory();
        if (!Directory.Exists(directory))
        {
            s_cachedDiskBytes = 0L;
            return 0L;
        }

        long total = 0L;
        try
        {
            foreach (string f in Directory.GetFiles(directory, "*.png", SearchOption.TopDirectoryOnly))
                total += new FileInfo(f).Length;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to measure persistent cache size: {e.Message}");
        }
        s_cachedDiskBytes = total;
        return s_cachedDiskBytes;
    }

    private static Texture2D LoadTextureFromPng(string path, string textureName)
    {
        try
        {
            byte[] pngBytes = File.ReadAllBytes(path);
            if (pngBytes == null || pngBytes.Length == 0)
            {
                TryDeleteFile(path);
                return null;
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!texture.LoadImage(pngBytes, false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                TryDeleteFile(path);
                return null;
            }

            texture.name = textureName;
            return texture;
        }
        catch
        {
            TryDeleteFile(path);
            return null;
        }
    }

    private static string GetPosterFilePath(ThumbnailRequest request, long dependencyHash)
    {
        return Path.Combine(GetCacheDirectory(), $"{GetCacheFileStem(request, dependencyHash)}_poster.png");
    }

    private static string GetCacheFileStem(ThumbnailRequest request, long dependencyHash)
    {
        string settingsToken = GetSettingsDependencyToken();
        string dependencyToken = unchecked((ulong)dependencyHash).ToString("X16");
        return $"{request.Guid}_{(int)request.AssetKind}_{(int)request.Surface}_{dependencyToken}_{settingsToken}_v{CacheFormatVersion}";
    }

    private static string GetCacheDirectory()
    {
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectRoot ?? string.Empty, "Library", CacheFolderName);
    }

    private static string GetSettingsDependencyToken()
    {
        return ImprovedThumbnailSettings.GetPersistentCacheSettingsToken();
    }

    private static void WriteAllBytesAtomic(string path, byte[] bytes)
    {
        if (string.IsNullOrEmpty(path) || bytes == null)
            return;

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

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
        catch { }
    }
}

}
