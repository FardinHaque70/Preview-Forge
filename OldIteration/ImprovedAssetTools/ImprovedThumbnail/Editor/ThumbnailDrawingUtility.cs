using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	public static class ThumbnailDrawingUtility
	{
		private const float ListViewIconOffsetX = 2f;
		private const float DefaultGridAssetScale = 0.97f;
		private const float ExpandableAssetScale = 0.9f;
		private const float BaseGridFrameThickness = 1f;
		private const float BaseGridFrameNotchWidth = 18f;
		private const float BaseGridFrameNotchHeight = 22f;
		private const float BaseGridFrameNotchOffset = 0f;

		public static Rect GetContentRect(Rect selectionRect, ThumbnailSurface surface, bool reserveExpandArrow = false)
		{
			if (surface == ThumbnailSurface.ProjectWindowList)
			{
				float size = Mathf.Max(0f, selectionRect.height);
				// One-column layout: icon starts exactly at selectionRect.x (no gap).
				// Two-column layout: Unity leaves a 2px gap before the icon.
				float offsetX = EditorCompatibilityUtility.IsProjectBrowserOneColumnLayout()
					? 0f
					: ListViewIconOffsetX;
				Rect rect = new Rect(
					selectionRect.x + offsetX,
					selectionRect.y,
					size,
					size);
				Rect padded = ApplyPadding(rect, ImprovedThumbnailSettings.ThumbnailListPadding);
				return ApplyExpandableAssetScale(padded, surface, reserveExpandArrow);
			}

			// The thumbnail area is always square (width × width). For normal assets
			// selectionRect.height is typically width + 16. For assets with expandable
			// sub-assets the height grows further to include the expand arrow row and
			// the sub-asset container. Cap to width so the background stays square.
			float contentSize = selectionRect.width;
			Rect contentRect = new Rect(
				selectionRect.x,
				selectionRect.y,
				contentSize,
				contentSize);

			Rect paddedGrid = ApplyPadding(contentRect, ImprovedThumbnailSettings.ThumbnailGridPadding);
			return ApplyExpandableAssetScale(paddedGrid, surface, reserveExpandArrow);
		}

		public static void DrawThumbnail(
			Rect selectionRect,
			Texture texture,
			ThumbnailBadgeType badgeType,
			ThumbnailSurface surface,
			bool reserveExpandArrow = false,
			bool drawBadge = true,
			string assetPath = null)
		{
			if (texture == null)
				return;

			Rect contentRect = GetContentRect(selectionRect, surface, reserveExpandArrow);
			EditorGUI.DrawRect(contentRect, ImprovedThumbnailSettings.ThumbnailBackgroundColor);

			DrawExpandArrowGutterCover(selectionRect, surface, reserveExpandArrow);
			GUI.DrawTexture(contentRect, texture, ScaleMode.ScaleToFit, true);
			DrawBaseGridFrame(contentRect, surface, reserveExpandArrow);

			if (drawBadge && ImprovedThumbnailSettings.ShowThumbnailBadges && surface != ThumbnailSurface.ProjectWindowList)
				ThumbnailBadgeUtility.Draw(selectionRect, badgeType, reserveExpandArrow, assetPath);
			if (drawBadge && ImprovedThumbnailSettings.ShowListViewTypeLabel && surface == ThumbnailSurface.ProjectWindowList)
				ThumbnailBadgeUtility.DrawListViewTypeLabel(selectionRect, badgeType, assetPath);
		}

		public static void DrawBadgeOnly(
			Rect selectionRect,
			ThumbnailBadgeType badgeType,
			ThumbnailSurface surface,
			bool reserveExpandArrow = false,
			bool drawBadge = true,
			string assetPath = null)
		{
			DrawExpandArrowGutterCover(selectionRect, surface, reserveExpandArrow);

			if (drawBadge && ImprovedThumbnailSettings.ShowThumbnailBadges && surface != ThumbnailSurface.ProjectWindowList)
				ThumbnailBadgeUtility.Draw(selectionRect, badgeType, reserveExpandArrow, assetPath);
			if (drawBadge && ImprovedThumbnailSettings.ShowListViewTypeLabel && surface == ThumbnailSurface.ProjectWindowList)
				ThumbnailBadgeUtility.DrawListViewTypeLabel(selectionRect, badgeType, assetPath);
		}

		private static void DrawExpandArrowGutterCover(Rect selectionRect, ThumbnailSurface surface, bool reserveExpandArrow)
		{
			// In Project window grid mode Unity overlays the expand affordance on top of the
			// thumbnail. Reserving a custom gutter here creates a visible vertical strip.
			// Let Unity draw its affordance over the thumbnail just like the native layout.
		}

		public static bool LooksLikeListView(Rect selectionRect)
		{
			return selectionRect.height <= 24f || selectionRect.width > selectionRect.height * 2f;
		}

		private static Rect ApplyPadding(Rect rect, float paddingPercent)
		{
			if (paddingPercent <= 0f)
				return rect;

			float inset = Mathf.Min(rect.width, rect.height) * paddingPercent;
			return new Rect(
				rect.x + inset,
				rect.y + inset,
				Mathf.Max(0f, rect.width - inset * 2f),
				Mathf.Max(0f, rect.height - inset * 2f));
		}

		private static Rect ApplyExpandableAssetScale(Rect rect, ThumbnailSurface surface, bool reserveExpandArrow)
		{
			if (surface != ThumbnailSurface.ProjectWindowGrid)
				return rect;

			float scale = reserveExpandArrow ? ExpandableAssetScale : DefaultGridAssetScale;
			return ApplyUniformScaleSnapped(rect, scale);
		}

		private static Rect ApplyUniformScaleSnapped(Rect rect, float scale)
		{
			if (scale >= 0.999f || rect.width <= 0f || rect.height <= 0f)
				return rect;

			float insetX = Mathf.Round(rect.width * (1f - scale) * 0.5f);
			float insetY = Mathf.Round(rect.height * (1f - scale) * 0.5f);

			float minEdge = 1f;
			float maxInsetX = Mathf.Max(0f, (rect.width - minEdge) * 0.5f);
			float maxInsetY = Mathf.Max(0f, (rect.height - minEdge) * 0.5f);
			insetX = Mathf.Clamp(insetX, 0f, maxInsetX);
			insetY = Mathf.Clamp(insetY, 0f, maxInsetY);

			float xMin = Mathf.Round(rect.x + insetX);
			float yMin = Mathf.Round(rect.y + insetY);
			float xMax = Mathf.Round(rect.xMax - insetX);
			float yMax = Mathf.Round(rect.yMax - insetY);

			if (xMax <= xMin)
				xMax = xMin + minEdge;
			if (yMax <= yMin)
				yMax = yMin + minEdge;

			return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
		}

		private static void DrawBaseGridFrame(Rect contentRect, ThumbnailSurface surface, bool reserveExpandArrow)
		{
			if (surface != ThumbnailSurface.ProjectWindowGrid)
				return;

			float thickness = Mathf.Max(1f, Mathf.Round(BaseGridFrameThickness));
			Color frameColor = ImprovedThumbnailSettings.UnselectedGridFrameColor;
			EditorGUI.DrawRect(new Rect(contentRect.x, contentRect.y, contentRect.width, thickness), frameColor);
			EditorGUI.DrawRect(new Rect(contentRect.x, contentRect.yMax - thickness, contentRect.width, thickness), frameColor);
			EditorGUI.DrawRect(new Rect(contentRect.x, contentRect.y, thickness, contentRect.height), frameColor);

			bool useNotch = reserveExpandArrow
				&& contentRect.width > BaseGridFrameNotchWidth
				&& contentRect.height > thickness * 2f;
				if (!useNotch)
				{
					EditorGUI.DrawRect(new Rect(contentRect.xMax - thickness, contentRect.y, thickness, contentRect.height), frameColor);
					return;
				}

			float maxGapHeight = contentRect.height - thickness * 2f;
			float gapHeight = Mathf.Round(Mathf.Min(BaseGridFrameNotchHeight, maxGapHeight));
				if (gapHeight <= 0f)
				{
					EditorGUI.DrawRect(new Rect(contentRect.xMax - thickness, contentRect.y, thickness, contentRect.height), frameColor);
					return;
				}

			float minGapY = contentRect.y + thickness;
			float maxGapY = contentRect.yMax - thickness - gapHeight;
			float gapY = contentRect.y + (contentRect.height - gapHeight) * 0.5f + BaseGridFrameNotchOffset;
			gapY = Mathf.Round(Mathf.Clamp(gapY, minGapY, maxGapY));

				float topHeight = gapY - contentRect.y;
				if (topHeight > 0f)
					EditorGUI.DrawRect(new Rect(contentRect.xMax - thickness, contentRect.y, thickness, topHeight), frameColor);

				float bottomY = gapY + gapHeight;
				float bottomHeight = contentRect.yMax - bottomY;
				if (bottomHeight > 0f)
					EditorGUI.DrawRect(new Rect(contentRect.xMax - thickness, bottomY, thickness, bottomHeight), frameColor);
			}
	}
}
