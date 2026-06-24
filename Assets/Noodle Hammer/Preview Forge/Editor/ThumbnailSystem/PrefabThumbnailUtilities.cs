using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Provides shared detection, renderer resolution, and Project-window UI helpers for prefab thumbnails.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PrefabThumbnailDetection
    {
        public static bool IsParticlePrefab(GameObject root)
        {
            return root != null && root.GetComponentInChildren<ParticleSystem>(true) != null;
        }
    }

    internal static class PrefabThumbnailRendererRegistry
    {
        private static readonly List<IPrefabThumbnailRenderer> Renderers = new();

        static PrefabThumbnailRendererRegistry()
        {
            Register(new ParticlePrefabThumbnailRenderer());
            Register(new UiPrefabThumbnailRenderer());
        }

        public static IPrefabThumbnailRenderer FindBestRenderer(GameObject prefab, string guid, string assetPath, out PrefabThumbnailSupportInfo supportInfo)
        {
            supportInfo = PrefabThumbnailSupportInfo.Unsupported;
            IPrefabThumbnailRenderer bestRenderer = null;

            for (int i = 0; i < Renderers.Count; i++)
            {
                IPrefabThumbnailRenderer renderer = Renderers[i];
                PrefabThumbnailSupportInfo candidate = renderer.GetSupportInfo(prefab, guid, assetPath);
                if (!candidate.Supported)
                    continue;

                if (bestRenderer == null || candidate.Priority < supportInfo.Priority)
                {
                    bestRenderer = renderer;
                    supportInfo = candidate;
                }
            }

            return bestRenderer;
        }

        private static void Register(IPrefabThumbnailRenderer renderer)
        {
            if (renderer == null)
                return;

            Renderers.Add(renderer);
        }
    }

    internal static class PrefabThumbnailProjectWindowUi
    {
        private const float ListViewIconOffsetX = 2f;
        private const float ThumbnailRectScale = 1f;

        public static PrefabThumbnailSurface GetSurface(Rect selectionRect)
        {
            if (selectionRect.width > selectionRect.height * 2f)
                return PrefabThumbnailSurface.ProjectWindowList;

            if (selectionRect.width < selectionRect.height * 1.2f)
                return PrefabThumbnailSurface.ProjectWindowGrid;

            return selectionRect.height <= 24f
                ? PrefabThumbnailSurface.ProjectWindowList
                : PrefabThumbnailSurface.ProjectWindowGrid;
        }

        public static Rect GetContentRect(Rect selectionRect, PrefabThumbnailSurface surface)
        {
            Rect iconRect = GetBaseIconRect(selectionRect, surface);
            return ScaleRectAroundCenter(iconRect, ThumbnailRectScale);
        }

        private static Rect GetBaseIconRect(Rect selectionRect, PrefabThumbnailSurface surface)
        {
            if (surface == PrefabThumbnailSurface.ProjectWindowList)
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
