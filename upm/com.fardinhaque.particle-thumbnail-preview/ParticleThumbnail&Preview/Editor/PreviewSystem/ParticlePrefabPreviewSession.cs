using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// Builds and maintains isolated particle prefab preview scenes, handling simulation stepping, framing, interaction, and cleanup behavior.

namespace ParticleThumbnailAndPreview.Editor
{
	internal enum ParticlePreviewMotionShape
	{
		Circle = 0,
		Line = 1,
		Figure8 = 2,
	}

	internal sealed class ParticlePrefabPreviewSession
	{
		#region Constants

		private const double SessionRestoreWindowSeconds = 2.0d;
		private const int MaxCachedSessionStates = 64;
		private const int IntensityProfileSampleCount = 64;
		private const int MaxParticleBuffer = 10000;

		private const float IntroZoomMultiplier = 1.5f;
		private const float IntroZoomMinimumExtraDistance = 0.05f;
		private const float ZoomSmooth = 8f;
		private const float OrbitVelocitySmoothing = 0.35f;
		private const float FallbackOrbitInputDeltaTime = 1f / 60f;
		private const double OrbitHoldStillResetSeconds = 0.08d;
		private const float OrbitDragDeltaDeadzoneSqr = 0.0001f;
		private const float OrbitEpsilon = 0.01f;
		private const float DistanceEpsilon = 0.001f;
		private const float PivotEpsilon = 0.0001f;
		private const float AngularVelocityEpsilon = 0.01f;

			private const float MinDistance = 0.25f;
			private const float MaxDistance = 300f;
			private const float OrbitSensitivity = 1.2f;
			private const float ZoomFactorPerScrollUnit = 0.1f;
			private const float PitchMin = -85f;
			private const float PitchMax = 85f;
			private const float MaxDeltaTime = 0.05f;
			private const float MinAxisExtent = 0.02f;

		private const float FramingScanMaxSeconds = 2f;
		private const float FramingScanStep = 1f / 60f;
		private const float FramingBoundsPadding = 0.08f;
		private const float MotionDistanceBaseMultiplier = 1.5f;
		private const float MinContainmentDistance = 0.3f;
		private const float NonMotionFitDistanceScale = 0.8f;
		private const float MotionFitDistanceScale = 0.85f;
		private const float MaxAutoFrameDistance = 20f;

		#endregion

		#region Runtime State

		private readonly List<ParticleSystem> _particleSystems = new();
		private readonly List<Renderer> _renderers = new();
		private static readonly ParticleSystem.Particle[] ParticleBuffer = new ParticleSystem.Particle[MaxParticleBuffer];

		private PreviewRenderUtility _preview;
		private GameObject _previewRoot;
		private Light _sunLight;
		private Light _rimLight;
		private int _prefabInstanceId;
		private string _prefabAssetPath;
		private Vector3 _authoredRootPosition;
		private Quaternion _authoredRootRotation;
		private bool _needsMotion;

		private Vector3 _pivot = Vector3.zero;
		private Vector3 _targetPivot = Vector3.zero;
		private Vector2 _orbit = new Vector2(35f, 18f);
		private Vector2 _targetOrbit = new Vector2(35f, 18f);
		private Vector2 _orbitAngularVelocity = Vector2.zero;
		private float _distance = 8f;
		private float _targetDistance = 8f;
		private bool _isOrbitDragging;
		private double _lastOrbitInputTime = -1d;

		private bool _playing;
		private bool _gridEnabled = true;
		private bool _lightsEnabled;
		private bool _hasLoopingSystem;
		private ParticlePreviewMotionShape _motionShape = ParticlePreviewMotionShape.Circle;
		private float _motionSpeed = 60f;
		private float _motionSize = 3f;
		private float _maxPlaybackTime = 5f;
		private float[] _intensityProfile = Array.Empty<float>();
			private int _peakVisibleParticleCount;
			private int _subParticleSystemCount;

			private double _lastUpdateTime = -1d;
			private double _lastInteractionUpdateTime = -1d;
			private double _playbackAccumulatorSeconds;
			private float _playbackTime;
			private static readonly Dictionary<string, SessionStateSnapshot> SessionStateByAssetPath = new();
			private static string s_lastSetupAssetPath;
		private static readonly PreviewCameraInteractionConfig CameraInteractionConfig = new(
			PitchMin,
			PitchMax,
			MaxDeltaTime,
			OrbitEpsilon,
			DistanceEpsilon,
			PivotEpsilon,
			AngularVelocityEpsilon,
			(float)OrbitHoldStillResetSeconds,
			ZoomSmooth);

		#endregion

		#region Session Snapshot Cache

			private struct SessionStateSnapshot
			{
				internal float PlaybackTime;
				internal bool Playing;
				internal bool GridEnabled;
				internal bool LightsEnabled;
				internal Vector2 Orbit;
				internal float Distance;
				internal float TargetDistance;
				internal Vector3 Pivot;
				internal double SavedAt;
			}

		#endregion

		#region Read-Only Surface

