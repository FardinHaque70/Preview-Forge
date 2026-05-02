using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewToolbarTheme
    {
        private static readonly Color Accent = new Color(0.11f, 0.84f, 0.39f, 1f);
        private static readonly Color AccentSoft = new Color(0.11f, 0.84f, 0.39f, 0.22f);
        private static readonly Color AccentBright = new Color(0.18f, 0.95f, 0.46f, 1f);
        private static readonly Color Surface = new Color(0.10f, 0.10f, 0.10f, 0.98f);
        private static readonly Color SurfaceAlt = new Color(0.19f, 0.19f, 0.19f, 1f);
        private static readonly Color SurfaceHover = new Color(0.24f, 0.24f, 0.24f, 1f);
        private static readonly Color BorderStrong = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color TextColor = new Color(0.90f, 0.90f, 0.90f, 0.92f);
        private static readonly Color IconOnActive = new Color(0.08f, 0.08f, 0.08f, 1f);
        private static readonly Color IconOnInactive = new Color(0.95f, 0.95f, 0.95f, 1f);

        private static GUIStyle s_toolbarTextButtonStyle;
        private static GUIStyle s_infoValueStyle;

        internal static GUIStyle ToolbarTextButtonStyle
        {
            get
            {
                if (s_toolbarTextButtonStyle == null)
                {
                    s_toolbarTextButtonStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = TextColor },
                        hover = { textColor = TextColor },
                        active = { textColor = TextColor },
                    };
                }

                return s_toolbarTextButtonStyle;
            }
        }

        internal static GUIStyle InfoValueStyle
        {
            get
            {
                if (s_infoValueStyle == null)
                {
                    s_infoValueStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.UpperLeft,
                        clipping = TextClipping.Clip,
                        wordWrap = false,
                        normal = { textColor = TextColor },
                    };
                }

                return s_infoValueStyle;
            }
        }

        internal static void DrawToolbarBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, Surface);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), BorderStrong);
        }

        internal static void DrawDivider(Rect rect)
        {
            EditorGUI.DrawRect(rect, BorderStrong);
        }

        internal static Color GetToolbarButtonBackground(bool active, bool hovered)
        {
            if (active)
                return Accent;

            return hovered ? SurfaceHover : SurfaceAlt;
        }

        internal static Color GetToolbarButtonBackground(bool active, bool hovered, bool pressed)
        {
            if (pressed)
                return active ? AccentBright : new Color(0.28f, 0.28f, 0.28f, 1f);

            return GetToolbarButtonBackground(active, hovered);
        }

        internal static Color GetToolbarButtonBorder(bool active)
        {
            return active ? AccentBright : new Color(1f, 1f, 1f, 0.07f);
        }

        internal static Color GetToolbarIconTint(bool active)
        {
            return active ? IconOnActive : IconOnInactive;
        }

        internal static Color GetToolbarIconTint(bool active, bool pressed)
        {
            if (pressed && !active)
                return new Color(1f, 1f, 1f, 0.92f);

            return GetToolbarIconTint(active);
        }

        internal static Color GetSliderTrackColor() => new Color(0.16f, 0.16f, 0.16f, 1f);
        internal static Color GetSliderThumbColor(bool active) => active ? AccentBright : TextColor;
        internal static Color GetSliderFillStart() => AccentSoft;
        internal static Color GetSliderFillEnd() => Accent;
    }
}
