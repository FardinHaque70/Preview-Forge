using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
// Builds an isolated sprite-prefab preview scene with switchable 2D/3D camera modes, pan/zoom-orbit interaction, and shared grid rendering.

namespace ParticleThumbnailAndPreview.Editor
{
    internal sealed class SpritePrefabPreviewSession : IPreviewToolbarCommonSession
    {
        private const double SessionRestoreWindowSeconds = 2.0d;
        private const int MaxCachedSessionStates = 64;
        private const float MaxDeltaTime = 0.05f;
        private const float ZoomSmooth = 8f;
        private const float DistanceEpsilon = 0.001f;
        private const float PivotEpsilon = 0.0001f;
        private const float AngularVelocityEpsilon = 0.01f;
        private const float MinOrthoSize = 0.1f;
        private const float MaxOrthoSize = 500f;
        private const float MinDistance = 0.1f;
        private const float MaxDistance = 500f;
        private const float ZoomFactorPerScrollUnit = 0.1f;
        private const float FramingPadding = 1.15f;
        private const float CameraDepthOffset = 10f;
        private const float OrbitSensitivity = 1.2f;
        private const float PitchMin = -85f;
        private const float PitchMax = 85f;
        private const float OrbitVelocitySmoothing = 0.35f;
        private const float FallbackOrbitInputDeltaTime = 1f / 60f;
        private const float OrbitHoldStillResetSeconds = 0.08f;
        private const float OrbitDragDeltaDeadzoneSqr = 0.0001f;

        private static readonly PreviewCameraInteractionConfig CameraInteractionConfig = new(
            PitchMin,
            PitchMax,
            MaxDeltaTime,
            orbitEpsilon: 0.01f,
            distanceEpsilon: DistanceEpsilon,
            pivotEpsilon: PivotEpsilon,
            angularVelocityEpsilon: AngularVelocityEpsilon,
            orbitHoldStillResetSeconds: OrbitHoldStillResetSeconds,
            zoomSmooth: ZoomSmooth);
        private static readonly Dictionary<string, SessionStateSnapshot> SessionStateByAssetPath = new();
        private static string s_lastSetupAssetPath;

        private readonly List<SpriteRenderer> _spriteRenderers = new();
        private readonly List<ParticleSystem> _particleSystems = new();
        private readonly List<Renderer> _renderers = new();
        private readonly List<bool> _rendererInitialStates = new();
        private readonly List<Collider2D> _colliders2D = new();
        private readonly List<SpriteRenderer> _colliderSpriteRenderers = new();
        private readonly List<Color> _colliderSpriteOriginalColors = new();
        private readonly List<SpriteMask> _spriteMasks = new();
        private readonly List<SortingGroup> _sortingGroups = new();

        private PreviewRenderUtility _preview;
        private GameObject _previewRoot;
        private int _prefabInstanceId;
        private string _prefabAssetPath;
        private Bounds _framedBounds;
        private bool _hasFramedBounds;
        private bool _gridEnabled = true;
        private bool _boundsOverlayEnabled = PreviewSettings.SharedBoundsRulerDefaultEnabled;
        private bool _colliderOverlayEnabled;
        private bool _hasParticles;
        private PreviewModeOverride _modeOverride = PreviewModeOverride.Force2D;
        private double _lastInteractionUpdateTime = -1d;
        private Vector3 _pivot;
        private Vector3 _targetPivot;
        private float _orthoSize = 5f;
        private float _targetOrthoSize = 5f;
        private Vector2 _orbit = Vector2.zero;
        private Vector2 _targetOrbit = Vector2.zero;
        private Vector2 _orbitAngularVelocity = Vector2.zero;
        private float _distance = 8f;
        private float _targetDistance = 8f;
        private bool _isOrbitDragging;
        private double _lastOrbitInputTime = -1d;

