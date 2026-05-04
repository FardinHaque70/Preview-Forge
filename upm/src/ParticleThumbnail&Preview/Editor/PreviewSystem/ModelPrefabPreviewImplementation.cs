using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Coordinates model-prefab preview lifecycle calls and delegates rendering responsibilities to the dedicated model preview session.

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ModelPrefabPreviewImplementation : IPrefabPreviewImplementation
    {
        private static readonly string[] ToolbarIconRoots =
        {
            "Assets/ParticleThumbnail&Preview/Editor/Common/PreviewAssets/ToolbarIcons",
            "Packages/com.fardinhaque.particle-thumbnail-preview/ParticleThumbnail&Preview/Editor/Common/PreviewAssets/ToolbarIcons",
        };
        private static readonly string[] TurntableIcons = BuildIconNames("Model_Turntable_Round_White.png", "d_SceneViewTools", "SceneViewTools", "d_RotateTool", "RotateTool");
        private static readonly string[] InfoIcons = BuildIconNames("Model_Info_Round_White.png", "d_SelectionList Icon", "SelectionList Icon", "d_Search Icon", "Search Icon");
        private static readonly string[] LightsIcons = BuildIconNames("Model_Lightbulb_Round_White.png", "d_Light Icon", "Light Icon", "d_SceneViewLighting", "SceneViewLighting");
        private static readonly string[] ColliderIcons = BuildIconNames("Model_Collider_Round_White.png", "d_BoxCollider Icon", "BoxCollider Icon", "d_EditCollider", "EditCollider");
        private static readonly string[] LightGizmoIcons = BuildIconNames("Model_LightGizmo_Round_White.png", "d_PreMatSphere", "PreMatSphere", "d_SceneViewTools", "SceneViewTools");
        private static readonly string[] GridIcons = BuildIconNames("Model_GridOn_Round_White.png", "d_Grid Icon", "Grid Icon", "d_Grid.Default", "Grid.Default");
        private static readonly string[] SkyboxIcons = BuildIconNames("Model_Panorama_Round_White.png", "d_Cubemap Icon", "Cubemap Icon", "d_PreMatSphere", "PreMatSphere");
        private static readonly string[] VisualDefaultIcons = BuildIconNames("Model_Texture_Round_White.png", "d_Texture Icon", "Texture Icon");
        private static readonly string[] VisualMatcapIcons = BuildIconNames("Model_Matcap_Round_White.png", "d_PreMatSphere", "PreMatSphere", "d_Material Icon", "Material Icon");

        private readonly ModelPrefabPreviewSession _session = new();
        private readonly List<PreviewToolbarItem> _toolbarItems;
        private readonly bool _showColliderToggle;
        private readonly int _turntableIndex;
        private readonly int _infoIndex;
        private readonly int _lightsIndex;
        private readonly int _collidersIndex;
        private readonly int _gridIndex;
        private readonly int _lightGizmoIndex;
        private readonly int _skyboxIndex;
        private readonly int _visualModeIndex;
        private readonly int _modeIndex;
        private Action _requestRepaint;
        private bool _updateRegistered;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Model;

        internal ModelPrefabPreviewImplementation(bool showColliderToggle = true)
        {
            _showColliderToggle = showColliderToggle;

            int index = 0;
            _turntableIndex = index++;
            _infoIndex = index++;
            _lightsIndex = index++;
            _collidersIndex = _showColliderToggle ? index++ : -1;
            _gridIndex = index++;
            _lightGizmoIndex = index++;
            _skyboxIndex = index++;
            _visualModeIndex = index++;
            _modeIndex = index++;
            _toolbarItems = new List<PreviewToolbarItem>(index);
        }

        private static string[] BuildIconNames(string fileName, params string[] fallbacks)
        {
            var values = new List<string>(ToolbarIconRoots.Length + (fallbacks?.Length ?? 0));
            for (int i = 0; i < ToolbarIconRoots.Length; i++)
                values.Add(ToolbarIconRoots[i] + "/" + fileName);
            if (fallbacks != null && fallbacks.Length > 0)
                values.AddRange(fallbacks);
            return values.ToArray();
        }

        public void SetRepaintCallback(Action repaintCallback)
        {
            _requestRepaint = repaintCallback;
        }

        public bool EnsureReady(GameObject prefab)
        {
            _session.Setup(prefab);
            return _session.IsReady;
        }

        internal void SetPreviewAnimationClip(AnimationClip clip)
        {
            _session.SetPreviewAnimationClip(clip);
        }

        public void Cleanup(bool selectionIsEmpty)
        {
            DisableUpdate();
            if (selectionIsEmpty)
                ModelPrefabPreviewSession.ClearSessionStateCache();

            _session.Cleanup(cacheState: !selectionIsEmpty);
            PreviewDiagnostics.Log("ModelImpl", $"Cleanup selectionEmpty={selectionIsEmpty}");
        }

        public GUIContent GetPreviewTitle(GameObject prefab)
        {
            string prefabName = prefab != null ? prefab.name : null;
            return string.IsNullOrEmpty(prefabName) ? new GUIContent("Model Preview") : new GUIContent(prefabName);
        }

        public void Draw(Rect rect, GUIStyle background, bool isInteractive)
        {
            if (!_session.IsReady)
                return;

            Rect previewRect = DrawToolbar(rect);
            bool inputChanged = _session.HandleInput(previewRect, Event.current);
            bool cameraChanged = _session.TickInteraction();
            _session.Draw(previewRect, background);

            if (_session.InfoEnabled)
                DrawInfoPanel(previewRect);

            if (inputChanged || cameraChanged)
                RequestRepaint();

            if (_session.HasPendingCameraMotion || _session.HasPendingAnimationPlayback)
                EnableUpdate();
            else
                DisableUpdate();
        }

        #region Toolbar
        private Rect DrawToolbar(Rect fullRect)
        {
            EnsureToolbarItems();
            UpdateToolbarItemState();

            PreviewToolbarMetrics metrics = PreviewToolbarMetrics.FromSettings();
            return PreviewToolbarRenderer.Draw(fullRect, PreviewToolbarLayoutPreset.EqualGrid, _toolbarItems, metrics);
        }

        private void EnsureToolbarItems()
        {
            if (_toolbarItems.Count > 0)
                return;

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Button)
            {
                OnClick = OnTurntableClicked,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnInfoToggled,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnLightsToggled,
            });

            if (_showColliderToggle)
            {
                _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
                {
                    OnToggleChanged = OnCollidersToggled,
                });
            }

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnGridToggled,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnLightGizmoToggled,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnSkyboxToggled,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.SplitButton)
            {
                OnSplitPrimaryClick = OnVisualModePrimaryClicked,
                OnSplitSecondaryClick = OnVisualModeSecondaryClicked,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Button)
            {
                OnClick = OnModeButtonClicked,
            });
        }

        private void UpdateToolbarItemState()
        {
            bool environmentLocked = _session.ModeContext.Effective2D;

            PreviewToolbarItem turntable = _toolbarItems[_turntableIndex];
            turntable.IsActive = _session.TurntableEnabled && !environmentLocked;
            turntable.IsEnabled = !environmentLocked;
            turntable.FallbackText = "Auto";
            turntable.Tooltip = "Toggle turntable auto-rotation";
            turntable.IconNames = TurntableIcons;

            PreviewToolbarItem info = _toolbarItems[_infoIndex];
            info.IsActive = _session.InfoEnabled;
            info.IsEnabled = true;
            info.FallbackText = "Info";
            info.Tooltip = "Toggle preview info";
            info.IconNames = InfoIcons;

            PreviewToolbarItem lights = _toolbarItems[_lightsIndex];
            lights.IsActive = _session.LightsEnabled;
            lights.IsEnabled = !environmentLocked;
            lights.FallbackText = "Lights";
            lights.Tooltip = "Toggle model lights";
            lights.IconNames = LightsIcons;

            if (_collidersIndex >= 0)
            {
                PreviewToolbarItem colliders = _toolbarItems[_collidersIndex];
                colliders.IsActive = _session.ColliderOverlayEnabled;
                colliders.IsEnabled = true;
                colliders.FallbackText = "Coll";
                colliders.Tooltip = "Toggle collider and trigger overlay";
                colliders.IconNames = ColliderIcons;
            }

            PreviewToolbarItem grid = _toolbarItems[_gridIndex];
            grid.IsActive = _session.GridEnabled;
            grid.IsEnabled = true;
            grid.FallbackText = "Grid";
            grid.Tooltip = "Toggle preview grid";
            grid.IconNames = GridIcons;

            PreviewToolbarItem lightGizmo = _toolbarItems[_lightGizmoIndex];
            lightGizmo.IsActive = _session.LightWidgetEnabled;
            lightGizmo.IsEnabled = !environmentLocked;
            lightGizmo.FallbackText = "Gizmo";
            lightGizmo.Tooltip = "Toggle light rig gizmo";
            lightGizmo.IconNames = LightGizmoIcons;

            PreviewToolbarItem skybox = _toolbarItems[_skyboxIndex];
            skybox.IsActive = _session.SkyboxEnabled;
            skybox.IsEnabled = !environmentLocked;
            skybox.FallbackText = "Skybox";
            skybox.Tooltip = "Toggle model skybox";
            skybox.IconNames = SkyboxIcons;

            PreviewToolbarItem visualMode = _toolbarItems[_visualModeIndex];
            visualMode.IsActive = _session.VisualMode != ModelPreviewVisualMode.None;
            GetVisualModeButtonContent(
                _session.VisualMode == ModelPreviewVisualMode.None ? _session.LastNonNoneVisualMode : _session.VisualMode,
                out string visualLabel,
                out string visualTooltip,
                out bool visualTintIcon,
                out string[] visualIcons);
            visualMode.FallbackText = visualLabel;
            visualMode.Tooltip = visualTooltip;
            visualMode.TintIcon = visualTintIcon;
            visualMode.IconNames = visualIcons;

            PreviewToolbarItem mode = _toolbarItems[_modeIndex];
            mode.IsActive = _session.ModeOverride == PreviewModeOverride.Force2D;
            mode.IsEnabled = true;
            mode.FallbackText = _session.ModeContext.Effective2D ? "2D" : "3D";
            mode.Tooltip = "Switch preview mode (2D/3D)";
            mode.IconNames = null;
        }

        private void OnTurntableClicked()
        {
            _session.SetTurntableEnabled(!_session.TurntableEnabled);
            RequestRepaint();
        }

        private void OnInfoToggled(bool value)
        {
            if (value == _session.InfoEnabled)
                return;

            _session.SetInfoEnabled(value);
            RequestRepaint();
        }

        private void OnLightsToggled(bool value)
        {
            if (value == _session.LightsEnabled)
                return;

            _session.SetLightsEnabled(value);
            RequestRepaint();
        }

        private void OnGridToggled(bool value)
        {
            if (value == _session.GridEnabled)
                return;

            _session.SetGridEnabled(value);
            RequestRepaint();
        }

        private void OnCollidersToggled(bool value)
        {
            if (value == _session.ColliderOverlayEnabled)
                return;

            _session.SetColliderOverlayEnabled(value);
            RequestRepaint();
        }

        private void OnLightGizmoToggled(bool value)
        {
            if (value == _session.LightWidgetEnabled)
                return;

            _session.SetLightWidgetEnabled(value);
            RequestRepaint();
        }

        private void OnSkyboxToggled(bool value)
        {
            if (value == _session.SkyboxEnabled)
                return;

            _session.SetSkyboxEnabled(value);
            RequestRepaint();
        }

        private void OnVisualModePrimaryClicked()
        {
            _session.CycleVisualMode();
            RequestRepaint();
        }

        private void OnVisualModeSecondaryClicked(Rect splitRect)
        {
            ShowVisualModeMenu(splitRect);
        }

        private void OnModeButtonClicked()
        {
            _session.CycleModeOverride();
            RequestRepaint();
        }
        #endregion

        private void ShowVisualModeMenu(Rect splitRect)
        {
            GenericMenu menu = new GenericMenu();
            AddVisualModeMenuItem(menu, "None", ModelPreviewVisualMode.None);
            AddVisualModeMenuItem(menu, "Normals", ModelPreviewVisualMode.Normals);
            AddVisualModeMenuItem(menu, "UV Checker", ModelPreviewVisualMode.UvChecker);
            AddVisualModeMenuItem(menu, "Vertex Color", ModelPreviewVisualMode.VertexColor);
            AddVisualModeMenuItem(menu, "Matcap", ModelPreviewVisualMode.Matcap);
            AddVisualModeMenuItem(menu, "Overdraw", ModelPreviewVisualMode.Overdraw);
            const float arrowZoneWidth = 16f;
            Rect dropdownRect = new Rect(splitRect.xMax - arrowZoneWidth, splitRect.y, arrowZoneWidth, splitRect.height);
            menu.DropDown(dropdownRect);
        }

        private void AddVisualModeMenuItem(GenericMenu menu, string name, ModelPreviewVisualMode mode)
        {
            menu.AddItem(new GUIContent(name), _session.VisualMode == mode, () =>
            {
                _session.SetVisualMode(mode);
                RequestRepaint();
            });
        }

        private static void GetVisualModeButtonContent(
            ModelPreviewVisualMode mode,
            out string label,
            out string tooltip,
            out bool tintIcon,
            out string[] icons)
        {
            switch (mode)
            {
                case ModelPreviewVisualMode.Normals:
                    label = "NM";
                    tooltip = "Normals";
                    tintIcon = false;
                    icons = VisualDefaultIcons;
                    break;
                case ModelPreviewVisualMode.UvChecker:
                    label = "UV";
                    tooltip = "UV Checker";
                    tintIcon = false;
                    icons = VisualDefaultIcons;
                    break;
                case ModelPreviewVisualMode.VertexColor:
                    label = "VC";
                    tooltip = "Vertex Colors";
                    tintIcon = false;
                    icons = VisualDefaultIcons;
                    break;
                case ModelPreviewVisualMode.Matcap:
                    label = "MC";
                    tooltip = "Matcap";
                    tintIcon = false;
                    icons = VisualMatcapIcons;
                    break;
                case ModelPreviewVisualMode.Overdraw:
                    label = "OD";
                    tooltip = "Overdraw";
                    tintIcon = false;
                    icons = VisualDefaultIcons;
                    break;
                default:
                    label = "NM";
                    tooltip = "Visual mode";
                    tintIcon = false;
                    icons = VisualDefaultIcons;
                    break;
            }
        }

        private void DrawInfoPanel(Rect previewRect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string line1 = $"Mode: {_session.ModeLabel}";
            string line2 = $"Renderers: {_session.RendererCount}";
            string line3 = $"Tris: {_session.TriangleCount}  Mats: {_session.MaterialSlotCount}";
            Vector3 boundsSize = _session.BoundsSize;
            string line4 = $"Bounds: {boundsSize.x:F2}, {boundsSize.y:F2}, {boundsSize.z:F2}";

            const float padding = 4f;
            const float spacing = 2f;
            GUIStyle style = PreviewToolbarTheme.InfoValueStyle;

            float width = Mathf.Max(
                Mathf.Max(style.CalcSize(new GUIContent(line1)).x, style.CalcSize(new GUIContent(line2)).x),
                Mathf.Max(style.CalcSize(new GUIContent(line3)).x, style.CalcSize(new GUIContent(line4)).x)) + padding * 2f;
            float height = style.lineHeight * 4f + spacing * 3f + padding * 2f;
            Rect panelRect = new Rect(previewRect.x + 5f, previewRect.yMax - height - 5f, width, height);

            float y = panelRect.y + padding;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line1, style);
            y += style.lineHeight + spacing;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line2, style);
            y += style.lineHeight + spacing;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line3, style);
            y += style.lineHeight + spacing;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line4, style);
        }

        #region Update Loop
        private void EnableUpdate()
        {
            if (_updateRegistered)
                return;

            PreviewUpdateLoop.EnsureRegistered(ref _updateRegistered, OnUpdate);
        }

        private void DisableUpdate()
        {
            if (!_updateRegistered)
                return;

            PreviewUpdateLoop.EnsureUnregistered(ref _updateRegistered, OnUpdate);
        }

        private void OnUpdate()
        {
            if (!_session.IsReady)
            {
                DisableUpdate();
                return;
            }

            if (_session.TickInteraction())
                RequestRepaint();

            if (!_session.HasPendingCameraMotion)
                DisableUpdate();
        }
        #endregion

        private void RequestRepaint()
        {
            _requestRepaint?.Invoke();
        }
    }
}
