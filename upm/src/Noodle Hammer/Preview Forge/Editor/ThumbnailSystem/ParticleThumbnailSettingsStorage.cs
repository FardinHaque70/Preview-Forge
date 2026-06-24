using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
    public sealed class PrefabThumbnailSettingsStorage : ScriptableObject
    {
        private const string SettingsPath = "Assets/Noodle Hammer/Preview Forge/Settings/ParticleThumbnailSettings.asset";

        internal static PrefabThumbnailSettingsStorage instance => ProjectSettingsAssetUtility.LoadOrCreate<PrefabThumbnailSettingsStorage>(
            SettingsPath,
            storage =>
            {
                storage.ResetToDefaults();
                storage.ApplySerializedSettings(SettingsPath);
            });

        [SerializeField] internal bool enabled = true;
        [SerializeField] internal bool drawInProjectGrid = true;
        [SerializeField] internal bool drawInProjectList = true;
        [SerializeField] internal bool showGridViewBadges = PrefabThumbnailSettings.D_ShowGridViewBadges;
        [SerializeField] internal int gridRenderSize = PrefabThumbnailSettings.D_GridRenderSize;
        [SerializeField] internal int listRenderSize = PrefabThumbnailSettings.D_ListRenderSize;
        [SerializeField] internal Color backgroundColor = PrefabThumbnailSettings.D_BackgroundColor;
        [SerializeField] internal float boundsPadding = PrefabThumbnailSettings.D_BoundsPadding;
        [SerializeField] internal float cameraFov = PrefabThumbnailSettings.D_CameraFov;
        [SerializeField] internal float cameraYaw = PrefabThumbnailSettings.D_CameraYaw;
        [SerializeField] internal float cameraPitch = PrefabThumbnailSettings.D_CameraPitch;
        [SerializeField] internal float scanMaxSeconds = PrefabThumbnailSettings.D_ScanMaxSeconds;
        [SerializeField] internal float motionPadding = PrefabThumbnailSettings.D_MotionPadding;
        [SerializeField] internal float motionRadius = PrefabThumbnailSettings.D_MotionRadius;
        [SerializeField] internal float motionSpeed = PrefabThumbnailSettings.D_MotionSpeed;
        [SerializeField] internal float thumbnailFillTarget = PrefabThumbnailSettings.D_ThumbnailFillTarget;
        [SerializeField] internal bool enableTightFraming = PrefabThumbnailSettings.D_EnableTightFraming;
        [SerializeField] internal float particleFramingPercentile = PrefabThumbnailSettings.D_ParticleFramingPercentile;
        [SerializeField] internal int maxRendersPerUpdate = PrefabThumbnailSettings.D_MaxRendersPerUpdate;
        [SerializeField] internal float renderBudgetMs = PrefabThumbnailSettings.D_RenderBudgetMs;
        [SerializeField] internal int memoryCacheMaxEntries = PrefabThumbnailSettings.D_MemoryCacheMaxEntries;
        [SerializeField] internal bool enablePersistentCache = true;

        internal void SaveStorage()
        {
            ProjectSettingsAssetUtility.Save(this);
        }

        internal void ResetToDefaults()
        {
            enabled = true;
            drawInProjectGrid = true;
            drawInProjectList = true;
            showGridViewBadges = PrefabThumbnailSettings.D_ShowGridViewBadges;
            gridRenderSize = PrefabThumbnailSettings.D_GridRenderSize;
            listRenderSize = PrefabThumbnailSettings.D_ListRenderSize;
            backgroundColor = PrefabThumbnailSettings.D_BackgroundColor;
            boundsPadding = PrefabThumbnailSettings.D_BoundsPadding;
            cameraFov = PrefabThumbnailSettings.D_CameraFov;
            cameraYaw = PrefabThumbnailSettings.D_CameraYaw;
            cameraPitch = PrefabThumbnailSettings.D_CameraPitch;
            scanMaxSeconds = PrefabThumbnailSettings.D_ScanMaxSeconds;
            motionPadding = PrefabThumbnailSettings.D_MotionPadding;
            motionRadius = PrefabThumbnailSettings.D_MotionRadius;
            motionSpeed = PrefabThumbnailSettings.D_MotionSpeed;
            thumbnailFillTarget = PrefabThumbnailSettings.D_ThumbnailFillTarget;
            enableTightFraming = PrefabThumbnailSettings.D_EnableTightFraming;
            particleFramingPercentile = PrefabThumbnailSettings.D_ParticleFramingPercentile;
            maxRendersPerUpdate = PrefabThumbnailSettings.D_MaxRendersPerUpdate;
            renderBudgetMs = PrefabThumbnailSettings.D_RenderBudgetMs;
            memoryCacheMaxEntries = PrefabThumbnailSettings.D_MemoryCacheMaxEntries;
            enablePersistentCache = true;
        }

        private void ApplySerializedSettings(string settingsPath)
        {
            if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(enabled), out bool boolValue))
                enabled = boolValue;
            if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(drawInProjectGrid), out boolValue))
                drawInProjectGrid = boolValue;
            if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(drawInProjectList), out boolValue))
                drawInProjectList = boolValue;
            if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(showGridViewBadges), out boolValue))
                showGridViewBadges = boolValue;
            if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(gridRenderSize), out int intValue))
                gridRenderSize = intValue;
            if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(listRenderSize), out intValue))
                listRenderSize = intValue;
            if (ProjectSettingsAssetUtility.TryReadColor(settingsPath, nameof(backgroundColor), out Color colorValue))
                backgroundColor = colorValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(boundsPadding), out float floatValue))
                boundsPadding = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(cameraFov), out floatValue))
                cameraFov = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(cameraYaw), out floatValue))
                cameraYaw = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(cameraPitch), out floatValue))
                cameraPitch = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(scanMaxSeconds), out floatValue))
                scanMaxSeconds = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(motionPadding), out floatValue))
                motionPadding = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(motionRadius), out floatValue))
                motionRadius = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(motionSpeed), out floatValue))
                motionSpeed = floatValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(thumbnailFillTarget), out floatValue))
                thumbnailFillTarget = floatValue;
            if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(enableTightFraming), out boolValue))
                enableTightFraming = boolValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(particleFramingPercentile), out floatValue))
                particleFramingPercentile = floatValue;
            if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(maxRendersPerUpdate), out intValue))
                maxRendersPerUpdate = intValue;
            if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(renderBudgetMs), out floatValue))
                renderBudgetMs = floatValue;
            if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(memoryCacheMaxEntries), out intValue))
                memoryCacheMaxEntries = intValue;
            if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(enablePersistentCache), out boolValue))
                enablePersistentCache = boolValue;
        }
    }
}
