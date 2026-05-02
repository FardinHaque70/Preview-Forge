using System;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ModelPrefabPreviewImplementation : IPrefabPreviewImplementation
    {
        private readonly ModelPrefabPreviewSession _session = new();
        private Action _requestRepaint;
        private bool _updateRegistered;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Model;

        public void SetRepaintCallback(Action repaintCallback)
        {
            _requestRepaint = repaintCallback;
        }

        public bool EnsureReady(GameObject prefab)
        {
            _session.Setup(prefab);
            return _session.IsReady;
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

            if (_session.HasPendingCameraMotion)
                EnableUpdate();
            else
                DisableUpdate();
        }

        private Rect DrawToolbar(Rect fullRect)
        {
            const float rowHeight = 40f;
            const float buttonHeight = 29f;
            const float sidePadding = 6f;
            const float buttonGap = 4f;
            const int buttonCount = 7;

            Rect toolbarRect = new Rect(fullRect.x, fullRect.y, fullRect.width, rowHeight);
            Rect previewRect = new Rect(fullRect.x, fullRect.y + rowHeight, fullRect.width, fullRect.height - rowHeight);

            PreviewToolbarTheme.DrawToolbarBackground(toolbarRect);

            float centerY = toolbarRect.y + rowHeight * 0.5f;
            float y = Mathf.Round(centerY - buttonHeight * 0.5f);
            float availableWidth = toolbarRect.width - sidePadding * 2f - (buttonCount - 1) * buttonGap;
            float buttonWidth = Mathf.Max(1f, availableWidth / buttonCount);
            bool environmentLocked = _session.ModeContext.Effective2D;

            float x = toolbarRect.x + sidePadding;
            Rect turntableRect = new Rect(x, y, buttonWidth, buttonHeight);
            x += buttonWidth + buttonGap;
            Rect infoRect = new Rect(x, y, buttonWidth, buttonHeight);
            x += buttonWidth + buttonGap;
            Rect lightsRect = new Rect(x, y, buttonWidth, buttonHeight);
            x += buttonWidth + buttonGap;
            Rect gridRect = new Rect(x, y, buttonWidth, buttonHeight);
            x += buttonWidth + buttonGap;
            Rect skyboxRect = new Rect(x, y, buttonWidth, buttonHeight);
            x += buttonWidth + buttonGap;
            Rect visualModeRect = new Rect(x, y, buttonWidth, buttonHeight);
            x += buttonWidth + buttonGap;
            Rect modeRect = new Rect(x, y, buttonWidth, buttonHeight);

            if (PreviewToolbarControls.DrawButton(
                    turntableRect,
                    _session.TurntableEnabled && !environmentLocked,
                    "Auto",
                    "Toggle turntable auto-rotation",
                    !environmentLocked,
                    "RotateTool",
                    "d_RotateTool"))
            {
                _session.SetTurntableEnabled(!_session.TurntableEnabled);
                RequestRepaint();
            }

            if (DrawToolbarToggleButton(
                    lightsRect,
                    _session.LightsEnabled,
                    "Lights",
                    "Toggle model lights",
                    !environmentLocked,
                    "SceneViewLighting",
                    "d_SceneViewLighting",
                    out bool lightsEnabled)
                && lightsEnabled != _session.LightsEnabled)
            {
                _session.SetLightsEnabled(lightsEnabled);
                RequestRepaint();
            }

            if (DrawToolbarToggleButton(
                    gridRect,
                    _session.GridEnabled,
                    "Grid",
                    "Toggle preview grid",
                    true,
                    "Grid.BoxTool",
                    "d_Grid.BoxTool",
                    out bool gridEnabled)
                && gridEnabled != _session.GridEnabled)
            {
                _session.SetGridEnabled(gridEnabled);
                RequestRepaint();
            }

            if (DrawToolbarToggleButton(
                    infoRect,
                    _session.InfoEnabled,
                    "Info",
                    "Toggle preview info",
                    true,
                    "Search Icon",
                    "d_Search Icon",
                    out bool nextInfoEnabled)
                && nextInfoEnabled != _session.InfoEnabled)
            {
                _session.SetInfoEnabled(nextInfoEnabled);
                RequestRepaint();
            }

            if (DrawToolbarToggleButton(
                    skyboxRect,
                    _session.SkyboxEnabled,
                    "Skybox",
                    "Toggle model skybox",
                    !environmentLocked,
                    "PreMatSphere",
                    "d_PreMatSphere",
                    out bool skyboxEnabled)
                && skyboxEnabled != _session.SkyboxEnabled)
            {
                _session.SetSkyboxEnabled(skyboxEnabled);
                RequestRepaint();
            }

            int visualModeAction = DrawVisualModeSplitButton(visualModeRect);
            if (visualModeAction == 1)
            {
                _session.CycleVisualMode();
                RequestRepaint();
            }
            else if (visualModeAction == 2)
            {
                ShowVisualModeMenu(visualModeRect);
            }

            string modeLabel = _session.ModeContext.Effective2D ? "2D" : "3D";
            if (PreviewToolbarControls.DrawButton(
                    modeRect,
                    _session.ModeOverride == PreviewModeOverride.Force2D,
                    modeLabel,
                    "Switch preview mode (2D/3D)"))
            {
                _session.CycleModeOverride();
                RequestRepaint();
            }

            return previewRect;
        }

        private static bool DrawToolbarToggleButton(
            Rect rect,
            bool currentValue,
            string fallbackText,
            string tooltip,
            bool isEnabled,
            string lightIconName,
            string darkIconName,
            out bool newValue)
        {
            bool clicked = PreviewToolbarControls.DrawToggleButton(
                rect,
                currentValue,
                fallbackText,
                tooltip,
                isEnabled,
                out newValue,
                lightIconName,
                darkIconName);

            return clicked;
        }

        private int DrawVisualModeSplitButton(Rect rect)
        {
            ModelPreviewVisualMode modeToShow = _session.VisualMode == ModelPreviewVisualMode.None
                ? _session.LastNonNoneVisualMode
                : _session.VisualMode;
            GetVisualModeButtonContent(modeToShow, out string label, out string tooltip, out bool tintIcon, out string[] icons);

            return PreviewToolbarControls.DrawSplitButton(
                rect,
                _session.VisualMode != ModelPreviewVisualMode.None,
                tintIcon,
                label,
                tooltip,
                icons);
        }

        private void ShowVisualModeMenu(Rect splitRect)
        {
            GenericMenu menu = new GenericMenu();
            AddVisualModeMenuItem(menu, "None", ModelPreviewVisualMode.None);
            AddVisualModeMenuItem(menu, "Normals", ModelPreviewVisualMode.Normals);
            AddVisualModeMenuItem(menu, "UV Checker", ModelPreviewVisualMode.UvChecker);
            AddVisualModeMenuItem(menu, "Vertex Color", ModelPreviewVisualMode.VertexColor);
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
                    tintIcon = true;
                    icons = new[] { "d_Mesh Icon", "Mesh Icon", "d_PreMatSphere", "PreMatSphere" };
                    break;
                case ModelPreviewVisualMode.UvChecker:
                    label = "UV";
                    tooltip = "UV Checker";
                    tintIcon = false;
                    icons = new[] { "d_PreTextureRGB", "PreTextureRGB", "d_RawImage Icon", "RawImage Icon" };
                    break;
                case ModelPreviewVisualMode.VertexColor:
                    label = "VC";
                    tooltip = "Vertex Colors";
                    tintIcon = false;
                    icons = new[] { "d_ColorPicker.CycleSlider", "ColorPicker.CycleSlider", "d_PreMatSphere", "PreMatSphere" };
                    break;
                case ModelPreviewVisualMode.Overdraw:
                    label = "OD";
                    tooltip = "Overdraw";
                    tintIcon = true;
                    icons = new[] { "d_Profiler.Rendering", "Profiler.Rendering", "d_SceneViewFx", "SceneViewFx" };
                    break;
                default:
                    label = "NM";
                    tooltip = "Visual mode";
                    tintIcon = false;
                    icons = new[] { "d_SceneViewFx", "SceneViewFx" };
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

        private void RequestRepaint()
        {
            _requestRepaint?.Invoke();
        }
    }
}
