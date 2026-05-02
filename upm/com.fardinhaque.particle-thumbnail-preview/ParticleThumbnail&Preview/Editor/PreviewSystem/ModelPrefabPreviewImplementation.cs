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
            const float buttonSize = 29f;
            const float sidePadding = 6f;
            const float buttonGap = 4f;

            Rect toolbarRect = new Rect(fullRect.x, fullRect.y, fullRect.width, rowHeight);
            Rect previewRect = new Rect(fullRect.x, fullRect.y + rowHeight, fullRect.width, fullRect.height - rowHeight);

            DrawToolbarBackground(toolbarRect);

            float centerY = toolbarRect.y + rowHeight * 0.5f;
            float y = Mathf.Round(centerY - buttonSize * 0.5f);

            float leftX = toolbarRect.x + sidePadding;
            Rect modeRect = new Rect(leftX, y, 64f, buttonSize);
            Rect visualModeRect = new Rect(modeRect.xMax + buttonGap, y, 86f, buttonSize);
            Rect lightsRect = new Rect(visualModeRect.xMax + buttonGap, y, buttonSize, buttonSize);
            Rect skyboxRect = new Rect(lightsRect.xMax + buttonGap, y, buttonSize, buttonSize);
            Rect gridRect = new Rect(skyboxRect.xMax + buttonGap, y, buttonSize, buttonSize);
            Rect boundsRect = new Rect(gridRect.xMax + buttonGap, y, buttonSize, buttonSize);
            Rect pivotRect = new Rect(boundsRect.xMax + buttonGap, y, buttonSize, buttonSize);
            Rect axesRect = new Rect(pivotRect.xMax + buttonGap, y, buttonSize, buttonSize);
            Rect infoRect = new Rect(axesRect.xMax + buttonGap, y, buttonSize, buttonSize);

            if (DrawModeButton(modeRect))
            {
                _session.CycleModeOverride();
                RequestRepaint();
            }

            if (DrawVisualModeSplitButton(visualModeRect))
                RequestRepaint();

            if (DrawToolbarIconToggle(lightsRect, _session.LightsEnabled, GetIconContent("L", "Toggle model lights", "SceneViewLighting", "d_SceneViewLighting"), out bool lightsEnabled)
                && lightsEnabled != _session.LightsEnabled)
            {
                _session.SetLightsEnabled(lightsEnabled);
                RequestRepaint();
            }

            if (DrawToolbarIconToggle(skyboxRect, _session.SkyboxEnabled, GetIconContent("S", "Toggle model skybox", "PreMatSphere", "d_PreMatSphere"), out bool skyboxEnabled)
                && skyboxEnabled != _session.SkyboxEnabled)
            {
                _session.SetSkyboxEnabled(skyboxEnabled);
                RequestRepaint();
            }

            if (DrawToolbarIconToggle(gridRect, _session.GridEnabled, GetIconContent("Grid", "Toggle preview grid", "Grid.BoxTool", "d_Grid.BoxTool"), out bool gridEnabled)
                && gridEnabled != _session.GridEnabled)
            {
                _session.SetGridEnabled(gridEnabled);
                RequestRepaint();
            }

            if (DrawToolbarIconToggle(boundsRect, _session.BoundsEnabled, GetIconContent("B", "Toggle bounds overlay", "RectTool", "d_RectTool"), out bool boundsEnabled)
                && boundsEnabled != _session.BoundsEnabled)
            {
                _session.SetBoundsEnabled(boundsEnabled);
                RequestRepaint();
            }

            if (DrawToolbarIconToggle(pivotRect, _session.PivotEnabled, GetIconContent("P", "Toggle pivot overlay", "MoveTool", "d_MoveTool"), out bool pivotEnabled)
                && pivotEnabled != _session.PivotEnabled)
            {
                _session.SetPivotEnabled(pivotEnabled);
                RequestRepaint();
            }

            if (DrawToolbarIconToggle(axesRect, _session.AxesEnabled, GetIconContent("A", "Toggle axes overlay", "RotateTool", "d_RotateTool"), out bool axesEnabled)
                && axesEnabled != _session.AxesEnabled)
            {
                _session.SetAxesEnabled(axesEnabled);
                RequestRepaint();
            }

            if (DrawToolbarIconToggle(infoRect, _session.InfoEnabled, GetIconContent("Info", "Toggle preview info", "Search Icon", "d_Search Icon"), out bool nextInfoEnabled)
                && nextInfoEnabled != _session.InfoEnabled)
            {
                _session.SetInfoEnabled(nextInfoEnabled);
                RequestRepaint();
            }

            return previewRect;
        }

        private bool DrawModeButton(Rect rect)
        {
            string label = _session.ModeOverride switch
            {
                PreviewModeOverride.Auto => "Auto",
                PreviewModeOverride.Force2D => "2D",
                _ => "3D",
            };

            string tooltip = "Switch preview mode (Auto/2D/3D)";
            Event evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);
            bool pressed = hovered && evt.type == EventType.MouseDown && evt.button == 0;
            if (pressed)
                evt.Use();

            Color bg = pressed
                ? new Color(0.18f, 0.95f, 0.46f, 1f)
                : hovered ? new Color(0.24f, 0.24f, 0.24f, 1f) : new Color(0.19f, 0.19f, 0.19f, 1f);
            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.07f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.07f));

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            GUI.Label(rect, new GUIContent(label, tooltip), style);

            return pressed;
        }

        private bool DrawVisualModeSplitButton(Rect rect)
        {
            Rect mainRect = new Rect(rect.x, rect.y, rect.width - 16f, rect.height);
            Rect dropRect = new Rect(mainRect.xMax, rect.y, 16f, rect.height);

            string label = GetVisualModeLabel(_session.VisualMode == ModelPreviewVisualMode.None
                ? _session.LastNonNoneVisualMode
                : _session.VisualMode);

            bool changed = false;
            if (DrawFlatButton(mainRect, $"Viz:{label}", "Toggle visual inspection mode"))
            {
                _session.CycleVisualMode();
                changed = true;
            }

            if (DrawFlatButton(dropRect, "▼", "Select visual mode"))
            {
                GenericMenu menu = new GenericMenu();
                AddVisualModeMenuItem(menu, "None", ModelPreviewVisualMode.None);
                AddVisualModeMenuItem(menu, "Normals", ModelPreviewVisualMode.Normals);
                AddVisualModeMenuItem(menu, "UV Checker", ModelPreviewVisualMode.UvChecker);
                AddVisualModeMenuItem(menu, "Vertex Color", ModelPreviewVisualMode.VertexColor);
                AddVisualModeMenuItem(menu, "Overdraw", ModelPreviewVisualMode.Overdraw);
                menu.DropDown(dropRect);
            }

            return changed;
        }

        private void AddVisualModeMenuItem(GenericMenu menu, string name, ModelPreviewVisualMode mode)
        {
            menu.AddItem(new GUIContent(name), _session.VisualMode == mode, () =>
            {
                _session.SetVisualMode(mode);
                RequestRepaint();
            });
        }

        private static string GetVisualModeLabel(ModelPreviewVisualMode mode)
        {
            return mode switch
            {
                ModelPreviewVisualMode.Normals => "NM",
                ModelPreviewVisualMode.UvChecker => "UV",
                ModelPreviewVisualMode.VertexColor => "VC",
                ModelPreviewVisualMode.Overdraw => "OD",
                _ => "None",
            };
        }

        private static bool DrawFlatButton(Rect rect, string label, string tooltip)
        {
            Event evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);
            bool pressed = hovered && evt.type == EventType.MouseDown && evt.button == 0;
            if (pressed)
                evt.Use();

            Color bg = pressed
                ? new Color(0.18f, 0.95f, 0.46f, 1f)
                : hovered ? new Color(0.24f, 0.24f, 0.24f, 1f) : new Color(0.19f, 0.19f, 0.19f, 1f);

            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.07f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.07f));

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            GUI.Label(rect, new GUIContent(label, tooltip), style);
            return pressed;
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
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.90f, 0.90f, 0.90f, 0.92f) }
            };

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

            EditorApplication.update += OnUpdate;
            _updateRegistered = true;
        }

        private void DisableUpdate()
        {
            if (!_updateRegistered)
                return;

            EditorApplication.update -= OnUpdate;
            _updateRegistered = false;
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

        private static void DrawToolbarBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.10f, 0.10f, 0.10f, 0.98f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.14f));
        }

        private static bool DrawToolbarIconToggle(Rect rect, bool currentValue, GUIContent content, out bool newValue)
        {
            Event evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);
            bool pressed = hovered && evt.type == EventType.MouseDown && evt.button == 0;
            bool clicked = false;

            newValue = currentValue;
            if (pressed)
            {
                newValue = !currentValue;
                clicked = true;
                evt.Use();
            }

            DrawToolbarIconButtonVisual(rect, content, currentValue, hovered, pressed);
            return clicked;
        }

        private static void DrawToolbarIconButtonVisual(Rect rect, GUIContent content, bool active, bool hovered, bool pressed)
        {
            Color bg;
            if (pressed)
                bg = active ? new Color(0.18f, 0.95f, 0.46f, 1f) : new Color(0.28f, 0.28f, 0.28f, 1f);
            else if (active)
                bg = new Color(0.11f, 0.84f, 0.39f, 1f);
            else
                bg = hovered ? new Color(0.24f, 0.24f, 0.24f, 1f) : new Color(0.19f, 0.19f, 0.19f, 1f);

            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.07f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), new Color(1f, 1f, 1f, 0.07f));

            Texture image = content.image;
            if (image != null)
            {
                float iconSize = Mathf.Min(rect.width, rect.height) - 8f;
                Rect iconRect = new Rect(
                    Mathf.Round(rect.center.x - iconSize * 0.5f),
                    Mathf.Round(rect.center.y - iconSize * 0.5f),
                    iconSize,
                    iconSize);

                Color previous = GUI.color;
                GUI.color = active ? new Color(0.08f, 0.08f, 0.08f, 1f) : new Color(0.95f, 0.95f, 0.95f, 1f);
                GUI.DrawTexture(iconRect, image, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
            }
            else
            {
                GUI.Label(rect, content.text, EditorStyles.miniLabel);
            }
        }

        private static GUIContent GetIconContent(string fallbackText, string tooltip, string lightIconName, string darkIconName)
        {
            GUIContent iconContent = null;
            if (EditorGUIUtility.isProSkin)
                iconContent = EditorGUIUtility.IconContent(darkIconName);
            if (iconContent == null || iconContent.image == null)
                iconContent = EditorGUIUtility.IconContent(lightIconName);

            if (iconContent != null)
            {
                iconContent.tooltip = tooltip;
                if (iconContent.image != null)
                    return iconContent;
            }

            return new GUIContent(fallbackText, tooltip);
        }
    }
}