        private struct SessionStateSnapshot
        {
            internal Vector3 Pivot;
            internal Vector3 TargetPivot;
            internal float OrthoSize;
            internal float TargetOrthoSize;
            internal Vector2 Orbit;
            internal Vector2 TargetOrbit;
            internal float Distance;
            internal float TargetDistance;
            internal PreviewModeOverride ModeOverride;
            internal bool GridEnabled;
            internal bool BoundsOverlayEnabled;
            internal bool ColliderOverlayEnabled;
            internal double SavedAt;
        }

        internal bool IsReady => _preview != null && _previewRoot != null;
        internal bool GridEnabled => _gridEnabled;
        internal bool BoundsOverlayEnabled => _boundsOverlayEnabled;
        internal bool ColliderOverlayEnabled => _colliderOverlayEnabled;
        internal bool HasPendingCameraMotion => ComputeHasPendingCameraMotion();
        internal int SpriteRendererCount => _spriteRenderers.Count;
        internal int ParticleSystemCount => _particleSystems.Count;
        internal int SortingGroupCount => _sortingGroups.Count;
        internal int SpriteMaskCount => _spriteMasks.Count;
        internal int Collider2DCount => _colliders2D.Count;
        internal Vector3 BoundsSize => _hasFramedBounds ? _framedBounds.size : Vector3.zero;
        internal PreviewModeOverride ModeOverride => _modeOverride;
        internal PreviewModeContext ModeContext => PreviewModeResolver.Resolve(_modeOverride);
        internal string ModeLabel => ModeContext.CameraIs2D ? "2D" : "3D";

        internal static Color ComputeColliderPreviewTintForTests(Color source)
        {
            float gray = source.r * 0.299f + source.g * 0.587f + source.b * 0.114f;
            return new Color(gray, gray, gray, source.a);
        }

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

            Cleanup(cacheState: !isSwitchingToDifferentPrefab);

            _preview = new PreviewRenderUtility(true);
            _preview.camera.clearFlags = CameraClearFlags.SolidColor;
            _preview.camera.backgroundColor = PreviewSettings.BackgroundColor;
            _preview.camera.fieldOfView = 30f;
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 200f;

            BuildPreviewRoot(prefab);
            CollectPreviewContent();
            PrepareParticleSystems();

            _modeOverride = PreviewModeOverride.Force2D;
            _gridEnabled = PreviewSettings.SharedGridDefaultEnabled;
            _boundsOverlayEnabled = PreviewSettings.SharedBoundsRulerDefaultEnabled;
            _colliderOverlayEnabled = false;
            FrameCameraToContent();

            bool shouldRestoreState = !isSwitchingToDifferentPrefab && isTransientRebuildOfSameSelection;
            if (shouldRestoreState && TryRestoreSessionState(assetPath, out SessionStateSnapshot restored))
            {
                _pivot = restored.Pivot;
                _targetPivot = restored.TargetPivot;
                _orthoSize = Mathf.Clamp(restored.OrthoSize, MinOrthoSize, MaxOrthoSize);
                _targetOrthoSize = Mathf.Clamp(restored.TargetOrthoSize, MinOrthoSize, MaxOrthoSize);
                _orbit = restored.Orbit;
                _targetOrbit = restored.TargetOrbit;
                _distance = Mathf.Clamp(restored.Distance, MinDistance, MaxDistance);
                _targetDistance = Mathf.Clamp(restored.TargetDistance, MinDistance, MaxDistance);
                _modeOverride = NormalizeModeOverride(restored.ModeOverride);
                _gridEnabled = restored.GridEnabled;
                _boundsOverlayEnabled = restored.BoundsOverlayEnabled;
                _colliderOverlayEnabled = restored.ColliderOverlayEnabled;
                if (ModeContext.CameraIs2D)
                {
                    _orbit = Vector2.zero;
                    _targetOrbit = Vector2.zero;
                }
            }

            _prefabInstanceId = instanceId;
            _prefabAssetPath = assetPath;
            s_lastSetupAssetPath = assetPath;
            _lastInteractionUpdateTime = -1d;
            _orbitAngularVelocity = Vector2.zero;
            _isOrbitDragging = false;
            _lastOrbitInputTime = -1d;
            RefreshColliderSpriteTint();
        }

