using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleThumbnailAndPreview.Editor
{
    internal enum ModelPreviewVisualMode
    {
        None,
        Normals,
        UvChecker,
        VertexColor,
        Overdraw,
    }

    internal sealed class ModelPrefabPreviewSession
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

        private const float DefaultGridHalfSize = 6f;
        private const float DefaultGridStep = 0.5f;
        private const float DefaultGridAlpha = 0.14f;
        private const float AxisSize = 0.65f;
        private const float PivotMarkerRadius = 0.07f;
        private const int PivotMarkerSegments = 24;

        private static Mesh s_gridMesh3D;
        private static Mesh s_gridMesh2D;
        private static Mesh s_boundsWireCubeMesh;
        private static Mesh s_pivotCrossMesh;
        private static Mesh s_axesMesh;
        private static Material s_gridMaterial;
        private static Material s_solidLineMaterial;
        private static Material s_skyboxMaterial;
        private static Cubemap s_skyboxMaterialCubemap;
        private static Material s_normalsMaterial;
        private static Material s_uvCheckerMaterial;
        private static Material s_vertexColorMaterial;
        private static Material s_overdrawMaterial;
        private static readonly List<Material[]> SharedMaterialRestoreCache = new();
        private static readonly Dictionary<string, SessionStateSnapshot> SessionStateByAssetPath = new();
        private static string s_lastSetupAssetPath;
        private static readonly Type TmpTextMeshProType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
        private static readonly Type TmpTextMeshProUiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");

        private readonly List<Renderer> _renderers = new();
        private readonly List<bool> _rendererInitialStates = new();

        private PreviewRenderUtility _preview;
        private GameObject _previewRoot;
        private int _prefabInstanceId;
        private string _prefabAssetPath;
        private Skybox _cameraSkybox;
        private Light _rimLight;

        private Vector3 _pivot;
        private Vector3 _targetPivot;
        private Vector2 _orbit = new Vector2(35f, 18f);
        private Vector2 _targetOrbit = new Vector2(35f, 18f);
        private Vector2 _orbitAngularVelocity;
        private float _distance = 8f;
        private float _targetDistance = 8f;
        private bool _isOrbitDragging;
        private double _lastOrbitInputTime = -1d;
        private double _lastInteractionUpdateTime = -1d;

        private bool _gridEnabled = true;
        private bool _lightsEnabled;
        private bool _skyboxEnabled;
        private bool _boundsEnabled;
        private bool _pivotEnabled;
        private bool _axesEnabled;
        private bool _infoEnabled = true;
        private PreviewModeOverride _modeOverride = PreviewModeOverride.Auto;
        private ModelPreviewVisualMode _visualMode = ModelPreviewVisualMode.None;
        private ModelPreviewVisualMode _lastNonNoneVisualMode = ModelPreviewVisualMode.Normals;
        private Bounds _framedBounds;
        private bool _hasFramedBounds;
        private int _triangleCount;
        private int _materialSlotCount;
        private ModelPreviewVisualMode _loggedVisualModeFailure = ModelPreviewVisualMode.None;
        private string _lastGridDiagnosticsKey;

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
            internal bool SkyboxEnabled;
            internal bool BoundsEnabled;
            internal bool PivotEnabled;
            internal bool AxesEnabled;
            internal bool InfoEnabled;
            internal PreviewModeOverride ModeOverride;
            internal ModelPreviewVisualMode VisualMode;
            internal ModelPreviewVisualMode LastNonNoneVisualMode;
            internal double SavedAt;
        }

        internal bool IsReady => _preview != null && _previewRoot != null;
        internal bool GridEnabled => _gridEnabled;
        internal bool LightsEnabled => _lightsEnabled;
        internal bool SkyboxEnabled => _skyboxEnabled;
        internal bool BoundsEnabled => _boundsEnabled;
        internal bool PivotEnabled => _pivotEnabled;
        internal bool AxesEnabled => _axesEnabled;
        internal bool InfoEnabled => _infoEnabled;
        internal PreviewModeOverride ModeOverride => _modeOverride;
        internal ModelPreviewVisualMode VisualMode => _visualMode;
        internal ModelPreviewVisualMode LastNonNoneVisualMode => _lastNonNoneVisualMode;
        internal PreviewModeContext ModeContext => PreviewModeResolver.Resolve(_modeOverride);
        internal bool HasPendingCameraMotion => ComputeHasPendingCameraMotion();
        internal int RendererCount => _renderers.Count;
        internal int TriangleCount => _triangleCount;
        internal int MaterialSlotCount => _materialSlotCount;
        internal Vector3 BoundsSize => _hasFramedBounds ? _framedBounds.size : Vector3.zero;
        internal string ModeLabel => ModeContext.Effective2D ? "2D" : "3D";

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
            _preview.camera.backgroundColor = ParticlePreviewSettings.BackgroundColor;
            _preview.camera.fieldOfView = 30f;
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 1000f;
            _preview.camera.orthographic = false;
            _cameraSkybox = _preview.camera.GetComponent<Skybox>();
            if (_cameraSkybox == null)
                _cameraSkybox = _preview.camera.gameObject.AddComponent<Skybox>();
            _cameraSkybox.enabled = false;

            _previewRoot = UnityEngine.Object.Instantiate(prefab);
            _previewRoot.name = "ModelPreviewRoot";
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;
            ForceActivateHierarchy(_previewRoot);
            _previewRoot.transform.position = Vector3.zero;
            _previewRoot.transform.rotation = prefab.transform.rotation;

            _preview.AddSingleGO(_previewRoot);
            _previewRoot.GetComponentsInChildren(true, _renderers);
            _rendererInitialStates.Clear();
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                _rendererInitialStates.Add(renderer != null && renderer.enabled);
            }
            ComputeStats();

            _modeOverride = ParticlePreviewSettings.ModelPreviewMode;
            _lightsEnabled = ParticlePreviewSettings.ModelDefaultLightingEnabled;
            _skyboxEnabled = ParticlePreviewSettings.ModelDefaultSkyboxEnabled;
            _boundsEnabled = false;
            _pivotEnabled = false;
            _axesEnabled = false;
            _infoEnabled = true;
            _visualMode = ModelPreviewVisualMode.None;
            _lastNonNoneVisualMode = ModelPreviewVisualMode.Normals;
            _lastGridDiagnosticsKey = null;
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
                _skyboxEnabled = restored.SkyboxEnabled;
                _boundsEnabled = restored.BoundsEnabled;
                _pivotEnabled = restored.PivotEnabled;
                _axesEnabled = restored.AxesEnabled;
                _infoEnabled = restored.InfoEnabled;
                _modeOverride = restored.ModeOverride;
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

            _prefabInstanceId = instanceId;
            _prefabAssetPath = assetPath;
            s_lastSetupAssetPath = assetPath;
            _lastInteractionUpdateTime = -1d;
            EnsureGridResources();
            EnsureRimLight();
            PreviewDiagnostics.Log("ModelSession", $"Setup complete id={instanceId} asset='{assetPath}'");
        }

        internal void Cleanup(bool cacheState)
        {
            if (cacheState)
                CacheCurrentSessionState();

            _renderers.Clear();
            _rendererInitialStates.Clear();
            _prefabInstanceId = 0;
            _prefabAssetPath = null;
            _lastInteractionUpdateTime = -1d;
            _isOrbitDragging = false;
            _lastOrbitInputTime = -1d;
            _lastGridDiagnosticsKey = null;
            _hasFramedBounds = false;
            _cameraSkybox = null;
            _rimLight = null;
            _loggedVisualModeFailure = ModelPreviewVisualMode.None;

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

            SessionStateByAssetPath[_prefabAssetPath] = new SessionStateSnapshot
            {
                Pivot = _pivot,
                TargetPivot = _targetPivot,
                Orbit = _orbit,
                TargetOrbit = _targetOrbit,
                Distance = Mathf.Clamp(_distance, MinDistance, MaxDistance),
                TargetDistance = Mathf.Clamp(_targetDistance, MinDistance, MaxDistance),
                GridEnabled = _gridEnabled,
                LightsEnabled = _lightsEnabled,
                SkyboxEnabled = _skyboxEnabled,
                BoundsEnabled = _boundsEnabled,
                PivotEnabled = _pivotEnabled,
                AxesEnabled = _axesEnabled,
                InfoEnabled = _infoEnabled,
                ModeOverride = _modeOverride,
                VisualMode = _visualMode,
                LastNonNoneVisualMode = _lastNonNoneVisualMode,
                SavedAt = EditorApplication.timeSinceStartup,
            };

            if (SessionStateByAssetPath.Count <= MaxCachedSessionStates)
                return;

            string oldestKey = null;
            double oldestTime = double.MaxValue;
            foreach (KeyValuePair<string, SessionStateSnapshot> pair in SessionStateByAssetPath)
            {
                if (pair.Value.SavedAt >= oldestTime)
                    continue;

                oldestTime = pair.Value.SavedAt;
                oldestKey = pair.Key;
            }

            if (!string.IsNullOrEmpty(oldestKey))
                SessionStateByAssetPath.Remove(oldestKey);
        }

        private static bool TryRestoreSessionState(string assetPath, out SessionStateSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (!SessionStateByAssetPath.TryGetValue(assetPath, out snapshot))
                return false;

            if (EditorApplication.timeSinceStartup - snapshot.SavedAt > SessionRestoreWindowSeconds)
            {
                SessionStateByAssetPath.Remove(assetPath);
                return false;
            }

            return true;
        }

        internal void SetGridEnabled(bool enabled)
        {
            _gridEnabled = enabled;
            _lastGridDiagnosticsKey = null;
            LogGridDiagnosticsState("toggle");
        }

        internal void SetLightsEnabled(bool enabled)
        {
            _lightsEnabled = enabled;
        }

        internal void SetSkyboxEnabled(bool enabled)
        {
            _skyboxEnabled = enabled;
        }

        internal void SetBoundsEnabled(bool enabled)
        {
            _boundsEnabled = enabled;
        }

        internal void SetPivotEnabled(bool enabled)
        {
            _pivotEnabled = enabled;
        }

        internal void SetAxesEnabled(bool enabled)
        {
            _axesEnabled = enabled;
        }

        internal void SetInfoEnabled(bool enabled)
        {
            _infoEnabled = enabled;
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
            _modeOverride = _modeOverride switch
            {
                PreviewModeOverride.Auto => PreviewModeOverride.Force2D,
                PreviewModeOverride.Force2D => PreviewModeOverride.Force3D,
                _ => PreviewModeOverride.Auto,
            };

            if (ModeContext.Effective2D)
            {
                _targetOrbit = Vector2.zero;
                _orbit = Vector2.zero;
                _orbitAngularVelocity = Vector2.zero;
                _isOrbitDragging = false;
                _lastOrbitInputTime = -1d;
                _lightsEnabled = false;
                _skyboxEnabled = false;
            }

            _lastGridDiagnosticsKey = null;
            LogGridDiagnosticsState("mode-changed");
        }

        internal bool HandleInput(Rect previewRect, Event evt)
        {
            if (!IsReady || evt == null)
                return false;

            if (GUIUtility.hotControl != 0)
                return false;

            bool changed = false;
            double now = EditorApplication.timeSinceStartup;
            bool pointerInPreview = previewRect.Contains(evt.mousePosition);
            bool effective2D = ModeContext.Effective2D;

            bool isPanDrag = evt.type == EventType.MouseDrag && (evt.button == 2 || (evt.button == 0 && evt.command));
            if (isPanDrag && pointerInPreview)
            {
                PanPreviewTarget(evt.delta, previewRect, effective2D);
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
                                dt = Mathf.Clamp((float)(now - _lastOrbitInputTime), 1f / 240f, MaxDeltaTime);
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
            bool effective2D = ModeContext.Effective2D;

            double now = EditorApplication.timeSinceStartup;
            if (_lastInteractionUpdateTime < 0d)
            {
                _lastInteractionUpdateTime = now;
                return ComputeHasPendingCameraMotion();
            }

            float dt = Mathf.Clamp((float)(now - _lastInteractionUpdateTime), 0f, MaxDeltaTime);
            _lastInteractionUpdateTime = now;
            if (dt <= 0f)
                return ComputeHasPendingCameraMotion();

            if (_isOrbitDragging && _lastOrbitInputTime >= 0d && now - _lastOrbitInputTime > OrbitHoldStillResetSeconds)
                _orbitAngularVelocity = Vector2.zero;

            float angularVelocityEpsilonSq = AngularVelocityEpsilon * AngularVelocityEpsilon;
            if (!_isOrbitDragging && _orbitAngularVelocity.sqrMagnitude > angularVelocityEpsilonSq)
            {
                _targetOrbit.x += _orbitAngularVelocity.x * dt;
                if (!effective2D)
                    _targetOrbit.y = Mathf.Clamp(_targetOrbit.y + _orbitAngularVelocity.y * dt, PitchMin, PitchMax);

                float velocityDecay = Mathf.Exp(-orbitSmoothing * dt);
                _orbitAngularVelocity *= velocityDecay;
                if (_orbitAngularVelocity.sqrMagnitude <= angularVelocityEpsilonSq)
                    _orbitAngularVelocity = Vector2.zero;
            }

            _targetOrbit.y = effective2D ? 0f : Mathf.Clamp(_targetOrbit.y, PitchMin, PitchMax);

            if (Vector2.Distance(_orbit, _targetOrbit) > OrbitEpsilon)
            {
                float orbitBlend = 1f - Mathf.Exp(-orbitSmoothing * dt);
                _orbit = Vector2.Lerp(_orbit, _targetOrbit, orbitBlend);
            }
            else
            {
                _orbit = _targetOrbit;
            }

            if (Mathf.Abs(_distance - _targetDistance) > DistanceEpsilon)
            {
                float zoomBlend = 1f - Mathf.Exp(-ZoomSmooth * dt);
                _distance = Mathf.Lerp(_distance, _targetDistance, zoomBlend);
            }
            else
            {
                _distance = _targetDistance;
            }

            float pivotDeltaSqr = (_pivot - _targetPivot).sqrMagnitude;
            if (pivotDeltaSqr > PivotEpsilon * PivotEpsilon)
            {
                float panBlend = 1f - Mathf.Exp(-panSmoothing * dt);
                _pivot = Vector3.Lerp(_pivot, _targetPivot, panBlend);
            }
            else
            {
                _pivot = _targetPivot;
            }

            return ComputeHasPendingCameraMotion();
        }

        internal void Draw(Rect previewRect, GUIStyle background)
        {
            if (!IsReady || Event.current.type != EventType.Repaint)
                return;

            _preview.camera.backgroundColor = ParticlePreviewSettings.BackgroundColor;
            UpdateCameraTransform();
            ApplyEnvironmentState();

            _preview.BeginPreview(previewRect, background ?? GUIStyle.none);
            if (_gridEnabled)
            {
                DrawGrid();
            }
            else
            {
                LogGridDiagnosticsState("skip-grid-disabled");
            }

            bool restoreMaterials = false;
            if (TryApplyVisualModeMaterial(out Material visualModeMaterial))
            {
                SwapRendererMaterials(visualModeMaterial);
                restoreMaterials = true;
            }

            try
            {
                using (ParticleRenderCompatibilityUtility.EnableRenderersScoped(_renderers))
                {
                    ParticleRenderCompatibilityUtility.RenderPreviewWithLegacyCameraPath(_preview);
                }

                if (_boundsEnabled)
                    DrawBoundsOverlay();
                if (_pivotEnabled)
                    DrawPivotOverlay();
                if (_axesEnabled)
                    DrawAxesOverlay();
            }
            finally
            {
                if (restoreMaterials)
                    RestoreRendererMaterials();
            }

            Texture previewTexture = _preview.EndPreview();
            if (previewTexture != null)
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.StretchToFill);
        }

        private void UpdateCameraTransform()
        {
            Camera camera = _preview.camera;
            bool effective2D = ModeContext.Effective2D;
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
            bool effective2D = ModeContext.Effective2D;
            bool lightingEnabled = _lightsEnabled && !effective2D;
            bool skyboxEnabled = _skyboxEnabled && !effective2D;

            _preview.ambientColor = lightingEnabled
                ? ParticlePreviewSettings.ModelAmbientColor
                : Color.black;

            ApplyDirectionalLight(_preview.lights[0], lightingEnabled, ParticlePreviewSettings.ModelKeyLightIntensity, ParticlePreviewSettings.ModelKeyLightRotation, Color.white);
            ApplyDirectionalLight(_preview.lights[1], lightingEnabled, ParticlePreviewSettings.ModelFillLightIntensity, ParticlePreviewSettings.ModelFillLightRotation, Color.white);

            EnsureRimLight();
            if (_rimLight != null)
            {
                bool rimEnabled = lightingEnabled && ParticlePreviewSettings.ModelRimLightEnabled;
                _rimLight.enabled = rimEnabled;
                if (rimEnabled)
                {
                    _rimLight.intensity = ParticlePreviewSettings.ModelRimLightIntensity;
                    _rimLight.color = ParticlePreviewSettings.ModelRimLightColor;
                    _rimLight.transform.rotation = RotationFromYawPitch(ParticlePreviewSettings.ModelRimLightRotation);
                }
            }

            if (_cameraSkybox != null)
            {
                Cubemap cubemap = ParticlePreviewSettings.ModelSkyboxCubemap;
                bool canUseSkybox = skyboxEnabled && cubemap != null;
                _cameraSkybox.enabled = canUseSkybox;
                if (canUseSkybox)
                {
                    EnsureSkyboxMaterial(cubemap);
                    _cameraSkybox.material = s_skyboxMaterial;
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

        private static void ApplyDirectionalLight(Light light, bool enabled, float intensity, Vector2 yawPitch, Color color)
        {
            if (light == null)
                return;

            light.type = LightType.Directional;
            light.enabled = enabled;
            light.intensity = enabled ? intensity : 0f;
            light.color = color;
            light.transform.rotation = RotationFromYawPitch(yawPitch);
        }

        private static Quaternion RotationFromYawPitch(Vector2 yawPitch)
        {
            return Quaternion.Euler(yawPitch.y, yawPitch.x, 0f);
        }

        private void EnsureRimLight()
        {
            if (_rimLight != null || _preview == null)
                return;

            var rimRoot = new GameObject("PreviewRimLight") { hideFlags = HideFlags.HideAndDontSave };
            _rimLight = rimRoot.AddComponent<Light>();
            _rimLight.type = LightType.Directional;
            _rimLight.shadows = LightShadows.None;
            _preview.AddSingleGO(rimRoot);
        }

        private static void EnsureSkyboxMaterial(Cubemap cubemap)
        {
            if (cubemap == null)
                return;

            if (s_skyboxMaterial == null)
            {
                Shader shader = Shader.Find("Skybox/Cubemap");
                if (shader == null)
                {
                    if (ParticlePreviewSettings.EnableDiagnostics)
                        PreviewDiagnostics.Log("ModelSkybox", "Skybox/Cubemap shader is unavailable; falling back to solid background.");
                    return;
                }
                s_skyboxMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (s_skyboxMaterialCubemap == cubemap)
                return;

            s_skyboxMaterial.SetTexture("_Tex", cubemap);
            s_skyboxMaterialCubemap = cubemap;
        }

        private bool TryApplyVisualModeMaterial(out Material material)
        {
            material = _visualMode switch
            {
                ModelPreviewVisualMode.Normals => s_normalsMaterial ??= CreateNormalsMaterial(),
                ModelPreviewVisualMode.UvChecker => s_uvCheckerMaterial ??= CreateUvCheckerMaterial(),
                ModelPreviewVisualMode.VertexColor => s_vertexColorMaterial ??= CreateVertexColorMaterial(),
                ModelPreviewVisualMode.Overdraw => s_overdrawMaterial ??= CreateOverdrawMaterial(),
                _ => null,
            };

            if (material == null && _visualMode != ModelPreviewVisualMode.None && ParticlePreviewSettings.EnableDiagnostics
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

        private void DrawBoundsOverlay()
        {
            if (!_hasFramedBounds || s_solidLineMaterial == null || s_boundsWireCubeMesh == null)
                return;

            Matrix4x4 matrix = Matrix4x4.TRS(_framedBounds.center, Quaternion.identity, _framedBounds.size);
            _preview.DrawMesh(s_boundsWireCubeMesh, matrix, s_solidLineMaterial, 0);
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

            bool effective2D = ModeContext.Effective2D;
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
                _orbit = new Vector2(35f, 18f);
                _targetOrbit = _orbit;
            }

            _orbitAngularVelocity = Vector2.zero;
            _isOrbitDragging = false;
            _lastOrbitInputTime = -1d;

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

        internal static bool ComputeLightingEnabledForTests(bool toggleEnabled, bool effective2D)
        {
            return toggleEnabled && !effective2D;
        }

        internal static bool ComputeSkyboxEnabledForTests(bool toggleEnabled, bool hasCubemap, bool effective2D)
        {
            return toggleEnabled && hasCubemap && !effective2D;
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
            float angularVelocityEpsilonSq = AngularVelocityEpsilon * AngularVelocityEpsilon;
            return Vector2.Distance(_orbit, _targetOrbit) > OrbitEpsilon
                   || Mathf.Abs(_distance - _targetDistance) > DistanceEpsilon
                   || (_pivot - _targetPivot).sqrMagnitude > PivotEpsilon * PivotEpsilon
                   || (!_isOrbitDragging && _orbitAngularVelocity.sqrMagnitude > angularVelocityEpsilonSq);
        }

        private static void EnsureGridResources()
        {
            if (s_gridMaterial == null)
            {
                s_gridMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };

                s_gridMaterial.SetInt("_ZWrite", 0);
                s_gridMaterial.SetInt("_Cull", 0);
                s_gridMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                s_gridMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                s_gridMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                s_gridMaterial.renderQueue = 2999;
            }

            if (s_solidLineMaterial == null)
            {
                s_solidLineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                s_solidLineMaterial.SetInt("_ZWrite", 0);
                s_solidLineMaterial.SetInt("_Cull", 0);
                s_solidLineMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
                s_solidLineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                s_solidLineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                s_solidLineMaterial.renderQueue = 2999;
            }

            if (s_gridMesh3D == null)
            {
                s_gridMesh3D = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildGridMesh(s_gridMesh3D, DefaultGridHalfSize, DefaultGridStep, DefaultGridAlpha, is2D: false);
            }

            if (s_gridMesh2D == null)
            {
                s_gridMesh2D = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildGridMesh(s_gridMesh2D, DefaultGridHalfSize, DefaultGridStep, DefaultGridAlpha, is2D: true);
            }

            if (s_boundsWireCubeMesh == null)
            {
                s_boundsWireCubeMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildBoundsWireCubeMesh(s_boundsWireCubeMesh);
            }

            if (s_pivotCrossMesh == null)
            {
                s_pivotCrossMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildPivotMesh(s_pivotCrossMesh);
            }

            if (s_axesMesh == null)
            {
                s_axesMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                BuildAxesMesh(s_axesMesh);
            }
        }

        private void DrawGrid()
        {
            if (s_gridMaterial == null)
            {
                LogGridDiagnosticsState("skip-material-null");
                return;
            }

            Mesh gridMesh = ModeContext.Effective2D ? s_gridMesh2D : s_gridMesh3D;
            if (gridMesh == null)
            {
                LogGridDiagnosticsState("skip-mesh-null");
                return;
            }

            Matrix4x4 gridMatrix = BuildGridMatrix();
            _preview.DrawMesh(gridMesh, gridMatrix, s_gridMaterial, 0);
            LogGridDiagnosticsState("drawn");
        }

        private Matrix4x4 BuildGridMatrix()
        {
            Vector3 anchor = _pivot;
            if (!ModeContext.Effective2D && TryComputeGridFloorY(out float floorY))
                anchor.y = floorY;

            return Matrix4x4.TRS(anchor, Quaternion.identity, Vector3.one);
        }

        private bool TryComputeGridFloorY(out float floorY)
        {
            floorY = 0f;

            bool foundPreferred = false;
            float preferredMinY = float.PositiveInfinity;
            bool foundFallback = false;
            float fallbackMinY = float.PositiveInfinity;

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

                float minY = worldBounds.min.y;
                if (minY < fallbackMinY)
                {
                    fallbackMinY = minY;
                    foundFallback = true;
                }

                // In mixed prefabs (mesh + sprite/canvas), keep the 3D grid on mesh floor.
                bool preferred3DRenderer = renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
                if (preferred3DRenderer && minY < preferredMinY)
                {
                    preferredMinY = minY;
                    foundPreferred = true;
                }
            }

            if (foundPreferred)
            {
                floorY = preferredMinY;
                return true;
            }

            if (foundFallback)
            {
                floorY = fallbackMinY;
                return true;
            }

            return false;
        }

        private void LogGridDiagnosticsState(string state)
        {
            bool hasCanvas = _previewRoot != null && _previewRoot.GetComponentInChildren<Canvas>(true) != null;
            bool hasTmpUi = HasComponentInChildren(_previewRoot, TmpTextMeshProUiType);
            bool hasTmpMesh = HasComponentInChildren(_previewRoot, TmpTextMeshProType);
            bool effective2D = ModeContext.Effective2D;
            Vector3 gridAnchor = _pivot;
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

        private static void BuildBoundsWireCubeMesh(Mesh mesh)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            Color c = new Color(0.98f, 0.82f, 0.18f, 0.95f);

            static void AddEdge(List<Vector3> verts, List<Color> cols, Vector3 a, Vector3 b, Color col)
            {
                verts.Add(a);
                cols.Add(col);
                verts.Add(b);
                cols.Add(col);
            }

            Vector3[] p =
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
            };

            AddEdge(vertices, colors, p[0], p[1], c); AddEdge(vertices, colors, p[1], p[2], c);
            AddEdge(vertices, colors, p[2], p[3], c); AddEdge(vertices, colors, p[3], p[0], c);
            AddEdge(vertices, colors, p[4], p[5], c); AddEdge(vertices, colors, p[5], p[6], c);
            AddEdge(vertices, colors, p[6], p[7], c); AddEdge(vertices, colors, p[7], p[4], c);
            AddEdge(vertices, colors, p[0], p[4], c); AddEdge(vertices, colors, p[1], p[5], c);
            AddEdge(vertices, colors, p[2], p[6], c); AddEdge(vertices, colors, p[3], p[7], c);

            SetLineMeshData(mesh, vertices, colors);
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

        private static Material CreateNormalsMaterial()
        {
            const string src = @"
Shader ""Hidden/ParticlePreview/Normals""
{
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" }
		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
			struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.n = UnityObjectToWorldNormal(v.normal);
				return o;
			}
			fixed4 frag(v2f i) : SV_Target
			{
				float3 n = normalize(i.n);
				return fixed4(n * 0.5 + 0.5, 1);
			}
			ENDCG
		}
	}
}";
            return CreateMaterialFromSource(src);
        }

        private static Material CreateUvCheckerMaterial()
        {
            const string src = @"
Shader ""Hidden/ParticlePreview/UvChecker""
{
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" }
		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			fixed4 frag(v2f i) : SV_Target
			{
				float coarse = fmod(floor(i.uv.x * 8.0) + floor(i.uv.y * 8.0), 2.0);
				float fine = fmod(floor(i.uv.x * 64.0) + floor(i.uv.y * 64.0), 2.0);
				float3 color = lerp(float3(0.15, 0.15, 0.18), float3(0.85, 0.85, 0.82), coarse);
				color *= lerp(0.92, 1.0, fine);
				return fixed4(color, 1);
			}
			ENDCG
		}
	}
}";
            return CreateMaterialFromSource(src);
        }

        private static Material CreateVertexColorMaterial()
        {
            const string src = @"
Shader ""Hidden/ParticlePreview/VertexColor""
{
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" }
		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
			struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				return o;
			}
			fixed4 frag(v2f i) : SV_Target
			{
				return i.color;
			}
			ENDCG
		}
	}
}";
            return CreateMaterialFromSource(src);
        }

        private static Material CreateOverdrawMaterial()
        {
            const string src = @"
Shader ""Hidden/ParticlePreview/Overdraw""
{
	SubShader
	{
		Tags { ""RenderType""=""Transparent"" ""Queue""=""Transparent"" }
		Pass
		{
			Blend One One
			ZWrite Off
			ZTest Always
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; };
			struct v2f { float4 pos : SV_POSITION; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}
			fixed4 frag(v2f i) : SV_Target
			{
				return fixed4(0.1, 0.04, 0.02, 0.0);
			}
			ENDCG
		}
	}
}";
            return CreateMaterialFromSource(src);
        }

        private static Material CreateMaterialFromSource(string shaderSource)
        {
            Shader shader = ShaderUtil.CreateShaderAsset(shaderSource, false);
            if (shader == null)
                return null;

            shader.hideFlags = HideFlags.HideAndDontSave;
            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private static void BuildGridMesh(Mesh mesh, float halfSize, float step, float alpha, bool is2D)
        {
            if (mesh == null)
                return;

            float safeHalfSize = Mathf.Max(0.05f, halfSize);
            float safeStep = Mathf.Max(0.05f, step);
            int count = Mathf.Max(1, Mathf.RoundToInt(safeHalfSize / safeStep));

            var vertices = new List<Vector3>();
            var colors = new List<Color>();

            Color baseColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, alpha)
                : new Color(0f, 0f, 0f, alpha);
            Color axisX = EditorGUIUtility.isProSkin
                ? new Color(1f, 0.28f, 0.28f, Mathf.Clamp01(alpha * 1.85f))
                : new Color(0.75f, 0.12f, 0.12f, Mathf.Clamp01(alpha * 1.85f));
            Color axisY = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 1f, 0.28f, Mathf.Clamp01(alpha * 1.85f))
                : new Color(0.12f, 0.58f, 0.12f, Mathf.Clamp01(alpha * 1.85f));

            for (int i = -count; i <= count; i++)
            {
                float position = i * safeStep;
                float fade = Mathf.Pow(1f - Mathf.Abs(position) / safeHalfSize, 2f);
                bool isCenter = i == 0;

                Color xLine = isCenter ? axisX : baseColor;
                Color xPeak = new Color(xLine.r, xLine.g, xLine.b, xLine.a * fade);
                Color xEdge = new Color(xLine.r, xLine.g, xLine.b, isCenter ? xLine.a * 0.32f : 0f);

                Color yLine = isCenter ? axisY : baseColor;
                Color yPeak = new Color(yLine.r, yLine.g, yLine.b, yLine.a * fade);
                Color yEdge = new Color(yLine.r, yLine.g, yLine.b, isCenter ? yLine.a * 0.32f : 0f);

                if (is2D)
                {
                    vertices.Add(new Vector3(-safeHalfSize, position, 0f));
                    colors.Add(xEdge);
                    vertices.Add(new Vector3(0f, position, 0f));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(0f, position, 0f));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(safeHalfSize, position, 0f));
                    colors.Add(xEdge);

                    vertices.Add(new Vector3(position, -safeHalfSize, 0f));
                    colors.Add(yEdge);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(yPeak);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(yPeak);
                    vertices.Add(new Vector3(position, safeHalfSize, 0f));
                    colors.Add(yEdge);
                }
                else
                {
                    vertices.Add(new Vector3(-safeHalfSize, 0f, position));
                    colors.Add(xEdge);
                    vertices.Add(new Vector3(0f, 0f, position));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(0f, 0f, position));
                    colors.Add(xPeak);
                    vertices.Add(new Vector3(safeHalfSize, 0f, position));
                    colors.Add(xEdge);

                    vertices.Add(new Vector3(position, 0f, -safeHalfSize));
                    colors.Add(yEdge);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(yPeak);
                    vertices.Add(new Vector3(position, 0f, 0f));
                    colors.Add(yPeak);
                    vertices.Add(new Vector3(position, 0f, safeHalfSize));
                    colors.Add(yEdge);
                }
            }

            int[] indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }

        private static void ForceActivateHierarchy(GameObject root)
        {
            if (root == null)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null)
                    transform.gameObject.SetActive(true);
            }
        }
    }
}
