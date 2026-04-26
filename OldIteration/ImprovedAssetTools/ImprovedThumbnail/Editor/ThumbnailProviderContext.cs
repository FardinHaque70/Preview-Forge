using System;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class ThumbnailProviderContext
{
    public ThumbnailProviderContext(ThumbnailRequest request, UnityEngine.Object asset)
    {
        Request = request;
        Asset = asset;
    }

    public ThumbnailRequest Request { get; }
    public UnityEngine.Object Asset { get; }
    public GameObject Prefab => Asset as GameObject;
    public string Guid => Request.Guid;
    public string AssetPath => Request.AssetPath;
    public ThumbnailAssetKind AssetKind => Request.AssetKind;
    public ThumbnailSurface Surface => Request.Surface;

    public long GetDependencyHash()
    {
        if (string.IsNullOrEmpty(AssetPath))
            return 0;

        try
        {
            return AssetDatabase.GetAssetDependencyHash(AssetPath).GetHashCode();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ImprovedThumbnail] Failed to compute dependency hash for '{AssetPath}': {e.Message}");
            return 0;
        }
    }
}

}
