#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace FardinHaque.ImprovedAssetTools.Editor
{

[FilePath("ProjectSettings/ImprovedAssetTools/ImprovedThumbnailSettings.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class ImprovedThumbnailSettingsStorage : ScriptableSingleton<ImprovedThumbnailSettingsStorage>
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

public sealed class ImprovedThumbnailSettingsAsset : ScriptableObject
{
    [HideInInspector] public string appliedSettingsJson;
    [HideInInspector] public int appliedRevision = 1;

    public bool isActive = true;
    public int thumbnailRenderPerUpdate = ImprovedThumbnailSettings.D_ThumbnailRenderPerUpdate;
    public float thumbnailRenderBudgetMs = ImprovedThumbnailSettings.D_ThumbnailRenderBudgetMs;
    public int thumbnailCacheMaxSize = ImprovedThumbnailSettings.D_ThumbnailCacheMaxSize;
    public bool verboseLogging = ImprovedThumbnailSettings.D_VerboseLogging;
    public float thumbnailCameraFov = ImprovedThumbnailSettings.D_ThumbnailCameraFov;
    public float thumbnailCameraYaw = ImprovedThumbnailSettings.D_ThumbnailCameraYaw;
    public float thumbnailCameraPitch = ImprovedThumbnailSettings.D_ThumbnailCameraPitch;
    [FormerlySerializedAs("thumbnailSkyboxCubemap")]
    public Texture thumbnailSkyboxTexture;
    public float thumbnailBoundsPadding = ImprovedThumbnailSettings.D_ThumbnailBoundsPadding;
    public int thumbnailGridRenderSize = ImprovedThumbnailSettings.D_ThumbnailGridRenderSize;
    public int thumbnailListRenderSize = ImprovedThumbnailSettings.D_ThumbnailListRenderSize;
    public float particleThumbnailMotionPadding = ImprovedThumbnailSettings.D_ParticleThumbnailMotionPadding;
    public float particleScanMaxSeconds = ImprovedThumbnailSettings.D_ParticleScanMaxSeconds;
    public bool thumbnailDrawInProjectGrid = ImprovedThumbnailSettings.D_ThumbnailDrawInProjectGrid;
    public bool thumbnailDrawInProjectList = ImprovedThumbnailSettings.D_ThumbnailDrawInProjectList;
    public bool thumbnailDrawInObjectPicker = ImprovedThumbnailSettings.D_ThumbnailDrawInObjectPicker;
    public int thumbnailMinProjectIconSize = ImprovedThumbnailSettings.D_ThumbnailMinProjectIconSize;
    public Color thumbnailBackgroundColor = ImprovedThumbnailSettings.D_ThumbnailBackgroundColor;
    public bool showThumbnailBadges = ImprovedThumbnailSettings.D_ShowThumbnailBadges;
    public float thumbnailBadgeScale = ImprovedThumbnailSettings.D_ThumbnailBadgeScale;
    public bool showListViewTypeLabel = ImprovedThumbnailSettings.D_ShowListViewTypeLabel;
    public float listViewBadgeHorizontalOffset = ImprovedThumbnailSettings.D_ListViewBadgeHorizontalOffset;
    public bool showSelectionFrame = ImprovedThumbnailSettings.D_ShowSelectionFrame;
    public Color selectionFrameColor = ImprovedThumbnailSettings.D_SelectionFrameColor;
    public Color unselectedGridFrameColor = ImprovedThumbnailSettings.D_UnselectedGridFrameColor;

    public float thumbnailGroundGridHalfSize = ImprovedThumbnailSettings.D_ThumbnailGroundGridHalfSize;
    public float thumbnailGroundGridStep = ImprovedThumbnailSettings.D_ThumbnailGroundGridStep;
    public float thumbnailGroundGridAlpha = ImprovedThumbnailSettings.D_ThumbnailGroundGridAlpha;
    public bool enableParticleProvider = ImprovedThumbnailSettings.D_EnableParticleProvider;
    public bool enableUiProvider = ImprovedThumbnailSettings.D_EnableUiProvider;
    public bool enableSpriteProvider = ImprovedThumbnailSettings.D_EnableSpriteProvider;
    public bool enableGeneralProvider = ImprovedThumbnailSettings.D_EnableGeneralProvider;
    public bool enableModelProvider = ImprovedThumbnailSettings.D_EnableModelProvider;
    public bool enableMaterialProvider = ImprovedThumbnailSettings.D_EnableMaterialProvider;
    public List<ThumbnailProviderType> providerPriorityOrder = ImprovedThumbnailSettings.GetDefaultProviderPriorityOrder();
    public float generalThumbnailLightIntensity = ImprovedThumbnailSettings.D_GeneralThumbnailLightIntensity;
    public float modelThumbnailLightIntensity = ImprovedThumbnailSettings.D_ModelThumbnailLightIntensity;
    public float modelThumbnailBoundsPadding = ImprovedThumbnailSettings.D_ModelThumbnailBoundsPadding;
    public float modelThumbnailVerticalBias = ImprovedThumbnailSettings.D_ModelThumbnailVerticalBias;
    public bool spriteThumbnailFrontView = ImprovedThumbnailSettings.D_SpriteThumbnailFrontView;
    public float spriteThumbnailLightIntensity = ImprovedThumbnailSettings.D_SpriteThumbnailLightIntensity;
    public void EnsureAppliedSnapshotInitialized()
    {
        if (!AppliedSettingsUtility.EnsureAppliedSnapshot(this, ref appliedSettingsJson, ref appliedRevision))
            return;

        EditorUtility.SetDirty(this);
    }

    public void PersistDraftState() => ImprovedThumbnailSettings.SaveDraftState(this);

    public void ClearCache()
    {
        PrefabThumbnailService.ClearAllCaches();
        EditorApplication.RepaintProjectWindow();
    }

    public void GenerateAllThumbnails()
    {
        PrefabThumbnailService.GenerateAllThumbnailsWithProgress();
    }

    public void ResetToDefaults()
    {
        Undo.RecordObject(this, "Reset Improved Thumbnail Settings");
        ImprovedThumbnailSettings.ApplyDefaults(this);
        EditorUtility.SetDirty(this);
        PersistDraftState();
    }

    public bool ApplySettings(PrefabThumbnailService.SettingsApplyImpact impact)
    {
        EnsureAppliedSnapshotInitialized();
        if (!HasPendingChanges())
            return false;

        AppliedSettingsUtility.ApplyCurrentToSnapshot(this, ref appliedSettingsJson, ref appliedRevision);
        EditorUtility.SetDirty(this);
        PersistDraftState();
        ImprovedThumbnailSettings.InvalidateAppliedSnapshotCache();
        PrefabThumbnailService.ApplySettings(impact);
        return true;
    }

    public bool DiscardPendingChanges()
    {
        EnsureAppliedSnapshotInitialized();
        if (!HasPendingChanges())
            return false;

        Undo.RecordObject(this, "Discard Improved Thumbnail Settings");
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
}

[CustomEditor(typeof(ImprovedThumbnailSettingsAsset))]
public sealed class ImprovedThumbnailSettingsAssetEditor : UnityEditor.Editor
{
    private enum ThumbnailSettingsTab
    {
        Visuals,
        ProjectWindow,
        Rendering,
        AssetTypes,
        Performance,
    }

    private const float ResetButtonWidth = 24f;
    private const float ResetButtonHorizontalPadding = 8f;
    private const float ResetButtonSlotWidth = ResetButtonWidth + ResetButtonHorizontalPadding * 2f;
    private const float StackedSettingRowWidthThreshold = 620f;
    private const float BooleanToggleWidth = 18f;
    private const float BooleanToggleMinGap = 24f;
    private const float BooleanToggleSlotWidth = BooleanToggleWidth + BooleanToggleMinGap;

    private static readonly string[] MainTabTitles =
    {
        "Visuals",
        "Project Window",
        "Rendering",
        "Asset Types",
        "Performance",
    };

    private static readonly string[] AssetTypesTabProperties =
    {
        "providerPriorityOrder",
        "enableGeneralProvider",
        "generalThumbnailLightIntensity",
        "enableParticleProvider",
        "particleThumbnailMotionPadding",
        "particleScanMaxSeconds",
        "enableSpriteProvider",
        "spriteThumbnailFrontView",
        "spriteThumbnailLightIntensity",
        "enableUiProvider",
        "enableModelProvider",
        "modelThumbnailLightIntensity",
        "modelThumbnailBoundsPadding",
        "modelThumbnailVerticalBias",
        "enableMaterialProvider",
    };

    private static readonly string[] RenderingTabProperties =
    {
        "thumbnailCameraFov",
        "thumbnailCameraYaw",
        "thumbnailCameraPitch",
        "thumbnailSkyboxTexture",
        "thumbnailBoundsPadding",
        "thumbnailGridRenderSize",
        "thumbnailListRenderSize",
        "thumbnailGroundGridHalfSize",
        "thumbnailGroundGridStep",
        "thumbnailGroundGridAlpha",
    };

    private static readonly string[] ProjectWindowTabProperties =
    {
        "thumbnailDrawInProjectGrid",
        "thumbnailDrawInProjectList",
        "thumbnailDrawInObjectPicker",
        "thumbnailMinProjectIconSize",
    };

    private static readonly string[] VisualsTabProperties =
    {
        "thumbnailBackgroundColor",
        "showSelectionFrame",
        "selectionFrameColor",
        "unselectedGridFrameColor",
        "showThumbnailBadges",
        "thumbnailBadgeScale",
        "showListViewTypeLabel",
        "listViewBadgeHorizontalOffset",
    };

    private static readonly string[] PerformanceTabProperties =
    {
        "thumbnailRenderPerUpdate",
        "thumbnailRenderBudgetMs",
        "verboseLogging",
        "thumbnailCacheMaxSize",
    };

    private ImprovedThumbnailSettingsAsset _defaultValues;
    private ImprovedThumbnailSettingsAsset _appliedSnapshot;
    private SerializedObject _appliedSerializedObject;
    private string _appliedSnapshotJson;
    private GUIContent _resetButtonContent;
    private bool _isActive;
    private bool _deferredPersistQueued;
    private Vector2 _settingsValuesScroll;
    private ThumbnailSettingsTab _selectedTab;
    private GUIStyle _centeredSectionHeaderStyle;
    private int _fieldRowIndex;

    private void OnEnable()
    {
        ImprovedThumbnailSettingsAsset settingsAsset = (ImprovedThumbnailSettingsAsset)target;
        settingsAsset.EnsureAppliedSnapshotInitialized();

        _defaultValues = ScriptableObject.CreateInstance<ImprovedThumbnailSettingsAsset>();
        ImprovedThumbnailSettings.ApplyDefaults(_defaultValues);

        _resetButtonContent = GetResetButtonContent();
        _selectedTab = ThumbnailSettingsTab.Visuals;

        RefreshAppliedSnapshot();
    }

    private void OnDisable()
    {
        if (_deferredPersistQueued)
        {
            EditorApplication.delayCall -= FlushDeferredDraftPersist;
            _deferredPersistQueued = false;
            if (target is ImprovedThumbnailSettingsAsset settingsAsset)
                settingsAsset.PersistDraftState();
        }

        DestroyAppliedSnapshot();
        if (_defaultValues != null)
            DestroyImmediate(_defaultValues);
    }

    public override void OnInspectorGUI()
    {
        ImprovedThumbnailSettingsAsset settingsAsset = (ImprovedThumbnailSettingsAsset)target;
        settingsAsset.EnsureAppliedSnapshotInitialized();

        serializedObject.Update();
        RefreshAppliedSnapshot();

        _isActive = serializedObject.FindProperty("isActive").boolValue;

        EditorGUI.BeginChangeCheck();
        DrawSimpleActiveToggle(
            "Enable Improved Thumbnail",
            "Turn custom thumbnail generation on or off for this project.");
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

        DrawMainTabs();
        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            DrawSelectedMainTabContent();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(8f);
        DrawThumbnailActionsPanel(settingsAsset);
        DrawThumbnailRuntimeInfo(_isActive);

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(target);
            if (GUIUtility.hotControl == 0)
            {
                settingsAsset.PersistDraftState();
                RefreshAppliedSnapshot();
            }
            else
            {
                QueueDeferredDraftPersist();
            }
        }
    }

    private void DrawSimpleActiveToggle(string label, string tooltip)
    {
        SerializedProperty activeProperty = serializedObject.FindProperty("isActive");
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            activeProperty.boolValue = EditorGUILayout.Toggle(activeProperty.boolValue, GUILayout.Width(18f));
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

    private void DrawMainTabs()
    {
        string[] labels =
        {
            BuildTabLabel(MainTabTitles[0], AreAnyPropertiesPending(VisualsTabProperties)),
            BuildTabLabel(MainTabTitles[1], AreAnyPropertiesPending(ProjectWindowTabProperties)),
            BuildTabLabel(MainTabTitles[2], AreAnyPropertiesPending(RenderingTabProperties)),
            BuildTabLabel(MainTabTitles[3], AreAnyPropertiesPending(AssetTypesTabProperties)),
            BuildTabLabel(MainTabTitles[4], AreAnyPropertiesPending(PerformanceTabProperties)),
        };

        int selected = ImprovedEditorTheme.DrawSegmentedTabs((int)_selectedTab, labels);
        if (selected != (int)_selectedTab)
        {
            _selectedTab = (ThumbnailSettingsTab)selected;
            GUI.FocusControl(null);
        }
    }

    private void DrawSelectedMainTabContent()
    {
        _fieldRowIndex = 0;

        switch (_selectedTab)
        {
            case ThumbnailSettingsTab.AssetTypes:
                DrawAssetTypesTab();
                break;
            case ThumbnailSettingsTab.Rendering:
                DrawRenderingTab();
                break;
            case ThumbnailSettingsTab.ProjectWindow:
                DrawProjectWindowTab();
                break;
            case ThumbnailSettingsTab.Visuals:
                DrawVisualsTab();
                break;
            case ThumbnailSettingsTab.Performance:
                DrawPerformanceTab();
                break;
        }
    }

    private void DrawAssetTypesTab()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSectionHeader("Provider Priority");
            DrawProviderPriorityOrderEditor(
                "Order",
                "Drag or move items so the top-most supported provider wins for overlapping prefab types.");
        }

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Type Settings");
        DrawAssetTypeSettingsBlocks();
    }

    private void DrawAssetTypeSettingsBlocks()
    {
        DrawAssetTypeSettingsBlock(
            "General Prefabs",
            "enableGeneralProvider",
            "Enable General Prefabs",
            "Allow regular prefabs to use the general thumbnail generator.",
            typeof(GameObject),
            () => DrawSlider("generalThumbnailLightIntensity", "Light Intensity", "Main light intensity used for general prefab thumbnails.", 0f, 4f));

        DrawAssetTypeSettingsBlock(
            "Particle Prefabs",
            "enableParticleProvider",
            "Enable Particle Prefabs",
            "Allow particle prefabs to use the particle thumbnail generator.",
            typeof(ParticleSystem),
            () =>
            {
                DrawSlider("particleThumbnailMotionPadding", "Motion Padding", "Additional framing space for moving particle effects.", 0f, 3f);
                DrawSlider("particleScanMaxSeconds", "Scan Duration", "Maximum simulation time scanned to find the particle system's peak density.", 0.5f, 10f);
            });

        DrawAssetTypeSettingsBlock(
            "Sprite Prefabs",
            "enableSpriteProvider",
            "Enable Sprite Prefabs",
            "Allow sprite-based prefabs to use the sprite thumbnail generator.",
            typeof(SpriteRenderer),
            () =>
            {
                DrawProperty("spriteThumbnailFrontView", "Use Front View", "Render sprite thumbnails from a front-facing view.");
                DrawSlider("spriteThumbnailLightIntensity", "Light Intensity", "Light intensity used for sprite thumbnails.", 0f, 4f);
            });

        DrawAssetTypeSettingsBlock(
            "Model Assets",
            "enableModelProvider",
            "Enable Model Assets",
            "Allow imported model assets to use the model thumbnail generator.",
            typeof(Mesh),
            () =>
            {
                DrawSlider("modelThumbnailLightIntensity", "Light Intensity", "Light intensity used for imported model thumbnails.", 0f, 4f);
                DrawSlider("modelThumbnailBoundsPadding", "Bounds Padding", "Extra framing space added around imported models.", 0f, 1f);
                DrawSlider("modelThumbnailVerticalBias", "Vertical Bias", "Shift the framing up or down for tall or grounded models.", -0.5f, 0.5f);
            });

        if (ImprovedThumbnailSettings.HasUguiSupport)
        {
            DrawAssetTypeSettingsBlock(
                "UI Prefabs",
                "enableUiProvider",
                "Enable UI Prefabs",
                "Allow UI prefabs to use the UI thumbnail generator.",
                typeof(Canvas),
                () => EditorGUILayout.HelpBox("No additional UI-specific options yet.", MessageType.None));
        }
        else
        {
            DrawAssetTypeSettingsBlock(
                "UI Prefabs",
                "enableUiProvider",
                "Enable UI Prefabs",
                "UI provider requires com.unity.ugui.",
                typeof(Canvas),
                () => EditorGUILayout.HelpBox("No additional UI-specific options yet.", MessageType.None),
                isSupported: false,
                unsupportedMessage: "Install the Unity UI package (com.unity.ugui) to enable UI prefab thumbnails.");
        }

        DrawAssetTypeSettingsBlock(
            "Materials",
            "enableMaterialProvider",
            "Enable Materials",
            "Allow material assets to use the material thumbnail generator.",
            typeof(Material),
            () => EditorGUILayout.HelpBox("No additional material-specific options yet.", MessageType.None));
    }

    private void DrawAssetTypeSettingsBlock(
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
            DrawSectionHeader(title, icon);

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
                EditorGUILayout.HelpBox("Enable this asset type to edit its specific settings.", MessageType.None);
        }
    }

    private void DrawRenderingTab()
    {
        DrawSectionHeader("Camera");
        DrawSlider("thumbnailCameraFov", "Camera Field Of View", "Camera field of view used when rendering thumbnails.", 10f, 90f);
        DrawSlider("thumbnailCameraYaw", "Camera Yaw", "Shared horizontal camera angle for thumbnail rendering (general prefabs, particles, models, materials, and angled sprite views).", -180f, 180f);
        DrawSlider("thumbnailCameraPitch", "Camera Pitch", "Shared vertical camera angle for 3D thumbnail rendering (general prefabs, particles, models, and materials).", -89f, 89f);

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Environment");
        DrawProperty("thumbnailSkyboxTexture", "HDRI Skybox", "Optional HDRI Texture/Cubemap used for thumbnail lighting and reflections. If cleared, the packaged default skybox is assigned automatically.");
        DrawSlider("thumbnailBoundsPadding", "Default Bounds Padding", "Extra framing space added around rendered assets.", 0f, 1f);

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Output Resolution");
        DrawIntSlider("thumbnailGridRenderSize", "Grid View (Pixels)", "Render resolution (pixels) for grid-view thumbnails. Higher values are sharper but use more memory and disk space. Powers of 2 recommended (64, 128, 256).", 32, 512);
        DrawIntSlider("thumbnailListRenderSize", "List View (Pixels)", "Render resolution (pixels) for list-view thumbnails. Powers of 2 recommended (16, 32, 64).", 16, 128);

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Ground Grid");
        DrawSlider("thumbnailGroundGridHalfSize", "Ground Grid Half Size", "How far the thumbnail ground grid extends from the center in each direction.", 0.5f, 50f);
        DrawSlider("thumbnailGroundGridStep", "Ground Grid Step", "Spacing between thumbnail ground grid lines.", 0.05f, 10f);
        DrawSlider("thumbnailGroundGridAlpha", "Ground Grid Opacity", "Opacity of thumbnail ground grid lines.", 0f, 1f);
    }

    private void DrawProjectWindowTab()
    {
        DrawSectionHeader("Display Surfaces");
        DrawProperty("thumbnailDrawInProjectGrid", "Enable In Project Grid View", "Draw custom thumbnails in the Project window grid view.");
        DrawProperty("thumbnailDrawInProjectList", "Enable In Project List View", "Draw custom thumbnails in the Project window list view.");
        DrawProperty("thumbnailDrawInObjectPicker", "Enable In Object Picker", "Draw custom thumbnails in the object picker (Select Object) window.");

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Visibility Threshold");
        DrawIntSlider("thumbnailMinProjectIconSize", "Minimum Project Icon Size", "Smallest icon size that will still draw a custom thumbnail.", 16, 128);
    }

    private void DrawVisualsTab()
    {
        DrawSectionHeader("Background and Frame");
        DrawProperty("thumbnailBackgroundColor", "Background Color", "Solid background color for generated thumbnails.");
        DrawProperty("showSelectionFrame", "Show Selection Frame", "Draw a custom selection frame for selected improved thumbnails in Project grid view.");
        if (serializedObject.FindProperty("showSelectionFrame").boolValue)
            DrawProperty("selectionFrameColor", "Selection Frame Color", "Tint color used for the custom selection frame.");
        DrawProperty("unselectedGridFrameColor", "Unselected Grid Frame Color", "Color of the always-on frame drawn around improved thumbnails in Project grid view.");

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Badges");
        DrawProperty("showThumbnailBadges", "Show Asset Type Badges", "Overlay a small badge that identifies the asset type (grid view only).");
        if (serializedObject.FindProperty("showThumbnailBadges").boolValue)
            DrawSlider("thumbnailBadgeScale", "Asset Type Badge Scale", "Size multiplier for the asset type badge overlay.", 0.5f, 2f);
        DrawProperty("showListViewTypeLabel", "Show Type Badge in List View", "Overlay the asset type badge on list view thumbnails at an adjustable horizontal position.");
        if (serializedObject.FindProperty("showListViewTypeLabel").boolValue)
            DrawSlider("listViewBadgeHorizontalOffset", "List View Badge Horizontal Offset", "Horizontal offset of the type badge in list view. Adjust until the badge sits where Unity's default icon slot is.", -40f, 40f);
    }

    private void DrawPerformanceTab()
    {
        DrawSectionHeader("Generation Budget");
        DrawIntSlider("thumbnailRenderPerUpdate", "Max Thumbnails Per Update", "Safety cap on the maximum number of thumbnails rendered per editor update, regardless of the time budget.", 1, 16);
        DrawSlider("thumbnailRenderBudgetMs", "Render Time Budget (ms)", "Time budget in milliseconds for rendering thumbnails per editor update. Generation pauses when the budget is exceeded.", 1f, 100f);

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Diagnostics");
        DrawProperty("verboseLogging", "Verbose Logging", "Log detailed thumbnail render events and cache activity to the Console for debugging.");

        EditorGUILayout.Space(6f);
        DrawSectionHeader("Cache Limits");
        DrawIntSlider("thumbnailCacheMaxSize", "Cache Max Entries", "Maximum number of thumbnails kept in memory at once. Oldest entries are evicted when the limit is reached.", 10, 1000);
    }

    private void DrawThumbnailActionsPanel(ImprovedThumbnailSettingsAsset settingsAsset)
    {
        DrawPendingChangesBanner();
        DrawApplyButtons(settingsAsset);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Cache", GUILayout.Height(28f)))
            settingsAsset.ClearCache();

        if (GUILayout.Button("Reset To Defaults", GUILayout.Height(28f)) &&
            EditorUtility.DisplayDialog(
                "Reset Improved Thumbnail Settings",
                "Reset all Improved Thumbnail settings back to their default values?",
                "Reset",
                "Cancel"))
        {
            settingsAsset.ResetToDefaults();
            serializedObject.Update();
            settingsAsset.ApplySettings(DetermineThumbnailApplyImpact());
            serializedObject.Update();
            RefreshAppliedSnapshot();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Generate All Thumbnails", GUILayout.Height(32f)))
            settingsAsset.GenerateAllThumbnails();
    }

    private static void DrawThumbnailRuntimeInfo(bool isActive)
    {
        if (!isActive)
            return;

        EditorGUILayout.Space(6f);
        PrefabThumbnailService.CacheStats stats = PrefabThumbnailService.GetCacheStats();
        string memStr = stats.MemoryCacheBytes < 1048576
            ? $"{stats.MemoryCacheBytes / 1024f:F0} KB"
            : $"{stats.MemoryCacheBytes / 1048576f:F1} MB";
        string diskStr = stats.DiskCacheBytes < 1048576
            ? $"{stats.DiskCacheBytes / 1024f:F0} KB"
            : $"{stats.DiskCacheBytes / 1048576f:F1} MB";

        EditorGUILayout.HelpBox(
            $"Memory: {stats.TotalEntries} entries ({memStr})  |  Disk: {stats.PersistentEntryCount} files ({diskStr})  |  Generating: {stats.GeneratingCount}  |  Failed: {stats.FailedCount}  |  Queue: {stats.QueueDepth}",
            MessageType.None);
    }

    private void DrawPendingChangesBanner()
    {
        if (!_isActive)
            return;

        if (!HasNonActivationPendingChanges())
            return;

        EditorGUILayout.HelpBox("Some settings have changed but are not live yet. Click Apply Settings when you are ready to refresh the systems safely.", MessageType.Warning);
    }

    private void DrawApplyButtons(ImprovedThumbnailSettingsAsset settingsAsset)
    {
        bool hasPendingChanges = HasPendingChanges();

        EditorGUILayout.BeginHorizontal();
        Color previousButtonColor = GUI.backgroundColor;
        try
        {
            if (hasPendingChanges)
                GUI.backgroundColor = ImprovedEditorTheme.Accent;

            using (new EditorGUI.DisabledScope(!hasPendingChanges))
            {
                if (GUILayout.Button("Apply Settings", GUILayout.Height(30f)))
                {
                    CommitPendingSerializedChanges();
                    settingsAsset.ApplySettings(DetermineThumbnailApplyImpact());
                    serializedObject.Update();
                    RefreshAppliedSnapshot();
                    GUIUtility.ExitGUI();
                }
            }
        }
        finally
        {
            GUI.backgroundColor = previousButtonColor;
        }

        using (new EditorGUI.DisabledScope(!hasPendingChanges))
        {
            if (GUILayout.Button("Discard Pending Changes", GUILayout.Height(30f)))
            {
                settingsAsset.DiscardPendingChanges();
                serializedObject.Update();
                RefreshAppliedSnapshot();
                GUIUtility.ExitGUI();
            }
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
                            property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(BooleanToggleWidth));
                        }
                        DrawPendingIndicator(propertyName);
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
                        property.boolValue = EditorGUILayout.Toggle(property.boolValue, GUILayout.Width(BooleanToggleWidth));
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    DrawPendingIndicator(propertyName);
                    DrawResetButton(propertyName);
                }
                return;
            }

            if (!stacked)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
                    DrawPendingIndicator(propertyName);
                    DrawResetButton(propertyName);
                }
                return;
            }

            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip), includeChildren);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                DrawPendingIndicator(propertyName);
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
                    property.floatValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), property.floatValue, min, max);
                    DrawPendingIndicator(propertyName);
                    DrawResetButton(propertyName);
                }
                return;
            }

            property.floatValue = EditorGUILayout.Slider(new GUIContent(label, tooltip), property.floatValue, min, max);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                DrawPendingIndicator(propertyName);
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
                    property.intValue = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), property.intValue, min, max);
                    DrawPendingIndicator(propertyName);
                    DrawResetButton(propertyName);
                }
                return;
            }

            property.intValue = EditorGUILayout.IntSlider(new GUIContent(label, tooltip), property.intValue, min, max);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                DrawPendingIndicator(propertyName);
                DrawResetButton(propertyName);
            }
        });
    }

    private static bool ShouldUseStackedSettingRowLayout()
    {
        return EditorGUIUtility.currentViewWidth <= StackedSettingRowWidthThreshold;
    }

    private void DrawPendingIndicator(string propertyName)
    {
        bool isPending = IsPropertyPending(propertyName);
        Color previousColor = GUI.color;
        if (isPending)
            GUI.color = ImprovedEditorTheme.Warning;

        GUILayout.Label(new GUIContent(isPending ? "*" : " ", isPending ? "Changed, not yet applied" : string.Empty), GUILayout.Width(10f));
        GUI.color = previousColor;
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

    private void DrawProviderPriorityOrderEditor(string label, string tooltip)
    {
        SerializedProperty orderProperty = serializedObject.FindProperty("providerPriorityOrder");
        if (orderProperty == null || !orderProperty.isArray)
        {
            EditorGUILayout.HelpBox("Missing serialized property: providerPriorityOrder", MessageType.Warning);
            return;
        }

        EnsureProviderPriorityOrderProperty(orderProperty);

        EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel);

        bool moved = false;
        for (int i = 0; i < orderProperty.arraySize; i++)
        {
            SerializedProperty element = orderProperty.GetArrayElementAtIndex(i);
            ThumbnailProviderType providerType = (ThumbnailProviderType)element.enumValueIndex;
            GUIContent icon = GetProviderTypeIcon(providerType);
            string displayName = GetProviderTypeDisplayName(providerType);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{i + 1}.", GUILayout.Width(20f));
                if (icon?.image != null)
                    GUILayout.Label(icon.image, GUILayout.Width(18f), GUILayout.Height(18f));
                GUILayout.Label(displayName, GUILayout.ExpandWidth(true));

                using (new EditorGUI.DisabledScope(i <= 0))
                {
                    if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        orderProperty.MoveArrayElement(i, i - 1);
                        moved = true;
                    }
                }

                using (new EditorGUI.DisabledScope(i >= orderProperty.arraySize - 1))
                {
                    if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(24f)))
                    {
                        orderProperty.MoveArrayElement(i, i + 1);
                        moved = true;
                    }
                }
            }

            if (moved)
                break;
        }

        if (moved)
            GUI.changed = true;

        EditorGUILayout.HelpBox("Top item has highest priority when multiple providers can render the same asset.", MessageType.None);
    }

    private void EnsureProviderPriorityOrderProperty(SerializedProperty orderProperty)
    {
        List<ThumbnailProviderType> sanitized = new List<ThumbnailProviderType>();
        HashSet<ThumbnailProviderType> seen = new HashSet<ThumbnailProviderType>();

        for (int i = 0; i < orderProperty.arraySize; i++)
        {
            SerializedProperty element = orderProperty.GetArrayElementAtIndex(i);
            if (!TryGetKnownProviderType(element.enumValueIndex, out ThumbnailProviderType providerType))
                continue;

            if (seen.Add(providerType))
                sanitized.Add(providerType);
        }

        List<ThumbnailProviderType> defaultOrder = ImprovedThumbnailSettings.GetDefaultProviderPriorityOrder();
        for (int i = 0; i < defaultOrder.Count; i++)
        {
            ThumbnailProviderType providerType = defaultOrder[i];
            if (seen.Add(providerType))
                sanitized.Add(providerType);
        }

        if (!IsProviderOrderPropertyDifferent(orderProperty, sanitized))
            return;

        orderProperty.arraySize = sanitized.Count;
        for (int i = 0; i < sanitized.Count; i++)
            orderProperty.GetArrayElementAtIndex(i).enumValueIndex = (int)sanitized[i];
    }

    private static bool IsProviderOrderPropertyDifferent(SerializedProperty orderProperty, List<ThumbnailProviderType> sanitized)
    {
        if (orderProperty.arraySize != sanitized.Count)
            return true;

        for (int i = 0; i < sanitized.Count; i++)
        {
            if (orderProperty.GetArrayElementAtIndex(i).enumValueIndex != (int)sanitized[i])
                return true;
        }

        return false;
    }

    private static bool TryGetKnownProviderType(int enumIndex, out ThumbnailProviderType providerType)
    {
        providerType = (ThumbnailProviderType)enumIndex;
        switch (providerType)
        {
            case ThumbnailProviderType.ParticlePrefab:
            case ThumbnailProviderType.UiPrefab:
            case ThumbnailProviderType.SpritePrefab:
            case ThumbnailProviderType.GeneralPrefab:
            case ThumbnailProviderType.ModelAsset:
            case ThumbnailProviderType.MaterialAsset:
                return true;
            default:
                return false;
        }
    }

    private static string GetProviderTypeDisplayName(ThumbnailProviderType providerType)
    {
        switch (providerType)
        {
            case ThumbnailProviderType.ParticlePrefab:
                return "Particle Prefabs";
            case ThumbnailProviderType.UiPrefab:
                return "UI Prefabs";
            case ThumbnailProviderType.SpritePrefab:
                return "Sprite Prefabs";
            case ThumbnailProviderType.GeneralPrefab:
                return "General Prefabs";
            case ThumbnailProviderType.ModelAsset:
                return "Model Assets";
            case ThumbnailProviderType.MaterialAsset:
                return "Materials";
            default:
                return providerType.ToString();
        }
    }

    private static GUIContent GetProviderTypeIcon(ThumbnailProviderType providerType)
    {
        switch (providerType)
        {
            case ThumbnailProviderType.ParticlePrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(ParticleSystem));
            case ThumbnailProviderType.UiPrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(Canvas));
            case ThumbnailProviderType.SpritePrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(SpriteRenderer));
            case ThumbnailProviderType.GeneralPrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(GameObject));
            case ThumbnailProviderType.ModelAsset:
                return EditorGUIUtility.ObjectContent(null, typeof(Mesh));
            case ThumbnailProviderType.MaterialAsset:
                return EditorGUIUtility.ObjectContent(null, typeof(Material));
            default:
                return null;
        }
    }

    private void QueueDeferredDraftPersist()
    {
        if (_deferredPersistQueued)
            return;

        _deferredPersistQueued = true;
        EditorApplication.delayCall += FlushDeferredDraftPersist;
    }

    private void FlushDeferredDraftPersist()
    {
        EditorApplication.delayCall -= FlushDeferredDraftPersist;
        _deferredPersistQueued = false;

        if (!(target is ImprovedThumbnailSettingsAsset settingsAsset))
            return;

        settingsAsset.PersistDraftState();
        RefreshAppliedSnapshot();
        Repaint();
    }

    private void CommitPendingSerializedChanges()
    {
        if (!serializedObject.ApplyModifiedProperties())
            return;

        EditorUtility.SetDirty(target);
        ((ImprovedThumbnailSettingsAsset)target).PersistDraftState();
    }

    private void RefreshAppliedSnapshot()
    {
        ImprovedThumbnailSettingsAsset settingsAsset = (ImprovedThumbnailSettingsAsset)target;
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

    private bool HasPendingChanges()
    {
        return ((ImprovedThumbnailSettingsAsset)target).HasPendingChanges();
    }

    private bool HasNonActivationPendingChanges()
    {
        return ((ImprovedThumbnailSettingsAsset)target).HasPendingChanges();
    }

    private bool IsPropertyPending(string propertyName)
    {
        if (AppliedSettingsUtility.PropertyDiffers(serializedObject, _appliedSerializedObject, propertyName))
            return true;

        if (_appliedSnapshot == null)
            return false;

        FieldInfo field = typeof(ImprovedThumbnailSettingsAsset).GetField(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
            return false;

        object currentVal = field.GetValue(target);
        object appliedVal = field.GetValue(_appliedSnapshot);
        if (currentVal == null && appliedVal == null)
            return false;
        if (currentVal == null || appliedVal == null)
            return true;

        if (currentVal is IList currentList && appliedVal is IList appliedList)
        {
            if (currentList.Count != appliedList.Count)
                return true;

            for (int i = 0; i < currentList.Count; i++)
            {
                if (!Equals(currentList[i], appliedList[i]))
                    return true;
            }

            return false;
        }

        return !currentVal.Equals(appliedVal);
    }

    private bool AreAnyPropertiesPending(params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (IsPropertyPending(propertyName))
                return true;
        }

        return false;
    }

    private PrefabThumbnailService.SettingsApplyImpact DetermineThumbnailApplyImpact()
    {
        PrefabThumbnailService.SettingsApplyImpact impact = PrefabThumbnailService.SettingsApplyImpact.Repaint;
        if (!HasPendingChanges())
            return impact;

        string[] inMemoryProperties =
        {
            "thumbnailRenderPerUpdate",
            "thumbnailRenderBudgetMs",
            "thumbnailCacheMaxSize",
            "thumbnailGridRenderSize",
            "thumbnailListRenderSize",
            "verboseLogging",
            "thumbnailDrawInProjectGrid",
            "thumbnailDrawInProjectList",
            "thumbnailDrawInObjectPicker",
            "thumbnailMinProjectIconSize",
            "showThumbnailBadges",
            "thumbnailBadgeScale",
            "showListViewTypeLabel",
            "listViewBadgeHorizontalOffset",
            "thumbnailGroundGridHalfSize",
            "thumbnailGroundGridStep",
            "thumbnailGroundGridAlpha",
        };

        string[] persistentVersionedProperties =
        {
            "thumbnailCameraFov",
            "thumbnailCameraYaw",
            "thumbnailCameraPitch",
            "thumbnailSkyboxTexture",
            "thumbnailBoundsPadding",
            "thumbnailGridRenderSize",
            "thumbnailListRenderSize",
            "particleThumbnailMotionPadding",
            "particleScanMaxSeconds",
            "enableParticleProvider",
            "enableUiProvider",
            "enableSpriteProvider",
            "enableGeneralProvider",
            "enableModelProvider",
            "enableMaterialProvider",
            "providerPriorityOrder",
            "generalThumbnailLightIntensity",
            "modelThumbnailLightIntensity",
            "modelThumbnailBoundsPadding",
            "modelThumbnailVerticalBias",
            "spriteThumbnailFrontView",
            "spriteThumbnailLightIntensity",
            "thumbnailGroundGridHalfSize",
            "thumbnailGroundGridStep",
            "thumbnailGroundGridAlpha",
        };

        if (AreAnyPropertiesPending(inMemoryProperties))
            impact |= PrefabThumbnailService.SettingsApplyImpact.ClearInMemoryCache;

        if (AreAnyPropertiesPending(persistentVersionedProperties))
            impact |= PrefabThumbnailService.SettingsApplyImpact.VersionPersistentCache;

        return impact;
    }

    private void ApplyImmediateActiveToggle(ImprovedThumbnailSettingsAsset settingsAsset)
    {
        if (settingsAsset == null)
            return;

        RefreshAppliedSnapshot();
        if (_appliedSnapshot != null && _appliedSerializedObject != null)
        {
            SerializedProperty currentActiveProperty = serializedObject.FindProperty("isActive");
            SerializedProperty appliedActiveProperty = _appliedSerializedObject.FindProperty("isActive");
            if (currentActiveProperty != null && appliedActiveProperty != null)
            {
                _appliedSerializedObject.Update();
                appliedActiveProperty.boolValue = currentActiveProperty.boolValue;
                _appliedSerializedObject.ApplyModifiedPropertiesWithoutUndo();

                settingsAsset.appliedSettingsJson = AppliedSettingsUtility.CaptureSnapshotJson(_appliedSnapshot);
                settingsAsset.appliedRevision = Mathf.Max(1, settingsAsset.appliedRevision + 1);
                EditorUtility.SetDirty(settingsAsset);
                settingsAsset.PersistDraftState();
                ImprovedThumbnailSettings.InvalidateAppliedSnapshotCache();
            }
        }

        PrefabThumbnailService.SetActive(settingsAsset.isActive);
    }

    private bool CanResetProperty(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || _defaultValues == null)
            return false;

        FieldInfo field = typeof(ImprovedThumbnailSettingsAsset).GetField(
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
                return property.boolValue == (bool)defaultValue;
            case SerializedPropertyType.Integer:
                return property.intValue == (int)defaultValue;
            case SerializedPropertyType.Float:
                return Mathf.Approximately(property.floatValue, (float)defaultValue);
            case SerializedPropertyType.Color:
                return property.colorValue.Equals((Color)defaultValue);
            case SerializedPropertyType.Vector2:
                return property.vector2Value == (Vector2)defaultValue;
            case SerializedPropertyType.Vector3:
                return property.vector3Value == (Vector3)defaultValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue == (Object)defaultValue;
            case SerializedPropertyType.Enum:
                return property.intValue == System.Convert.ToInt32(defaultValue);
            default:
                return true;
        }
    }

    private void ResetPropertyToDefault(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || _defaultValues == null)
            return;

        FieldInfo field = typeof(ImprovedThumbnailSettingsAsset).GetField(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
            return;

        Undo.RecordObject(target, $"Reset {propertyName}");
        object defaultValue = field.GetValue(_defaultValues);

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                property.boolValue = (bool)defaultValue;
                break;
            case SerializedPropertyType.Integer:
                property.intValue = (int)defaultValue;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = (float)defaultValue;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = (Color)defaultValue;
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = (Vector2)defaultValue;
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = (Vector3)defaultValue;
                break;
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = defaultValue as Object;
                break;
            case SerializedPropertyType.Enum:
                property.intValue = System.Convert.ToInt32(defaultValue);
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

    private static string BuildTabLabel(string title, bool hasPendingChanges)
    {
        return hasPendingChanges ? title + " *" : title;
    }
}
public static class ImprovedThumbnailSettings
{
    private const string ProjectSettingsFilePath = "ProjectSettings/ImprovedAssetTools/ImprovedThumbnailSettings.asset";
    private const string ScriptFileName = "ImprovedThumbnailSettingsAsset.cs";
    private const string DefaultSkyboxFileName = "HDRI.jpg";
    private const string SecondarySkyboxFileName = "SkyboxPreview.png";
    private const string LegacyDefaultSkyboxFileName = "ImprovedThumbnailSkybox.jpg";
    private const string DefaultSkyboxPreviewGuid = "83fb20c8d36eb4afe9b376da509dc0d6";
    private const string LegacyThumbnailSkyboxGuid = "d5e4a01407b83475e8830da736bbe949";

