using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class ParticleThumbnailScoring
    {
        public const float MinCoverageForScoring = 0.01f;
        public const float MinInViewForScoring = 0.20f;

        public static float ComputeScore(int liveCount, int peakLiveCount, float coverage, float inViewRatio, float centerBias)
        {
            if (liveCount <= 0 || peakLiveCount <= 0)
                return 0f;

            float safeCoverage = Mathf.Clamp01(coverage);
            float safeInViewRatio = Mathf.Clamp01(inViewRatio);
            float safeCenterBias = Mathf.Clamp01(centerBias);

            if (safeCoverage < MinCoverageForScoring || safeInViewRatio < MinInViewForScoring)
                return 0f;

            float liveRatio = Mathf.Clamp01((float)liveCount / peakLiveCount);
            return
                safeCoverage * 0.55f +
                liveRatio * 0.30f +
                safeInViewRatio * 0.10f +
                safeCenterBias * 0.05f;
        }

        public static float ComputePreliminaryVisibility(int liveCount, int peakLiveCount, float coverage, float inViewRatio, float centerBias)
        {
            float safeCoverage = Mathf.Clamp01(coverage);
            float safeInViewRatio = Mathf.Clamp01(inViewRatio);
            float safeCenterBias = Mathf.Clamp01(centerBias);
            float liveRatio = peakLiveCount > 0 ? Mathf.Clamp01((float)liveCount / peakLiveCount) : 0f;

            return
                safeCoverage * 0.65f +
                safeInViewRatio * 0.20f +
                liveRatio * 0.10f +
                safeCenterBias * 0.05f;
        }
    }
}
