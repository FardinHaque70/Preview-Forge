using System;
using System.Collections.Generic;
using UnityEngine;
// Lays out and renders the preview toolbar, including adaptive grouping, spacing rules, and interaction handoff.

namespace ParticleThumbnailAndPreview.Editor
{
    internal enum PreviewToolbarLayoutPreset
    {
        EqualGrid,
        Segmented,
    }

    internal enum PreviewToolbarItemKind
    {
        Button,
        Toggle,
        SplitButton,
        Divider,
        CustomSlot,
    }

    internal enum PreviewToolbarItemGroup
    {
        Auto,
        Left,
        Center,
        Right,
    }

    internal readonly struct PreviewToolbarMetrics
    {
        public readonly float RowHeight;
        public readonly float SidePadding;
        public readonly float ButtonGap;
        public readonly float ButtonSize;
        public readonly float DividerGap;
        public readonly float DividerInset;
        public readonly float DividerHeight;
        public readonly float SliderHeight;
        public readonly float MinSliderWidth;

        private PreviewToolbarMetrics(
            float rowHeight,
            float sidePadding,
            float buttonGap,
            float buttonSize,
            float dividerGap,
            float dividerInset,
            float dividerHeight,
            float sliderHeight,
            float minSliderWidth)
        {
            RowHeight = rowHeight;
            SidePadding = sidePadding;
            ButtonGap = buttonGap;
            ButtonSize = buttonSize;
            DividerGap = dividerGap;
            DividerInset = dividerInset;
            DividerHeight = dividerHeight;
            SliderHeight = sliderHeight;
            MinSliderWidth = minSliderWidth;
        }

        public static PreviewToolbarMetrics FromSettings()
        {
            return FromHeight(PreviewSettings.ToolbarHeight);
        }

        public static PreviewToolbarMetrics FromHeight(float rowHeight)
        {
            float clampedHeight = Mathf.Clamp(rowHeight, PreviewSettings.MinToolbarHeight, PreviewSettings.MaxToolbarHeight);
            float sidePadding = Mathf.Max(2f, Mathf.Round(clampedHeight * 0.18f));
            float buttonGap = Mathf.Max(2f, Mathf.Round(clampedHeight * 0.12f));
            float verticalInset = Mathf.Max(2f, Mathf.Round(clampedHeight * 0.15f));
            float buttonSize = Mathf.Max(12f, clampedHeight - verticalInset * 2f);
            float dividerGap = Mathf.Max(4f, Mathf.Round(clampedHeight * 0.3f));
            float dividerInset = Mathf.Max(2f, Mathf.Round(clampedHeight * 0.2f));
            float dividerHeight = Mathf.Max(1f, clampedHeight - dividerInset * 2f);
            float sliderHeight = Mathf.Clamp(buttonSize * 0.62f, 8f, 18f);
            float minSliderWidth = Mathf.Max(40f, clampedHeight * 2f);

            return new PreviewToolbarMetrics(
                clampedHeight,
                sidePadding,
                buttonGap,
                buttonSize,
                dividerGap,
                dividerInset,
                dividerHeight,
                sliderHeight,
                minSliderWidth);
        }
    }

    internal sealed class PreviewToolbarItem
    {
        public PreviewToolbarItemKind Kind;
        public PreviewToolbarItemGroup Group;
        public string FallbackText;
        public string Tooltip;
        public string[] IconNames;
        public bool IsEnabled;
        public bool IsActive;
        public bool TintIcon;
        public bool UseSliderHeight;
        public float MinWidth;
        public float FixedWidth;
        public Action OnClick;
        public Action<bool> OnToggleChanged;
        public Action OnSplitPrimaryClick;
        public Action<Rect> OnSplitSecondaryClick;
        public Action<Rect> OnDrawCustom;

        public PreviewToolbarItem(PreviewToolbarItemKind kind, PreviewToolbarItemGroup group = PreviewToolbarItemGroup.Auto)
        {
            Kind = kind;
            Group = group;
            IsEnabled = true;
        }
    }

    internal static class PreviewToolbarRenderer
    {
        private static readonly string[] EmptyIconNames = Array.Empty<string>();

        #region Draw
        public static Rect Draw(Rect fullRect, PreviewToolbarLayoutPreset layoutPreset, IList<PreviewToolbarItem> items, in PreviewToolbarMetrics metrics)
        {
            Rect toolbarRect = new Rect(fullRect.x, fullRect.y, fullRect.width, metrics.RowHeight);
            Rect previewRect = new Rect(fullRect.x, fullRect.y + metrics.RowHeight, fullRect.width, fullRect.height - metrics.RowHeight);

            PreviewToolbarTheme.DrawToolbarBackground(toolbarRect);

            if (items == null || items.Count == 0)
                return previewRect;

            switch (layoutPreset)
            {
                case PreviewToolbarLayoutPreset.EqualGrid:
                    DrawEqualGrid(toolbarRect, items, metrics);
                    break;
                case PreviewToolbarLayoutPreset.Segmented:
                    DrawSegmented(toolbarRect, items, metrics);
                    break;
            }

            return previewRect;
        }
        #endregion

