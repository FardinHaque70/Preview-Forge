using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class ParticleThumbnailWelcomeBootstrap
    {
        private const string PrefKeyPrefix = "ParticleThumbnailAndPreview.WelcomePopupHandled.";
        private const double StartupDelaySeconds = 2.0;
        private static readonly double StartupTimestamp;
        private static bool showScheduled;

        static ParticleThumbnailWelcomeBootstrap()
        {
            StartupTimestamp = EditorApplication.timeSinceStartup;
            ScheduleWelcomeCheck();
        }

        private static void ScheduleWelcomeCheck()
        {
            if (showScheduled)
                return;

            showScheduled = true;
            EditorApplication.update += TryShowWelcomePopup;
        }

        private static void TryShowWelcomePopup()
        {
            if (Application.isBatchMode)
            {
                EditorApplication.update -= TryShowWelcomePopup;
                showScheduled = false;
                return;
            }

            if (EditorPrefs.GetBool(GetPrimaryPrefKey(), false) || EditorPrefs.GetBool(GetLegacyPrefKey(), false))
            {
                EditorApplication.update -= TryShowWelcomePopup;
                showScheduled = false;
                return;
            }

            if (EditorApplication.timeSinceStartup - StartupTimestamp < StartupDelaySeconds)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (ParticleThumbnailWelcomePopupWindow.IsOpen)
                return;

            EditorApplication.update -= TryShowWelcomePopup;
            showScheduled = false;
            ParticleThumbnailWelcomePopupWindow.ShowWindow(GetPrimaryPrefKey(), GetLegacyPrefKey());
        }

        internal static void MarkWelcomeHandled()
        {
            EditorPrefs.SetBool(GetPrimaryPrefKey(), true);
            EditorPrefs.SetBool(GetLegacyPrefKey(), true);
        }

        private static string GetPrimaryPrefKey()
        {
            string productToken = PlayerSettings.productGUID.ToString("N");
            if (!string.IsNullOrEmpty(productToken))
                return PrefKeyPrefix + productToken;

            return GetLegacyPrefKey();
        }

        private static string GetLegacyPrefKey()
        {
            return PrefKeyPrefix + Hash128.Compute(Application.dataPath).ToString();
        }

        [MenuItem("Tools/Particle Thumbnail/Show Welcome Popup")]
        private static void MenuShowWelcomePopup()
        {
            string primaryKey = GetPrimaryPrefKey();
            string legacyKey = GetLegacyPrefKey();
            EditorPrefs.DeleteKey(primaryKey);
            EditorPrefs.DeleteKey(legacyKey);
            ParticleThumbnailWelcomePopupWindow.ShowWindow(primaryKey, legacyKey);
        }
    }

    internal sealed class ParticleThumbnailWelcomePopupWindow : EditorWindow
    {
        private static readonly Vector2 WindowSize = new Vector2(560f, 250f);
        private static readonly Color Background = new Color(0.055f, 0.06f, 0.07f, 1f);
        private static readonly Color Accent = new Color(0.11f, 0.84f, 0.39f, 1f);
        private static readonly Color AccentBright = new Color(0.18f, 0.95f, 0.46f, 1f);
        private static readonly Color MutedText = new Color(0.68f, 0.70f, 0.74f, 1f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.12f);
        private static GUIStyle titleStyle;
        private static GUIStyle bodyStyle;
        private static GUIStyle generateButtonStyle;
        private static GUIStyle skipButtonStyle;

        private string primaryPrefKey = string.Empty;
        private string legacyPrefKey = string.Empty;
        private bool handled;
        internal static bool IsOpen { get; private set; }

        internal static void ShowWindow(string primaryPrefKey, string legacyPrefKey)
        {
            ParticleThumbnailWelcomePopupWindow window = GetWindow<ParticleThumbnailWelcomePopupWindow>(true, "Welcome", true);
            window.primaryPrefKey = primaryPrefKey ?? string.Empty;
            window.legacyPrefKey = legacyPrefKey ?? string.Empty;
            window.titleContent = new GUIContent("Welcome");
            window.minSize = WindowSize;
            window.maxSize = WindowSize;
            Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
            Vector2 center = mainWindow.center;
            window.position = new Rect(
                center.x - WindowSize.x * 0.5f,
                center.y - WindowSize.y * 0.5f,
                WindowSize.x,
                WindowSize.y);
            window.ShowUtility();
            window.Focus();
        }

        private void OnEnable()
        {
            IsOpen = true;
        }

        private void OnDestroy()
        {
            IsOpen = false;
        }

        private void OnGUI()
        {
            EnsureStyles();

            Rect fullRect = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(fullRect, Background);
            EditorGUI.DrawRect(new Rect(0f, 0f, position.width, 4f), Accent);
            DrawFrameBorder(fullRect);

            float pad = 24f;
            float contentWidth = position.width - pad * 2f;
            float y = 22f;

            EditorGUI.LabelField(new Rect(pad, y, contentWidth, 34f), "Particle Thumbnail", titleStyle);
            y += 40f;

            string bodyText =
                "Would you like to generate thumbnails for all particle prefabs in this project now?\n\n" +
                "You can also do this later from Project Settings \u2192 Particle Thumbnail & Preview \u2192 Particle Thumbnails.\n\n" +
                "Recommended: Restart Unity once after setup so preview hooks initialize cleanly.";
            float bodyHeight = bodyStyle.CalcHeight(new GUIContent(bodyText), contentWidth);
            EditorGUI.LabelField(new Rect(pad, y, contentWidth, bodyHeight), bodyText, bodyStyle);

            float buttonHeight = 36f;
            float buttonWidth = 190f;
            float buttonSpacing = 12f;
            float totalButtonWidth = buttonWidth * 2f + buttonSpacing;
            float buttonX = (position.width - totalButtonWidth) * 0.5f;
            float buttonY = position.height - buttonHeight - 20f;

            Color previousBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = AccentBright;
            if (GUI.Button(
                    new Rect(buttonX, buttonY, buttonWidth, buttonHeight),
                    new GUIContent("Generate All", "Queue generation for all supported particle prefab thumbnails in this project."),
                    generateButtonStyle))
            {
                MarkHandled();
                Close();
                ParticleThumbnailService.GenerateAllThumbnailsInProjectFromSettings();
            }

            GUI.backgroundColor = new Color(0.09f, 0.10f, 0.12f, 1f);
            if (GUI.Button(
                    new Rect(buttonX + buttonWidth + buttonSpacing, buttonY, buttonWidth, buttonHeight),
                    new GUIContent("Skip", "Skip generation for now. This welcome popup will not be shown again."),
                    skipButtonStyle))
            {
                MarkHandled();
                Close();
            }

            GUI.backgroundColor = previousBackgroundColor;
        }

        private void OnDisable()
        {
            IsOpen = false;

            // Treat closing the popup as Skip so it does not reappear.
            if (!handled)
                MarkHandled();
        }

        private void MarkHandled()
        {
            if (handled)
                return;

            handled = true;
            if (!string.IsNullOrEmpty(primaryPrefKey))
                EditorPrefs.SetBool(primaryPrefKey, true);
            if (!string.IsNullOrEmpty(legacyPrefKey))
                EditorPrefs.SetBool(legacyPrefKey, true);
        }

        private static void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter
                };
                titleStyle.normal.textColor = new Color(0.90f, 0.92f, 0.95f, 1f);
            }

            if (bodyStyle == null)
            {
                bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 12,
                    wordWrap = true
                };
                bodyStyle.normal.textColor = MutedText;
            }

            if (generateButtonStyle == null)
            {
                generateButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                };
                generateButtonStyle.normal.textColor = Color.white;
                generateButtonStyle.hover.textColor = Color.white;
                generateButtonStyle.active.textColor = Color.white;
            }

            if (skipButtonStyle == null)
            {
                skipButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Normal
                };
                skipButtonStyle.normal.textColor = MutedText;
                skipButtonStyle.hover.textColor = new Color(0.88f, 0.90f, 0.93f, 1f);
                skipButtonStyle.active.textColor = new Color(0.88f, 0.90f, 0.93f, 1f);
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
