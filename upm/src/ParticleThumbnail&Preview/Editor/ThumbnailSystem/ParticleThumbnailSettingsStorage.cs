using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
	public sealed class ParticleThumbnailSettingsStorage : ScriptableObject
	{
		private const string SettingsPath = "Assets/ParticleThumbnail&Preview/Settings/ParticleThumbnailSettings.asset";
		private const string LegacySettingsPath = "ProjectSettings/ParticleThumbnailAndPreview/ParticleThumbnailSettings.asset";

		internal static ParticleThumbnailSettingsStorage instance => ProjectSettingsAssetUtility.LoadOrCreate<ParticleThumbnailSettingsStorage>(
			SettingsPath,
			storage =>
			{
				storage.ResetToDefaults();
				storage.ApplySerializedSettings(LegacySettingsPath);
				storage.ApplySerializedSettings(SettingsPath);
			});

		[SerializeField] internal bool enabled = true;
		[SerializeField] internal bool drawInProjectGrid = true;
		[SerializeField] internal bool drawInProjectList = true;
		[SerializeField] internal int gridRenderSize = ParticleThumbnailSettings.D_GridRenderSize;
		[SerializeField] internal int listRenderSize = ParticleThumbnailSettings.D_ListRenderSize;
		[SerializeField] internal Color backgroundColor = ParticleThumbnailSettings.D_BackgroundColor;
		[SerializeField] internal float cameraFov = ParticleThumbnailSettings.D_CameraFov;
		[SerializeField] internal float cameraYaw = ParticleThumbnailSettings.D_CameraYaw;
		[SerializeField] internal float cameraPitch = ParticleThumbnailSettings.D_CameraPitch;
		[SerializeField] internal float boundsPadding = ParticleThumbnailSettings.D_BoundsPadding;
		[SerializeField] internal float scanMaxSeconds = ParticleThumbnailSettings.D_ScanMaxSeconds;
		[SerializeField] internal float motionPadding = ParticleThumbnailSettings.D_MotionPadding;
		[SerializeField] internal float motionRadius = ParticleThumbnailSettings.D_MotionRadius;
		[SerializeField] internal float motionSpeed = ParticleThumbnailSettings.D_MotionSpeed;
		[SerializeField] internal float thumbnailFillTarget = ParticleThumbnailSettings.D_ThumbnailFillTarget;
		[SerializeField] internal bool enableTightFraming = ParticleThumbnailSettings.D_EnableTightFraming;
		[SerializeField] internal float particleFramingPercentile = ParticleThumbnailSettings.D_ParticleFramingPercentile;
		[SerializeField] internal int maxRendersPerUpdate = ParticleThumbnailSettings.D_MaxRendersPerUpdate;
		[SerializeField] internal float renderBudgetMs = ParticleThumbnailSettings.D_RenderBudgetMs;
		[SerializeField] internal int memoryCacheMaxEntries = ParticleThumbnailSettings.D_MemoryCacheMaxEntries;
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
			gridRenderSize = ParticleThumbnailSettings.D_GridRenderSize;
			listRenderSize = ParticleThumbnailSettings.D_ListRenderSize;
			backgroundColor = ParticleThumbnailSettings.D_BackgroundColor;
			cameraFov = ParticleThumbnailSettings.D_CameraFov;
			cameraYaw = ParticleThumbnailSettings.D_CameraYaw;
			cameraPitch = ParticleThumbnailSettings.D_CameraPitch;
			boundsPadding = ParticleThumbnailSettings.D_BoundsPadding;
			scanMaxSeconds = ParticleThumbnailSettings.D_ScanMaxSeconds;
			motionPadding = ParticleThumbnailSettings.D_MotionPadding;
			motionRadius = ParticleThumbnailSettings.D_MotionRadius;
			motionSpeed = ParticleThumbnailSettings.D_MotionSpeed;
			thumbnailFillTarget = ParticleThumbnailSettings.D_ThumbnailFillTarget;
			enableTightFraming = ParticleThumbnailSettings.D_EnableTightFraming;
			particleFramingPercentile = ParticleThumbnailSettings.D_ParticleFramingPercentile;
			maxRendersPerUpdate = ParticleThumbnailSettings.D_MaxRendersPerUpdate;
			renderBudgetMs = ParticleThumbnailSettings.D_RenderBudgetMs;
			memoryCacheMaxEntries = ParticleThumbnailSettings.D_MemoryCacheMaxEntries;
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
			if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(gridRenderSize), out int intValue))
				gridRenderSize = intValue;
			if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(listRenderSize), out intValue))
				listRenderSize = intValue;
			if (ProjectSettingsAssetUtility.TryReadColor(settingsPath, nameof(backgroundColor), out Color colorValue))
				backgroundColor = colorValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(cameraFov), out float floatValue))
				cameraFov = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(cameraYaw), out floatValue))
				cameraYaw = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(cameraPitch), out floatValue))
				cameraPitch = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(boundsPadding), out floatValue))
				boundsPadding = floatValue;
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
