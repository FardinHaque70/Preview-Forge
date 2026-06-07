using System;
using UnityEditor;
using UnityEngine;
// Stores thumbnail feature configuration and renders the settings interface used to tune quality and behavior.

namespace NoodleHammer.PreviewForge.Editor
{
	internal static class ParticleThumbnailSettings
	{
		public const int D_GridRenderSize = 128;
		public const int D_ListRenderSize = 32;
		public static readonly Color D_BackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

		public const float D_CameraFov = 30f;
		public const float D_CameraYaw = 35f;
		public const float D_CameraPitch = 25f;
		public const float D_BoundsPadding = 0.15f;

		public const float D_ScanMaxSeconds = 3f;
		public const float D_MotionPadding = 0.2f;
		public const float D_MotionRadius = 3f;
		public const float D_MotionSpeed = 60f;
		public const float D_ThumbnailFillTarget = 1f;
		public const bool D_EnableTightFraming = true;
		public const float D_ParticleFramingPercentile = 0.92f;

		public const int D_MaxRendersPerUpdate = 1;
		public const float D_RenderBudgetMs = 12f;
		public const int D_MemoryCacheMaxEntries = 200;

		private const string SettingsTokenVersion = "particle-thumb-v3";

		public static event Action SettingsChanged;

		private static ParticleThumbnailSettingsStorage Storage => ParticleThumbnailSettingsStorage.instance;

		public static bool Enabled => Storage.enabled;
		public static bool DrawInProjectGrid => Storage.drawInProjectGrid;
		public static bool DrawInProjectList => Storage.drawInProjectList;

		public static int GridRenderSize => Mathf.Clamp(Storage.gridRenderSize, 32, 128);
		public static int ListRenderSize => Mathf.Clamp(Storage.listRenderSize, 16, 64);

		public static int GetRenderSize(ParticleThumbnailSurface surface)
		{
			return surface == ParticleThumbnailSurface.ProjectWindowList ? ListRenderSize : GridRenderSize;
		}

		public static Color BackgroundColor => Storage.backgroundColor;

		public static float CameraFov => Mathf.Clamp(Storage.cameraFov, 10f, 90f);
		public static float CameraYaw => Mathf.Clamp(Storage.cameraYaw, -180f, 180f);
		public static float CameraPitch => Mathf.Clamp(Storage.cameraPitch, -89f, 89f);
		public static float BoundsPadding => Mathf.Clamp(Storage.boundsPadding, 0f, 1f);

		public static float ScanMaxSeconds => Mathf.Clamp(Storage.scanMaxSeconds, 0.5f, 10f);
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

		public static string GetPersistentSettingsToken()
		{
			string payload =
				$"{Enabled}|{DrawInProjectGrid}|{DrawInProjectList}|{GridRenderSize}|{ListRenderSize}|" +
				$"{BackgroundColor.r:F4},{BackgroundColor.g:F4},{BackgroundColor.b:F4},{BackgroundColor.a:F4}|" +
				$"{CameraFov:F4}|{CameraYaw:F4}|{CameraPitch:F4}|{BoundsPadding:F4}|" +
				$"{ScanMaxSeconds:F4}|{MotionPadding:F4}|{MotionRadius:F4}|{MotionSpeed:F4}|" +
				$"{BuildFramingSettingsFragment(EnableTightFraming, ParticleFramingPercentile, ThumbnailFillTarget)}|" +
				$"{MaxRendersPerUpdate}|{RenderBudgetMs:F4}|{MemoryCacheMaxEntries}|{EnablePersistentCache}|{SettingsTokenVersion}";
			return Hash128.Compute(payload).ToString();
		}

		internal static string BuildFramingSettingsFragment(bool enableTightFraming, float framingPercentile, float thumbnailFillTarget)
		{
			return
				$"{enableTightFraming}|{Mathf.Clamp(framingPercentile, 0.80f, 0.99f):F4}|" +
				$"{Mathf.Clamp(thumbnailFillTarget, 0.45f, 1f):F4}";
		}

		public static void NotifyChanged()
		{
			SettingsChanged?.Invoke();
			EditorApplication.RepaintProjectWindow();
		}
	}

	internal static class ParticleThumbnailSettingsProvider
	{
		private enum SettingsTab
		{
			Display = 0,
			Rendering = 1,
			Simulation = 2,
			Performance = 3,
		}

		private static readonly string[] MainTabTitles =
		{
			"Display",
			"Rendering",
			"Simulation",
			"Performance",
		};

		private static Vector2 SettingsScroll;
		private static SettingsTab SelectedTab = SettingsTab.Display;
		private static GUIStyle CenteredSectionHeaderStyle;

