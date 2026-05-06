using System;
using System.IO;
using UnityEditor;
using UnityEngine;
// Stores preview configuration and renders the settings UI that controls particle and model preview behavior in the editor.

namespace ParticleThumbnailAndPreview.Editor
{
	[FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
	internal sealed class PreviewSettingsStorage : ScriptableSingleton<PreviewSettingsStorage>
	{
		private const string SettingsPath = "ProjectSettings/ParticleThumbnailAndPreview/ParticlePreviewSettings.asset";

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
			[SerializeField] internal PreviewModeOverride modelPreviewMode = PreviewSettings.D_ModelPreviewMode;
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
			string directory = Path.GetDirectoryName(SettingsPath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			Save(true);
		}

		internal void ResetToDefaults()
		{
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
			modelPreviewMode = PreviewSettings.D_ModelPreviewMode;
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
	}

	internal static class PreviewSettings
	{
		public const bool D_Active = true;
		public const int D_RefreshFps = 45;
		public const int MinRefreshFps = 15;
		public const int MaxRefreshFps = 60;
		public const float D_OrbitSmoothing = 20f;
		public const float MinOrbitSmoothing = 1f;
		public const float MaxOrbitSmoothing = 20f;
		public const float D_PanSmoothing = 20f;
		public const float MinPanSmoothing = 1f;
		public const float MaxPanSmoothing = 20f;
		public const float D_MotionPadding = 0f;
		public const float MinMotionPadding = 0f;
		public const float MaxMotionPadding = 3f;
		public const float D_MotionRadius = 3f;
		public const float MinMotionRadius = 0.1f;
		public const float MaxMotionRadius = 50f;
		public const float D_MotionSpeed = 30f;
		public const float MinMotionSpeed = 0.1f;
		public const float MaxMotionSpeed = 200f;
		public static readonly Color D_BackgroundColor = new Color(0.11f, 0.11f, 0.11f, 1f);
		public const PreviewToolbarColorPreset D_ToolbarColorPreset = PreviewToolbarColorPreset.UnityBlue;
		public const float D_ToolbarHeight = 35f;
		public const float MinToolbarHeight = 16f;
		public const float MaxToolbarHeight = 40f;
		public const bool D_ModelPreviewActive = true;
		public const bool D_SpritePrefabPreviewActive = true;
		public const bool D_ModelImporterPreviewActive = true;
			public const PreviewModeOverride D_ModelPreviewMode = PreviewModeOverride.Auto;
			public const bool D_ModelDefaultTurntableEnabled = true;
			public const bool D_ShowStatsEnabled = true;
			public const bool D_SharedBoundsRulerDefaultEnabled = false;
			public const bool D_ModelDefaultLightRotationGizmosEnabled = true;
			public const bool D_ModelDefaultSkyboxEnabled = true;
			public const bool D_SharedGridDefaultEnabled = true;
			public const bool D_SharedGridAxisTextDefaultEnabled = true;
				public const float D_SharedGridHalfSize = 6f;
				public const float D_SharedGridStep = 0.5f;
				public const float D_SharedGridAlpha = 0.15f;
				public const float D_SharedGridFadeStartBoundsScale = 1f;
				public const float D_SharedGridFadeStartBoundsPadding = 7.16f;
				public const PreviewGridStyle D_SharedGridStyle = PreviewGridStyle.Stylized;
				public const float MinSharedGridHalfSize = 0.5f;
				public const float MaxSharedGridHalfSize = 200f;
				public const float MinSharedGridStep = 0.05f;
				public const float MaxSharedGridStep = 5f;
				public const float MinSharedGridAlpha = 0f;
				public const float MaxSharedGridAlpha = 1f;
				public const float MinSharedGridFadeStartBoundsScale = 1f;
				public const float MaxSharedGridFadeStartBoundsScale = 3f;
				public const float MinSharedGridFadeStartBoundsPadding = 0f;
				public const float MaxSharedGridFadeStartBoundsPadding = 50f;
			public const Cubemap D_ModelSkyboxCubemap = null;
			public const Cubemap D_ModelReflectionCubemap = null;
			public static readonly Color D_ModelAmbientLightColor = new Color(0.6132076f, 0.6045301f, 0.6045301f, 1f);
			public const bool D_ModelSunLightEnabled = true;
		public static readonly Color D_ModelSunLightColor = new Color(1f, 0.9604328f, 0.8915094f, 1f);
		public const float D_ModelSunLightIntensity = 1f;
		public const float D_ModelSunLightShadowStrength = 1f;
		public static readonly Vector2 D_ModelSunLightRotation = new Vector2(10.8f, 66.8f);
		public const bool D_ModelKeyLightEnabled = true;
		public const float D_ModelKeyLightIntensity = 0.8f;
		public static readonly Vector2 D_ModelKeyLightRotation = new Vector2(35f, 35f);
		public const bool D_ModelFillLightEnabled = false;
		public const float D_ModelFillLightIntensity = 0.3f;
		public static readonly Vector2 D_ModelFillLightRotation = new Vector2(200f, -30f);
		public const bool D_ModelRimLightEnabled = false;
		public const float D_ModelRimLightIntensity = 0.5f;
		public static readonly Vector2 D_ModelRimLightRotation = new Vector2(160f, 0f);
		public static readonly Color D_ModelRimLightColor = Color.white;
		public const float MinModelLightIntensity = 0f;
		public const float MaxModelLightIntensity = 8f;
		public const float MinModelShadowStrength = 0f;
		public const float MaxModelShadowStrength = 1f;
		public const bool D_EnableDiagnostics = false;

		public static event Action SettingsChanged;

		private static PreviewSettingsStorage Storage => PreviewSettingsStorage.instance;

		public static bool ParticlePrefabPreviewActive => Storage.active;
		public static bool Active => ParticlePrefabPreviewActive;
		public static bool AnyPrefabCustomPreviewActive => ParticlePrefabPreviewActive || ModelPreviewActive || SpritePrefabPreviewActive;
		public static bool Autoplay => true;
		public static int RefreshFps => Mathf.Clamp(Storage.refreshFps, MinRefreshFps, MaxRefreshFps);

		public static float OrbitSmoothing => Storage.orbitSmoothing <= 0f
			? D_OrbitSmoothing
			: Mathf.Clamp(Storage.orbitSmoothing, MinOrbitSmoothing, MaxOrbitSmoothing);

		public static float PanSmoothing => Storage.panSmoothing <= 0f
			? D_PanSmoothing
			: Mathf.Clamp(Storage.panSmoothing, MinPanSmoothing, MaxPanSmoothing);

		public static float MotionPadding => Mathf.Clamp(Storage.motionPadding, MinMotionPadding, MaxMotionPadding);
		public static float MotionRadius => Mathf.Clamp(Storage.motionRadius, MinMotionRadius, MaxMotionRadius);
		public static float MotionSpeed => Mathf.Clamp(Storage.motionSpeed, MinMotionSpeed, MaxMotionSpeed);

			public static Color BackgroundColor => Storage.backgroundColor;
			public static PreviewToolbarColorPreset ToolbarColorPreset => Enum.IsDefined(typeof(PreviewToolbarColorPreset), Storage.toolbarColorPreset)
				? Storage.toolbarColorPreset
				: D_ToolbarColorPreset;
			public static float ToolbarHeight => Storage.toolbarHeight <= 0f
				? D_ToolbarHeight
				: Mathf.Clamp(Storage.toolbarHeight, MinToolbarHeight, MaxToolbarHeight);
			public static bool ModelPreviewActive => Storage.modelPreviewActive;
			public static bool SpritePrefabPreviewActive => Storage.spritePrefabPreviewActive;
			public static bool ThreeDAssetPreviewActive => Storage.modelImporterPreviewActive;
			public static bool ModelImporterPreviewActive => ThreeDAssetPreviewActive;
			public static PreviewModeOverride ModelPreviewMode => Storage.modelPreviewMode;
			public static bool ModelDefaultTurntableEnabled => Storage.modelDefaultTurntableEnabled;
			public static bool ShowStatsEnabled => Storage.showStatsEnabled;
			public static bool SharedBoundsRulerDefaultEnabled => Storage.sharedBoundsRulerDefaultEnabled;
			public static bool ModelDefaultLightRotationGizmosEnabled => Storage.modelDefaultLightRotationGizmosEnabled;
			public static bool ModelDefaultSkyboxEnabled => Storage.modelDefaultSkyboxEnabled;
			public static bool SharedGridDefaultEnabled => Storage.sharedGridDefaultEnabled;
			public static bool SharedGridAxisTextDefaultEnabled => Storage.sharedGridAxisTextDefaultEnabled;
				public static float SharedGridHalfSize => Mathf.Clamp(Storage.sharedGridHalfSize, MinSharedGridHalfSize, MaxSharedGridHalfSize);
				public static float SharedGridStep => Mathf.Clamp(Storage.sharedGridStep, MinSharedGridStep, MaxSharedGridStep);
				public static float SharedGridAlpha => Mathf.Clamp(Storage.sharedGridAlpha, MinSharedGridAlpha, MaxSharedGridAlpha);
				public static float SharedGridFadeStartBoundsScale => Mathf.Clamp(Storage.sharedGridFadeStartBoundsScale, MinSharedGridFadeStartBoundsScale, MaxSharedGridFadeStartBoundsScale);
				public static float SharedGridFadeStartBoundsPadding => Mathf.Clamp(Storage.sharedGridFadeStartBoundsPadding, MinSharedGridFadeStartBoundsPadding, MaxSharedGridFadeStartBoundsPadding);
				public static PreviewGridStyle SharedGridStyle => Enum.IsDefined(typeof(PreviewGridStyle), Storage.sharedGridStyle)
					? Storage.sharedGridStyle
					: D_SharedGridStyle;
			public static PreviewGridProfile SharedGridProfile => new PreviewGridProfile(
				SharedGridDefaultEnabled,
				SharedGridHalfSize,
				SharedGridStep,
				SharedGridAlpha,
				SharedGridStyle);
			public static Cubemap ModelSkyboxCubemap => Storage.modelSkyboxCubemap;

			public static Material ModelSkyboxMaterial
			{
				get
				{
					Cubemap resolvedCubemap = Storage.modelSkyboxCubemap ?? PreviewSkyboxAssets.GetDefaultSkyboxCubemap();
					return Storage.modelSkyboxMaterial != null
						? Storage.modelSkyboxMaterial
						: PreviewSkyboxAssets.GetDefaultSkyboxMaterialForCubemap(resolvedCubemap);
				}
			}

			public static Cubemap ModelReflectionCubemap
			{
				get
				{
					return Storage.modelReflectionCubemap;
				}
			}

			public static Color ModelAmbientLightColor => Storage.modelAmbientLightColor;
			public static bool ModelSunLightEnabled => Storage.modelSunLightEnabled;
			public static Color ModelSunLightColor => Storage.modelSunLightColor;
		public static float ModelSunLightIntensity => Mathf.Clamp(Storage.modelSunLightIntensity, MinModelLightIntensity, MaxModelLightIntensity);
		public static float ModelSunLightShadowStrength => Mathf.Clamp(Storage.modelSunLightShadowStrength, MinModelShadowStrength, MaxModelShadowStrength);
		public static Vector2 ModelSunLightRotation => Storage.modelSunLightRotation;
		public static bool ModelKeyLightEnabled => Storage.modelKeyLightEnabled;
		public static float ModelKeyLightIntensity => Mathf.Clamp(Storage.modelKeyLightIntensity, MinModelLightIntensity, MaxModelLightIntensity);
		public static Vector2 ModelKeyLightRotation => Storage.modelKeyLightRotation;
		public static bool ModelFillLightEnabled => Storage.modelFillLightEnabled;
		public static float ModelFillLightIntensity => Mathf.Clamp(Storage.modelFillLightIntensity, MinModelLightIntensity, MaxModelLightIntensity);
		public static Vector2 ModelFillLightRotation => Storage.modelFillLightRotation;
		public static bool ModelRimLightEnabled => Storage.modelRimLightEnabled;
		public static float ModelRimLightIntensity => Mathf.Clamp(Storage.modelRimLightIntensity, MinModelLightIntensity, MaxModelLightIntensity);
		public static Vector2 ModelRimLightRotation => Storage.modelRimLightRotation;
		public static Color ModelRimLightColor => Storage.modelRimLightColor;
		public static bool EnableDiagnostics => Storage.enableDiagnostics;

		internal static float ClampModelLightIntensityForTests(float value)
		{
			return Mathf.Clamp(value, MinModelLightIntensity, MaxModelLightIntensity);
		}

		internal static void NotifyChanged()
		{
			SettingsChanged?.Invoke();
		}

		}

	internal static class PreviewSettingsProvider
	{
		private const string SettingsPath = "Project/Particle Thumbnail & Preview/Prefab Preview";

		#region Tab Navigation
		private enum SettingsTab
		{
			Common = 0,
			Lighting = 1,
			DefaultEnabledState = 2,
			Grid = 3,
		}

		private static readonly string[] MainTabTitles =
		{
			"Common",
			"Lighting",
			"Default Enabled State",
			"Grid",
		};
		#endregion

		private static Vector2 SettingsScroll;
		private static SettingsTab SelectedTab = SettingsTab.Common;
		private static GUIStyle CenteredSectionHeaderStyle;
		private static readonly string[] ParticlePreviewSystemIcons =
		{
			"d_ParticleSystem Icon",
			"ParticleSystem Icon",
		};
		private static readonly string[] PrefabPreviewSystemIcons =
		{
			"d_Prefab Icon",
			"Prefab Icon",
		};
		private static readonly string[] SpritePrefabPreviewSystemIcons =
		{
			"d_SpriteRenderer Icon",
			"SpriteRenderer Icon",
		};
		private static readonly string[] ModelImporterPreviewSystemIcons =
		{
			"d_Mesh Icon",
			"Mesh Icon",
		};

		#region Serialized Property Names
		private const string ActivePropertyName = "active";
		private const string RefreshFpsPropertyName = "refreshFps";
		private const string OrbitSmoothingPropertyName = "orbitSmoothing";
		private const string PanSmoothingPropertyName = "panSmoothing";
		private const string BackgroundColorPropertyName = "backgroundColor";
		private const string ToolbarColorPresetPropertyName = "toolbarColorPreset";
		private const string ToolbarHeightPropertyName = "toolbarHeight";
		private const string ModelPreviewActivePropertyName = "modelPreviewActive";
		private const string SpritePrefabPreviewActivePropertyName = "spritePrefabPreviewActive";
		private const string ModelImporterPreviewActivePropertyName = "modelImporterPreviewActive";
		private const string ModelPreviewModePropertyName = "modelPreviewMode";
		private const string ModelDefaultTurntableEnabledPropertyName = "modelDefaultTurntableEnabled";
		private const string ShowStatsEnabledPropertyName = "showStatsEnabled";
		private const string SharedBoundsRulerDefaultEnabledPropertyName = "sharedBoundsRulerDefaultEnabled";
		private const string ModelDefaultLightRotationGizmosEnabledPropertyName = "modelDefaultLightRotationGizmosEnabled";
		private const string ModelDefaultSkyboxEnabledPropertyName = "modelDefaultSkyboxEnabled";
		private const string SharedGridDefaultEnabledPropertyName = "sharedGridDefaultEnabled";
		private const string SharedGridAxisTextDefaultEnabledPropertyName = "sharedGridAxisTextDefaultEnabled";
		private const string SharedGridStylePropertyName = "sharedGridStyle";
		private const string SharedGridHalfSizePropertyName = "sharedGridHalfSize";
		private const string SharedGridStepPropertyName = "sharedGridStep";
		private const string SharedGridAlphaPropertyName = "sharedGridAlpha";
		private const string SharedGridFadeStartBoundsScalePropertyName = "sharedGridFadeStartBoundsScale";
		private const string SharedGridFadeStartBoundsPaddingPropertyName = "sharedGridFadeStartBoundsPadding";
		private const string ModelSkyboxMaterialPropertyName = "modelSkyboxMaterial";
		private const string ModelAmbientLightColorPropertyName = "modelAmbientLightColor";
		private const string ModelSunLightEnabledPropertyName = "modelSunLightEnabled";
		private const string ModelSunLightColorPropertyName = "modelSunLightColor";
		private const string ModelSunLightIntensityPropertyName = "modelSunLightIntensity";
		private const string ModelSunLightShadowStrengthPropertyName = "modelSunLightShadowStrength";
		private const string ModelSunLightRotationPropertyName = "modelSunLightRotation";
		private const string ModelKeyLightEnabledPropertyName = "modelKeyLightEnabled";
		private const string ModelKeyLightIntensityPropertyName = "modelKeyLightIntensity";
		private const string ModelKeyLightRotationPropertyName = "modelKeyLightRotation";
		private const string ModelFillLightEnabledPropertyName = "modelFillLightEnabled";
		private const string ModelFillLightIntensityPropertyName = "modelFillLightIntensity";
		private const string ModelFillLightRotationPropertyName = "modelFillLightRotation";
		private const string ModelRimLightEnabledPropertyName = "modelRimLightEnabled";
		private const string ModelRimLightIntensityPropertyName = "modelRimLightIntensity";
		private const string ModelRimLightRotationPropertyName = "modelRimLightRotation";
		private const string ModelRimLightColorPropertyName = "modelRimLightColor";
		private const string EnableDiagnosticsPropertyName = "enableDiagnostics";
		#endregion

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
		{
			return new SettingsProvider(SettingsPath, SettingsScope.Project)
			{
				label = "Prefab Preview",
				guiHandler = _ => DrawGui(),
				keywords = new System.Collections.Generic.HashSet<string>
				{
					"particle",
					"model",
					"preview",
					"prefab",
					"harmony",
				},
			};
		}

		internal static void OpenSettings()
		{
			SettingsService.OpenProjectSettings(SettingsPath);
		}

		#region Main GUI
		private static void DrawGui()
		{
			PreviewSettingsStorage storage = PreviewSettingsStorage.instance;
			SerializedObject serializedObject = ProjectSettingsUndoUtility.CreateSerializedObject(storage, () => SaveAndNotify(storage));

			SettingsScroll = EditorGUILayout.BeginScrollView(SettingsScroll, false, false);
			DrawMainTabs();
			EditorGUILayout.Space(6f);
			DrawSelectedTab(serializedObject);
			// DrawSectionCard("Motion Assist", () =>
			// {
			// 	storage.motionPadding = EditorGUILayout.Slider(
			// 		new GUIContent("Motion Padding", "Extra framing margin when motion simulation is used."),
			// 		storage.motionPadding,
			// 		PreviewSettings.MinMotionPadding,
			// 		PreviewSettings.MaxMotionPadding);
			// 	storage.motionRadius = EditorGUILayout.Slider(
			// 		new GUIContent("Motion Radius", "Radius of deterministic motion path for world-space movement previews."),
			// 		storage.motionRadius,
			// 		PreviewSettings.MinMotionRadius,
			// 		PreviewSettings.MaxMotionRadius);
			// 	storage.motionSpeed = EditorGUILayout.Slider(
			// 		new GUIContent("Motion Speed", "Speed of deterministic motion path during preview simulation."),
			// 		storage.motionSpeed,
			// 		PreviewSettings.MinMotionSpeed,
			// 		PreviewSettings.MaxMotionSpeed);
			// });

			EditorGUILayout.EndScrollView();
			EditorGUILayout.Space(8f);
			DrawBottomActionsPanel(storage);
			ProjectSettingsUndoUtility.ApplyModifiedProperties(serializedObject);
		}
		#endregion

		#region Tab Content
		private static void DrawMainTabs()
		{
			int selected = GUILayout.Toolbar((int) SelectedTab, MainTabTitles);
			if (selected != (int) SelectedTab)
			{
				SelectedTab = (SettingsTab) selected;
				GUI.FocusControl(null);
			}
		}

		private static void DrawSelectedTab(SerializedObject serializedObject)
		{
			switch (SelectedTab)
			{
				case SettingsTab.Common:
					DrawCommonTab(serializedObject);
					break;
				case SettingsTab.Lighting:
					DrawLightingTab(serializedObject);
					break;
				case SettingsTab.DefaultEnabledState:
					DrawDefaultEnabledStateTab(serializedObject);
					break;
				case SettingsTab.Grid:
					DrawGridTab(serializedObject);
					break;
				default:
					DrawCommonTab(serializedObject);
					break;
			}
		}

		private static void DrawCommonTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Custom Preview Systems", () =>
			{
				DrawIconToggleLeft(
					serializedObject.FindProperty(ActivePropertyName),
					"Draw Particle Prefab Custom Preview",
					"Enable custom preview rendering for particle prefabs.",
					ParticlePreviewSystemIcons);
				DrawIconToggleLeft(
					serializedObject.FindProperty(ModelPreviewActivePropertyName),
					"Draw Normal Prefab Custom Preview",
					"Enable custom preview rendering for non-particle prefabs that use mesh/skinned renderers.",
					PrefabPreviewSystemIcons);
				DrawIconToggleLeft(
					serializedObject.FindProperty(SpritePrefabPreviewActivePropertyName),
					"Draw Sprite Prefab Custom Preview",
					"Enable custom preview rendering for sprite-based world-space prefabs.",
					SpritePrefabPreviewSystemIcons);
				DrawIconToggleLeft(
					serializedObject.FindProperty(ModelImporterPreviewActivePropertyName),
					"Draw 3D File (FBX/BLEND) Asset Custom Preview",
					"Enable custom preview rendering for imported 3D model assets when the Model tab is active.",
					ModelImporterPreviewSystemIcons);
			});

			DrawSectionCard("Playback", () =>
			{
				DrawIntSlider(serializedObject.FindProperty(RefreshFpsPropertyName), "Preview FPS", "Preview update rate while the preview is visible.", PreviewSettings.MinRefreshFps, PreviewSettings.MaxRefreshFps);
			});

			DrawSectionCard("Color", () =>
			{
				DrawColorField(serializedObject.FindProperty(BackgroundColorPropertyName), "Background Color", "Background color behind custom prefab preview rendering.");
				SerializedProperty toolbarColorPresetProperty = serializedObject.FindProperty(ToolbarColorPresetPropertyName);
				if (!Enum.IsDefined(typeof(PreviewToolbarColorPreset), toolbarColorPresetProperty.intValue))
					toolbarColorPresetProperty.intValue = (int) PreviewSettings.D_ToolbarColorPreset;
				DrawEnumPopup(toolbarColorPresetProperty, typeof(PreviewToolbarColorPreset), "Toolbar Color Preset", "Temporary active-toolbar color preset while we evaluate final branding.");
				DrawFloatSliderWithFallback(serializedObject.FindProperty(ToolbarHeightPropertyName), "Toolbar Height", "Shared height for particle and model preview toolbars. Button and scrubber sizes scale automatically.", PreviewSettings.MinToolbarHeight, PreviewSettings.MaxToolbarHeight, PreviewSettings.D_ToolbarHeight);
			});

			DrawSectionCard("Interaction", () =>
			{
				DrawFloatSliderWithFallback(serializedObject.FindProperty(OrbitSmoothingPropertyName), "Orbit Smoothing", "Smoothing strength for orbit rotation input. Higher values feel softer.", PreviewSettings.MinOrbitSmoothing, PreviewSettings.MaxOrbitSmoothing, PreviewSettings.D_OrbitSmoothing);
				DrawFloatSliderWithFallback(serializedObject.FindProperty(PanSmoothingPropertyName), "Pan Smoothing", "Smoothing strength for panning input. Higher values feel softer.", PreviewSettings.MinPanSmoothing, PreviewSettings.MaxPanSmoothing, PreviewSettings.D_PanSmoothing);
			});
		}

		private static void DrawLightingTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Environment", () =>
			{
				SerializedProperty modelSkyboxMaterialProperty = serializedObject.FindProperty(ModelSkyboxMaterialPropertyName);
				Material displayedMaterial = modelSkyboxMaterialProperty.objectReferenceValue as Material;
				if (displayedMaterial == null)
					displayedMaterial = PreviewSkyboxAssets.TryLoadDefaultSkyboxMaterial();

				UnityEngine.Object selectedMaterial = EditorGUILayout.ObjectField(
					new GUIContent("Skybox Material", "Material used by model preview skybox."),
					displayedMaterial,
					typeof(Material),
					false);
				if (selectedMaterial != modelSkyboxMaterialProperty.objectReferenceValue)
					modelSkyboxMaterialProperty.objectReferenceValue = selectedMaterial;

				DrawHdrColorField(serializedObject.FindProperty(ModelAmbientLightColorPropertyName), "Ambient (HDR)", "Shared ambient lighting color used by model and particle preview when lights are enabled.");
			});

