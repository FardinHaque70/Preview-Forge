#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ScriptRelativeAssetUtility
{
    private const string ImprovedAssetToolsProjectSettingsFolder = "ProjectSettings/ImprovedAssetTools";
    private static readonly Dictionary<string, string> s_scriptDirectories = new();
    private static readonly Dictionary<string, bool> s_scriptExistence = new();

    public static string GetScriptDirectory(string scriptFileName, string fallbackDirectory)
    {
        if (string.IsNullOrEmpty(scriptFileName))
            return NormalizeAssetPath(fallbackDirectory);

        if (s_scriptDirectories.TryGetValue(scriptFileName, out string cachedDirectory))
            return cachedDirectory;

        string searchTerm = Path.GetFileNameWithoutExtension(scriptFileName);
        string[] guids = AssetDatabase.FindAssets($"{searchTerm} t:Script");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.Equals(Path.GetFileName(path), scriptFileName, System.StringComparison.Ordinal))
                continue;

            string directory = NormalizeAssetPath(Path.GetDirectoryName(path));
            s_scriptDirectories[scriptFileName] = directory;
            return directory;
        }

        string fallback = NormalizeAssetPath(fallbackDirectory);
        s_scriptDirectories[scriptFileName] = fallback;
        return fallback;
    }

    public static bool ScriptExists(string scriptFileName)
    {
        if (string.IsNullOrEmpty(scriptFileName))
            return false;

        if (s_scriptExistence.TryGetValue(scriptFileName, out bool exists))
            return exists;

        string searchTerm = Path.GetFileNameWithoutExtension(scriptFileName);
        string[] guids = AssetDatabase.FindAssets($"{searchTerm} t:Script");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.Equals(Path.GetFileName(path), scriptFileName, System.StringComparison.Ordinal))
                continue;

            s_scriptExistence[scriptFileName] = true;
            return true;
        }

        s_scriptExistence[scriptFileName] = false;
        return false;
    }

    public static string FindFirstAssetPathOfType<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
                return path;
        }

        return null;
    }

    public static bool TryLoadFirstByGuids<T>(IEnumerable<string> guidCandidates, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        if (guidCandidates == null)
            return false;

        foreach (string guid in guidCandidates)
        {
            if (string.IsNullOrEmpty(guid))
                continue;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                continue;

            T candidate = AssetDatabase.LoadAssetAtPath<T>(path);
            if (candidate == null)
                continue;

            asset = candidate;
            return true;
        }

        return false;
    }

    public static bool TryLoadFirstAtPaths<T>(IEnumerable<string> assetPaths, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        if (assetPaths == null)
            return false;

        foreach (string rawPath in assetPaths)
        {
            string assetPath = NormalizeAssetPath(rawPath);
            if (string.IsNullOrEmpty(assetPath))
                continue;

            T candidate = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (candidate == null)
                continue;

            asset = candidate;
            return true;
        }

        return false;
    }

    public static bool TryFindAssetByFileName<T>(string fileName, string requiredFolder, out T asset) where T : UnityEngine.Object
    {
        asset = null;
        if (string.IsNullOrEmpty(fileName))
            return false;

        string searchTerm = Path.GetFileNameWithoutExtension(fileName);
        string[] guids = AssetDatabase.FindAssets(searchTerm);
        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!string.Equals(Path.GetFileName(candidatePath), fileName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!PathContainsFolder(candidatePath, requiredFolder))
                continue;

            T candidate = AssetDatabase.LoadAssetAtPath<T>(candidatePath);
            if (candidate == null)
                continue;

            asset = candidate;
            return true;
        }

        return false;
    }

    public static bool IsValidAssetReference(UnityEngine.Object asset)
    {
        if (asset == null)
            return false;

        string assetPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
        if (string.IsNullOrEmpty(assetPath))
            return false;

        return AssetDatabase.LoadMainAssetAtPath(assetPath) != null;
    }

    public static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return;

        string parent = NormalizeAssetPath(Path.GetDirectoryName(path));
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    public static void EnsureImprovedAssetToolsProjectSettingsFolder()
    {
        EnsureFolder(ImprovedAssetToolsProjectSettingsFolder);
    }

    public static string CombineAssetPath(string left, string right)
    {
        return NormalizeAssetPath(Path.Combine(left ?? string.Empty, right ?? string.Empty));
    }

    public static string GetParentAssetPath(string path)
    {
        return NormalizeAssetPath(Path.GetDirectoryName(path));
    }

    public static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrEmpty(path)
            ? string.Empty
            : path.Replace('\\', '/');
    }

    private static bool PathContainsFolder(string path, string requiredFolder)
    {
        if (string.IsNullOrEmpty(requiredFolder))
            return true;

        string normalizedPath = NormalizeAssetPath(path);
        string normalizedFolder = NormalizeAssetPath(requiredFolder);
        return normalizedPath.IndexOf(normalizedFolder, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
}
#endif