		internal bool IsReady => _preview != null && _previewRoot != null;
		internal bool IsPlaying => _playing;
		internal bool NeedsMotion => _needsMotion;
		internal bool GridEnabled => _gridEnabled;
		internal bool LightsEnabled => _lightsEnabled;
		internal ParticlePreviewMotionShape MotionShape => _motionShape;
		internal float MotionSpeed => _motionSpeed;
		internal float MotionSize => _motionSize;
		internal float PlaybackTime => _playbackTime;
		internal float MaxPlaybackTime => _maxPlaybackTime;
		internal IReadOnlyList<float> IntensityProfile => _intensityProfile;
		internal int PeakVisibleParticleCount => _peakVisibleParticleCount;
		internal int SubParticleSystemCount => _subParticleSystemCount;
		internal bool HasPendingCameraMotion => ComputeHasPendingCameraMotion();

		#endregion

		#region Session Setup and Teardown

		internal void Setup(GameObject prefab)
		{
			if (prefab == null)
				return;

			int instanceId = prefab.GetInstanceID();
			string assetPath = AssetDatabase.GetAssetPath(prefab);
			bool isTransientRebuildOfSameSelection = !string.IsNullOrEmpty(assetPath)
			                                         && string.Equals(s_lastSetupAssetPath, assetPath, StringComparison.Ordinal);
			bool isSwitchingToDifferentPrefab = IsReady
			                                    && !string.IsNullOrEmpty(_prefabAssetPath)
			                                    && !string.Equals(_prefabAssetPath, assetPath, StringComparison.Ordinal);

			if (IsReady && _prefabInstanceId == instanceId)
				return;

			if (IsReady && !string.IsNullOrEmpty(_prefabAssetPath)
			            && string.Equals(_prefabAssetPath, assetPath, StringComparison.Ordinal))
			{
				_prefabInstanceId = instanceId;
				return;
			}

			Cleanup(cacheState: !isSwitchingToDifferentPrefab);

			_preview = new PreviewRenderUtility(true);
			_preview.camera.clearFlags = CameraClearFlags.SolidColor;
			_preview.camera.backgroundColor = ParticlePreviewSettings.BackgroundColor;
			_preview.camera.fieldOfView = 30f;
			_preview.camera.nearClipPlane = 0.01f;
			_preview.camera.farClipPlane = 1000f;
			_preview.camera.orthographic = false;
			PreviewLightingSystem.EnsureSunLight(_preview, ref _sunLight);
			PreviewLightingSystem.EnsureRimLight(_preview, ref _rimLight);

			_previewRoot = UnityEngine.Object.Instantiate(prefab);
			_previewRoot.name = "ParticlePreviewRoot";
			_previewRoot.hideFlags = HideFlags.HideAndDontSave;
			PreviewHierarchyUtility.ForceActivateHierarchy(_previewRoot);
			_previewRoot.transform.position = Vector3.zero;
			// Preserve authored prefab root rotation/scale for direction-dependent effects.
			_previewRoot.transform.rotation = prefab.transform.rotation;
			_authoredRootPosition = _previewRoot.transform.position;
			_authoredRootRotation = _previewRoot.transform.rotation;
			_pivot = _authoredRootPosition;
			_targetPivot = _pivot;

				_preview.AddSingleGO(_previewRoot);
				_previewRoot.GetComponentsInChildren(true, _particleSystems);
				SanitizeCustomSimulationSpaces();
				_previewRoot.GetComponentsInChildren(true, _renderers);
				ParticleRenderCompatibilityUtility.SetRenderersEnabled(_renderers, false);
				_needsMotion = ParticleMotionDetectionUtility.NeedsMotion(_particleSystems);
				_motionShape = ParticlePreviewMotionShape.Circle;
				_motionSpeed = ParticlePreviewSettings.MotionSpeed;
				_motionSize = ParticlePreviewSettings.MotionRadius;
				_gridEnabled = ParticlePreviewSettings.SharedGridDefaultEnabled;
				_lightsEnabled = false;

			EnsureDeterministicSeeds();
			ComputePlaybackRangeAndLoopingMode();
			FrameCameraToContent();
			RebuildIntensityProfile();
			RestartInternal();

			bool shouldRestoreState = !isSwitchingToDifferentPrefab && isTransientRebuildOfSameSelection;
			if (shouldRestoreState && TryRestoreSessionState(assetPath, out SessionStateSnapshot restored))
			{
				_orbit = restored.Orbit;
				_targetOrbit = restored.Orbit;
				_pivot = restored.Pivot;
				_targetPivot = restored.Pivot;
				float restoredDistance = Mathf.Clamp(restored.Distance, MinDistance, MaxDistance);
				_distance = restoredDistance;
				float restoredTargetDistance = restored.TargetDistance > 0.0001f
					? Mathf.Clamp(restored.TargetDistance, MinDistance, MaxDistance)
					: restoredDistance;
				_targetDistance = restoredTargetDistance;
				_orbitAngularVelocity = Vector2.zero;
				_isOrbitDragging = false;
				_lastOrbitInputTime = -1d;

					float restoredTime = Mathf.Clamp(restored.PlaybackTime, 0f, Mathf.Max(0.0001f, _maxPlaybackTime));
					if (restoredTime > 0.0001f)
						SimulateToTime(restoredTime);

					_playbackTime = restoredTime;
					_playing = restored.Playing;
					_gridEnabled = restored.GridEnabled;
					_lightsEnabled = restored.LightsEnabled;
				}
				else
				{
					_playing = ParticlePreviewSettings.Autoplay;
					_lightsEnabled = false;
				}

				_lastUpdateTime = -1d;
				_playbackAccumulatorSeconds = 0d;
				_lastInteractionUpdateTime = -1d;
				_prefabInstanceId = instanceId;
				_prefabAssetPath = assetPath;
				s_lastSetupAssetPath = assetPath;
			}

