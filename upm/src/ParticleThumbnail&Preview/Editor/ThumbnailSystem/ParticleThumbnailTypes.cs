using System;
using UnityEngine;
// Defines core thumbnail request, cache, and result data structures shared across rendering, caching, and service layers.

namespace ParticleThumbnailAndPreview.Editor
{
    internal enum ParticleThumbnailSurface
    {
        ProjectWindowGrid = 0,
        ProjectWindowList = 1,
    }

    internal readonly struct ParticleThumbnailRequest : IEquatable<ParticleThumbnailRequest>
    {
        public readonly string Guid;
        public readonly string AssetPath;
        public readonly ParticleThumbnailSurface Surface;

        public ParticleThumbnailRequest(string guid, string assetPath, ParticleThumbnailSurface surface)
        {
            Guid = guid ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Surface = surface;
        }

        public bool Equals(ParticleThumbnailRequest other)
        {
            return Guid == other.Guid
                && AssetPath == other.AssetPath
                && Surface == other.Surface;
        }

        public override bool Equals(object obj)
        {
            return obj is ParticleThumbnailRequest other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Guid.GetHashCode();
                hash = (hash * 397) ^ AssetPath.GetHashCode();
                hash = (hash * 397) ^ (int)Surface;
                return hash;
            }
        }

        public static bool operator ==(ParticleThumbnailRequest left, ParticleThumbnailRequest right) => left.Equals(right);
        public static bool operator !=(ParticleThumbnailRequest left, ParticleThumbnailRequest right) => !left.Equals(right);
    }

    internal sealed class ParticleThumbnailRecord
    {
        public string Guid;
        public string AssetPath;
        public string DependencyToken;
        public ParticleThumbnailSurface Surface;
        public Texture2D Texture;

        public bool IsValid => Texture != null;
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
