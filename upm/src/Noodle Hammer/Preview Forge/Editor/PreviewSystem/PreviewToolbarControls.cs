using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Defines reusable toolbar control helpers, icon resolution logic, and small UI primitives shared by preview toolbars.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PreviewToolbarControls
    {
        private static readonly Dictionary<string, Texture> IconCache = new();

        internal static GUIContent GetIconContent(string fallbackText, string tooltip, params string[] iconNames)
        {
            for (int i = 0; i < iconNames.Length; i++)
            {
                string iconName = iconNames[i];
                if (string.IsNullOrEmpty(iconName))
                    continue;

                if (!IconCache.TryGetValue(iconName, out Texture icon))
                {
                    // Support project/package icon assets in addition to Unity built-in icon names.
                    if (iconName.StartsWith("Assets/", System.StringComparison.Ordinal) ||
                        iconName.StartsWith("Packages/", System.StringComparison.Ordinal))
                    {
                        icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconName);
                    }
                    else
                    {
                        GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
                        icon = iconContent != null ? iconContent.image : EditorGUIUtility.FindTexture(iconName);
                    }

                    IconCache[iconName] = icon;
                }

                if (icon != null)
                    return new GUIContent(icon, tooltip);
            }

            return new GUIContent(fallbackText, tooltip);
        }

        internal static bool DrawToggleButton(
            Rect rect,
            bool currentValue,
            string fallbackText,
            string tooltip,
            bool isEnabled,
            out bool newValue,
            params string[] iconNames)
        {
            bool clicked = DrawButton(rect, currentValue, fallbackText, tooltip, isEnabled, iconNames);
            newValue = clicked ? !currentValue : currentValue;
            return clicked;
        }

        internal static bool DrawIconButton(Rect rect, GUIContent content)
        {
            Event evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);
            bool pressed = hovered && evt.type == EventType.MouseDown && evt.button == 0;
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            DrawButtonVisual(rect, content, false, hovered, pressed, isEnabled: true);
            return clicked;
        }

        internal static bool DrawButton(
            Rect rect,
            bool active,
            string fallbackText,
            string tooltip,
            params string[] iconNames)
        {
            return DrawButton(rect, active, fallbackText, tooltip, true, iconNames);
        }

        internal static bool DrawButton(
            Rect rect,
            bool active,
            string fallbackText,
            string tooltip,
            bool isEnabled,
            params string[] iconNames)
        {
            bool clicked = isEnabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked)
                GUI.changed = true;

            GUIContent content = GetIconContent(fallbackText, tooltip, iconNames);
            Event evt = Event.current;
            bool hovered = rect.Contains(evt.mousePosition);
            bool pressed = hovered && evt.type == EventType.MouseDown && evt.button == 0;

            DrawButtonVisual(rect, content, active, hovered, pressed, isEnabled);

            if (!string.IsNullOrEmpty(content.tooltip) && rect.Contains(evt.mousePosition))
                GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));

            return clicked;
        }

        internal static int DrawSplitButton(
            Rect rect,
            bool active,
            bool tintIcon,
            string fallbackText,
            string tooltip,
            params string[] iconNames)
        {
            const float arrowZoneWidth = 16f;
            const float dividerWidth = 1f;

            Rect iconZone = new Rect(rect.x, rect.y, rect.width - arrowZoneWidth, rect.height);
            Rect arrowZone = new Rect(rect.xMax - arrowZoneWidth, rect.y, arrowZoneWidth, rect.height);

            int iconControlId = GUIUtility.GetControlID("PreviewSplitBtnIcon".GetHashCode(), FocusType.Passive, iconZone);
            int arrowControlId = GUIUtility.GetControlID("PreviewSplitBtnArrow".GetHashCode(), FocusType.Passive, arrowZone);

            int result = 0;
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if (arrowZone.Contains(evt.mousePosition))
                {
                    GUIUtility.hotControl = arrowControlId;
                    evt.Use();
                    result = 2;
                }
                else if (iconZone.Contains(evt.mousePosition))
                {
                    GUIUtility.hotControl = iconControlId;
                    evt.Use();
                    result = 1;
                }
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                if (GUIUtility.hotControl == iconControlId || GUIUtility.hotControl == arrowControlId)
                    GUIUtility.hotControl = 0;
            }

            GUIContent content = GetIconContent(fallbackText, tooltip, iconNames);
            bool iconHovered = iconZone.Contains(evt.mousePosition);
            bool arrowHovered = arrowZone.Contains(evt.mousePosition);
            Color iconBg = PreviewToolbarTheme.GetToolbarButtonBackground(active, iconHovered);
            EditorGUI.DrawRect(iconZone, iconBg);
            Color arrowBg = PreviewToolbarTheme.GetToolbarButtonBackground(false, arrowHovered);
            EditorGUI.DrawRect(arrowZone, arrowBg);

            Color iconBorder = PreviewToolbarTheme.GetToolbarButtonBorder(active);
            Color arrowBorder = PreviewToolbarTheme.GetToolbarButtonBorder(false);
            EditorGUI.DrawRect(new Rect(iconZone.x, iconZone.yMax - 1f, iconZone.width, 1f), iconBorder);
            EditorGUI.DrawRect(new Rect(arrowZone.x, arrowZone.yMax - 1f, arrowZoneWidth, 1f), arrowBorder);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), arrowBorder);
            EditorGUI.DrawRect(new Rect(arrowZone.x, rect.y + 4f, dividerWidth, rect.height - 8f), arrowBorder);

            if (content.image != null)
            {
                float iconSize = Mathf.Min(iconZone.width, iconZone.height) - 8f;
                Rect iconRect = new Rect(
                    Mathf.Round(iconZone.center.x - iconSize * 0.5f),
                    Mathf.Round(iconZone.center.y - iconSize * 0.5f),
                    iconSize,
                    iconSize);
                if (tintIcon)
                {
                    Color previousColor = GUI.color;
                    GUI.color = PreviewToolbarTheme.GetToolbarIconTint(active);
                    GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
                    GUI.color = previousColor;
                }
                else
                {
                    GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
                }
            }
            else
            {
                GUI.Label(iconZone, content.text, PreviewToolbarTheme.ToolbarTextButtonStyle);
            }

            Color arrowColor = PreviewToolbarTheme.GetToolbarIconTint(false);
            float ax = Mathf.Round(arrowZone.center.x - 3.5f);
            float ay = Mathf.Round(arrowZone.center.y - 1f);
            EditorGUI.DrawRect(new Rect(ax, ay - 1f, 7f, 1f), arrowColor);
            EditorGUI.DrawRect(new Rect(ax + 1f, ay, 5f, 1f), arrowColor);
            EditorGUI.DrawRect(new Rect(ax + 2f, ay + 1f, 3f, 1f), arrowColor);
            EditorGUI.DrawRect(new Rect(ax + 3f, ay + 2f, 1f, 1f), arrowColor);

            if (!string.IsNullOrEmpty(content.tooltip) && rect.Contains(evt.mousePosition))
                GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));

            return result;
        }

        private static void DrawButtonVisual(Rect rect, GUIContent content, bool active, bool hovered, bool pressed, bool isEnabled)
        {
            Color bg = PreviewToolbarTheme.GetToolbarButtonBackground(active, hovered, pressed);
            Color border = PreviewToolbarTheme.GetToolbarButtonBorder(active);
            if (!isEnabled)
            {
                bg *= new Color(1f, 1f, 1f, 0.5f);
                border *= new Color(1f, 1f, 1f, 0.65f);
            }

            EditorGUI.DrawRect(rect, bg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

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
                Color tint = PreviewToolbarTheme.GetToolbarIconTint(active, pressed);
                if (!isEnabled)
                    tint *= new Color(1f, 1f, 1f, 0.45f);
                GUI.color = tint;
                GUI.DrawTexture(iconRect, image, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
            }
            else
            {
                Color previousColor = GUI.color;
                if (!isEnabled)
                    GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * 0.55f);
                GUI.Label(rect, content.text, PreviewToolbarTheme.ToolbarTextButtonStyle);
                GUI.color = previousColor;
            }
        }
    }
}
