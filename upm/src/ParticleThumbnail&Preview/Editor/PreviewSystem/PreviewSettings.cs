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
			[SerializeField] internal bool modelImporterPreviewActive = PreviewSettings.D_ModelImporterPreviewActive;
			[SerializeField] internal PreviewModeOverride modelPreviewMode = PreviewSettings.D_ModelPreviewMode;
			[SerializeField] internal bool modelDefaultTurntableEnabled = PreviewSettings.D_ModelDefaultTurntableEnabled;
			[SerializeField] internal bool modelDefaultInfoEnabled = PreviewSettings.D_ModelDefaultInfoEnabled;
			[SerializeField] internal bool modelDefaultLightRotationGizmosEnabled = PreviewSettings.D_ModelDefaultLightRotationGizmosEnabled;
			[SerializeField] internal bool modelDefaultSkyboxEnabled = PreviewSettings.D_ModelDefaultSkyboxEnabled;
			[SerializeField] internal bool sharedGridDefaultEnabled = PreviewSettings.D_SharedGridDefaultEnabled;
			[SerializeField] internal bool sharedGridAxisTextDefaultEnabled = PreviewSettings.D_SharedGridAxisTextDefaultEnabled;
			[SerializeField] internal float sharedGridHalfSize = PreviewSettings.D_SharedGridHalfSize;
			[SerializeField] internal float sharedGridStep = PreviewSettings.D_SharedGridStep;
			[SerializeField] internal float sharedGridAlpha = PreviewSettings.D_SharedGridAlpha;
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
			modelImporterPreviewActive = PreviewSettings.D_ModelImporterPreviewActive;
			modelPreviewMode = PreviewSettings.D_ModelPreviewMode;
			modelDefaultTurntableEnabled = PreviewSettings.D_ModelDefaultTurntableEnabled;
			modelDefaultInfoEnabled = PreviewSettings.D_ModelDefaultInfoEnabled;
			modelDefaultLightRotationGizmosEnabled = PreviewSettings.D_ModelDefaultLightRotationGizmosEnabled;
			modelDefaultSkyboxEnabled = PreviewSettings.D_ModelDefaultSkyboxEnabled;
			sharedGridDefaultEnabled = PreviewSettings.D_SharedGridDefaultEnabled;
			sharedGridAxisTextDefaultEnabled = PreviewSettings.D_SharedGridAxisTextDefaultEnabled;
			sharedGridHalfSize = PreviewSettings.D_SharedGridHalfSize;
			sharedGridStep = PreviewSettings.D_SharedGridStep;
			sharedGridAlpha = PreviewSettings.D_SharedGridAlpha;
			sharedGridStyle = PreviewSettings.D_SharedGridStyle;
				modelSkyboxCubemap = PreviewSettings.D_ModelSkyboxCubemap;
				modelSkyboxMaterial = PreviewSkyboxAssets.GetOrCreateSkyboxMaterialForCubemap(modelSkyboxCubemap);
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
		public const float D_OrbitSmoothing = 15f;
		public const float MinOrbitSmoothing = 1f;
		public const float MaxOrbitSmoothing = 20f;
		public const float D_PanSmoothing = 15f;
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
		public const bool D_ModelImporterPreviewActive = true;
			public const PreviewModeOverride D_ModelPreviewMode = PreviewModeOverride.Auto;
			public const bool D_ModelDefaultTurntableEnabled = true;
			public const bool D_ModelDefaultInfoEnabled = true;
			public const bool D_ModelDefaultLightRotationGizmosEnabled = true;
			public const bool D_ModelDefaultSkyboxEnabled = true;
			public const bool D_SharedGridDefaultEnabled = true;
			public const bool D_SharedGridAxisTextDefaultEnabled = true;
			public const float D_SharedGridHalfSize = 6f;
			public const float D_SharedGridStep = 0.5f;
			public const float D_SharedGridAlpha = 0.217f;
			public const PreviewGridStyle D_SharedGridStyle = PreviewGridStyle.Stylized;
			public const float MinSharedGridHalfSize = 0.5f;
			public const float MaxSharedGridHalfSize = 50f;
			public const float MinSharedGridStep = 0.05f;
			public const float MaxSharedGridStep = 5f;
			public const float MinSharedGridAlpha = 0f;
			public const float MaxSharedGridAlpha = 1f;
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
		public static bool AnyPrefabCustomPreviewActive => ParticlePrefabPreviewActive || ModelPreviewActive;
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
			public static bool ThreeDAssetPreviewActive => Storage.modelImporterPreviewActive;
			public static bool ModelImporterPreviewActive => ThreeDAssetPreviewActive;
			public static PreviewModeOverride ModelPreviewMode => Storage.modelPreviewMode;
			public static bool ModelDefaultTurntableEnabled => Storage.modelDefaultTurntableEnabled;
			public static bool ModelDefaultInfoEnabled => Storage.modelDefaultInfoEnabled;
			public static bool ModelDefaultLightRotationGizmosEnabled => Storage.modelDefaultLightRotationGizmosEnabled;
			public static bool ModelDefaultSkyboxEnabled => Storage.modelDefaultSkyboxEnabled;
			public static bool SharedGridDefaultEnabled => Storage.sharedGridDefaultEnabled;
			public static bool SharedGridAxisTextDefaultEnabled => Storage.sharedGridAxisTextDefaultEnabled;
			public static float SharedGridHalfSize => Mathf.Clamp(Storage.sharedGridHalfSize, MinSharedGridHalfSize, MaxSharedGridHalfSize);
			public static float SharedGridStep => Mathf.Clamp(Storage.sharedGridStep, MinSharedGridStep, MaxSharedGridStep);
			public static float SharedGridAlpha => Mathf.Clamp(Storage.sharedGridAlpha, MinSharedGridAlpha, MaxSharedGridAlpha);
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
				EnsureModelSkyboxMaterialBackfill();
				return Storage.modelSkyboxMaterial;
			}
		}

			public static Cubemap ModelReflectionCubemap
			{
			get
			{
				EnsureModelSkyboxMaterialBackfill();
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

		private static void EnsureModelSkyboxMaterialBackfill()
		{
			if (Storage.modelSkyboxMaterial != null)
				return;

			if (Storage.modelSkyboxCubemap == null)
				Storage.modelSkyboxCubemap = PreviewSkyboxAssets.GetDefaultSkyboxCubemap();
			Storage.modelSkyboxMaterial = PreviewSkyboxAssets.TryLoadDefaultSkyboxMaterial()
			                              ?? PreviewSkyboxAssets.GetOrCreateSkyboxMaterialForCubemap(Storage.modelSkyboxCubemap);
			Storage.SaveStorage();
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
		private static readonly string[] ModelImporterPreviewSystemIcons =
		{
			"d_Mesh Icon",
			"Mesh Icon",
		};

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

			EditorGUI.BeginChangeCheck();
			SettingsScroll = EditorGUILayout.BeginScrollView(SettingsScroll, false, false);
			DrawMainTabs();
			EditorGUILayout.Space(6f);
			DrawSelectedTab(storage);
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

			if (EditorGUI.EndChangeCheck())
			{
				storage.SaveStorage();
				PreviewSettings.NotifyChanged();
			}
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

		private static void DrawSelectedTab(PreviewSettingsStorage storage)
		{
			switch (SelectedTab)
			{
				case SettingsTab.Common:
					DrawCommonTab(storage);
					break;
				case SettingsTab.Lighting:
					DrawLightingTab(storage);
					break;
				case SettingsTab.DefaultEnabledState:
					DrawDefaultEnabledStateTab(storage);
					break;
				case SettingsTab.Grid:
					DrawGridTab(storage);
					break;
				default:
					DrawCommonTab(storage);
					break;
			}
		}

		private static void DrawCommonTab(PreviewSettingsStorage storage)
		{
			DrawSectionCard("Custom Preview Systems", () =>
			{
				storage.active = DrawIconToggleLeft(
					storage.active,
					"Draw Particle Prefab Custom Preview",
					"Enable custom preview rendering for particle prefabs.",
					ParticlePreviewSystemIcons);
				storage.modelPreviewActive = DrawIconToggleLeft(
					storage.modelPreviewActive,
					"Draw Normal Prefab Custom Preview",
					"Enable custom preview rendering for non-particle prefabs that use mesh/skinned renderers.",
					PrefabPreviewSystemIcons);
				storage.modelImporterPreviewActive = DrawIconToggleLeft(
					storage.modelImporterPreviewActive,
					"Draw 3D File (FBX/BLEND) Asset Custom Preview",
					"Enable custom preview rendering for imported 3D model assets when the Model tab is active.",
					ModelImporterPreviewSystemIcons);
			});

			DrawSectionCard("Playback", () =>
			{
				storage.refreshFps = EditorGUILayout.IntSlider(
					new GUIContent("Preview FPS", "Preview update rate while the preview is visible."),
					storage.refreshFps,
					PreviewSettings.MinRefreshFps,
					PreviewSettings.MaxRefreshFps);
			});

			DrawSectionCard("Color", () =>
			{
				storage.backgroundColor = EditorGUILayout.ColorField(
					new GUIContent("Background Color", "Background color behind custom prefab preview rendering."),
					storage.backgroundColor);
				if (!Enum.IsDefined(typeof(PreviewToolbarColorPreset), storage.toolbarColorPreset))
					storage.toolbarColorPreset = PreviewSettings.D_ToolbarColorPreset;
				storage.toolbarColorPreset = (PreviewToolbarColorPreset) EditorGUILayout.EnumPopup(
					new GUIContent("Toolbar Color Preset", "Temporary active-toolbar color preset while we evaluate final branding."),
					storage.toolbarColorPreset);
				float toolbarHeight = storage.toolbarHeight <= 0f
					? PreviewSettings.D_ToolbarHeight
					: storage.toolbarHeight;
				storage.toolbarHeight = EditorGUILayout.Slider(
					new GUIContent("Toolbar Height", "Shared height for particle and model preview toolbars. Button and scrubber sizes scale automatically."),
					toolbarHeight,
					PreviewSettings.MinToolbarHeight,
					PreviewSettings.MaxToolbarHeight);
			});

			DrawSectionCard("Interaction", () =>
			{
				float orbitSmoothing = storage.orbitSmoothing <= 0f
					? PreviewSettings.D_OrbitSmoothing
					: storage.orbitSmoothing;
				float panSmoothing = storage.panSmoothing <= 0f
					? PreviewSettings.D_PanSmoothing
					: storage.panSmoothing;
				storage.orbitSmoothing = EditorGUILayout.Slider(
					new GUIContent("Orbit Smoothing", "Smoothing strength for orbit rotation input. Higher values feel softer."),
					orbitSmoothing,
					PreviewSettings.MinOrbitSmoothing,
					PreviewSettings.MaxOrbitSmoothing);
				storage.panSmoothing = EditorGUILayout.Slider(
					new GUIContent("Pan Smoothing", "Smoothing strength for panning input. Higher values feel softer."),
					panSmoothing,
					PreviewSettings.MinPanSmoothing,
					PreviewSettings.MaxPanSmoothing);
			});
		}

		private static void DrawLightingTab(PreviewSettingsStorage storage)
		{
			DrawSectionCard("Environment", () =>
			{
				storage.modelSkyboxMaterial = (Material) EditorGUILayout.ObjectField(
					new GUIContent("Skybox Material", "Material used by model preview skybox."),
					storage.modelSkyboxMaterial != null ? storage.modelSkyboxMaterial : PreviewSkyboxAssets.TryLoadDefaultSkyboxMaterial(),
					typeof(Material),
					false);
				storage.modelAmbientLightColor = EditorGUILayout.ColorField(
					new GUIContent("Ambient (HDR)", "Shared ambient lighting color used by model and particle preview when lights are enabled."),
					storage.modelAmbientLightColor,
					true,
					true,
					true);
			});

			DrawSectionCard("Directional Light", () =>
			{
				storage.modelSunLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable sunlight in model preview."),
					storage.modelSunLightEnabled);
				storage.modelSunLightColor = EditorGUILayout.ColorField(
					new GUIContent("Color", "Color of the sunlight directional light."),
					storage.modelSunLightColor);
				storage.modelSunLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Intensity of the sunlight directional light."),
					storage.modelSunLightIntensity,
					PreviewSettings.MinModelLightIntensity,
					PreviewSettings.MaxModelLightIntensity);
				storage.modelSunLightShadowStrength = EditorGUILayout.Slider(
					new GUIContent("Shadow Strength", "How dark sunlight shadows appear."),
					storage.modelSunLightShadowStrength,
					PreviewSettings.MinModelShadowStrength,
					PreviewSettings.MaxModelShadowStrength);
				storage.modelSunLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Sunlight rotation as Yaw/Pitch in degrees."),
					storage.modelSunLightRotation);
			});

			DrawSectionCard("Key Light", () =>
			{
				storage.modelKeyLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable key light in model preview."),
					storage.modelKeyLightEnabled);
				storage.modelKeyLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Key directional light intensity."),
					storage.modelKeyLightIntensity,
					PreviewSettings.MinModelLightIntensity,
					PreviewSettings.MaxModelLightIntensity);
				storage.modelKeyLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Key light rotation as Yaw/Pitch in degrees."),
					storage.modelKeyLightRotation);
			});

			DrawSectionCard("Fill Light", () =>
			{
				storage.modelFillLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable fill light in model preview."),
					storage.modelFillLightEnabled);
				storage.modelFillLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Fill directional light intensity."),
					storage.modelFillLightIntensity,
					PreviewSettings.MinModelLightIntensity,
					PreviewSettings.MaxModelLightIntensity);
				storage.modelFillLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Fill light rotation as Yaw/Pitch in degrees."),
					storage.modelFillLightRotation);
			});

			DrawSectionCard("Rim Light", () =>
			{
				storage.modelRimLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable the optional rim light in model preview."),
					storage.modelRimLightEnabled);
				storage.modelRimLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Rim directional light intensity."),
					storage.modelRimLightIntensity,
					PreviewSettings.MinModelLightIntensity,
					PreviewSettings.MaxModelLightIntensity);
				storage.modelRimLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Rim light rotation as Yaw/Pitch in degrees."),
					storage.modelRimLightRotation);
				storage.modelRimLightColor = EditorGUILayout.ColorField(
					new GUIContent("Color", "Rim light color."),
					storage.modelRimLightColor);
			});
		}

		private static void DrawDefaultEnabledStateTab(PreviewSettingsStorage storage)
		{
			DrawSectionCard("Mode", () =>
			{
				storage.modelPreviewMode = (PreviewModeOverride) EditorGUILayout.EnumPopup(
					new GUIContent("Mode Override", "Auto resolves to project default (2D or 3D) when a model preview session starts. 2D/3D force a mode."),
					storage.modelPreviewMode);
			});

			DrawSectionCard("Default enabled state", () =>
			{
				storage.modelDefaultTurntableEnabled = EditorGUILayout.Toggle(
					new GUIContent("Turntable", "Default state for the Turntable toggle."),
					storage.modelDefaultTurntableEnabled);
				storage.modelDefaultInfoEnabled = EditorGUILayout.Toggle(
					new GUIContent("Stat Info", "Default state for the Stat Info toggle."),
					storage.modelDefaultInfoEnabled);
				storage.modelDefaultLightRotationGizmosEnabled = EditorGUILayout.Toggle(
					new GUIContent("Light Rotation Gizmos", "Default state for the Light Rotation Gizmos toggle."),
					storage.modelDefaultLightRotationGizmosEnabled);
				storage.modelDefaultSkyboxEnabled = EditorGUILayout.Toggle(
					new GUIContent("Skybox", "Default state for the Skybox toggle."),
					storage.modelDefaultSkyboxEnabled);
			});

			DrawSectionCard("Debug", () =>
			{
				storage.enableDiagnostics = EditorGUILayout.Toggle(
					new GUIContent("Enable Diagnostics", "Write preview lifecycle diagnostics to the Unity Console."),
					storage.enableDiagnostics);
			});
		}

		private static void DrawGridTab(PreviewSettingsStorage storage)
		{
			DrawSectionCard("Shared Grid", () =>
			{
				storage.sharedGridDefaultEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled By Default", "Initial grid toggle state for both model and particle preview sessions."),
					storage.sharedGridDefaultEnabled);
				storage.sharedGridAxisTextDefaultEnabled = EditorGUILayout.Toggle(
					new GUIContent("Axis Text Enabled By Default", "Default visibility for +X/-X/+Z/-Z grid axis text in 3D preview."),
					storage.sharedGridAxisTextDefaultEnabled);
				storage.sharedGridStyle = (PreviewGridStyle) EditorGUILayout.EnumPopup(
					new GUIContent("Style", "Visual style used for both model and particle preview grids."),
					storage.sharedGridStyle);
				storage.sharedGridHalfSize = EditorGUILayout.Slider(
					new GUIContent("Half Size", "World-space half extent of the preview grid."),
					storage.sharedGridHalfSize,
					PreviewSettings.MinSharedGridHalfSize,
					PreviewSettings.MaxSharedGridHalfSize);
				storage.sharedGridStep = EditorGUILayout.Slider(
					new GUIContent("Step", "Distance between adjacent grid lines."),
					storage.sharedGridStep,
					PreviewSettings.MinSharedGridStep,
					PreviewSettings.MaxSharedGridStep);
				storage.sharedGridAlpha = EditorGUILayout.Slider(
					new GUIContent("Alpha", "Overall transparency for grid lines."),
					storage.sharedGridAlpha,
					PreviewSettings.MinSharedGridAlpha,
					PreviewSettings.MaxSharedGridAlpha);
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
						storage.ResetToDefaults();
						storage.SaveStorage();
						PreviewSettings.NotifyChanged();
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

		private static bool DrawIconToggleLeft(bool currentValue, string label, string tooltip, params string[] iconNames)
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
			bool newValue = EditorGUILayout.ToggleLeft(content, currentValue);
			EditorGUIUtility.SetIconSize(previousIconSize);
			return newValue;
		}
		#endregion
	}

	internal static class PreviewSkyboxAssets
	{
		private const string SkyboxFolderPath = "Assets/ParticleThumbnail&Preview/Editor/Common/PreviewAssets/Skybox";
		private const string DefaultSkyboxGuid = "83fb20c8d36eb4afe9b376da509dc0d6";
		private const string DefaultReflectionGuid = "e628e60fedd134eae92c45e351f5f566";
		private const string DefaultSkyboxMaterialPath = SkyboxFolderPath + "/PreviewSkybox.mat";

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
			return GetOrCreateSkyboxMaterialForCubemap(GetDefaultSkyboxCubemap());
		}

		internal static Material TryLoadDefaultSkyboxMaterial()
		{
			return AssetDatabase.LoadAssetAtPath<Material>(DefaultSkyboxMaterialPath);
		}

		internal static Material GetOrCreateSkyboxMaterialForCubemap(Cubemap cubemap)
		{
			Cubemap resolvedCubemap = cubemap != null ? cubemap : GetDefaultSkyboxCubemap();
			if (resolvedCubemap == null)
				return null;

			EnsureSkyboxFolderExists();
			string materialPath = DefaultSkyboxMaterialPath;

			Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			if (existing != null)
			{
				if (existing.GetTexture("_Tex") != resolvedCubemap)
				{
					existing.SetTexture("_Tex", resolvedCubemap);
					EditorUtility.SetDirty(existing);
					AssetDatabase.SaveAssets();
				}

				return existing;
			}

			Shader shader = Shader.Find("Skybox/Cubemap");
			if (shader == null)
				return null;

			Material material = new Material(shader)
			{
				name = Path.GetFileNameWithoutExtension(materialPath),
			};
			material.SetTexture("_Tex", resolvedCubemap);
			AssetDatabase.CreateAsset(material, materialPath);
			AssetDatabase.SaveAssets();
			return material;
		}

		private static void EnsureSkyboxFolderExists()
		{
			if (AssetDatabase.IsValidFolder(SkyboxFolderPath))
				return;

			string[] parts = SkyboxFolderPath.Split('/');
			string current = parts[0];
			for (int i = 1; i < parts.Length; i++)
			{
				string next = $"{current}/{parts[i]}";
				if (!AssetDatabase.IsValidFolder(next))
					AssetDatabase.CreateFolder(current, parts[i]);
				current = next;
			}
		}
	}
}
