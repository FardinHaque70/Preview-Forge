using System;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public enum ThumbnailAssetKind
{
    Unknown = 0,
    ParticlePrefab = 1,
    GeneralPrefab = 2,
    SpritePrefab = 3,
    UiPrefab = 4,
    TmpUiPrefab = 5,
    ModelAsset = 6,
    TextureAsset = 7,
    MaterialAsset = 8,
    PrefabVariant = 9,
}

public enum ThumbnailSurface
{
    ProjectWindowGrid = 0,
    ProjectWindowList = 1,
}

public enum ThumbnailProviderType
{
    ParticlePrefab = 0,
    UiPrefab = 1,
    SpritePrefab = 2,
    GeneralPrefab = 3,
    ModelAsset = 4,
    MaterialAsset = 5,
}

public readonly struct ThumbnailRequest : IEquatable<ThumbnailRequest>
{
    public readonly string Guid;
    public readonly string AssetPath;
    public readonly ThumbnailAssetKind AssetKind;
    public readonly ThumbnailSurface Surface;

    public ThumbnailRequest(
        string guid,
        string assetPath,
        ThumbnailAssetKind assetKind,
        ThumbnailSurface surface)
    {
        Guid = guid ?? string.Empty;
        AssetPath = assetPath ?? string.Empty;
        AssetKind = assetKind;
        Surface = surface;
    }

    public bool Equals(ThumbnailRequest other)
    {
        return Guid == other.Guid
            && AssetPath == other.AssetPath
            && AssetKind == other.AssetKind
            && Surface == other.Surface;
    }

    public override bool Equals(object obj)
    {
        return obj is ThumbnailRequest other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Guid.GetHashCode();
            hash = (hash * 397) ^ AssetPath.GetHashCode();
            hash = (hash * 397) ^ (int)AssetKind;
            hash = (hash * 397) ^ (int)Surface;
            return hash;
        }
    }

    public static bool operator ==(ThumbnailRequest left, ThumbnailRequest right) => left.Equals(right);
    public static bool operator !=(ThumbnailRequest left, ThumbnailRequest right) => !left.Equals(right);
}

public sealed class ThumbnailFrameSet
{
    public Texture2D StaticFrame;
    public bool HasStaticFrame => StaticFrame != null;
}

public class ThumbnailCacheRecord
{
    public string Guid;
    public string AssetPath;
    public long DependencyHash;
    public ThumbnailAssetKind AssetKind;
    public ThumbnailFrameSet Frames;
}

public readonly struct ThumbnailSupportInfo
{
    public readonly bool Supported;
    public readonly ThumbnailAssetKind AssetKind;
    public readonly int Priority;

    public ThumbnailSupportInfo(bool supported, ThumbnailAssetKind assetKind, int priority)
    {
        Supported = supported;
        AssetKind = assetKind;
        Priority = priority;
    }

    public static ThumbnailSupportInfo Unsupported => new ThumbnailSupportInfo(false, ThumbnailAssetKind.Unknown, int.MaxValue);
}

}
