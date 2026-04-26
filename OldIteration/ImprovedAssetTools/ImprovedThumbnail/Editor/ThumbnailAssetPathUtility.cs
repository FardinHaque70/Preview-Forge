using System;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ThumbnailAssetPathUtility
{
    private static readonly string[] SupportedModelExtensions =
    {
        ".fbx",
        ".obj",
        ".blend",
        ".dae",
    };

    public static bool IsThumbnailSourceAssetPath(string assetPath)
    {
        return IsPrefabAssetPath(assetPath) || IsModelAssetPath(assetPath) || IsMaterialAssetPath(assetPath);
    }

    public static bool IsPrefabAssetPath(string assetPath)
    {
        return HasExtension(assetPath, ".prefab");
    }

    public static bool IsModelAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        for (int i = 0; i < SupportedModelExtensions.Length; i++)
        {
            if (HasExtension(assetPath, SupportedModelExtensions[i]))
                return true;
        }

        return false;
    }

    public static bool IsMaterialAssetPath(string assetPath)
    {
        return HasExtension(assetPath, ".mat");
    }

    private static bool HasExtension(string assetPath, string extension)
    {
        return !string.IsNullOrEmpty(assetPath)
            && assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }
}

}