        #region Layout
        private static void DrawEqualGrid(Rect toolbarRect, IList<PreviewToolbarItem> items, in PreviewToolbarMetrics metrics)
        {
            int count = items.Count;
            float availableWidth = toolbarRect.width - metrics.SidePadding * 2f - (count - 1) * metrics.ButtonGap;
            float slotWidth = Mathf.Max(1f, availableWidth / count);
            float x = toolbarRect.x + metrics.SidePadding;

            for (int i = 0; i < count; i++)
            {
                Rect slotRect = new Rect(x, toolbarRect.y, slotWidth, toolbarRect.height);
                DrawItem(toolbarRect, slotRect, items[i], metrics);
                x += slotWidth + metrics.ButtonGap;
            }
        }

        private static void DrawSegmented(Rect toolbarRect, IList<PreviewToolbarItem> items, in PreviewToolbarMetrics metrics)
        {
            float leftStart = toolbarRect.x + metrics.SidePadding;
            float leftWidth = MeasureGroupWidth(items, PreviewToolbarItemGroup.Left, metrics);
            float rightWidth = MeasureGroupWidth(items, PreviewToolbarItemGroup.Right, metrics);
            float rightStart = toolbarRect.xMax - metrics.SidePadding - rightWidth;

            DrawGroupForward(toolbarRect, items, PreviewToolbarItemGroup.Left, leftStart, metrics);
            DrawGroupForward(toolbarRect, items, PreviewToolbarItemGroup.Right, rightStart, metrics);

            float centerLeft = leftStart + leftWidth;
            float centerRight = rightStart;
            DrawCenterGroup(toolbarRect, items, centerLeft, centerRight, metrics);
        }

        private static void DrawCenterGroup(Rect toolbarRect, IList<PreviewToolbarItem> items, float centerLeft, float centerRight, in PreviewToolbarMetrics metrics)
        {
            float availableWidth = centerRight - centerLeft;
            if (availableWidth <= 0f)
                return;

            int centerCount = 0;
            int flexibleCount = 0;
            float fixedWidth = 0f;
            bool hasPrevious = false;
            bool previousDivider = false;

            for (int i = 0; i < items.Count; i++)
            {
                PreviewToolbarItem item = items[i];
                if (!IsCenterGroup(item.Group))
                    continue;

                if (hasPrevious)
                    fixedWidth += (previousDivider || item.Kind == PreviewToolbarItemKind.Divider) ? metrics.DividerGap : metrics.ButtonGap;

                if (item.Kind == PreviewToolbarItemKind.CustomSlot)
                {
                    flexibleCount++;
                    fixedWidth += Mathf.Max(0f, item.MinWidth);
                }
                else
                {
                    fixedWidth += ResolveFixedWidth(item, metrics);
                }

                previousDivider = item.Kind == PreviewToolbarItemKind.Divider;
                hasPrevious = true;
                centerCount++;
            }

            if (centerCount == 0)
                return;

            float extraSpace = Mathf.Max(0f, availableWidth - fixedWidth);
            float extraPerFlexible = flexibleCount > 0 ? extraSpace / flexibleCount : 0f;

            float x = centerLeft;
            bool started = false;
            bool prevWasDivider = false;

            for (int i = 0; i < items.Count; i++)
            {
                PreviewToolbarItem item = items[i];
                if (!IsCenterGroup(item.Group))
                    continue;

                if (started)
                    x += (prevWasDivider || item.Kind == PreviewToolbarItemKind.Divider) ? metrics.DividerGap : metrics.ButtonGap;

                float width = ResolveFixedWidth(item, metrics);
                if (item.Kind == PreviewToolbarItemKind.CustomSlot)
                    width += extraPerFlexible;

                // Prevent center content (for example the particle scrubber) from crossing into the right group.
                float remainingWidth = Mathf.Max(0f, centerRight - x);
                width = Mathf.Min(width, remainingWidth);
                if (width <= 0f)
                    break;

                Rect slotRect = new Rect(x, toolbarRect.y, width, toolbarRect.height);
                DrawItem(toolbarRect, slotRect, item, metrics);

                x += width;
                prevWasDivider = item.Kind == PreviewToolbarItemKind.Divider;
                started = true;
            }
        }

