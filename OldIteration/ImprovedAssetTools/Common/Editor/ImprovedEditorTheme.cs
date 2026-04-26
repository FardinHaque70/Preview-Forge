#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ImprovedEditorTheme
{
    private const float StyledSliderStackThreshold = 330f;
    private static readonly int StyledSliderHash = "ImprovedEditorTheme.StyledSlider".GetHashCode();
    public static readonly Color Accent = new Color(0.11f, 0.84f, 0.39f, 1f);
    public static readonly Color AccentSoft = new Color(0.11f, 0.84f, 0.39f, 0.22f);
    public static readonly Color AccentBright = new Color(0.18f, 0.95f, 0.46f, 1f);
    public static readonly Color Background = new Color(0.08f, 0.08f, 0.08f, 1f);
    public static readonly Color Surface = new Color(0.10f, 0.10f, 0.10f, 0.98f);
    public static readonly Color SurfaceAlt = new Color(0.19f, 0.19f, 0.19f, 1f);
    public static readonly Color SurfaceHover = new Color(0.24f, 0.24f, 0.24f, 1f);
    public static readonly Color SectionSurface = new Color(0.20f, 0.20f, 0.20f, 1f);
    public static readonly Color RowSurfaceA = new Color(0f, 0f, 0f, 0.10f);
    public static readonly Color RowSurfaceB = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color Border = new Color(1f, 1f, 1f, 0.08f);
    public static readonly Color BorderStrong = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color Text = new Color(0.96f, 0.96f, 0.96f, 1f);
    public static readonly Color MutedText = new Color(0.67f, 0.67f, 0.67f, 1f);
    public static readonly Color Info = new Color(0.30f, 0.76f, 1f, 1f);
    public static readonly Color Success = new Color(0.19f, 0.83f, 0.45f, 1f);
    public static readonly Color Warning = new Color(1.0f, 0.73f, 0.24f, 1f);
    public static readonly Color Error = new Color(1.0f, 0.35f, 0.36f, 1f);
    public static readonly Color TooltipBackground = new Color(0.06f, 0.07f, 0.08f, 0.98f);
    public static readonly Color IconOnActive = new Color(0.08f, 0.08f, 0.08f, 1f);
    public static readonly Color IconOnInactive = new Color(0.95f, 0.95f, 0.95f, 1f);
    public static readonly Color HierarchyGuide = new Color(1f, 1f, 1f, 0.13f);

    private static GUIStyle _titleStyle;
    private static GUIStyle _subtitleStyle;
    private static GUIStyle _headerSubtitleStyle;
    private static GUIStyle _sectionTitleStyle;
    private static GUIStyle _sectionBodyStyle;
    private static GUIStyle _sectionChevronStyle;
    private static GUIStyle _segmentedTabLabelStyle;
    private static GUIStyle _rowStyleA;
    private static GUIStyle _rowStyleB;
    private static Texture2D _sectionBodyTexture;
    private static Texture2D _rowSurfaceATexture;
    private static Texture2D _rowSurfaceBTexture;
    private static readonly Dictionary<string, float> ToggleAnimation = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> SectionHeaderPressAnimation = new Dictionary<string, float>();
    private static bool _sectionSpacingPrimed;

    private static void RepaintFocusedWindow()
    {
        EditorWindow focused = EditorWindow.focusedWindow;
        if (focused != null)
            focused.Repaint();
    }
    public static void DrawInspectorHeader(string title, string subtitle, bool isActive = true)
    {
        _sectionSpacingPrimed = false;
        Color frameColor = GetFrameAccent(isActive);
        bool hasTitle = !string.IsNullOrWhiteSpace(title);
        Rect rect = EditorGUILayout.GetControlRect(false, hasTitle ? 58f : 42f);
        EditorGUI.DrawRect(rect, Surface);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), frameColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderStrong);

        Rect textRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, rect.height - 12f);
        if (hasTitle)
        {
            EditorGUI.LabelField(new Rect(textRect.x, textRect.y, textRect.width, 22f), title, TitleStyle);
            EditorGUI.LabelField(new Rect(textRect.x, textRect.y + 24f, textRect.width, 18f), subtitle, HeaderSubtitleStyle);
        }
        else
        {
            float subtitleY = textRect.y + (textRect.height - 18f) * 0.5f;
            EditorGUI.LabelField(new Rect(textRect.x, subtitleY, textRect.width, 18f), subtitle, HeaderSubtitleStyle);
        }

        EditorGUILayout.Space(6f);
    }

    public static void DrawToggleHeader(SerializedProperty activeProperty, string title = "Is Active")
    {
        Color frameColor = GetFrameAccent(activeProperty.boolValue);
        Rect rect = EditorGUILayout.GetControlRect(false, 40f);
        EditorGUI.DrawRect(rect, Surface);
        DrawOutline(rect, frameColor);

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 11,
            normal = { textColor = Text }
        };

        EditorGUI.LabelField(new Rect(rect.x + 14f, rect.y + 10f, 140f, 20f), title, labelStyle);
        Rect toggleRect = new Rect(rect.xMax - 90f, rect.y + 6f, 64f, 28f);
        DrawHeaderToggle(toggleRect, activeProperty);
        _sectionSpacingPrimed = true;
        EditorGUILayout.Space(8f);
    }

    public static void DrawInlineToggle(Rect rect, SerializedProperty property)
    {
        DrawHeaderToggle(rect, property);
    }

    public static bool DrawSectionHeader(bool expanded, string title, string subtitle = null, bool isActive = true)
    {
        if (_sectionSpacingPrimed)
            _sectionSpacingPrimed = false;
        else
            EditorGUILayout.Space(8f);

        Color frameColor = GetFrameAccent(isActive);
        Rect rect = EditorGUILayout.GetControlRect(false, subtitle == null ? 34f : 50f);
        string pressKey = title + "|" + subtitle;
        EditorGUI.DrawRect(rect, Surface);
        DrawSectionHeaderPressFeedback(rect, pressKey);
        DrawOutline(rect, frameColor);

        Rect foldoutRect = new Rect(rect.x + 10f, rect.y + 6f, rect.width - 16f, rect.height - 8f);
        Rect titleRect = new Rect(foldoutRect.x, rect.y + 6f, foldoutRect.width, 20f);
        Rect subtitleRect = new Rect(titleRect.x, rect.y + 24f, titleRect.width, 18f);
        Rect chevronRect = new Rect(rect.xMax - 26f, rect.y + (rect.height - 20f) * 0.5f, 16f, 20f);

        Event evt = Event.current;
        if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
        {
            SectionHeaderPressAnimation[pressKey] = 1f;
            expanded = !expanded;
            evt.Use();
        }

        EditorGUI.LabelField(titleRect, title, SectionTitleStyle);
        if (!string.IsNullOrEmpty(subtitle))
            EditorGUI.LabelField(subtitleRect, subtitle, SubtitleStyle);
        GUI.Label(chevronRect, expanded ? "▼" : "▶", SectionChevronStyle);

        return expanded;
    }

    public static void BeginSectionBody(bool isActive)
    {
        EditorGUILayout.BeginVertical(SectionBodyStyle);
    }

    public static void EndSectionBody()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(12f);
    }

    public static void DrawStyledProperty(SerializedProperty property, GUIContent label)
    {
        EditorGUILayout.PropertyField(property, label, true);
    }

    public static GUIStyle GetAlternatingRowStyle(int rowIndex)
    {
        return (rowIndex & 1) == 0 ? RowStyleA : RowStyleB;
    }

    public static void DrawAlternatingRowBackground(Rect rect, int rowIndex, float horizontalExpand = 0f, float verticalExpand = 0f)
    {
        Color color = (rowIndex & 1) == 0 ? RowSurfaceA : RowSurfaceB;
        Rect expandedRect = new Rect(
            rect.x - horizontalExpand,
            rect.y - verticalExpand,
            rect.width + horizontalExpand * 2f,
            rect.height + verticalExpand * 2f);
        EditorGUI.DrawRect(expandedRect, color);
    }

    public static void DrawHierarchyGuide(Rect rowRect, float x)
    {
        float spineTop = rowRect.y;
        float spineBottom = rowRect.yMax;
        float centerY = rowRect.y + rowRect.height * 0.5f;

        EditorGUI.DrawRect(new Rect(x, spineTop, 1f, Mathf.Max(0f, spineBottom - spineTop)), HierarchyGuide);
        EditorGUI.DrawRect(new Rect(x, centerY, 12f, 1f), HierarchyGuide);
    }

    public static float GetStyledSliderHeight(float availableWidth, GUIContent label)
    {
        return ShouldUseStackedSliderLayout(availableWidth, label)
            ? EditorGUIUtility.singleLineHeight * 2f + 6f
            : EditorGUIUtility.singleLineHeight;
    }

    public static float GetStyledToggleHeight(float availableWidth, GUIContent label, float toggleWidth)
    {
        return ShouldUseStackedToggleLayout(availableWidth, label, toggleWidth)
            ? EditorGUIUtility.singleLineHeight * 2f + 4f
            : EditorGUIUtility.singleLineHeight;
    }

    public static float DrawStyledSlider(Rect rect, GUIContent label, float value, float min, float max, int displayedDecimals = -1)
    {
        const float valueWidth = 64f;
        const float gap = 10f;
        const float stackedGap = 4f;
        bool stacked = rect.height > EditorGUIUtility.singleLineHeight + 1f;

        Rect labelRect;
        Rect valueRect;
        Rect sliderRect;

        if (stacked)
        {
            labelRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            float controlY = rect.y + EditorGUIUtility.singleLineHeight + stackedGap;
            valueRect = new Rect(rect.xMax - valueWidth, controlY, valueWidth, EditorGUIUtility.singleLineHeight);
            sliderRect = new Rect(
                rect.x,
                controlY,
                Mathf.Max(40f, valueRect.x - gap - rect.x),
                EditorGUIUtility.singleLineHeight);
        }
        else
        {
            float labelWidth = Mathf.Clamp(rect.width * 0.42f, 140f, 260f);
            labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            valueRect = new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height);
            sliderRect = new Rect(
                labelRect.xMax + gap,
                rect.y,
                Mathf.Max(40f, valueRect.x - gap - (labelRect.xMax + gap)),
                rect.height);
        }

        EditorGUI.LabelField(labelRect, label);
        float clampedValue = Mathf.Clamp(value, min, max);
        float newValue = DrawSliderTrack(sliderRect, clampedValue, min, max);
        float displayedValue = RoundDisplayedValue(newValue, displayedDecimals);
        newValue = EditorGUI.FloatField(valueRect, displayedValue);
        newValue = RoundDisplayedValue(newValue, displayedDecimals);
        return Mathf.Clamp(newValue, min, max);
    }

    public static int DrawStyledIntSlider(Rect rect, GUIContent label, int value, int min, int max)
    {
        const float valueWidth = 64f;
        const float gap = 10f;
        const float stackedGap = 4f;
        bool stacked = rect.height > EditorGUIUtility.singleLineHeight + 1f;

        Rect labelRect;
        Rect valueRect;
        Rect sliderRect;

        if (stacked)
        {
            labelRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            float controlY = rect.y + EditorGUIUtility.singleLineHeight + stackedGap;
            valueRect = new Rect(rect.xMax - valueWidth, controlY, valueWidth, EditorGUIUtility.singleLineHeight);
            sliderRect = new Rect(
                rect.x,
                controlY,
                Mathf.Max(40f, valueRect.x - gap - rect.x),
                EditorGUIUtility.singleLineHeight);
        }
        else
        {
            float labelWidth = Mathf.Clamp(rect.width * 0.42f, 140f, 260f);
            labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            valueRect = new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height);
            sliderRect = new Rect(
                labelRect.xMax + gap,
                rect.y,
                Mathf.Max(40f, valueRect.x - gap - (labelRect.xMax + gap)),
                rect.height);
        }

        EditorGUI.LabelField(labelRect, label);
        int clampedValue = Mathf.Clamp(value, min, max);
        float newValue = DrawSliderTrack(sliderRect, clampedValue, min, max);
        int rounded = Mathf.RoundToInt(newValue);
        rounded = EditorGUI.IntField(valueRect, rounded);
        return Mathf.Clamp(rounded, min, max);
    }

    public static void WithAccentValueText(Action draw)
    {
        draw();
    }

    private static float RoundDisplayedValue(float value, int displayedDecimals)
    {
        if (displayedDecimals < 0)
            return value;

        return (float)Math.Round(value, displayedDecimals, MidpointRounding.AwayFromZero);
    }

    public static void DrawToolbarBackground(Rect rect)
    {
        EditorGUI.DrawRect(rect, Surface);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderStrong);
    }

    public static int DrawSegmentedTabs(int selectedIndex, IReadOnlyList<string> labels, float height = 30f)
    {
        if (labels == null || labels.Count == 0)
            return selectedIndex;

        Rect rect = EditorGUILayout.GetControlRect(false, height);
        EditorGUI.DrawRect(rect, SurfaceAlt);
        DrawOutline(rect, BorderStrong);

        float segmentWidth = rect.width / labels.Count;
        Event evt = Event.current;
        const float inset = 1f;

        for (int i = 0; i < labels.Count; i++)
        {
            Rect segmentRect = new Rect(
                rect.x + segmentWidth * i,
                rect.y,
                i == labels.Count - 1 ? rect.xMax - (rect.x + segmentWidth * i) : segmentWidth,
                rect.height);
            Rect fillRect = new Rect(
                segmentRect.x + inset,
                segmentRect.y + inset,
                Mathf.Max(0f, segmentRect.width - inset * 2f),
                Mathf.Max(0f, segmentRect.height - inset * 2f));

            bool isSelected = i == selectedIndex;
            bool isHovered = segmentRect.Contains(evt.mousePosition);
            bool isPressed = isHovered && evt.type == EventType.MouseDown && evt.button == 0;
            Color fill = isSelected
                ? Accent
                : isPressed
                    ? new Color(0.22f, 0.22f, 0.22f, 1f)
                    : isHovered
                        ? new Color(0.25f, 0.25f, 0.25f, 1f)
                        : SurfaceAlt;
            EditorGUI.DrawRect(fillRect, fill);

            Color previousColor = GUI.color;
            GUI.color = isSelected ? IconOnActive : Text;
            GUI.Label(segmentRect, labels[i], SegmentedTabLabelStyle);
            GUI.color = previousColor;

            if (evt.type == EventType.MouseDown && evt.button == 0 && segmentRect.Contains(evt.mousePosition))
            {
                selectedIndex = i;
                evt.Use();
            }
        }

        return selectedIndex;
    }

    public static void DrawDivider(Rect rect)
    {
        EditorGUI.DrawRect(rect, BorderStrong);
    }

    public static Color GetToolbarButtonBackground(bool active)
    {
        return active ? Accent : SurfaceAlt;
    }

    public static Color GetToolbarButtonBackground(bool active, bool hovered)
    {
        if (active)
            return Accent;

        return hovered ? SurfaceHover : SurfaceAlt;
    }

    public static Color GetToolbarButtonBorder(bool active)
    {
        return active ? AccentBright : new Color(1f, 1f, 1f, 0.07f);
    }

    public static Color GetActionFill(Color accent, bool hovered, bool enabled)
    {
        return GetActionFill(accent, hovered, false, enabled);
    }

    public static Color GetActionFill(Color accent, bool hovered, bool pressed, bool enabled)
    {
        float tint = !enabled
            ? 0.10f
            : pressed
                ? 0.54f
                : hovered
                    ? 0.46f
                    : 0.40f;

        Color baseSurface = pressed ? SurfaceAlt : Surface;
        Color fill = Color.Lerp(baseSurface, accent, tint);
        fill.a = 1f;
        return fill;
    }

    public static Color GetActionBorder(Color accent, bool enabled)
    {
        Color darkBorder = new Color(0f, 0f, 0f, enabled ? 0.42f : 0.28f);
        return Color.Lerp(darkBorder, accent, enabled ? 0.10f : 0.02f);
    }

    public static Color GetActionTextColor(bool enabled)
    {
        return enabled ? Text : MutedText;
    }

    public static Color GetActionIconColor(bool enabled)
    {
        return enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
    }

    public static Color GetActionTopHighlight(bool hovered, bool pressed, bool enabled)
    {
        if (!enabled)
            return new Color(1f, 1f, 1f, 0.025f);

        if (pressed)
            return new Color(1f, 1f, 1f, 0.035f);

        return hovered ? new Color(1f, 1f, 1f, 0.07f) : new Color(1f, 1f, 1f, 0.05f);
    }

    public static Color GetActionBottomShadow(bool pressed, bool enabled)
    {
        if (!enabled)
            return new Color(0f, 0f, 0f, 0.12f);

        return pressed ? new Color(0f, 0f, 0f, 0.14f) : new Color(0f, 0f, 0f, 0.24f);
    }

    public static Color GetToolbarIconTint(bool active)
    {
        return active ? IconOnActive : IconOnInactive;
    }

    public static Color GetSliderTrackColor()
    {
        return new Color(0.16f, 0.16f, 0.16f, 1f);
    }

    public static Color GetSliderThumbColor(bool active)
    {
        return active ? AccentBright : Text;
    }

    public static Color GetSliderFillStart()
    {
        return AccentSoft;
    }

    public static Color GetSliderFillEnd()
    {
        return Accent;
    }

    private static GUIStyle TitleStyle
    {
        get
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 21,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Text }
                };
            }

            return _titleStyle;
        }
    }

    private static GUIStyle SubtitleStyle
    {
        get
        {
            if (_subtitleStyle == null)
            {
                _subtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                {
                    alignment = TextAnchor.UpperLeft,
                    normal = { textColor = MutedText }
                };
            }

            return _subtitleStyle;
        }
    }

    private static GUIStyle HeaderSubtitleStyle
    {
        get
        {
            if (_headerSubtitleStyle == null)
            {
                _headerSubtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                {
                    fontSize = 12,
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = MutedText }
                };
            }

            return _headerSubtitleStyle;
        }
    }

    private static GUIStyle SectionTitleStyle
    {
        get
        {
            if (_sectionTitleStyle == null)
            {
                _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 11,
                    normal = { textColor = Text }
                };
            }

            return _sectionTitleStyle;
        }
    }

    private static GUIStyle SectionBodyStyle
    {
        get
        {
            if (_sectionBodyStyle == null)
            {
                _sectionBodyStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(14, 14, 0, 0),
                    margin = new RectOffset(0, 0, -1, 0),
                    overflow = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(1, 1, 1, 1)
                };
                _sectionBodyStyle.normal.background = SectionBodyTexture;
                _sectionBodyStyle.hover.background = SectionBodyTexture;
                _sectionBodyStyle.active.background = SectionBodyTexture;
                _sectionBodyStyle.focused.background = SectionBodyTexture;
            }

            return _sectionBodyStyle;
        }
    }

    private static GUIStyle SectionChevronStyle
    {
        get
        {
            if (_sectionChevronStyle == null)
            {
                _sectionChevronStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    normal = { textColor = MutedText }
                };
            }

            return _sectionChevronStyle;
        }
    }

    private static GUIStyle SegmentedTabLabelStyle
    {
        get
        {
            if (_segmentedTabLabelStyle == null)
            {
                _segmentedTabLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    clipping = TextClipping.Clip
                };
            }

            return _segmentedTabLabelStyle;
        }
    }

    private static GUIStyle RowStyleA
    {
        get
        {
            if (_rowStyleA == null)
                _rowStyleA = CreateAlternatingRowStyle(RowSurfaceATexture);

            return _rowStyleA;
        }
    }

    private static GUIStyle RowStyleB
    {
        get
        {
            if (_rowStyleB == null)
                _rowStyleB = CreateAlternatingRowStyle(RowSurfaceBTexture);

            return _rowStyleB;
        }
    }

    public static void DrawOutline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    private static Color GetFrameAccent(bool isActive)
    {
        return isActive ? Accent : new Color(0.07f, 0.45f, 0.22f, 1f);
    }

    private static Texture2D SectionBodyTexture =>
        _sectionBodyTexture ?? (_sectionBodyTexture = MakeFramedTexture(new Color(1f, 1f, 1f, 0.16f), new Color(0.24f, 0.24f, 0.24f, 1f)));

    private static Texture2D RowSurfaceATexture =>
        _rowSurfaceATexture ?? (_rowSurfaceATexture = MakeSolidTexture(RowSurfaceA));

    private static Texture2D RowSurfaceBTexture =>
        _rowSurfaceBTexture ?? (_rowSurfaceBTexture = MakeSolidTexture(RowSurfaceB));

    public static Texture2D GetSectionBodyBackgroundTexture()
    {
        return SectionBodyTexture;
    }

    private static void DrawSectionHeaderPressFeedback(Rect rect, string key)
    {
        if (!SectionHeaderPressAnimation.TryGetValue(key, out float anim))
            return;

        if (Event.current.type == EventType.Repaint)
        {
            anim = Mathf.Lerp(anim, 0f, 0.32f);
            if (anim < 0.02f)
            {
                SectionHeaderPressAnimation.Remove(key);
                return;
            }

            SectionHeaderPressAnimation[key] = anim;
            RepaintFocusedWindow();
        }

        Color overlay = new Color(Accent.r, Accent.g, Accent.b, 0.18f * anim);
        EditorGUI.DrawRect(rect, overlay);
    }

    private static float DrawSliderTrack(Rect rect, float value, float min, float max)
    {
        Event evt = Event.current;
        int controlId = GUIUtility.GetControlID(StyledSliderHash, FocusType.Passive, rect);
        float safeRange = Mathf.Max(0.0001f, max - min);
        float normalized = Mathf.InverseLerp(min, max, value);

        Rect trackRect = new Rect(rect.x, rect.y + rect.height * 0.5f - 3f, rect.width, 6f);
        Rect fillRect = new Rect(trackRect.x, trackRect.y, trackRect.width * normalized, trackRect.height);
        float thumbCenterX = Mathf.Lerp(trackRect.x, trackRect.xMax, normalized);
        Rect thumbRect = new Rect(thumbCenterX - 5f, rect.y + rect.height * 0.5f - 8f, 10f, 16f);

        EditorGUI.DrawRect(trackRect, GetSliderTrackColor());
        if (fillRect.width > 0f)
            EditorGUI.DrawRect(fillRect, GetSliderFillEnd());
        EditorGUI.DrawRect(thumbRect, GetSliderThumbColor(GUIUtility.hotControl == controlId));

        switch (evt.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (trackRect.Contains(evt.mousePosition) || thumbRect.Contains(evt.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    GUI.changed = true;
                    evt.Use();
                    return SliderValueFromMouse(trackRect, evt.mousePosition.x, min, safeRange);
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId)
                {
                    GUI.changed = true;
                    evt.Use();
                    return SliderValueFromMouse(trackRect, evt.mousePosition.x, min, safeRange);
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId)
                {
                    GUIUtility.hotControl = 0;
                    GUI.changed = true;
                    evt.Use();
                    return SliderValueFromMouse(trackRect, evt.mousePosition.x, min, safeRange);
                }
                break;
        }

        return value;
    }

    private static float SliderValueFromMouse(Rect trackRect, float mouseX, float min, float range)
    {
        float normalized = Mathf.Clamp01(Mathf.InverseLerp(trackRect.x, trackRect.xMax, mouseX));
        return min + range * normalized;
    }

    private static bool ShouldUseStackedSliderLayout(float availableWidth, GUIContent label)
    {
        const float valueWidth = 64f;
        const float gap = 10f;
        const float minimumSliderWidth = 56f;

        float labelWidth = Mathf.Clamp(availableWidth * 0.42f, 140f, 260f);
        float labelRequiredWidth = EditorStyles.label.CalcSize(label).x + 8f;
        float sliderWidth = availableWidth - labelWidth - gap - valueWidth;

        return availableWidth < StyledSliderStackThreshold
            || labelRequiredWidth > labelWidth
            || sliderWidth < minimumSliderWidth;
    }

    private static bool ShouldUseStackedToggleLayout(float availableWidth, GUIContent label, float toggleWidth)
    {
        const float gap = 10f;
        const float minimumInlineLabelWidth = 44f;

        float labelAvailableWidth = availableWidth - toggleWidth - gap;
        float labelRequiredWidth = EditorStyles.label.CalcSize(label).x + 8f;
        return labelAvailableWidth < minimumInlineLabelWidth || labelRequiredWidth > labelAvailableWidth;
    }

    private static void DrawHeaderToggle(Rect rect, SerializedProperty property)
    {
        string key = property.serializedObject.targetObject.GetInstanceID() + ":" + property.propertyPath;
        Event evt = Event.current;
        bool hovered = rect.Contains(evt.mousePosition);
        bool isActive = property.boolValue;

        if (evt.type == EventType.MouseDown && evt.button == 0 && hovered)
        {
            isActive = !isActive;
            property.boolValue = isActive;
            GUI.changed = true;
            evt.Use();
        }

        float target = isActive ? 1f : 0f;

        if (!ToggleAnimation.TryGetValue(key, out float anim))
        {
            anim = target;
        }
        else if (Event.current.type == EventType.Repaint)
        {
            anim = Mathf.Lerp(anim, target, 0.28f);
            if (Mathf.Abs(anim - target) < 0.01f)
                anim = target;
            else
                RepaintFocusedWindow();
        }

        ToggleAnimation[key] = anim;

        Color offFill = hovered ? new Color(0.24f, 0.24f, 0.24f, 1f) : SurfaceAlt;
        Color onFill = new Color(0.10f, 0.65f, 0.30f, 1f);
        Color trackFill = Color.Lerp(offFill, onFill, anim);
        Color trackBorder = BorderStrong;
        EditorGUI.DrawRect(rect, trackFill);
        DrawOutline(rect, trackBorder);

        float knobPadding = 3f;
        float knobWidth = 22f;
        float knobHeight = rect.height - knobPadding * 2f;
        float knobX = Mathf.Lerp(rect.x + knobPadding, rect.xMax - knobWidth - knobPadding, anim);
        Rect knobRect = new Rect(knobX, rect.y + knobPadding, knobWidth, knobHeight);
        Color offKnob = Accent;
        Color knobColor = Color.Lerp(offKnob, Color.white, anim);

        EditorGUI.DrawRect(knobRect, knobColor);
    }

    private static Texture2D MakeFramedTexture(Color borderColor, Color fillColor)
    {
        Texture2D tex = new Texture2D(3, 3, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                bool isBorder = x == 0 || x == 2 || y == 0 || y == 2;
                tex.SetPixel(x, y, isBorder ? borderColor : fillColor);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D MakeSolidTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };

        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private static GUIStyle CreateAlternatingRowStyle(Texture2D background)
    {
        GUIStyle style = new GUIStyle
        {
            margin = new RectOffset(0, 0, 0, 0),
            border = new RectOffset(0, 0, 0, 0),
            overflow = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(0, 0, 1, 1)
        };
        style.normal.background = background;
        style.hover.background = background;
        style.active.background = background;
        style.focused.background = background;
        return style;
    }

}
}
#endif
