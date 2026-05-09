using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Manages the full model preview scene lifecycle, including camera controls, lighting, overlays, visual modes, and render-state restoration.

namespace ParticleThumbnailAndPreview.Editor
{
	internal enum ModelPreviewVisualMode
	{
		None,
		Normals,
		UvChecker,
		VertexColor,
		Overdraw,
		Matcap,
	}

	internal sealed class ModelPrefabPreviewSession : IPreviewToolbarCommonSession
	{
		private const double SessionRestoreWindowSeconds = 2.0d;
		private const int MaxCachedSessionStates = 64;
		private const float OrbitSensitivity = 1.2f;
		private const float ZoomFactorPerScrollUnit = 0.1f;
		private const float PitchMin = -85f;
		private const float PitchMax = 85f;
		private const float MaxDeltaTime = 0.05f;
		private const float OrbitEpsilon = 0.01f;
		private const float DistanceEpsilon = 0.001f;
		private const float PivotEpsilon = 0.0001f;
		private const float AngularVelocityEpsilon = 0.01f;
		private const float OrbitVelocitySmoothing = 0.35f;
		private const float FallbackOrbitInputDeltaTime = 1f / 60f;
		private const float OrbitHoldStillResetSeconds = 0.08f;
		private const float OrbitDragDeltaDeadzoneSqr = 0.0001f;
		private const float MinDistance = 0.1f;
		private const float MaxDistance = 500f;
		private const float ZoomSmooth = 8f;
		private const float MinAnimationClipLengthSeconds = 0.0001f;
		private const float DefaultAnimationPlaybackSpeed = 1f;

		private const float AxisSize = 0.65f;
		private const float PivotMarkerRadius = 0.07f;
		private const int PivotMarkerSegments = 24;
		private const float TurntableDegreesPerSecond = 24f;
		private const float GridLodLevelSmoothing = 10f;
		private const float GridLodMaxLevelsPerSecond = 1.25f;
		private static readonly float[] GridLodDistances = {10f, 42f, 110f, 260f};
		private static readonly float[] GridLodStepMultipliers = {1f, 2f, 4f, 8f};
		private static readonly float[] GridLodAlphaMultipliers = {1f, 0.74f, 0.5f, 0.28f};
		private static readonly int LightWidgetControlHash = "ModelPreviewLightWidget".GetHashCode();
		private const float LightPadPanelSize = 89.6f;
		private const float LightPadPanelPadding = 8f;
		private const float LightPadInnerPadding = 7.2f;
		private const float LightPadMarkerSize = 6.4f;
		private const string NormalsMaterialPath = "PrefabPreviewNormals.mat";
		private const string UvCheckerMaterialPath = "PrefabPreviewUvChecker.mat";
		private const string VertexColorMaterialPath = "PrefabPreviewVertexColor.mat";
		private const string OverdrawMaterialPath = "PrefabPreviewOverdraw.mat";

		private static Mesh s_pivotCrossMesh;
		private static Mesh s_axesMesh;
		private static Material s_solidLineMaterial;
		private static Material s_normalsMaterial;
		private static Material s_uvCheckerMaterial;
		private static Material s_vertexColorMaterial;
		private static Material s_overdrawMaterial;
		private static readonly List<Material[]> SharedMaterialRestoreCache = new();
		private static readonly Dictionary<string, SessionStateSnapshot> SessionStateByAssetPath = new();
		private static string s_lastSetupAssetPath;
		private static bool s_overlayResourceCleanupRegistered;
		private static readonly Type TmpTextMeshProType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
		private static readonly Type TmpTextMeshProUiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");

		private static readonly PreviewCameraInteractionConfig CameraInteractionConfig = new(
			PitchMin,
			PitchMax,
			MaxDeltaTime,
			OrbitEpsilon,
			DistanceEpsilon,
			PivotEpsilon,
			AngularVelocityEpsilon,
			OrbitHoldStillResetSeconds,
			ZoomSmooth);

		private readonly List<Renderer> _renderers = new();
		private readonly List<bool> _rendererInitialStates = new();
		private readonly List<Collider> _colliders = new();
		private readonly List<Collider2D> _colliders2D = new();

		private PreviewRenderUtility _preview;
		private GameObject _previewRoot;
		private int _prefabInstanceId;
		private string _prefabAssetPath;
		private Skybox _cameraSkybox;
		private Light _sunLight;
		private Light _rimLight;

		private Vector3 _pivot;
		private Vector3 _targetPivot;
		private Vector2 _orbit = new Vector2(-20f, 18f);
		private Vector2 _targetOrbit = new Vector2(-20f, 18f);
		private Vector2 _orbitAngularVelocity;
		private float _distance = 8f;
		private float _targetDistance = 8f;
		private bool _isOrbitDragging;
		private double _lastOrbitInputTime = -1d;
		private double _lastInteractionUpdateTime = -1d;

		private bool _gridEnabled = true;
		private bool _lightsEnabled;
		private bool _lightWidgetEnabled;
		private bool _skyboxEnabled;
		private bool _turntableEnabled = true;
		private bool _boundsOverlayEnabled = PreviewSettings.SharedBoundsRulerDefaultEnabled;
		private bool _colliderOverlayEnabled;
		private PreviewModeOverride _modeOverride = PreviewModeOverride.Force3D;
		private ModelPreviewVisualMode _visualMode = ModelPreviewVisualMode.None;
		private ModelPreviewVisualMode _lastNonNoneVisualMode = ModelPreviewVisualMode.Normals;
		private Bounds _framedBounds;
		private bool _hasFramedBounds;
		private int _triangleCount;
		private int _materialSlotCount;
		private ModelPreviewVisualMode _loggedVisualModeFailure = ModelPreviewVisualMode.None;
		private string _lastGridDiagnosticsKey;
		private Vector3 _lightRigDirectionWorld;
		private AnimationClip _previewAnimationClip;
		private bool _previewAnimationPlaying;
		private float _previewAnimationTime;
		private float _previewAnimationSpeed = DefaultAnimationPlaybackSpeed;
		private double _lastPreviewAnimationSampleTime = -1d;
		private string _lastAnimationSampleErrorKey;
		private float _smoothedGridLodLevel;
		private bool _hasSmoothedGridLodLevel;
		private double _lastGridLodLevelSampleTime = -1d;

		private struct SessionStateSnapshot
		{
			internal Vector3 Pivot;
			internal Vector3 TargetPivot;
			internal Vector2 Orbit;
			internal Vector2 TargetOrbit;
			internal float Distance;
			internal float TargetDistance;
			internal bool GridEnabled;
			internal bool LightsEnabled;
			internal bool LightWidgetEnabled;
			internal bool SkyboxEnabled;
			internal bool TurntableEnabled;
			internal bool BoundsOverlayEnabled;
			internal bool ColliderOverlayEnabled;
			internal PreviewModeOverride ModeOverride;
			internal ModelPreviewVisualMode VisualMode;
			internal ModelPreviewVisualMode LastNonNoneVisualMode;
			internal double SavedAt;
		}

		private readonly struct GridLodBlend
		{
			internal readonly PreviewGridProfile NearProfile;
			internal readonly float NearOpacityMultiplier;
			internal readonly bool HasFarProfile;
			internal readonly PreviewGridProfile FarProfile;
			internal readonly float FarOpacityMultiplier;
			internal readonly PreviewGridProfile AxisProfile;
			internal readonly float AxisOpacityMultiplier;

			internal GridLodBlend(
				PreviewGridProfile nearProfile,
				float nearOpacityMultiplier,
				bool hasFarProfile,
				PreviewGridProfile farProfile,
				float farOpacityMultiplier,
				PreviewGridProfile axisProfile,
				float axisOpacityMultiplier)
			{
				NearProfile = nearProfile;
				NearOpacityMultiplier = nearOpacityMultiplier;
				HasFarProfile = hasFarProfile;
				FarProfile = farProfile;
				FarOpacityMultiplier = farOpacityMultiplier;
				AxisProfile = axisProfile;
				AxisOpacityMultiplier = axisOpacityMultiplier;
			}
		}