    public const bool D_IsActive = true;
    public const int D_ThumbnailGridRenderSize = 128;
    public const int D_ThumbnailListRenderSize = 32;
    public const float D_ThumbnailCameraFov = 30f;
    public const float D_ThumbnailCameraYaw = 35f;
    public const float D_ThumbnailCameraPitch = 25f;
    public const float D_ThumbnailBoundsPadding = 0.15f;
    public static readonly Color D_ThumbnailBackgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    public const int D_ThumbnailRenderPerUpdate = 1;
    public const float D_ThumbnailRenderBudgetMs = 16f;
    public const int D_ThumbnailCacheMaxSize = 200;
    public const bool D_VerboseLogging = false;
    public const int D_ThumbnailMinProjectIconSize = 40;
    public const bool D_ThumbnailDrawInProjectGrid = true;
    public const bool D_ThumbnailDrawInProjectList = true;
    public const bool D_ThumbnailDrawInObjectPicker = true;

    public const float D_ThumbnailGroundGridHalfSize = 7f;
    public const float D_ThumbnailGroundGridStep = 0.5f;
    public const float D_ThumbnailGroundGridAlpha = 0.05f;
    public const float D_GeneralThumbnailLightIntensity = 1.3f;
    public const float D_ModelThumbnailLightIntensity = 1.3f;
    public const float D_ModelThumbnailBoundsPadding = 0.2f;
    public const float D_ModelThumbnailVerticalBias = 0.08f;
    public const float D_SpriteThumbnailLightIntensity = 1.1f;
    public const bool D_SpriteThumbnailFrontView = true;
    public const float D_ParticleThumbnailMotionPadding = 0.2f;
    public const float D_ParticleScanMaxSeconds = 3.0f;
    public const bool D_EnableParticleProvider = true;
    public const bool D_EnableUiProvider = true;
    public const bool D_EnableSpriteProvider = true;
    public const bool D_EnableGeneralProvider = true;
    public const bool D_EnableModelProvider = true;
    public const bool D_EnableMaterialProvider = true;
    public const string D_MaterialFallbackCacheVersion = "material-fallback-v1";
    private static readonly ThumbnailProviderType[] D_ProviderPriorityOrder =
    {
        ThumbnailProviderType.ParticlePrefab,
        ThumbnailProviderType.UiPrefab,
        ThumbnailProviderType.SpritePrefab,
        ThumbnailProviderType.GeneralPrefab,
        ThumbnailProviderType.ModelAsset,
        ThumbnailProviderType.MaterialAsset,
    };
    public const bool D_ShowThumbnailBadges = true;
    public const float D_ThumbnailBadgeScale = 0.8f;
    public const bool D_ShowListViewTypeLabel = true;
    public const float D_ListViewBadgeHorizontalOffset = -12f;
    public const bool D_ShowSelectionFrame = true;
    public static readonly Color D_SelectionFrameColor = new Color(0.5019608f, 0.8392157f, 0.9882353f, 1f);
    public static readonly Color D_UnselectedGridFrameColor = new Color(0.3f, 0.3f, 0.3f, 1f);