			DrawSectionCard("Directional Light", () =>
			{
				DrawToggle(serializedObject.FindProperty(ModelSunLightEnabledPropertyName), "Enabled", "Enable sunlight in model preview.");
				DrawColorField(serializedObject.FindProperty(ModelSunLightColorPropertyName), "Color", "Color of the sunlight directional light.");
				DrawFloatSlider(serializedObject.FindProperty(ModelSunLightIntensityPropertyName), "Intensity", "Intensity of the sunlight directional light.", PreviewSettings.MinModelLightIntensity, PreviewSettings.MaxModelLightIntensity);
				DrawFloatSlider(serializedObject.FindProperty(ModelSunLightShadowStrengthPropertyName), "Shadow Strength", "How dark sunlight shadows appear.", PreviewSettings.MinModelShadowStrength, PreviewSettings.MaxModelShadowStrength);
				DrawVector2Field(serializedObject.FindProperty(ModelSunLightRotationPropertyName), "Rotation", "Sunlight rotation as Yaw/Pitch in degrees.");
			});

			DrawSectionCard("Key Light", () =>
			{
				DrawToggle(serializedObject.FindProperty(ModelKeyLightEnabledPropertyName), "Enabled", "Enable key light in model preview.");
				DrawFloatSlider(serializedObject.FindProperty(ModelKeyLightIntensityPropertyName), "Intensity", "Key directional light intensity.", PreviewSettings.MinModelLightIntensity, PreviewSettings.MaxModelLightIntensity);
				DrawVector2Field(serializedObject.FindProperty(ModelKeyLightRotationPropertyName), "Rotation", "Key light rotation as Yaw/Pitch in degrees.");
			});