		internal bool IsReady => _preview != null && _previewRoot != null;
		internal bool GridEnabled => _gridEnabled;
		internal bool LightsEnabled => _lightsEnabled;
		internal bool LightWidgetEnabled => _lightWidgetEnabled;
		internal bool LightingControlsSupported => ComputeLightingControlsSupportedForTests(ModeContext.IsUrp2DRenderer);
		internal bool SkyboxEnabled => _skyboxEnabled;
		internal bool SkyboxSupported => ComputeSkyboxSupportedForTests(ModeContext.IsUrp2DRenderer);
		internal bool TurntableEnabled => _turntableEnabled;
		internal bool BoundsOverlayEnabled => _boundsOverlayEnabled;
		internal bool ColliderOverlayEnabled => _colliderOverlayEnabled;
		internal PreviewModeOverride ModeOverride => _modeOverride;
		internal ModelPreviewVisualMode VisualMode => _visualMode;
		internal ModelPreviewVisualMode LastNonNoneVisualMode => _lastNonNoneVisualMode;
		internal PreviewModeContext ModeContext => PreviewModeResolver.Resolve(_modeOverride);
		internal bool HasPendingCameraMotion => ComputeHasPendingCameraMotion();
		internal bool HasPendingAnimationPlayback => _previewAnimationClip != null && _previewAnimationPlaying;
		internal int RendererCount => _renderers.Count;
		internal int TriangleCount => _triangleCount;
		internal int MaterialSlotCount => _materialSlotCount;
		internal Vector3 BoundsSize => _hasFramedBounds ? _framedBounds.size : Vector3.zero;
		internal string ModeLabel => ModeContext.CameraIs2D ? "2D" : "3D";

		bool IPreviewToolbarCommonSession.GridEnabled
		{
			get => GridEnabled;
			set => SetGridEnabled(value);
		}

		bool IPreviewToolbarCommonSession.BoundsOverlayEnabled
		{
			get => BoundsOverlayEnabled;
			set => SetBoundsOverlayEnabled(value);
		}

		bool IPreviewToolbarCommonSession.ColliderOverlayEnabled
		{
			get => ColliderOverlayEnabled;
			set => SetColliderOverlayEnabled(value);
		}

		PreviewModeOverride IPreviewToolbarCommonSession.ModeOverride => ModeOverride;
		PreviewModeContext IPreviewToolbarCommonSession.ModeContext => ModeContext;

		void IPreviewToolbarCommonSession.CycleModeOverride()
		{
			CycleModeOverride();
		}

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

			if (IsReady && !string.IsNullOrEmpty(_prefabAssetPath) && string.Equals(_prefabAssetPath, assetPath, StringComparison.Ordinal))
			{
				_prefabInstanceId = instanceId;
				return;
			}

			PreviewDiagnostics.Log(
				"ModelSession",
				$"Setup begin id={instanceId} asset='{assetPath}' ready={IsReady} transientSame={isTransientRebuildOfSameSelection} switching={isSwitchingToDifferentPrefab}");

			Cleanup(cacheState: !isSwitchingToDifferentPrefab);

			_preview = new PreviewRenderUtility(true);
			_preview.camera.clearFlags = CameraClearFlags.SolidColor;
			_preview.camera.backgroundColor = PreviewSettings.BackgroundColor;
			_preview.camera.fieldOfView = 30f;
			_preview.camera.nearClipPlane = 0.01f;
			_preview.camera.farClipPlane = 1000f;
			_preview.camera.orthographic = false;
			_cameraSkybox = _preview.camera.GetComponent<Skybox>();
			if (_cameraSkybox == null)
				_cameraSkybox = _preview.camera.gameObject.AddComponent<Skybox>();
			_cameraSkybox.enabled = false;

			BuildPreviewRootFromPrefab(prefab);

			_modeOverride = PreviewModeOverride.Force3D;
			_gridEnabled = PreviewSettings.SharedGridDefaultEnabled;
			_lightsEnabled = true;
			_lightWidgetEnabled = PreviewSettings.ModelDefaultLightRotationGizmosEnabled;
			_skyboxEnabled = PreviewSettings.ModelDefaultSkyboxEnabled;
			_turntableEnabled = PreviewSettings.ModelDefaultTurntableEnabled;
			_boundsOverlayEnabled = PreviewSettings.SharedBoundsRulerDefaultEnabled;
			_colliderOverlayEnabled = false;
			_visualMode = ModelPreviewVisualMode.None;
			_lastNonNoneVisualMode = ModelPreviewVisualMode.Normals;
			_lastGridDiagnosticsKey = null;
			ResetLightRigDirection();
			FrameCameraToContent(reason: "initial");

			bool shouldRestoreState = !isSwitchingToDifferentPrefab && isTransientRebuildOfSameSelection;
			if (shouldRestoreState && TryRestoreSessionState(assetPath, out SessionStateSnapshot restored))
			{
				_pivot = restored.Pivot;
				_targetPivot = restored.TargetPivot;
				_orbit = restored.Orbit;
				_targetOrbit = restored.TargetOrbit;
				_distance = Mathf.Clamp(restored.Distance, MinDistance, MaxDistance);
				_targetDistance = Mathf.Clamp(restored.TargetDistance, MinDistance, MaxDistance);
				_gridEnabled = restored.GridEnabled;
				_lightsEnabled = restored.LightsEnabled;
				_lightWidgetEnabled = restored.LightWidgetEnabled;
				_skyboxEnabled = restored.SkyboxEnabled;
				_turntableEnabled = restored.TurntableEnabled;
				_boundsOverlayEnabled = restored.BoundsOverlayEnabled;
				_colliderOverlayEnabled = restored.ColliderOverlayEnabled;
				_modeOverride = NormalizeModeOverride(restored.ModeOverride);
				_visualMode = restored.VisualMode;
				_lastNonNoneVisualMode = restored.LastNonNoneVisualMode == ModelPreviewVisualMode.None
					? ModelPreviewVisualMode.Normals
					: restored.LastNonNoneVisualMode;
				_orbitAngularVelocity = Vector2.zero;
				_isOrbitDragging = false;
				_lastOrbitInputTime = -1d;
				_lastGridDiagnosticsKey = null;
				PreviewDiagnostics.Log("ModelSession", $"Restored cached state asset='{assetPath}'");
			}

			_lightsEnabled = LightingControlsSupported && _lightsEnabled;
			_lightWidgetEnabled = LightingControlsSupported && _lightWidgetEnabled;
			_skyboxEnabled = SkyboxSupported && _skyboxEnabled;

			if (ModeContext.CameraIs2D)
				_turntableEnabled = false;

			_prefabInstanceId = instanceId;
			_prefabAssetPath = assetPath;
			s_lastSetupAssetPath = assetPath;
			_lastInteractionUpdateTime = -1d;
			_previewAnimationClip = null;
			_previewAnimationPlaying = false;
			_previewAnimationTime = 0f;
			_previewAnimationSpeed = DefaultAnimationPlaybackSpeed;
			_lastPreviewAnimationSampleTime = -1d;
			_lastAnimationSampleErrorKey = null;
			EnsureOverlayResources();
			PreviewLightingSystem.EnsureSunLight(_preview, ref _sunLight);
			PreviewLightingSystem.EnsureRimLight(_preview, ref _rimLight);
			PreviewDiagnostics.Log("ModelSession", $"Setup complete id={instanceId} asset='{assetPath}'");
		}

		internal void Cleanup(bool cacheState)
		{
			if (cacheState)
				CacheCurrentSessionState();

			_renderers.Clear();
			_rendererInitialStates.Clear();
			_colliders.Clear();
			_colliders2D.Clear();
			_prefabInstanceId = 0;
			_prefabAssetPath = null;
			_lastInteractionUpdateTime = -1d;
			_isOrbitDragging = false;
			_lastOrbitInputTime = -1d;
			_lastGridDiagnosticsKey = null;
			_hasFramedBounds = false;
			_cameraSkybox = null;
			_sunLight = null;
			_rimLight = null;
			_lightRigDirectionWorld = Vector3.zero;
			_loggedVisualModeFailure = ModelPreviewVisualMode.None;
			_previewAnimationClip = null;
			_previewAnimationPlaying = false;
			_previewAnimationTime = 0f;
			_previewAnimationSpeed = DefaultAnimationPlaybackSpeed;
			_lastPreviewAnimationSampleTime = -1d;
			_lastAnimationSampleErrorKey = null;

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

			PreviewDiagnostics.Log("ModelSession", $"Cleanup cacheState={cacheState}");
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
					Pivot = _pivot,
					TargetPivot = _targetPivot,
					Orbit = _orbit,
					TargetOrbit = _targetOrbit,
					Distance = Mathf.Clamp(_distance, MinDistance, MaxDistance),
					TargetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxDistance),
					GridEnabled = _gridEnabled,
					LightsEnabled = _lightsEnabled,
					LightWidgetEnabled = _lightWidgetEnabled,
					SkyboxEnabled = _skyboxEnabled,
					TurntableEnabled = _turntableEnabled,
					BoundsOverlayEnabled = _boundsOverlayEnabled,
					ColliderOverlayEnabled = _colliderOverlayEnabled,
					ModeOverride = NormalizeModeOverride(_modeOverride),
					VisualMode = _visualMode,
					LastNonNoneVisualMode = _lastNonNoneVisualMode,
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

