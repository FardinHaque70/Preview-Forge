using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PrefabThumbnailBadgeResolver
    {
        #region Public API

        public static PrefabThumbnailBadgeType Resolve(PrefabThumbnailAssetKind assetKind)
        {
            return assetKind switch
            {
                PrefabThumbnailAssetKind.ParticlePrefab => PrefabThumbnailBadgeType.Particle,
                PrefabThumbnailAssetKind.UiPrefab => PrefabThumbnailBadgeType.Ui,
                _ => PrefabThumbnailBadgeType.None,
            };
        }

        #endregion
    }

    internal static class PrefabThumbnailBadgeDrawer
    {
        private const float BadgeMargin = 4f;
        private const float MinBadgeSize = 12f;
        private const float MaxBadgeSize = 18f;
        private const float BadgeSizeRatio = 0.18f;

        private static readonly Texture ParticleBadgeIcon = ResolveIcon(typeof(ParticleSystem));
        private static readonly Texture UiBadgeIcon = ResolveIcon(typeof(Canvas));

        #region Public API

        public static void Draw(Rect contentRect, PrefabThumbnailBadgeType badgeType, PrefabThumbnailSurface surface)
        {
            if (!ShouldDraw(badgeType, surface, PrefabThumbnailSettings.ShowGridViewBadges))
                return;

            Texture icon = GetIcon(badgeType);
            if (icon == null)
                return;

            GUI.DrawTexture(GetBadgeRect(contentRect), icon, ScaleMode.ScaleToFit, true);
        }

        internal static bool ShouldDraw(PrefabThumbnailBadgeType badgeType, PrefabThumbnailSurface surface, bool showGridViewBadges)
        {
            return showGridViewBadges
                && surface == PrefabThumbnailSurface.ProjectWindowGrid
                && badgeType != PrefabThumbnailBadgeType.None;
        }

        #endregion

        #region Helpers

        private static Texture GetIcon(PrefabThumbnailBadgeType badgeType)
        {
            return badgeType switch
            {
                PrefabThumbnailBadgeType.Particle => ParticleBadgeIcon,
                PrefabThumbnailBadgeType.Ui => UiBadgeIcon,
                _ => null,
            };
        }

        private static Rect GetBadgeRect(Rect contentRect)
        {
            float badgeSize = Mathf.Clamp(Mathf.Min(contentRect.width, contentRect.height) * BadgeSizeRatio, MinBadgeSize, MaxBadgeSize);
            return new Rect(
                contentRect.xMax - badgeSize - BadgeMargin,
                contentRect.y + BadgeMargin,
                badgeSize,
                badgeSize);
        }

        private static Texture ResolveIcon(System.Type type)
        {
            return EditorGUIUtility.ObjectContent(null, type)?.image;
        }

        #endregion
    }
}
