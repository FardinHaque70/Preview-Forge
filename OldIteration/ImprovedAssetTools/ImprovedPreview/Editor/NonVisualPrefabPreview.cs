#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class NonVisualPrefabPreview : CustomPreviewBase
{
	private static readonly Type s_graphicType = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI");
	private static readonly Type s_tmpUiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
	private static readonly Type s_tmpWorldType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");

	private static GUIStyle s_titleStyle;
	private static GUIStyle s_messageStyle;

	public override PreviewAssetTypeKey PreviewTypeKey => PreviewAssetTypeKey.NonVisualPrefab;

	public override bool Supports(GameObject prefab)
	{
		if (prefab == null) return false;
		if (!EditorUtility.IsPersistent(prefab)) return false;
		if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) return false;
		if (prefab.GetComponentInChildren<Renderer>(true) != null) return false;
		if (HasUiRenderableContent(prefab)) return false;

		return true;
	}

	protected override bool RequiresPreviewSceneSetup() => false;
	protected override bool ShouldDrawSharedToolbarInPreview() => false;

	protected override void DrawOverlay(Rect r)
	{
		if (Event.current.type != EventType.Repaint)
			return;

		EditorGUI.DrawRect(r, GetBackgroundColor());
		GUI.Box(r, GUIContent.none, EditorStyles.helpBox);

		float contentWidth = Mathf.Max(120f, r.width - 32f);
		float centerY = r.y + r.height * 0.5f;

		Rect titleRect = new Rect(
			r.x + (r.width - contentWidth) * 0.5f,
			centerY - 26f,
			contentWidth,
			24f);

		Rect messageRect = new Rect(
			r.x + (r.width - contentWidth) * 0.5f,
			titleRect.yMax + 8f,
			contentWidth,
			40f);

		GUI.Label(titleRect, "No Visual", TitleStyle);
		GUI.Label(messageRect, "This prefab has no renderable content.", MessageStyle);
	}

	private static bool HasUiRenderableContent(GameObject prefab)
	{
		if (prefab == null)
			return false;

		if (prefab.GetComponentInChildren<Renderer>(true) != null)
			return false;
		if (s_tmpWorldType != null && prefab.GetComponentInChildren(s_tmpWorldType, true) != null)
			return false;

		bool hasRectTransform = prefab.GetComponentInChildren<RectTransform>(true) != null;
		bool hasGraphic = s_graphicType != null && prefab.GetComponentInChildren(s_graphicType, true) != null;
		bool hasTmpUi = s_tmpUiType != null && prefab.GetComponentInChildren(s_tmpUiType, true) != null;
		if (!hasRectTransform || (!hasGraphic && !hasTmpUi))
			return false;

		Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			if (transforms[i] is not RectTransform)
				return false;
		}

		return true;
	}

	private static Color GetBackgroundColor()
	{
		return EditorGUIUtility.isProSkin
			? new Color(0.16f, 0.16f, 0.16f, 1f)
			: new Color(0.76f, 0.76f, 0.76f, 1f);
	}

	private static GUIStyle TitleStyle
	{
		get
		{
			if (s_titleStyle == null)
			{
				s_titleStyle = new GUIStyle(EditorStyles.boldLabel)
				{
					alignment = TextAnchor.MiddleCenter,
					fontSize = 15,
					wordWrap = true
				};
			}

			return s_titleStyle;
		}
	}

	private static GUIStyle MessageStyle
	{
		get
		{
			if (s_messageStyle == null)
			{
				s_messageStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
				{
					alignment = TextAnchor.UpperCenter
				};
			}

			return s_messageStyle;
		}
	}
}

}
#endif