    private static ImprovedThumbnailSettingsAsset _cachedAsset;
    private static ImprovedThumbnailSettingsAsset _cachedAppliedAsset;
    private static string _cachedAppliedJson;
    private static bool _legacyMigrationChecked;
    private static UnityEditor.Editor _settingsEditor;

    public static bool Active => AppliedAsset.isActive;
    public static bool HasUguiSupport
    {
        get
        {
            return System.Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI") != null
                && System.Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI") != null
                && System.Type.GetType("UnityEngine.UI.LayoutRebuilder, UnityEngine.UI") != null;
        }
    }
    public static int GridRenderSize => Mathf.Clamp(AppliedAsset.thumbnailGridRenderSize, 32, 512);
    public static int ListRenderSize => Mathf.Clamp(AppliedAsset.thumbnailListRenderSize, 16, 128);
    public static int ThumbnailSize => GridRenderSize;
    public static int GetThumbnailSize(ThumbnailSurface surface) => surface == ThumbnailSurface.ProjectWindowList ? ListRenderSize : GridRenderSize;
    public static float ThumbnailCameraFov => AppliedAsset.thumbnailCameraFov;
    public static float ThumbnailCameraYaw
    {
        get
        {
            return Mathf.Clamp(AppliedAsset.thumbnailCameraYaw, -180f, 180f);
        }
    }
    public static float ThumbnailCameraPitch => Mathf.Clamp(AppliedAsset.thumbnailCameraPitch, -89f, 89f);
    public static Texture ThumbnailSkyboxTexture => AppliedAsset.thumbnailSkyboxTexture;
    public static float ThumbnailBoundsPadding => AppliedAsset.thumbnailBoundsPadding;
    public static Color ThumbnailBackgroundColor => AppliedAsset.thumbnailBackgroundColor;
    public static Color RenderBackgroundColor => ThumbnailBackgroundColor;
    public static int ThumbnailRenderPerUpdate => Mathf.Max(1, AppliedAsset.thumbnailRenderPerUpdate);
    public static float ThumbnailRenderBudgetMs => Mathf.Clamp(AppliedAsset.thumbnailRenderBudgetMs, 1f, 100f);
    public static int ThumbnailCacheMaxSize => Mathf.Max(10, AppliedAsset.thumbnailCacheMaxSize);
    public static bool VerboseLogging => AppliedAsset.verboseLogging;
    public static int ThumbnailMinProjectIconSize => Mathf.Max(16, AppliedAsset.thumbnailMinProjectIconSize);
    public static bool ThumbnailDrawInProjectGrid => AppliedAsset.thumbnailDrawInProjectGrid;
    public static bool ThumbnailDrawInProjectList => AppliedAsset.thumbnailDrawInProjectList;
    public static bool ThumbnailDrawInObjectPicker => AppliedAsset.thumbnailDrawInObjectPicker;
    public static float ThumbnailGridPadding => 0f;
    public static float ThumbnailListPadding => 0f;
    public static float ThumbnailGroundGridHalfSize => Mathf.Max(0.25f, AppliedAsset.thumbnailGroundGridHalfSize);
    public static float ThumbnailGroundGridStep => Mathf.Max(0.05f, AppliedAsset.thumbnailGroundGridStep);
    public static float ThumbnailGroundGridAlpha => Mathf.Clamp01(AppliedAsset.thumbnailGroundGridAlpha);
    public static float GeneralThumbnailYaw => ThumbnailCameraYaw;
    public static float GeneralThumbnailPitch => ThumbnailCameraPitch;
    public static float GeneralThumbnailLightIntensity => AppliedAsset.generalThumbnailLightIntensity;
    public static float ModelThumbnailYaw => ThumbnailCameraYaw;
    public static float ModelThumbnailPitch => ThumbnailCameraPitch;
    public static float ModelThumbnailLightIntensity => AppliedAsset.modelThumbnailLightIntensity;
    public static float ModelThumbnailBoundsPadding => AppliedAsset.modelThumbnailBoundsPadding;
    public static float ModelThumbnailVerticalBias => AppliedAsset.modelThumbnailVerticalBias;
    public static float SpriteThumbnailLightIntensity => AppliedAsset.spriteThumbnailLightIntensity;
    public static bool SpriteThumbnailFrontView => AppliedAsset.spriteThumbnailFrontView;
    public static float ParticleThumbnailMotionPadding => AppliedAsset.particleThumbnailMotionPadding;
    public static float ParticleScanMaxSeconds => Mathf.Clamp(AppliedAsset.particleScanMaxSeconds, 0.5f, 10f);
    public static bool EnableParticleThumbnailProvider => AppliedAsset.enableParticleProvider;
    public static bool EnableUiThumbnailProvider => HasUguiSupport && AppliedAsset.enableUiProvider;
    public static bool EnableSpriteThumbnailProvider => AppliedAsset.enableSpriteProvider;
    public static bool EnableGeneralThumbnailProvider => AppliedAsset.enableGeneralProvider;
    public static bool EnableModelThumbnailProvider => AppliedAsset.enableModelProvider;
    public static bool EnableMaterialThumbnailProvider => AppliedAsset.enableMaterialProvider;
    public static int ParticleThumbnailProviderPriority => GetProviderPriority(ThumbnailProviderType.ParticlePrefab);
    public static int UiThumbnailProviderPriority => GetProviderPriority(ThumbnailProviderType.UiPrefab);
    public static int SpriteThumbnailProviderPriority => GetProviderPriority(ThumbnailProviderType.SpritePrefab);
    public static int GeneralThumbnailProviderPriority => GetProviderPriority(ThumbnailProviderType.GeneralPrefab);
    public static int ModelThumbnailProviderPriority => GetProviderPriority(ThumbnailProviderType.ModelAsset);
    public static int MaterialThumbnailProviderPriority => GetProviderPriority(ThumbnailProviderType.MaterialAsset);
    public static bool ShowThumbnailBadges => AppliedAsset.showThumbnailBadges;
    public static float ThumbnailBadgeScale => Mathf.Clamp(AppliedAsset.thumbnailBadgeScale, 0.5f, 2f);
    public static bool ShowListViewTypeLabel => AppliedAsset.showListViewTypeLabel;
    public static float ListViewBadgeHorizontalOffset => AppliedAsset.listViewBadgeHorizontalOffset;
    public static bool ShowSelectionFrame => AppliedAsset.showSelectionFrame;
    public static Color SelectionFrameColor => AppliedAsset.selectionFrameColor;
    public static Color UnselectedGridFrameColor => AppliedAsset.unselectedGridFrameColor;
    public static int AppliedRevision => Mathf.Max(1, Asset.appliedRevision);

