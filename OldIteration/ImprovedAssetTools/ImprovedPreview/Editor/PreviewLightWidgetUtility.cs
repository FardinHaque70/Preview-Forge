#if UNITY_EDITOR
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	[System.Obsolete("Use PreviewLightWidgetSystem instead.")]
	internal static class PreviewLightWidgetUtility
	{
		public static float BaseSphereSize => PreviewLightWidgetSystem.DefaultLayoutConfig.BaseSphereSize;
		public static float SphereScale => PreviewLightWidgetSystem.DefaultLayoutConfig.SphereScale;
		public static float TopPadding => PreviewLightWidgetSystem.DefaultLayoutConfig.TopPadding;
		public static float RightPadding => PreviewLightWidgetSystem.DefaultLayoutConfig.RightPadding;
		public static float DragSensitivity => PreviewLightWidgetSystem.DefaultLayoutConfig.DragSensitivity;
		public static int TextureSize => PreviewLightWidgetSystem.DefaultLayoutConfig.TextureSize;

		public static Rect GetSphereRect(Rect previewRect)
		{
			return PreviewLightWidgetSystem.GetSphereRect(previewRect, PreviewLightWidgetSystem.DefaultLayoutConfig);
		}
	}
}
#endif
