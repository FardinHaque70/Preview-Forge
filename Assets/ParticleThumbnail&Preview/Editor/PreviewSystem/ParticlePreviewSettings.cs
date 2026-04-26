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
		public const float D_MotionPadding = 0.2f;
		public const float MinMotionPadding = 0f;
		public const float MaxMotionPadding = 3f;
		public const float D_MotionRadius = 3f;
		public const float MinMotionRadius = 0.1f;
		public const float MaxMotionRadius = 50f;
		public const float D_MotionSpeed = 60f;
		public const float MinMotionSpeed = 0.1f;
		public const float MaxMotionSpeed = 200f;
		public static readonly Color D_BackgroundColor = new Color(0.11f, 0.11f, 0.11f, 1f);

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
					new GUIContent("Refresh FPS", "Preview update rate while the preview is visible."),
					storage.refreshFps,
					ParticlePreviewSettings.MinRefreshFps,
					ParticlePreviewSettings.MaxRefreshFps);
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
			DrawSectionCard("Motion Assist", () =>
			{
				storage.motionPadding = EditorGUILayout.Slider(
					new GUIContent("Motion Padding", "Extra framing margin when motion simulation is used."),
					storage.motionPadding,
					ParticlePreviewSettings.MinMotionPadding,
					ParticlePreviewSettings.MaxMotionPadding);
				storage.motionRadius = EditorGUILayout.Slider(
					new GUIContent("Motion Radius", "Radius of deterministic motion path for world-space movement previews."),
					storage.motionRadius,
					ParticlePreviewSettings.MinMotionRadius,
					ParticlePreviewSettings.MaxMotionRadius);
				storage.motionSpeed = EditorGUILayout.Slider(
					new GUIContent("Motion Speed", "Speed of deterministic motion path during preview simulation."),
					storage.motionSpeed,
					ParticlePreviewSettings.MinMotionSpeed,
					ParticlePreviewSettings.MaxMotionSpeed);
			});
			DrawSectionCard("Render", () =>
			{
				storage.backgroundColor = EditorGUILayout.ColorField(
					new GUIContent("Background Color", "Background color behind particle preview rendering."),
					storage.backgroundColor);
			});
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
				DrawSectionHeader("Actions");
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