		#region Serialized Property Names
		private const string EnabledPropertyName = "enabled";
		private const string DrawInProjectGridPropertyName = "drawInProjectGrid";
		private const string DrawInProjectListPropertyName = "drawInProjectList";
		private const string GridRenderSizePropertyName = "gridRenderSize";
		private const string ListRenderSizePropertyName = "listRenderSize";
		private const string BackgroundColorPropertyName = "backgroundColor";
		private const string CameraFovPropertyName = "cameraFov";
		private const string CameraYawPropertyName = "cameraYaw";
		private const string CameraPitchPropertyName = "cameraPitch";
		private const string BoundsPaddingPropertyName = "boundsPadding";
		private const string ScanMaxSecondsPropertyName = "scanMaxSeconds";
		private const string MotionPaddingPropertyName = "motionPadding";
		private const string MotionRadiusPropertyName = "motionRadius";
		private const string MotionSpeedPropertyName = "motionSpeed";
		private const string MaxRendersPerUpdatePropertyName = "maxRendersPerUpdate";
		private const string RenderBudgetMsPropertyName = "renderBudgetMs";
		private const string MemoryCacheMaxEntriesPropertyName = "memoryCacheMaxEntries";
		private const string EnablePersistentCachePropertyName = "enablePersistentCache";
		#endregion

		[SettingsProvider]
		public static SettingsProvider CreateProvider()
		{
			return new SettingsProvider("Project/Preview Forge/Particle Thumbnails", SettingsScope.Project)
			{
				label = "Particle Thumbnails",
				guiHandler = _ => DrawGui(),
				keywords = new System.Collections.Generic.HashSet<string>
				{
					"particle",
					"thumbnail",
					"project window",
					"preview",
				},
			};
		}

