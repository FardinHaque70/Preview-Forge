using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Coordinates model-prefab preview lifecycle calls and delegates rendering responsibilities to the dedicated model preview session.

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ModelPrefabPreviewImplementation : IPrefabPreviewImplementation
    {
        private static readonly string[] LightsIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Lightbulb_Round_White.png", "d_Light Icon", "Light Icon", "d_SceneViewLighting", "SceneViewLighting");
        private static readonly string[] ColliderIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Collider_BoxOutline_Round_White.png", "d_BoxCollider Icon", "BoxCollider Icon", "d_EditCollider", "EditCollider");
        private static readonly string[] BoundsIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Bounds_Round_White.png", "d_ScaleTool", "ScaleTool");
        private static readonly string[] GridIcons = PreviewToolbarIconUtility.BuildIconNames("Model_GridOn_Round_White.png", "d_Grid Icon", "Grid Icon", "d_Grid.Default", "Grid.Default");
        private static readonly string[] SkyboxIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Panorama_Round_White.png", "d_Cubemap Icon", "Cubemap Icon", "d_PreMatSphere", "PreMatSphere");
        private static readonly string[] VisualDefaultIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Texture_Round_White.png", "d_Texture Icon", "Texture Icon");
        private static readonly string[] VisualMatcapIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Matcap_Round_White.png", "d_PreMatSphere", "PreMatSphere", "d_Material Icon", "Material Icon");

        private readonly ModelPrefabPreviewSession _session = new();
        private readonly List<PreviewToolbarItem> _toolbarItems;
        private readonly bool _showColliderToggle;
        private readonly bool _force3DWhenAutoMode;
        private readonly PreviewToolbarCommonFeatureBinding _colliderFeature;
        private readonly PreviewToolbarCommonFeatureBinding _boundsFeature;
        private readonly PreviewToolbarCommonFeatureBinding _gridFeature;
        private readonly PreviewToolbarCommonFeatureBinding _modeFeature;
        private PreviewToolbarItem _lightsItem;
        private PreviewToolbarItem _skyboxItem;
        private PreviewToolbarItem _visualModeItem;
        private Action _requestRepaint;
        private bool _updateRegistered;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Model;

        internal ModelPrefabPreviewImplementation(bool showColliderToggle = true, bool force3DWhenAutoMode = false)
        {
            _showColliderToggle = showColliderToggle;
            _force3DWhenAutoMode = force3DWhenAutoMode;
            _toolbarItems = new List<PreviewToolbarItem>(_showColliderToggle ? 8 : 7);
            _colliderFeature = PreviewToolbarCommonFeatures.CreateColliderToggle(_session, RequestRepaint, ColliderIcons);
            _boundsFeature = PreviewToolbarCommonFeatures.CreateBoundsToggle(_session, RequestRepaint, BoundsIcons);
            _gridFeature = PreviewToolbarCommonFeatures.CreateGridToggle(_session, RequestRepaint, GridIcons);
            _modeFeature = PreviewToolbarCommonFeatures.CreateModeButton(_session, RequestRepaint);
        }

        public void SetRepaintCallback(Action repaintCallback)
        {
            _requestRepaint = repaintCallback;
        }

        public bool EnsureReady(GameObject prefab)
        {
            _session.Setup(prefab, _force3DWhenAutoMode);
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

            if (PreviewSettings.ShowStatsEnabled)
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

            _lightsItem = new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnLightsToggled,
            };
            _toolbarItems.Add(_lightsItem);

            if (_showColliderToggle)
                _toolbarItems.Add(_colliderFeature.Item);

            _toolbarItems.Add(_boundsFeature.Item);

            _toolbarItems.Add(_gridFeature.Item);

            _skyboxItem = new PreviewToolbarItem(PreviewToolbarItemKind.Toggle)
            {
                OnToggleChanged = OnSkyboxToggled,
            };
            _toolbarItems.Add(_skyboxItem);

            _visualModeItem = new PreviewToolbarItem(PreviewToolbarItemKind.SplitButton)
            {
                OnSplitPrimaryClick = OnVisualModePrimaryClicked,
                OnSplitSecondaryClick = OnVisualModeSecondaryClicked,
            };
            _toolbarItems.Add(_modeFeature.Item);
            _toolbarItems.Add(_visualModeItem);
        }

        private void UpdateToolbarItemState()
        {
            bool environmentLocked = _session.ModeContext.Effective2D;

            PreviewToolbarItem lights = _lightsItem;
            lights.IsActive = _session.LightingControlsSupported && _session.LightsEnabled;
            lights.IsEnabled = _session.LightingControlsSupported && !environmentLocked;
            lights.FallbackText = "Lights";
            lights.Tooltip = _session.LightingControlsSupported
                ? "Toggle model lights"
                : "Lighting controls are unavailable when the active renderer is URP 2D.";
            lights.IconNames = LightsIcons;

            if (_showColliderToggle)
                PreviewToolbarCommonFeatures.Refresh(_session, _colliderFeature);

            PreviewToolbarCommonFeatures.Refresh(_session, _boundsFeature);
            PreviewToolbarCommonFeatures.Refresh(_session, _gridFeature);

            PreviewToolbarItem skybox = _skyboxItem;
            skybox.IsActive = _session.SkyboxSupported && _session.SkyboxEnabled;
            skybox.IsEnabled = _session.SkyboxSupported;
            skybox.FallbackText = "Skybox";
            skybox.Tooltip = _session.SkyboxSupported
                ? "Toggle model skybox"
                : "Skybox is unavailable when the active renderer is URP 2D.";
            skybox.IconNames = SkyboxIcons;

            PreviewToolbarItem visualMode = _visualModeItem;
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
            PreviewToolbarCommonFeatures.Refresh(_session, _modeFeature);
        }

        private void OnLightsToggled(bool value)
        {
            if (!_session.LightingControlsSupported)
                return;

            if (value == _session.LightsEnabled)
                return;

            _session.SetLightsEnabled(value);
            RequestRepaint();
        }

        private void OnSkyboxToggled(bool value)
        {
            if (!_session.SkyboxSupported)
                return;

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

            string line1 = $"Renderers: {_session.RendererCount}";
            string line2 = $"Tris: {_session.TriangleCount}  Mats: {_session.MaterialSlotCount}";
            Vector3 boundsSize = _session.BoundsSize;
            string line3 = $"Bounds: {boundsSize.x:F2}, {boundsSize.y:F2}, {boundsSize.z:F2}";

            const float padding = 4f;
            const float spacing = 2f;
            GUIStyle style = PreviewToolbarTheme.InfoValueStyle;

            float width = Mathf.Max(
                style.CalcSize(new GUIContent(line1)).x,
                Mathf.Max(style.CalcSize(new GUIContent(line2)).x, style.CalcSize(new GUIContent(line3)).x)) + padding * 2f;
            float height = style.lineHeight * 3f + spacing * 2f + padding * 2f;
            Rect panelRect = new Rect(previewRect.x + 5f, previewRect.yMax - height - 5f, width, height);

            float y = panelRect.y + padding;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line1, style);
            y += style.lineHeight + spacing;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line2, style);
            y += style.lineHeight + spacing;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line3, style);
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
