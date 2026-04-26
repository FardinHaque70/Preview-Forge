using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public enum ThumbnailBadgeType
{
    GeneralPrefab,
    PrefabVariant,
    ModelPrefab,
    SpritePrefab,
    ParticlePrefab,
    UiPrefab,
    TmpPrefab,
    TextureAsset,
    CubemapAsset,
    MaterialAsset,
}

public static class ThumbnailBadgeUtility
{
    private const float Margin = 3f;
    private const float MaxSize = 18f;
    private const float MinSize = 12f;
    private static readonly System.Type s_tmpUiType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");

    public static void Draw(Rect selectionRect, ThumbnailBadgeType badgeType, bool reserveExpandArrow = false, string assetPath = null)
    {
        Rect iconRect = GetIconRect(selectionRect, reserveExpandArrow);
        Texture icon = GetIcon(badgeType, assetPath);
        if (icon == null)
            return;

        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
    }

    /// <summary>
    /// Draws the badge using <paramref name="contentRect"/> directly as the icon area,
    /// bypassing <see cref="ThumbnailDrawingUtility.GetContentRect"/>. Use this when the
    /// caller already has the final content rect (e.g. ObjectSelector's postAssetIconDrawCallback
    /// provides iconRect which IS the content area — passing it through GetContentRect would
    /// apply padding a second time).
    /// </summary>
    public static void DrawAtContentRect(Rect contentRect, ThumbnailBadgeType badgeType, string assetPath = null)
    {
        Texture icon = GetIcon(badgeType, assetPath);
        if (icon == null) return;
        float size = Mathf.Clamp(
            Mathf.Min(contentRect.width, contentRect.height) * 0.24f * ImprovedThumbnailSettings.ThumbnailBadgeScale,
            MinSize, MaxSize * 2f);
        Rect r = new Rect(contentRect.xMax - size - Margin, contentRect.y + Margin, size, size);
        GUI.DrawTexture(r, icon, ScaleMode.ScaleToFit, true);
    }

    public static void DrawListViewTypeLabel(Rect rowRect, ThumbnailBadgeType badgeType, string assetPath = null)
    {
        Texture icon = GetIcon(badgeType, assetPath);
        if (icon == null)
            return;

        float size = rowRect.height * 0.8f;
        float offsetX = ImprovedThumbnailSettings.ListViewBadgeHorizontalOffset;
        float centerY = rowRect.y + (rowRect.height - size) * 0.5f;
        Rect badgeRect = new Rect(rowRect.x + offsetX, centerY, size, size);
        GUI.DrawTexture(badgeRect, icon, ScaleMode.ScaleToFit, true);
    }

    private static Rect GetIconRect(Rect selectionRect, bool reserveExpandArrow)
    {
        Rect contentRect = ThumbnailDrawingUtility.GetContentRect(selectionRect, ThumbnailSurface.ProjectWindowGrid, reserveExpandArrow);
        float size = Mathf.Clamp(
            Mathf.Min(contentRect.width, contentRect.height) * 0.24f * ImprovedThumbnailSettings.ThumbnailBadgeScale,
            MinSize,
            MaxSize * 2f);

        return new Rect(
            contentRect.xMax - size - Margin,
            contentRect.y + Margin,
            size,
            size);
    }

    private static Texture GetIcon(ThumbnailBadgeType badgeType, string assetPath)
    {
        switch (badgeType)
        {
            case ThumbnailBadgeType.GeneralPrefab:
                return EditorGUIUtility.IconContent("Prefab Icon").image
                    ?? EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image;
            case ThumbnailBadgeType.PrefabVariant:
                return EditorGUIUtility.IconContent("PrefabVariant Icon").image
                    ?? EditorGUIUtility.IconContent("Prefab Icon").image
                    ?? EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image;
            case ThumbnailBadgeType.ModelPrefab:
                return EditorGUIUtility.IconContent("PrefabModel Icon").image
                    ?? EditorGUIUtility.ObjectContent(null, typeof(Mesh)).image;
            case ThumbnailBadgeType.SpritePrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(SpriteRenderer)).image;
            case ThumbnailBadgeType.ParticlePrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(ParticleSystem)).image;
            case ThumbnailBadgeType.UiPrefab:
                return EditorGUIUtility.ObjectContent(null, typeof(Canvas)).image;
            case ThumbnailBadgeType.TmpPrefab:
                return s_tmpUiType != null
                    ? EditorGUIUtility.ObjectContent(null, s_tmpUiType).image
                    : EditorGUIUtility.ObjectContent(null, typeof(Canvas)).image;
            case ThumbnailBadgeType.TextureAsset:
                return GetTextureAssetIcon(assetPath);
            case ThumbnailBadgeType.CubemapAsset:
                return EditorGUIUtility.IconContent("Cubemap Icon").image
                    ?? EditorGUIUtility.ObjectContent(null, typeof(Cubemap)).image;
            case ThumbnailBadgeType.MaterialAsset:
                return EditorGUIUtility.ObjectContent(null, typeof(Material)).image;
            default:
                return null;
        }
    }

    private static Texture GetTextureAssetIcon(string assetPath)
    {
        if (IsSpriteTextureAsset(assetPath))
        {
            Texture spriteIcon = EditorGUIUtility.IconContent("Sprite Icon").image;
            if (spriteIcon != null)
                return spriteIcon;

            return EditorGUIUtility.ObjectContent(null, typeof(Sprite)).image;
        }

        return EditorGUIUtility.ObjectContent(null, typeof(Texture2D)).image;
    }

    private static bool IsSpriteTextureAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        TextureImporter textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        return textureImporter != null && textureImporter.textureType == TextureImporterType.Sprite;
    }
}

}