		private static void DrawGui()
		{
			ParticleThumbnailSettingsStorage storage = ParticleThumbnailSettingsStorage.instance;
			SerializedObject serializedObject = ProjectSettingsUndoUtility.CreateSerializedObject(storage, () => SaveAndNotify(storage));

			DrawEnabledToggle(serializedObject);
			if (!serializedObject.FindProperty(EnabledPropertyName).boolValue)
			{
				ProjectSettingsUndoUtility.ApplyModifiedProperties(serializedObject);
				return;
			}

			SettingsScroll = EditorGUILayout.BeginScrollView(SettingsScroll, false, false);
			DrawMainTabs();
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
				SerializedProperty enabledProperty = serializedObject.FindProperty(EnabledPropertyName);
				bool enabled = EditorGUILayout.Toggle(enabledProperty.boolValue, GUILayout.Width(18f));
				if (enabled != enabledProperty.boolValue)
					enabledProperty.boolValue = enabled;

				EditorGUILayout.LabelField(
					new GUIContent("Enable Particle Thumbnails", "Turn custom particle prefab thumbnails on or off for this project."),
					EditorStyles.boldLabel);
			}
		}

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
				case SettingsTab.Display:
					DrawDisplayTab(serializedObject);
					break;
				case SettingsTab.Rendering:
					DrawRenderingTab(serializedObject);
					break;
				case SettingsTab.Simulation:
					DrawSimulationTab(serializedObject);
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
				DrawToggle(serializedObject.FindProperty(DrawInProjectGridPropertyName), "Draw In Grid", "Render custom particle thumbnails in Project window grid view.");
				DrawToggle(serializedObject.FindProperty(DrawInProjectListPropertyName), "Draw In List", "Render custom particle thumbnails in Project window list view.");
			});

			DrawSectionCard("Background color ", () =>
			{
				DrawColorField(serializedObject.FindProperty(BackgroundColorPropertyName), "Background Color", "Background color used behind rendered particle thumbnails.");
			});
			DrawSectionCard("Thumbnail Size", () =>
			{
				DrawIntSlider(serializedObject.FindProperty(GridRenderSizePropertyName), "Grid Size (pixel)", "Render resolution for grid-view thumbnails. Higher values improve clarity but increase memory and disk cost.", 32, 128);
				DrawIntSlider(serializedObject.FindProperty(ListRenderSizePropertyName), "List Size (pixel)", "Render resolution for list-view thumbnails. Higher values improve clarity but use more memory.", 16, 64);
			});
		}

		private static void DrawRenderingTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Camera", () =>
			{
				DrawFloatSlider(serializedObject.FindProperty(CameraFovPropertyName), "Camera FOV", "Field of view used when framing particle thumbnails.", 10f, 90f);
				DrawFloatSlider(serializedObject.FindProperty(CameraYawPropertyName), "Camera Yaw", "Horizontal viewing angle for thumbnail camera.", -180f, 180f);
				DrawFloatSlider(serializedObject.FindProperty(CameraPitchPropertyName), "Camera Pitch", "Vertical viewing angle for thumbnail camera.", -89f, 89f);
			});

			DrawSectionCard("Framing", () =>
			{
				DrawFloatSlider(serializedObject.FindProperty(BoundsPaddingPropertyName), "Bounds Padding", "Extra safety padding around detected particle bounds before framing.", 0f, 1f);
			});
		}

		private static void DrawSimulationTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Scan Window", () =>
			{
				DrawFloatSlider(serializedObject.FindProperty(ScanMaxSecondsPropertyName), "Scan Max Seconds", "Maximum simulation duration scanned to find the capture window.", 0.5f, 10f);
			});

			DrawSectionCard("Motion Assist (For moving particle)", () =>
			{
				DrawFloatSlider(serializedObject.FindProperty(MotionPaddingPropertyName), "Motion Padding", "Additional framing margin when motion simulation is required.", 0f, 3f);
				DrawFloatSlider(serializedObject.FindProperty(MotionRadiusPropertyName), "Motion Radius", "Radius of deterministic motion path used for world-space moving effects.", 0.1f, 50f);
				DrawFloatSlider(serializedObject.FindProperty(MotionSpeedPropertyName), "Motion Speed", "Speed of deterministic motion path used during thumbnail simulation.", 0.1f, 200f);
			});
		}

		private static void DrawPerformanceTab(SerializedObject serializedObject)
		{
			DrawSectionCard("Generation Budget", () =>
			{
				DrawIntSlider(serializedObject.FindProperty(MaxRendersPerUpdatePropertyName), "Max Renders / Update", "Maximum thumbnails generated per editor update tick.", 1, 16);
				DrawFloatSlider(serializedObject.FindProperty(RenderBudgetMsPropertyName), "Render Budget (ms)", "Time budget per editor update for thumbnail rendering to avoid stalls.", 1f, 100f);
			});

			DrawSectionCard("Cache Limits", () =>
			{
				DrawIntSlider(serializedObject.FindProperty(MemoryCacheMaxEntriesPropertyName), "Memory Cache Entries", "Maximum number of thumbnails kept in in-memory cache.", 10, 1000);
				DrawToggle(serializedObject.FindProperty(EnablePersistentCachePropertyName), "Enable Persistent Cache", "Store thumbnail PNGs in Library cache so they survive editor restart.");
			});
		}

		private static void DrawBottomActionsPanel(ParticleThumbnailSettingsStorage storage)
		{
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					if (GUILayout.Button(new GUIContent("Reset To Defaults", "Reset all particle thumbnail settings to default values."), GUILayout.Height(28f)) &&
					    EditorUtility.DisplayDialog(
						    "Reset Particle Thumbnail Settings",
						    "Reset all particle thumbnail settings back to default values?",
						    "Reset",
						    "Cancel"))
					{
						ProjectSettingsUndoUtility.ResetToDefaultsWithUndo(
							storage,
							"Reset Particle Thumbnail Settings",
							storage.ResetToDefaults,
							() => SaveAndNotify(storage));
						ParticleThumbnailService.ClearMemoryCache();
						GUIUtility.ExitGUI();
					}
				}

				if (GUILayout.Button(new GUIContent("Clear Disk Cache", "Delete all persistent thumbnail PNG files from Library cache."), GUILayout.Height(28f)))
					ParticleThumbnailService.ClearPersistentCache();

				EditorGUILayout.Space(4f);
				if (GUILayout.Button(new GUIContent("Generate All Thumbnails In Project", "Queue thumbnail generation for all supported particle prefabs in the project."), GUILayout.Height(32f)))
				{
					EditorApplication.delayCall += ParticleThumbnailService.GenerateAllThumbnailsInProjectFromSettings;
				}
			}
		}

		private static void DrawCacheRuntimeInfo()
		{
			ParticleThumbnailService.CacheStats stats = ParticleThumbnailService.GetCacheStats();
			string memorySize = FormatBytes(stats.MemoryCacheBytes);
			string diskSize = FormatBytes(stats.DiskCacheBytes);

			EditorGUILayout.Space(6f);
			EditorGUILayout.HelpBox(
				$"Memory: {stats.TotalEntries} entries ({memorySize})  |  Disk: {stats.PersistentEntryCount} files ({diskSize})  |  Generating: {stats.GeneratingCount}  |  Failed: {stats.FailedCount}  |  Queue: {stats.QueueDepth}",
				MessageType.None);
		}

		private static string FormatBytes(long bytes)
		{
			if (bytes < 1048576L)
				return $"{bytes / 1024f:F0} KB";

			return $"{bytes / 1048576f:F1} MB";
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

		private static void SaveAndNotify(ParticleThumbnailSettingsStorage storage)
		{
			storage.SaveStorage();
			ParticleThumbnailSettings.NotifyChanged();
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
	}
}
