using System;
using UnityEngine;
// Defines shared prefab-thumbnail request, cache, support, and renderer contract types.

namespace NoodleHammer.PreviewForge.Editor
{
    internal enum PrefabThumbnailAssetKind
    {
        Unsupported = 0,
        ParticlePrefab = 1,
        UiPrefab = 2,
    }

    internal enum PrefabThumbnailSurface
    {
        ProjectWindowGrid = 0,
        ProjectWindowList = 1,
    }

    internal readonly struct PrefabThumbnailRequest : IEquatable<PrefabThumbnailRequest>
    {
        public readonly string Guid;
        public readonly string AssetPath;
        public readonly PrefabThumbnailAssetKind AssetKind;
        public readonly PrefabThumbnailSurface Surface;

        public PrefabThumbnailRequest(string guid, string assetPath, PrefabThumbnailAssetKind assetKind, PrefabThumbnailSurface surface)
        {
            Guid = guid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            AssetKind = assetKind;
            Surface = surface;
        }

        public bool Equals(PrefabThumbnailRequest other)
        {
            return Guid == other.Guid
                && AssetPath == other.AssetPath
                && AssetKind == other.AssetKind
                && Surface == other.Surface;
        }

        public override bool Equals(object obj)
        {
            return obj is PrefabThumbnailRequest other && Equals(other);
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

        public static bool operator ==(PrefabThumbnailRequest left, PrefabThumbnailRequest right) => left.Equals(right);
        public static bool operator !=(PrefabThumbnailRequest left, PrefabThumbnailRequest right) => !left.Equals(right);
    }

    internal sealed class PrefabThumbnailRecord
    {
        public string Guid;
        public string AssetPath;
        public string DependencyToken;
        public PrefabThumbnailAssetKind AssetKind;
        public PrefabThumbnailSurface Surface;
        public Texture2D Texture;

        public bool IsValid => Texture != null;
    }

    internal readonly struct PrefabThumbnailSupportInfo
    {
        public readonly bool Supported;
        public readonly PrefabThumbnailAssetKind AssetKind;
        public readonly int Priority;

        public PrefabThumbnailSupportInfo(bool supported, PrefabThumbnailAssetKind assetKind, int priority)
        {
            Supported = supported;
            AssetKind = assetKind;
            Priority = priority;
        }

        public static PrefabThumbnailSupportInfo Unsupported => new(false, PrefabThumbnailAssetKind.Unsupported, int.MaxValue);
    }

    internal interface IPrefabThumbnailRenderer
    {
        PrefabThumbnailAssetKind Kind { get; }
        int Priority { get; }
        PrefabThumbnailSupportInfo GetSupportInfo(GameObject prefab, string guid, string assetPath);
        Texture2D Render(string assetPath, PrefabThumbnailSurface surface);
    }

    internal readonly struct ParticleFrameCandidate
    {
        public readonly float Time;
        public readonly float Score;
        public readonly Bounds Bounds;
        public readonly int LiveCount;
        public readonly float Coverage;
        public readonly float InViewRatio;
        public readonly float CenterBias;

        public ParticleFrameCandidate(float time, float score, Bounds bounds, int liveCount, float coverage, float inViewRatio, float centerBias)
        {
            Time = time;
            Score = score;
            Bounds = bounds;
            LiveCount = liveCount;
            Coverage = coverage;
            InViewRatio = inViewRatio;
            CenterBias = centerBias;
        }
    }
}
