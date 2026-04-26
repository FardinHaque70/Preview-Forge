#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

internal sealed class ThumbnailWelcomePopup : EditorWindow
{
    private static readonly Vector2 WindowSize = new Vector2(420f, 190f);

    public static bool Open()
    {
        ThumbnailWelcomePopup window = GetWindow<ThumbnailWelcomePopup>(true, "Welcome", true);
        if (window == null)
            return false;

        window.titleContent = new GUIContent("Welcome");
        window.minSize = WindowSize;
        window.maxSize = WindowSize;

        // Use the main Unity editor window rect so centering is correct on HiDPI displays
        Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
        Vector2 center = mainWindow.center;
        window.position = new Rect(center.x - WindowSize.x * 0.5f, center.y - WindowSize.y * 0.5f, WindowSize.x, WindowSize.y);
        return true;
    }

    private void OnGUI()
    {
        EditorGUI.DrawRect(new Rect(0f, 0f, position.width, position.height), ImprovedEditorTheme.Background);

        // Accent bar along top
        EditorGUI.DrawRect(new Rect(0f, 0f, position.width, 3f), ImprovedEditorTheme.Accent);

        float pad = 20f;
        float contentWidth = position.width - pad * 2f;
        float y = 20f;

        // Title
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = ImprovedEditorTheme.Text },
        };
        EditorGUI.LabelField(new Rect(pad, y, contentWidth, 24f), "Improved Thumbnail", titleStyle);
        y += 30f;

        // Body text
        GUIStyle bodyStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = ImprovedEditorTheme.MutedText },
        };
        string bodyText = "Would you like to generate thumbnails for all prefabs in this project now?\n\nYou can also do this later from Window \u2192 Improved Asset Tools \u2192 Settings.";
        float bodyHeight = bodyStyle.CalcHeight(new GUIContent(bodyText), contentWidth);
        EditorGUI.LabelField(new Rect(pad, y, contentWidth, bodyHeight), bodyText, bodyStyle);
        y += bodyHeight + 16f;

        // Buttons
        float buttonHeight = 32f;
        float buttonWidth = 140f;
        float buttonSpacing = 10f;
        float totalButtonWidth = buttonWidth * 2f + buttonSpacing;
        float buttonX = (position.width - totalButtonWidth) * 0.5f;

        Color prevBg = GUI.backgroundColor;

        GUI.backgroundColor = ImprovedEditorTheme.AccentBright;
        GUIStyle generateStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            hover = { textColor = Color.white },
        };
        if (GUI.Button(new Rect(buttonX, y, buttonWidth, buttonHeight), "Generate All", generateStyle))
        {
            ThumbnailWelcomeStateStorage.instance.MarkGenerated();
            Close();
            PrefabThumbnailService.GenerateAllThumbnailsWithProgress();
        }

        GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        GUIStyle skipStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            normal = { textColor = ImprovedEditorTheme.MutedText },
            hover = { textColor = ImprovedEditorTheme.Text },
        };
        if (GUI.Button(new Rect(buttonX + buttonWidth + buttonSpacing, y, buttonWidth, buttonHeight), "Skip", skipStyle))
        {
            ThumbnailWelcomeStateStorage.instance.MarkSkipped();
            Close();
        }

        GUI.backgroundColor = prevBg;
    }
}

}
#endif