		internal void SetGridEnabled(bool enabled)
		{
			_gridEnabled = enabled;
			_lastGridDiagnosticsKey = null;
			LogGridDiagnosticsState("toggle");
		}

		internal void SetLightsEnabled(bool enabled)
		{
			_lightsEnabled = LightingControlsSupported && enabled;
		}

		internal void SetLightWidgetEnabled(bool enabled)
		{
			_lightWidgetEnabled = LightingControlsSupported && enabled;
		}

		internal void SetSkyboxEnabled(bool enabled)
		{
			_skyboxEnabled = SkyboxSupported && enabled;
		}

		internal void SetBoundsOverlayEnabled(bool enabled)
		{
			_boundsOverlayEnabled = enabled;
		}

		internal void SetTurntableEnabled(bool enabled)
		{
			_turntableEnabled = enabled;
			if (enabled)
				_orbitAngularVelocity = Vector2.zero;
		}

		internal void SetColliderOverlayEnabled(bool enabled)
		{
			_colliderOverlayEnabled = enabled;
		}

		internal void SetPreviewAnimationClip(AnimationClip clip)
		{
			if (ReferenceEquals(_previewAnimationClip, clip))
				return;

			bool hadClip = _previewAnimationClip != null;
			_previewAnimationClip = clip;
			_previewAnimationPlaying = clip != null;
			_previewAnimationTime = 0f;
			_previewAnimationSpeed = DefaultAnimationPlaybackSpeed;
			_lastPreviewAnimationSampleTime = -1d;
			_lastAnimationSampleErrorKey = null;

			string clipName = clip != null ? clip.name : "<none>";
			float clipLength = clip != null ? clip.length : 0f;
			int curveBindings = clip != null ? AnimationUtility.GetCurveBindings(clip).Length : 0;
			PreviewDiagnostics.Log(
				"ModelAnim",
				$"SetPreviewAnimationClip clip='{clipName}' length={clipLength:F3}s curves={curveBindings} hadClip={hadClip}");

			if (hadClip && clip == null)
				RebuildPreviewRootFromSourceAsset();
		}

		internal void SetVisualMode(ModelPreviewVisualMode mode)
		{
			_visualMode = mode;
			if (_visualMode != ModelPreviewVisualMode.None)
				_lastNonNoneVisualMode = _visualMode;
		}

		internal void CycleVisualMode()
		{
			if (_visualMode == ModelPreviewVisualMode.None)
			{
				_visualMode = _lastNonNoneVisualMode == ModelPreviewVisualMode.None
					? ModelPreviewVisualMode.Normals
					: _lastNonNoneVisualMode;
			}
			else
			{
				_visualMode = ModelPreviewVisualMode.None;
			}
		}

		internal void CycleModeOverride()
		{
			bool wasEffective2D = ModeContext.CameraIs2D;

			_modeOverride = _modeOverride == PreviewModeOverride.Force2D
				? PreviewModeOverride.Force3D
				: PreviewModeOverride.Force2D;

			bool isEffective2D = ModeContext.CameraIs2D;
			if (isEffective2D)
			{
				_targetOrbit = Vector2.zero;
				_orbit = Vector2.zero;
				_orbitAngularVelocity = Vector2.zero;
				_isOrbitDragging = false;
				_lastOrbitInputTime = -1d;
				_turntableEnabled = false;
			}
			else if (wasEffective2D)
			{
				FrameCameraToContent(reason: "mode-switch-3d");
				_turntableEnabled = true;
			}

			_lastGridDiagnosticsKey = null;
			LogGridDiagnosticsState("mode-changed");
		}

		internal bool HandleInput(Rect previewRect, Event evt)
		{
			if (!IsReady || evt == null)
				return false;

			bool effective2D = ModeContext.CameraIs2D;
			Rect lightWidgetRect = GetLightWidgetPadRect(previewRect);
			bool lightWidgetInteractive = _lightWidgetEnabled && LightingControlsSupported && !effective2D;
			int lightRigControlId = lightWidgetInteractive
				? GUIUtility.GetControlID(LightWidgetControlHash, FocusType.Passive, lightWidgetRect)
				: 0;
			if (GUIUtility.hotControl != 0 && GUIUtility.hotControl != lightRigControlId)
				return false;

			bool changed = false;
			double now = EditorApplication.timeSinceStartup;
			bool pointerInPreview = previewRect.Contains(evt.mousePosition);

			if (lightWidgetInteractive && HandleLightWidgetInput(evt, lightWidgetRect, lightRigControlId, effective2D, ref changed))
				return changed;

			bool isPanDrag = evt.type == EventType.MouseDrag && (evt.button == 2 || (evt.button == 0 && evt.command));
			if (isPanDrag && pointerInPreview)
			{
				PanPreviewTarget(evt.delta, previewRect, effective2D);
				_turntableEnabled = false;
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
						_turntableEnabled = false;
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
							if (!effective2D)
								_targetOrbit.y = Mathf.Clamp(_targetOrbit.y + delta.y, PitchMin, PitchMax);
							else
								_targetOrbit.y = 0f;

							Vector2 rawVelocity = new Vector2(
								delta.x / Mathf.Max(0.0001f, dt),
								effective2D ? 0f : delta.y / Mathf.Max(0.0001f, dt));
							_orbitAngularVelocity = Vector2.Lerp(_orbitAngularVelocity, rawVelocity, OrbitVelocitySmoothing);
							_turntableEnabled = false;
							changed = true;
						}

						evt.Use();
					}

					break;

				case EventType.MouseUp:
					if (evt.button == 0 && _isOrbitDragging)
					{
						double idleSinceLastMove = _lastOrbitInputTime >= 0d ? now - _lastOrbitInputTime : double.MaxValue;
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
						_turntableEnabled = false;
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

			float orbitSmoothing = Mathf.Max(0.0001f, PreviewSettings.OrbitSmoothing);
			float panSmoothing = Mathf.Max(0.0001f, PreviewSettings.PanSmoothing);
			bool effective2D = ModeContext.CameraIs2D;

			double now = EditorApplication.timeSinceStartup;
			if (_turntableEnabled && _lastInteractionUpdateTime >= 0d)
			{
				float turntableDt = Mathf.Clamp((float) (now - _lastInteractionUpdateTime), 0f, MaxDeltaTime);
				if (turntableDt > 0f)
					_targetOrbit.x += TurntableDegreesPerSecond * turntableDt;
			}

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
				effective2D,
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

			if (effective2D)
				_targetOrbit.y = 0f;

			bool animationPlaying = TickPreviewAnimationPlayback(now);
			return _turntableEnabled || pending || animationPlaying;
		}

		internal void Draw(Rect previewRect, GUIStyle background)
		{
			if (!IsReady || Event.current.type != EventType.Repaint)
				return;

			_preview.camera.backgroundColor = PreviewSettings.BackgroundColor;
			UpdateCameraTransform();
			ApplyEnvironmentState();
			SamplePreviewAnimation();
			_preview.BeginPreview(previewRect, background ?? GUIStyle.none);
			if (DrawGrid())
				LogGridDiagnosticsState("drawn");
			else
				LogGridDiagnosticsState(_gridEnabled ? "skip-grid-draw-failed" : "skip-grid-disabled");
			if (_boundsOverlayEnabled && _hasFramedBounds)
				PreviewBoundsVisualizer.DrawWire(_preview, _framedBounds);
			if (_colliderOverlayEnabled)
				ModelColliderOverlayRenderer.Draw(_preview, _colliders, _colliders2D);

			bool restoreMaterials = false;
			if (TryApplyVisualModeMaterial(out Material visualModeMaterial))
			{
				SwapRendererMaterials(visualModeMaterial);
				restoreMaterials = true;
			}

			try
			{
				using (PreviewRenderCompatibilityUtility.EnableRenderersScoped(_renderers, _rendererInitialStates))
				{
					PreviewRenderCompatibilityUtility.RenderPreviewWithCameraPath(_preview);
				}
			}
			finally
			{
				if (restoreMaterials)
					RestoreRendererMaterials();
			}

			Texture previewTexture = _preview.EndPreview();
			if (previewTexture != null)
				EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.StretchToFill);