			DrawSectionCard("Fill Light", () =>
			{
				DrawToggle(serializedObject.FindProperty(ModelFillLightEnabledPropertyName), "Enabled", "Enable fill light in model preview.");
				DrawFloatSlider(serializedObject.FindProperty(ModelFillLightIntensityPropertyName), "Intensity", "Fill directional light intensity.", PreviewSettings.MinModelLightIntensity, PreviewSettings.MaxModelLightIntensity);
				DrawVector2Field(serializedObject.FindProperty(ModelFillLightRotationPropertyName), "Rotation", "Fill light rotation as Yaw/Pitch in degrees.");
			});

			DrawSectionCard("Rim Light", () =>
			{
				DrawToggle(serializedObject.FindProperty(ModelRimLightEnabledPropertyName), "Enabled", "Enable the optional rim light in model preview.");
				DrawFloatSlider(serializedObject.FindProperty(ModelRimLightIntensityPropertyName), "Intensity", "Rim directional light intensity.", PreviewSettings.MinModelLightIntensity, PreviewSettings.MaxModelLightIntensity);
				DrawVector2Field(serializedObject.FindProperty(ModelRimLightRotationPropertyName), "Rotation", "Rim light rotation as Yaw/Pitch in degrees.");
				DrawColorField(serializedObject.FindProperty(ModelRimLightColorPropertyName), "Color", "Rim light color.");
			});
		}

		private static void DrawDefaultEnabledStateTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Mode", () =>
			{
				DrawEnumPopup(serializedObject.FindProperty(ModelPreviewModePropertyName), typeof(PreviewModeOverride), "Mode Override", "Auto resolves to project default (2D or 3D) when a model preview session starts. 2D/3D force a mode.");
			});

			DrawSectionCard("Default enabled state", () =>
			{
				DrawToggle(serializedObject.FindProperty(ModelDefaultTurntableEnabledPropertyName), "Turntable", "Default state for the Turntable toggle.");
				DrawToggle(serializedObject.FindProperty(ShowStatsEnabledPropertyName), "Show Stats", "Show stats overlays in model, sprite, and particle preview.");
				DrawToggle(serializedObject.FindProperty(SharedBoundsRulerDefaultEnabledPropertyName), "Bounds Ruler", "Default state for the bounds ruler toggle in model and sprite preview.");
				DrawToggle(serializedObject.FindProperty(ModelDefaultLightRotationGizmosEnabledPropertyName), "Light Rotation Gizmos", "Default state for the Light Rotation Gizmos toggle.");
				DrawToggle(serializedObject.FindProperty(ModelDefaultSkyboxEnabledPropertyName), "Skybox", "Default state for the Skybox toggle.");
			});

			DrawSectionCard("Debug", () =>
			{
				DrawToggle(serializedObject.FindProperty(EnableDiagnosticsPropertyName), "Enable Diagnostics", "Write preview lifecycle diagnostics to the Unity Console.");
			});
		}

		private static void DrawGridTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Shared Grid", () =>
			{
				DrawToggle(serializedObject.FindProperty(SharedGridDefaultEnabledPropertyName), "Enabled By Default", "Initial grid toggle state for both model and particle preview sessions.");
				DrawToggle(serializedObject.FindProperty(SharedGridAxisTextDefaultEnabledPropertyName), "Axis Text Enabled By Default", "Default visibility for +X/-X/+Z/-Z grid axis text in 3D preview.");
				SerializedProperty sharedGridStyleProperty = serializedObject.FindProperty(SharedGridStylePropertyName);
				if (!Enum.IsDefined(typeof(PreviewGridStyle), sharedGridStyleProperty.intValue))
					sharedGridStyleProperty.intValue = (int) PreviewSettings.D_SharedGridStyle;
				DrawEnumPopup(sharedGridStyleProperty, typeof(PreviewGridStyle), "Style", "Visual style used for both model and particle preview grids.");
				DrawFloatSlider(serializedObject.FindProperty(SharedGridHalfSizePropertyName), "Half Size", "World-space half extent of the preview grid.", PreviewSettings.MinSharedGridHalfSize, PreviewSettings.MaxSharedGridHalfSize);
				DrawFloatSlider(serializedObject.FindProperty(SharedGridStepPropertyName), "Step", "Distance between adjacent grid lines.", PreviewSettings.MinSharedGridStep, PreviewSettings.MaxSharedGridStep);
				DrawFloatSlider(serializedObject.FindProperty(SharedGridAlphaPropertyName), "Alpha", "Overall transparency for grid lines.", PreviewSettings.MinSharedGridAlpha, PreviewSettings.MaxSharedGridAlpha);
				DrawFloatSlider(serializedObject.FindProperty(SharedGridFadeStartBoundsScalePropertyName), "Fade Start Bounds Scale", "Multiplier applied to bounds radius before grid fade starts. Values above 1 start fade outside bounds.", PreviewSettings.MinSharedGridFadeStartBoundsScale, PreviewSettings.MaxSharedGridFadeStartBoundsScale);
				DrawFloatSlider(serializedObject.FindProperty(SharedGridFadeStartBoundsPaddingPropertyName), "Fade Start Bounds Padding", "Extra world-space padding added after scaled bounds before fade starts.", PreviewSettings.MinSharedGridFadeStartBoundsPadding, PreviewSettings.MaxSharedGridFadeStartBoundsPadding);
				});
			}
		#endregion

		#region Shared UI Helpers
		private static void DrawBottomActionsPanel(PreviewSettingsStorage storage)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// DrawSectionHeader("Actions");
				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Button(new GUIContent("Reset To Defaults", "Reset all prefab preview settings to default values."), GUILayout.Height(28f)) &&
					    EditorUtility.DisplayDialog(
						    "Reset Prefab Preview Settings",
						    "Reset all prefab preview settings back to default values?",
						    "Reset",
						    "Cancel"))
					{
						ProjectSettingsUndoUtility.ResetToDefaultsWithUndo(
							storage,
							"Reset Prefab Preview Settings",
							storage.ResetToDefaults,
							() => SaveAndNotify(storage));
						GUI.FocusControl(null);
						GUIUtility.ExitGUI();
					}
				}
			}
		}

		private static void DrawSectionCard(string title, Action drawContent)
		{
			EditorGUILayout.Space(6f);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				DrawSectionHeader(title);
				drawContent?.Invoke();
			}
		}

		private static void DrawSectionHeader(string title)
		{
			if (CenteredSectionHeaderStyle == null)
			{
				CenteredSectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
				{
					alignment = TextAnchor.MiddleCenter
				};
			}

			Rect rect = EditorGUILayout.GetControlRect(false, 20f);
			EditorGUI.LabelField(rect, title, CenteredSectionHeaderStyle);

			Rect lineRect = new Rect(rect.x, rect.yMax + 1f, rect.width, 1f);
			EditorGUI.DrawRect(lineRect, new Color(0.30f, 0.30f, 0.30f, 1f));
			EditorGUILayout.Space(4f);
		}

		private static void SaveAndNotify(PreviewSettingsStorage storage)
		{
			storage.SaveStorage();
			PreviewSettings.NotifyChanged();
		}

		private static void DrawToggle(SerializedProperty property, string label, string tooltip)
		{
			bool newValue = EditorGUILayout.Toggle(new GUIContent(label, tooltip), property.boolValue);
			if (newValue != property.boolValue)
				property.boolValue = newValue;
		}

		private static void DrawColorField(SerializedProperty property, string label, string tooltip)
		{
			Color newValue = EditorGUILayout.ColorField(new GUIContent(label, tooltip), property.colorValue);
			if (newValue != property.colorValue)
				property.colorValue = newValue;
		}

		private static void DrawHdrColorField(SerializedProperty property, string label, string tooltip)
		{
			Color newValue = EditorGUILayout.ColorField(new GUIContent(label, tooltip), property.colorValue, true, true, true);
			if (newValue != property.colorValue)
				property.colorValue = newValue;
		}

		private static void DrawIntSlider(SerializedProperty property, string label, string tooltip, int minValue, int maxValue)
		{
			int newValue = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), property.intValue, minValue, maxValue);
			if (newValue != property.intValue)
				property.intValue = newValue;
		}

		private static void DrawFloatSlider(SerializedProperty property, string label, string tooltip, float minValue, float maxValue)
		{
			float newValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), property.floatValue, minValue, maxValue);
			if (!Mathf.Approximately(newValue, property.floatValue))
				property.floatValue = newValue;
		}

		private static void DrawFloatSliderWithFallback(SerializedProperty property, string label, string tooltip, float minValue, float maxValue, float defaultValue)
		{
			float currentValue = property.floatValue <= 0f ? defaultValue : property.floatValue;
			float newValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), currentValue, minValue, maxValue);
			if (!Mathf.Approximately(newValue, property.floatValue))
				property.floatValue = newValue;
		}

		private static void DrawEnumPopup(SerializedProperty property, Type enumType, string label, string tooltip)
		{
			Enum currentValue = (Enum) Enum.ToObject(enumType, property.intValue);
			Enum newValue = EditorGUILayout.EnumPopup(new GUIContent(label, tooltip), currentValue);
			int newIntValue = Convert.ToInt32(newValue);
			if (newIntValue != property.intValue)
				property.intValue = newIntValue;
		}

		private static void DrawVector2Field(SerializedProperty property, string label, string tooltip)
		{
			Vector2 newValue = EditorGUILayout.Vector2Field(new GUIContent(label, tooltip), property.vector2Value);
			if (newValue != property.vector2Value)
				property.vector2Value = newValue;
		}

		private static void DrawIconToggleLeft(SerializedProperty property, string label, string tooltip, params string[] iconNames)
		{
			GUIContent iconContent = PreviewToolbarControls.GetIconContent(string.Empty, tooltip, iconNames);
			Texture icon = iconContent != null ? iconContent.image : null;
			GUIContent content = icon != null
				? new GUIContent(label, icon, tooltip)
				: new GUIContent(label, tooltip);

			Vector2 previousIconSize = EditorGUIUtility.GetIconSize();
			Vector2 reducedIconSize = previousIconSize.sqrMagnitude > 0f
				? previousIconSize * 0.9f
				: new Vector2(14.4f, 14.4f);
			EditorGUIUtility.SetIconSize(reducedIconSize);
			bool newValue = EditorGUILayout.ToggleLeft(content, property.boolValue);
			EditorGUIUtility.SetIconSize(previousIconSize);
			if (newValue != property.boolValue)
				property.boolValue = newValue;
		}
		#endregion
	}

	internal static class PreviewSkyboxAssets
	{
		private const string SkyboxFolderPath = "Editor/Common/PreviewAssets/Skybox";
		private const string DefaultSkyboxGuid = "83fb20c8d36eb4afe9b376da509dc0d6";
		private const string DefaultReflectionGuid = "e628e60fedd134eae92c45e351f5f566";
		private const string DefaultSkyboxMaterialPath = SkyboxFolderPath + "/PreviewSkybox.mat";
		private static Material s_generatedSkyboxMaterial;
		private static int s_generatedSkyboxSourceCubemapId;

		internal static Cubemap GetDefaultSkyboxCubemap()
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(DefaultSkyboxGuid);
			if (string.IsNullOrEmpty(assetPath))
				return null;

			return AssetDatabase.LoadAssetAtPath<Cubemap>(assetPath);
		}

		internal static Cubemap GetDefaultReflectionCubemap()
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(DefaultReflectionGuid);
			if (string.IsNullOrEmpty(assetPath))
				return null;

			return AssetDatabase.LoadAssetAtPath<Cubemap>(assetPath);
		}

		internal static Material GetDefaultSkyboxMaterial()
		{
			return GetDefaultSkyboxMaterialForCubemap(GetDefaultSkyboxCubemap());
		}

		internal static Material TryLoadDefaultSkyboxMaterial()
		{
			return PreviewInstallLayout.LoadFirstAssetAtRelativePath<Material>(DefaultSkyboxMaterialPath);
		}

		internal static Material GetDefaultSkyboxMaterialForCubemap(Cubemap cubemap)
		{
			Cubemap resolvedCubemap = cubemap != null ? cubemap : GetDefaultSkyboxCubemap();
			Material defaultMaterial = TryLoadDefaultSkyboxMaterial();
			if (resolvedCubemap == null)
				return defaultMaterial;

			if (defaultMaterial != null && defaultMaterial.GetTexture("_Tex") == resolvedCubemap)
				return defaultMaterial;

			int sourceCubemapId = resolvedCubemap.GetInstanceID();
			if (s_generatedSkyboxMaterial != null && s_generatedSkyboxSourceCubemapId == sourceCubemapId)
				return s_generatedSkyboxMaterial;

			if (s_generatedSkyboxMaterial != null)
				UnityEngine.Object.DestroyImmediate(s_generatedSkyboxMaterial);

			if (defaultMaterial != null)
			{
				s_generatedSkyboxMaterial = new Material(defaultMaterial);
			}
			else
			{
				Shader shader = Shader.Find("Skybox/Cubemap");
				if (shader == null)
					return null;

				s_generatedSkyboxMaterial = new Material(shader);
			}

			s_generatedSkyboxMaterial.name = "PreviewSkybox (Generated)";
			s_generatedSkyboxMaterial.hideFlags = HideFlags.HideAndDontSave;
			s_generatedSkyboxMaterial.SetTexture("_Tex", resolvedCubemap);
			s_generatedSkyboxSourceCubemapId = sourceCubemapId;
			return s_generatedSkyboxMaterial;
		}
	}
}
