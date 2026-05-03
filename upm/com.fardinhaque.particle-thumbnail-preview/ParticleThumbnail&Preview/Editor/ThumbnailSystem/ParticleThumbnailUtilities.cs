using UnityEditor;
using UnityEngine;
// Provides shared utility helpers for thumbnail drawing, overlay rendering, texture handling, and GUI convenience workflows.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class ParticleThumbnailDetection
    {
        public static bool IsParticlePrefab(GameObject root)
        {
            return root != null && root.GetComponent<ParticleSystem>() != null;
        }
    }

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

    internal static class ParticleThumbnailProjectWindowUi
    {
        private const float ListViewIconOffsetX = 2f;
        private const float ThumbnailRectScale = 0.95f;

        public static ParticleThumbnailSurface GetSurface(Rect selectionRect)
        {
            if (selectionRect.width > selectionRect.height * 2f)
                return ParticleThumbnailSurface.ProjectWindowList;

            if (selectionRect.width < selectionRect.height * 1.2f)
                return ParticleThumbnailSurface.ProjectWindowGrid;

            return selectionRect.height <= 24f
                ? ParticleThumbnailSurface.ProjectWindowList
                : ParticleThumbnailSurface.ProjectWindowGrid;
        }

        public static Rect GetContentRect(Rect selectionRect, ParticleThumbnailSurface surface)
        {
            Rect iconRect = GetBaseIconRect(selectionRect, surface);
            return ScaleRectAroundCenter(iconRect, ThumbnailRectScale);
        }

        public static Rect GetOutlineRect(Rect selectionRect, ParticleThumbnailSurface surface)
        {
            // Outline uses full icon rect so it fills the padding introduced by thumbnail scaling.
            return GetBaseIconRect(selectionRect, surface);
        }

        private static Rect GetBaseIconRect(Rect selectionRect, ParticleThumbnailSurface surface)
        {
            if (surface == ParticleThumbnailSurface.ProjectWindowList)
            {
                float size = Mathf.Max(0f, selectionRect.height);
                return new Rect(selectionRect.x + ListViewIconOffsetX, selectionRect.y, size, size);
            }

            float contentSize = Mathf.Max(0f, selectionRect.width);
            return new Rect(selectionRect.x, selectionRect.y, contentSize, contentSize);
        }

        private static Rect ScaleRectAroundCenter(Rect rect, float scale)
        {
            float clampedScale = Mathf.Clamp(scale, 0.1f, 1f);
            float width = rect.width * clampedScale;
            float height = rect.height * clampedScale;
            float x = rect.x + (rect.width - width) * 0.5f;
            float y = rect.y + (rect.height - height) * 0.5f;
            return new Rect(x, y, width, height);
        }

        public static bool ShouldSkipObjectSelectorContext()
        {
            return IsObjectSelector(EditorWindow.focusedWindow) || IsObjectSelector(EditorWindow.mouseOverWindow);
        }

        private static bool IsObjectSelector(EditorWindow window)
        {
            return window != null && window.GetType().Name.Contains("ObjectSelector");
        }
    }
}