        private static void DrawGroupForward(
            Rect toolbarRect,
            IList<PreviewToolbarItem> items,
            PreviewToolbarItemGroup group,
            float startX,
            in PreviewToolbarMetrics metrics)
        {
            float x = startX;
            bool started = false;
            bool previousDivider = false;

            for (int i = 0; i < items.Count; i++)
            {
                PreviewToolbarItem item = items[i];
                if (item.Group != group)
                    continue;

                if (started)
                    x += (previousDivider || item.Kind == PreviewToolbarItemKind.Divider) ? metrics.DividerGap : metrics.ButtonGap;

                float width = ResolveFixedWidth(item, metrics);
                Rect slotRect = new Rect(x, toolbarRect.y, width, toolbarRect.height);
                DrawItem(toolbarRect, slotRect, item, metrics);

                x += width;
                previousDivider = item.Kind == PreviewToolbarItemKind.Divider;
                started = true;
            }
        }

        private static float MeasureGroupWidth(IList<PreviewToolbarItem> items, PreviewToolbarItemGroup group, in PreviewToolbarMetrics metrics)
        {
            float width = 0f;
            bool started = false;
            bool previousDivider = false;

            for (int i = 0; i < items.Count; i++)
            {
                PreviewToolbarItem item = items[i];
                if (item.Group != group)
                    continue;

                if (started)
                    width += (previousDivider || item.Kind == PreviewToolbarItemKind.Divider) ? metrics.DividerGap : metrics.ButtonGap;

                width += ResolveFixedWidth(item, metrics);
                previousDivider = item.Kind == PreviewToolbarItemKind.Divider;
                started = true;
            }

            return width;
        }
        #endregion

        #region Item Draw
        private static void DrawItem(Rect toolbarRect, Rect slotRect, PreviewToolbarItem item, in PreviewToolbarMetrics metrics)
        {
            switch (item.Kind)
            {
                case PreviewToolbarItemKind.Button:
                {
                    Rect buttonRect = AlignRectToHeight(slotRect, metrics.ButtonSize);
                    if (PreviewToolbarControls.DrawButton(
                            buttonRect,
                            item.IsActive,
                            item.FallbackText,
                            item.Tooltip,
                            item.IsEnabled,
                            item.IconNames ?? EmptyIconNames))
                    {
                        item.OnClick?.Invoke();
                    }

                    break;
                }
                case PreviewToolbarItemKind.Toggle:
                {
                    Rect toggleRect = AlignRectToHeight(slotRect, metrics.ButtonSize);
                    if (PreviewToolbarControls.DrawToggleButton(
                            toggleRect,
                            item.IsActive,
                            item.FallbackText,
                            item.Tooltip,
                            item.IsEnabled,
                            out bool newValue,
                            item.IconNames ?? EmptyIconNames)
                        && newValue != item.IsActive)
                    {
                        item.OnToggleChanged?.Invoke(newValue);
                    }

                    break;
                }
                case PreviewToolbarItemKind.SplitButton:
                {
                    Rect splitRect = AlignRectToHeight(slotRect, metrics.ButtonSize);
                    int action = PreviewToolbarControls.DrawSplitButton(
                        splitRect,
                        item.IsActive,
                        item.TintIcon,
                        item.FallbackText,
                        item.Tooltip,
                        item.IconNames ?? EmptyIconNames);
                    if (action == 1)
                        item.OnSplitPrimaryClick?.Invoke();
                    else if (action == 2)
                        item.OnSplitSecondaryClick?.Invoke(splitRect);

                    break;
                }
                case PreviewToolbarItemKind.Divider:
                {
                    Rect dividerRect = new Rect(slotRect.x, toolbarRect.y + metrics.DividerInset, 1f, metrics.DividerHeight);
                    PreviewToolbarTheme.DrawDivider(dividerRect);
                    break;
                }
                case PreviewToolbarItemKind.CustomSlot:
                {
                    float targetHeight = item.UseSliderHeight ? metrics.SliderHeight : metrics.ButtonSize;
                    Rect customRect = AlignRectToHeight(slotRect, targetHeight);
                    item.OnDrawCustom?.Invoke(customRect);
                    break;
                }
            }
        }

        private static Rect AlignRectToHeight(Rect rect, float targetHeight)
        {
            float y = Mathf.Round(rect.center.y - targetHeight * 0.5f);
            return new Rect(rect.x, y, rect.width, targetHeight);
        }

        private static float ResolveFixedWidth(PreviewToolbarItem item, in PreviewToolbarMetrics metrics)
        {
            if (item.FixedWidth > 0f)
                return item.FixedWidth;

            if (item.Kind == PreviewToolbarItemKind.Divider)
                return 1f;

            if (item.Kind == PreviewToolbarItemKind.CustomSlot)
                return Mathf.Max(item.MinWidth, metrics.MinSliderWidth);

            return metrics.ButtonSize;
        }

        private static bool IsCenterGroup(PreviewToolbarItemGroup group)
        {
            return group == PreviewToolbarItemGroup.Center || group == PreviewToolbarItemGroup.Auto;
        }
        #endregion
    }
}