			if (_boundsOverlayEnabled && _hasFramedBounds)
				PreviewBoundsVisualizer.DrawLabels(previewRect, _preview.camera, _framedBounds);

			if (_lightWidgetEnabled && LightingControlsSupported)
				DrawLightWidget(previewRect);
		}

		private bool TickPreviewAnimationPlayback(double now)
		{
			if (_previewAnimationClip == null || !_previewAnimationPlaying)
			{
				_lastPreviewAnimationSampleTime = now;
				return false;
			}

			if (_lastPreviewAnimationSampleTime < 0d)
			{
				_lastPreviewAnimationSampleTime = now;
				return true;
			}

			float delta = Mathf.Clamp((float) (now - _lastPreviewAnimationSampleTime), 0f, MaxDeltaTime);
			_lastPreviewAnimationSampleTime = now;

			if (delta > 0f)
			{
				float clipLength = Mathf.Max(MinAnimationClipLengthSeconds, _previewAnimationClip.length);
				_previewAnimationTime = Mathf.Repeat(_previewAnimationTime + delta * _previewAnimationSpeed, clipLength);
			}

			return true;
		}

		private void SamplePreviewAnimation()
		{
			if (_previewAnimationClip == null || _previewRoot == null)
				return;

			float clipLength = Mathf.Max(MinAnimationClipLengthSeconds, _previewAnimationClip.length);
			float sampleTime = Mathf.Repeat(_previewAnimationTime, clipLength);

			try
			{
				_previewAnimationClip.SampleAnimation(_previewRoot, sampleTime);
			}
			catch (Exception exception)
			{
				string clipName = _previewAnimationClip != null ? _previewAnimationClip.name : "<null>";
				string key = clipName + "|" + exception.GetType().Name + "|" + exception.Message;
				if (!string.Equals(_lastAnimationSampleErrorKey, key, StringComparison.Ordinal))
				{
					_lastAnimationSampleErrorKey = key;
					PreviewDiagnostics.Warn(
						"ModelAnim",
						$"SampleAnimation failed clip='{clipName}' time={sampleTime:F3}s error={exception.GetType().Name}: {exception.Message}");
				}
			}
		}

		private void BuildPreviewRootFromPrefab(GameObject prefab)
		{
			if (prefab == null || _preview == null)
				return;

			_previewRoot = UnityEngine.Object.Instantiate(prefab);
			_previewRoot.name = "ModelPreviewRoot";
			_previewRoot.hideFlags = HideFlags.HideAndDontSave;
			_previewRoot.transform.position = Vector3.zero;
			_previewRoot.transform.rotation = prefab.transform.rotation;

			_preview.AddSingleGO(_previewRoot);
			_previewRoot.GetComponentsInChildren(true, _renderers);
			_previewRoot.GetComponentsInChildren(true, _colliders);
			_previewRoot.GetComponentsInChildren(true, _colliders2D);
			_rendererInitialStates.Clear();
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				_rendererInitialStates.Add(renderer != null && renderer.enabled);
			}

			ComputeStats();
			int skinnedRendererCount = _previewRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
			int meshRendererCount = _previewRoot.GetComponentsInChildren<MeshRenderer>(true).Length;
			int animatorCount = _previewRoot.GetComponentsInChildren<Animator>(true).Length;
			int animationCount = _previewRoot.GetComponentsInChildren<Animation>(true).Length;
			string sourceAssetPath = AssetDatabase.GetAssetPath(prefab);
			PreviewDiagnostics.Log(
				"ModelSession",
				$"BuildPreviewRoot asset='{sourceAssetPath}' root='{_previewRoot.name}' renderers={_renderers.Count} skinned={skinnedRendererCount} mesh={meshRendererCount} animators={animatorCount} animations={animationCount}");
			if (_renderers.Count == 0)
			{
				PreviewDiagnostics.Warn(
					"ModelSession",
					$"No renderers found on preview root '{_previewRoot.name}'. Model preview will show grid only.");
			}
		}

		private void RebuildPreviewRootFromSourceAsset()
		{
			if (!IsReady || string.IsNullOrEmpty(_prefabAssetPath))
				return;

			GameObject prefabSource = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabAssetPath);
			if (prefabSource == null)
				return;

			if (_previewRoot != null)
			{
				UnityEngine.Object.DestroyImmediate(_previewRoot);
				_previewRoot = null;
			}

			_renderers.Clear();
			_rendererInitialStates.Clear();
			BuildPreviewRootFromPrefab(prefabSource);
			FrameCameraToContent(reason: "animation-cleared");
		}

		private Rect GetLightWidgetPadRect(Rect previewRect)
		{
			Rect panelRect = new Rect(
				previewRect.xMax - LightPadPanelSize - LightPadPanelPadding,
				previewRect.y + LightPadPanelPadding,
				LightPadPanelSize,
				LightPadPanelSize);
			return new Rect(
				panelRect.x + LightPadInnerPadding,
				panelRect.y + LightPadInnerPadding,
				panelRect.width - LightPadInnerPadding * 2f,
				panelRect.height - LightPadInnerPadding * 2f);
		}

		private void DrawLightWidget(Rect previewRect)
		{
			if (Event.current.type != EventType.Repaint || ModeContext.CameraIs2D)
				return;

			Rect padRect = GetLightWidgetPadRect(previewRect);

			Vector2 center = padRect.center;
			float radius = Mathf.Max(4f, Mathf.Min(padRect.width, padRect.height) * 0.5f - 4f);
			Handles.BeginGUI();
			Color prev = Handles.color;
			Handles.color = new Color(1f, 1f, 1f, 0.221f);
			Handles.DrawWireDisc(center, Vector3.forward, radius);
			Handles.DrawLine(new Vector3(center.x - radius, center.y), new Vector3(center.x + radius, center.y));
			Handles.DrawLine(new Vector3(center.x, center.y - radius), new Vector3(center.x, center.y + radius));
			Handles.color = prev;
			Handles.EndGUI();

			Vector2 marker = GetLightWidgetMarkerPosition(padRect);
			Rect markerRect = new Rect(marker.x - LightPadMarkerSize * 0.5f, marker.y - LightPadMarkerSize * 0.5f, LightPadMarkerSize, LightPadMarkerSize);
			EditorGUI.DrawRect(markerRect, PreviewToolbarTheme.GetToolbarButtonBackground(active: true, hovered: false));
			DrawRectBorder(markerRect, PreviewToolbarTheme.GetToolbarButtonBorder(active: true));
		}

		private bool HandleLightWidgetInput(Event evt, Rect padRect, int controlId, bool effective2D, ref bool changed)
		{
			if (effective2D)
				return false;

			switch (evt.GetTypeForControl(controlId))
			{
				case EventType.MouseDown:
					if (evt.button == 0 && padRect.Contains(evt.mousePosition))
					{
						GUIUtility.hotControl = controlId;
						SetLightRigDirectionFromPadPoint(evt.mousePosition, padRect);
						_turntableEnabled = false;
						changed = true;
						evt.Use();
						return true;
					}

					break;

				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlId)
					{
						SetLightRigDirectionFromPadPoint(evt.mousePosition, padRect);
						_turntableEnabled = false;
						changed = true;
						evt.Use();
						return true;
					}

					break;

				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlId)
					{
						GUIUtility.hotControl = 0;
						evt.Use();
						return true;
					}

					break;
			}

			return false;
		}

		private Vector2 GetLightWidgetMarkerPosition(Rect padRect)
		{
			if (_preview == null || _preview.camera == null)
				return padRect.center;

			Vector3 local = _preview.camera.transform.InverseTransformDirection(GetLightRigDirectionWorld()).normalized;
			Vector2 planar = new Vector2(local.x, local.y);
			if (planar.sqrMagnitude > 1f)
				planar.Normalize();

			float radius = Mathf.Max(4f, Mathf.Min(padRect.width, padRect.height) * 0.5f - 4f);
			return padRect.center + new Vector2(planar.x, -planar.y) * radius;
		}

		private void SetLightRigDirectionFromPadPoint(Vector2 mousePosition, Rect padRect)
		{
			if (_preview == null || _preview.camera == null)
				return;

			Vector2 center = padRect.center;
			float radius = Mathf.Max(4f, Mathf.Min(padRect.width, padRect.height) * 0.5f - 4f);
			Vector2 delta = (mousePosition - center) / Mathf.Max(0.0001f, radius);
			if (delta.sqrMagnitude > 1f)
				delta.Normalize();

			float x = delta.x;
			float y = -delta.y;
			float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
			Vector3 local = new Vector3(x, y, z).normalized;
			Vector3 world = _preview.camera.transform.TransformDirection(local).normalized;
			SetLightRigDirectionWorld(world);
		}

		private static void DrawRectBorder(Rect rect, Color color)
		{
			EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
			EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
			EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
			EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
		}

		private void ResetLightRigDirection()
		{
			_lightRigDirectionWorld = GetDefaultLightRigDirection();
		}

		private Vector3 GetDefaultLightRigDirection()
		{
			Vector3 direction = PreviewLightingSystem.RotationFromYawPitch(PreviewSettings.ModelKeyLightRotation) * Vector3.forward;
			return direction.sqrMagnitude <= 0.0001f ? Vector3.forward : direction.normalized;
		}

		private Vector3 GetLightRigDirectionWorld()
		{
			if (_lightRigDirectionWorld.sqrMagnitude <= 0.0001f)
				_lightRigDirectionWorld = GetDefaultLightRigDirection();

			return _lightRigDirectionWorld.normalized;
		}

		private void SetLightRigDirectionWorld(Vector3 direction)
		{
			if (direction.sqrMagnitude <= 0.0001f)
				return;

			_lightRigDirectionWorld = direction.normalized;
		}

		private void UpdateCameraTransform()
		{
			Camera camera = _preview.camera;
			bool effective2D = ModeContext.CameraIs2D;
			if (effective2D)
			{
				camera.orthographic = true;
				camera.orthographicSize = Mathf.Max(0.01f, _distance * 0.5f);
				camera.transform.position = _pivot + Vector3.back * _distance;
				camera.transform.rotation = Quaternion.identity;
				camera.nearClipPlane = 0.01f;
				camera.farClipPlane = Mathf.Max(100f, _distance * 30f);
				return;
			}

			camera.orthographic = false;
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
			bool lightingEnabled = ComputeLightingEnabledForTests(_lightsEnabled, ModeContext.IsUrp2DRenderer);
			bool skyboxEnabled = ComputeSkyboxEnabledForTests(
				_skyboxEnabled,
				PreviewSettings.ModelSkyboxMaterial != null,
				ModeContext.IsUrp2DRenderer);
			PreviewLightingSystem.EnsureSunLight(_preview, ref _sunLight);
			PreviewLightingSystem.EnsureRimLight(_preview, ref _rimLight);
			SharedPreviewLightingProfile lightingProfile = PreviewLightingSystem.CreateProfileFromSettings();
			PreviewLightingSystem.ApplyLighting(
				_preview,
				_sunLight,
				_rimLight,
				in lightingProfile,
				lightingEnabled,
				GetRiggedLightRotation(PreviewSettings.ModelSunLightRotation),
				GetRiggedLightRotation(PreviewSettings.ModelKeyLightRotation),
				GetRiggedLightRotation(PreviewSettings.ModelFillLightRotation),
				GetRiggedLightRotation(PreviewSettings.ModelRimLightRotation));

			if (_cameraSkybox != null)
			{
				Material skyboxMaterial = PreviewSettings.ModelSkyboxMaterial;
				bool canUseSkybox = skyboxEnabled && skyboxMaterial != null;
				_cameraSkybox.enabled = canUseSkybox;
				if (canUseSkybox)
				{
					_cameraSkybox.material = skyboxMaterial;
					_preview.camera.clearFlags = CameraClearFlags.Skybox;
				}
				else
				{
					_cameraSkybox.material = null;
					_preview.camera.clearFlags = CameraClearFlags.SolidColor;
				}
			}
			else
			{
				_preview.camera.clearFlags = CameraClearFlags.SolidColor;
			}

			if (_visualMode == ModelPreviewVisualMode.Overdraw)
			{
				_preview.camera.clearFlags = CameraClearFlags.SolidColor;
				_preview.camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
			}
		}

		private Quaternion GetRiggedLightRotation(Vector2 baseYawPitch)
		{
			Quaternion baseRotation = PreviewLightingSystem.RotationFromYawPitch(baseYawPitch);
			Quaternion rigRotation = Quaternion.FromToRotation(GetDefaultLightRigDirection(), GetLightRigDirectionWorld());
			return rigRotation * baseRotation;
		}

		private bool TryApplyVisualModeMaterial(out Material material)
		{
			EnsureVisualModeMaterials();

			material = _visualMode switch
			{
				ModelPreviewVisualMode.Normals => s_normalsMaterial,
				ModelPreviewVisualMode.UvChecker => s_uvCheckerMaterial,
				ModelPreviewVisualMode.VertexColor => s_vertexColorMaterial,
				ModelPreviewVisualMode.Matcap => PreviewMatcapAssets.GetConfiguredMatcapMaterial(),
				ModelPreviewVisualMode.Overdraw => s_overdrawMaterial,
				_ => null,
			};

			if (material == null && _visualMode != ModelPreviewVisualMode.None && PreviewSettings.EnableDiagnostics
			    && _loggedVisualModeFailure != _visualMode)
			{
				_loggedVisualModeFailure = _visualMode;
				PreviewDiagnostics.Log("ModelVisualMode", $"Material unavailable for visual mode '{_visualMode}'.");
			}

			return material != null;
		}

		private void SwapRendererMaterials(Material replacement)
		{
			SharedMaterialRestoreCache.Clear();
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;

				Material[] original = renderer.sharedMaterials;
				SharedMaterialRestoreCache.Add(original);
				if (original == null || original.Length == 0)
					continue;

				var temp = new Material[original.Length];
				for (int j = 0; j < temp.Length; j++)
					temp[j] = replacement;
				renderer.sharedMaterials = temp;
			}
		}

		private void RestoreRendererMaterials()
		{
			int restoreIndex = 0;
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;
				if (restoreIndex < SharedMaterialRestoreCache.Count)
					renderer.sharedMaterials = SharedMaterialRestoreCache[restoreIndex];
				restoreIndex++;
			}

			SharedMaterialRestoreCache.Clear();
		}

		private void DrawPivotOverlay()
		{
			if (s_solidLineMaterial == null || s_pivotCrossMesh == null)
				return;

			Matrix4x4 matrix = Matrix4x4.TRS(_pivot, Quaternion.identity, Vector3.one);
			_preview.DrawMesh(s_pivotCrossMesh, matrix, s_solidLineMaterial, 0);
		}

		private void DrawAxesOverlay()
		{
			if (s_solidLineMaterial == null || s_axesMesh == null)
				return;

			Quaternion localRotation = _previewRoot != null ? _previewRoot.transform.rotation : Quaternion.identity;
			Matrix4x4 matrix = Matrix4x4.TRS(_pivot, localRotation, Vector3.one);
			_preview.DrawMesh(s_axesMesh, matrix, s_solidLineMaterial, 0);
		}

		private void ComputeStats()
		{
			_triangleCount = 0;
			_materialSlotCount = 0;

			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;

				Material[] sharedMaterials = renderer.sharedMaterials;
				if (sharedMaterials != null)
					_materialSlotCount += sharedMaterials.Length;

				Mesh mesh = null;
				if (renderer is SkinnedMeshRenderer skinned)
				{
					mesh = skinned.sharedMesh;
				}
				else if (renderer is MeshRenderer)
				{
					MeshFilter filter = renderer.GetComponent<MeshFilter>();
					mesh = filter != null ? filter.sharedMesh : null;
				}

				if (mesh != null)
					_triangleCount += mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
			}
		}

		private void FrameCameraToContent(string reason)
		{
			if (!TryComputeFramingBounds(out Bounds bounds))
				bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
			_framedBounds = bounds;
			_hasFramedBounds = true;

			_pivot = bounds.center;
			_targetPivot = _pivot;

			bool effective2D = ModeContext.CameraIs2D;
			if (effective2D)
			{
				float target = ComputeInitialDistanceForBoundsForTests(bounds, true, _preview.camera.fieldOfView);
				_targetDistance = Mathf.Clamp(target, MinDistance, MaxDistance);
				_distance = Mathf.Clamp(Mathf.Max(_targetDistance * 1.35f, _targetDistance + 0.35f), MinDistance, MaxDistance);
				_orbit = Vector2.zero;
				_targetOrbit = Vector2.zero;
			}
			else
			{
				float target = ComputeInitialDistanceForBoundsForTests(bounds, false, _preview.camera.fieldOfView);
				_targetDistance = Mathf.Clamp(target, MinDistance, MaxDistance);
				_distance = Mathf.Clamp(Mathf.Max(_targetDistance * 1.35f, _targetDistance + 0.35f), MinDistance, MaxDistance);
				_orbit = new Vector2(130f, 18f);
				_targetOrbit = _orbit;
			}

			_orbitAngularVelocity = Vector2.zero;
			_isOrbitDragging = false;
			_lastOrbitInputTime = -1d;
			_hasSmoothedGridLodLevel = false;
			_lastGridLodLevelSampleTime = -1d;

			PreviewDiagnostics.Log(
				"ModelSession",
				$"FrameCamera reason={reason} mode={(effective2D ? "2D" : "3D")} distance={_distance:F3} target={_targetDistance:F3}");
		}

		internal static float ComputeInitialDistanceForBoundsForTests(Bounds bounds, bool effective2D, float cameraFov)
		{
			if (effective2D)
			{
				float twoDSize = Mathf.Max(bounds.extents.x, bounds.extents.y, 0.1f);
				return Mathf.Max(twoDSize * 3.2f, 0.3f);
			}

			float size = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.1f);
			float fovRad = cameraFov * Mathf.Deg2Rad * 0.5f;
			return Mathf.Max(size * 1.5f / Mathf.Max(Mathf.Tan(fovRad), 0.001f), 0.3f);
		}

		internal static bool ComputeLightingControlsSupportedForTests(bool isUrp2DRenderer)
		{
			return !isUrp2DRenderer;
		}

		internal static bool ComputeLightingEnabledForTests(bool toggleEnabled, bool isUrp2DRenderer)
		{
			return toggleEnabled && ComputeLightingControlsSupportedForTests(isUrp2DRenderer);
		}

		internal static bool ComputeSkyboxSupportedForTests(bool isUrp2DRenderer)
		{
			return !isUrp2DRenderer;
		}

		internal static bool ComputeSkyboxEnabledForTests(bool toggleEnabled, bool hasCubemap, bool isUrp2DRenderer)
		{
			return toggleEnabled && hasCubemap && ComputeSkyboxSupportedForTests(isUrp2DRenderer);
		}

		private bool TryComputeFramingBounds(out Bounds bounds)
		{
			bounds = default;
			var candidates = BuildFramingCandidates();
			if (candidates.Count == 0)
				return false;

			if (!TrySelectRobustFramingBounds(candidates, out bounds))
				bounds = ComputeLegacyFramingBounds(candidates);

			return true;
		}

		private readonly struct FramingCandidate
		{
			public readonly Bounds WorldBounds;
			public readonly Vector3 Center;
			public readonly float Diagonal;

			public FramingCandidate(Bounds worldBounds)
			{
				WorldBounds = worldBounds;
				Center = worldBounds.center;
				Diagonal = worldBounds.size.magnitude;
			}
		}

		private List<FramingCandidate> BuildFramingCandidates()
		{
			var candidates = new List<FramingCandidate>(_renderers.Count);
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
					continue;

				if (renderer is ParticleSystemRenderer)
					continue;

				Bounds worldBounds = GetRendererWorldBounds(renderer);
				if (worldBounds.size.sqrMagnitude <= 0.000001f)
					continue;

				candidates.Add(new FramingCandidate(worldBounds));
			}

			return candidates;
		}

		private static Bounds GetRendererWorldBounds(Renderer renderer)
		{
			if (renderer is SkinnedMeshRenderer)
				return renderer.bounds;

			Bounds local = renderer.localBounds;
			return TransformBounds(local, renderer.transform.localToWorldMatrix);
		}

		private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
		{
			Vector3 center = local.center;
			Vector3 extents = local.extents;
			Bounds worldBounds = new Bounds(matrix.MultiplyPoint3x4(center), Vector3.zero);

			for (int x = -1; x <= 1; x += 2)
			for (int y = -1; y <= 1; y += 2)
			for (int z = -1; z <= 1; z += 2)
			{
				Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
				worldBounds.Encapsulate(matrix.MultiplyPoint3x4(corner));
			}

			return worldBounds;
		}

		private static Bounds ComputeLegacyFramingBounds(List<FramingCandidate> candidates)
		{
			Bounds bounds = candidates[0].WorldBounds;
			for (int i = 1; i < candidates.Count; i++)
				bounds.Encapsulate(candidates[i].WorldBounds);

			return bounds;
		}

		private static bool TrySelectRobustFramingBounds(List<FramingCandidate> candidates, out Bounds selectedBounds)
		{
			selectedBounds = default;
			if (candidates == null || candidates.Count == 0)
				return false;

			float medianDiagonal = ComputeMedianDiagonal(candidates);
			float connectionThreshold = Mathf.Clamp(medianDiagonal * 10f, 0.5f, 25f);
			float connectionThresholdSqr = connectionThreshold * connectionThreshold;

			var neighbors = new List<List<int>>(candidates.Count);
			for (int i = 0; i < candidates.Count; i++)
				neighbors.Add(new List<int>());

			for (int i = 0; i < candidates.Count; i++)
			{
				Bounds a = candidates[i].WorldBounds;
				for (int j = i + 1; j < candidates.Count; j++)
				{
					Bounds b = candidates[j].WorldBounds;
					if (ComputeBoundsGapSqr(a, b) > connectionThresholdSqr)
						continue;

					neighbors[i].Add(j);
					neighbors[j].Add(i);
				}
			}

			bool[] visited = new bool[candidates.Count];
			var components = new List<List<int>>();
			for (int i = 0; i < candidates.Count; i++)
			{
				if (visited[i])
					continue;
				components.Add(BuildComponentIndices(i, neighbors, visited));
			}

			if (components.Count == 0)
				return false;

			float nearestCenterDistance = float.PositiveInfinity;
			for (int i = 0; i < candidates.Count; i++)
			{
				float distance = Vector3.Distance(candidates[i].Center, Vector3.zero);
				if (distance < nearestCenterDistance)
					nearestCenterDistance = distance;
			}

			bool pivotReliable = nearestCenterDistance <= Mathf.Max(1.5f, medianDiagonal * 3f);
			List<int> bestComponent = null;
			float bestAverageDistance = float.PositiveInfinity;
			int bestRendererCount = -1;

			for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
			{
				List<int> component = components[componentIndex];
				if (component == null || component.Count == 0)
					continue;

				float averageDistance = 0f;
				for (int i = 0; i < component.Count; i++)
					averageDistance += Vector3.Distance(candidates[component[i]].Center, Vector3.zero);

				averageDistance /= component.Count;

				if (pivotReliable)
				{
					bool betterDistance = averageDistance < bestAverageDistance - 0.0001f;
					bool tieDistanceMoreRenderers = Mathf.Abs(averageDistance - bestAverageDistance) <= 0.0001f
					                                && component.Count > bestRendererCount;
					if (!betterDistance && !tieDistanceMoreRenderers)
						continue;
				}
				else
				{
					bool moreRenderers = component.Count > bestRendererCount;
					bool tieRenderersCloser = component.Count == bestRendererCount
					                          && averageDistance < bestAverageDistance - 0.0001f;
					if (!moreRenderers && !tieRenderersCloser)
						continue;
				}

				bestComponent = component;
				bestRendererCount = component.Count;
				bestAverageDistance = averageDistance;
			}

			if (bestComponent == null || bestComponent.Count == 0)
				return false;

			Bounds bounds = candidates[bestComponent[0]].WorldBounds;
			for (int i = 1; i < bestComponent.Count; i++)
				bounds.Encapsulate(candidates[bestComponent[i]].WorldBounds);

			selectedBounds = bounds;
			return true;
		}

		private static List<int> BuildComponentIndices(int startIndex, List<List<int>> neighbors, bool[] visited)
		{
			var stack = new Stack<int>();
			var indices = new List<int>();
			stack.Push(startIndex);
			visited[startIndex] = true;

			while (stack.Count > 0)
			{
				int index = stack.Pop();
				indices.Add(index);

				List<int> adjacent = neighbors[index];
				for (int i = 0; i < adjacent.Count; i++)
				{
					int next = adjacent[i];
					if (visited[next])
						continue;

					visited[next] = true;
					stack.Push(next);
				}
			}

			return indices;
		}

		private static float ComputeBoundsGapSqr(Bounds a, Bounds b)
		{
			Vector3 aMin = a.min;
			Vector3 aMax = a.max;
			Vector3 bMin = b.min;
			Vector3 bMax = b.max;

			float dx = Mathf.Max(0f, Mathf.Max(aMin.x - bMax.x, bMin.x - aMax.x));
			float dy = Mathf.Max(0f, Mathf.Max(aMin.y - bMax.y, bMin.y - aMax.y));
			float dz = Mathf.Max(0f, Mathf.Max(aMin.z - bMax.z, bMin.z - aMax.z));
			return dx * dx + dy * dy + dz * dz;
		}

		private static float ComputeMedianDiagonal(List<FramingCandidate> candidates)
		{
			float[] diagonals = new float[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
				diagonals[i] = Mathf.Max(0f, candidates[i].Diagonal);

			Array.Sort(diagonals);
			int mid = diagonals.Length / 2;
			if ((diagonals.Length & 1) == 1)
				return diagonals[mid];

			return (diagonals[mid - 1] + diagonals[mid]) * 0.5f;
		}

		private void PanPreviewTarget(Vector2 delta, Rect previewRect, bool effective2D)
		{
			if (_preview == null || _preview.camera == null)
				return;

			float width = Mathf.Max(1f, previewRect.width);
			float height = Mathf.Max(1f, previewRect.height);

			float verticalWorldSize;
			float horizontalWorldSize;
			if (effective2D)
			{
				verticalWorldSize = Mathf.Max(0.001f, _preview.camera.orthographicSize * 2f);
				horizontalWorldSize = verticalWorldSize * (width / height);
			}
			else
			{
				float fovRadians = _preview.camera.fieldOfView * Mathf.Deg2Rad;
				verticalWorldSize = 2f * Mathf.Tan(fovRadians * 0.5f) * Mathf.Max(_targetDistance, 0.01f);
				horizontalWorldSize = verticalWorldSize * (width / height);
			}

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

			return _turntableEnabled || PreviewCameraController.HasPendingMotion(state, CameraInteractionConfig);
		}

		private static PreviewModeOverride NormalizeModeOverride(PreviewModeOverride modeOverride)
		{
			if (modeOverride == PreviewModeOverride.Force2D || modeOverride == PreviewModeOverride.Force3D)
				return modeOverride;

			return PreviewModeOverride.Force3D;
		}

		private static void EnsureOverlayResources()
		{
			EnsureOverlayResourceCleanupCallbacks();

			if (s_solidLineMaterial == null)
			{
				s_solidLineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
				{
					hideFlags = HideFlags.HideAndDontSave,
				};
				s_solidLineMaterial.SetInt("_ZWrite", 0);
				s_solidLineMaterial.SetInt("_Cull", 0);
				s_solidLineMaterial.SetInt("_ZTest", (int) CompareFunction.LessEqual);
				s_solidLineMaterial.SetInt("_SrcBlend", (int) BlendMode.SrcAlpha);
				s_solidLineMaterial.SetInt("_DstBlend", (int) BlendMode.OneMinusSrcAlpha);
				s_solidLineMaterial.renderQueue = 2999;
			}

			if (s_pivotCrossMesh == null)
			{
				s_pivotCrossMesh = new Mesh {hideFlags = HideFlags.HideAndDontSave};
				BuildPivotMesh(s_pivotCrossMesh);
			}

			if (s_axesMesh == null)
			{
				s_axesMesh = new Mesh {hideFlags = HideFlags.HideAndDontSave};
				BuildAxesMesh(s_axesMesh);
			}
		}

		private static void EnsureOverlayResourceCleanupCallbacks()
		{
			if (s_overlayResourceCleanupRegistered)
				return;

			s_overlayResourceCleanupRegistered = true;
			AssemblyReloadEvents.beforeAssemblyReload += DisposeOverlayResources;
			EditorApplication.quitting += DisposeOverlayResources;
		}

		private static void DisposeOverlayResources()
		{
			if (s_overlayResourceCleanupRegistered)
			{
				AssemblyReloadEvents.beforeAssemblyReload -= DisposeOverlayResources;
				EditorApplication.quitting -= DisposeOverlayResources;
				s_overlayResourceCleanupRegistered = false;
			}

			DestroyOwnedObject(ref s_solidLineMaterial);
			DestroyOwnedObject(ref s_pivotCrossMesh);
			DestroyOwnedObject(ref s_axesMesh);
		}

		private bool DrawGrid()
		{
			bool effective2D = ModeContext.CameraIs2D;
			PreviewGridSpace space = effective2D
				? PreviewGridSpace.Plane2D
				: PreviewGridSpace.Plane3D;
			GridLodBlend lodBlend = BuildAdaptiveGridLodBlend(effective2D);
			bool drewAny = false;

			var nearRequest = new PreviewGridDrawRequest(
				_preview,
				space,
				_gridEnabled,
				gridTransformOverride: Matrix4x4.identity,
				profileOverride: lodBlend.NearProfile,
				opacityMultiplier: lodBlend.NearOpacityMultiplier,
				drawAxisMarkers: !lodBlend.HasFarProfile);
			drewAny |= PreviewGridSystem.Draw(nearRequest);

			if (lodBlend.HasFarProfile)
			{
				var farRequest = new PreviewGridDrawRequest(
					_preview,
					space,
					_gridEnabled,
					gridTransformOverride: Matrix4x4.identity,
					profileOverride: lodBlend.FarProfile,
					opacityMultiplier: lodBlend.FarOpacityMultiplier,
					drawAxisMarkers: false);
				drewAny |= PreviewGridSystem.Draw(farRequest);

				var axisRequest = new PreviewGridDrawRequest(
					_preview,
					space,
					_gridEnabled,
					gridTransformOverride: Matrix4x4.identity,
					profileOverride: lodBlend.AxisProfile,
					opacityMultiplier: lodBlend.AxisOpacityMultiplier,
					drawAxisMarkers: true,
					drawGridLines: false);
				drewAny |= PreviewGridSystem.Draw(axisRequest);
			}

			return drewAny;
		}

		private GridLodBlend BuildAdaptiveGridLodBlend(bool effective2D)
		{
			PreviewGridProfile sharedProfile = PreviewSettings.SharedGridProfile;
			float adaptiveHalfSize = _hasFramedBounds
				? Mathf.Clamp(
					ComputeRequiredGridHalfSize(_framedBounds, effective2D),
					sharedProfile.HalfSize,
					PreviewSettings.MaxSharedGridHalfSize)
				: sharedProfile.HalfSize;

			int lodCount = Mathf.Min(GridLodDistances.Length, Mathf.Min(GridLodStepMultipliers.Length, GridLodAlphaMultipliers.Length));
			if (lodCount <= 0)
			{
				PreviewGridProfile fallbackProfile = new PreviewGridProfile(
					sharedProfile.DefaultEnabled,
					adaptiveHalfSize,
					sharedProfile.Step,
					sharedProfile.Alpha,
					sharedProfile.Style);
				return new GridLodBlend(
					fallbackProfile,
					1f,
					false,
					fallbackProfile,
					0f,
					fallbackProfile,
					1f);
			}

			float lodLevel = Mathf.Clamp(GetSmoothedGridLodLevel(lodCount), 0f, lodCount - 1);
			int nearIndex = Mathf.Clamp(Mathf.FloorToInt(lodLevel), 0, lodCount - 1);
			int farIndex = Mathf.Clamp(nearIndex + 1, nearIndex, lodCount - 1);
			float blend = Mathf.Clamp01(lodLevel - nearIndex);
			blend = blend * blend * (3f - 2f * blend);

			float nearStep = Mathf.Clamp(sharedProfile.Step * GridLodStepMultipliers[nearIndex], PreviewSettings.MinSharedGridStep, PreviewSettings.MaxSharedGridStep);
			float farStep = Mathf.Clamp(sharedProfile.Step * GridLodStepMultipliers[farIndex], PreviewSettings.MinSharedGridStep, PreviewSettings.MaxSharedGridStep);

			PreviewGridProfile nearProfile = new PreviewGridProfile(
				sharedProfile.DefaultEnabled,
				adaptiveHalfSize,
				nearStep,
				sharedProfile.Alpha,
				sharedProfile.Style);

			if (nearIndex == farIndex || Mathf.Abs(farStep - nearStep) <= 0.0001f)
			{
				return new GridLodBlend(
					nearProfile,
					GridLodAlphaMultipliers[nearIndex],
					false,
					nearProfile,
					0f,
					nearProfile,
					GridLodAlphaMultipliers[nearIndex]);
			}

			PreviewGridProfile farProfile = new PreviewGridProfile(
				sharedProfile.DefaultEnabled,
				adaptiveHalfSize,
				farStep,
				sharedProfile.Alpha,
				sharedProfile.Style);

			float nearOpacity = Mathf.Clamp01((1f - blend) * GridLodAlphaMultipliers[nearIndex]);
			float farOpacity = Mathf.Clamp01(blend * GridLodAlphaMultipliers[farIndex]);
			bool preferFarAxis = farOpacity > nearOpacity;
			return new GridLodBlend(
				nearProfile,
				nearOpacity,
				true,
				farProfile,
				farOpacity,
				preferFarAxis ? farProfile : nearProfile,
				Mathf.Max(nearOpacity, farOpacity));
		}

		private float GetSmoothedGridLodLevel(int lodCount)
		{
			float targetLodLevel = ComputeTargetGridLodLevel(Mathf.Max(0f, _distance), lodCount);
			double now = EditorApplication.timeSinceStartup;
			if (!_hasSmoothedGridLodLevel || _lastGridLodLevelSampleTime < 0d)
			{
				_smoothedGridLodLevel = targetLodLevel;
				_hasSmoothedGridLodLevel = true;
				_lastGridLodLevelSampleTime = now;
				return _smoothedGridLodLevel;
			}

			float dt = Mathf.Clamp((float) (now - _lastGridLodLevelSampleTime), 0f, 0.1f);
			_lastGridLodLevelSampleTime = now;
			if (dt <= 0f)
				return _smoothedGridLodLevel;

			float blend = 1f - Mathf.Exp(-GridLodLevelSmoothing * dt);
			float blendedLevel = Mathf.Lerp(_smoothedGridLodLevel, targetLodLevel, blend);
			float maxDelta = GridLodMaxLevelsPerSecond * dt;
			_smoothedGridLodLevel = Mathf.MoveTowards(_smoothedGridLodLevel, blendedLevel, maxDelta);
			return _smoothedGridLodLevel;
		}

		private static float ComputeTargetGridLodLevel(float distance, int lodCount)
		{
			if (lodCount <= 1)
				return 0f;

			float clampedDistance = Mathf.Max(0f, distance);
			if (clampedDistance <= GridLodDistances[0])
				return 0f;

			for (int i = 0; i < lodCount - 1; i++)
			{
				float start = GridLodDistances[i];
				float end = GridLodDistances[i + 1];
				if (clampedDistance > end)
					continue;

				float range = Mathf.Max(0.0001f, end - start);
				float t = Mathf.Clamp01((clampedDistance - start) / range);
				return i + t;
			}

			return lodCount - 1;
		}

		private static float ComputeRequiredGridHalfSize(Bounds bounds, bool effective2D)
		{
			Vector3 min = bounds.min;
			Vector3 max = bounds.max;
			float maxAbsX = Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x));
			float maxAbsSecondaryAxis = effective2D
				? Mathf.Max(Mathf.Abs(min.y), Mathf.Abs(max.y))
				: Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z));
			float boundsHalfSpanFromOrigin = Mathf.Max(maxAbsX, maxAbsSecondaryAxis);
			float scale = PreviewSettings.SharedGridFadeStartBoundsScale;
			float padding = PreviewSettings.SharedGridFadeStartBoundsPadding;
			return boundsHalfSpanFromOrigin * scale + padding;
		}

		private void LogGridDiagnosticsState(string state)
		{
			bool hasCanvas = _previewRoot != null && _previewRoot.GetComponentInChildren<Canvas>(true) != null;
			bool hasTmpUi = HasComponentInChildren(_previewRoot, TmpTextMeshProUiType);
			bool hasTmpMesh = HasComponentInChildren(_previewRoot, TmpTextMeshProType);
			bool effective2D = ModeContext.CameraIs2D;
			Vector3 gridAnchor = Vector3.zero;
			string key = string.Concat(
				state, "|", _gridEnabled ? "1" : "0", "|", effective2D ? "2d" : "3d", "|",
				hasCanvas ? "canvas1" : "canvas0", "|", hasTmpUi ? "tmpui1" : "tmpui0", "|", hasTmpMesh ? "tmpmesh1" : "tmpmesh0");

			if (string.Equals(_lastGridDiagnosticsKey, key, StringComparison.Ordinal))
				return;

			_lastGridDiagnosticsKey = key;
			PreviewDiagnostics.Log(
				"ModelGrid",
				$"state={state} asset='{_prefabAssetPath}' mode={(effective2D ? "2D" : "3D")} gridEnabled={_gridEnabled} renderers={_renderers.Count} gridAnchor={gridAnchor} hasCanvas={hasCanvas} hasTMP_UI={hasTmpUi} hasTMP_Mesh={hasTmpMesh}");
		}

		private static bool HasComponentInChildren(GameObject root, Type componentType)
		{
			if (root == null || componentType == null)
				return false;

			return root.GetComponentInChildren(componentType, true) != null;
		}

		private static void BuildPivotMesh(Mesh mesh)
		{
			var vertices = new List<Vector3>();
			var colors = new List<Color>();
			Color c = new Color(1f, 1f, 1f, 0.95f);
			float r = PivotMarkerRadius;

			static void AddEdge(List<Vector3> verts, List<Color> cols, Vector3 a, Vector3 b, Color col)
			{
				verts.Add(a);
				cols.Add(col);
				verts.Add(b);
				cols.Add(col);
			}

			AddEdge(vertices, colors, new Vector3(-r, 0f, 0f), new Vector3(r, 0f, 0f), c);
			AddEdge(vertices, colors, new Vector3(0f, -r, 0f), new Vector3(0f, r, 0f), c);
			AddEdge(vertices, colors, new Vector3(0f, 0f, -r), new Vector3(0f, 0f, r), c);

			for (int i = 0; i < PivotMarkerSegments; i++)
			{
				float t0 = i / PivotMarkerSegments * Mathf.PI * 2f;
				float t1 = (i + 1f) / PivotMarkerSegments * Mathf.PI * 2f;
				Vector3 a = new Vector3(Mathf.Cos(t0) * r, Mathf.Sin(t0) * r, 0f);
				Vector3 b = new Vector3(Mathf.Cos(t1) * r, Mathf.Sin(t1) * r, 0f);
				AddEdge(vertices, colors, a, b, c);
			}

			SetLineMeshData(mesh, vertices, colors);
		}

		private static void BuildAxesMesh(Mesh mesh)
		{
			var vertices = new List<Vector3>();
			var colors = new List<Color>();

			static void AddEdge(List<Vector3> verts, List<Color> cols, Vector3 a, Vector3 b, Color col)
			{
				verts.Add(a);
				cols.Add(col);
				verts.Add(b);
				cols.Add(col);
			}

			AddEdge(vertices, colors, Vector3.zero, Vector3.right * AxisSize, new Color(1f, 0.28f, 0.28f, 0.95f));
			AddEdge(vertices, colors, Vector3.zero, Vector3.up * AxisSize, new Color(0.28f, 1f, 0.28f, 0.95f));
			AddEdge(vertices, colors, Vector3.zero, Vector3.forward * AxisSize, new Color(0.35f, 0.7f, 1f, 0.95f));

			SetLineMeshData(mesh, vertices, colors);
		}

		private static void DestroyOwnedObject<T>(ref T value) where T : UnityEngine.Object
		{
			if (value == null)
				return;

			UnityEngine.Object.DestroyImmediate(value);
			value = null;
		}

		private static void SetLineMeshData(Mesh mesh, List<Vector3> vertices, List<Color> colors)
		{
			mesh.Clear();
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			int[] indices = new int[vertices.Count];
			for (int i = 0; i < indices.Length; i++)
				indices[i] = i;
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
		}

		private static void EnsureVisualModeMaterials()
		{
			s_normalsMaterial ??= LoadVisualModeMaterial(NormalsMaterialPath);
			s_uvCheckerMaterial ??= LoadVisualModeMaterial(UvCheckerMaterialPath);
			s_vertexColorMaterial ??= LoadVisualModeMaterial(VertexColorMaterialPath);
			s_overdrawMaterial ??= LoadVisualModeMaterial(OverdrawMaterialPath);
		}

		private static Material LoadVisualModeMaterial(string fileName)
		{
			string[] candidatePaths = PreviewInstallLayout.BuildAssetPaths("Editor/Common/PreviewAssets/VisualModes/" + fileName);
			for (int i = 0; i < candidatePaths.Length; i++)
			{
				Material loaded = AssetDatabase.LoadAssetAtPath<Material>(candidatePaths[i]);
				if (loaded != null)
					return loaded;
			}

			return null;
		}
	}
}