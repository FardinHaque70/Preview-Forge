using System;
using UnityEditor;
using UnityEngine;
// Stores shared prefab-thumbnail configuration and renders the project settings UI.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PrefabThumbnailSettings
    {
        public const int D_GridRenderSize = 128;
        public const int D_ListRenderSize = 32;
        public const bool D_ShowGridViewBadges = true;
        public static readonly Color D_BackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        public const float D_CameraFov = 30f;
        public const float D_CameraYaw = 35f;
        public const float D_CameraPitch = 25f;
        public const float D_BoundsPadding = 0.15f;

        public const float D_ScanMaxSeconds = 3f;
        public const float D_ParticleRenderTimeoutSeconds = 3f;
        public const float D_MotionPadding = 0.2f;
        public const float D_MotionRadius = 3f;
        public const float D_MotionSpeed = 60f;
        public const float D_ThumbnailFillTarget = 1f;
        public const bool D_EnableTightFraming = true;
        public const float D_ParticleFramingPercentile = 0.92f;

        public const int D_MaxRendersPerUpdate = 1;
        public const float D_RenderBudgetMs = 12f;
        public const int D_MemoryCacheMaxEntries = 200;

        private const string SettingsTokenVersion = "prefab-thumb-v2";

        public static event Action SettingsChanged;

        private static PrefabThumbnailSettingsStorage Storage => PrefabThumbnailSettingsStorage.instance;

        public static bool Enabled => Storage.enabled;
        public static bool DrawInProjectGrid => Storage.drawInProjectGrid;
        public static bool DrawInProjectList => Storage.drawInProjectList;
        public static bool ShowGridViewBadges => Storage.showGridViewBadges;
        public static int GridRenderSize => Mathf.Clamp(Storage.gridRenderSize, 32, 128);
        public static int ListRenderSize => Mathf.Clamp(Storage.listRenderSize, 16, 64);
        public static Color BackgroundColor => Storage.backgroundColor;
        public static float BoundsPadding => Mathf.Clamp(Storage.boundsPadding, 0f, 1f);
        public static float CameraFov => Mathf.Clamp(Storage.cameraFov, 10f, 90f);
        public static float CameraYaw => Mathf.Clamp(Storage.cameraYaw, -180f, 180f);
        public static float CameraPitch => Mathf.Clamp(Storage.cameraPitch, -89f, 89f);
        public static float ScanMaxSeconds => Mathf.Clamp(Storage.scanMaxSeconds, 0.5f, 10f);
        public static float ParticleRenderTimeoutSeconds => Mathf.Clamp(Storage.particleRenderTimeoutSeconds, 0.5f, 10f);
        public static float MotionPadding => Mathf.Clamp(Storage.motionPadding, 0f, 3f);
        public static float MotionRadius => Mathf.Clamp(Storage.motionRadius, 0.1f, 50f);
        public static float MotionSpeed => Mathf.Clamp(Storage.motionSpeed, 0.1f, 200f);
        public static float ThumbnailFillTarget => Mathf.Clamp(Storage.thumbnailFillTarget, 0.45f, 1f);
        public static bool EnableTightFraming => Storage.enableTightFraming;
        public static float ParticleFramingPercentile => Mathf.Clamp(Storage.particleFramingPercentile, 0.80f, 0.99f);
        public static int MaxRendersPerUpdate => Mathf.Max(1, Storage.maxRendersPerUpdate);
        public static float RenderBudgetMs => Mathf.Clamp(Storage.renderBudgetMs, 1f, 100f);
        public static int MemoryCacheMaxEntries => Mathf.Max(10, Storage.memoryCacheMaxEntries);
        public static bool EnablePersistentCache => Storage.enablePersistentCache;

        public static int GetRenderSize(PrefabThumbnailSurface surface)
        {
            return surface == PrefabThumbnailSurface.ProjectWindowList ? ListRenderSize : GridRenderSize;
        }

        public static string GetPersistentSettingsToken()
        {
            return BuildPersistentSettingsToken(Storage);
        }

        internal static string BuildPersistentSettingsToken(PrefabThumbnailSettingsStorage storage)
        {
            if (storage == null)
            {
                return BuildPersistentSettingsToken(
                    enabled: true,
                    drawInProjectGrid: true,
                    drawInProjectList: true,
                    gridRenderSize: D_GridRenderSize,
                    listRenderSize: D_ListRenderSize,
                    backgroundColor: D_BackgroundColor,
                    boundsPadding: D_BoundsPadding,
                    cameraFov: D_CameraFov,
                    cameraYaw: D_CameraYaw,
                    cameraPitch: D_CameraPitch,
                    scanMaxSeconds: D_ScanMaxSeconds,
                    particleRenderTimeoutSeconds: D_ParticleRenderTimeoutSeconds,
                    motionPadding: D_MotionPadding,
                    motionRadius: D_MotionRadius,
                    motionSpeed: D_MotionSpeed,
                    enableTightFraming: D_EnableTightFraming,
                    particleFramingPercentile: D_ParticleFramingPercentile,
                    thumbnailFillTarget: D_ThumbnailFillTarget,
                    maxRendersPerUpdate: D_MaxRendersPerUpdate,
                    renderBudgetMs: D_RenderBudgetMs,
                    memoryCacheMaxEntries: D_MemoryCacheMaxEntries,
                    enablePersistentCache: true);
            }

            return BuildPersistentSettingsToken(
                storage.enabled,
                storage.drawInProjectGrid,
                storage.drawInProjectList,
                storage.gridRenderSize,
                storage.listRenderSize,
                storage.backgroundColor,
                storage.boundsPadding,
                storage.cameraFov,
                storage.cameraYaw,
                storage.cameraPitch,
                storage.scanMaxSeconds,
                storage.particleRenderTimeoutSeconds,
                storage.motionPadding,
                storage.motionRadius,
                storage.motionSpeed,
                storage.enableTightFraming,
                storage.particleFramingPercentile,
                storage.thumbnailFillTarget,
                storage.maxRendersPerUpdate,
                storage.renderBudgetMs,
                storage.memoryCacheMaxEntries,
                storage.enablePersistentCache);
        }

        internal static string BuildParticleFramingSettingsFragment(bool enableTightFraming, float framingPercentile, float thumbnailFillTarget)
        {
            return
                $"{enableTightFraming}|{Mathf.Clamp(framingPercentile, 0.80f, 0.99f):F4}|" +
                $"{Mathf.Clamp(thumbnailFillTarget, 0.45f, 1f):F4}";
        }

        internal static string BuildPersistentSettingsToken(
            bool enabled,
            bool drawInProjectGrid,
            bool drawInProjectList,
            int gridRenderSize,
            int listRenderSize,
            Color backgroundColor,
            float boundsPadding,
            float cameraFov,
            float cameraYaw,
            float cameraPitch,
            float scanMaxSeconds,
            float particleRenderTimeoutSeconds,
            float motionPadding,
            float motionRadius,
            float motionSpeed,
            bool enableTightFraming,
            float particleFramingPercentile,
            float thumbnailFillTarget,
            int maxRendersPerUpdate,
            float renderBudgetMs,
            int memoryCacheMaxEntries,
            bool enablePersistentCache)
        {
            string payload =
                $"{enabled}|{drawInProjectGrid}|{drawInProjectList}|{Mathf.Clamp(gridRenderSize, 32, 128)}|{Mathf.Clamp(listRenderSize, 16, 64)}|" +
                $"{backgroundColor.r:F4},{backgroundColor.g:F4},{backgroundColor.b:F4},{backgroundColor.a:F4}|" +
                $"{Mathf.Clamp(boundsPadding, 0f, 1f):F4}|{Mathf.Clamp(cameraFov, 10f, 90f):F4}|{Mathf.Clamp(cameraYaw, -180f, 180f):F4}|{Mathf.Clamp(cameraPitch, -89f, 89f):F4}|" +
                $"{Mathf.Clamp(scanMaxSeconds, 0.5f, 10f):F4}|{Mathf.Clamp(particleRenderTimeoutSeconds, 0.5f, 10f):F4}|{Mathf.Clamp(motionPadding, 0f, 3f):F4}|{Mathf.Clamp(motionRadius, 0.1f, 50f):F4}|{Mathf.Clamp(motionSpeed, 0.1f, 200f):F4}|" +
                $"{BuildParticleFramingSettingsFragment(enableTightFraming, particleFramingPercentile, thumbnailFillTarget)}|" +
                $"{Mathf.Max(1, maxRendersPerUpdate)}|{Mathf.Clamp(renderBudgetMs, 1f, 100f):F4}|{Mathf.Max(10, memoryCacheMaxEntries)}|{enablePersistentCache}|{SettingsTokenVersion}";
            return Hash128.Compute(payload).ToString();
        }

        public static void NotifyChanged()
        {
            SettingsChanged?.Invoke();
            EditorApplication.RepaintProjectWindow();
        }
    }

    internal static class PrefabThumbnailSettingsProvider
    {
        private enum SettingsTab
        {
            Display = 0,
            Framing = 1,
            Particle = 2,
            Performance = 3,
        }

        private static readonly string[] MainTabTitles =
        {
            "Display",
            "Framing",
            "Particle",
            "Performance",
        };

        private static Vector2 SettingsScroll;
        private static SettingsTab SelectedTab = SettingsTab.Display;

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/Preview Forge/Prefab Thumbnails", SettingsScope.Project)
            {
                label = "Prefab Thumbnails",
                guiHandler = _ => DrawGui(),
                keywords = new System.Collections.Generic.HashSet<string>
                {
                    "prefab",
                    "particle",
                    "ui",
                    "thumbnail",
                    "project window",
                    "preview",
                },
            };
        }

        private static void DrawGui()
        {
            PrefabThumbnailSettingsStorage storage = PrefabThumbnailSettingsStorage.instance;
            SerializedObject serializedObject = ProjectSettingsUndoUtility.CreateSerializedObject(storage, () => SaveAndNotify(storage));

            DrawEnabledToggle(serializedObject);
            if (!serializedObject.FindProperty(nameof(storage.enabled)).boolValue)
            {
                ProjectSettingsUndoUtility.ApplyModifiedProperties(serializedObject);
                return;
            }

            SettingsScroll = EditorGUILayout.BeginScrollView(SettingsScroll, false, false);
            SelectedTab = (SettingsTab)GUILayout.Toolbar((int)SelectedTab, MainTabTitles);
            EditorGUILayout.Space(6f);
            DrawSelectedTab(serializedObject);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8f);
            DrawBottomActionsPanel(storage);
            DrawCacheRuntimeInfo();
            ProjectSettingsUndoUtility.ApplyModifiedProperties(serializedObject);
        }

        private static void DrawEnabledToggle(SerializedObject serializedObject)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                SerializedProperty enabledProperty = serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.enabled));
                bool enabled = EditorGUILayout.Toggle(enabledProperty.boolValue, GUILayout.Width(18f));
                if (enabled != enabledProperty.boolValue)
                    enabledProperty.boolValue = enabled;

                EditorGUILayout.LabelField(
                    new GUIContent("Enable Prefab Thumbnails", "Turn custom particle and UI prefab thumbnails on or off for this project."),
                    EditorStyles.boldLabel);
            }
        }

        private static void DrawSelectedTab(SerializedObject serializedObject)
        {
            switch (SelectedTab)
            {
                case SettingsTab.Display:
                    DrawDisplayTab(serializedObject);
                    break;
                case SettingsTab.Framing:
                    DrawFramingTab(serializedObject);
                    break;
                case SettingsTab.Particle:
                    DrawParticleTab(serializedObject);
                    break;
                case SettingsTab.Performance:
                    DrawPerformanceTab(serializedObject);
                    break;
            }
        }

        private static void DrawDisplayTab(SerializedObject serializedObject)
        {
            DrawSectionCard("Draw Modes", () =>
            {
                DrawToggle(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.drawInProjectGrid)), "Draw In Grid", "Render custom thumbnails in Project window grid view.");
                DrawToggle(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.drawInProjectList)), "Draw In List", "Render custom thumbnails in Project window list view.");
                DrawToggle(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.showGridViewBadges)), "Show Grid View Badges", "Draw small type badges on prefab thumbnails in Project window grid view only.");
            });

            DrawSectionCard("Background", () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.backgroundColor)), new GUIContent("Background Color"));
            });

            DrawSectionCard("Thumbnail Size", () =>
            {
                DrawIntSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.gridRenderSize)), "Grid Size", 32, 128);
                DrawIntSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.listRenderSize)), "List Size", 16, 64);
            });
        }

        private static void DrawFramingTab(SerializedObject serializedObject)
        {
            DrawSectionCard("Shared Framing", () =>
            {
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.boundsPadding)), "Bounds Padding", 0f, 1f);
            });

            DrawSectionCard("Particle Camera", () =>
            {
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.cameraFov)), "Camera FOV", 10f, 90f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.cameraYaw)), "Camera Yaw", -180f, 180f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.cameraPitch)), "Camera Pitch", -89f, 89f);
            });
        }

        private static void DrawParticleTab(SerializedObject serializedObject)
        {
            DrawSectionCard("Particle Scan Window", () =>
            {
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.scanMaxSeconds)), "Scan Max Seconds", 0.5f, 10f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.particleRenderTimeoutSeconds)), "Render Timeout (s)", 0.5f, 10f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.motionPadding)), "Motion Padding", 0f, 3f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.motionRadius)), "Motion Radius", 0.1f, 50f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.motionSpeed)), "Motion Speed", 0.1f, 200f);
            });

            DrawSectionCard("Particle Framing", () =>
            {
                DrawToggle(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.enableTightFraming)), "Enable Tight Framing", "Use particle percentile bounds when possible.");
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.particleFramingPercentile)), "Framing Percentile", 0.80f, 0.99f);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.thumbnailFillTarget)), "Fill Target", 0.45f, 1f);
            });
        }

        private static void DrawPerformanceTab(SerializedObject serializedObject)
        {
            DrawSectionCard("Render Budget", () =>
            {
                DrawIntSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.maxRendersPerUpdate)), "Max Renders Per Update", 1, 64);
                DrawFloatSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.renderBudgetMs)), "Render Budget (ms)", 1f, 100f);
            });

            DrawSectionCard("Caching", () =>
            {
                DrawIntSlider(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.memoryCacheMaxEntries)), "Memory Cache Entries", 10, 1000);
                DrawToggle(serializedObject.FindProperty(nameof(PrefabThumbnailSettingsStorage.enablePersistentCache)), "Enable Persistent Cache", "Store rendered thumbnails under Library for faster reuse.");
            });
        }

        private static void DrawBottomActionsPanel(PrefabThumbnailSettingsStorage storage)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Memory Cache"))
                    PrefabThumbnailService.ClearMemoryCache();

                if (GUILayout.Button("Clear Persistent Cache"))
                    PrefabThumbnailService.ClearPersistentCache();

                if (GUILayout.Button("Generate All Supported Prefab Thumbnails"))
                    EditorApplication.delayCall += PrefabThumbnailService.GenerateAllThumbnailsInProjectFromSettings;
            }
        }

        private static void DrawCacheRuntimeInfo()
        {
            PrefabThumbnailService.CacheStats stats = PrefabThumbnailService.GetCacheStats();
            EditorGUILayout.HelpBox(
                $"Entries: {stats.TotalEntries} | Queue: {stats.QueueDepth} | Persistent: {stats.PersistentEntryCount} | " +
                $"Failed: {stats.FailedCount} | Memory: {EditorUtility.FormatBytes(stats.MemoryCacheBytes)} | Disk: {EditorUtility.FormatBytes(stats.DiskCacheBytes)}",
                MessageType.None);
        }

        private static void DrawSectionCard(string title, Action drawBody)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUILayout.Space(2f);
                drawBody?.Invoke();
            }
        }

        private static void DrawToggle(SerializedProperty property, string label, string tooltip)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }

        private static void DrawIntSlider(SerializedProperty property, string label, int min, int max)
        {
            EditorGUILayout.IntSlider(property, min, max, new GUIContent(label));
        }

        private static void DrawFloatSlider(SerializedProperty property, string label, float min, float max)
        {
            EditorGUILayout.Slider(property, min, max, new GUIContent(label));
        }

        private static void SaveAndNotify(PrefabThumbnailSettingsStorage storage)
        {
            storage.SaveStorage();
            PrefabThumbnailSettings.NotifyChanged();
        }
    }
}
