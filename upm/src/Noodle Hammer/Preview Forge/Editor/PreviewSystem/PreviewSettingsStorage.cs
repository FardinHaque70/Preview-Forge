using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
	public sealed class PreviewSettingsStorage : ScriptableObject
	{
		private const string SettingsPath = "Assets/Noodle Hammer/Preview Forge/Settings/ParticlePreviewSettings.asset";

		internal static PreviewSettingsStorage instance => ProjectSettingsAssetUtility.LoadOrCreate<PreviewSettingsStorage>(
			SettingsPath,
			storage =>
			{
				storage.ResetToDefaults();
				storage.ApplySerializedSettings(SettingsPath);
			});

		[SerializeField] internal bool enabled = PreviewSettings.D_Enabled;
		[SerializeField] internal bool active = PreviewSettings.D_Active;
		[SerializeField] internal int refreshFps = PreviewSettings.D_RefreshFps;
		[SerializeField] internal float orbitSmoothing = PreviewSettings.D_OrbitSmoothing;
		[SerializeField] internal float panSmoothing = PreviewSettings.D_PanSmoothing;
		[SerializeField] internal float motionPadding = PreviewSettings.D_MotionPadding;
		[SerializeField] internal float motionRadius = PreviewSettings.D_MotionRadius;
		[SerializeField] internal float motionSpeed = PreviewSettings.D_MotionSpeed;
		[SerializeField] internal Color backgroundColor = PreviewSettings.D_BackgroundColor;
		[SerializeField] internal PreviewToolbarColorPreset toolbarColorPreset = PreviewSettings.D_ToolbarColorPreset;
		[SerializeField] internal float toolbarHeight = PreviewSettings.D_ToolbarHeight;
		[SerializeField] internal bool modelPreviewActive = PreviewSettings.D_ModelPreviewActive;
		[SerializeField] internal bool spritePrefabPreviewActive = PreviewSettings.D_SpritePrefabPreviewActive;
		[SerializeField] internal bool modelImporterPreviewActive = PreviewSettings.D_ModelImporterPreviewActive;
		[SerializeField] internal bool modelDefaultTurntableEnabled = PreviewSettings.D_ModelDefaultTurntableEnabled;
		[SerializeField] internal bool showStatsEnabled = PreviewSettings.D_ShowStatsEnabled;
		[SerializeField] internal bool sharedBoundsRulerDefaultEnabled = PreviewSettings.D_SharedBoundsRulerDefaultEnabled;
		[SerializeField] internal bool modelDefaultLightRotationGizmosEnabled = PreviewSettings.D_ModelDefaultLightRotationGizmosEnabled;
		[SerializeField] internal bool modelDefaultSkyboxEnabled = PreviewSettings.D_ModelDefaultSkyboxEnabled;
		[SerializeField] internal bool sharedGridDefaultEnabled = PreviewSettings.D_SharedGridDefaultEnabled;
		[SerializeField] internal bool sharedGridAxisTextDefaultEnabled = PreviewSettings.D_SharedGridAxisTextDefaultEnabled;
		[SerializeField] internal float sharedGridHalfSize = PreviewSettings.D_SharedGridHalfSize;
		[SerializeField] internal float sharedGridStep = PreviewSettings.D_SharedGridStep;
		[SerializeField] internal float sharedGridAlpha = PreviewSettings.D_SharedGridAlpha;
		[SerializeField] internal float sharedGridFadeStartBoundsScale = PreviewSettings.D_SharedGridFadeStartBoundsScale;
		[SerializeField] internal float sharedGridFadeStartBoundsPadding = PreviewSettings.D_SharedGridFadeStartBoundsPadding;
		[SerializeField] internal PreviewGridStyle sharedGridStyle = PreviewSettings.D_SharedGridStyle;
		[SerializeField] internal Cubemap modelSkyboxCubemap = PreviewSettings.D_ModelSkyboxCubemap;
		[SerializeField] internal Material modelSkyboxMaterial;
		[SerializeField] internal PreviewMatcapPreset modelMatcapPreset = PreviewSettings.D_ModelMatcapPreset;
		[SerializeField] internal Cubemap modelReflectionCubemap = PreviewSettings.D_ModelReflectionCubemap;
		[SerializeField] internal Color modelAmbientLightColor = PreviewSettings.D_ModelAmbientLightColor;
		[SerializeField] internal bool modelSunLightEnabled = PreviewSettings.D_ModelSunLightEnabled;
		[SerializeField] internal Color modelSunLightColor = PreviewSettings.D_ModelSunLightColor;
		[SerializeField] internal float modelSunLightIntensity = PreviewSettings.D_ModelSunLightIntensity;
		[SerializeField] internal float modelSunLightShadowStrength = PreviewSettings.D_ModelSunLightShadowStrength;
		[SerializeField] internal Vector2 modelSunLightRotation = PreviewSettings.D_ModelSunLightRotation;
		[SerializeField] internal bool modelKeyLightEnabled = PreviewSettings.D_ModelKeyLightEnabled;
		[SerializeField] internal float modelKeyLightIntensity = PreviewSettings.D_ModelKeyLightIntensity;
		[SerializeField] internal Vector2 modelKeyLightRotation = PreviewSettings.D_ModelKeyLightRotation;
		[SerializeField] internal bool modelFillLightEnabled = PreviewSettings.D_ModelFillLightEnabled;
		[SerializeField] internal float modelFillLightIntensity = PreviewSettings.D_ModelFillLightIntensity;
		[SerializeField] internal Vector2 modelFillLightRotation = PreviewSettings.D_ModelFillLightRotation;
		[SerializeField] internal bool modelRimLightEnabled = PreviewSettings.D_ModelRimLightEnabled;
		[SerializeField] internal float modelRimLightIntensity = PreviewSettings.D_ModelRimLightIntensity;
		[SerializeField] internal Vector2 modelRimLightRotation = PreviewSettings.D_ModelRimLightRotation;
		[SerializeField] internal Color modelRimLightColor = PreviewSettings.D_ModelRimLightColor;
		[SerializeField] internal bool enableDiagnostics = PreviewSettings.D_EnableDiagnostics;

		internal void SaveStorage()
		{
			ProjectSettingsAssetUtility.Save(this);
		}

		internal void ResetToDefaults()
		{
			enabled = PreviewSettings.D_Enabled;
			active = PreviewSettings.D_Active;
			refreshFps = PreviewSettings.D_RefreshFps;
			orbitSmoothing = PreviewSettings.D_OrbitSmoothing;
			panSmoothing = PreviewSettings.D_PanSmoothing;
			motionPadding = PreviewSettings.D_MotionPadding;
			motionRadius = PreviewSettings.D_MotionRadius;
			motionSpeed = PreviewSettings.D_MotionSpeed;
			backgroundColor = PreviewSettings.D_BackgroundColor;
			toolbarColorPreset = PreviewSettings.D_ToolbarColorPreset;
			toolbarHeight = PreviewSettings.D_ToolbarHeight;
			modelPreviewActive = PreviewSettings.D_ModelPreviewActive;
			spritePrefabPreviewActive = PreviewSettings.D_SpritePrefabPreviewActive;
			modelImporterPreviewActive = PreviewSettings.D_ModelImporterPreviewActive;
			modelDefaultTurntableEnabled = PreviewSettings.D_ModelDefaultTurntableEnabled;
			showStatsEnabled = PreviewSettings.D_ShowStatsEnabled;
			sharedBoundsRulerDefaultEnabled = PreviewSettings.D_SharedBoundsRulerDefaultEnabled;
			modelDefaultLightRotationGizmosEnabled = PreviewSettings.D_ModelDefaultLightRotationGizmosEnabled;
			modelDefaultSkyboxEnabled = PreviewSettings.D_ModelDefaultSkyboxEnabled;
			sharedGridDefaultEnabled = PreviewSettings.D_SharedGridDefaultEnabled;
			sharedGridAxisTextDefaultEnabled = PreviewSettings.D_SharedGridAxisTextDefaultEnabled;
			sharedGridHalfSize = PreviewSettings.D_SharedGridHalfSize;
			sharedGridStep = PreviewSettings.D_SharedGridStep;
			sharedGridAlpha = PreviewSettings.D_SharedGridAlpha;
			sharedGridFadeStartBoundsScale = PreviewSettings.D_SharedGridFadeStartBoundsScale;
			sharedGridFadeStartBoundsPadding = PreviewSettings.D_SharedGridFadeStartBoundsPadding;
			sharedGridStyle = PreviewSettings.D_SharedGridStyle;
			modelSkyboxCubemap = PreviewSettings.D_ModelSkyboxCubemap;
			modelSkyboxMaterial = null;
			modelMatcapPreset = PreviewSettings.D_ModelMatcapPreset;
			modelReflectionCubemap = PreviewSettings.D_ModelReflectionCubemap;
			modelAmbientLightColor = PreviewSettings.D_ModelAmbientLightColor;
			modelSunLightEnabled = PreviewSettings.D_ModelSunLightEnabled;
			modelSunLightColor = PreviewSettings.D_ModelSunLightColor;
			modelSunLightIntensity = PreviewSettings.D_ModelSunLightIntensity;
			modelSunLightShadowStrength = PreviewSettings.D_ModelSunLightShadowStrength;
			modelSunLightRotation = PreviewSettings.D_ModelSunLightRotation;
			modelKeyLightEnabled = PreviewSettings.D_ModelKeyLightEnabled;
			modelKeyLightIntensity = PreviewSettings.D_ModelKeyLightIntensity;
			modelKeyLightRotation = PreviewSettings.D_ModelKeyLightRotation;
			modelFillLightEnabled = PreviewSettings.D_ModelFillLightEnabled;
			modelFillLightIntensity = PreviewSettings.D_ModelFillLightIntensity;
			modelFillLightRotation = PreviewSettings.D_ModelFillLightRotation;
			modelRimLightEnabled = PreviewSettings.D_ModelRimLightEnabled;
			modelRimLightIntensity = PreviewSettings.D_ModelRimLightIntensity;
			modelRimLightRotation = PreviewSettings.D_ModelRimLightRotation;
			modelRimLightColor = PreviewSettings.D_ModelRimLightColor;
			enableDiagnostics = PreviewSettings.D_EnableDiagnostics;
		}

		private void ApplySerializedSettings(string settingsPath)
		{
			bool hasEnabled = ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(enabled), out bool boolValue);
			if (hasEnabled)
				enabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(active), out boolValue))
			{
				active = boolValue;
				if (!hasEnabled)
					enabled = boolValue;
			}
			if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(refreshFps), out int intValue))
				refreshFps = intValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(orbitSmoothing), out float floatValue))
				orbitSmoothing = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(panSmoothing), out floatValue))
				panSmoothing = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(motionPadding), out floatValue))
				motionPadding = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(motionRadius), out floatValue))
				motionRadius = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(motionSpeed), out floatValue))
				motionSpeed = floatValue;
			if (ProjectSettingsAssetUtility.TryReadColor(settingsPath, nameof(backgroundColor), out Color colorValue))
				backgroundColor = colorValue;
			if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(toolbarColorPreset), out intValue))
				toolbarColorPreset = (PreviewToolbarColorPreset) intValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(toolbarHeight), out floatValue))
				toolbarHeight = floatValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelPreviewActive), out boolValue))
				modelPreviewActive = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(spritePrefabPreviewActive), out boolValue))
				spritePrefabPreviewActive = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelImporterPreviewActive), out boolValue))
				modelImporterPreviewActive = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelDefaultTurntableEnabled), out boolValue))
				modelDefaultTurntableEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(showStatsEnabled), out boolValue))
				showStatsEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(sharedBoundsRulerDefaultEnabled), out boolValue))
				sharedBoundsRulerDefaultEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelDefaultLightRotationGizmosEnabled), out boolValue))
				modelDefaultLightRotationGizmosEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelDefaultSkyboxEnabled), out boolValue))
				modelDefaultSkyboxEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(sharedGridDefaultEnabled), out boolValue))
				sharedGridDefaultEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(sharedGridAxisTextDefaultEnabled), out boolValue))
				sharedGridAxisTextDefaultEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(sharedGridHalfSize), out floatValue))
				sharedGridHalfSize = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(sharedGridStep), out floatValue))
				sharedGridStep = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(sharedGridAlpha), out floatValue))
				sharedGridAlpha = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(sharedGridFadeStartBoundsScale), out floatValue))
				sharedGridFadeStartBoundsScale = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(sharedGridFadeStartBoundsPadding), out floatValue))
				sharedGridFadeStartBoundsPadding = floatValue;
			if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(sharedGridStyle), out intValue))
				sharedGridStyle = (PreviewGridStyle) intValue;
			if (ProjectSettingsAssetUtility.TryReadObject(settingsPath, nameof(modelSkyboxCubemap), out Cubemap cubemapValue))
				modelSkyboxCubemap = cubemapValue;
			if (ProjectSettingsAssetUtility.TryReadObject(settingsPath, nameof(modelSkyboxMaterial), out Material materialValue))
				modelSkyboxMaterial = materialValue;
			if (ProjectSettingsAssetUtility.TryReadInt(settingsPath, nameof(modelMatcapPreset), out intValue))
				modelMatcapPreset = (PreviewMatcapPreset) intValue;
			if (ProjectSettingsAssetUtility.TryReadObject(settingsPath, nameof(modelReflectionCubemap), out cubemapValue))
				modelReflectionCubemap = cubemapValue;
			if (ProjectSettingsAssetUtility.TryReadColor(settingsPath, nameof(modelAmbientLightColor), out colorValue))
				modelAmbientLightColor = colorValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelSunLightEnabled), out boolValue))
				modelSunLightEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadColor(settingsPath, nameof(modelSunLightColor), out colorValue))
				modelSunLightColor = colorValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(modelSunLightIntensity), out floatValue))
				modelSunLightIntensity = floatValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(modelSunLightShadowStrength), out floatValue))
				modelSunLightShadowStrength = floatValue;
			if (ProjectSettingsAssetUtility.TryReadVector2(settingsPath, nameof(modelSunLightRotation), out Vector2 vectorValue))
				modelSunLightRotation = vectorValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelKeyLightEnabled), out boolValue))
				modelKeyLightEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(modelKeyLightIntensity), out floatValue))
				modelKeyLightIntensity = floatValue;
			if (ProjectSettingsAssetUtility.TryReadVector2(settingsPath, nameof(modelKeyLightRotation), out vectorValue))
				modelKeyLightRotation = vectorValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelFillLightEnabled), out boolValue))
				modelFillLightEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(modelFillLightIntensity), out floatValue))
				modelFillLightIntensity = floatValue;
			if (ProjectSettingsAssetUtility.TryReadVector2(settingsPath, nameof(modelFillLightRotation), out vectorValue))
				modelFillLightRotation = vectorValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(modelRimLightEnabled), out boolValue))
				modelRimLightEnabled = boolValue;
			if (ProjectSettingsAssetUtility.TryReadFloat(settingsPath, nameof(modelRimLightIntensity), out floatValue))
				modelRimLightIntensity = floatValue;
			if (ProjectSettingsAssetUtility.TryReadVector2(settingsPath, nameof(modelRimLightRotation), out vectorValue))
				modelRimLightRotation = vectorValue;
			if (ProjectSettingsAssetUtility.TryReadColor(settingsPath, nameof(modelRimLightColor), out colorValue))
				modelRimLightColor = colorValue;
			if (ProjectSettingsAssetUtility.TryReadBool(settingsPath, nameof(enableDiagnostics), out boolValue))
				enableDiagnostics = boolValue;
		}
	}
}
