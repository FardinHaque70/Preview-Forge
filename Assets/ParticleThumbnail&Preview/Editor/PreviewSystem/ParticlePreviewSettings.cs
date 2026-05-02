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
		[SerializeField] internal bool modelDefaultLightingEnabled = ParticlePreviewSettings.D_ModelDefaultLightingEnabled;
		[SerializeField] internal bool modelDefaultSkyboxEnabled = ParticlePreviewSettings.D_ModelDefaultSkyboxEnabled;
		[SerializeField] internal Cubemap modelSkyboxCubemap = ParticlePreviewSettings.D_ModelSkyboxCubemap;
		[SerializeField] internal Color modelAmbientColor = ParticlePreviewSettings.D_ModelAmbientColor;
		[SerializeField] internal float modelKeyLightIntensity = ParticlePreviewSettings.D_ModelKeyLightIntensity;
		[SerializeField] internal Vector2 modelKeyLightRotation = ParticlePreviewSettings.D_ModelKeyLightRotation;
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
			modelDefaultLightingEnabled = ParticlePreviewSettings.D_ModelDefaultLightingEnabled;
			modelDefaultSkyboxEnabled = ParticlePreviewSettings.D_ModelDefaultSkyboxEnabled;
			modelSkyboxCubemap = ParticlePreviewSettings.D_ModelSkyboxCubemap;
			modelAmbientColor = ParticlePreviewSettings.D_ModelAmbientColor;
			modelKeyLightIntensity = ParticlePreviewSettings.D_ModelKeyLightIntensity;
			modelKeyLightRotation = ParticlePreviewSettings.D_ModelKeyLightRotation;
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
		public const bool D_ModelDefaultLightingEnabled = true;
		public const bool D_ModelDefaultSkyboxEnabled = true;
		public const Cubemap D_ModelSkyboxCubemap = null;
		public static readonly Color D_ModelAmbientColor = new Color(0.58f, 0.58f, 0.58f, 1f);
		public const float D_ModelKeyLightIntensity = 1.15f;
		public static readonly Vector2 D_ModelKeyLightRotation = new Vector2(35f, 35f);
		public const float D_ModelFillLightIntensity = 0.7f;
		public static readonly Vector2 D_ModelFillLightRotation = new Vector2(200f, -30f);
		public const bool D_ModelRimLightEnabled = true;
		public const float D_ModelRimLightIntensity = 0.5f;
		public static readonly Vector2 D_ModelRimLightRotation = new Vector2(160f, 0f);
		public static readonly Color D_ModelRimLightColor = Color.white;
		public const float MinModelLightIntensity = 0f;
		public const float MaxModelLightIntensity = 8f;
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
		public static bool ModelDefaultLightingEnabled => Storage.modelDefaultLightingEnabled;
		public static bool ModelDefaultSkyboxEnabled => Storage.modelDefaultSkyboxEnabled;
		public static Cubemap ModelSkyboxCubemap => Storage.modelSkyboxCubemap;
		public static Color ModelAmbientColor => Storage.modelAmbientColor;
		public static float ModelKeyLightIntensity => Mathf.Clamp(Storage.modelKeyLightIntensity, MinModelLightIntensity, MaxModelLightIntensity);
		public static Vector2 ModelKeyLightRotation => Storage.modelKeyLightRotation;
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
				storage.modelPreviewMode = (PreviewModeOverride)EditorGUILayout.EnumPopup(
					new GUIContent("Mode Override", "Auto follows project/pipeline detection. 2D/3D force a mode for model previews."),
					storage.modelPreviewMode);
			});
			DrawSectionCard("Model Environment", () =>
			{
				storage.modelDefaultLightingEnabled = EditorGUILayout.Toggle(
					new GUIContent("Default Lights", "Default toolbar state for model lights."),
					storage.modelDefaultLightingEnabled);
				storage.modelDefaultSkyboxEnabled = EditorGUILayout.Toggle(
					new GUIContent("Default Skybox", "Default toolbar state for model skybox."),
					storage.modelDefaultSkyboxEnabled);
				storage.modelSkyboxCubemap = (Cubemap)EditorGUILayout.ObjectField(
					new GUIContent("Skybox Cubemap", "Cubemap used when model skybox is enabled."),
					storage.modelSkyboxCubemap,
					typeof(Cubemap),
					false);
				storage.modelAmbientColor = EditorGUILayout.ColorField(
					new GUIContent("Ambient Color", "Ambient contribution for model preview lighting."),
					storage.modelAmbientColor);
				storage.modelKeyLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Key Intensity", "Key directional light intensity."),
					storage.modelKeyLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelKeyLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Key Rotation", "Key light rotation as Yaw/Pitch in degrees."),
					storage.modelKeyLightRotation);
				storage.modelFillLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Fill Intensity", "Fill directional light intensity."),
					storage.modelFillLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelFillLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Fill Rotation", "Fill light rotation as Yaw/Pitch in degrees."),
					storage.modelFillLightRotation);
				storage.modelRimLightEnabled = EditorGUILayout.Toggle(
					new GUIContent("Rim Enabled", "Enable the optional rim light in model preview."),
					storage.modelRimLightEnabled);
				storage.modelRimLightIntensity = EditorGUILayout.Slider(
					new GUIContent("Rim Intensity", "Rim directional light intensity."),
					storage.modelRimLightIntensity,
					ParticlePreviewSettings.MinModelLightIntensity,
					ParticlePreviewSettings.MaxModelLightIntensity);
				storage.modelRimLightRotation = EditorGUILayout.Vector2Field(
					new GUIContent("Rim Rotation", "Rim light rotation as Yaw/Pitch in degrees."),
					storage.modelRimLightRotation);
				storage.modelRimLightColor = EditorGUILayout.ColorField(
					new GUIContent("Rim Color", "Rim light color."),
					storage.modelRimLightColor);
			});
			DrawSectionCard("Debug", () =>
			{
				storage.enableDiagnostics = EditorGUILayout.Toggle(
					new GUIContent("Enable Diagnostics", "Write preview lifecycle diagnostics to the Unity Console."),
					storage.enableDiagnostics);
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
	}
}