    public static List<ThumbnailProviderType> GetDefaultProviderPriorityOrder()
    {
        return new List<ThumbnailProviderType>(D_ProviderPriorityOrder);
    }

    private static int GetProviderPriority(ThumbnailProviderType providerType)
    {
        ImprovedThumbnailSettingsAsset asset = AppliedAsset;
        if (asset?.providerPriorityOrder == null)
            return GetDefaultProviderPriority(providerType);

        for (int i = 0; i < asset.providerPriorityOrder.Count; i++)
        {
            if (asset.providerPriorityOrder[i] == providerType)
                return i;
        }

        return GetDefaultProviderPriority(providerType);
    }

    private static int GetDefaultProviderPriority(ThumbnailProviderType providerType)
    {
        for (int i = 0; i < D_ProviderPriorityOrder.Length; i++)
        {
            if (D_ProviderPriorityOrder[i] == providerType)
                return i;
        }

        return int.MaxValue;
    }

    private static bool EnsureProviderPriorityOrder(ImprovedThumbnailSettingsAsset asset)
    {
        if (asset == null)
            return false;

        if (asset.providerPriorityOrder == null)
            asset.providerPriorityOrder = new List<ThumbnailProviderType>();

        List<ThumbnailProviderType> sanitized = new List<ThumbnailProviderType>();
        HashSet<ThumbnailProviderType> seen = new HashSet<ThumbnailProviderType>();

        for (int i = 0; i < asset.providerPriorityOrder.Count; i++)
        {
            ThumbnailProviderType providerType = asset.providerPriorityOrder[i];
            if (!IsKnownProviderType(providerType))
                continue;

            if (seen.Add(providerType))
                sanitized.Add(providerType);
        }

        for (int i = 0; i < D_ProviderPriorityOrder.Length; i++)
        {
            ThumbnailProviderType providerType = D_ProviderPriorityOrder[i];
            if (seen.Add(providerType))
                sanitized.Add(providerType);
        }

        if (sanitized.Count == asset.providerPriorityOrder.Count)
        {
            bool matches = true;
            for (int i = 0; i < sanitized.Count; i++)
            {
                if (sanitized[i] != asset.providerPriorityOrder[i])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return false;
        }

        asset.providerPriorityOrder = sanitized;
        return true;
    }

    private static bool IsKnownProviderType(ThumbnailProviderType providerType)
    {
        for (int i = 0; i < D_ProviderPriorityOrder.Length; i++)
        {
            if (D_ProviderPriorityOrder[i] == providerType)
                return true;
        }

        return false;
    }

    [MenuItem("Window/Improved Asset Tools/Settings/Improved Thumbnail")]
    public static void SelectSettingsAsset()
    {
        SettingsService.OpenProjectSettings("Project/Improved Asset Tools/Improved Thumbnail");
    }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        return new SettingsProvider("Project/Improved Asset Tools/Improved Thumbnail", SettingsScope.Project)
        {
            label = "Improved Thumbnail",
            guiHandler = _ => DrawSettingsProviderGui(),
            keywords = new HashSet<string>
            {
                "thumbnail",
                "prefab",
                "model",
                "material",
                "particle",
                "preview",
                "asset tools",
            },
        };
    }

