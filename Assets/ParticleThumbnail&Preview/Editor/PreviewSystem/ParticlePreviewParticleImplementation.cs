using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Provides the particle-prefab preview implementation bridge between the host editor and the underlying particle preview session.

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class ParticlePreviewParticleImplementation : IPrefabPreviewImplementation
    {
        private static readonly string[] PlayIcons = BuildIconNames("Particle_PlayArrow_Round_White.png", "d_PlayButton", "PlayButton");
        private static readonly string[] PauseIcons = BuildIconNames("Particle_Pause_Round_White.png", "d_PauseButton", "PauseButton");
        private static readonly string[] RestartIcons = BuildIconNames("Particle_Replay_Round_White.png", "d_Refresh", "Refresh");
        private static readonly string[] InfoIcons = BuildIconNames("Particle_Info_Round_White.png", "d_Search Icon", "Search Icon");
        private static readonly string[] LightsIcons = BuildIconNames("Model_Lightbulb_Round_White.png", "d_SceneViewLighting", "SceneViewLighting");
        private static readonly string[] GridIcons = BuildIconNames("Particle_GridOn_Round_White.png", "d_Grid.BoxTool", "Grid.BoxTool");

        private const int PlayIndex = 0;
        private const int RestartIndex = 1;
        private const int ScrubberIndex = 3;
        private const int InfoIndex = 5;
        private const int LightsIndex = 6;
        private const int GridIndex = 7;

        private ParticlePrefabPreviewSession _session;
        private readonly List<PreviewToolbarItem> _toolbarItems = new(8);
        private bool _playbackUpdateRegistered;
        private Action _requestRepaint;
        private static bool s_infoOverlayEnabled = true;

        public PrefabPreviewTargetKind Kind => PrefabPreviewTargetKind.Particle;

        private static string[] BuildIconNames(string fileName, params string[] fallbacks)
        {
            string[] assetPaths = PreviewInstallLayout.BuildAssetPaths("Editor/Common/PreviewAssets/ToolbarIcons/" + fileName);
            var values = new List<string>(assetPaths.Length + (fallbacks?.Length ?? 0));
            values.AddRange(assetPaths);
            if (fallbacks != null && fallbacks.Length > 0)
                values.AddRange(fallbacks);
            return values.ToArray();
        }

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

        #region Toolbar
        private Rect DrawToolbar(Rect fullRect)
        {
            EnsureToolbarItems();
            UpdateToolbarItemState();

            PreviewToolbarMetrics metrics = PreviewToolbarMetrics.FromSettings();
            return PreviewToolbarRenderer.Draw(fullRect, PreviewToolbarLayoutPreset.Segmented, _toolbarItems, metrics);
        }

        private void EnsureToolbarItems()
        {
            if (_toolbarItems.Count > 0)
                return;

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, PreviewToolbarItemGroup.Left)
            {
                OnToggleChanged = OnPlayToggle,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Button, PreviewToolbarItemGroup.Left)
            {
                OnClick = OnRestartClicked,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Divider, PreviewToolbarItemGroup.Left));

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.CustomSlot, PreviewToolbarItemGroup.Center)
            {
                MinWidth = 40f,
                UseSliderHeight = true,
                OnDrawCustom = DrawScrubberSlot,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Divider, PreviewToolbarItemGroup.Right));

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, PreviewToolbarItemGroup.Right)
            {
                OnToggleChanged = OnInfoToggled,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, PreviewToolbarItemGroup.Right)
            {
                OnToggleChanged = OnLightsToggled,
            });

            _toolbarItems.Add(new PreviewToolbarItem(PreviewToolbarItemKind.Toggle, PreviewToolbarItemGroup.Right)
            {
                OnToggleChanged = OnGridToggled,
            });
        }

        private void UpdateToolbarItemState()
        {
            PreviewToolbarItem play = _toolbarItems[PlayIndex];
            play.IsActive = _session.IsPlaying;
            play.IsEnabled = true;
            play.FallbackText = "Play";
            play.Tooltip = "Play or pause particle preview playback";
            play.IconNames = _session.IsPlaying ? PauseIcons : PlayIcons;

            PreviewToolbarItem restart = _toolbarItems[RestartIndex];
            restart.IsActive = false;
            restart.IsEnabled = true;
            restart.FallbackText = "Restart";
            restart.Tooltip = "Restart particle preview";
            restart.IconNames = RestartIcons;

            PreviewToolbarItem scrubber = _toolbarItems[ScrubberIndex];
            scrubber.MinWidth = 40f;

            PreviewToolbarItem info = _toolbarItems[InfoIndex];
            info.IsActive = s_infoOverlayEnabled;
            info.IsEnabled = true;
            info.FallbackText = "Info";
            info.Tooltip = "Toggle preview info";
            info.IconNames = InfoIcons;

            PreviewToolbarItem lights = _toolbarItems[LightsIndex];
            lights.IsActive = _session.LightsEnabled;
            lights.IsEnabled = true;
            lights.FallbackText = "Lights";
            lights.Tooltip = "Toggle shared preview lights";
            lights.IconNames = LightsIcons;

            PreviewToolbarItem grid = _toolbarItems[GridIndex];
            grid.IsActive = _session.GridEnabled;
            grid.IsEnabled = true;
            grid.FallbackText = "Grid";
            grid.Tooltip = "Toggle preview grid";
            grid.IconNames = GridIcons;
        }

        private void OnPlayToggle(bool nextPlaying)
        {
            if (nextPlaying == _session.IsPlaying)
                return;

            _session.SetPlaying(nextPlaying);
            if (nextPlaying)
                EnablePlaybackUpdate();
            else
                DisablePlaybackUpdate();

            RequestPreviewRepaint();
        }

        private void OnRestartClicked()
        {
            _session.Restart();
            _session.SetPlaying(true);
            EnablePlaybackUpdate();
            RequestPreviewRepaint();
        }

        private void DrawScrubberSlot(Rect rect)
        {
            float safeMax = Mathf.Max(0.0001f, _session.MaxPlaybackTime);
            float clampedTime = Mathf.Clamp(_session.PlaybackTime, 0f, safeMax);
            float normalized = Mathf.Clamp01(clampedTime / safeMax);
            float newTime = DrawPlaybackSlider(rect, clampedTime, safeMax, normalized, _session.IntensityProfile);

            if (!Mathf.Approximately(newTime, clampedTime))
            {
                _session.SetPlaying(false);
                _session.SetPlaybackTime(newTime);
                DisablePlaybackUpdate();
                RequestPreviewRepaint();
            }
        }

        private void OnInfoToggled(bool value)
        {
            if (value == s_infoOverlayEnabled)
                return;

            s_infoOverlayEnabled = value;
            RequestPreviewRepaint();
        }

        private void OnLightsToggled(bool value)
        {
            if (value == _session.LightsEnabled)
                return;

            _session.SetLightsEnabled(value);
            RequestPreviewRepaint();
        }

        private void OnGridToggled(bool value)
        {
            if (value == _session.GridEnabled)
                return;

            _session.SetGridEnabled(value);
            RequestPreviewRepaint();
        }
        #endregion

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

        #region Update Loop
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
        #endregion

        private void RequestPreviewRepaint()
        {
            _requestRepaint?.Invoke();
        }
    }
}
