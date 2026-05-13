using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    // Connects the prefab preview host to the dedicated UI preview session and exposes a focused toolbar.
    internal sealed class UiPrefabPreviewImplementation : IPrefabPreviewImplementation
    {
        private static readonly string[] BoundsIcons = PreviewToolbarIconUtility.BuildIconNames("Model_Bounds_Round_White.png", "d_ScaleTool", "ScaleTool");
        private static readonly string[] GridIcons = PreviewToolbarIconUtility.BuildIconNames("Particle_GridOn_Round_White.png", "d_Grid.BoxTool", "Grid.BoxTool");
        private const int SetupWarmupRepaintFrames = 6;

        private readonly UiPrefabPreviewSession _session = new();
        private readonly List<PreviewToolbarItem> _toolbarItems = new();
        private readonly PreviewToolbarCommonFeatureBinding _boundsFeature;
        private readonly PreviewToolbarCommonFeatureBinding _gridFeature;
        private Action _requestRepaint;
        private bool _updateRegistered;
        private int _warmupRepaintFramesRemaining;
        private int _lastPrefabInstanceId;
        private string _lastPrefabAssetPath;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Ui;

        internal UiPrefabPreviewImplementation()
        {
            _boundsFeature = PreviewToolbarCommonFeatures.CreateBoundsToggle(_session, RequestRepaint, BoundsIcons, PreviewToolbarItemGroup.Right);
            _gridFeature = PreviewToolbarCommonFeatures.CreateGridToggle(_session, RequestRepaint, GridIcons, PreviewToolbarItemGroup.Right);
        }

        public void SetRepaintCallback(Action repaintCallback)
        {
            _requestRepaint = repaintCallback;
        }

        public bool EnsureReady(GameObject prefab)
        {
            bool isNewTarget = IsNewTarget(prefab);
            _session.Setup(prefab);
            bool ready = _session.IsReady;
            if (ready && isNewTarget)
            {
                _warmupRepaintFramesRemaining = SetupWarmupRepaintFrames;
                RequestRepaint();
            }

            return ready;
        }

        public void Cleanup(bool selectionIsEmpty)
        {
            DisableUpdate();
            if (selectionIsEmpty)
                UiPrefabPreviewSession.ClearSessionStateCache();

            _session.Cleanup(cacheState: !selectionIsEmpty);
            if (selectionIsEmpty)
            {
                _lastPrefabInstanceId = 0;
                _lastPrefabAssetPath = null;
                _warmupRepaintFramesRemaining = 0;
            }
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
                RequestRepaint();

            if (_warmupRepaintFramesRemaining > 0)
            {
                _warmupRepaintFramesRemaining--;
                RequestRepaint();
            }

            if (_session.HasPendingCameraMotion || _warmupRepaintFramesRemaining > 0)
                EnableUpdate();
            else
                DisableUpdate();
        }

        public GUIContent GetPreviewTitle(GameObject prefab)
        {
            string prefabName = prefab != null ? prefab.name : null;
            return string.IsNullOrEmpty(prefabName) ? new GUIContent("UI Preview") : new GUIContent(prefabName);
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
            _toolbarItems.Add(_gridFeature.Item);
        }

        private void UpdateToolbarItemState()
        {
            PreviewToolbarCommonFeatures.Refresh(_session, _boundsFeature);
            PreviewToolbarCommonFeatures.Refresh(_session, _gridFeature);
        }

        private void EnableUpdate()
        {
            PreviewUpdateLoop.EnsureRegistered(ref _updateRegistered, OnUpdate);
        }

        private void DisableUpdate()
        {
            PreviewUpdateLoop.EnsureUnregistered(ref _updateRegistered, OnUpdate);
        }

        private void OnUpdate()
        {
            RequestRepaint();
        }

        private void RequestRepaint()
        {
            _requestRepaint?.Invoke();
        }

        private bool IsNewTarget(GameObject prefab)
        {
            int instanceId = prefab != null ? prefab.GetInstanceID() : 0;
            string assetPath = prefab != null ? AssetDatabase.GetAssetPath(prefab) : null;
            bool changed = instanceId != _lastPrefabInstanceId
                           || !string.Equals(assetPath, _lastPrefabAssetPath, StringComparison.Ordinal);
            _lastPrefabInstanceId = instanceId;
            _lastPrefabAssetPath = assetPath;
            return changed;
        }

        private void DrawInfoPanel(Rect previewRect)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            string line1 = $"RectTransforms: {_session.RectTransformCount}";
            string line2 = $"Canvases: {_session.CanvasCount}";

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
