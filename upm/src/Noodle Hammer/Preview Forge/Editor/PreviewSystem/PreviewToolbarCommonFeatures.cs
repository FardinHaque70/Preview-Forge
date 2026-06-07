using System;
using System.Collections.Generic;

namespace NoodleHammer.PreviewForge.Editor
{
    internal enum PreviewToolbarCommonFeatureKind
    {
        Bounds,
        Grid,
        Collider,
        Mode,
    }

    internal interface IPreviewToolbarCommonSession
    {
        bool BoundsOverlayEnabled { get; set; }
        bool GridEnabled { get; set; }
        bool ColliderOverlayEnabled { get; set; }
        PreviewModeOverride ModeOverride { get; }
        PreviewModeContext ModeContext { get; }
        void CycleModeOverride();
    }

    internal readonly struct PreviewToolbarCommonFeatureBinding
    {
        internal readonly PreviewToolbarCommonFeatureKind Kind;
        internal readonly PreviewToolbarItem Item;
        internal readonly string[] IconNames;

        internal PreviewToolbarCommonFeatureBinding(
            PreviewToolbarCommonFeatureKind kind,
            PreviewToolbarItem item,
            string[] iconNames)
        {
            Kind = kind;
            Item = item;
            IconNames = iconNames;
        }
    }

    internal static class PreviewToolbarIconUtility
    {
        internal static string[] BuildIconNames(string fileName, params string[] fallbacks)
        {
            string[] assetPaths = PreviewInstallLayout.BuildAssetPaths("Editor/Common/PreviewAssets/ToolbarIcons/" + fileName);
            var values = new List<string>(assetPaths.Length + (fallbacks?.Length ?? 0));
            values.AddRange(assetPaths);
            if (fallbacks != null && fallbacks.Length > 0)
                values.AddRange(fallbacks);
            return values.ToArray();
        }
    }

    internal static class PreviewToolbarCommonFeatures
    {
        internal static PreviewToolbarCommonFeatureBinding CreateGridToggle(
            IPreviewToolbarCommonSession session,
            Action requestRepaint,
            string[] iconNames,
            PreviewToolbarItemGroup group = PreviewToolbarItemGroup.Auto)
        {
            var item = new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, group)
            {
                OnToggleChanged = value =>
                {
                    if (value == session.GridEnabled)
                        return;

                    session.GridEnabled = value;
                    requestRepaint?.Invoke();
                },
            };

            return new PreviewToolbarCommonFeatureBinding(PreviewToolbarCommonFeatureKind.Grid, item, iconNames);
        }

        internal static PreviewToolbarCommonFeatureBinding CreateBoundsToggle(
            IPreviewToolbarCommonSession session,
            Action requestRepaint,
            string[] iconNames,
            PreviewToolbarItemGroup group = PreviewToolbarItemGroup.Auto)
        {
            var item = new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, group)
            {
                OnToggleChanged = value =>
                {
                    if (value == session.BoundsOverlayEnabled)
                        return;

                    session.BoundsOverlayEnabled = value;
                    requestRepaint?.Invoke();
                },
            };

            return new PreviewToolbarCommonFeatureBinding(PreviewToolbarCommonFeatureKind.Bounds, item, iconNames);
        }

        internal static PreviewToolbarCommonFeatureBinding CreateColliderToggle(
            IPreviewToolbarCommonSession session,
            Action requestRepaint,
            string[] iconNames,
            PreviewToolbarItemGroup group = PreviewToolbarItemGroup.Auto)
        {
            var item = new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, group)
            {
                OnToggleChanged = value =>
                {
                    if (value == session.ColliderOverlayEnabled)
                        return;

                    session.ColliderOverlayEnabled = value;
                    requestRepaint?.Invoke();
                },
            };

            return new PreviewToolbarCommonFeatureBinding(PreviewToolbarCommonFeatureKind.Collider, item, iconNames);
        }

        internal static PreviewToolbarCommonFeatureBinding CreateModeButton(
            IPreviewToolbarCommonSession session,
            Action requestRepaint,
            PreviewToolbarItemGroup group = PreviewToolbarItemGroup.Auto)
        {
            var item = new PreviewToolbarItem(PreviewToolbarItemKind.Button, group)
            {
                OnClick = () =>
                {
                    session.CycleModeOverride();
                    requestRepaint?.Invoke();
                },
            };

            return new PreviewToolbarCommonFeatureBinding(PreviewToolbarCommonFeatureKind.Mode, item, null);
        }

        internal static void Refresh(IPreviewToolbarCommonSession session, in PreviewToolbarCommonFeatureBinding binding)
        {
            PreviewToolbarItem item = binding.Item;
            switch (binding.Kind)
            {
                case PreviewToolbarCommonFeatureKind.Grid:
                    item.IsActive = session.GridEnabled;
                    item.IsEnabled = true;
                    item.FallbackText = "Grid";
                    item.Tooltip = "Toggle preview grid";
                    item.IconNames = binding.IconNames;
                    break;

                case PreviewToolbarCommonFeatureKind.Bounds:
                    item.IsActive = session.BoundsOverlayEnabled;
                    item.IsEnabled = true;
                    item.FallbackText = "Bnds";
                    item.Tooltip = "Toggle bounds visualizer";
                    item.IconNames = binding.IconNames;
                    break;

                case PreviewToolbarCommonFeatureKind.Collider:
                    item.IsActive = session.ColliderOverlayEnabled;
                    item.IsEnabled = true;
                    item.FallbackText = "Coll";
                    item.Tooltip = "Toggle collider and trigger overlay";
                    item.IconNames = binding.IconNames;
                    break;

                case PreviewToolbarCommonFeatureKind.Mode:
                    item.IsActive = session.ModeOverride == PreviewModeOverride.Force2D;
                    item.IsEnabled = true;
                    item.FallbackText = session.ModeContext.CameraIs2D ? "2D" : "3D";
                    item.Tooltip = "Switch preview mode (2D/3D)";
                    item.IconNames = null;
                    break;
            }
        }
    }
}