        internal void Cleanup(bool cacheState)
        {
            if (cacheState)
                CacheCurrentSessionState();

            RestoreColliderSpriteColors();
            _spriteRenderers.Clear();
            _particleSystems.Clear();
            _renderers.Clear();
            _rendererInitialStates.Clear();
            _colliders2D.Clear();
            _colliderSpriteRenderers.Clear();
            _colliderSpriteOriginalColors.Clear();
            _spriteMasks.Clear();
            _sortingGroups.Clear();
            _prefabInstanceId = 0;
            _prefabAssetPath = null;
            _hasFramedBounds = false;
            _hasParticles = false;
            _lastInteractionUpdateTime = -1d;
            _orbitAngularVelocity = Vector2.zero;
            _isOrbitDragging = false;
            _lastOrbitInputTime = -1d;

            if (_previewRoot != null)
                UnityEngine.Object.DestroyImmediate(_previewRoot);
            _previewRoot = null;

            _preview?.Cleanup();
            _preview = null;
        }

        internal bool HandleInput(Rect previewRect, Event evt)
        {
            if (!IsReady || evt == null)
                return false;

            if (GUIUtility.hotControl != 0)
                return false;

            bool effective2D = ModeContext.CameraIs2D;
            bool pointerInPreview = previewRect.Contains(evt.mousePosition);
            bool changed = false;
            double now = EditorApplication.timeSinceStartup;

            bool isPanDrag = evt.type == EventType.MouseDrag && (evt.button == 2 || (evt.button == 0 && evt.command) || (effective2D && evt.button == 0));
            if (isPanDrag && pointerInPreview)
            {
                PanPreviewTarget(evt.delta, previewRect, effective2D);
                if (effective2D)
                    evt.Use();
                else
                {
                    evt.Use();
                    changed = true;
                }

                return true;
            }

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (!effective2D && evt.button == 0 && pointerInPreview)
                    {
                        _isOrbitDragging = true;
                        _orbitAngularVelocity = Vector2.zero;
                        _lastOrbitInputTime = now;
                        evt.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (!effective2D && evt.button == 0 && _isOrbitDragging)
                    {
                        Vector2 delta = evt.delta * OrbitSensitivity;
                        if (delta.sqrMagnitude > OrbitDragDeltaDeadzoneSqr)
                        {
                            float dt = FallbackOrbitInputDeltaTime;
                            if (_lastOrbitInputTime >= 0d)
                                dt = Mathf.Clamp((float)(now - _lastOrbitInputTime), 1f / 240f, MaxDeltaTime);
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
                    if (!effective2D && evt.button == 0 && _isOrbitDragging)
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
                        if (effective2D)
                        {
                            float nextSize = _targetOrthoSize * (1f + evt.delta.y * ZoomFactorPerScrollUnit);
                            _targetOrthoSize = Mathf.Clamp(nextSize, MinOrthoSize, MaxOrthoSize);
                        }
                        else
                        {
                            float nextDistance = _targetDistance * (1f + evt.delta.y * ZoomFactorPerScrollUnit);
                            _targetDistance = Mathf.Clamp(nextDistance, MinDistance, MaxDistance);
                        }

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

            var state = new PreviewCameraInteractionState
            {
                Orbit = _orbit,
                TargetOrbit = _targetOrbit,
                OrbitAngularVelocity = _orbitAngularVelocity,
                Distance = effective2D ? _orthoSize : _distance,
                TargetDistance = effective2D ? _targetOrthoSize : _targetDistance,
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
            _pivot = state.Pivot;
            _targetPivot = state.TargetPivot;
            _isOrbitDragging = state.IsOrbitDragging;
            _lastOrbitInputTime = state.LastOrbitInputTime;

            if (effective2D)
            {
                _orthoSize = state.Distance;
                _targetOrthoSize = state.TargetDistance;
                _orbit = Vector2.zero;
                _targetOrbit = Vector2.zero;
            }
            else
            {
                _distance = state.Distance;
                _targetDistance = state.TargetDistance;
            }

            return pending;
        }

        internal void Draw(Rect previewRect, GUIStyle background)
        {
            if (!IsReady || Event.current.type != EventType.Repaint)
                return;

            _preview.camera.backgroundColor = PreviewSettings.BackgroundColor;
            UpdateCameraTransform(previewRect);

            _preview.BeginPreview(previewRect, background ?? GUIStyle.none);
            DrawGrid();
            if (_boundsOverlayEnabled && _hasFramedBounds)
                PreviewBoundsVisualizer.DrawWire(_preview, _framedBounds);
            if (_colliderOverlayEnabled)
                ModelColliderOverlayRenderer.Draw(_preview, null, _colliders2D);

            using (PreviewRenderCompatibilityUtility.PushShaderTime(0f))
            using (PreviewRenderCompatibilityUtility.EnableRenderersScoped(_renderers, _rendererInitialStates))
            {
                PreviewRenderCompatibilityUtility.RenderPreviewWithCameraPath(_preview);
            }

            Texture previewTexture = _preview.EndPreview();
            if (previewTexture != null)
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.StretchToFill);

            if (_boundsOverlayEnabled && _hasFramedBounds)
                PreviewBoundsVisualizer.DrawLabels(previewRect, _preview.camera, _framedBounds);
        }

        internal void SetGridEnabled(bool enabled)
        {
            _gridEnabled = enabled;
        }

        internal void SetBoundsOverlayEnabled(bool enabled)
        {
            _boundsOverlayEnabled = enabled;
        }

        internal void SetColliderOverlayEnabled(bool enabled)
        {
            _colliderOverlayEnabled = enabled;
            RefreshColliderSpriteTint();
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
                _orbit = Vector2.zero;
                _targetOrbit = Vector2.zero;
                _orbitAngularVelocity = Vector2.zero;
                _isOrbitDragging = false;
                _lastOrbitInputTime = -1d;
            }

            if (isEffective2D != wasEffective2D)
                FrameCameraToContent();
        }

        internal static float ComputeInitialOrthoSizeForBoundsForTests(Bounds bounds, float aspect)
        {
            float safeAspect = Mathf.Max(0.0001f, aspect);
            float vertical = Mathf.Max(bounds.extents.y, 0.1f);
            float horizontal = Mathf.Max(bounds.extents.x / safeAspect, 0.1f);
            return Mathf.Max(vertical, horizontal) * FramingPadding;
        }

        internal static float ComputeInitialDistanceForBoundsForTests(Bounds bounds, float cameraFov)
        {
            float size = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.1f);
            float fovRad = cameraFov * Mathf.Deg2Rad * 0.5f;
            return Mathf.Max(size * 1.5f / Mathf.Max(Mathf.Tan(fovRad), 0.001f), 0.3f);
        }

        private void BuildPreviewRoot(GameObject prefab)
        {
            _previewRoot = UnityEngine.Object.Instantiate(prefab);
            _previewRoot.name = "SpritePreviewRoot";
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;
            _previewRoot.transform.position = Vector3.zero;
            _previewRoot.transform.rotation = Quaternion.identity;
            _previewRoot.transform.localScale = prefab.transform.localScale;
            _preview.AddSingleGO(_previewRoot);
        }

        private void CollectPreviewContent()
        {
            _previewRoot.GetComponentsInChildren(true, _spriteRenderers);
            _previewRoot.GetComponentsInChildren(true, _particleSystems);
            _previewRoot.GetComponentsInChildren(true, _renderers);
            _previewRoot.GetComponentsInChildren(true, _colliders2D);
            _previewRoot.GetComponentsInChildren(true, _spriteMasks);
            _previewRoot.GetComponentsInChildren(true, _sortingGroups);

            _rendererInitialStates.Clear();
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                _rendererInitialStates.Add(renderer != null && renderer.enabled);
            }

            CollectColliderSpriteRenderers();
            _hasParticles = _particleSystems.Count > 0;
        }