    private static void DrawSettingsProviderGui()
    {
        ImprovedThumbnailSettingsAsset asset = Asset;
        if (_settingsEditor == null || _settingsEditor.target != asset)
        {
            if (_settingsEditor != null)
                Object.DestroyImmediate(_settingsEditor);

            _settingsEditor = UnityEditor.Editor.CreateEditor(asset);
        }

        _settingsEditor?.OnInspectorGUI();
    }

    public static ImprovedThumbnailSettingsAsset Asset
    {
        get
        {
            if (_cachedAsset == null)
                _cachedAsset = LoadDraftAsset();

            TryMigrateLegacySettingsIfNeeded(_cachedAsset);
            EnsureDefaultThumbnailSkyboxAssigned(_cachedAsset);
            if (EnsureProviderPriorityOrder(_cachedAsset))
                SaveDraftState(_cachedAsset);
            _cachedAsset.EnsureAppliedSnapshotInitialized();
            return _cachedAsset;
        }
    }

    internal static ImprovedThumbnailSettingsAsset AppliedAsset
    {
        get
        {
            ImprovedThumbnailSettingsAsset asset = Asset;
            string appliedJson = asset.appliedSettingsJson ?? string.Empty;
            if (_cachedAppliedAsset == null || _cachedAppliedJson != appliedJson)
            {
                if (_cachedAppliedAsset != null)
                    Object.DestroyImmediate(_cachedAppliedAsset);

                _cachedAppliedAsset = AppliedSettingsUtility.CreateAppliedClone(asset, appliedJson);
                EnsureProviderPriorityOrder(_cachedAppliedAsset);
                _cachedAppliedJson = appliedJson;
            }

            return _cachedAppliedAsset ?? asset;
        }
    }

