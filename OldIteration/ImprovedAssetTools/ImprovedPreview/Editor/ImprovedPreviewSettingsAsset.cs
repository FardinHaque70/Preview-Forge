#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	[FilePath("ProjectSettings/ImprovedAssetTools/ImprovedPreviewSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal sealed class ImprovedPreviewSettingsStorage : ScriptableSingleton<ImprovedPreviewSettingsStorage>
	{
		[SerializeField] private string settingsJson;

		public string SettingsJson
		{
			get => settingsJson;
			set => settingsJson = value;
		}

		public void SaveStorage()
		{
			ScriptRelativeAssetUtility.EnsureImprovedAssetToolsProjectSettingsFolder();
			Save(true);
		}
	}

	public sealed class ImprovedPreviewSettingsAsset : ScriptableObject
	{
		[HideInInspector] public string appliedSettingsJson;
		[HideInInspector] public int appliedRevision = 1;

		public bool isActive = true;

		public float defaultDist = ImprovedPreviewSettings.D_DefaultDist;
		public float fov = ImprovedPreviewSettings.D_Fov;
		public float orbitSmooth = ImprovedPreviewSettings.D_OrbitSmooth;
		public float zoomSmooth = ImprovedPreviewSettings.D_ZoomSmooth;
		public float maxPreviewResolutionScale = ImprovedPreviewSettings.D_MaxPreviewResolutionScale;

		public Color backgroundColor = ImprovedPreviewSettings.D_BgColor;
		public Cubemap skyboxCubemap;
		public float gridHalfSize = ImprovedPreviewSettings.D_GridHalfSize;
		public float gridStep = ImprovedPreviewSettings.D_GridStep;
		public float gridAlpha = ImprovedPreviewSettings.D_GridAlpha;

		public float keyIntensity = ImprovedPreviewSettings.D_KeyIntensity;
		public Vector3 keyPos = ImprovedPreviewSettings.D_KeyPos;
		public float fillIntensity = ImprovedPreviewSettings.D_FillIntensity;
		public Vector3 fillPos = ImprovedPreviewSettings.D_FillPos;
		public bool light2Enabled = ImprovedPreviewSettings.D_Light2Enabled;
		public float light2Intensity = ImprovedPreviewSettings.D_Light2Intensity;
		public Vector2 light2Rotation = ImprovedPreviewSettings.D_Light2Rotation;
		public Color light2Color = ImprovedPreviewSettings.D_Light2Color;
		public bool enableModelPrefabPreview = ImprovedPreviewSettings.D_EnableModelPrefabPreview;
		public bool enableSpritePrefabPreview = ImprovedPreviewSettings.D_EnableSpritePrefabPreview;
		public bool enableUiPrefabPreview = ImprovedPreviewSettings.D_EnableUiPrefabPreview;
		public bool enableParticlePrefabPreview = ImprovedPreviewSettings.D_EnableParticlePrefabPreview;
		public bool enableMaterialPreview = ImprovedPreviewSettings.D_EnableMaterialPreview;
		public bool enableNonVisualPrefabPreview = ImprovedPreviewSettings.D_EnableNonVisualPrefabPreview;
		public bool modelPreviewDefaultTurntable = ImprovedPreviewSettings.D_ModelPreviewDefaultTurntable;
		public bool modelPreviewDefaultLights = ImprovedPreviewSettings.D_ModelPreviewDefaultLights;
		public bool modelPreviewDefaultGrid = ImprovedPreviewSettings.D_ModelPreviewDefaultGrid;
		public bool modelPreviewDefaultSkybox = ImprovedPreviewSettings.D_ModelPreviewDefaultSkybox;
		public bool modelPreviewDefaultStats = ImprovedPreviewSettings.D_ModelPreviewDefaultStats;
		public bool modelPreviewDefaultBounds = ImprovedPreviewSettings.D_ModelPreviewDefaultBounds;
		public ModelPreviewDefaultVisualMode modelPreviewDefaultVisualMode = ImprovedPreviewSettings.D_ModelPreviewDefaultVisualMode;
		public float modelPreviewTwoDDistanceFactor = ImprovedPreviewSettings.D_ModelPreviewTwoDDistanceFactor;
		public float modelPreviewPerspectiveFitMultiplier = ImprovedPreviewSettings.D_ModelPreviewPerspectiveFitMultiplier;
		public float modelPreviewPerspectivePaddingMultiplier = ImprovedPreviewSettings.D_ModelPreviewPerspectivePaddingMultiplier;
		public float modelPreviewMinimumDistance = ImprovedPreviewSettings.D_ModelPreviewMinimumDistance;
		public bool spritePreviewDefaultGrid = ImprovedPreviewSettings.D_SpritePreviewDefaultGrid;
		[HideInInspector] // Legacy: sprite previews no longer expose skybox defaults.
		public bool spritePreviewDefaultSkybox = ImprovedPreviewSettings.D_SpritePreviewDefaultSkybox;
		public float spritePreviewTwoDDistanceFactor = ImprovedPreviewSettings.D_SpritePreviewTwoDDistanceFactor;
		[HideInInspector] // Legacy: sprite previews are always orthographic.
		public float spritePreviewPerspectivePaddingMultiplier = ImprovedPreviewSettings.D_SpritePreviewPerspectivePaddingMultiplier;
		[HideInInspector] // Legacy: sprite previews are always orthographic.
		public float spritePreviewMinimumPerspectiveDistance = ImprovedPreviewSettings.D_SpritePreviewMinimumPerspectiveDistance;
		public bool uiPreviewDefaultGrid = ImprovedPreviewSettings.D_UiPreviewDefaultGrid;
		[HideInInspector] // Legacy: UI previews no longer expose skybox defaults.
		public bool uiPreviewDefaultSkybox = ImprovedPreviewSettings.D_UiPreviewDefaultSkybox;
		public float uiPreviewDistanceFactor = ImprovedPreviewSettings.D_UiPreviewDistanceFactor;
		public float uiPreviewMinimumDistance = ImprovedPreviewSettings.D_UiPreviewMinimumDistance;
		public bool particlePreviewDefaultAutoplay = ImprovedPreviewSettings.D_ParticlePreviewDefaultAutoplay;
		public bool particlePreviewDefaultGrid = ImprovedPreviewSettings.D_ParticlePreviewDefaultGrid;
		public bool particlePreviewDefaultSkybox = ImprovedPreviewSettings.D_ParticlePreviewDefaultSkybox;
		public ParticlePreviewDefaultMotionShape particlePreviewDefaultMotionShape = ImprovedPreviewSettings.D_ParticlePreviewDefaultMotionShape;
		public float particlePreviewDefaultMotionSpeed = ImprovedPreviewSettings.D_ParticlePreviewDefaultMotionSpeed;
		public float particlePreviewDefaultMotionSize = ImprovedPreviewSettings.D_ParticlePreviewDefaultMotionSize;
		public float particlePreviewDurationDistanceMultiplier = ImprovedPreviewSettings.D_ParticlePreviewDurationDistanceMultiplier;
		public float particlePreviewMinimumFittedDistance = ImprovedPreviewSettings.D_ParticlePreviewMinimumFittedDistance;
		public float particlePreviewMotionFitMultiplier = ImprovedPreviewSettings.D_ParticlePreviewMotionFitMultiplier;
		public float particlePreviewFinalDistancePaddingMultiplier = ImprovedPreviewSettings.D_ParticlePreviewFinalDistancePaddingMultiplier;
		public bool materialPreviewDefaultTurntable = ImprovedPreviewSettings.D_MaterialPreviewDefaultTurntable;
		public bool materialPreviewDefaultLights = ImprovedPreviewSettings.D_MaterialPreviewDefaultLights;
		public bool materialPreviewDefaultGrid = ImprovedPreviewSettings.D_MaterialPreviewDefaultGrid;
		public bool materialPreviewDefaultSkybox = ImprovedPreviewSettings.D_MaterialPreviewDefaultSkybox;
		public bool materialPreviewDefaultReflection = ImprovedPreviewSettings.D_MaterialPreviewDefaultReflection;
		public MaterialPreviewDefaultMeshMode materialPreviewDefaultMeshMode = ImprovedPreviewSettings.D_MaterialPreviewDefaultMeshMode;
		public float materialPreviewFitMultiplier = ImprovedPreviewSettings.D_MaterialPreviewFitMultiplier;
		public float materialPreviewIntroOvershootMultiplier = ImprovedPreviewSettings.D_MaterialPreviewIntroOvershootMultiplier;
		public float materialPreviewIntroOvershootOffset = ImprovedPreviewSettings.D_MaterialPreviewIntroOvershootOffset;
		public float materialPreviewMinimumDistance = ImprovedPreviewSettings.D_MaterialPreviewMinimumDistance;

		[HideInInspector] // Legacy: replaced by per-type defaults in the Asset Types tab.
		public bool showToolbarSkyboxToggle = ImprovedPreviewSettings.D_ShowToolbarSkyboxToggle;

		[HideInInspector] // Legacy: replaced by per-type defaults in the Asset Types tab.
		public bool showToolbarLightsToggle = ImprovedPreviewSettings.D_ShowToolbarLightsToggle;

		[HideInInspector] // Legacy: replaced by per-type defaults in the Asset Types tab.
		public bool showToolbarGridToggle = ImprovedPreviewSettings.D_ShowToolbarGridToggle;

		public int maxPlaybackFps = ImprovedPreviewSettings.D_PreviewRefreshFps;
		public bool showGizmos = ImprovedPreviewSettings.D_ShowLightGizmos;
		public bool enableDiagnostics = ImprovedPreviewSettings.D_EnableDiagnostics;

		public void EnsureAppliedSnapshotInitialized()
		{
			bool changed = AppliedSettingsUtility.EnsureAppliedSnapshot(this, ref appliedSettingsJson, ref appliedRevision);
			changed |= NormalizeLegacyAppliedSnapshotJson();
			if (!changed)
				return;

			EditorUtility.SetDirty(this);
		}

		public void PersistDraftState() => ImprovedPreviewSettings.SaveDraftState(this);

		public void ResetToDefaults()
		{
			Undo.RecordObject(this, "Reset Improved Preview Settings");
			ImprovedPreviewSettings.ApplyDefaults(this);
			EditorUtility.SetDirty(this);
			PersistDraftState();
		}

		public bool ApplySettings()
		{
			EnsureAppliedSnapshotInitialized();
			if (!HasPendingChanges())
				return false;

			AppliedSettingsUtility.ApplyCurrentToSnapshot(this, ref appliedSettingsJson, ref appliedRevision);
			EditorUtility.SetDirty(this);
			PersistDraftState();
			ImprovedPreviewSettings.NotifySettingsApplied();
			return true;
		}

		public bool DiscardPendingChanges()
		{
			EnsureAppliedSnapshotInitialized();
			if (!HasPendingChanges())
				return false;

			Undo.RecordObject(this, "Discard Improved Preview Settings");
			AppliedSettingsUtility.RestoreFromSnapshot(this, appliedSettingsJson);
			EditorUtility.SetDirty(this);
			PersistDraftState();
			return true;
		}

		public bool HasPendingChanges()
		{
			EnsureAppliedSnapshotInitialized();
			return AppliedSettingsUtility.CaptureSnapshotJson(this) != (appliedSettingsJson ?? string.Empty);
		}

		private void OnValidate()
		{
			EnsureAppliedSnapshotInitialized();
		}

		private bool NormalizeLegacyAppliedSnapshotJson()
		{
			if (string.IsNullOrEmpty(appliedSettingsJson) || !ContainsRemovedLegacySnapshotFields(appliedSettingsJson))
				return false;

			ImprovedPreviewSettingsAsset snapshot = ScriptableObject.CreateInstance<ImprovedPreviewSettingsAsset>();
			try
			{
				EditorJsonUtility.FromJsonOverwrite(appliedSettingsJson, snapshot);
				string normalizedJson = AppliedSettingsUtility.CaptureSnapshotJson(snapshot);
				if (string.Equals(normalizedJson, appliedSettingsJson, System.StringComparison.Ordinal))
					return false;

				appliedSettingsJson = normalizedJson;
				return true;
			}
			finally
			{
				if (snapshot != null)
					DestroyImmediate(snapshot);
			}
		}

		private static bool ContainsRemovedLegacySnapshotFields(string json)
		{
			return json.IndexOf("\"showToolbar2DAlphaView\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"showToolbar2DSpriteOutline\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"showToolbar2DSortingLayer\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"showToolbar2DColliders\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"showCapabilityWarnings\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"showFloor\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"floorColor\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"floorSize\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"floorYOffset\"", System.StringComparison.Ordinal) >= 0
			       || json.IndexOf("\"showToolbarFloorToggle\"", System.StringComparison.Ordinal) >= 0;
		}
	}

	[CustomEditor(typeof(ImprovedPreviewSettingsAsset))]
	public sealed class ImprovedPreviewSettingsAssetEditor : UnityEditor.Editor
	{
    private enum PreviewSettingsTab
    {
        Camera,
        Environment,
        Lighting,
        AssetTypes,
        Performance,
        Debug,
    }

		private const float ResetButtonWidth = 24f;
		private const float ResetButtonHorizontalPadding = 8f;
		private const float ResetButtonSlotWidth = ResetButtonWidth + ResetButtonHorizontalPadding * 2f;
		private const float StackedSettingRowWidthThreshold = 620f;
		private const float BooleanToggleWidth = 18f;
		private const float BooleanToggleMinGap = 24f;
		private const float BooleanToggleSlotWidth = BooleanToggleWidth + BooleanToggleMinGap;

    private static readonly string[] TabTitles =
    {
        "Camera",
        "Environment",
        "Lighting",
        "Asset Types",
        "Performance",
        "Debug",
    };

		private ImprovedPreviewSettingsAsset _defaultValues;
		private ImprovedPreviewSettingsAsset _appliedSnapshot;
		private SerializedObject _appliedSerializedObject;
		private string _appliedSnapshotJson;
		private GUIContent _resetButtonContent;
		private bool _isActive;
		private Vector2 _settingsValuesScroll;
		private PreviewSettingsTab _selectedTab;
		private GUIStyle _centeredSectionHeaderStyle;
		private int _fieldRowIndex;
		private readonly Dictionary<string, bool> _assetTypeFoldoutStates = new();

		private void OnEnable()
		{
			ImprovedPreviewSettingsAsset settingsAsset = (ImprovedPreviewSettingsAsset) target;
			settingsAsset.EnsureAppliedSnapshotInitialized();

			_defaultValues = ScriptableObject.CreateInstance<ImprovedPreviewSettingsAsset>();
			ImprovedPreviewSettings.ApplyDefaults(_defaultValues);

			_resetButtonContent = GetResetButtonContent();
			_selectedTab = PreviewSettingsTab.Camera;
			Undo.undoRedoPerformed += HandleUndoRedoPerformed;

			RefreshAppliedSnapshot();
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
			DestroyAppliedSnapshot();
			if (_defaultValues != null)
				DestroyImmediate(_defaultValues);
		}

		public override void OnInspectorGUI()
		{
			ImprovedPreviewSettingsAsset settingsAsset = (ImprovedPreviewSettingsAsset) target;
			settingsAsset.EnsureAppliedSnapshotInitialized();

			serializedObject.Update();
			RefreshAppliedSnapshot();

			_isActive = serializedObject.FindProperty("isActive").boolValue;

			PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();

			EditorGUI.BeginChangeCheck();
			DrawSimpleActiveToggle(
				"Enable Improved Preview",
				"Turn custom inspector preview enhancements on or off for this project.");
			if (EditorGUI.EndChangeCheck())
			{
				CommitPendingSerializedChanges();
				ApplyImmediateActiveToggle(settingsAsset);
				serializedObject.Update();
				RefreshAppliedSnapshot();
				GUIUtility.ExitGUI();
			}

			if (!_isActive)
				return;

			_settingsValuesScroll = EditorGUILayout.BeginScrollView(_settingsValuesScroll, false, false);

			DrawRendererCompatibilitySummary(pipelineContext);

			int selected = ImprovedEditorTheme.DrawSegmentedTabs((int) _selectedTab, TabTitles);
			if (selected != (int) _selectedTab)
			{
				_selectedTab = (PreviewSettingsTab) selected;
				GUI.FocusControl(null);
			}

			EditorGUILayout.Space(6f);
			using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
				DrawSelectedTabContent(settingsAsset, pipelineContext);

			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space(8f);
			DrawPreviewActionsPanel(settingsAsset);

			if (serializedObject.ApplyModifiedProperties())
			{
				EditorUtility.SetDirty(target);
				settingsAsset.ApplySettings();
				RefreshAppliedSnapshot();
			}
		}

		private void DrawSimpleActiveToggle(string label, string tooltip)
		{
			SerializedProperty activeProperty = serializedObject.FindProperty("isActive");
			using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
			{
				bool value = activeProperty.boolValue;
				EditorGUI.BeginChangeCheck();
				value = EditorGUILayout.Toggle(value, GUILayout.Width(18f));
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(target, $"Change {label}");
					activeProperty.boolValue = value;
				}

				EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel);
			}
		}

    private void DrawSectionHeader(string title)
    {
        DrawSectionHeader(title, null);
    }

    private void DrawSectionHeader(string title, Texture icon)
    {
        if (_centeredSectionHeaderStyle == null)
        {
            _centeredSectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        Rect rect = EditorGUILayout.GetControlRect(false, 20f);
        const float iconSize = 16f;
        const float iconSpacing = 4f;

        if (icon != null)
        {
            Vector2 textSize = _centeredSectionHeaderStyle.CalcSize(new GUIContent(title));
            float totalWidth = iconSize + iconSpacing + textSize.x;
            float startX = rect.x + Mathf.Max(0f, (rect.width - totalWidth) * 0.5f);
            Rect iconRect = new Rect(startX, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            Rect textRect = new Rect(iconRect.xMax + iconSpacing, rect.y, textSize.x + 2f, rect.height);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            EditorGUI.LabelField(textRect, title, _centeredSectionHeaderStyle);
        }
        else
        {
            EditorGUI.LabelField(rect, title, _centeredSectionHeaderStyle);
        }

        Rect lineRect = new Rect(rect.x, rect.yMax + 1f, rect.width, 1f);
        EditorGUI.DrawRect(lineRect, ImprovedEditorTheme.BorderStrong);
        EditorGUILayout.Space(4f);
    }

		private void DrawSelectedTabContent(ImprovedPreviewSettingsAsset settingsAsset, PreviewPipelineContext pipelineContext)
		{
			_fieldRowIndex = 0;

			switch (_selectedTab)
			{
				case PreviewSettingsTab.Camera:
					DrawCameraTab();
					break;
				case PreviewSettingsTab.Environment:
					DrawEnvironmentTab(pipelineContext);
					break;
            case PreviewSettingsTab.Lighting:
                DrawLightingTab(pipelineContext);
                break;
            case PreviewSettingsTab.AssetTypes:
                DrawAssetTypesTab();
                break;
            case PreviewSettingsTab.Performance:
                DrawPerformanceTab();
					break;
				case PreviewSettingsTab.Debug:
					DrawDebugTab(settingsAsset, pipelineContext);
					break;
			}
		}

		private void DrawCameraTab()
		{
			DrawSectionHeader("Framing");
			DrawSlider("defaultDist", "Default Camera Distance", "Starting distance from the preview camera to the subject.", 0.5f, 50f);
			DrawSlider("fov", "Field Of View", "Camera field of view used in the preview window.", 10f, 90f);

			EditorGUILayout.Space(4f);
			DrawSectionHeader("Navigation Smoothing");
			DrawSlider("orbitSmooth", "Orbit Smoothness", "How quickly orbit movement settles after dragging.", 1f, 50f);
			DrawSlider("zoomSmooth", "Zoom Smoothness", "How quickly zoom changes settle after scrolling.", 1f, 50f);
		}

		private void DrawEnvironmentTab(PreviewPipelineContext pipelineContext)
		{
			bool supportsSkybox = pipelineContext.Capabilities.SupportsSkybox;
			DrawSectionHeader("Background");
			DrawProperty("backgroundColor", "Background Color", "Solid background color used when the skybox is disabled.");

			EditorGUILayout.Space(4f);
			DrawSectionHeader("3D Environment");
			using (new EditorGUI.DisabledScope(!supportsSkybox))
			{
				DrawProperty("skyboxCubemap", "Skybox Cubemap", "Optional cubemap override used by the preview skybox toggle in 3D-compatible projects.");
			}

			EditorGUILayout.Space(4f);
			DrawSectionHeader("Grid");
			DrawSlider("gridHalfSize", "Grid Half Size", "How far the grid extends from the center in each direction.", 1f, 50f);
			DrawSlider("gridStep", "Grid Step Size", "Spacing between grid lines.", 0.1f, 10f);
			DrawSlider("gridAlpha", "Grid Opacity", "Opacity of the ground grid lines.", 0f, 1f);
		}

		private void DrawLightingTab(PreviewPipelineContext pipelineContext)
		{
			bool supportsLightRig = pipelineContext.Capabilities.SupportsLightRig;
			if (!supportsLightRig)
			{
				DrawSectionHelpBox(
					$"Lighting controls are unavailable in {PreviewFeatureGuard.ToDisplayLabel(pipelineContext.Kind)} mode.",
					MessageType.Info);
			}

			using (new EditorGUI.DisabledScope(!supportsLightRig))
			{
				DrawSectionHeader("Key Light");
				DrawSlider("keyIntensity", "Key Light Intensity", "Brightness of the main preview light.", 0f, 4f);
				DrawProperty("keyPos", "Key Light Position", "Position of the main preview light relative to the subject.");
				EditorGUILayout.Space(4f);
				DrawSectionHeader("Fill Light");
				DrawSlider("fillIntensity", "Fill Light Intensity", "Brightness of the secondary fill light.", 0f, 4f);
				DrawProperty("fillPos", "Fill Light Position", "Position of the secondary fill light relative to the subject.");

				EditorGUILayout.Space(4f);
				DrawSectionHeader("Rim Light");
				DrawProperty("light2Enabled", "Enable Rim Light", "Turns the rim/back preview light on or off.");

				using (new EditorGUI.DisabledScope(!supportsLightRig || !serializedObject.FindProperty("light2Enabled").boolValue))
				{
					DrawSlider("light2Intensity", "Rim Light Intensity", "Brightness of the rim/back light.", 0f, 4f);
					DrawProperty("light2Rotation", "Rim Light Rotation", "Rotation of the rim/back light in degrees.");
					DrawProperty("light2Color", "Rim Light Color", "Color of the rim/back light.");
				}
			}
		}

    private void DrawAssetTypesTab()
    {
        DrawSectionHeader("Type Settings");
        DrawAssetTypeSettingsBlocks();
    }

    private void DrawAssetTypeSettingsBlocks()
    {
        DrawPreviewTypeSettingsBlock(
            "UI Prefabs",
            "enableUiPrefabPreview",
            "Enable UI Prefabs",
            "Allow UI prefabs to use the improved UI preview.",
            typeof(RectTransform),
            () =>
            {
                DrawProperty("uiPreviewDefaultGrid", "Show Grid", "Default grid visibility when the UI preview opens.");
                DrawSlider("uiPreviewDistanceFactor", "Framing Distance Factor", "Distance multiplier used for UI framing.", 0.5f, 10f);
                DrawSlider("uiPreviewMinimumDistance", "Minimum Distance", "Minimum UI preview camera distance.", 0.1f, 25f);
                EditorGUILayout.HelpBox("UI preview always uses 2D compatibility framing (orthographic camera with XY grid).", MessageType.None);
            },
            isSupported: ImprovedPreviewSettings.HasUguiSupport,
            unsupportedMessage: "Install com.unity.ugui to enable UI prefab preview.");

        DrawPreviewTypeSettingsBlock(
            "Particle Prefabs",
            "enableParticlePrefabPreview",
            "Enable Particle Prefabs",
            "Allow particle prefabs to use the improved particle preview.",
            typeof(ParticleSystem),
            () =>
            {
                DrawProperty("particlePreviewDefaultAutoplay", "Default Autoplay", "Start particle playback automatically when the preview opens.");
                DrawProperty("particlePreviewDefaultGrid", "Default Grid", "Default grid toggle state on first open.");
                DrawProperty("particlePreviewDefaultSkybox", "Default Skybox", "Default skybox toggle state on first open.");
                DrawProperty("particlePreviewDefaultMotionShape", "Default Motion Shape", "Default motion path used for distance-emission previews.");
                DrawSlider("particlePreviewDefaultMotionSpeed", "Default Motion Speed", "Default motion speed used for particle preview motion.", 0f, 360f);
                DrawSlider("particlePreviewDefaultMotionSize", "Default Motion Size", "Default motion path size used for particle preview motion.", 0f, 20f);
                DrawSlider("particlePreviewDurationDistanceMultiplier", "Duration Distance Multiplier", "Base camera distance multiplier from particle duration.", 0.1f, 10f);
                DrawSlider("particlePreviewMinimumFittedDistance", "Minimum Fitted Distance", "Minimum fitted distance used during particle framing.", 0.1f, 100f);
                DrawSlider("particlePreviewMotionFitMultiplier", "Motion Fit Multiplier", "Camera fit multiplier for particle motion paths.", 0.1f, 10f);
                DrawSlider("particlePreviewFinalDistancePaddingMultiplier", "Final Distance Padding", "Final framing padding applied after fitting.", 0.1f, 10f);
            });

        DrawPreviewTypeSettingsBlock(
            "Materials",
            "enableMaterialPreview",
            "Enable Materials",
            "Allow material assets to use the improved material preview.",
            typeof(Material),
            () =>
            {
                DrawProperty("materialPreviewDefaultTurntable", "Default Turntable", "Default turntable state on first open.");
                DrawProperty("materialPreviewDefaultLights", "Default Lights", "Default lights toggle state on first open.");
                DrawProperty("materialPreviewDefaultGrid", "Default Grid", "Default grid toggle state on first open.");
                DrawProperty("materialPreviewDefaultSkybox", "Default Skybox", "Default skybox toggle state on first open.");
                DrawProperty("materialPreviewDefaultReflection", "Default Reflection", "Default reflection toggle state on first open.");
                DrawProperty("materialPreviewDefaultMeshMode", "Default Mesh Mode", "Default material preview mesh selection.");
                DrawSlider("materialPreviewFitMultiplier", "Fit Multiplier", "Base camera fit multiplier for material mesh framing.", 0.1f, 4f);
                DrawSlider("materialPreviewIntroOvershootMultiplier", "Intro Overshoot Multiplier", "Multiplier used for intro zoom-out before settling.", 1f, 4f);
                DrawSlider("materialPreviewIntroOvershootOffset", "Intro Overshoot Offset", "Additive intro overshoot distance offset.", 0f, 10f);
                DrawSlider("materialPreviewMinimumDistance", "Minimum Distance", "Minimum material preview camera distance.", 0.1f, 25f);
            });

        DrawPreviewTypeSettingsBlock(
            "Non-Visual Prefabs",
            "enableNonVisualPrefabPreview",
            "Enable Non-Visual Prefabs",
            "Allow prefabs without renderable content to use the custom non-visual preview.",
            typeof(GameObject),
            () => EditorGUILayout.HelpBox("No render controls apply for non-visual prefabs.", MessageType.None));
    }

    private void DrawPreviewTypeSettingsBlock(
        string title,
        string enablePropertyName,
        string enableLabel,
        string enableTooltip,
        System.Type iconType,
        System.Action drawContent,
        bool isSupported = true,
        string unsupportedMessage = null)
    {
        SerializedProperty enableProperty = serializedObject.FindProperty(enablePropertyName);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            Texture icon = iconType == null ? null : EditorGUIUtility.ObjectContent(null, iconType).image;
            bool isExpanded = GetAssetTypeSectionExpanded(enablePropertyName);
            GUIContent foldoutContent = icon != null ? new GUIContent(title, icon) : new GUIContent(title);
            Rect foldoutRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            isExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, foldoutContent, true);
            SetAssetTypeSectionExpanded(enablePropertyName, isExpanded);
            if (!isExpanded)
                return;

            EditorGUILayout.Space(2f);

            if (enableProperty != null)
            {
                using (new EditorGUI.DisabledScope(!isSupported))
                    DrawProperty(enablePropertyName, enableLabel, enableTooltip);
            }

            if (!isSupported)
            {
                if (!string.IsNullOrEmpty(unsupportedMessage))
                    EditorGUILayout.HelpBox(unsupportedMessage, MessageType.Info);
                return;
            }

            bool isEnabled = enableProperty != null && enableProperty.boolValue;
            using (new EditorGUI.DisabledScope(!isEnabled))
                drawContent();

            if (!isEnabled)
                EditorGUILayout.HelpBox("Enable this preview type to edit its specific defaults and framing values.", MessageType.None);
        }
    }

	private bool GetAssetTypeSectionExpanded(string key)
	{
		if (_assetTypeFoldoutStates.TryGetValue(key, out bool isExpanded))
			return isExpanded;

		// Keep collapsed by default when the tab is first opened.
		_assetTypeFoldoutStates[key] = false;
		return false;
	}

	private void SetAssetTypeSectionExpanded(string key, bool isExpanded)
	{
		_assetTypeFoldoutStates[key] = isExpanded;
	}

		private void DrawPerformanceTab()
		{
			DrawIntSlider(
				"maxPlaybackFps",
				"Preview Refresh FPS",
				"Maximum update rate for preview playback and live material refresh.",
				ImprovedPreviewSettings.MinPreviewRefreshFps,
				ImprovedPreviewSettings.MaxPreviewRefreshFps);
			DrawSlider(
				"maxPreviewResolutionScale",
				"Max Preview Resolution Scale",
				"Upper bound for preview render resolution scaling. Lower values improve editor responsiveness.",
				ImprovedPreviewSettings.MinPreviewResolutionScale,
				ImprovedPreviewSettings.MaxPreviewResolutionScale);
		}

		private void DrawDebugTab(ImprovedPreviewSettingsAsset settingsAsset, PreviewPipelineContext pipelineContext)
		{
			bool supportsLightRig = pipelineContext.Capabilities.SupportsLightRig;
			using (new EditorGUI.DisabledScope(!supportsLightRig))
			{
				DrawProperty("showGizmos", "Show Light Gizmos", "Draw light helper gizmos in the preview for troubleshooting.");
			}

			EditorGUILayout.Space(4f);
			EditorGUI.BeginChangeCheck();
			DrawProperty("enableDiagnostics", "Enable Diagnostics", "Emit preview debug logs for skybox lookup and runtime fallback decisions.");
			if (EditorGUI.EndChangeCheck())
			{
				CommitPendingSerializedChanges();
				ApplyImmediateBooleanToggle(settingsAsset, "enableDiagnostics");
				serializedObject.Update();
				RefreshAppliedSnapshot();
				GUIUtility.ExitGUI();
			}
		}

		private void DrawPreviewActionsPanel(ImprovedPreviewSettingsAsset settingsAsset)
		{
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Reset To Defaults", GUILayout.Height(28f)) &&
			    EditorUtility.DisplayDialog(
				    "Reset Improved Preview Settings",
				    "Reset all Improved Preview settings back to their default values?",
				    "Reset",
				    "Cancel"))
			{
				settingsAsset.ResetToDefaults();
				serializedObject.Update();
				RefreshAppliedSnapshot();
				GUIUtility.ExitGUI();
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawProperty(string propertyName, string label, string tooltip, bool includeChildren = true)
		{
			SerializedProperty property = serializedObject.FindProperty(propertyName);
			if (property == null)
			{
				EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Warning);
				return;
			}

			DrawAlternatingFieldRow(() =>
			{
				bool stacked = ShouldUseStackedSettingRowLayout();
				if (property.propertyType == SerializedPropertyType.Boolean)
				{
					if (!stacked)
					{
						using (new EditorGUILayout.HorizontalScope())
						{
							EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.ExpandWidth(true));
							using (new EditorGUILayout.HorizontalScope(GUILayout.Width(BooleanToggleSlotWidth)))
							{
								GUILayout.FlexibleSpace();
								bool value = property.boolValue;
								EditorGUI.BeginChangeCheck();
								value = EditorGUILayout.Toggle(value, GUILayout.Width(BooleanToggleWidth));
								if (EditorGUI.EndChangeCheck())
								{
									Undo.RecordObject(target, $"Change {label}");
									property.boolValue = value;
								}
							}

							DrawResetButton(propertyName);
						}

						return;
					}

					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.ExpandWidth(true));
						using (new EditorGUILayout.HorizontalScope(GUILayout.Width(BooleanToggleSlotWidth)))
						{
							GUILayout.FlexibleSpace();
							bool value = property.boolValue;
							EditorGUI.BeginChangeCheck();
							value = EditorGUILayout.Toggle(value, GUILayout.Width(BooleanToggleWidth));
							if (EditorGUI.EndChangeCheck())
							{
								Undo.RecordObject(target, $"Change {label}");
								property.boolValue = value;
							}
						}
					}

					using (new EditorGUILayout.HorizontalScope())
					{
						GUILayout.FlexibleSpace();
						DrawResetButton(propertyName);
					}

					return;
				}

				if (!stacked)
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
						if (EditorGUI.EndChangeCheck())
							Undo.RecordObject(target, $"Change {label}");
						DrawResetButton(propertyName);
					}

					return;
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
				if (EditorGUI.EndChangeCheck())
					Undo.RecordObject(target, $"Change {label}");
				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();
					DrawResetButton(propertyName);
				}
			});
		}

		private void DrawSlider(string propertyName, string label, string tooltip, float min, float max)
		{
			SerializedProperty property = serializedObject.FindProperty(propertyName);
			if (property == null)
			{
				EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Warning);
				return;
			}

			DrawAlternatingFieldRow(() =>
			{
				bool stacked = ShouldUseStackedSettingRowLayout();
				if (!stacked)
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						float value = property.floatValue;
						EditorGUI.BeginChangeCheck();
						value = EditorGUILayout.Slider(new GUIContent(label, tooltip), value, min, max);
						if (EditorGUI.EndChangeCheck())
						{
							Undo.RecordObject(target, $"Change {label}");
							property.floatValue = value;
						}

						DrawResetButton(propertyName);
					}

					return;
				}

				float stackedValue = property.floatValue;
				EditorGUI.BeginChangeCheck();
				stackedValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), stackedValue, min, max);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(target, $"Change {label}");
					property.floatValue = stackedValue;
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();
					DrawResetButton(propertyName);
				}
			});
		}

		private void DrawIntSlider(string propertyName, string label, string tooltip, int min, int max)
		{
			SerializedProperty property = serializedObject.FindProperty(propertyName);
			if (property == null)
			{
				EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Warning);
				return;
			}

			DrawAlternatingFieldRow(() =>
			{
				bool stacked = ShouldUseStackedSettingRowLayout();
				if (!stacked)
				{
					using (new EditorGUILayout.HorizontalScope())
					{
						int value = property.intValue;
						EditorGUI.BeginChangeCheck();
						value = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), value, min, max);
						if (EditorGUI.EndChangeCheck())
						{
							Undo.RecordObject(target, $"Change {label}");
							property.intValue = value;
						}

						DrawResetButton(propertyName);
					}

					return;
				}

				int stackedValue = property.intValue;
				EditorGUI.BeginChangeCheck();
				stackedValue = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), stackedValue, min, max);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(target, $"Change {label}");
					property.intValue = stackedValue;
				}

				using (new EditorGUILayout.HorizontalScope())
				{
					GUILayout.FlexibleSpace();
					DrawResetButton(propertyName);
				}
			});
		}

		private static bool ShouldUseStackedSettingRowLayout()
		{
			return EditorGUIUtility.currentViewWidth <= StackedSettingRowWidthThreshold;
		}

		private void DrawAlternatingFieldRow(System.Action drawContent)
		{
			int rowIndex = _fieldRowIndex++;
			using (new EditorGUILayout.VerticalScope(ImprovedEditorTheme.GetAlternatingRowStyle(rowIndex)))
				drawContent();
		}

		private void DrawResetButton(string propertyName)
		{
			using (new EditorGUILayout.HorizontalScope(GUILayout.Width(ResetButtonSlotWidth)))
			{
				GUILayout.Space(ResetButtonHorizontalPadding);

				bool canReset = CanResetProperty(propertyName);
				using (new EditorGUI.DisabledScope(!canReset))
				{
					if (GUILayout.Button(_resetButtonContent, EditorStyles.miniButton, GUILayout.Width(ResetButtonWidth)))
						ResetPropertyToDefault(propertyName);
				}

				GUILayout.Space(ResetButtonHorizontalPadding);
			}
		}

		private void CommitPendingSerializedChanges()
		{
			if (!serializedObject.ApplyModifiedProperties())
				return;

			EditorUtility.SetDirty(target);
			((ImprovedPreviewSettingsAsset) target).PersistDraftState();
		}

		private void HandleUndoRedoPerformed()
		{
			if (!(target is ImprovedPreviewSettingsAsset settingsAsset))
				return;

			settingsAsset.PersistDraftState();
			settingsAsset.ApplySettings();
			serializedObject.Update();
			RefreshAppliedSnapshot();
			Repaint();
		}

		private void RefreshAppliedSnapshot()
		{
			ImprovedPreviewSettingsAsset settingsAsset = (ImprovedPreviewSettingsAsset) target;
			string appliedJson = settingsAsset.appliedSettingsJson ?? string.Empty;
			if (_appliedSnapshot != null && _appliedSerializedObject != null && _appliedSnapshotJson == appliedJson)
			{
				_appliedSerializedObject.Update();
				return;
			}

			DestroyAppliedSnapshot();
			_appliedSnapshot = AppliedSettingsUtility.CreateAppliedClone(settingsAsset, appliedJson);
			_appliedSerializedObject = _appliedSnapshot != null ? new SerializedObject(_appliedSnapshot) : null;
			_appliedSnapshotJson = appliedJson;
		}

		private void DestroyAppliedSnapshot()
		{
			_appliedSerializedObject = null;
			_appliedSnapshotJson = string.Empty;
			if (_appliedSnapshot == null)
				return;

			DestroyImmediate(_appliedSnapshot);
			_appliedSnapshot = null;
		}

		private void ApplyImmediateActiveToggle(ImprovedPreviewSettingsAsset settingsAsset)
		{
			ApplyImmediateBooleanToggle(settingsAsset, "isActive");
		}

		private void ApplyImmediateBooleanToggle(ImprovedPreviewSettingsAsset settingsAsset, string propertyName)
		{
			if (settingsAsset == null)
				return;

			RefreshAppliedSnapshot();
			if (_appliedSnapshot == null || _appliedSerializedObject == null)
				return;

			SerializedProperty currentProperty = serializedObject.FindProperty(propertyName);
			SerializedProperty appliedProperty = _appliedSerializedObject.FindProperty(propertyName);
			if (currentProperty == null || appliedProperty == null)
				return;

			if (currentProperty.propertyType != SerializedPropertyType.Boolean ||
			    appliedProperty.propertyType != SerializedPropertyType.Boolean)
				return;

			_appliedSerializedObject.Update();
			appliedProperty.boolValue = currentProperty.boolValue;
			_appliedSerializedObject.ApplyModifiedPropertiesWithoutUndo();

			settingsAsset.appliedSettingsJson = AppliedSettingsUtility.CaptureSnapshotJson(_appliedSnapshot);
			settingsAsset.appliedRevision = Mathf.Max(1, settingsAsset.appliedRevision + 1);
			EditorUtility.SetDirty(settingsAsset);
			settingsAsset.PersistDraftState();
			ImprovedPreviewSettings.NotifySettingsApplied();
		}

		private bool CanResetProperty(string propertyName)
		{
			SerializedProperty property = serializedObject.FindProperty(propertyName);
			if (property == null || _defaultValues == null)
				return false;

			FieldInfo field = typeof(ImprovedPreviewSettingsAsset).GetField(
				propertyName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
				return false;

			object defaultValue = field.GetValue(_defaultValues);
			return !IsPropertyAtDefault(property, defaultValue);
		}

		private static bool IsPropertyAtDefault(SerializedProperty property, object defaultValue)
		{
			switch (property.propertyType)
			{
				case SerializedPropertyType.Boolean:
					return property.boolValue == (bool) defaultValue;
				case SerializedPropertyType.Integer:
					return property.intValue == (int) defaultValue;
				case SerializedPropertyType.Float:
					return Mathf.Approximately(property.floatValue, (float) defaultValue);
				case SerializedPropertyType.Color:
					return property.colorValue.Equals((Color) defaultValue);
				case SerializedPropertyType.Vector2:
					return property.vector2Value == (Vector2) defaultValue;
				case SerializedPropertyType.Vector3:
					return property.vector3Value == (Vector3) defaultValue;
				case SerializedPropertyType.ObjectReference:
					return property.objectReferenceValue == (Object) defaultValue;
				default:
					return true;
			}
		}

		private void ResetPropertyToDefault(string propertyName)
		{
			SerializedProperty property = serializedObject.FindProperty(propertyName);
			if (property == null || _defaultValues == null)
				return;

			FieldInfo field = typeof(ImprovedPreviewSettingsAsset).GetField(
				propertyName,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (field == null)
				return;

			Undo.RecordObject(target, $"Reset {propertyName}");
			object defaultValue = field.GetValue(_defaultValues);

			switch (property.propertyType)
			{
				case SerializedPropertyType.Boolean:
					property.boolValue = (bool) defaultValue;
					break;
				case SerializedPropertyType.Integer:
					property.intValue = (int) defaultValue;
					break;
				case SerializedPropertyType.Float:
					property.floatValue = (float) defaultValue;
					break;
				case SerializedPropertyType.Color:
					property.colorValue = (Color) defaultValue;
					break;
				case SerializedPropertyType.Vector2:
					property.vector2Value = (Vector2) defaultValue;
					break;
				case SerializedPropertyType.Vector3:
					property.vector3Value = (Vector3) defaultValue;
					break;
				case SerializedPropertyType.ObjectReference:
					property.objectReferenceValue = defaultValue as Object;
					break;
				default:
					return;
			}

			GUI.changed = true;
		}

		private static GUIContent GetResetButtonContent()
		{
			Texture icon = EditorGUIUtility.FindTexture("Refresh");
			if (icon == null)
				icon = EditorGUIUtility.FindTexture("d_Refresh");

			return icon != null
				? new GUIContent(icon, "Reset this value to its default")
				: new GUIContent("R", "Reset this value to its default");
		}

		private static void DrawRendererCompatibilitySummary(PreviewPipelineContext pipelineContext)
		{
			List<string> unsupportedFeatures = GetUnsupportedFeatureLabels(pipelineContext.Capabilities);
			if (unsupportedFeatures.Count == 0)
				return;

			string pipelineLabel = string.IsNullOrEmpty(pipelineContext.Label)
				? PreviewFeatureGuard.ToDisplayLabel(pipelineContext.Kind)
				: pipelineContext.Label;
			string fallbackNote = pipelineContext.UseEditor2DFallback
				? " Editor Default Behaviour Mode is set to 2D, so built-in/legacy previews are using 2D-safe limitations."
				: string.Empty;
			string message = $"{pipelineLabel} has preview limitations. Unavailable: {string.Join(", ", unsupportedFeatures)}.{fallbackNote}";
			EditorGUILayout.HelpBox(message, MessageType.Info);
		}

		private static List<string> GetUnsupportedFeatureLabels(PreviewPipelineCapabilities capabilities)
		{
			var labels = new List<string>();
			if (!capabilities.SupportsSkybox)
				labels.Add("Skybox");
			if (!capabilities.SupportsReflectionMap)
				labels.Add("Reflection Map");
			if (!capabilities.SupportsLightRig)
				labels.Add("Custom Light Rig");
			if (!capabilities.SupportsPerspectiveOrbit)
				labels.Add("Perspective Orbit");
			if (!capabilities.Supports3DGridOrientation)
				labels.Add("3D Grid Orientation");
			return labels;
		}

		private static void DrawSectionHelpBox(string message, MessageType messageType)
		{
			EditorGUILayout.HelpBox(message, messageType);
		}
	}
}
#endif