		internal void Cleanup(bool cacheState = true)
		{
			if (cacheState)
				CacheCurrentSessionState();

			_particleSystems.Clear();
			_renderers.Clear();
			_prefabInstanceId = 0;
			_prefabAssetPath = null;
			_playing = false;
			_playbackTime = 0f;
			_lastUpdateTime = -1d;
			_playbackAccumulatorSeconds = 0d;
			_hasLoopingSystem = false;
			_maxPlaybackTime = 5f;
			_intensityProfile = Array.Empty<float>();
			_peakVisibleParticleCount = 0;
			_subParticleSystemCount = 0;
			_targetPivot = Vector3.zero;
			_targetOrbit = _orbit;
			_orbitAngularVelocity = Vector2.zero;
			_targetDistance = _distance;
			_needsMotion = false;
			_lightsEnabled = false;
			_authoredRootPosition = Vector3.zero;
			_authoredRootRotation = Quaternion.identity;
			_isOrbitDragging = false;
			_lastOrbitInputTime = -1d;
			_lastInteractionUpdateTime = -1d;

			if (_previewRoot != null)
			{
				UnityEngine.Object.DestroyImmediate(_previewRoot);
				_previewRoot = null;
			}

			if (_preview != null)
			{
				_preview.Cleanup();
				_preview = null;
			}

			_sunLight = null;
			_rimLight = null;
		}

		internal static void ClearSessionStateCache()
		{
			SessionStateByAssetPath.Clear();
			s_lastSetupAssetPath = null;
		}

		private void CacheCurrentSessionState()
		{
			if (string.IsNullOrEmpty(_prefabAssetPath))
				return;

			PreviewSessionStateCache.SaveAndTrim(
				SessionStateByAssetPath,
				_prefabAssetPath,
					new SessionStateSnapshot
					{
						PlaybackTime = Mathf.Clamp(_playbackTime, 0f, Mathf.Max(0.0001f, _maxPlaybackTime)),
						Playing = _playing,
						GridEnabled = _gridEnabled,
						LightsEnabled = _lightsEnabled,
						Orbit = _orbit,
						Distance = Mathf.Clamp(_distance, MinDistance, MaxDistance),
						TargetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxDistance),
						Pivot = _pivot,
						SavedAt = EditorApplication.timeSinceStartup,
					},
				MaxCachedSessionStates,
				static snapshot => snapshot.SavedAt);
		}

		private static bool TryRestoreSessionState(string assetPath, out SessionStateSnapshot snapshot)
		{
			return PreviewSessionStateCache.TryRestore(
				SessionStateByAssetPath,
				assetPath,
				EditorApplication.timeSinceStartup,
				SessionRestoreWindowSeconds,
				static restored => restored.SavedAt,
				out snapshot);
		}

		#endregion

		#region Playback API

		internal void SetPlaying(bool playing)
		{
			if (_playing == playing)
				return;

			_playing = playing;
			if (_playing)
			{
				_lastUpdateTime = -1d;
				_playbackAccumulatorSeconds = 0d;
			}
		}

		internal void Restart()
		{
			RestartInternal();
			_lastUpdateTime = -1d;
			_playbackAccumulatorSeconds = 0d;
		}

		internal void SetGridEnabled(bool enabled)
		{
			_gridEnabled = enabled;
		}

		internal void SetLightsEnabled(bool enabled)
		{
			_lightsEnabled = enabled;
		}

		internal void SetMotionShape(ParticlePreviewMotionShape shape)
		{
			if (_motionShape == shape)
				return;

			_motionShape = shape;
			ApplyMotionConfigurationChange();
		}

		internal void SetMotionSpeed(float speed)
		{
			float clamped = Mathf.Clamp(speed, ParticlePreviewSettings.MinMotionSpeed, ParticlePreviewSettings.MaxMotionSpeed);
			if (Mathf.Approximately(_motionSpeed, clamped))
				return;

			_motionSpeed = clamped;
			ApplyMotionConfigurationChange();
		}

		internal void SetMotionSize(float size)
		{
			float clamped = Mathf.Clamp(size, ParticlePreviewSettings.MinMotionRadius, ParticlePreviewSettings.MaxMotionRadius);
			if (Mathf.Approximately(_motionSize, clamped))
				return;

			_motionSize = clamped;
			ApplyMotionConfigurationChange();
		}