    internal static void InvalidateAppliedSnapshotCache()
    {
        _cachedAppliedJson = string.Empty;
        if (_cachedAppliedAsset != null)
        {
            Object.DestroyImmediate(_cachedAppliedAsset);
            _cachedAppliedAsset = null;
        }
    }

    internal static void SaveDraftState(ImprovedThumbnailSettingsAsset asset)
    {
        if (asset == null)
            return;

        ImprovedThumbnailSettingsStorage storage = ImprovedThumbnailSettingsStorage.instance;
        string json = EditorJsonUtility.ToJson(asset, false);
        if (storage.SettingsJson == json)
            return;

        storage.SettingsJson = json;
        storage.SaveStorage();
        _cachedAsset = asset;
    }

    internal static string GetPersistentCacheSettingsToken()
    {
        ImprovedThumbnailSettingsAsset asset = AppliedAsset;
        string thumbnailSkyboxGuid = string.Empty;
        if (asset.thumbnailSkyboxTexture != null)
        {
            string skyboxPath = AssetDatabase.GetAssetPath(asset.thumbnailSkyboxTexture);
            if (!string.IsNullOrEmpty(skyboxPath))
                thumbnailSkyboxGuid = AssetDatabase.AssetPathToGUID(skyboxPath);
        }

        string providerOrderToken = string.Empty;
        if (asset.providerPriorityOrder != null && asset.providerPriorityOrder.Count > 0)
            providerOrderToken = string.Join(",", asset.providerPriorityOrder);

        string payload =
            $"{asset.thumbnailCameraFov}|{asset.thumbnailCameraYaw}|{asset.thumbnailCameraPitch}|{thumbnailSkyboxGuid}|{asset.thumbnailBoundsPadding}|{asset.thumbnailGridRenderSize}|{asset.thumbnailListRenderSize}|{asset.particleThumbnailMotionPadding}|{asset.particleScanMaxSeconds}|{asset.enableParticleProvider}|{asset.enableUiProvider}|{asset.enableSpriteProvider}|{asset.enableGeneralProvider}|{asset.enableModelProvider}|{asset.enableMaterialProvider}|{providerOrderToken}|{asset.generalThumbnailLightIntensity}|{asset.modelThumbnailLightIntensity}|{asset.modelThumbnailBoundsPadding}|{asset.modelThumbnailVerticalBias}|{asset.spriteThumbnailFrontView}|{asset.spriteThumbnailLightIntensity}|{asset.thumbnailGroundGridHalfSize}|{asset.thumbnailGroundGridStep}|{asset.thumbnailGroundGridAlpha}|{D_MaterialFallbackCacheVersion}";
        return Hash128.Compute(payload).ToString();
    }

