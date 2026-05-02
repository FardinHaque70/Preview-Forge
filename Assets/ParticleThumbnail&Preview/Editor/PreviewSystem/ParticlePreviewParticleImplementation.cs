using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ParticlePreviewParticleImplementation : IPrefabPreviewImplementation
    {
        private ParticlePrefabPreviewSession _session;
        private bool _playbackUpdateRegistered;
        private Action _requestRepaint;
        private static bool s_infoOverlayEnabled = true;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Particle;

        public void SetRepaintCallback(Action repaintCallback)
        {
            _requestRepaint = repaintCallback;
        }

        public bool EnsureReady(GameObject prefab)
        {
            if (prefab == null)
                return false;

            EnsureSession();
            _session.Setup(prefab);
            return _session.IsReady;
        }

        public void Cleanup(bool selectionIsEmpty)
        {
            DisablePlaybackUpdate();
            if (_session == null)
                return;

            if (selectionIsEmpty)
                ParticlePrefabPreviewSession.ClearSessionStateCache();

            _session.Cleanup(cacheState: !selectionIsEmpty);
            _session = null;
        }

        public GUIContent GetPreviewTitle(GameObject prefab)
        {
            string prefabName = prefab != null ? prefab.name : null;
            return string.IsNullOrEmpty(prefabName) ? GUIContent.none : new GUIContent(prefabName);
        }

        public void Draw(Rect rect, GUIStyle background, bool isInteractive)
        {
            if (_session == null || !_session.IsReady)
            {
                DisablePlaybackUpdate();
                return;
            }

            Rect previewRect = DrawToolbar(rect);
            if (_session.NeedsMotion)
                DrawMotionBar(ref previewRect);

            bool inputChanged = _session.HandleInput(previewRect, Event.current);
            bool cameraChanged = _session.TickInteraction();
            _session.Draw(previewRect, background);
            if (s_infoOverlayEnabled)
                DrawInfoPanel(previewRect);

            if (inputChanged || cameraChanged)
                RequestPreviewRepaint();

            if (_session.IsPlaying || _session.HasPendingCameraMotion)
                EnablePlaybackUpdate();
            else
                DisablePlaybackUpdate();
        }

        private void EnsureSession()
        {
            _session ??= new ParticlePrefabPreviewSession();
        }

        private Rect DrawToolbar(Rect fullRect)
        {
            const float rowHeight = 40f;
            const float buttonSize = 29f;
            const float sidePadding = 6f;
            const float buttonGap = 4f;
            const float transportGap = 16f;
            const float dividerGap = 10f;

            Rect toolbarRect = new Rect(fullRect.x, fullRect.y, fullRect.width, rowHeight);
            Rect previewRect = new Rect(fullRect.x, fullRect.y + rowHeight, fullRect.width, fullRect.height - rowHeight);
            PreviewToolbarTheme.DrawToolbarBackground(toolbarRect);

            float centerY = toolbarRect.y + rowHeight * 0.5f;
            float y = Mathf.Round(centerY - buttonSize * 0.5f);
            float leftX = toolbarRect.x + sidePadding;

            Rect playRect = new Rect(leftX, y, buttonSize, buttonSize);
            Rect restartRect = new Rect(playRect.xMax + buttonGap, y, buttonSize, buttonSize);
            float restartDividerX = restartRect.xMax + buttonGap + 6f;

            float rightGroupWidth = buttonSize * 2f + buttonGap;
            float rightGroupX = toolbarRect.xMax - sidePadding - rightGroupWidth;
            float dividerX = rightGroupX - dividerGap;
            float scrubberLeft = restartDividerX + transportGap;
            float scrubberRight = dividerX - dividerGap;
            float sliderWidth = Mathf.Max(56f, scrubberRight - scrubberLeft);
            Rect sliderRect = new Rect(scrubberLeft, Mathf.Round(centerY - 9f), sliderWidth, 18f);
            Rect infoRect = new Rect(rightGroupX, y, buttonSize, buttonSize);
            Rect gridRect = new Rect(infoRect.xMax + buttonGap, y, buttonSize, buttonSize);

            GUIContent playContent = PreviewToolbarControls.GetIconContent(_session.IsPlaying ? "Pause" : "Play", "Play or pause particle preview playback", "PlayButton", "d_PlayButton");
            if (_session.IsPlaying)
                playContent = PreviewToolbarControls.GetIconContent("Pause", "Pause particle preview playback", "PauseButton", "d_PauseButton");

            if (PreviewToolbarControls.DrawToggleButton(playRect, _session.IsPlaying, "Play", "Play or pause particle preview playback", true, out bool nextPlaying, _session.IsPlaying ? "PauseButton" : "PlayButton", _session.IsPlaying ? "d_PauseButton" : "d_PlayButton")
                && nextPlaying != _session.IsPlaying)
            {
                _session.SetPlaying(nextPlaying);
                if (nextPlaying)
                    EnablePlaybackUpdate();
                else
                    DisablePlaybackUpdate();

                RequestPreviewRepaint();
            }

            if (PreviewToolbarControls.DrawIconButton(restartRect, PreviewToolbarControls.GetIconContent("Restart", "Restart particle preview", "Refresh", "d_Refresh")))
            {
                _session.Restart();
                _session.SetPlaying(true);
                EnablePlaybackUpdate();
                RequestPreviewRepaint();
            }

            PreviewToolbarTheme.DrawDivider(new Rect(restartDividerX, toolbarRect.y + 8f, 1f, rowHeight - 16f));

            float safeMax = Mathf.Max(0.0001f, _session.MaxPlaybackTime);
            float clampedTime = Mathf.Clamp(_session.PlaybackTime, 0f, safeMax);
            float normalized = Mathf.Clamp01(clampedTime / safeMax);
            float newTime = DrawPlaybackSlider(sliderRect, clampedTime, safeMax, normalized, _session.IntensityProfile);

            PreviewToolbarTheme.DrawDivider(new Rect(dividerX, toolbarRect.y + 8f, 1f, rowHeight - 16f));

            if (PreviewToolbarControls.DrawToggleButton(infoRect, s_infoOverlayEnabled, "Info", "Toggle preview info", true, out bool nextInfoEnabled, "Search Icon", "d_Search Icon")
                && nextInfoEnabled != s_infoOverlayEnabled)
            {
                s_infoOverlayEnabled = nextInfoEnabled;
                RequestPreviewRepaint();
            }

            if (PreviewToolbarControls.DrawToggleButton(gridRect, _session.GridEnabled, "Grid", "Toggle preview grid", true, out bool gridEnabled, "Grid.BoxTool", "d_Grid.BoxTool")
                && gridEnabled != _session.GridEnabled)
            {
                _session.SetGridEnabled(gridEnabled);
                RequestPreviewRepaint();
            }

            if (!Mathf.Approximately(newTime, clampedTime))
            {
                _session.SetPlaying(false);
                _session.SetPlaybackTime(newTime);
                DisablePlaybackUpdate();
                RequestPreviewRepaint();
            }

            return previewRect;
        }

        private void DrawMotionBar(ref Rect previewRect)
        {
            const float barHeight = 28f;
            Rect barRect = new Rect(previewRect.x, previewRect.y, previewRect.width, barHeight);
            previewRect = new Rect(previewRect.x, previewRect.y + barHeight, previewRect.width, previewRect.height - barHeight);

            Color barBackground = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 0.95f)
                : new Color(0.78f, 0.78f, 0.78f, 0.95f);
            EditorGUI.DrawRect(barRect, barBackground);
            EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width, 1f), new Color(0f, 0f, 0f, 0.3f));

            float x = barRect.x + 8f;
            float y = barRect.y + 5f;
            float h = barHeight - 10f;

            GUI.Label(new Rect(x, y, 100f, h), "Movement Speed", EditorStyles.miniLabel);
            x += 104f;
            float newSpeed = Mathf.Max(0.01f, EditorGUI.FloatField(new Rect(x, y, 46f, h), _session.MotionSpeed, EditorStyles.numberField));
            x += 62f;

            GUI.Label(new Rect(x, y, 44f, h), "Shape", EditorStyles.miniLabel);
            x += 48f;
            ParticlePreviewMotionShape newShape = (ParticlePreviewMotionShape)EditorGUI.EnumPopup(
                new Rect(x, y, 80f, h),
                _session.MotionShape,
                EditorStyles.miniPullDown);
            x += 96f;

            GUI.Label(new Rect(x, y, 68f, h), "Shape Size", EditorStyles.miniLabel);
            x += 72f;
            float newSize = Mathf.Max(0.1f, EditorGUI.FloatField(new Rect(x, y, 46f, h), _session.MotionSize, EditorStyles.numberField));

            bool changed = false;
            if (!Mathf.Approximately(newSpeed, _session.MotionSpeed))
            {
                _session.SetMotionSpeed(newSpeed);
                changed = true;
            }

            if (newShape != _session.MotionShape)
            {
                _session.SetMotionShape(newShape);
                changed = true;
            }

            if (!Mathf.Approximately(newSize, _session.MotionSize))
            {
                _session.SetMotionSize(newSize);
                changed = true;
            }

            if (changed)
                RequestPreviewRepaint();

            Event evt = Event.current;
            if (barRect.Contains(evt.mousePosition)
                && GUIUtility.hotControl == 0
                && (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag || evt.type == EventType.ScrollWheel))
            {
                evt.Use();
            }
        }

        private void DrawInfoPanel(Rect previewRect)
        {
            if (_session == null || !_session.IsReady || Event.current.type != EventType.Repaint)
                return;

            string line1 = $"Time: {_session.PlaybackTime:F2}s";
            string line2 = $"Total: {_session.MaxPlaybackTime:F2}s";
            string line3 = $"Peak Visible Particles: {_session.PeakVisibleParticleCount}";
            string line4 = $"Sub Particle Systems: {_session.SubParticleSystemCount}";

            const float padding = 4f;
            const float spacing = 2f;
            GUIStyle valueStyle = PreviewToolbarTheme.InfoValueStyle;

            float contentWidth = Mathf.Max(
                valueStyle.CalcSize(new GUIContent(line1)).x,
                valueStyle.CalcSize(new GUIContent(line2)).x,
                valueStyle.CalcSize(new GUIContent(line3)).x,
                valueStyle.CalcSize(new GUIContent(line4)).x);

            float contentHeight =
                valueStyle.lineHeight +
                spacing +
                valueStyle.lineHeight +
                spacing +
                valueStyle.lineHeight +
                spacing +
                valueStyle.lineHeight;

            float panelWidth = contentWidth + padding * 2f;
            float panelHeight = contentHeight + padding * 2f;
            Rect panelRect = new Rect(
                previewRect.x + 5f,
                previewRect.yMax - panelHeight - 5f,
                panelWidth,
                panelHeight);

            float y = panelRect.y + padding;
            Rect line1Rect = new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, valueStyle.lineHeight);
            GUI.Label(line1Rect, line1, valueStyle);
            y += valueStyle.lineHeight + spacing;

            Rect line2Rect = new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, valueStyle.lineHeight);
            GUI.Label(line2Rect, line2, valueStyle);
            y += valueStyle.lineHeight + spacing;

            Rect line3Rect = new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, valueStyle.lineHeight);
            GUI.Label(line3Rect, line3, valueStyle);
            y += valueStyle.lineHeight + spacing;

            Rect line4Rect = new Rect(panelRect.x + padding, y, panelRect.width - padding * 2f, valueStyle.lineHeight);
            GUI.Label(line4Rect, line4, valueStyle);
        }

        private float DrawPlaybackSlider(Rect rect, float currentTime, float maxTime, float normalizedTime, IReadOnlyList<float> intensityProfile)
        {
            Event evt = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);

            Rect trackRect = new Rect(rect.x, rect.y + rect.height * 0.5f - 3f, rect.width, 6f);
            float thumbCenterX = Mathf.Lerp(trackRect.x, trackRect.xMax, normalizedTime);
            Rect thumbRect = new Rect(thumbCenterX - 5f, rect.y + rect.height * 0.5f - 8f, 10f, 16f);

            EditorGUI.DrawRect(trackRect, PreviewToolbarTheme.GetSliderTrackColor());
            DrawIntensityProfile(trackRect, intensityProfile);
            DrawCurrentTimeMarker(trackRect, thumbCenterX);
            EditorGUI.DrawRect(thumbRect, PreviewToolbarTheme.GetSliderThumbColor(GUIUtility.hotControl == controlId));

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        GUIUtility.keyboardControl = 0;
                        evt.Use();
                        return TimeFromMouse(rect, evt.mousePosition.x, maxTime);
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        evt.Use();
                        return TimeFromMouse(rect, evt.mousePosition.x, maxTime);
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                        return TimeFromMouse(rect, evt.mousePosition.x, maxTime);
                    }
                    break;

                case EventType.Ignore:
                case EventType.MouseLeaveWindow:
                    if (GUIUtility.hotControl == controlId)
                        GUIUtility.hotControl = 0;
                    break;
            }

            return currentTime;
        }

        private static void DrawCurrentTimeMarker(Rect trackRect, float thumbCenterX)
        {
            Rect glowRect = new Rect(Mathf.Round(thumbCenterX - 2f), trackRect.y - 2f, 4f, trackRect.height + 4f);
            Rect markerRect = new Rect(Mathf.Round(thumbCenterX - 1f), trackRect.y - 3f, 2f, trackRect.height + 6f);

            EditorGUI.DrawRect(glowRect, new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(markerRect, new Color(1f, 1f, 1f, 0.95f));
        }

        private static void DrawIntensityProfile(Rect trackRect, IReadOnlyList<float> profile)
        {
            if (profile == null || profile.Count < 2)
                return;

            float segmentWidth = trackRect.width / (profile.Count - 1);
            for (int i = 0; i < profile.Count - 1; i++)
            {
                float left = Mathf.Clamp01(profile[i]);
                float right = Mathf.Clamp01(profile[i + 1]);
                float strength = Mathf.Clamp01(Mathf.Max(left, right));
                Color segmentColor = Color.Lerp(PreviewToolbarTheme.GetSliderFillStart(), PreviewToolbarTheme.GetSliderFillEnd(), strength);
                Rect segmentRect = new Rect(trackRect.x + segmentWidth * i, trackRect.y, Mathf.Ceil(segmentWidth + 1f), trackRect.height);
                EditorGUI.DrawRect(segmentRect, segmentColor);
            }
        }

        private static float TimeFromMouse(Rect rect, float mouseX, float maxTime)
        {
            float t = Mathf.InverseLerp(rect.x, rect.xMax, mouseX);
            return Mathf.Lerp(0f, maxTime, Mathf.Clamp01(t));
        }

        private void EnablePlaybackUpdate()
        {
            if (_playbackUpdateRegistered)
                return;

            PreviewUpdateLoop.EnsureRegistered(ref _playbackUpdateRegistered, OnPlaybackUpdate);
        }

        private void DisablePlaybackUpdate()
        {
            if (!_playbackUpdateRegistered)
                return;

            PreviewUpdateLoop.EnsureUnregistered(ref _playbackUpdateRegistered, OnPlaybackUpdate);
        }

        private void OnPlaybackUpdate()
        {
            if (_session == null || !_session.IsReady)
            {
                DisablePlaybackUpdate();
                return;
            }

            bool needsRepaint = false;
            if (_session.IsPlaying && _session.TickPlayback())
                needsRepaint = true;

            if (_session.TickInteraction())
                needsRepaint = true;

            if (!_session.IsPlaying && !_session.HasPendingCameraMotion)
            {
                DisablePlaybackUpdate();
                return;
            }

            if (needsRepaint)
                RequestPreviewRepaint();
        }

        private void RequestPreviewRepaint()
        {
            _requestRepaint?.Invoke();
        }

    }
}