		internal void SetPlaybackTime(float time)
		{
			if (!IsReady)
				return;

			float clamped = Mathf.Clamp(time, 0f, Mathf.Max(0.0001f, _maxPlaybackTime));
			bool restorePlaying = _playing;
			_playing = false;

			RestartInternal();
			SimulateToTime(clamped);

			_playbackTime = clamped;
			_lastUpdateTime = -1d;
			_playbackAccumulatorSeconds = 0d;
			_playing = restorePlaying;
		}

		internal bool TickPlayback()
		{
			if (!IsReady || !_playing || _particleSystems.Count == 0)
				return false;

			double now = EditorApplication.timeSinceStartup;
			if (_lastUpdateTime < 0d)
			{
				_lastUpdateTime = now;
				_playbackAccumulatorSeconds = 0d;
				return false;
			}

			float dt = Mathf.Clamp((float) (now - _lastUpdateTime), 0f, MaxDeltaTime);
			_lastUpdateTime = now;
			if (dt <= 0f)
				return false;

			double frameInterval = 1.0 / Mathf.Max(1, ParticlePreviewSettings.RefreshFps);
			_playbackAccumulatorSeconds = Math.Min(_playbackAccumulatorSeconds + dt, frameInterval * 8.0);
			if (_playbackAccumulatorSeconds < frameInterval)
				return false;

			int stepCount = Math.Min(4, (int) (_playbackAccumulatorSeconds / frameInterval));
			if (stepCount <= 0)
				return false;

			for (int i = 0; i < stepCount; i++)
				SimulateStep((float) frameInterval);

			_playbackAccumulatorSeconds -= frameInterval * stepCount;

			if (!_hasLoopingSystem && _playbackTime >= _maxPlaybackTime)
				RestartInternal();

			return true;
		}

		private void ApplyMotionConfigurationChange()
		{
			if (!IsReady)
				return;

			bool wasPlaying = _playing;
			FrameCameraToContent();
			RebuildIntensityProfile();
			_playing = wasPlaying;
			_lastUpdateTime = -1d;
			_playbackAccumulatorSeconds = 0d;
		}

		#endregion

		#region Interaction

		internal bool HandleInput(Rect previewRect, Event evt)
		{
			if (!IsReady || evt == null)
				return false;

			// While another control owns hotControl (e.g. scrubber drag), skip viewport input.
			if (GUIUtility.hotControl != 0)
				return false;

			bool changed = false;
			double now = EditorApplication.timeSinceStartup;
			bool pointerInPreview = previewRect.Contains(evt.mousePosition);

			bool isPanDrag = evt.type == EventType.MouseDrag && (evt.button == 2 || (evt.button == 0 && evt.command));
			if (isPanDrag && pointerInPreview)
			{
				PanPreviewTarget(evt.delta, previewRect);
				evt.Use();
				return true;
			}

			switch (evt.type)
			{
				case EventType.MouseDown:
					if (evt.button == 0 && pointerInPreview)
					{
						_isOrbitDragging = true;
						_orbitAngularVelocity = Vector2.zero;
						_lastOrbitInputTime = now;
						evt.Use();
					}

					break;

				case EventType.MouseDrag:
					if (evt.button == 0 && _isOrbitDragging)
					{
						Vector2 delta = evt.delta * OrbitSensitivity;
						if (delta.sqrMagnitude > OrbitDragDeltaDeadzoneSqr)
						{
							float dt = FallbackOrbitInputDeltaTime;
							if (_lastOrbitInputTime >= 0d)
								dt = Mathf.Clamp((float) (now - _lastOrbitInputTime), 1f / 240f, MaxDeltaTime);
							_lastOrbitInputTime = now;

							_targetOrbit.x += delta.x;
							_targetOrbit.y = Mathf.Clamp(_targetOrbit.y + delta.y, PitchMin, PitchMax);

							Vector2 rawVelocity = new Vector2(
								delta.x / Mathf.Max(0.0001f, dt),
								delta.y / Mathf.Max(0.0001f, dt));
							_orbitAngularVelocity = Vector2.Lerp(_orbitAngularVelocity, rawVelocity, OrbitVelocitySmoothing);
							changed = true;
						}

						evt.Use();
					}

					break;

				case EventType.MouseUp:
					if (evt.button == 0 && _isOrbitDragging)
					{
						double idleSinceLastMove = _lastOrbitInputTime >= 0d
							? now - _lastOrbitInputTime
							: double.MaxValue;
						if (idleSinceLastMove > OrbitHoldStillResetSeconds)
							_orbitAngularVelocity = Vector2.zero;

						_isOrbitDragging = false;
						_lastOrbitInputTime = now;
						evt.Use();
					}

					break;

				case EventType.MouseLeaveWindow:
				case EventType.Ignore:
					if (_isOrbitDragging)
					{
						_isOrbitDragging = false;
						_lastOrbitInputTime = now;
					}

					break;

				case EventType.ScrollWheel:
					if (pointerInPreview)
					{
						float nextDistance = _targetDistance * (1f + evt.delta.y * ZoomFactorPerScrollUnit);
						_targetDistance = Mathf.Clamp(nextDistance, MinDistance, MaxDistance);
						evt.Use();
						changed = true;
					}

					break;
			}

			return changed;
		}

