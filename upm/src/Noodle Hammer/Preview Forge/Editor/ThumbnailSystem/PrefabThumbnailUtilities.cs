using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
// Provides shared detection, renderer resolution, and Project-window UI helpers for prefab thumbnails.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PrefabThumbnailDetection
    {
        public static bool IsParticlePrefab(GameObject root)
        {
            return root != null && root.GetComponent<ParticleSystem>() != null;
        }
    }

    internal static class PrefabThumbnailPrefabHealthProbe
    {
        private const string ObjectHeaderPrefix = "--- !u!";
        private const string MonoBehaviourHeaderPrefix = "--- !u!114";
        private const string ScriptFieldMarker = "m_Script:";
        private const string GuidMarker = "guid:";

        public static bool HasMissingScriptsAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                string absolutePath = GetAbsoluteAssetPath(assetPath);
                if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                    return false;

                string prefabText = File.ReadAllText(absolutePath);
                return HasMissingScriptsInPrefabText(prefabText, ResolveGuidToAssetPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static bool HasMissingScriptsAtPathForTests(string assetPath)
        {
            return HasMissingScriptsAtPath(assetPath);
        }

        internal static bool HasMissingScriptsInPrefabTextForTests(string prefabText, Func<string, string> guidResolver)
        {
            return HasMissingScriptsInPrefabText(prefabText, guidResolver);
        }

        private static bool HasMissingScriptsInPrefabText(string prefabText, Func<string, string> guidResolver)
        {
            if (string.IsNullOrEmpty(prefabText) || guidResolver == null)
                return false;

            Dictionary<string, bool> guidExistsByValue = new(StringComparer.OrdinalIgnoreCase);
            using StringReader reader = new StringReader(prefabText);
            bool inMonoBehaviourSection = false;

            while (reader.ReadLine() is { } line)
            {
                if (line.StartsWith(ObjectHeaderPrefix, StringComparison.Ordinal))
                {
                    inMonoBehaviourSection = line.StartsWith(MonoBehaviourHeaderPrefix, StringComparison.Ordinal);
                    continue;
                }

                if (!inMonoBehaviourSection || !TryExtractScriptGuid(line, out string scriptGuid) || IsUnsetGuid(scriptGuid))
                    continue;

                if (!guidExistsByValue.TryGetValue(scriptGuid, out bool exists))
                {
                    exists = !string.IsNullOrEmpty(guidResolver(scriptGuid));
                    guidExistsByValue[scriptGuid] = exists;
                }

                if (!exists)
                    return true;
            }

            return false;
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            string assetsDirectory = Application.dataPath;
            string projectDirectory = Directory.GetParent(assetsDirectory)?.FullName;
            if (string.IsNullOrEmpty(projectDirectory))
                return null;

            return Path.GetFullPath(Path.Combine(projectDirectory, assetPath));
        }

        private static string ResolveGuidToAssetPath(string guid)
        {
            return string.IsNullOrEmpty(guid) ? string.Empty : AssetDatabase.GUIDToAssetPath(guid);
        }

        private static bool TryExtractScriptGuid(string line, out string scriptGuid)
        {
            scriptGuid = string.Empty;
            if (string.IsNullOrEmpty(line))
                return false;

            int scriptFieldIndex = line.IndexOf(ScriptFieldMarker, StringComparison.Ordinal);
            if (scriptFieldIndex < 0)
                return false;

            int guidIndex = line.IndexOf(GuidMarker, scriptFieldIndex, StringComparison.Ordinal);
            if (guidIndex < 0)
                return false;

            guidIndex += GuidMarker.Length;
            while (guidIndex < line.Length && char.IsWhiteSpace(line[guidIndex]))
                guidIndex++;

            if (guidIndex + 32 > line.Length)
                return false;

            ReadOnlySpan<char> guidSpan = line.AsSpan(guidIndex, 32);
            for (int i = 0; i < guidSpan.Length; i++)
            {
                if (!Uri.IsHexDigit(guidSpan[i]))
                    return false;
            }

            scriptGuid = guidSpan.ToString();
            return true;
        }

        private static bool IsUnsetGuid(string scriptGuid)
        {
            if (string.IsNullOrEmpty(scriptGuid))
                return true;

            for (int i = 0; i < scriptGuid.Length; i++)
            {
                if (scriptGuid[i] != '0')
                    return false;
            }

            return true;
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
