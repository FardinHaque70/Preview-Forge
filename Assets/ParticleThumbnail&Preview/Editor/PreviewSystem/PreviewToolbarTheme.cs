using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    internal enum PreviewToolbarColorPreset
    {
        UnrealEngine = 0,
        Godot = 1,
        Blender = 2,
        Buildbox = 3,
        VibrantGreen = 4,
        UnityBlue = 5,
    }

    internal static class PreviewToolbarTheme
    {
        private readonly struct ToolbarPalette
        {
            internal ToolbarPalette(Color accent, Color accentSoft, Color accentBright, Color iconOnActive)
            {
                Accent = accent;
                AccentSoft = accentSoft;
                AccentBright = accentBright;
                IconOnActive = iconOnActive;
            }

            internal Color Accent { get; }
            internal Color AccentSoft { get; }
            internal Color AccentBright { get; }
            internal Color IconOnActive { get; }
        }

        private static readonly Color Surface = new Color(0.10f, 0.10f, 0.10f, 0.98f);
        private static readonly Color SurfaceAlt = new Color(0.19f, 0.19f, 0.19f, 1f);
        private static readonly Color SurfaceHover = new Color(0.24f, 0.24f, 0.24f, 1f);
        private static readonly Color BorderStrong = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color TextColor = new Color(0.90f, 0.90f, 0.90f, 0.92f);
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
            ToolbarPalette palette = GetPalette();
            if (active)
                return palette.Accent;

            return hovered ? SurfaceHover : SurfaceAlt;
        }

        internal static Color GetToolbarButtonBackground(bool active, bool hovered, bool pressed)
        {
            ToolbarPalette palette = GetPalette();
            if (pressed)
                return active ? palette.AccentBright : new Color(0.28f, 0.28f, 0.28f, 1f);

            return GetToolbarButtonBackground(active, hovered);
        }

        internal static Color GetToolbarButtonBorder(bool active)
        {
            ToolbarPalette palette = GetPalette();
            return active ? palette.AccentBright : new Color(1f, 1f, 1f, 0.07f);
        }

        internal static Color GetToolbarIconTint(bool active)
        {
            ToolbarPalette palette = GetPalette();
            return active ? palette.IconOnActive : IconOnInactive;
        }

        internal static Color GetToolbarIconTint(bool active, bool pressed)
        {
            if (pressed && !active)
                return new Color(1f, 1f, 1f, 0.92f);

            return GetToolbarIconTint(active);
        }

        internal static Color GetSliderTrackColor() => new Color(0.16f, 0.16f, 0.16f, 1f);
        internal static Color GetSliderThumbColor(bool active) => active ? GetPalette().AccentBright : TextColor;
        internal static Color GetSliderFillStart() => GetPalette().AccentSoft;
        internal static Color GetSliderFillEnd() => GetPalette().Accent;

        private static ToolbarPalette GetPalette()
        {
            return ParticlePreviewSettings.ToolbarColorPreset switch
            {
                PreviewToolbarColorPreset.UnrealEngine => new ToolbarPalette(
                    new Color32(0xF3, 0x9C, 0x12, 0xFF),
                    new Color32(0xF3, 0x9C, 0x12, 0x38),
                    new Color32(0xFF, 0xB5, 0x3B, 0xFF),
                    new Color32(0x10, 0x10, 0x10, 0xFF)),
                PreviewToolbarColorPreset.Godot => new ToolbarPalette(
                    new Color32(0x47, 0x8C, 0xBF, 0xFF),
                    new Color32(0x47, 0x8C, 0xBF, 0x38),
                    new Color32(0x69, 0x9C, 0xE8, 0xFF),
                    new Color32(0xF0, 0xF6, 0xFB, 0xFF)),
                PreviewToolbarColorPreset.Blender => new ToolbarPalette(
                    new Color32(0xEA, 0x76, 0x00, 0xFF),
                    new Color32(0xEA, 0x76, 0x00, 0x38),
                    new Color32(0xFF, 0x92, 0x2E, 0xFF),
                    new Color32(0x12, 0x12, 0x12, 0xFF)),
                PreviewToolbarColorPreset.Buildbox => new ToolbarPalette(
                    new Color32(0x43, 0x86, 0x93, 0xFF),
                    new Color32(0x43, 0x86, 0x93, 0x38),
                    new Color32(0x59, 0x9D, 0xAA, 0xFF),
                    new Color32(0xEA, 0xF6, 0xF8, 0xFF)),
                PreviewToolbarColorPreset.UnityBlue => new ToolbarPalette(
                    new Color32(0x3A, 0x72, 0xB8, 0xFF),
                    new Color32(0x3A, 0x72, 0xB8, 0x38),
                    new Color32(0x5A, 0x8D, 0xCC, 0xFF),
                    new Color32(0xEE, 0xF4, 0xFF, 0xFF)),
                PreviewToolbarColorPreset.VibrantGreen => new ToolbarPalette(
                    new Color(0.11f, 0.84f, 0.39f, 1f),
                    new Color(0.11f, 0.84f, 0.39f, 0.22f),
                    new Color(0.18f, 0.95f, 0.46f, 1f),
                    new Color(0.08f, 0.08f, 0.08f, 1f)),
                _ => new ToolbarPalette(
                    new Color32(0x47, 0x8C, 0xBF, 0xFF),
                    new Color32(0x47, 0x8C, 0xBF, 0x38),
                    new Color32(0x69, 0x9C, 0xE8, 0xFF),
                    new Color32(0xF0, 0xF6, 0xFB, 0xFF)),
            };
        }
    }
}