		internal bool TickInteraction()
		{
			if (!IsReady)
				return false;

			float orbitSmoothing = Mathf.Max(0.0001f, ParticlePreviewSettings.OrbitSmoothing);
			float panSmoothing = Mathf.Max(0.0001f, ParticlePreviewSettings.PanSmoothing);

			double now = EditorApplication.timeSinceStartup;
			var state = new PreviewCameraInteractionState
			{
				Orbit = _orbit,
				TargetOrbit = _targetOrbit,
				OrbitAngularVelocity = _orbitAngularVelocity,
				Distance = _distance,
				TargetDistance = _targetDistance,
				Pivot = _pivot,
				TargetPivot = _targetPivot,
				IsOrbitDragging = _isOrbitDragging,
				LastOrbitInputTime = _lastOrbitInputTime,
			};

			bool pending = PreviewCameraController.Tick(
				ref state,
				ref _lastInteractionUpdateTime,
				now,
				orbitSmoothing,
				panSmoothing,
				effective2D: false,
				CameraInteractionConfig);

			_orbit = state.Orbit;
			_targetOrbit = state.TargetOrbit;
			_orbitAngularVelocity = state.OrbitAngularVelocity;
			_distance = state.Distance;
			_targetDistance = state.TargetDistance;
			_pivot = state.Pivot;
			_targetPivot = state.TargetPivot;
			_isOrbitDragging = state.IsOrbitDragging;
			_lastOrbitInputTime = state.LastOrbitInputTime;

			return pending;
		}

		#endregion

		#region Rendering

