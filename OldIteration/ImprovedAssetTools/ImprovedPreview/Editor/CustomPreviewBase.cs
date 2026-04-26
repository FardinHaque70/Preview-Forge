#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FardinHaque.ImprovedAssetTools.Editor
{

/// <summary>
/// Base class for all custom prefab preview implementations.
///
/// Handles:
///   - Hidden Camera + RenderTexture in the main scene at a far world offset
///   - Orbit (left drag) and zoom (scroll) with exponential-decay smoothing
///   - Colored background
///   - 3D grid drawn via Graphics.DrawMesh
///
/// Subclasses override:
///   bool  Supports(GameObject prefab)
///   void  OnSetup(GameObject prefab)
///   void  OnSimulate(float dt)
///   void  DrawExtraToolbar(ref Rect r)
///   void  OnBeforeRender()
///   void  OnAfterRender()
///   void  OnCleanup()
/// </summary>
public abstract class CustomPreviewBase : IPreviewRenderHost
{
	private const double PreviewIdleSuspendSeconds = 0.35d;
	private const double InteractiveQualityHoldSeconds = 0.1d;
	private const double ExternalFocusPauseDebounceSeconds = 0.18d;
	private const float InteractiveResolutionScale = 0.9f;
	private const int InteractiveAntiAliasing = 2;
	private const float MaxPlaybackDeltaSeconds = 0.05f;

	// =====================================================================
	// Abstract / virtual interface
	// =====================================================================

	public abstract PreviewAssetTypeKey PreviewTypeKey { get; }
	public abstract bool Supports(GameObject prefab);

protected virtual void OnSetup(GameObject prefab) { }
protected virtual void OnSimulate(float dt) { }
protected virtual void DrawExtraToolbar(ref Rect r) { }
protected virtual void HandleOverlayInput(Rect r) { }
protected virtual void DrawOverlay(Rect r) { }
protected virtual void DrawExtraPreviewSettings() { }
protected virtual void OnBeforeRender() { }
protected virtual void OnAfterRender() { }
protected virtual void OnCleanup() { }
	protected virtual void OnManualCameraInteraction() { }
	protected virtual bool ShouldDrawSharedToolbarInPreview() => !IsTwoDimensionalRendererCompatibilityModeActive();
	protected virtual bool RequiresPreviewSceneSetup() => true;
	protected virtual bool RenderPreviewAtOrigin() => true;
	protected virtual bool ShouldShowLightsToolbarButton() => true;
	protected virtual bool ShouldShowGridToolbarButton() => true;
	protected virtual bool ShouldShowSkyboxToolbarButton() => true;
	// Return false when the current preview content should never use custom key/fill/rim lights.
	protected virtual bool SupportsPreviewLightRig() => true;
	// Return false to suppress the grid when the 2D renderer is active (e.g. sprite-only previews)
	protected virtual bool ShouldDrawGridIn2DMode() => true;
	// Return false to opt out of 2D renderer compatibility mode (orthographic front view, XY grid).
	// Override to false for previews that need 3D camera regardless of the active render pipeline.
	protected virtual bool ShouldUse2DCompatibilityMode() => true;
	// Return true to force 2D compatibility mode even when the active pipeline is not effectively 2D.
	protected virtual bool ShouldForce2DCompatibilityMode() => false;
	// Return true to keep XZ grid orientation for previews that stay in 3D mode in effectively-2D projects.
	protected virtual bool Prefer3DGridOrientationWhenNotIn2DCompatibilityMode() => false;
	// Return true for previews that should auto-pause playback when interaction moves outside the preview host.
	protected virtual bool ShouldAutoPausePlaybackOnExternalInteraction() => false;
	// Return a non-null color to override the render background (e.g. for alpha view modes)
	protected virtual Color? GetRenderBackgroundColorOverride() => null;
	// Return true to keep the camera from clearing the color buffer (i.e. preserve a background
	// that was blitted in OnPreviewRenderPrepared). Camera will use ClearFlags.Depth only.
	protected virtual bool ShouldPreserveCameraColorBuffer() => false;
	// Called after GL.Clear and before PreviewCam.Render — blit backgrounds here
	protected virtual void OnPreviewRenderPrepared(RenderTexture rt) { }

	// Set by PrefabPreviewEditor so the base can trigger inspector repaints
	public System.Action RepaintAction;

	// =====================================================================
	// Public API — called by PrefabPreviewEditor
	// =====================================================================

	public void Enable(System.Action repaintAction)
	{
		RepaintAction = repaintAction;
		AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
		EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		RegisterSceneLightCacheInvalidation();
	}

	public void Disable()
	{
		EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
		UnregisterSceneLightCacheInvalidation();
		AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
		Cleanup();
	}

	private void OnPlayModeStateChanged(PlayModeStateChange state)
	{
		if (state == PlayModeStateChange.ExitingEditMode)
			Cleanup();
	}

	public void OnPreviewGUI(Rect r, GameObject prefab)
	{
		if (prefab == null) return;
		Rect fullPreviewRect = r;

		bool requiresPreviewSceneSetup = RequiresPreviewSceneSetup();
		if (requiresPreviewSceneSetup)
			EnsureSetup(prefab);

		if (requiresPreviewSceneSetup)
			TrackPreviewHostWindow();

		if (requiresPreviewSceneSetup &&
		    _playing &&
		    ShouldAutoPausePlaybackOnExternalInteraction() &&
		    Event.current.type == EventType.MouseDown &&
		    !fullPreviewRect.Contains(Event.current.mousePosition))
		{
			StopPlayback();
			RequestPreviewRepaint();
		}

		if (requiresPreviewSceneSetup && _playing)
			EnsurePlaybackUpdateRegistered();

		if (requiresPreviewSceneSetup && HasPendingCameraMotion())
			EnsureCameraLerpRegistered();

		if (ShouldDrawSharedToolbarInPreview())
			DrawInPreviewToolbar(ref r);
		DrawExtraToolbar(ref r);
		HandleOverlayInput(r);
		if (requiresPreviewSceneSetup)
			HandleInput(r);

		if (requiresPreviewSceneSetup && Event.current.type == EventType.Repaint)
		{
			double now = EditorApplication.timeSinceStartup;
			// Compute time since the last rendered frame for turntable advancement.
			// Using the paint cycle (not EditorUpdate) keeps turntable speed tied to
			// actual rendered frame rate rather than the ~200Hz EditorUpdate tick rate.
			float repaintDt = _lastPreviewGuiTime >= 0d
				? Mathf.Clamp((float)(now - _lastPreviewGuiTime), 0f, 0.1f)
				: 0f;
			_lastPreviewGuiTime = now;

			// Advance turntable: move both orbit and target together so there is no
			// lerp lag — the camera rotates at a constant speed without chasing itself.
			// Refreshing _lastInteractionTime on each advance keeps IsUserEngagingWithPreviewHost
			// returning true via the grace period, so the repaint loop is self-sustaining
			// for as long as the turntable is enabled — the same way a continuous orbit
			// drag keeps the loop alive. The loop breaks the moment the turntable is toggled off.
			if (_turntableActive && repaintDt > 0f)
			{
				float delta = TurntableDegreesPerSecond * repaintDt;
				PreviewInteractionState interactionState = BuildInteractionState();
				interactionState.Orbit.x += delta;
				interactionState.TargetOrbit.x += delta;
				interactionState.AngularVelocity = Vector2.zero;
				interactionState.IsOrbitDragging = false;
				interactionState.LastOrbitInputTime = now;
				ApplyInteractionState(interactionState);
				_lastInteractionTime = now;
				_previewDirty = true;
			}

			bool useInteractiveQuality = ShouldUseInteractivePreviewQuality();
			int renderStateHash = ComputeRenderStateHash(r, useInteractiveQuality);
			if (_previewDirty || _lastPreviewTex == null || _lastRenderStateHash != renderStateHash)
				RenderPreview(r, useInteractiveQuality, renderStateHash);

			// Drive the repaint loop while there is something to animate.
			//
			// _playing (particles / animation): always repaint — the repaint is already
			// scoped to Inspector windows only, so other windows are never flooded.
			//
			// _turntableActive / HasPendingCameraMotion (camera motion): repaint only
			// while the user is engaged with the preview host window. This prevents the
			// 3D render cost from stalling input in other windows (e.g. Project Settings
			// sliders) when the user has moved away. The 0.5 s grace period inside
			// IsUserEngagingWithPreviewHost keeps the loop alive during orbit drags even
			// if Unity's window-hover API momentarily lags.
			bool isEngaged = IsUserEngagingWithPreviewHost();
			if (_playing || ((_turntableActive || HasPendingCameraMotion()) && isEngaged))
				RepaintAction?.Invoke();
		}

		if (_lastPreviewTex != null)
			EditorGUI.DrawPreviewTexture(r, _lastPreviewTex, null, ScaleMode.StretchToFill);

		DrawOverlay(r);
	}

	public void OnPreviewSettings(GameObject prefab)
	{
		// Leave the native Unity preview header untouched.
	}

	// =====================================================================
	// Protected helpers available to subclasses
	// =====================================================================

	/// <summary>The instantiated prefab clone, placed at WorldOffset.</summary>
	protected GameObject PreviewRoot { get; private set; }

	/// <summary>The hidden preview camera.</summary>
	protected Camera PreviewCam { get; private set; }

	/// <summary>World-space offset so the instance is invisible in Scene/Game views.</summary>
	protected static readonly Vector3 WorldOffset = new Vector3(5000f, 5000f, 5000f);

	/// <summary>Dedicated layer for preview objects — keeps lights and renderers
	/// isolated from Game/Scene cameras and other scene lights.</summary>
	protected const int PreviewLayer = 31;

	/// <summary>Move the camera orbit pivot. Default is WorldOffset.</summary>
	protected void SetPivot(Vector3 worldPos) => _pivot = worldPos;

	/// <summary>Set camera distance immediately (no lerp).</summary>
	protected void SetCameraDistance(float dist)
	{
		_distance = dist;
		_targetDistance = dist;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;
	}

	/// <summary>
	/// Start from a slightly farther distance and smoothly settle to the fitted target.
	/// Use this when a preview first appears so framing feels less abrupt.
	/// </summary>
	protected void SetCameraDistanceWithIntroZoom(float targetDist, float startDistanceMultiplier = 1.35f, float minimumExtraDistance = 0.35f)
	{
		float clampedTarget = Mathf.Clamp(targetDist, 0.05f, 500f);
		float clampedMultiplier = Mathf.Max(1f, startDistanceMultiplier);
		float clampedExtraDistance = Mathf.Max(0f, minimumExtraDistance);
		float introDistance = Mathf.Clamp(
			Mathf.Max(clampedTarget * clampedMultiplier, clampedTarget + clampedExtraDistance),
			0.05f,
			500f);

		_distance = introDistance;
		_targetDistance = clampedTarget;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;

		if (Mathf.Abs(_distance - _targetDistance) > 0.001f)
			EnsureCameraLerpRegistered();

		LogLifecycleDiagnostic(
			$"IntroZoom target={clampedTarget:F3} intro={introDistance:F3} " +
			$"asset='{_currentPrefabAssetPath ?? "<null>"}'");
	}

	/// <summary>Set initial orbit angles (yaw, pitch) immediately.</summary>
	protected void SetOrbit(Vector2 orbitDeg)
	{
		_orbit = orbitDeg;
		_targetOrbit = orbitDeg;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;
	}

	/// <summary>Read the current orbit angles (yaw, pitch).</summary>
	protected Vector2 GetCurrentOrbit() => _targetOrbit;

	/// <summary>
	/// Returns true while camera orbit/zoom lerp is still settling toward its target.
	/// Useful for preview-specific timing gates (e.g. particle auto-restart deferral).
	/// </summary>
	protected bool IsCameraMotionSettling() => HasPendingCameraMotion();

	protected Vector3 GetLightRigDirectionWorld()
	{
		EnsureLightRigInitialized();
		return _lightRigDirectionWorld;
	}

	protected void SetLightRigDirectionWorld(Vector3 direction)
	{
		if (direction.sqrMagnitude < 0.0001f)
			return;

		_lightRigDirectionWorld = direction.normalized;
		_hasCustomLightRigDirection = true;
		RequestPreviewRepaint();
	}

	protected void ResetLightRigDirection()
	{
		_lightRigDirectionWorld = GetDefaultLightRigDirection();
		_hasCustomLightRigDirection = true;
	}

	// Playback helpers — subclasses call these to drive OnSimulate via EditorUpdate
	protected bool IsPlaying => _playing;

	/// <summary>
	/// True while the preview is actively animating — either because playback is running
	/// or the camera is still lerping toward its target. Used by the host to decide
	/// whether to request continuous repaints.
	/// </summary>
	public bool IsAnimating => _playing || _cameraLerpRegistered;
	protected float CurrentTime => _time;
	protected float MaxTime { get => _maxTime; set => _maxTime = Mathf.Max(0.01f, value); }
	protected bool NeedsSeek { get => _needsSeek; set => _needsSeek = value; }
	protected float SimulatedTime { get => _simulatedTime; set => _simulatedTime = value; }

	/// <summary>Seek to a specific time. Stops playback and marks preview dirty.</summary>
	protected void SeekToTime(float t)
	{
		StopPlayback();
		_time = Mathf.Clamp(t, 0f, _maxTime);
		_needsSeek = true;
		_simulatedTime = 0f;
		RequestPreviewRepaint();
	}

	protected void StartPlayback()
	{
		if (_playing) return;

		if (_time >= _maxTime)
		{
			_time = 0f;
			_needsSeek = true;
			_simulatedTime = 0f;
		}

		_playing = true;
		_nextPlaybackTickTime = -1d;
		_focusMismatchStartTime = -1d;
		_externalInteractionPauseRequested = false;
		_previewHostWindow ??= EditorWindow.focusedWindow ?? EditorWindow.mouseOverWindow;
		_lastInteractionTime = EditorApplication.timeSinceStartup;
		EnsurePlaybackUpdateRegistered();

		RequestPreviewRepaint();
	}

	protected void StopPlayback()
	{
		if (!_playing) return;

		_playing = false;
		_focusMismatchStartTime = -1d;
		_externalInteractionPauseRequested = false;
		StopPlaybackUpdate();
		RequestPreviewRepaint();
	}

	/// <summary>
	/// Rebuild the simulated preview immediately while paused.
	/// Use this after SeekToTime(...) when scrubbing a slider.
	/// </summary>
	protected void ForcePausedSimulationRefresh()
	{
		if (!_isSetup || PreviewRoot == null) return;
		if (_playing) return;
		if (!_needsSeek) return;

		OnSimulate(0f);
		RequestPreviewRepaint();
	}

	/// <summary>
	/// Request all relevant editor views to repaint.
	/// </summary>
	protected void RequestPreviewRepaint()
	{
		_previewDirty = true;
		RepaintAction?.Invoke();
	}

	// =====================================================================
	// Private state
	// =====================================================================

	private Light _light0;
	private Light _light1;
	private Light _light2;
	private RenderTexture _previewRT;
	private int _rtW;
	private int _rtH;
	private Texture _lastPreviewTex;

	private Vector2 _orbit = new Vector2(35f, 15f);
	private Vector2 _targetOrbit = new Vector2(35f, 15f);
	private float _distance = 8f;
	private float _targetDistance = 8f;
	private Vector2 _orbitAngularVelocity;
	private bool _isOrbitDragging;
	private double _lastOrbitInputTime = -1d;
	private double _lastUpdateTime = -1;
	private Vector3 _pivot;

	private Color _bgColor = Color.black;

	private bool _useLighting = true;
	private bool _showGrid = true;
	private bool _showSkybox = true;

	protected bool IsLightingEnabled() => _useLighting;
	protected void ToggleLighting() => _useLighting = !_useLighting;
	protected void SetLightingEnabled(bool enabled) => _useLighting = enabled;
	protected bool IsGridEnabled() => _showGrid;
	protected void ToggleGrid() => _showGrid = !_showGrid;
	protected void SetGridEnabled(bool enabled) => _showGrid = enabled;
	protected bool IsSkyboxEnabled() => _showSkybox;
	protected void ToggleSkybox() => _showSkybox = !_showSkybox;
	protected void SetSkyboxEnabled(bool enabled) => _showSkybox = enabled;
	protected bool TurntableActive => _turntableActive;
	protected void SetTurntableActive(bool active) { _turntableActive = active; if (active) RequestPreviewRepaint(); }
	protected void ToggleTurntable() => SetTurntableActive(!_turntableActive);
	protected virtual float TurntableDegreesPerSecond => 30f;
	protected bool IsTwoDimensionalRendererCompatibilityModeActive()
	{
		PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();
		return ShouldForce2DCompatibilityMode() || (ShouldUse2DCompatibilityMode() && pipelineContext.IsEffectively2D);
	}
	private bool _lockCamera;

	private Mesh _gridMesh;
	private Material _gridMat;
	private Mesh _gizmoSphereMesh;
	private Material _gizmoMat;
	private Material _skyboxPreviewMat;
	private Texture _lastSkyboxTexture;

	private bool _playing;
	private double _lastEditorTime = -1;
	private double _nextPlaybackTickTime = -1d;
	private float _time;
	private float _maxTime = 2f;
	private bool _needsSeek = true;
	private float _simulatedTime;
	private Vector3 _lightRigDirectionWorld;
	private bool _hasCustomLightRigDirection;
	private bool _cameraLerpRegistered;
	private bool _playbackUpdateRegistered;
	private bool _previewDirty = true;
	private bool _turntableActive;
	private int _lastRenderStateHash;
	private double _lastPreviewGuiTime = -1d;
	private double _lastInteractionTime = -1d;
	private double _focusMismatchStartTime = -1d;
	private EditorWindow _previewHostWindow;
	private bool _externalInteractionPauseRequested;

	private bool _isSetup;
	private GameObject _currentPrefab;
	private string _currentPrefabAssetPath;

	// Cached scene lights — refreshed on hierarchy change, not every frame
	private readonly List<Light> _cachedSceneLights = new();
	private bool _sceneLightCacheDirty = true;
	private static readonly HashSet<string> s_loggedDiagnostics = new();
	private static readonly Dictionary<string, PersistedPreviewState> s_recentPreviewStates = new();
	private static bool s_selectionTrackingInitialized;
	private static string s_currentSelectionAssetPath;
	private static int s_selectionRevision;
	private static Mesh s_sharedGridMesh;
	private static Material s_sharedGridMaterial;
	private static float s_sharedGridHalfSize = -1f;
	private static float s_sharedGridStep = -1f;
	private static float s_sharedGridAlpha = -1f;
	private static Mesh s_sharedGizmoSphereMesh;
	private static Material s_sharedGizmoMaterial;

	// Cached gizmo sphere color arrays — avoid per-frame allocation
	private Color[] _gizmoKeyColors;
	private Color[] _gizmoFillColors;
	private Color _lastGizmoKeyColor = Color.clear;
	private Color _lastGizmoFillColor = Color.clear;

	private readonly struct PersistedPreviewState
	{
		public PersistedPreviewState(
			int selectionRevision,
			Vector3 pivot,
			Vector2 orbit,
			Vector2 targetOrbit,
			Vector2 orbitAngularVelocity,
			float distance,
			float targetDistance,
			bool turntableActive,
			bool useLighting,
			bool showGrid,
			bool showSkybox,
			double lastInteractionTime)
		{
			SelectionRevision = selectionRevision;
			Pivot = pivot;
			Orbit = orbit;
			TargetOrbit = targetOrbit;
			OrbitAngularVelocity = orbitAngularVelocity;
			Distance = distance;
			TargetDistance = targetDistance;
			TurntableActive = turntableActive;
			UseLighting = useLighting;
			ShowGrid = showGrid;
			ShowSkybox = showSkybox;
			LastInteractionTime = lastInteractionTime;
		}

		public int SelectionRevision { get; }
		public Vector3 Pivot { get; }
		public Vector2 Orbit { get; }
		public Vector2 TargetOrbit { get; }
		public Vector2 OrbitAngularVelocity { get; }
		public float Distance { get; }
		public float TargetDistance { get; }
		public bool TurntableActive { get; }
		public bool UseLighting { get; }
		public bool ShowGrid { get; }
		public bool ShowSkybox { get; }
		public double LastInteractionTime { get; }
	}

	string IPreviewRenderHost.HostName => GetType().Name;
	bool IPreviewRenderHost.PreserveCameraColorBuffer => ShouldPreserveCameraColorBuffer();

	// =====================================================================
	// Setup / Cleanup
	// =====================================================================

	private string BuildPersistedStateKey(string prefabAssetPath)
	{
		if (string.IsNullOrEmpty(prefabAssetPath))
			return null;

		return $"{GetType().FullName}|{prefabAssetPath}";
	}

	private static string GetCurrentSelectedAssetPath()
	{
		UnityEngine.Object selected = Selection.activeObject;
		if (selected == null)
			return null;

		string assetPath = AssetDatabase.GetAssetPath(selected);
		return string.IsNullOrEmpty(assetPath) ? null : assetPath;
	}

	private static void HandleSelectionChanged()
	{
		string nextSelectionAssetPath = GetCurrentSelectedAssetPath();
		if (string.Equals(s_currentSelectionAssetPath, nextSelectionAssetPath, System.StringComparison.Ordinal))
			return;

		s_currentSelectionAssetPath = nextSelectionAssetPath;
		s_selectionRevision++;
		s_recentPreviewStates.Clear();
	}

	private static void EnsureSelectionTrackingInitialized()
	{
		if (s_selectionTrackingInitialized)
			return;

		s_selectionTrackingInitialized = true;
		s_currentSelectionAssetPath = GetCurrentSelectedAssetPath();
		Selection.selectionChanged += HandleSelectionChanged;
	}

	private void SavePersistedStateIfEligible()
	{
		if (!_isSetup || string.IsNullOrEmpty(_currentPrefabAssetPath))
			return;

		string key = BuildPersistedStateKey(_currentPrefabAssetPath);
		if (string.IsNullOrEmpty(key))
			return;

		EnsureSelectionTrackingInitialized();
		s_recentPreviewStates[key] = new PersistedPreviewState(
			s_selectionRevision,
			_pivot,
			_orbit,
			_targetOrbit,
			_orbitAngularVelocity,
			_distance,
			_targetDistance,
			_turntableActive,
			_useLighting,
			_showGrid,
			_showSkybox,
			_lastInteractionTime);
	}

	private bool TryRestorePersistedState(string prefabAssetPath)
	{
		string key = BuildPersistedStateKey(prefabAssetPath);
		if (string.IsNullOrEmpty(key))
			return false;

		EnsureSelectionTrackingInitialized();

		if (!s_recentPreviewStates.TryGetValue(key, out PersistedPreviewState state))
			return false;

		if (state.SelectionRevision != s_selectionRevision)
			return false;

		_pivot = state.Pivot;
		_orbit = state.Orbit;
		_targetOrbit = state.TargetOrbit;
		_orbitAngularVelocity = state.OrbitAngularVelocity;
		_distance = state.Distance;
		_targetDistance = state.TargetDistance;
		_turntableActive = state.TurntableActive;
		_useLighting = state.UseLighting;
		_showGrid = state.ShowGrid;
		_showSkybox = state.ShowSkybox;
		_lastInteractionTime = state.LastInteractionTime;

		if (HasPendingCameraMotion())
			EnsureCameraLerpRegistered();

		LogLifecycleDiagnostic($"Restored recent state for asset='{prefabAssetPath}'");
		return true;
	}

	private void EnsureSetup(GameObject prefab)
	{
		if (prefab == null) return;
		string prefabAssetPath = AssetDatabase.GetAssetPath(prefab);
		bool isSamePrefab = _currentPrefab == prefab;
		if (!isSamePrefab
		    && !string.IsNullOrEmpty(_currentPrefabAssetPath)
		    && !string.IsNullOrEmpty(prefabAssetPath))
		{
			isSamePrefab = string.Equals(_currentPrefabAssetPath, prefabAssetPath, System.StringComparison.Ordinal);
		}

		if (_isSetup && PreviewRoot != null && isSamePrefab)
		{
			LogLifecycleDiagnostic(
				$"EnsureSetup skipped (already setup) prefab='{prefab.name}' id={prefab.GetInstanceID()} asset='{prefabAssetPath}'");
			return;
		}

		LogLifecycleDiagnostic(
			$"EnsureSetup begin prefab='{prefab.name}' id={prefab.GetInstanceID()} asset='{prefabAssetPath}' " +
			$"prevPrefab='{_currentPrefabAssetPath ?? _currentPrefab?.name ?? "<null>"}'");

		Cleanup();
		_currentPrefab = prefab;
		_currentPrefabAssetPath = prefabAssetPath;

		// Camera
		var camGO = new GameObject("___PreviewCam___") { hideFlags = HideFlags.HideAndDontSave };
		PreviewCam = camGO.AddComponent<Camera>();
		PreviewCam.clearFlags = CameraClearFlags.SolidColor;
		_bgColor = ImprovedPreviewSettings.BgColor;
		PreviewCam.backgroundColor = _bgColor;
		PreviewCam.nearClipPlane = 0.01f;
		PreviewCam.farClipPlane = 1000f;
		PreviewCam.fieldOfView = ImprovedPreviewSettings.Fov;
		PreviewCam.depthTextureMode = DepthTextureMode.Depth;
		PreviewCam.cullingMask = 1 << PreviewLayer;
		PreviewCam.enabled = false;

		// Lights
		var l0GO = new GameObject("___PreviewLight0___") { hideFlags = HideFlags.HideAndDontSave };
		_light0 = l0GO.AddComponent<Light>();
		_light0.type = LightType.Directional;
		_light0.intensity = 1.2f;
		_light0.cullingMask = 1 << PreviewLayer;
		_light0.shadows = LightShadows.Soft;
		_light0.shadowStrength = 0.8f;
		_light0.enabled = false;

		var l1GO = new GameObject("___PreviewLight1___") { hideFlags = HideFlags.HideAndDontSave };
		_light1 = l1GO.AddComponent<Light>();
		_light1.type = LightType.Directional;
		_light1.intensity = 0.6f;
		_light1.cullingMask = 1 << PreviewLayer;
		_light1.shadows = LightShadows.None;
		_light1.enabled = false;

		var l2GO = new GameObject("___PreviewLight2___") { hideFlags = HideFlags.HideAndDontSave };
		_light2 = l2GO.AddComponent<Light>();
		_light2.type = LightType.Directional;
		_light2.cullingMask = 1 << PreviewLayer;
		_light2.shadows = LightShadows.None;
		_light2.enabled = false;

		// Grid
		_gridMat = GetSharedGridMaterial();
		_gridMesh = GetSharedGridMesh();

		// Gizmo sphere for light position indicators
		_gizmoSphereMesh = GetSharedGizmoSphereMesh();
		_gizmoMat = GetSharedGizmoMaterial();

		// Prefab instance at far offset
		PreviewRoot = Object.Instantiate(prefab, WorldOffset, Quaternion.identity);
		PreviewRoot.name = "___PreviewRoot___";
		PreviewRoot.hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
		SetLayerRecursive(PreviewRoot, PreviewLayer);

		// Default camera state
		_pivot = WorldOffset;
		_orbit = _targetOrbit = new Vector2(35f, 15f);
		_distance = _targetDistance = ImprovedPreviewSettings.DefaultDist;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;
		_lockCamera = false;
		_lastUpdateTime = -1;
		_lastEditorTime = -1;
		_nextPlaybackTickTime = -1d;
		_time = 0f;
		_simulatedTime = 0f;
		_needsSeek = true;
		_playing = false;
		_hasCustomLightRigDirection = false;
		ResetLightRigDirection();
		_previewDirty = true;
		_lastRenderStateHash = 0;
		_lastPreviewGuiTime = -1d;
		_lastInteractionTime = -1d;
		_focusMismatchStartTime = -1d;
		_previewHostWindow = null;
		_externalInteractionPauseRequested = false;
		_turntableActive = false;
		_useLighting = ImprovedPreviewSettings.GetDefaultLightingEnabled(PreviewTypeKey);
		_showGrid = ImprovedPreviewSettings.GetDefaultGridEnabled(PreviewTypeKey);
		_showSkybox = ImprovedPreviewSettings.GetDefaultSkyboxEnabled(PreviewTypeKey);

		// Subclass initialisation
		OnSetup(prefab);
		TryRestorePersistedState(prefabAssetPath);

		_isSetup = true;
		LogLifecycleDiagnostic(
			$"EnsureSetup complete prefab='{prefab.name}' asset='{prefabAssetPath}' " +
			$"distance={_distance:F3} targetDistance={_targetDistance:F3}");

		EditorApplication.delayCall += RequestPreviewRepaint;
	}

	public void Cleanup()
	{
		bool hadState = _isSetup || PreviewRoot != null || _currentPrefab != null;
		if (hadState)
		{
			LogLifecycleDiagnostic(
				$"Cleanup begin prefab='{_currentPrefabAssetPath ?? _currentPrefab?.name ?? "<null>"}' " +
				$"isSetup={_isSetup}");
		}

		SavePersistedStateIfEligible();

		StopCameraLerpUpdate();
		StopPlaybackUpdate();
		_playing = false;
		_turntableActive = false;
		_focusMismatchStartTime = -1d;
		_previewHostWindow = null;
		_externalInteractionPauseRequested = false;

		OnCleanup();

		if (PreviewRoot != null)
		{
			Object.DestroyImmediate(PreviewRoot);
			PreviewRoot = null;
		}

		if (PreviewCam != null)
		{
			Object.DestroyImmediate(PreviewCam.gameObject);
			PreviewCam = null;
		}

		if (_light0 != null)
		{
			Object.DestroyImmediate(_light0.gameObject);
			_light0 = null;
		}

		if (_light1 != null)
		{
			Object.DestroyImmediate(_light1.gameObject);
			_light1 = null;
		}

		if (_light2 != null)
		{
			Object.DestroyImmediate(_light2.gameObject);
			_light2 = null;
		}

		_gridMesh = null;
		_gridMat = null;
		_gizmoSphereMesh = null;
		_gizmoMat = null;

		if (_skyboxPreviewMat != null)
		{
			Object.DestroyImmediate(_skyboxPreviewMat);
			_skyboxPreviewMat = null;
		}

		_lastSkyboxTexture = null;

		if (_previewRT != null)
		{
			_previewRT.Release();
			Object.DestroyImmediate(_previewRT);
			_previewRT = null;
		}

		_lastPreviewTex = null;
		_hasCustomLightRigDirection = false;
		_isSetup = false;
		_currentPrefab = null;
		_currentPrefabAssetPath = null;
		_cachedSceneLights.Clear();
		_sceneLightCacheDirty = true;
		_previewDirty = true;
		_lastRenderStateHash = 0;
		_lastPreviewGuiTime = -1d;
		_lastInteractionTime = -1d;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;

		if (hadState)
			LogLifecycleDiagnostic("Cleanup complete");
	}

	// =====================================================================
	// In-Preview Toolbar
	// =====================================================================

	protected void DrawInPreviewToolbar(ref Rect previewRect)
	{
		const float barHeight = 40f;
		const float buttonHeight = 29f;
		const float sidePadding = 6f;
		const float buttonGap = 4f;

		var bar = new Rect(previewRect.x, previewRect.y, previewRect.width, barHeight);
		previewRect = new Rect(previewRect.x, previewRect.y + barHeight, previewRect.width, previewRect.height - barHeight);

		ImprovedEditorTheme.DrawToolbarBackground(bar);

		int buttonCount = GetSharedPreviewToolbarButtonCount();
		if (buttonCount <= 0)
			return;

		float availableWidth = bar.width - sidePadding * 2f - (buttonCount - 1) * buttonGap;
		float buttonWidth = Mathf.Max(1f, availableWidth / buttonCount);
		float x = bar.x + sidePadding;
		float y = Mathf.Round(bar.center.y - buttonHeight * 0.5f);
		DrawSharedPreviewToolbarButtons(y, buttonWidth, buttonHeight, buttonGap, ref x);
	}

	protected float GetSharedPreviewToolbarWidth(float buttonWidth, float buttonGap)
	{
		int count = GetSharedPreviewToolbarButtonCount();
		if (count == 0) return 0f;
		return count * buttonWidth + (count - 1) * buttonGap;
	}

	protected int GetSharedPreviewToolbarButtonCount()
	{
		PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();
		int count = 0;
		if (ShouldShowLightsToolbarButton() && SupportsPreviewLightRig() && pipelineContext.Capabilities.SupportsLightRig) count++;
		if (ShouldShowGridToolbarButton()) count++;
		if (ShouldShowSkyboxToolbarButton()) count++;
		return count;
	}

	protected void DrawSharedPreviewToolbarButtons(float y, float buttonWidth, float buttonHeight, float buttonGap, ref float x)
	{
		PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();
		PreviewFeatureGuardResult guard = EvaluateFeatureGuard(pipelineContext);
		DrawSharedPreviewToolbarButtonsInternal(y, buttonWidth, buttonHeight, buttonGap, ref x, pipelineContext, guard);
	}

	private void DrawSharedPreviewToolbarButtonsInternal(
		float y,
		float buttonWidth,
		float buttonHeight,
		float buttonGap,
		ref float x,
		PreviewPipelineContext pipelineContext,
		PreviewFeatureGuardResult guard)
	{
		bool showWarnings = ImprovedPreviewSettings.ShowCapabilityWarnings;

		if (ShouldShowLightsToolbarButton() && SupportsPreviewLightRig() && pipelineContext.Capabilities.SupportsLightRig)
		{
			string tooltip = "Toggle preview lights";
			if (DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.LightingEnabled, "Lights", tooltip, true, "SceneViewLighting", "d_SceneViewLighting"))
			{
				_useLighting = !_useLighting;
				RequestPreviewRepaint();
			}
			x += buttonWidth + buttonGap;
		}

		if (ShouldShowGridToolbarButton())
		{
			string reason = guard.TryGetDisabledReason(PreviewFeature.Grid3DOrientation, out string gridReason) ? gridReason : string.Empty;
			bool canToggle = !guard.Use2DCompatibilityMode || ShouldDrawGridIn2DMode();
			string tooltip = !canToggle && showWarnings && !string.IsNullOrEmpty(reason)
				? reason
				: "Toggle preview grid";
			if (DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.GridEnabled, "Grid", tooltip, canToggle, "Grid.BoxTool", "d_Grid.BoxTool"))
			{
				_showGrid = !_showGrid;
				RequestPreviewRepaint();
			}
			x += buttonWidth + buttonGap;
		}

		if (ShouldShowSkyboxToolbarButton())
		{
			string reason = guard.TryGetDisabledReason(PreviewFeature.Skybox, out string skyboxReason) ? skyboxReason : string.Empty;
			bool canToggle = pipelineContext.Capabilities.SupportsSkybox;
			string tooltip = !canToggle && showWarnings && !string.IsNullOrEmpty(reason)
				? reason
				: "Toggle preview skybox";
			if (DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.SkyboxEnabled, "Skybox", tooltip, canToggle, "PreMatSphere", "d_PreMatSphere"))
			{
				_showSkybox = !_showSkybox;
				RequestPreviewRepaint();
			}
			x += buttonWidth + buttonGap;
		}
	}

	private PreviewFeatureGuardResult EvaluateFeatureGuard(PreviewPipelineContext pipelineContext)
	{
		bool lightingRequested = _useLighting && SupportsPreviewLightRig();
		return PreviewFeatureGuard.Evaluate(
			pipelineContext,
			ShouldUse2DCompatibilityMode(),
			ShouldForce2DCompatibilityMode(),
			ShouldDrawGridIn2DMode(),
			new PreviewFeatureState(
				_showSkybox,
				reflectionRequested: true,
				_showGrid,
				lightingRequested),
			Prefer3DGridOrientationWhenNotIn2DCompatibilityMode());
	}

	// =====================================================================
	// Input
	// =====================================================================

	private void HandleInput(Rect r)
	{
		if (GUIUtility.hotControl != 0) return;
		if (_lockCamera) return;

		var e = Event.current;
		bool pointerInPreview = r.Contains(e.mousePosition);
		double now = EditorApplication.timeSinceStartup;

		bool isPanDrag = e.type == EventType.MouseDrag && (e.button == 2 || (e.button == 0 && e.command));

		if (isPanDrag)
		{
			if (!pointerInPreview)
				return;

			PanPreview(e.delta, r);
			_lastInteractionTime = now;
			OnManualCameraInteraction();
			e.Use();
			RequestPreviewRepaint();
		}

		if (e.type == EventType.MouseDown && e.button == 0 && pointerInPreview)
		{
			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionController.BeginOrbitDrag(ref interactionState, now);
			ApplyInteractionState(interactionState);
		}

		if (e.type == EventType.MouseDrag && e.button == 0)
		{
			if (!pointerInPreview)
				return;

			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionInput input = new PreviewInteractionInput(e.delta, 0f, now);
			PreviewInteractionPolicy orbitPolicy = BuildInteractionPolicy(OnManualCameraInteraction);
			PreviewInteractionController.ApplyOrbitDrag(ref interactionState, input, orbitPolicy);
			ApplyInteractionState(interactionState);
			EnsureCameraLerpRegistered();
			_lastInteractionTime = now;
			e.Use();
			RequestPreviewRepaint();
		}

		if (e.type == EventType.MouseUp && e.button == 0)
		{
			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionController.EndOrbitDrag(ref interactionState, now);
			ApplyInteractionState(interactionState);
		}

		if (e.type == EventType.ScrollWheel && pointerInPreview)
		{
			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionInput input = new PreviewInteractionInput(Vector2.zero, e.delta.y, now);
			PreviewInteractionPolicy zoomPolicy = BuildInteractionPolicy(OnManualCameraInteraction);
			PreviewInteractionController.ApplyScrollZoom(ref interactionState, input, zoomPolicy);
			ApplyInteractionState(interactionState);
			EnsureCameraLerpRegistered();
			_lastInteractionTime = now;
			e.Use();
			RequestPreviewRepaint();
		}
	}

	private void PanPreview(Vector2 delta, Rect previewRect)
	{
		if (PreviewCam == null)
			return;

		float width = Mathf.Max(1f, previewRect.width);
		float height = Mathf.Max(1f, previewRect.height);
		float fovRadians = PreviewCam.fieldOfView * Mathf.Deg2Rad;
		float verticalWorldSize = 2f * Mathf.Tan(fovRadians * 0.5f) * Mathf.Max(_distance, 0.01f);
		float horizontalWorldSize = verticalWorldSize * (width / height);

		float panRight = -delta.x / width * horizontalWorldSize;
		float panUp = delta.y / height * verticalWorldSize;
		Vector3 worldPan = PreviewCam.transform.right * panRight + PreviewCam.transform.up * panUp;

		_pivot += worldPan;
	}

	// =====================================================================
	// CameraLerpUpdate
	// =====================================================================

	private void CameraLerpUpdate()
	{
		if (PreviewRoot == null) return;
		if (!IsPreviewRecentlyVisible())
		{
			StopCameraLerpUpdate();
			return;
		}

		double now = EditorApplication.timeSinceStartup;
		if (!PreviewUpdateLoopController.TryGetDeterministicDeltaTime(ref _lastUpdateTime, now, MaxPlaybackDeltaSeconds, out float dt))
			return;

		PreviewInteractionState interactionState = BuildInteractionState();
		PreviewInteractionPolicy policy = BuildInteractionPolicy(null);
		bool moving = PreviewInteractionController.Tick(
			ref interactionState,
			dt,
			ImprovedPreviewSettings.OrbitSmooth,
			ImprovedPreviewSettings.ZoomSmooth,
			policy);
		ApplyInteractionState(interactionState);

		if (moving)
		{
			_lastInteractionTime = EditorApplication.timeSinceStartup;
			RequestPreviewRepaint();
		}
		else
		{
			StopCameraLerpUpdate();
		}
	}

	private void EnsureCameraLerpRegistered()
	{
		PreviewUpdateLoopController.Start(ref _cameraLerpRegistered, ref _lastUpdateTime, CameraLerpUpdate);
	}

	private bool HasPendingCameraMotion()
	{
		PreviewInteractionState interactionState = BuildInteractionState();
		PreviewInteractionPolicy policy = BuildInteractionPolicy(null);
		return PreviewInteractionController.HasPendingMotion(interactionState, policy);
	}

	private void StopCameraLerpUpdate()
	{
		PreviewUpdateLoopController.Stop(ref _cameraLerpRegistered, ref _lastUpdateTime, CameraLerpUpdate);
	}

	private void EnsurePlaybackUpdateRegistered()
	{
		if (_playbackUpdateRegistered)
			return;

		_nextPlaybackTickTime = -1d;
		_focusMismatchStartTime = -1d;
		_externalInteractionPauseRequested = false;
		PreviewUpdateLoopController.Start(ref _playbackUpdateRegistered, ref _lastEditorTime, EditorUpdate);
	}

	private void StopPlaybackUpdate()
	{
		if (!_playbackUpdateRegistered)
			return;

		PreviewUpdateLoopController.Stop(ref _playbackUpdateRegistered, ref _lastEditorTime, EditorUpdate);
		_nextPlaybackTickTime = -1d;
		_focusMismatchStartTime = -1d;
		_externalInteractionPauseRequested = false;
	}

	private PreviewInteractionState BuildInteractionState()
	{
		return new PreviewInteractionState
		{
			Orbit = _orbit,
			TargetOrbit = _targetOrbit,
			AngularVelocity = _orbitAngularVelocity,
			Distance = _distance,
			TargetDistance = _targetDistance,
			IsOrbitDragging = _isOrbitDragging,
			LastOrbitInputTime = _lastOrbitInputTime
		};
	}

	private void ApplyInteractionState(in PreviewInteractionState state)
	{
		_orbit = state.Orbit;
		_targetOrbit = state.TargetOrbit;
		_orbitAngularVelocity = state.AngularVelocity;
		_distance = state.Distance;
		_targetDistance = state.TargetDistance;
		_isOrbitDragging = state.IsOrbitDragging;
		_lastOrbitInputTime = state.LastOrbitInputTime;
	}

	private PreviewInteractionPolicy BuildInteractionPolicy(System.Action onManualInteraction)
	{
		return new PreviewInteractionPolicy(
			canAdjustPitch: () => !IsTwoDimensionalRendererCompatibilityModeActive(),
			onManualInteraction: onManualInteraction,
			pitchMin: -89f,
			pitchMax: 89f,
			minDistance: 0.05f,
			maxDistance: 500f,
			zoomScrollFactor: 0.03f);
	}

	private void TrackPreviewHostWindow()
	{
		if (_previewHostWindow != null)
			return;

		// Prefer mouseOverWindow: OnPreviewGUI is invoked by the window that owns the
		// preview panel (typically the Inspector). mouseOverWindow reliably identifies
		// that window even when focusedWindow still points at the Project window — which
		// happens when the user just clicked a prefab to select it there.
		EditorWindow hostWindow = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
		if (hostWindow != null)
			_previewHostWindow = hostWindow;
	}

	/// <summary>
	/// Returns true when the user is actively engaged with the preview host window.
	/// Used to gate continuous repaint scheduling for turntable and camera-lerp so the
	/// 3D render cost does not stall input in other windows (e.g. Project Settings sliders).
	///
	/// A 0.5 s grace period after the last preview interaction keeps the loop alive during
	/// orbit/zoom/pan drags — Unity's mouseOverWindow API can transiently return null or
	/// point at the wrong window mid-gesture, which previously caused false breaks.
	/// </summary>
	private bool IsUserEngagingWithPreviewHost()
	{
		if (_previewHostWindow == null)
			return true; // host not yet identified — allow repaints

		// Keep rendering for 500 ms after the last interaction so orbit drags never
		// falsely interrupt the repaint loop mid-gesture.
		const double RecentInteractionGraceSeconds = 0.5d;
		double now = EditorApplication.timeSinceStartup;
		if (_lastInteractionTime >= 0d && (now - _lastInteractionTime) <= RecentInteractionGraceSeconds)
			return true;

		EditorWindow focused = EditorWindow.focusedWindow;
		EditorWindow hovered = EditorWindow.mouseOverWindow;
		return focused == _previewHostWindow || hovered == _previewHostWindow;
	}

	private bool ShouldPausePlaybackForExternalFocus(double now)
	{
		if (!ShouldAutoPausePlaybackOnExternalInteraction())
			return false;
		if (_previewHostWindow == null)
			return false;

		EditorWindow focusedWindow = EditorWindow.focusedWindow;
		EditorWindow hoveredWindow = EditorWindow.mouseOverWindow;

		// Consider the user "on host" when either the keyboard-focused window or the
		// mouse-hovered window matches the preview host. Checking hoveredWindow covers
		// orbit drags: during a drag the focused window may not follow the mouse, but
		// the pointer stays over the Inspector (the preview host) the entire time.
		bool onHost = (focusedWindow == _previewHostWindow)
		           || (hoveredWindow != null && hoveredWindow == _previewHostWindow);

		if (onHost)
		{
			_focusMismatchStartTime = -1d;
			return false;
		}

		if (_focusMismatchStartTime < 0d)
		{
			_focusMismatchStartTime = now;
			return false;
		}

		return now - _focusMismatchStartTime >= ExternalFocusPauseDebounceSeconds;
	}

	// =====================================================================
	// EditorUpdate
	// =====================================================================

	private void EditorUpdate()
	{
		if (PreviewRoot == null)
		{
			StopPlayback();
			return;
		}

		double now = EditorApplication.timeSinceStartup;

		if (!IsPreviewRecentlyVisible())
		{
			if (_playing)
			{
				// Advance simulation silently in the background — no Inspector repaint.
				// The repaint loop is driven by OnPreviewGUI when the Inspector is visible.
				// This lets playback continue while the user is in other Unity windows
				// without starving their input with continuous 3D renders.
				double bgTickInterval = 1d / Mathf.Max(1, ImprovedPreviewSettings.PreviewRefreshUpdatesPerSecond);
				if (_nextPlaybackTickTime < 0d)
					_nextPlaybackTickTime = now;
				if (now >= _nextPlaybackTickTime)
				{
					if (_lastEditorTime < 0) _lastEditorTime = now;
					float bgDt = Mathf.Clamp((float)(now - _lastEditorTime), 0f, MaxPlaybackDeltaSeconds);
					_lastEditorTime = now;
					_nextPlaybackTickTime = now + bgTickInterval;
					_time += bgDt;
					OnSimulate(bgDt);
					_previewDirty = true; // mark dirty so next render reflects latest sim state
				}
				return;
			}
			StopPlaybackUpdate();
			return;
		}
		if (_playing && ShouldAutoPausePlaybackOnExternalInteraction() && _previewHostWindow != null)
		{
			EditorWindow hoveredWindow = EditorWindow.mouseOverWindow;
			bool isExternalMouseInteraction = hoveredWindow != null &&
				hoveredWindow != _previewHostWindow &&
				(Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2));
			if (isExternalMouseInteraction)
				_externalInteractionPauseRequested = true;
		}

		if (_playing && _externalInteractionPauseRequested)
		{
			_externalInteractionPauseRequested = false;
			StopPlayback();
			RequestPreviewRepaint();
			return;
		}

		if (_playing && ShouldPausePlaybackForExternalFocus(now))
		{
			StopPlayback();
			RequestPreviewRepaint();
			return;
		}

		if (_lastEditorTime < 0)
		{
			_lastEditorTime = now;
			_nextPlaybackTickTime = now;
			return;
		}

		double tickInterval = 1d / Mathf.Max(1, ImprovedPreviewSettings.PreviewRefreshUpdatesPerSecond);
		if (_nextPlaybackTickTime < 0d)
			_nextPlaybackTickTime = now;
		if (now < _nextPlaybackTickTime)
			return;

		float dt = Mathf.Clamp((float)(now - _lastEditorTime), 0f, MaxPlaybackDeltaSeconds);
		_lastEditorTime = now;
		_nextPlaybackTickTime = now + tickInterval;

		_time += dt;
		OnSimulate(dt);
		_lastInteractionTime = now;
		_previewDirty = true; // repaint loop is driven by OnPreviewGUI, not here
	}

	// =====================================================================
	// Render
	// =====================================================================

	private void RenderPreview(Rect r, bool useInteractiveQuality, int renderStateHash)
	{
		if (PreviewCam == null || PreviewRoot == null) return;
		if (EditorApplication.isPlayingOrWillChangePlaymode) return;

		float pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
		int w = Mathf.Max(1, Mathf.RoundToInt(r.width * pixelsPerPoint));
		int h = Mathf.Max(1, Mathf.RoundToInt(r.height * pixelsPerPoint));

		float scale = GetActiveResolutionScale(useInteractiveQuality);
		int rtW = Mathf.Max(1, Mathf.RoundToInt(w * scale));
		int rtH = Mathf.Max(1, Mathf.RoundToInt(h * scale));
		int aa = GetActiveAntiAliasing(useInteractiveQuality);

		if (_previewRT == null || _rtW != rtW || _rtH != rtH || _previewRT.antiAliasing != aa)
		{
			if (_previewRT != null)
			{
				_previewRT.Release();
				Object.DestroyImmediate(_previewRT);
			}

			_previewRT = new RenderTexture(rtW, rtH, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = aa
			};
			_previewRT.Create();

			_rtW = rtW;
			_rtH = rtH;
		}

		bool renderAtOrigin = RenderPreviewAtOrigin();
		Vector3 renderPivot = renderAtOrigin ? (_pivot - WorldOffset) : _pivot;
		Vector3 surfaceOrigin = renderAtOrigin ? Vector3.zero : WorldOffset;
		PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();
		bool supportsPreviewLightRig = SupportsPreviewLightRig();
		PreviewFeatureGuardResult guard = EvaluateFeatureGuard(pipelineContext);
		IPreviewRenderStrategy strategy = PreviewRenderStrategyFactory.GetStrategy(pipelineContext.Kind);

		_bgColor = ImprovedPreviewSettings.BgColor;
		Color? bgOverride = GetRenderBackgroundColorOverride();
		if (bgOverride.HasValue)
			_bgColor = bgOverride.Value;
		strategy.ConfigureCamera(
			PreviewCam,
			new PreviewCameraRenderInput(
				(float)w / h,
				ImprovedPreviewSettings.Fov,
				_distance,
				_orbit,
				renderPivot,
				_bgColor,
				guard.Use2DCompatibilityMode));

		Material skyboxMat = null;
		bool skyboxReady = guard.SkyboxEnabled && TryGetRenderableSkyboxMaterial(RenderSettings.skybox, out skyboxMat);
		bool forceGridFallback = _showSkybox && !skyboxReady && !guard.Use2DCompatibilityMode;
		PreviewCam.clearFlags = skyboxReady ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;

		EnsureLightRigInitialized();
		Quaternion lightRigRotation = Quaternion.FromToRotation(GetDefaultLightRigDirection(), _lightRigDirectionWorld);
		Vector3 keyPos = renderPivot + lightRigRotation * ImprovedPreviewSettings.KeyPosition;
		Vector3 fillPos = renderPivot + lightRigRotation * ImprovedPreviewSettings.FillPosition;

		_light0.intensity = guard.LightingEnabled ? ImprovedPreviewSettings.KeyIntensity : 0f;
		_light1.intensity = guard.LightingEnabled ? ImprovedPreviewSettings.FillIntensity : 0f;
		_light2.intensity = guard.LightingEnabled && ImprovedPreviewSettings.RimLightEnabled ? ImprovedPreviewSettings.RimLightIntensity : 0f;
		_light2.color = ImprovedPreviewSettings.RimLightColor;

		_light0.transform.position = keyPos;
		_light1.transform.position = fillPos;
		_light2.transform.rotation = Quaternion.Euler(ImprovedPreviewSettings.RimLightRotation.x, ImprovedPreviewSettings.RimLightRotation.y, 0f);

		if ((renderPivot - keyPos).sqrMagnitude > 0.0001f)
			_light0.transform.LookAt(renderPivot);

		if ((renderPivot - fillPos).sqrMagnitude > 0.0001f)
			_light1.transform.LookAt(renderPivot);

		if (renderAtOrigin)
			PreviewRoot.transform.position -= WorldOffset;

		RebuildGridIfNeeded();

		bool shouldDrawGrid = guard.GridEnabled || forceGridFallback;
		if (shouldDrawGrid && _gridMesh != null && _gridMat != null)
		{
			Matrix4x4 gridMatrix = guard.ShouldDrawGridAs3D
				? Matrix4x4.TRS(surfaceOrigin + new Vector3(0f, -0.001f, 0f), Quaternion.identity, Vector3.one)
				: Matrix4x4.TRS(surfaceOrigin, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
			Graphics.DrawMesh(_gridMesh, gridMatrix, _gridMat, PreviewLayer, PreviewCam);
		}

		if (supportsPreviewLightRig && pipelineContext.Capabilities.SupportsLightRig && ImprovedPreviewSettings.ShowLightGizmos && _gizmoSphereMesh != null && _gizmoMat != null)
		{
			DrawGizmoSphere(keyPos, new Color(1f, 0.9f, 0.5f, 0.4f), PreviewCam, ref _gizmoKeyColors, ref _lastGizmoKeyColor);
			DrawGizmoSphere(fillPos, new Color(0.5f, 0.8f, 1f, 0.4f), PreviewCam, ref _gizmoFillColors, ref _lastGizmoFillColor);
		}

		_light0.enabled = guard.LightingEnabled && _light0.intensity > 0f;
		_light1.enabled = guard.LightingEnabled && _light1.intensity > 0f;
		_light2.enabled = _light2.intensity > 0f;

		GetSceneLights(_cachedSceneLights);
		foreach (var l in _cachedSceneLights)
			l.enabled = false;

		Vector4 prevTime = Shader.GetGlobalVector("_Time");
		Vector4 prevSinTime = Shader.GetGlobalVector("_SinTime");
		Vector4 prevCosTime = Shader.GetGlobalVector("_CosTime");
		Vector4 prevDeltaTime = Shader.GetGlobalVector("unity_DeltaTime");

		using var renderSettingsScope = PreviewEnvironmentRenderer.BeginRenderSettingsScope(
			guard.Use2DCompatibilityMode,
			guard.LightingEnabled,
			ImprovedPreviewSettings.AmbientColor,
			guard.ReflectionEnabled,
			ImprovedPreviewSettings.ReflectionCubemap,
			skyboxReady ? skyboxMat : null);

		try
		{
			float shaderTime = Mathf.Max(0f, _time);
			Shader.SetGlobalVector("_Time", new Vector4(shaderTime / 20f, shaderTime, shaderTime * 2f, shaderTime * 3f));
			Shader.SetGlobalVector("_SinTime", new Vector4(Mathf.Sin(shaderTime / 8f), Mathf.Sin(shaderTime / 4f), Mathf.Sin(shaderTime / 2f), Mathf.Sin(shaderTime)));
			Shader.SetGlobalVector("_CosTime", new Vector4(Mathf.Cos(shaderTime / 8f), Mathf.Cos(shaderTime / 4f), Mathf.Cos(shaderTime / 2f), Mathf.Cos(shaderTime)));
			Shader.SetGlobalVector("unity_DeltaTime", new Vector4(0.016f, 1f / 0.016f, 0f, 0f));

			OnBeforeRender();

			OnPreviewRenderPrepared(_previewRT);
			strategy.Render(this, PreviewCam, _previewRT, _bgColor, skyboxReady);
		}
		finally
		{
			OnAfterRender();
			Shader.SetGlobalVector("_Time", prevTime);
			Shader.SetGlobalVector("_SinTime", prevSinTime);
			Shader.SetGlobalVector("_CosTime", prevCosTime);
			Shader.SetGlobalVector("unity_DeltaTime", prevDeltaTime);

			_light0.enabled = false;
			_light1.enabled = false;
			_light2.enabled = false;

			foreach (var l in _cachedSceneLights)
				l.enabled = true;

			if (renderAtOrigin && PreviewRoot != null)
				PreviewRoot.transform.position += WorldOffset;
		}

		_lastPreviewTex = _previewRT;
		_lastRenderStateHash = renderStateHash;
		_previewDirty = false;
	}

	// =====================================================================
	// Grid
	// =====================================================================

	private static Material CreateGridMaterial()
	{
		var mat = new Material(Shader.Find("Hidden/Internal-Colored"))
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		mat.SetInt("_ZWrite", 0);
		mat.SetInt("_Cull", 0);
		mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		// Render just before sprites (queue 3000) so sprites always paint over the grid in 2D mode
		mat.renderQueue = 2999;
		return mat;
	}

	private static Material GetSharedGridMaterial()
	{
		if (s_sharedGridMaterial == null)
			s_sharedGridMaterial = CreateGridMaterial();
		return s_sharedGridMaterial;
	}

	private static Mesh GetSharedGridMesh()
	{
		if (s_sharedGridMesh == null)
			s_sharedGridMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
		return s_sharedGridMesh;
	}

	private static Mesh GetSharedGizmoSphereMesh()
	{
		if (s_sharedGizmoSphereMesh == null)
			s_sharedGizmoSphereMesh = CreateSphereMesh(0.12f, 10);
		return s_sharedGizmoSphereMesh;
	}

	private static Material GetSharedGizmoMaterial()
	{
		if (s_sharedGizmoMaterial == null)
			s_sharedGizmoMaterial = CreateGizmoMaterial();
		return s_sharedGizmoMaterial;
	}

	// =====================================================================
	// Helpers
	// =====================================================================

	private void RegisterSceneLightCacheInvalidation()
	{
		EditorApplication.hierarchyChanged += InvalidateSceneLightCache;
		EditorSceneManager.sceneOpened += OnSceneOpened;
		SceneManager.activeSceneChanged += OnActiveSceneChanged;
	}

	private void UnregisterSceneLightCacheInvalidation()
	{
		EditorApplication.hierarchyChanged -= InvalidateSceneLightCache;
		EditorSceneManager.sceneOpened -= OnSceneOpened;
		SceneManager.activeSceneChanged -= OnActiveSceneChanged;
	}

	private void InvalidateSceneLightCache() => _sceneLightCacheDirty = true;
	private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) => _sceneLightCacheDirty = true;
	private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene prev, UnityEngine.SceneManagement.Scene next) => _sceneLightCacheDirty = true;

	private void GetSceneLights(List<Light> result)
	{
		if (_sceneLightCacheDirty)
		{
			result.Clear();
			var all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
			foreach (var l in all)
			{
				if (l == _light0 || l == _light1 || l == _light2) continue;
				if (l.enabled) result.Add(l);
			}
			_sceneLightCacheDirty = false;
		}
	}

	private static void SetLayerRecursive(GameObject go, int layer)
	{
		go.layer = layer;
		foreach (Transform child in go.transform)
			SetLayerRecursive(child.gameObject, layer);
	}

	private void EnsureLightRigInitialized()
	{
		if (_hasCustomLightRigDirection)
			return;

		_lightRigDirectionWorld = GetDefaultLightRigDirection();
		_hasCustomLightRigDirection = true;
	}

	private static Vector3 GetDefaultLightRigDirection()
	{
		Vector3 key = ImprovedPreviewSettings.KeyPosition;
		return key.sqrMagnitude > 0.0001f
			? key.normalized
			: new Vector3(-0.35f, 0.55f, -0.75f).normalized;
	}

	private void RebuildGridIfNeeded()
	{
		GetEffectiveGridSettings(out float hs, out float step, out float alpha);

		if (_gridMesh == null)
			_gridMesh = GetSharedGridMesh();

		if (Mathf.Approximately(hs, s_sharedGridHalfSize) &&
		    Mathf.Approximately(step, s_sharedGridStep) &&
		    Mathf.Approximately(alpha, s_sharedGridAlpha))
			return;

		s_sharedGridHalfSize = hs;
		s_sharedGridStep = step;
		s_sharedGridAlpha = alpha;

		PreviewGridMeshBuilder.BuildGridMesh(_gridMesh, hs, step, alpha);
	}

	private void GetEffectiveGridSettings(out float halfSize, out float step, out float alpha)
	{
		halfSize = ImprovedPreviewSettings.GridHalfSize;
		step = ImprovedPreviewSettings.GridStep;
		alpha = ImprovedPreviewSettings.GridAlpha;
	}

	private bool IsPreviewRecentlyVisible()
	{
		return _lastPreviewGuiTime >= 0d
			&& (EditorApplication.timeSinceStartup - _lastPreviewGuiTime) <= PreviewIdleSuspendSeconds;
	}

	private bool ShouldUseInteractivePreviewQuality()
	{
		double now = EditorApplication.timeSinceStartup;
		return _playing
			|| _cameraLerpRegistered
			|| HasPendingCameraMotion()
			|| _turntableActive
			|| (_lastInteractionTime >= 0d && (now - _lastInteractionTime) <= InteractiveQualityHoldSeconds);
	}

	private float GetActiveResolutionScale(bool useInteractiveQuality)
	{
		float configuredScale = Mathf.Clamp(
			ImprovedPreviewSettings.ResolutionScale,
			ImprovedPreviewSettings.MinPreviewResolutionScale,
			ImprovedPreviewSettings.MaxPreviewResolutionScale);
		return useInteractiveQuality ? Mathf.Min(configuredScale, InteractiveResolutionScale) : configuredScale;
	}

	private int GetActiveAntiAliasing(bool useInteractiveQuality)
	{
		PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();
		if (pipelineContext.Kind != PreviewRenderPipelineKind.BuiltIn)
			return 1;

		int configuredAa = Mathf.Max(1, ImprovedPreviewSettings.AntiAliasing);
		return useInteractiveQuality ? InteractiveAntiAliasing : configuredAa;
	}

	private int ComputeRenderStateHash(Rect r, bool useInteractiveQuality)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + Mathf.RoundToInt(r.width);
			hash = hash * 31 + Mathf.RoundToInt(r.height);
			hash = hash * 31 + Mathf.RoundToInt(Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint) * 100f);
			hash = hash * 31 + (_currentPrefab != null ? _currentPrefab.GetInstanceID() : 0);
			hash = hash * 31 + (SupportsPreviewLightRig() ? 1 : 0);
			hash = hash * 31 + (_useLighting ? 1 : 0);
			hash = hash * 31 + (_showGrid ? 1 : 0);
			hash = hash * 31 + (_showSkybox ? 1 : 0);
			hash = hash * 31 + (useInteractiveQuality ? 1 : 0);
			hash = hash * 31 + ColorToHash(ImprovedPreviewSettings.BgColor);
			hash = hash * 31 + ColorToHash(ImprovedPreviewSettings.AmbientColor);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.Fov * 100f);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.GridHalfSize * 100f);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.GridStep * 100f);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.GridAlpha * 1000f);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.KeyIntensity * 100f);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.FillIntensity * 100f);
			hash = hash * 31 + Vector3ToHash(ImprovedPreviewSettings.KeyPosition);
			hash = hash * 31 + Vector3ToHash(ImprovedPreviewSettings.FillPosition);
			hash = hash * 31 + (ImprovedPreviewSettings.RimLightEnabled ? 1 : 0);
			hash = hash * 31 + Mathf.RoundToInt(ImprovedPreviewSettings.RimLightIntensity * 100f);
			hash = hash * 31 + Vector2ToHash(ImprovedPreviewSettings.RimLightRotation);
			hash = hash * 31 + ColorToHash(ImprovedPreviewSettings.RimLightColor);
			hash = hash * 31 + (ImprovedPreviewSettings.ShowLightGizmos ? 1 : 0);
			hash = hash * 31 + Mathf.RoundToInt(_orbit.x * 100f);
			hash = hash * 31 + Mathf.RoundToInt(_orbit.y * 100f);
			hash = hash * 31 + Mathf.RoundToInt(_distance * 1000f);
			hash = hash * 31 + ImprovedPreviewSettings.AppliedRevision;
			Texture skyboxTexture = ImprovedPreviewSettings.SkyboxTexture;
			hash = hash * 31 + (skyboxTexture != null ? skyboxTexture.GetInstanceID() : 0);
			Cubemap reflectionCubemap = ImprovedPreviewSettings.ReflectionCubemap;
			hash = hash * 31 + (reflectionCubemap != null ? reflectionCubemap.GetInstanceID() : 0);
			hash = hash * 31 + (IsTwoDimensionalRendererCompatibilityModeActive() ? 1 : 0);
			hash = hash * 31 + GetActiveAntiAliasing(useInteractiveQuality);
			hash = hash * 31 + Mathf.RoundToInt(GetActiveResolutionScale(useInteractiveQuality) * 100f);
			return hash;
		}
	}

	private static int Vector3ToHash(Vector3 value)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + Mathf.RoundToInt(value.x * 1000f);
			hash = hash * 31 + Mathf.RoundToInt(value.y * 1000f);
			hash = hash * 31 + Mathf.RoundToInt(value.z * 1000f);
			return hash;
		}
	}

	private static int Vector2ToHash(Vector2 value)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + Mathf.RoundToInt(value.x * 1000f);
			hash = hash * 31 + Mathf.RoundToInt(value.y * 1000f);
			return hash;
		}
	}

	private static int ColorToHash(Color color)
	{
		unchecked
		{
			int hash = 17;
			hash = hash * 31 + Mathf.RoundToInt(color.r * 255f);
			hash = hash * 31 + Mathf.RoundToInt(color.g * 255f);
			hash = hash * 31 + Mathf.RoundToInt(color.b * 255f);
			hash = hash * 31 + Mathf.RoundToInt(color.a * 255f);
			return hash;
		}
	}

	private bool TryGetRenderableSkyboxMaterial(Material fallbackSkybox, out Material skyboxMat)
	{
		skyboxMat = null;
		if (!_showSkybox)
		{
			LogDiagnosticOnce("skybox-disabled", "Skybox rendering disabled by toolbar toggle.");
			return false;
		}

		if (IsTwoDimensionalRendererCompatibilityModeActive())
		{
			string renderPipelineLabel = PreviewPipelineContextResolver.GetCurrentContext().Label;
			LogDiagnosticOnce(
				$"skybox-2d-renderer:{renderPipelineLabel}",
				$"2D renderer compatibility mode active for '{renderPipelineLabel}'. Disabling preview skybox path.");
			return false;
		}

		Texture skyboxTexture = ImprovedPreviewSettings.SkyboxTexture;
		if (skyboxTexture != null)
		{
			if (skyboxTexture != _lastSkyboxTexture)
			{
				if (_skyboxPreviewMat != null)
					Object.DestroyImmediate(_skyboxPreviewMat);

				_skyboxPreviewMat = CreateSkyboxMaterialFromTexture(skyboxTexture);
				_lastSkyboxTexture = skyboxTexture;
				ImprovedPreviewSettings.LogDiagnostic($"Created preview skybox material from texture '{skyboxTexture.name}' ({skyboxTexture.GetType().Name}).");
			}

			if (IsSkyboxMaterialRenderable(_skyboxPreviewMat))
			{
				skyboxMat = _skyboxPreviewMat;
				return true;
			}

			LogDiagnosticOnce($"skybox-unrenderable:{skyboxTexture.GetInstanceID()}", $"Skybox texture '{skyboxTexture.name}' could not produce a renderable skybox material.");
		}

		if (IsSkyboxMaterialRenderable(fallbackSkybox))
		{
			ImprovedPreviewSettings.LogDiagnostic($"Falling back to existing RenderSettings.skybox '{fallbackSkybox.name}'.");
			skyboxMat = fallbackSkybox;
			return true;
		}

		LogDiagnosticOnce("skybox-missing", "No renderable skybox material was available. Preview will use solid background and grid fallback.");
		return false;
	}

	private static bool IsSkyboxMaterialRenderable(Material material)
	{
		return material != null
			&& material.shader != null
			&& material.shader.isSupported
			&& material.passCount > 0;
	}

	private static Material CreateSkyboxMaterialFromTexture(Texture texture)
	{
		if (texture == null)
			return null;

		Shader shader;
		Material material;

		if (texture is Cubemap cubemap)
		{
			shader = Shader.Find("Skybox/Cubemap");
			if (shader == null || !shader.isSupported)
				return null;

			material = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			material.SetTexture("_Tex", cubemap);
			return material;
		}

		shader = Shader.Find("Skybox/Panoramic");
		if (shader == null || !shader.isSupported)
			return null;

		material = new Material(shader)
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		material.SetTexture("_MainTex", texture);
		return material;
	}

	private static void LogDiagnosticOnce(string key, string message)
	{
		if (!ImprovedPreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
			return;

		if (!s_loggedDiagnostics.Add(key))
			return;

		ImprovedPreviewSettings.LogDiagnostic(message);
	}

	private void LogLifecycleDiagnostic(string message)
	{
		if (!ImprovedPreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(message))
			return;

		ImprovedPreviewSettings.LogDiagnostic($"[{GetType().Name}] {message}");
	}

	private void DrawGizmoSphere(Vector3 worldPos, Color color, Camera cam, ref Color[] cachedColors, ref Color lastColor)
	{
		if (_gizmoSphereMesh == null || _gizmoMat == null) return;

		int vertCount = _gizmoSphereMesh.vertexCount;
		if (cachedColors == null || cachedColors.Length != vertCount || lastColor != color)
		{
			cachedColors = new Color[vertCount];
			for (int i = 0; i < vertCount; i++)
				cachedColors[i] = color;
			lastColor = color;
			_gizmoSphereMesh.colors = cachedColors;
		}

		Graphics.DrawMesh(
			_gizmoSphereMesh,
			Matrix4x4.TRS(worldPos, Quaternion.identity, Vector3.one),
			_gizmoMat,
			PreviewLayer,
			cam);
	}

	private static Mesh CreateSphereMesh(float radius, int segments)
	{
		var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
		var verts = new List<Vector3>();
		var tris = new List<int>();

		int rings = segments;
		int slices = segments * 2;

		for (int r = 0; r <= rings; r++)
		{
			float phi = Mathf.PI * r / rings;
			for (int s = 0; s <= slices; s++)
			{
				float theta = 2f * Mathf.PI * s / slices;
				verts.Add(new Vector3(
					radius * Mathf.Sin(phi) * Mathf.Cos(theta),
					radius * Mathf.Cos(phi),
					radius * Mathf.Sin(phi) * Mathf.Sin(theta)));
			}
		}

		for (int r = 0; r < rings; r++)
		{
			for (int s = 0; s < slices; s++)
			{
				int a = r * (slices + 1) + s;
				int b = a + slices + 1;
				tris.Add(a); tris.Add(b); tris.Add(a + 1);
				tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
			}
		}

		mesh.SetVertices(verts);
		mesh.SetTriangles(tris, 0);
		mesh.RecalculateNormals();
		return mesh;
	}

	private static Material CreateGizmoMaterial()
	{
		var mat = new Material(Shader.Find("Hidden/Internal-Colored"))
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		mat.SetInt("_ZWrite", 0);
		mat.SetInt("_Cull", 0);
		mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
		mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		mat.renderQueue = 3000;
		return mat;
	}

	protected static bool DrawPreviewToolbarButton(Rect rect, bool active, string fallbackText, string tooltip, params string[] iconNames)
	{
		return DrawPreviewToolbarButton(rect, active, fallbackText, tooltip, true, iconNames);
	}

	protected static bool DrawPreviewToolbarButton(Rect rect, bool active, string fallbackText, string tooltip, bool isEnabled, params string[] iconNames)
	{
		bool clicked = isEnabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
		if (clicked)
			GUI.changed = true;

		GUIContent content = GetIconContent(fallbackText, tooltip, iconNames);
		bool hovered = rect.Contains(Event.current.mousePosition);

		Color bg = ImprovedEditorTheme.GetToolbarButtonBackground(active, hovered);
		Color border = ImprovedEditorTheme.GetToolbarButtonBorder(active);
		if (!isEnabled)
		{
			bg *= new Color(1f, 1f, 1f, 0.5f);
			border *= new Color(1f, 1f, 1f, 0.65f);
		}

		EditorGUI.DrawRect(rect, bg);
		EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
		EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

		if (content.image != null)
		{
			float iconSize = Mathf.Min(rect.width, rect.height) - 8f;
			Rect iconRect = new Rect(
				Mathf.Round(rect.center.x - iconSize * 0.5f),
				Mathf.Round(rect.center.y - iconSize * 0.5f),
				iconSize,
				iconSize);
			Color previousColor = GUI.color;
			Color tint = ImprovedEditorTheme.GetToolbarIconTint(active);
			if (!isEnabled)
				tint *= new Color(1f, 1f, 1f, 0.45f);
			GUI.color = tint;
			GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
			GUI.color = previousColor;
		}
		else
		{
			Color previousColor = GUI.color;
			if (!isEnabled)
				GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * 0.55f);
			GUI.Label(rect, content.text, EditorStyles.miniLabel);
			GUI.color = previousColor;
		}

		if (!string.IsNullOrEmpty(content.tooltip) && rect.Contains(Event.current.mousePosition))
			GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));

		return clicked;
	}

	/// <summary>
	/// Draws a split toolbar button: left side is an icon toggle, right side is a dropdown arrow.
	/// Returns 1 if the icon (left) area was clicked, 2 if the dropdown (right) area was clicked, 0 otherwise.
	/// </summary>
	protected static int DrawPreviewToolbarSplitButton(Rect rect, bool active, bool tintIcon, string fallbackText, string tooltip, params string[] iconNames)
	{
		const float arrowZoneWidth = 16f;
		const float dividerWidth = 1f;

		Rect iconZone = new Rect(rect.x, rect.y, rect.width - arrowZoneWidth, rect.height);
		Rect arrowZone = new Rect(rect.xMax - arrowZoneWidth, rect.y, arrowZoneWidth, rect.height);

		int iconControlId = GUIUtility.GetControlID("SplitBtnIcon".GetHashCode(), FocusType.Passive, iconZone);
		int arrowControlId = GUIUtility.GetControlID("SplitBtnArrow".GetHashCode(), FocusType.Passive, arrowZone);

		int result = 0;
		Event evt = Event.current;
		if (evt.type == EventType.MouseDown && evt.button == 0)
		{
			if (arrowZone.Contains(evt.mousePosition))
			{
				GUIUtility.hotControl = arrowControlId;
				evt.Use();
				result = 2;
			}
			else if (iconZone.Contains(evt.mousePosition))
			{
				GUIUtility.hotControl = iconControlId;
				evt.Use();
				result = 1;
			}
		}
		else if (evt.type == EventType.MouseUp && evt.button == 0)
		{
			if (GUIUtility.hotControl == iconControlId || GUIUtility.hotControl == arrowControlId)
				GUIUtility.hotControl = 0;
		}

		GUIContent content = GetIconContent(fallbackText, tooltip, iconNames);
		bool iconHovered = iconZone.Contains(evt.mousePosition);
		bool arrowHovered = arrowZone.Contains(evt.mousePosition);

		// Background — icon zone (reflects active state)
		Color iconBg = ImprovedEditorTheme.GetToolbarButtonBackground(active, iconHovered);
		EditorGUI.DrawRect(iconZone, iconBg);

		// Background — arrow zone (never shows active state)
		Color arrowBg = ImprovedEditorTheme.GetToolbarButtonBackground(false, arrowHovered);
		EditorGUI.DrawRect(arrowZone, arrowBg);

		// Borders — icon zone uses active border, arrow zone always inactive
		Color iconBorder = ImprovedEditorTheme.GetToolbarButtonBorder(active);
		Color arrowBorder = ImprovedEditorTheme.GetToolbarButtonBorder(false);
		EditorGUI.DrawRect(new Rect(iconZone.x, iconZone.yMax - 1f, iconZone.width, 1f), iconBorder);
		EditorGUI.DrawRect(new Rect(arrowZone.x, arrowZone.yMax - 1f, arrowZoneWidth, 1f), arrowBorder);
		EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), arrowBorder);

		// Divider between icon and arrow
		EditorGUI.DrawRect(new Rect(arrowZone.x, rect.y + 4f, dividerWidth, rect.height - 8f), arrowBorder);

		// Icon or label
		if (content.image != null)
		{
			float iconSize = Mathf.Min(iconZone.width, iconZone.height) - 8f;
			Rect iconRect = new Rect(
				Mathf.Round(iconZone.center.x - iconSize * 0.5f),
				Mathf.Round(iconZone.center.y - iconSize * 0.5f),
				iconSize,
				iconSize);
			if (tintIcon)
			{
				Color previousColor = GUI.color;
				GUI.color = ImprovedEditorTheme.GetToolbarIconTint(active);
				GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
				GUI.color = previousColor;
			}
			else
			{
				GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
			}
		}
		else
		{
			GUI.Label(iconZone, content.text, EditorStyles.miniLabel);
		}

		// Dropdown arrow chevron
		Color arrowColor = ImprovedEditorTheme.GetToolbarIconTint(false);
		float ax = Mathf.Round(arrowZone.center.x - 3.5f);
		float ay = Mathf.Round(arrowZone.center.y - 1f);
		EditorGUI.DrawRect(new Rect(ax, ay - 1f, 7f, 1f), arrowColor);
		EditorGUI.DrawRect(new Rect(ax + 1f, ay, 5f, 1f), arrowColor);
		EditorGUI.DrawRect(new Rect(ax + 2f, ay + 1f, 3f, 1f), arrowColor);
		EditorGUI.DrawRect(new Rect(ax + 3f, ay + 2f, 1f, 1f), arrowColor);

		if (!string.IsNullOrEmpty(content.tooltip) && rect.Contains(evt.mousePosition))
			GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));

		return result;
	}

	private static readonly Dictionary<string, Texture> s_iconCache = new();

	protected static GUIContent GetIconContent(string fallbackText, string tooltip, params string[] iconNames)
	{
		for (int i = 0; i < iconNames.Length; i++)
		{
			if (!s_iconCache.TryGetValue(iconNames[i], out Texture icon))
			{
				GUIContent content = EditorGUIUtility.IconContent(iconNames[i]);
				icon = content != null ? content.image : EditorGUIUtility.FindTexture(iconNames[i]);
				s_iconCache[iconNames[i]] = icon;
			}

			if (icon != null)
				return new GUIContent(icon, tooltip);
		}

		return new GUIContent(fallbackText, tooltip);
	}

}
}
#endif