        private void CollectColliderSpriteRenderers()
        {
            _colliderSpriteRenderers.Clear();
            _colliderSpriteOriginalColors.Clear();

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                if (spriteRenderer.GetComponent<Collider2D>() == null)
                    continue;

                _colliderSpriteRenderers.Add(spriteRenderer);
                _colliderSpriteOriginalColors.Add(spriteRenderer.color);
            }
        }

        private void RefreshColliderSpriteTint()
        {
            for (int i = 0; i < _colliderSpriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _colliderSpriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                Color originalColor = i < _colliderSpriteOriginalColors.Count
                    ? _colliderSpriteOriginalColors[i]
                    : spriteRenderer.color;
                spriteRenderer.color = _colliderOverlayEnabled
                    ? ComputeColliderPreviewTintForTests(originalColor)
                    : originalColor;
            }
        }

        private void RestoreColliderSpriteColors()
        {
            for (int i = 0; i < _colliderSpriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _colliderSpriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                if (i < _colliderSpriteOriginalColors.Count)
                    spriteRenderer.color = _colliderSpriteOriginalColors[i];
            }
        }

        private void PrepareParticleSystems()
        {
            for (int i = 0; i < _particleSystems.Count; i++)
            {
                ParticleSystem system = _particleSystems[i];
                if (system == null)
                    continue;

                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.useUnscaledTime = true;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Clear(true);
                system.useAutoRandomSeed = false;
                system.randomSeed = (uint)(101 + i);
            }
        }

        private void FrameCameraToContent()
        {
            if (!TryComputeFramingBounds(out Bounds bounds))
                bounds = new Bounds(Vector3.zero, Vector3.one * 2f);

            _framedBounds = bounds;
            _hasFramedBounds = true;
            _pivot = bounds.center;
            _targetPivot = _pivot;

            if (ModeContext.CameraIs2D)
            {
                _orthoSize = Mathf.Clamp(ComputeInitialOrthoSizeForBoundsForTests(bounds, 1f), MinOrthoSize, MaxOrthoSize);
                _targetOrthoSize = _orthoSize;
            }
            else
            {
                _distance = Mathf.Clamp(ComputeInitialDistanceForBoundsForTests(bounds, _preview.camera.fieldOfView), MinDistance, MaxDistance);
                _targetDistance = _distance;
                _orbit = new Vector2(35f, 18f);
                _targetOrbit = _orbit;
            }
        }

        private bool TryComputeFramingBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer == null || spriteRenderer.sprite == null || !spriteRenderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = spriteRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(spriteRenderer.bounds);
                }
            }

            return hasBounds;
        }

        private void UpdateCameraTransform(Rect previewRect)
        {
            Camera camera = _preview.camera;
            if (ModeContext.CameraIs2D)
            {
                float aspect = previewRect.height > 0.0001f ? previewRect.width / previewRect.height : 1f;
                float minSize = Mathf.Clamp(ComputeInitialOrthoSizeForBoundsForTests(_framedBounds, aspect), MinOrthoSize, MaxOrthoSize) * 0.35f;
                _orthoSize = Mathf.Max(_orthoSize, minSize);
                _targetOrthoSize = Mathf.Max(_targetOrthoSize, minSize);

                camera.orthographic = true;
                camera.orthographicSize = _orthoSize;
                camera.transform.position = new Vector3(_pivot.x, _pivot.y, _pivot.z - CameraDepthOffset);
                camera.transform.rotation = Quaternion.identity;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = Mathf.Max(100f, CameraDepthOffset * 20f);
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

        private void DrawGrid()
        {
            var request = new PreviewGridDrawRequest(
                _preview,
                ModeContext.CameraIs2D ? PreviewGridSpace.Plane2D : PreviewGridSpace.Plane3D,
                _gridEnabled,
                gridTransformOverride: Matrix4x4.identity);
            PreviewGridSystem.Draw(request);
        }

        private void PanPreviewTarget(Vector2 delta, Rect previewRect, bool effective2D)
        {
            if (effective2D)
            {
                float safeHeight = Mathf.Max(1f, previewRect.height);
                float aspect = previewRect.height > 0.0001f ? previewRect.width / previewRect.height : 1f;
                float unitsPerPixelY = (_targetOrthoSize * 2f) / safeHeight;
                float unitsPerPixelX = unitsPerPixelY * aspect;
                _targetPivot += new Vector3(-delta.x * unitsPerPixelX, delta.y * unitsPerPixelY, 0f);
                return;
            }

            Camera camera = _preview != null ? _preview.camera : null;
            if (camera == null)
                return;

            float safeHeight3D = Mathf.Max(1f, previewRect.height);
            float distanceScale = Mathf.Max(_targetDistance, MinDistance);
            float worldPerPixel = 2f * distanceScale * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) / safeHeight3D;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            _targetPivot += (-right * delta.x + -up * delta.y) * worldPerPixel;
        }

        private bool ComputeHasPendingCameraMotion()
        {
            bool effective2D = ModeContext.CameraIs2D;
            var state = new PreviewCameraInteractionState
            {
                Orbit = _orbit,
                TargetOrbit = _targetOrbit,
                OrbitAngularVelocity = _orbitAngularVelocity,
                Distance = effective2D ? _orthoSize : _distance,
                TargetDistance = effective2D ? _targetOrthoSize : _targetDistance,
                Pivot = _pivot,
                TargetPivot = _targetPivot,
                IsOrbitDragging = _isOrbitDragging,
                LastOrbitInputTime = _lastOrbitInputTime,
            };

            return PreviewCameraController.HasPendingMotion(state, CameraInteractionConfig);
        }

        private void CacheCurrentSessionState()
        {
            if (string.IsNullOrEmpty(_prefabAssetPath))
                return;

            SessionStateByAssetPath[_prefabAssetPath] = new SessionStateSnapshot
            {
                Pivot = _pivot,
                TargetPivot = _targetPivot,
                OrthoSize = _orthoSize,
                TargetOrthoSize = _targetOrthoSize,
                Orbit = _orbit,
                TargetOrbit = _targetOrbit,
                Distance = _distance,
                TargetDistance = _targetDistance,
                ModeOverride = _modeOverride,
                GridEnabled = _gridEnabled,
                BoundsOverlayEnabled = _boundsOverlayEnabled,
                ColliderOverlayEnabled = _colliderOverlayEnabled,
                SavedAt = EditorApplication.timeSinceStartup,
            };

            if (SessionStateByAssetPath.Count <= MaxCachedSessionStates)
                return;

            string oldestKey = null;
            double oldestSavedAt = double.MaxValue;
            foreach (KeyValuePair<string, SessionStateSnapshot> pair in SessionStateByAssetPath)
            {
                if (pair.Value.SavedAt < oldestSavedAt)
                {
                    oldestSavedAt = pair.Value.SavedAt;
                    oldestKey = pair.Key;
                }
            }

            if (!string.IsNullOrEmpty(oldestKey))
                SessionStateByAssetPath.Remove(oldestKey);
        }

        private static bool TryRestoreSessionState(string assetPath, out SessionStateSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(assetPath) && SessionStateByAssetPath.TryGetValue(assetPath, out snapshot))
            {
                double age = EditorApplication.timeSinceStartup - snapshot.SavedAt;
                if (age <= SessionRestoreWindowSeconds)
                    return true;

                SessionStateByAssetPath.Remove(assetPath);
            }

            snapshot = default;
            return false;
        }

        private static PreviewModeOverride NormalizeModeOverride(PreviewModeOverride modeOverride)
        {
            return modeOverride == PreviewModeOverride.Force3D
                ? PreviewModeOverride.Force3D
                : PreviewModeOverride.Force2D;
        }

        internal static void ClearSessionStateCache()
        {
            SessionStateByAssetPath.Clear();
            s_lastSetupAssetPath = null;
        }
    }
}