		internal void Draw(Rect previewRect, GUIStyle background)
		{
			if (!IsReady || Event.current.type != EventType.Repaint)
				return;

			_preview.camera.backgroundColor = ParticlePreviewSettings.BackgroundColor;
			UpdateCameraTransform();
			ApplyEnvironmentState();

				_preview.BeginPreview(previewRect, background ?? GUIStyle.none);
				DrawGrid();

			using (ParticleRenderCompatibilityUtility.PushShaderTime(_playbackTime))
			using (ParticleRenderCompatibilityUtility.EnableRenderersScoped(_renderers))
			{
				ParticleRenderCompatibilityUtility.RenderPreviewWithCameraPath(_preview);
			}

			Texture previewTexture = _preview.EndPreview();
			if (previewTexture != null)
				EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.StretchToFill);
		}

		private void UpdateCameraTransform()
		{
			Camera camera = _preview.camera;
			Quaternion rotation = Quaternion.Euler(_orbit.y, _orbit.x, 0f);
			Vector3 forward = rotation * Vector3.forward;
			Vector3 cameraPosition = _pivot - forward * _distance;

			camera.transform.position = cameraPosition;
			camera.transform.rotation = rotation;
			camera.nearClipPlane = Mathf.Max(0.01f, _distance * 0.01f);
			camera.farClipPlane = Mathf.Max(100f, _distance * 30f);
		}

		private void ApplyEnvironmentState()
		{
			PreviewLightingSystem.EnsureSunLight(_preview, ref _sunLight);
			PreviewLightingSystem.EnsureRimLight(_preview, ref _rimLight);
			SharedPreviewLightingProfile lightingProfile = PreviewLightingSystem.CreateProfileFromSettings();
			PreviewLightingSystem.ApplyLighting(
				_preview,
				_sunLight,
				_rimLight,
				in lightingProfile,
				_lightsEnabled,
				PreviewLightingSystem.RotationFromYawPitch(ParticlePreviewSettings.ModelSunLightRotation),
				PreviewLightingSystem.RotationFromYawPitch(ParticlePreviewSettings.ModelKeyLightRotation),
				PreviewLightingSystem.RotationFromYawPitch(ParticlePreviewSettings.ModelFillLightRotation),
				PreviewLightingSystem.RotationFromYawPitch(ParticlePreviewSettings.ModelRimLightRotation));
		}

		#endregion

		#region Playback Simulation Internals

		private void RestartInternal()
		{
			_playbackTime = 0f;
			ApplyRootPoseAtTime(0f);

			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps == null)
					continue;

				ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
				ps.Clear(true);
				ps.Simulate(0f, withChildren: false, restart: true, fixedTimeStep: true);
			}
		}

		private void SimulateToTime(float targetTime)
		{
			if (targetTime <= 0f)
				return;

			const float seekStep = 1f / 60f;
			float remaining = targetTime;
			int guard = 0;
			while (remaining > 0f && guard++ < 10000)
			{
				float dt = Mathf.Min(seekStep, remaining);
				SimulateStep(dt);
				remaining -= dt;
			}
		}

		private void SimulateStep(float dt)
		{
			if (dt <= 0f)
				return;

			float nextTime = _playbackTime + dt;
			ApplyRootPoseAtTime(nextTime);

			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps == null)
					continue;

				ps.Simulate(dt, withChildren: false, restart: false, fixedTimeStep: true);
			}

			_playbackTime = nextTime;
		}

		private void EnsureDeterministicSeeds()
		{
			uint seed = 101u;
			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps == null)
					continue;

				ps.useAutoRandomSeed = false;
				ps.randomSeed = seed++;
			}
		}

		private void SanitizeCustomSimulationSpaces()
		{
			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps == null)
					continue;

				ParticleSystem.MainModule main = ps.main;
				if (main.simulationSpace != ParticleSystemSimulationSpace.Custom)
					continue;

				Transform customSpace = main.customSimulationSpace;
				bool isValid = customSpace != null
				               && _previewRoot != null
				               && (customSpace == _previewRoot.transform || customSpace.IsChildOf(_previewRoot.transform));
				if (isValid)
					continue;

				main.simulationSpace = ParticleSystemSimulationSpace.Local;
				main.customSimulationSpace = null;
			}
		}

		private void ComputePlaybackRangeAndLoopingMode()
		{
			_hasLoopingSystem = false;
			float maxDuration = 0.1f;
			int totalSystems = 0;

			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps == null)
					continue;
				totalSystems++;

				ParticleSystem.MainModule main = ps.main;
				if (main.loop)
					_hasLoopingSystem = true;

				float systemEnd = Mathf.Max(0.1f, main.duration + GetMaxStartLifetime(main.startLifetime));
				maxDuration = Mathf.Max(maxDuration, systemEnd);
			}

			_maxPlaybackTime = Mathf.Max(0.1f, maxDuration);
			_subParticleSystemCount = Mathf.Max(0, totalSystems - 1);
			if (_playbackTime > _maxPlaybackTime)
				_playbackTime = 0f;
		}

		private void RebuildIntensityProfile()
		{
			if (_particleSystems.Count == 0 || _maxPlaybackTime <= 0.0001f)
			{
				_intensityProfile = Array.Empty<float>();
				return;
			}

			bool wasPlaying = _playing;
			float restoreTime = Mathf.Clamp(_playbackTime, 0f, _maxPlaybackTime);

			float[] aliveCounts = new float[IntensityProfileSampleCount];
			float duration = Mathf.Max(0.0001f, _maxPlaybackTime);
			float maxAliveCount = 0f;
			int peakAlive = 0;

			RestartInternal();
			float previousSampleTime = 0f;
			for (int i = 0; i < IntensityProfileSampleCount; i++)
			{
				float sampleTime = duration * i / (IntensityProfileSampleCount - 1);
				float dt = sampleTime - previousSampleTime;
				if (dt > 0f)
					SimulateStep(dt);

				previousSampleTime = sampleTime;
				float alive = GetAliveParticleCount();
				aliveCounts[i] = alive;
				maxAliveCount = Mathf.Max(maxAliveCount, alive);
				peakAlive = Mathf.Max(peakAlive, Mathf.RoundToInt(alive));
			}

			if (maxAliveCount > 0.0001f)
			{
				for (int i = 0; i < aliveCounts.Length; i++)
					aliveCounts[i] /= maxAliveCount;
			}
			else
			{
				for (int i = 0; i < aliveCounts.Length; i++)
					aliveCounts[i] = 0f;
			}

			_intensityProfile = aliveCounts;
			_peakVisibleParticleCount = peakAlive;
			RestartInternal();

			if (restoreTime > 0.0001f)
				SimulateToTime(restoreTime);

			_playbackTime = restoreTime;
			_playing = wasPlaying;
			_lastUpdateTime = -1d;
		}

		private float GetAliveParticleCount()
		{
			float total = 0f;
			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps != null)
					total += ps.particleCount;
			}

			return total;
		}

		private static float GetMaxStartLifetime(ParticleSystem.MinMaxCurve curve)
		{
			switch (curve.mode)
			{
				case ParticleSystemCurveMode.Constant:
					return Mathf.Max(0f, curve.constant);

				case ParticleSystemCurveMode.TwoConstants:
					return Mathf.Max(0f, Mathf.Max(curve.constantMin, curve.constantMax));

				case ParticleSystemCurveMode.Curve:
					return Mathf.Max(0f, GetCurveMaxValue(curve.curve) * curve.curveMultiplier);

				case ParticleSystemCurveMode.TwoCurves:
					float maxA = GetCurveMaxValue(curve.curveMin) * curve.curveMultiplier;
					float maxB = GetCurveMaxValue(curve.curveMax) * curve.curveMultiplier;
					return Mathf.Max(0f, Mathf.Max(maxA, maxB));

				default:
					return Mathf.Max(0f, curve.constantMax);
			}
		}

		private static float GetCurveMaxValue(AnimationCurve curve)
		{
			if (curve == null || curve.length == 0)
				return 0f;

			float max = float.MinValue;
			Keyframe[] keys = curve.keys;
			for (int i = 0; i < keys.Length; i++)
				max = Mathf.Max(max, keys[i].value);

			return max;
		}

		#endregion

		#region Camera Framing and Pan

		private void FrameCameraToContent()
		{
			// Build framing bounds from a short deterministic simulation scan.
			if (!TryBuildFramingBoundsFromScan(out Bounds framingBounds))
				framingBounds = new Bounds(Vector3.zero, Vector3.one * 2f);

			Vector3 framingCenter = _needsMotion ? _authoredRootPosition : framingBounds.center;
			_pivot = framingCenter;
			_targetPivot = framingCenter;

			if (_needsMotion)
			{
				_targetDistance = Mathf.Clamp(
					ComputeMotionFramingDistance(
						_preview.camera.fieldOfView,
						_motionSize,
						ParticlePreviewSettings.MotionPadding),
					MinDistance,
					MaxAutoFrameDistance) * MotionFitDistanceScale;
			}
			else
			{
				Quaternion framingRotation = Quaternion.Euler(_targetOrbit.y, _targetOrbit.x, 0f);
				_targetDistance = Mathf.Clamp(
					ComputeContainmentDistance(
						_preview.camera.fieldOfView,
						framingBounds,
						framingRotation,
						FramingBoundsPadding),
					MinDistance,
					MaxAutoFrameDistance) * NonMotionFitDistanceScale;
			}

			_targetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxAutoFrameDistance);

			float introDistance = Mathf.Max(_targetDistance * IntroZoomMultiplier, _targetDistance + IntroZoomMinimumExtraDistance);
			_distance = Mathf.Clamp(introDistance, MinDistance, MaxDistance);
			_orbitAngularVelocity = Vector2.zero;
			_isOrbitDragging = false;
			_lastOrbitInputTime = -1d;
			RestartInternal();
		}

		private bool TryBuildFramingBoundsFromScan(out Bounds scanBounds)
		{
			scanBounds = default;
			bool hasBounds = false;

			float scanEnd = Mathf.Min(FramingScanMaxSeconds, Mathf.Max(0.0001f, _maxPlaybackTime));
			RestartInternal();

			if (TryComputeVisualBounds(out Bounds initialBounds))
			{
				scanBounds = initialBounds;
				hasBounds = true;
			}

			float elapsed = 0f;
			int guard = 0;
			while (elapsed < scanEnd - 0.0001f && guard++ < 10000)
			{
				float dt = Mathf.Min(FramingScanStep, scanEnd - elapsed);
				SimulateStep(dt);
				elapsed += dt;

				if (!TryComputeVisualBounds(out Bounds frameBounds))
					continue;

				if (!hasBounds)
				{
					scanBounds = frameBounds;
					hasBounds = true;
				}
				else
				{
					scanBounds.Encapsulate(frameBounds);
				}
			}

			if (!hasBounds)
				return false;

			scanBounds = EnsureMinimumBoundsExtent(scanBounds);
			return true;
		}

		private bool TryComputeVisualBounds(out Bounds bounds)
		{
			bounds = default;
			bool hasParticles = false;

			for (int i = 0; i < _particleSystems.Count; i++)
			{
				ParticleSystem ps = _particleSystems[i];
				if (ps == null)
					continue;

				int count = ps.GetParticles(ParticleBuffer);
				for (int j = 0; j < count; j++)
				{
					Vector3 position = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
						? ParticleBuffer[j].position
						: ps.transform.TransformPoint(ParticleBuffer[j].position);

					Vector3 size3 = ParticleBuffer[j].GetCurrentSize3D(ps);
					float diameter = Mathf.Max(size3.x, size3.y, size3.z, 0.01f);
					Bounds particleBounds = new Bounds(position, Vector3.one * diameter);

					if (!hasParticles)
					{
						bounds = particleBounds;
						hasParticles = true;
					}
					else
					{
						bounds.Encapsulate(particleBounds);
					}
				}
			}

			if (!hasParticles)
				return false;

			bounds = EnsureMinimumBoundsExtent(bounds);
			return true;
		}

		private static Bounds EnsureMinimumBoundsExtent(Bounds bounds)
		{
			Vector3 minSize = new Vector3(MinAxisExtent, MinAxisExtent, MinAxisExtent);
			bounds.size = Vector3.Max(bounds.size, minSize);
			return bounds;
		}

		private void ApplyRootPoseAtTime(float time)
		{
			if (_previewRoot == null)
				return;

			if (_needsMotion)
			{
				const float lookAheadTime = 0.001f;
				Vector3 offset = MotionPosition(time);
				Vector3 nextOffset = MotionPosition(time + lookAheadTime);
				Vector3 direction = nextOffset - offset;

				_previewRoot.transform.position = _authoredRootPosition + offset;
				if (direction.sqrMagnitude > 0.000001f)
				{
					_previewRoot.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * _authoredRootRotation;
				}
				else
				{
					_previewRoot.transform.rotation = _authoredRootRotation;
				}
				return;
			}

			_previewRoot.transform.position = _authoredRootPosition;
			_previewRoot.transform.rotation = _authoredRootRotation;
		}

		private Vector3 MotionPosition(float time)
		{
			switch (_motionShape)
			{
				case ParticlePreviewMotionShape.Line:
				{
					float period = (_motionSize * 2f) / Mathf.Max(0.001f, _motionSpeed);
					float x = Mathf.Lerp(-_motionSize, _motionSize, Mathf.PingPong(time / period, 1f));
					return new Vector3(x, 0f, 0f);
				}
				case ParticlePreviewMotionShape.Figure8:
				{
					float safeSize = Mathf.Max(_motionSize, 0.0001f);
					float angle = time * (_motionSpeed / safeSize);
					float denom = 1f + Mathf.Sin(angle) * Mathf.Sin(angle);
					return new Vector3(
						_motionSize * Mathf.Cos(angle) / Mathf.Max(0.001f, denom),
						0f,
						_motionSize * Mathf.Sin(angle) * Mathf.Cos(angle) / Mathf.Max(0.001f, denom));
				}
				default:
					return ComputeCircularMotionOffset(time, _motionSize, _motionSpeed);
			}
		}

		private static Vector3 ComputeCircularMotionOffset(float time, float radius, float speed)
		{
			float safeRadius = Mathf.Max(radius, 0.0001f);
			float angle = time * (speed / safeRadius);
			return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
		}

		private static float ComputeMotionFramingDistance(float cameraFov, float motionRadius, float motionPadding)
		{
			float halfFov = cameraFov * 0.5f * Mathf.Deg2Rad;
			float tanHalf = Mathf.Max(Mathf.Tan(halfFov), 0.0001f);
			float motionExtent = motionRadius * (MotionDistanceBaseMultiplier + Mathf.Max(0f, motionPadding));
			return Mathf.Max(motionExtent / tanHalf, MinContainmentDistance);
		}

		private static float ComputeContainmentDistance(float cameraFov, Bounds bounds, Quaternion cameraRotation, float boundsPadding)
		{
			Vector3 right = cameraRotation * Vector3.right;
			Vector3 up = cameraRotation * Vector3.up;

			Vector3 extents = bounds.extents * (1f + Mathf.Max(0f, boundsPadding));
			float halfFov = cameraFov * 0.5f * Mathf.Deg2Rad;
			float tanHalf = Mathf.Max(Mathf.Tan(halfFov), 0.0001f);
			float maxRight = 0f;
			float maxUp = 0f;

			for (int sx = -1; sx <= 1; sx += 2)
			for (int sy = -1; sy <= 1; sy += 2)
			for (int sz = -1; sz <= 1; sz += 2)
			{
				Vector3 corner = new Vector3(extents.x * sx, extents.y * sy, extents.z * sz);
				maxRight = Mathf.Max(maxRight, Mathf.Abs(Vector3.Dot(corner, right)));
				maxUp = Mathf.Max(maxUp, Mathf.Abs(Vector3.Dot(corner, up)));
			}

			return Mathf.Max(maxRight / tanHalf, maxUp / tanHalf, MinContainmentDistance);
		}

		internal static float ComputeMotionFramingDistanceForTests(float cameraFov, float motionRadius, float motionPadding)
		{
			return ComputeMotionFramingDistance(cameraFov, motionRadius, motionPadding);
		}

		internal static float ComputeContainmentDistanceForTests(float cameraFov, Bounds bounds, Quaternion cameraRotation, float boundsPadding)
		{
			return ComputeContainmentDistance(cameraFov, bounds, cameraRotation, boundsPadding);
		}

		internal static Vector3 ComputeCircularMotionOffsetForTests(float time, float radius, float speed)
		{
			return ComputeCircularMotionOffset(time, radius, speed);
		}

		private void PanPreviewTarget(Vector2 delta, Rect previewRect)
		{
			if (_preview == null || _preview.camera == null)
				return;

			float width = Mathf.Max(1f, previewRect.width);
			float height = Mathf.Max(1f, previewRect.height);
			float fovRadians = _preview.camera.fieldOfView * Mathf.Deg2Rad;
			float verticalWorldSize = 2f * Mathf.Tan(fovRadians * 0.5f) * Mathf.Max(_targetDistance, 0.01f);
			float horizontalWorldSize = verticalWorldSize * (width / height);

			float panRight = -delta.x / width * horizontalWorldSize;
			float panUp = delta.y / height * verticalWorldSize;
			Vector3 worldPan = _preview.camera.transform.right * panRight + _preview.camera.transform.up * panUp;
			_targetPivot += worldPan;
		}

		private bool ComputeHasPendingCameraMotion()
		{
			var state = new PreviewCameraInteractionState
			{
				Orbit = _orbit,
				TargetOrbit = _targetOrbit,
				OrbitAngularVelocity = _orbitAngularVelocity,
				Distance = _distance,
				TargetDistance = _targetDistance,
				Pivot = _pivot,
				TargetPivot = _targetPivot,
				IsOrbitDragging = _isOrbitDragging,
				LastOrbitInputTime = _lastOrbitInputTime,
			};

			return PreviewCameraController.HasPendingMotion(state, CameraInteractionConfig);
		}

		#endregion

		#region Grid

		private void DrawGrid()
		{
			var request = new PreviewGridDrawRequest(
				_preview,
				PreviewGridSpace.Plane3D,
				_gridEnabled,
				gridTransformOverride: Matrix4x4.identity);
			PreviewGridSystem.Draw(request);
		}

		#endregion
	}
}
