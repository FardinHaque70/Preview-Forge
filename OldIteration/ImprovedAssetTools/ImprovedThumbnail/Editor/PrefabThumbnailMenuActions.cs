using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class PrefabThumbnailMenuActions
{
    private const string AssetMenuRegenerate = "Assets/Improved Thumbnail/Regenerate Thumbnail";
    private const string AssetMenuClear = "Assets/Improved Thumbnail/Clear Thumbnail";
    private const string AssetMenuRegenerateSelected = "Assets/Improved Thumbnail/Regenerate Thumbnails For Selection";
    private const string AssetMenuClearSelected = "Assets/Improved Thumbnail/Clear Thumbnails For Selection";
    private const string AssetMenuRegenerateFolder = "Assets/Improved Thumbnail/Regenerate Thumbnails In Folder";
    private const string AssetMenuClearFolder = "Assets/Improved Thumbnail/Clear Thumbnails In Folder";

    [MenuItem(AssetMenuRegenerate, true)]
    private static bool ValidateRegenerateSingle()
    {
        return TryGetSingleSupportedSelection(out _);
    }

    [MenuItem(AssetMenuRegenerate)]
    private static void RegenerateSingle()
    {
        if (!TryGetSingleSupportedSelection(out string assetPath))
            return;

        PrefabThumbnailService.RegeneratePath(assetPath);
    }

    [MenuItem(AssetMenuClear, true)]
    private static bool ValidateClearSingle()
    {
        return TryGetSingleSupportedSelection(out _);
    }

    [MenuItem(AssetMenuClear)]
    private static void ClearSingle()
    {
        if (!TryGetSingleSupportedSelection(out string assetPath))
            return;

        PrefabThumbnailService.InvalidatePath(assetPath);
    }

    [MenuItem(AssetMenuRegenerateSelected, true)]
    private static bool ValidateRegenerateSelection()
    {
        return GetSupportedSelectedPaths().Count > 0;
    }

    [MenuItem(AssetMenuRegenerateSelected)]
    private static void RegenerateSelection()
    {
        PrefabThumbnailService.RegeneratePaths(GetSupportedSelectedPaths());
    }

    [MenuItem(AssetMenuClearSelected, true)]
    private static bool ValidateClearSelection()
    {
        return GetSupportedSelectedPaths().Count > 0;
    }

    [MenuItem(AssetMenuClearSelected)]
    private static void ClearSelection()
    {
        List<string> paths = GetSupportedSelectedPaths();
        for (int i = 0; i < paths.Count; i++)
            PrefabThumbnailService.InvalidatePath(paths[i]);
    }

    [MenuItem(AssetMenuRegenerateFolder, true)]
    private static bool ValidateRegenerateFolder()
    {
        return GetSupportedPathsInSelectedFolder().Count > 0;
    }

    [MenuItem(AssetMenuRegenerateFolder)]
    private static void RegenerateFolder()
    {
        PrefabThumbnailService.RegeneratePaths(GetSupportedPathsInSelectedFolder());
    }

    [MenuItem(AssetMenuClearFolder, true)]
    private static bool ValidateClearFolder()
    {
        return GetSupportedPathsInSelectedFolder().Count > 0;
    }

    [MenuItem(AssetMenuClearFolder)]
    private static void ClearFolder()
    {
        List<string> paths = GetSupportedPathsInSelectedFolder();
        for (int i = 0; i < paths.Count; i++)
            PrefabThumbnailService.InvalidatePath(paths[i]);
    }

    private static bool TryGetSingleSupportedSelection(out string assetPath)
    {
        assetPath = null;
        if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length != 1)
            return false;

        string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
        if (!PrefabThumbnailService.SupportsPath(path))
            return false;

        assetPath = path;
        return true;
    }

    private static List<string> GetSupportedSelectedPaths()
    {
        List<string> paths = new List<string>();
        if (Selection.assetGUIDs == null)
            return paths;

        for (int i = 0; i < Selection.assetGUIDs.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[i]);
            if (PrefabThumbnailService.SupportsPath(path))
                paths.Add(path);
        }

        return paths;
    }

    private static List<string> GetSupportedPathsInSelectedFolder()
    {
        List<string> paths = new List<string>();
        if (!TryGetSingleSelectedFolder(out string folderPath))
            return paths;

        string[] assetGuids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            if (PrefabThumbnailService.SupportsPath(path))
                paths.Add(path);
        }

        return paths;
    }

    private static bool TryGetSingleSelectedFolder(out string folderPath)
    {
        folderPath = null;
        if (Selection.assetGUIDs == null || Selection.assetGUIDs.Length != 1)
            return false;

        string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
        if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
            return false;

        folderPath = path;
        return true;
    }
}

}
