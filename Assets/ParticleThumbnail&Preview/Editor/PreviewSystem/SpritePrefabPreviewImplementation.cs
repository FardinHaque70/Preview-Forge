using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Connects the prefab preview host to the dedicated sprite preview session and exposes a lean 2D-first toolbar.

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class SpritePrefabPreviewImplementation : IPrefabPreviewImplementation
    {
        private static readonly string[] ColliderIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Collider_BoxOutline_Round_White.png", "d_BoxCollider Icon", "BoxCollider Icon", "d_EditCollider", "EditCollider");
        private static readonly string[] BoundsIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Bounds_Round_White.png", "d_ScaleTool", "ScaleTool");
        private static readonly string[] GridIcons = PreviewToolbarIconUtility.BuildIconNames("Particle_GridOn_Round_White.png", "d_Grid.BoxTool", "Grid.BoxTool");

        private readonly SpritePrefabPreviewSession _session = new();
        private readonly List<PreviewToolbarItem> _toolbarItems = new();
        private readonly PreviewToolbarCommonFeatureBinding _boundsFeature;
        private readonly PreviewToolbarCommonFeatureBinding _colliderFeature;
        private readonly PreviewToolbarCommonFeatureBinding _modeFeature;
        private readonly PreviewToolbarCommonFeatureBinding _gridFeature;
        private Action _requestRepaint;
        private bool _updateRegistered;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Sprite;

        internal SpritePrefabPreviewImplementation()
        {
            _boundsFeature = PreviewToolbarCommonFeatures.CreateBoundsToggle(_session, RequestPreviewRepaint, BoundsIcons, PreviewToolbarItemGroup.Right);
            _colliderFeature = PreviewToolbarCommonFeatures.CreateColliderToggle(_session, RequestPreviewRepaint, ColliderIcons, PreviewToolbarItemGroup.Right);
            _modeFeature = PreviewToolbarCommonFeatures.CreateModeButton(_session, RequestPreviewRepaint, PreviewToolbarItemGroup.Right);
            _gridFeature = PreviewToolbarCommonFeatures.CreateGridToggle(_session, RequestPreviewRepaint, GridIcons, PreviewToolbarItemGroup.Right);
        }

        public void SetRepaintCallback(Action repaintCallback)
        {
            _requestRepaint = repaintCallback;
        }

        public bool EnsureReady(GameObject prefab)
        {
            _session.Setup(prefab);
            return _session.IsReady;
        }

        public void Cleanup(bool selectionIsEmpty)
        {
            DisableUpdate();
            if (selectionIsEmpty)
                SpritePrefabPreviewSession.ClearSessionStateCache();

            _session.Cleanup(cacheState: !selectionIsEmpty);
        }

        public void Draw(Rect rect, GUIStyle background, bool isInteractive)
        {
            if (!_session.IsReady)
            {
                DisableUpdate();
                return;
            }

            Rect previewRect = DrawToolbar(rect);
            bool inputChanged = _session.HandleInput(previewRect, Event.current);
            bool cameraChanged = _session.TickInteraction();
            _session.Draw(previewRect, background);

            if (PreviewSettings.ShowStatsEnabled)
                DrawInfoPanel(previewRect);

            if (inputChanged || cameraChanged)
                RequestPreviewRepaint();

            if (_session.HasPendingCameraMotion)
                EnableUpdate();
            else
                DisableUpdate();
        }

        public GUIContent GetPreviewTitle(GameObject prefab)
        {
            string prefabName = prefab != null ? prefab.name : null;
            return string.IsNullOrEmpty(prefabName) ? new GUIContent("Sprite Preview") : new GUIContent(prefabName);
        }

        private Rect DrawToolbar(Rect fullRect)
        {
            EnsureToolbarItems();
            UpdateToolbarItemState();

            PreviewToolbarMetrics metrics = PreviewToolbarMetrics.FromSettings();
            return PreviewToolbarRenderer.Draw(fullRect, PreviewToolbarLayoutPreset.EqualGrid, _toolbarItems, metrics);
        }

        private void EnsureToolbarItems()
        {
            if (_toolbarItems.Count > 0)
                return;

            _toolbarItems.Add(_boundsFeature.Item);
            _toolbarItems.Add(_colliderFeature.Item);
            _toolbarItems.Add(_modeFeature.Item);
            _toolbarItems.Add(_gridFeature.Item);
        }

        private void UpdateToolbarItemState()
        {
            PreviewToolbarCommonFeatures.Refresh(_session, _boundsFeature);
            PreviewToolbarCommonFeatures.Refresh(_session, _colliderFeature);
            PreviewToolbarCommonFeatures.Refresh(_session, _modeFeature);
            PreviewToolbarCommonFeatures.Refresh(_session, _gridFeature);
        }

        private void EnableUpdate()
        {
            PreviewUpdateLoop.EnsureRegistered(ref _updateRegistered, RepaintUpdate);
        }

        private void DisableUpdate()
        {
            PreviewUpdateLoop.EnsureUnregistered(ref _updateRegistered, RepaintUpdate);
        }

        private void RepaintUpdate()
        {
            RequestPreviewRepaint();
        }

        private void RequestPreviewRepaint()
        {
            _requestRepaint?.Invoke();
        }

        private void DrawInfoPanel(Rect previewRect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string line1 = $"Sprites: {_session.SpriteRendererCount}";
            string line2 = $"Colliders: {_session.Collider2DCount}";

            const float padding = 4f;
            const float spacing = 2f;
            GUIStyle style = PreviewToolbarTheme.InfoValueStyle;

            float width = Mathf.Max(style.CalcSize(new GUIContent(line1)).x, style.CalcSize(new GUIContent(line2)).x) + padding * 2f;
            float height = style.lineHeight * 2f + spacing + padding * 2f;
            Rect panelRect = new Rect(previewRect.x + 5f, previewRect.yMax - height - 5f, width, height);

            float y = panelRect.y + padding;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line1, style);
            y += style.lineHeight + spacing;
            GUI.Label(new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, style.lineHeight), line2, style);
        }
    }
}