    public static void ApplyDefaults(ImprovedThumbnailSettingsAsset asset)
    {
        if (asset == null)
            return;

        asset.isActive = D_IsActive;
        asset.thumbnailCameraFov = D_ThumbnailCameraFov;
        asset.thumbnailCameraYaw = D_ThumbnailCameraYaw;
        asset.thumbnailCameraPitch = D_ThumbnailCameraPitch;
        asset.thumbnailSkyboxTexture = LoadDefaultThumbnailSkyboxTexture();
        asset.thumbnailBoundsPadding = D_ThumbnailBoundsPadding;
        asset.thumbnailGridRenderSize = D_ThumbnailGridRenderSize;
        asset.thumbnailListRenderSize = D_ThumbnailListRenderSize;
        asset.thumbnailBackgroundColor = D_ThumbnailBackgroundColor;
        asset.thumbnailRenderPerUpdate = D_ThumbnailRenderPerUpdate;
        asset.thumbnailRenderBudgetMs = D_ThumbnailRenderBudgetMs;
        asset.thumbnailCacheMaxSize = D_ThumbnailCacheMaxSize;
        asset.verboseLogging = D_VerboseLogging;
        asset.thumbnailMinProjectIconSize = D_ThumbnailMinProjectIconSize;
        asset.thumbnailDrawInProjectGrid = D_ThumbnailDrawInProjectGrid;
        asset.thumbnailDrawInProjectList = D_ThumbnailDrawInProjectList;
        asset.thumbnailDrawInObjectPicker = D_ThumbnailDrawInObjectPicker;

        asset.thumbnailGroundGridHalfSize = D_ThumbnailGroundGridHalfSize;
        asset.thumbnailGroundGridStep = D_ThumbnailGroundGridStep;
        asset.thumbnailGroundGridAlpha = D_ThumbnailGroundGridAlpha;
        asset.generalThumbnailLightIntensity = D_GeneralThumbnailLightIntensity;
        asset.modelThumbnailLightIntensity = D_ModelThumbnailLightIntensity;
        asset.modelThumbnailBoundsPadding = D_ModelThumbnailBoundsPadding;
        asset.modelThumbnailVerticalBias = D_ModelThumbnailVerticalBias;
        asset.spriteThumbnailLightIntensity = D_SpriteThumbnailLightIntensity;
        asset.spriteThumbnailFrontView = D_SpriteThumbnailFrontView;
        asset.particleThumbnailMotionPadding = D_ParticleThumbnailMotionPadding;
        asset.particleScanMaxSeconds = D_ParticleScanMaxSeconds;
        asset.enableParticleProvider = D_EnableParticleProvider;
        asset.enableUiProvider = D_EnableUiProvider;
        asset.enableSpriteProvider = D_EnableSpriteProvider;
        asset.enableGeneralProvider = D_EnableGeneralProvider;
        asset.enableModelProvider = D_EnableModelProvider;
        asset.enableMaterialProvider = D_EnableMaterialProvider;
        asset.providerPriorityOrder = GetDefaultProviderPriorityOrder();
        asset.showThumbnailBadges = D_ShowThumbnailBadges;
        asset.thumbnailBadgeScale = D_ThumbnailBadgeScale;
        asset.showListViewTypeLabel = D_ShowListViewTypeLabel;
        asset.listViewBadgeHorizontalOffset = D_ListViewBadgeHorizontalOffset;
        asset.showSelectionFrame = D_ShowSelectionFrame;
        asset.selectionFrameColor = D_SelectionFrameColor;
        asset.unselectedGridFrameColor = D_UnselectedGridFrameColor;
    }

