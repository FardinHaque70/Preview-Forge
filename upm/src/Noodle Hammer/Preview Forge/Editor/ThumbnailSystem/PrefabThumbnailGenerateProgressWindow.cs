using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
    internal sealed class PrefabThumbnailGenerateProgressWindow : EditorWindow
    {
        private static readonly Vector2 WindowSize = new Vector2(560f, 190f);
        private static readonly Color Background = new Color(0.055f, 0.06f, 0.07f, 1f);
        private static readonly Color Accent = new Color(0.11f, 0.84f, 0.39f, 1f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color ProgressTrack = new Color(0.17f, 0.19f, 0.22f, 1f);
        private static readonly Color ProgressFill = new Color(0.11f, 0.84f, 0.39f, 1f);
        private static readonly Color ProgressFillBright = new Color(0.18f, 0.95f, 0.46f, 1f);
        private static readonly Color TitleText = new Color(0.90f, 0.92f, 0.95f, 1f);
        private static readonly Color MutedText = new Color(0.68f, 0.70f, 0.74f, 1f);
        private static PrefabThumbnailGenerateProgressWindow instance;
        private static bool closeRequestedByCode;
        private static GUIStyle titleStyle;
        private static GUIStyle headlineStyle;
        private static GUIStyle detailStyle;
        private static GUIStyle percentStyle;

        internal static void ShowOrRefresh()
        {
            if (instance == null)
            {
                PrefabThumbnailGenerateProgressWindow window = CreateInstance<PrefabThumbnailGenerateProgressWindow>();
                if (window == null)
                    return;

                window.titleContent = new GUIContent("");
                window.minSize = WindowSize;
                window.maxSize = WindowSize;

                Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
                Vector2 center = mainWindow.center;
                window.position = new Rect(
                    center.x - WindowSize.x * 0.5f,
                    center.y - WindowSize.y * 0.5f,
                    WindowSize.x,
                    WindowSize.y);

                instance = window;
                window.ShowUtility();
            }

            instance?.Repaint();
        }

        internal static void CloseIfOpen()
        {
            if (instance == null)
                return;

            PrefabThumbnailGenerateProgressWindow existing = instance;
            instance = null;
            closeRequestedByCode = true;
            try
            {
                existing.Close();
            }
            finally
            {
                closeRequestedByCode = false;
            }
        }

        private void OnEnable()
        {
            EditorApplication.update += HandleEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= HandleEditorUpdate;
            bool shouldNotifyDismiss = !closeRequestedByCode;
            if (instance == this)
                instance = null;

            if (shouldNotifyDismiss)
                PrefabThumbnailService.NotifyProgressWindowClosedByUser();
        }

        private void HandleEditorUpdate()
        {
            if (instance != this)
                return;

            if (!PrefabThumbnailService.TryGetGenerateAllProgressWindowState(out _, out _, out _))
            {
                CloseIfOpen();
                return;
            }

            Repaint();
        }

        private void OnGUI()
        {
            if (!PrefabThumbnailService.TryGetGenerateAllProgressWindowState(out string headline, out string detail, out float progress01))
            {
                CloseIfOpen();
                return;
            }

            EnsureStyles();

            Rect fullRect = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(fullRect, Background);
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, 4f), Accent);
            DrawFrameBorder(fullRect);

            float pad = 24f;
            float contentWidth = position.width - pad * 2f;
            float y = 20f;

            EditorGUI.LabelField(new Rect(pad, y, contentWidth, 28f), "Generating Prefab Thumbnails", titleStyle);
            y += 34f;

            EditorGUI.LabelField(new Rect(pad, y, contentWidth, 24f), headline, headlineStyle);
            y += 26f;

            float detailHeight = detailStyle.CalcHeight(new GUIContent(detail), contentWidth);
            EditorGUI.LabelField(new Rect(pad, y, contentWidth, detailHeight), detail, detailStyle);
            y += detailHeight + 14f;

            Rect trackRect = new Rect(pad, y, contentWidth, 16f);
            DrawProgressBar(trackRect, progress01);

            Rect percentRect = new Rect(trackRect.x, trackRect.y - 1f, trackRect.width, trackRect.height);
            EditorGUI.LabelField(percentRect, $"{Mathf.RoundToInt(progress01 * 100f)}%", percentStyle);

            Rect cancelRect = new Rect(position.width - pad - 96f, position.height - 34f, 96f, 22f);
            if (GUI.Button(cancelRect, "Cancel", EditorStyles.miniButton))
                PrefabThumbnailService.CancelGenerateAllThumbnails();
        }

        private static void DrawProgressBar(Rect rect, float progress01)
        {
            float progress = Mathf.Clamp01(progress01);
            EditorGUI.DrawRect(rect, ProgressTrack);
            if (progress <= 0f)
                return;

            float fillWidth = rect.width * progress;
            Rect fillRect = new Rect(rect.x, rect.y, fillWidth, rect.height);
            EditorGUI.DrawRect(fillRect, ProgressFill);

            float glowWidth = Mathf.Min(8f, fillRect.width);
            if (glowWidth > 0f)
            {
                Rect glowRect = new Rect(fillRect.xMax - glowWidth, fillRect.y, glowWidth, fillRect.height);
                EditorGUI.DrawRect(glowRect, ProgressFillBright);
            }
        }

        private static void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = TitleText;
            }

            if (headlineStyle == null)
            {
                headlineStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft
                };
                headlineStyle.normal.textColor = TitleText;
            }

            if (detailStyle == null)
            {
                detailStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                {
                    fontSize = 11,
                    wordWrap = true
                };
                detailStyle.normal.textColor = MutedText;
            }

            if (percentStyle == null)
            {
                percentStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                percentStyle.normal.textColor = TitleText;
            }
        }

        private static void DrawFrameBorder(Rect fullRect)
        {
            EditorGUI.DrawRect(new Rect(fullRect.x, fullRect.y, fullRect.width, 1f), Border);
            EditorGUI.DrawRect(new Rect(fullRect.x, fullRect.yMax - 1f, fullRect.width, 1f), Border);
            EditorGUI.DrawRect(new Rect(fullRect.x, fullRect.y, 1f, fullRect.height), Border);
            EditorGUI.DrawRect(new Rect(fullRect.xMax - 1f, fullRect.y, 1f, fullRect.height), Border);
        }
    }
}
