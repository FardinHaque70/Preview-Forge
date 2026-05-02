using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
	[FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
	internal sealed class ParticlePreviewSettingsStorage : ScriptableSingleton<ParticlePreviewSettingsStorage>
	{
		private const string SettingsPath = "ProjectSettings/ParticleThumbnailAndPreview/ParticlePreviewSettings.asset";

		[SerializeField] internal bool active = ParticlePreviewSettings.D_Active;
		[SerializeField] internal int refreshFps = ParticlePreviewSettings.D_RefreshFps;
		[SerializeField] internal float orbitSmoothing = ParticlePreviewSettings.D_OrbitSmoothing;
		[SerializeField] internal float panSmoothing = ParticlePreviewSettings.D_PanSmoothing;
		[SerializeField] internal float motionPadding = ParticlePreviewSettings.D_MotionPadding;
		[SerializeField] internal float motionRadius = ParticlePreviewSettings.D_MotionRadius;
		[SerializeField] internal float motionSpeed = ParticlePreviewSettings.D_MotionSpeed;
		[SerializeField] internal Color backgroundColor = ParticlePreviewSettings.D_BackgroundColor;
		[SerializeField] internal bool modelPreviewActive = ParticlePreviewSettings.D_ModelPreviewActive;
		[SerializeField] internal PreviewModeOverride modelPreviewMode = ParticlePreviewSettings.D_ModelPreviewMode;
		[SerializeField] internal bool modelDefaultTurntableEnabled = ParticlePreviewSettings.D_ModelDefaultTurntableEnabled;
		[SerializeField] internal bool modelDefaultInfoEnabled = ParticlePreviewSettings.D_ModelDefaultInfoEnabled;
		[SerializeField] internal bool modelDefaultGridEnabled = ParticlePreviewSettings.D_ModelDefaultGridEnabled;
		[SerializeField] internal bool modelDefaultSkyboxEnabled = ParticlePreviewSettings.D_ModelDefaultSkyboxEnabled;
		[SerializeField] internal Cubemap modelSkyboxCubemap = ParticlePreviewSettings.D_ModelSkyboxCubemap;
		[SerializeField] internal Material modelSkyboxMaterial;
		[SerializeField] internal Cubemap modelReflectionCubemap = ParticlePreviewSettings.D_ModelReflectionCubemap;
		[SerializeField] internal float modelReflectionIntensity = ParticlePreviewSettings.D_ModelReflectionIntensity;
		[SerializeField] internal bool modelSunLightEnabled = ParticlePreviewSettings.D_ModelSunLightEnabled;
		[SerializeField] internal Color modelSunLightColor = ParticlePreviewSettings.D_ModelSunLightColor;
		[SerializeField] internal float modelSunLightIntensity = ParticlePreviewSettings.D_ModelSunLightIntensity;
		[SerializeField] internal float modelSunLightShadowStrength = ParticlePreviewSettings.D_ModelSunLightShadowStrength;
		[SerializeField] internal Vector2 modelSunLightRotation = ParticlePreviewSettings.D_ModelSunLightRotation;
		[SerializeField] internal bool modelKeyLightEnabled = ParticlePreviewSettings.D_ModelKeyLightEnabled;
		[SerializeField] internal float modelKeyLightIntensity = ParticlePreviewSettings.D_ModelKeyLightIntensity;
		[SerializeField] internal Vector2 modelKeyLightRotation = ParticlePreviewSettings.D_ModelKeyLightRotation;
		[SerializeField] internal bool modelFillLightEnabled = ParticlePreviewSettings.D_ModelFillLightEnabled;
		[SerializeField] internal float modelFillLightIntensity = ParticlePreviewSettings.D_ModelFillLightIntensity;
		[SerializeField] internal Vector2 modelFillLightRotation = ParticlePreviewSettings.D_ModelFillLightRotation;
		[SerializeField] internal bool modelRimLightEnabled = ParticlePreviewSettings.D_ModelRimLightEnabled;
		[SerializeField] internal float modelRimLightIntensity = ParticlePreviewSettings.D_ModelRimLightIntensity;
		[SerializeField] internal Vector2 modelRimLightRotation = ParticlePreviewSettings.D_ModelRimLightRotation;
		[SerializeField] internal Color modelRimLightColor = ParticlePreviewSettings.D_ModelRimLightColor;
		[SerializeField] internal bool enableDiagnostics = ParticlePreviewSettings.D_EnableDiagnostics;

		internal void SaveStorage()
		{
			string directory = Path.GetDirectoryName(SettingsPath);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			Save(true);
		}

		internal void ResetToDefaults()
		{
			active = ParticlePreviewSettings.D_Active;
			refreshFps = ParticlePreviewSettings.D_RefreshFps;
			orbitSmoothing = ParticlePreviewSettings.D_OrbitSmoothing;
			panSmoothing = ParticlePreviewSettings.D_PanSmoothing;
			motionPadding = ParticlePreviewSettings.D_MotionPadding;
			motionRadius = ParticlePreviewSettings.D_MotionRadius;
			motionSpeed = ParticlePreviewSettings.D_MotionSpeed;
			backgroundColor = ParticlePreviewSettings.D_BackgroundColor;
			modelPreviewActive = ParticlePreviewSettings.D_ModelPreviewActive;
			modelPreviewMode = ParticlePreviewSettings.D_ModelPreviewMode;
			modelDefaultTurntableEnabled = ParticlePreviewSettings.D_ModelDefaultTurntableEnabled;
			modelDefaultInfoEnabled = ParticlePreviewSettings.D_ModelDefaultInfoEnabled;
			modelDefaultGridEnabled = ParticlePreviewSettings.D_ModelDefaultGridEnabled;
			modelDefaultSkyboxEnabled = ParticlePreviewSettings.D_ModelDefaultSkyboxEnabled;
			modelSkyboxCubemap = ParticlePreviewSettings.D_ModelSkyboxCubemap;
			modelSkyboxMaterial = ParticlePreviewSkyboxAssets.GetOrCreateSkyboxMaterialForCubemap(modelSkyboxCubemap);
			modelSunLightEnabled = ParticlePreviewSettings.D_ModelSunLightEnabled;
			modelSunLightColor = ParticlePreviewSettings.D_ModelSunLightColor;
			modelSunLightIntensity = ParticlePreviewSettings.D_ModelSunLightIntensity;
			modelSunLightShadowStrength = ParticlePreviewSettings.D_ModelSunLightShadowStrength;
			modelSunLightRotation = ParticlePreviewSettings.D_ModelSunLightRotation;
			modelKeyLightEnabled = ParticlePreviewSettings.D_ModelKeyLightEnabled;
			modelKeyLightIntensity = ParticlePreviewSettings.D_ModelKeyLightIntensity;
			modelKeyLightRotation = ParticlePreviewSettings.D_ModelKeyLightRotation;
			modelFillLightEnabled = ParticlePreviewSettings.D_ModelFillLightEnabled;
			modelFillLightIntensity = ParticlePreviewSettings.D_ModelFillLightIntensity;
			modelFillLightRotation = ParticlePreviewSettings.D_ModelFillLightRotation;
			modelRimLightEnabled = ParticlePreviewSettings.D_ModelRimLightEnabled;
			modelRimLightIntensity = ParticlePreviewSettings.D_ModelRimLightIntensity;
			modelRimLightRotation = ParticlePreviewSettings.D_ModelRimLightRotation;
			modelRimLightColor = ParticlePreviewSettings.D_ModelRimLightColor;
			enableDiagnostics = ParticlePreviewSettings.D_EnableDiagnostics;
		}
	}

	internal static class ParticlePreviewSettings
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
		public const bool D_ModelPreviewActive = true;
		public const PreviewModeOverride D_ModelPreviewMode = PreviewModeOverride.Auto;
		public const bool D_ModelDefaultTurntableEnabled = true;
		public const bool D_ModelDefaultInfoEnabled = true;
		public const bool D_ModelDefaultGridEnabled = true;
		public const bool D_ModelDefaultSkyboxEnabled = true;
		public const Cubemap D_ModelSkyboxCubemap = null;
		public const Cubemap D_ModelReflectionCubemap = null;
		public const float D_ModelReflectionIntensity = 0.25f;
		public const bool D_ModelSunLightEnabled = true;
		public static readonly Color D_ModelSunLightColor = new Color(1f, 0.95f, 0.86f, 1f);
		public const float D_ModelSunLightIntensity = 0.55f;
		public const float D_ModelSunLightShadowStrength = 0.8f;
		public static readonly Vector2 D_ModelSunLightRotation = new Vector2(45f, 42f);
		public const bool D_ModelKeyLightEnabled = true;
		public const float D_ModelKeyLightIntensity = 1.15f;
		public static readonly Vector2 D_ModelKeyLightRotation = new Vector2(45f, 43.314f);
		public const bool D_ModelFillLightEnabled = true;
		public const float D_ModelFillLightIntensity = 0.7f;
		public static readonly Vector2 D_ModelFillLightRotation = new Vector2(225f, 25.239f);
		public const bool D_ModelRimLightEnabled = true;
		public const float D_ModelRimLightIntensity = 0.5f;
		public static readonly Vector2 D_ModelRimLightRotation = new Vector2(160f, 0f);
		public static readonly Color D_ModelRimLightColor = Color.white;
		public const float MinModelLightIntensity = 0f;
		public const float MaxModelLightIntensity = 8f;
		public const float MinModelReflectionIntensity = 0f;
		public const float MaxModelReflectionIntensity = 1f;
		public const float MinModelShadowStrength = 0f;
		public const float MaxModelShadowStrength = 1f;
		public const bool D_EnableDiagnostics = false;

		public static event Action SettingsChanged;

		private static ParticlePreviewSettingsStorage Storage => ParticlePreviewSettingsStorage.instance;

		public static bool Active => Storage.active;
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
		public static bool ModelPreviewActive => Storage.modelPreviewActive;
		public static PreviewModeOverride ModelPreviewMode => Storage.modelPreviewMode;
		public static bool ModelDefaultTurntableEnabled => Storage.modelDefaultTurntableEnabled;
		public static bool ModelDefaultInfoEnabled => Storage.modelDefaultInfoEnabled;
		public static bool ModelDefaultGridEnabled => Storage.modelDefaultGridEnabled;
		public static bool ModelDefaultSkyboxEnabled => Storage.modelDefaultSkyboxEnabled;
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

		public static float ModelReflectionIntensity => Mathf.Clamp(Storage.modelReflectionIntensity, MinModelReflectionIntensity, MaxModelReflectionIntensity);
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
				Storage.modelSkyboxCubemap = ParticlePreviewSkyboxAssets.GetDefaultSkyboxCubemap();
			Storage.modelSkyboxMaterial = ParticlePreviewSkyboxAssets.TryLoadDefaultSkyboxMaterial()
			                              ?? ParticlePreviewSkyboxAssets.GetOrCreateSkyboxMaterialForCubemap(Storage.modelSkyboxCubemap);
			Storage.SaveStorage();
		}
	}

	internal static class ParticlePreviewSettingsProvider
	{
		private const string SettingsPath = "Project/Particle Thumbnail & Preview/Particle Preview";

		private static Vector2 SettingsScroll;
		private static GUIStyle CenteredSectionHeaderStyle;

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
		{
			return new SettingsProvider(SettingsPath, SettingsScope.Project)
			{
				label = "Particle Preview",
				guiHandler = _ => DrawGui(),
				keywords = new System.Collections.Generic.HashSet<string>
				{
					"particle",
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

		private static void DrawGui()
		{
			ParticlePreviewSettingsStorage storage = ParticlePreviewSettingsStorage.instance;

			EditorGUI.BeginChangeCheck();
			DrawEnabledToggle(storage);
			SettingsScroll = EditorGUILayout.BeginScrollView(SettingsScroll, false, false);
			DrawSectionCard("Playback", () =>
			{
				storage.refreshFps = EditorGUILayout.IntSlider(
					new GUIContent("Preview FPS", "Preview update rate while the preview is visible."),
					storage.refreshFps,
					ParticlePreviewSettings.MinRefreshFps,
					ParticlePreviewSettings.MaxRefreshFps);
			});
			DrawSectionCard("Color", () =>
			{
				storage.backgroundColor = EditorGUILayout.ColorField(
					new GUIContent("Background Color", "Background color behind particle preview rendering."),
					storage.backgroundColor);
			});
			DrawSectionCard("Model Preview", () =>
			{
				storage.modelPreviewActive = EditorGUILayout.Toggle(
					new GUIContent("Enable Model Preview", "Enable custom preview for prefabs with mesh/skinned renderers."),
					storage.modelPreviewActive);
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
				storage.modelDefaultGridEnabled = EditorGUILayout.Toggle(
					new GUIContent("Grid", "Default state for the Grid toggle."),
					storage.modelDefaultGridEnabled);
				storage.modelDefaultSkyboxEnabled = EditorGUILayout.Toggle(
					new GUIContent("Skybox", "Default state for the Skybox toggle."),
					storage.modelDefaultSkyboxEnabled);
			});
			DrawSectionCard("Environment", () =>
			{
				storage.modelSkyboxMaterial = (Material) EditorGUILayout.ObjectField(
					new GUIContent("Skybox Material", "Material used by model preview skybox."),
					storage.modelSkyboxMaterial != null ? storage.modelSkyboxMaterial : ParticlePreviewSkyboxAssets.TryLoadDefaultSkyboxMaterial(),
					typeof(Material),
					false);
				DrawLightSectionHeader("Directional Light");
				storage.modelSunLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable sunlight in model preview."),
					storage.modelSunLightEnabled);
				storage.modelSunLightColor = EditorGUILayout.ColorField(
					new GUIContent("Color", "Color of the sunlight directional light."),
					storage.modelSunLightColor);
				storage.modelSunLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Intensity of the sunlight directional light."),
					storage.modelSunLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelSunLightShadowStrength = EditorGUILayout.Slider(
					new GUIContent("Shadow Strength", "How dark sunlight shadows appear."),
					storage.modelSunLightShadowStrength,
					ParticlePreviewSettings.MinModelShadowStrength,
					ParticlePreviewSettings.MaxModelShadowStrength);
				storage.modelSunLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Sunlight rotation as Yaw/Pitch in degrees."),
					storage.modelSunLightRotation);

				DrawLightSectionHeader("Key");
				storage.modelKeyLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable key light in model preview."),
					storage.modelKeyLightEnabled);
				storage.modelKeyLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Key directional light intensity."),
					storage.modelKeyLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelKeyLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Key light rotation as Yaw/Pitch in degrees."),
					storage.modelKeyLightRotation);

				DrawLightSectionHeader("Fill");
				storage.modelFillLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable fill light in model preview."),
					storage.modelFillLightEnabled);
				storage.modelFillLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Fill directional light intensity."),
					storage.modelFillLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelFillLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Fill light rotation as Yaw/Pitch in degrees."),
					storage.modelFillLightRotation);

				DrawLightSectionHeader("Rim");
				storage.modelRimLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Enabled", "Enable the optional rim light in model preview."),
					storage.modelRimLightEnabled);
				storage.modelRimLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Intensity", "Rim directional light intensity."),
					storage.modelRimLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelRimLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rotation", "Rim light rotation as Yaw/Pitch in degrees."),
					storage.modelRimLightRotation);
				storage.modelRimLightColor = EditorGUILayout.ColorField(
					new GUIContent("Color", "Rim light color."),
					storage.modelRimLightColor);
			});

			DrawSectionCard("Interaction", () =>
			{
				float orbitSmoothing = storage.orbitSmoothing <= 0f
					? ParticlePreviewSettings.D_OrbitSmoothing
					: storage.orbitSmoothing;
				float panSmoothing = storage.panSmoothing <= 0f
					? ParticlePreviewSettings.D_PanSmoothing
					: storage.panSmoothing;
				storage.orbitSmoothing = EditorGUILayout.Slider(
					new GUIContent("Orbit Smoothing", "Smoothing strength for orbit rotation input. Higher values feel softer."),
					orbitSmoothing,
					ParticlePreviewSettings.MinOrbitSmoothing,
					ParticlePreviewSettings.MaxOrbitSmoothing);
				storage.panSmoothing = EditorGUILayout.Slider(
					new GUIContent("Pan Smoothing", "Smoothing strength for panning input. Higher values feel softer."),
					panSmoothing,
					ParticlePreviewSettings.MinPanSmoothing,
					ParticlePreviewSettings.MaxPanSmoothing);
			});
			DrawSectionCard("Debug", () =>
			{
				storage.enableDiagnostics = EditorGUILayout.Toggle(
					new GUIContent("Enable Diagnostics", "Write preview lifecycle diagnostics to the Unity Console."),
					storage.enableDiagnostics);
			});
			// DrawSectionCard("Motion Assist", () =>
			// {
			// 	storage.motionPadding = EditorGUILayout.Slider(
			// 		new GUIContent("Motion Padding", "Extra framing margin when motion simulation is used."),
			// 		storage.motionPadding,
			// 		ParticlePreviewSettings.MinMotionPadding,
			// 		ParticlePreviewSettings.MaxMotionPadding);
			// 	storage.motionRadius = EditorGUILayout.Slider(
			// 		new GUIContent("Motion Radius", "Radius of deterministic motion path for world-space movement previews."),
			// 		storage.motionRadius,
			// 		ParticlePreviewSettings.MinMotionRadius,
			// 		ParticlePreviewSettings.MaxMotionRadius);
			// 	storage.motionSpeed = EditorGUILayout.Slider(
			// 		new GUIContent("Motion Speed", "Speed of deterministic motion path during preview simulation."),
			// 		storage.motionSpeed,
			// 		ParticlePreviewSettings.MinMotionSpeed,
			// 		ParticlePreviewSettings.MaxMotionSpeed);
			// });

			EditorGUILayout.EndScrollView();
			EditorGUILayout.Space(8f);
			DrawBottomActionsPanel(storage);

			if (EditorGUI.EndChangeCheck())
			{
				storage.SaveStorage();
				ParticlePreviewSettings.NotifyChanged();
			}
		}

		private static void DrawEnabledToggle(ParticlePreviewSettingsStorage storage)
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
			{
				storage.active = EditorGUILayout.Toggle(storage.active, GUILayout.Width(18f));
				EditorGUILayout.LabelField(
					new GUIContent("Enable Particle Preview", "Turn the custom particle preview extension on or off for this project."),
					EditorStyles.boldLabel);
			}
		}

		private static void DrawBottomActionsPanel(ParticlePreviewSettingsStorage storage)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				// DrawSectionHeader("Actions");
				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Button(new GUIContent("Reset To Defaults", "Reset all particle preview settings to default values."), GUILayout.Height(28f)) &&
					    EditorUtility.DisplayDialog(
						    "Reset Particle Preview Settings",
						    "Reset all particle preview settings back to default values?",
						    "Reset",
						    "Cancel"))
					{
						storage.ResetToDefaults();
						storage.SaveStorage();
						ParticlePreviewSettings.NotifyChanged();
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

		private static void DrawLightSectionHeader(string title)
		{
			EditorGUILayout.Space(4f);
			DrawSectionHeader(title);
		}
	}

	internal static class ParticlePreviewSkyboxAssets
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

		[InitializeOnLoadMethod]
		private static void EnsureDefaultSkyboxMaterialAssetOnLoad()
		{
			GetOrCreateSkyboxMaterialForCubemap(null);
		}
	}
}