    private static Texture LoadDefaultThumbnailSkyboxTexture()
    {
        string commonHdriPath = CombineAssetPath(GetToolRootDirectory(), $"Common/Skybox/{DefaultSkyboxFileName}");
        string commonPreviewPath = CombineAssetPath(GetToolRootDirectory(), $"Common/Skybox/{SecondarySkyboxFileName}");
        string legacyPath = CombineAssetPath(GetSystemRootDirectory(), $"Skybox/{LegacyDefaultSkyboxFileName}");
        string[] guidCandidates = { DefaultSkyboxPreviewGuid, LegacyThumbnailSkyboxGuid };
        string[] pathCandidates = { commonHdriPath, commonPreviewPath, legacyPath };

        if (ScriptRelativeAssetUtility.TryLoadFirstByGuids(guidCandidates, out Cubemap guidCubemap))
            return guidCubemap;
        if (ScriptRelativeAssetUtility.TryLoadFirstByGuids(guidCandidates, out Texture guidTexture))
            return guidTexture;

        if (ScriptRelativeAssetUtility.TryLoadFirstAtPaths(pathCandidates, out Cubemap pathCubemap))
            return pathCubemap;
        if (ScriptRelativeAssetUtility.TryLoadFirstAtPaths(pathCandidates, out Texture pathTexture))
            return pathTexture;

        if (ScriptRelativeAssetUtility.TryFindAssetByFileName(DefaultSkyboxFileName, "/Common/Skybox/", out Cubemap commonHdriCubemap))
            return commonHdriCubemap;
        if (ScriptRelativeAssetUtility.TryFindAssetByFileName(DefaultSkyboxFileName, "/Common/Skybox/", out Texture commonHdriTexture))
            return commonHdriTexture;
        if (ScriptRelativeAssetUtility.TryFindAssetByFileName(SecondarySkyboxFileName, "/Common/Skybox/", out Cubemap commonPreviewCubemap))
            return commonPreviewCubemap;
        if (ScriptRelativeAssetUtility.TryFindAssetByFileName(SecondarySkyboxFileName, "/Common/Skybox/", out Texture commonPreviewTexture))
            return commonPreviewTexture;
        if (ScriptRelativeAssetUtility.TryFindAssetByFileName(LegacyDefaultSkyboxFileName, "/ImprovedThumbnail/Skybox/", out Cubemap legacyCubemap))
            return legacyCubemap;
        if (ScriptRelativeAssetUtility.TryFindAssetByFileName(LegacyDefaultSkyboxFileName, "/ImprovedThumbnail/Skybox/", out Texture legacyTexture))
            return legacyTexture;

        return null;
    }

    private static void EnsureDefaultThumbnailSkyboxAssigned(ImprovedThumbnailSettingsAsset asset)
    {
        if (asset == null)
            return;

        Texture defaultSkybox = LoadDefaultThumbnailSkyboxTexture();
        if (defaultSkybox == null)
            return;

        bool changed = false;
        if (!ScriptRelativeAssetUtility.IsValidAssetReference(asset.thumbnailSkyboxTexture))
        {
            asset.thumbnailSkyboxTexture = defaultSkybox;
            changed = true;
        }

        string appliedJson = asset.appliedSettingsJson ?? string.Empty;
        if (!string.IsNullOrEmpty(appliedJson))
        {
            ImprovedThumbnailSettingsAsset appliedSnapshot = AppliedSettingsUtility.CreateAppliedClone(asset, appliedJson);
            if (appliedSnapshot != null)
            {
                if (!ScriptRelativeAssetUtility.IsValidAssetReference(appliedSnapshot.thumbnailSkyboxTexture))
                {
                    appliedSnapshot.thumbnailSkyboxTexture = defaultSkybox;
                    asset.appliedSettingsJson = AppliedSettingsUtility.CaptureSnapshotJson(appliedSnapshot);
                    asset.appliedRevision = Mathf.Max(1, asset.appliedRevision + 1);
                    changed = true;
                }

                Object.DestroyImmediate(appliedSnapshot);
            }
        }

        if (changed)
            SaveDraftState(asset);
    }

    private static string GetScriptDirectory()
    {
        return ScriptRelativeAssetUtility.GetScriptDirectory(
            ScriptFileName,
            "Assets/ImprovedAssetTools/ImprovedThumbnail/Editor");
    }

    private static string GetSystemRootDirectory()
    {
        return ScriptRelativeAssetUtility.GetParentAssetPath(GetScriptDirectory());
    }

    private static string GetToolRootDirectory()
    {
        return ScriptRelativeAssetUtility.GetParentAssetPath(GetSystemRootDirectory());
    }

    private static string CombineAssetPath(string left, string right)
    {
        return ScriptRelativeAssetUtility.CombineAssetPath(left, right);
    }

    private static void TryMigrateLegacySettingsIfNeeded(ImprovedThumbnailSettingsAsset asset)
    {
        if (_legacyMigrationChecked || asset == null)
            return;

        _legacyMigrationChecked = true;

        ImprovedThumbnailSettingsStorage storage = ImprovedThumbnailSettingsStorage.instance;
        if (!string.IsNullOrEmpty(storage.SettingsJson))
            return;

        string[] guids = AssetDatabase.FindAssets("t:ImprovedThumbnailSettingsAsset");
        for (int i = 0; i < guids.Length; i++)
        {
            string legacyPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(legacyPath) || !legacyPath.StartsWith("Assets/ImprovedAssetToolsSettings"))
                continue;

            ImprovedThumbnailSettingsAsset legacyAsset = AssetDatabase.LoadAssetAtPath<ImprovedThumbnailSettingsAsset>(legacyPath);
            if (legacyAsset == null || legacyAsset == asset)
                continue;

            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(legacyAsset, false), asset);
            asset.EnsureAppliedSnapshotInitialized();
            asset.PersistDraftState();
            InvalidateAppliedSnapshotCache();
            break;
        }
    }

    private static ImprovedThumbnailSettingsAsset LoadDraftAsset()
    {
        ImprovedThumbnailSettingsAsset asset = ScriptableObject.CreateInstance<ImprovedThumbnailSettingsAsset>();
        asset.hideFlags =
            HideFlags.HideInHierarchy |
            HideFlags.DontSaveInEditor |
            HideFlags.DontSaveInBuild |
            HideFlags.DontUnloadUnusedAsset;
        ApplyDefaults(asset);

        string settingsJson = ImprovedThumbnailSettingsStorage.instance.SettingsJson;
        if (!string.IsNullOrEmpty(settingsJson))
            EditorJsonUtility.FromJsonOverwrite(settingsJson, asset);

        EnsureDefaultThumbnailSkyboxAssigned(asset);

        return asset;
    }
}
}
#endif
