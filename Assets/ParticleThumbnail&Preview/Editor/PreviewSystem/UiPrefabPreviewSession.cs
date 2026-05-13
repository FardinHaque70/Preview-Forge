using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
    // Builds and renders RectTransform-based UI prefab previews in an isolated preview scene.
    internal sealed class UiPrefabPreviewSession : IPreviewToolbarCommonSession
    {
        private const double SessionRestoreWindowSeconds = 2.0d;
        private const int MaxCachedSessionStates = 64;
        private const float MinOrthographicSize = 0.05f;
        private const float MaxOrthographicSize = 2000f;
        private const float ZoomFactorPerScrollUnit = 0.1f;
        private const float PanScaleFactor = 2f;
        private const float DefaultCameraDistance = 10f;
        private const float BoundsPaddingFactor = 1.15f;
        private const float SizeEpsilon = 0.0001f;
        private const float UiPlaneEpsilon = 0.0001f;
        private const float CheckerTileWorldSize = 40f;
        private const float CheckerDepthOffset = 50f;
        private const string CheckerTextureRelativePath = "Editor/Common/PreviewAssets/Checkerboard.png";
        private const string CheckerShaderRelativePath = "Editor/Common/PreviewAssets/UiPreviewChecker.shader";
        private const string CheckerShaderName = "Hidden/PrefabPreview/UiChecker";
        private static readonly Color CheckerTintColor = new(0.30f, 0.30f, 0.30f, 0.20f);
        private const float MinUiGridAlpha = 0.04f;
        private const float MaxUiGridHalfSize = 5000f;

        private static readonly MethodInfo LayoutRebuilderForceRebuildMethod = ResolveLayoutRebuilderForceRebuildMethod();
        private static readonly Type UiGraphicType = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI");
        private static readonly Dictionary<string, SessionStateSnapshot> SessionStateByAssetPath = new();
        private static readonly Vector3[] RectWorldCorners = new Vector3[4];
        private static string s_lastSetupAssetPath;
        private static Texture2D s_checkerTexture;
        private static Material s_checkerMaterial;
        private static Mesh s_checkerQuadMesh;

        private readonly List<RectTransform> _rectTransforms = new();
        private readonly List<Canvas> _canvases = new();
        private readonly List<RectTransform> _layoutRoots = new();

        private PreviewRenderUtility _preview;
        private GameObject _previewRoot;
        private GameObject _canvasWrapper;
        private string _prefabAssetPath;
        private int _prefabInstanceId;

        private Vector3 _pivot;
        private Vector3 _targetPivot;
        private float _orthographicSize = 5f;
        private float _targetOrthographicSize = 5f;
        private bool _gridEnabled = PreviewSettings.SharedGridDefaultEnabled;
        private bool _boundsOverlayEnabled = PreviewSettings.SharedBoundsRulerDefaultEnabled;
        private Bounds _framedBounds;
        private bool _hasFramedBounds;
        private Vector2 _uiRulerSize;
        private bool _hasUiRulerSize;
        private double _lastInteractionUpdateTime = -1d;
        private bool _initialFitPending;

        private struct SessionStateSnapshot
        {
            internal Vector3 Pivot;
            internal Vector3 TargetPivot;
            internal float OrthographicSize;
            internal float TargetOrthographicSize;
            internal bool GridEnabled;
            internal bool BoundsOverlayEnabled;
            internal double SavedAt;
        }

        internal bool IsReady => _preview != null && _previewRoot != null;
        internal bool GridEnabled => _gridEnabled;
        internal bool BoundsOverlayEnabled => _boundsOverlayEnabled;
        internal bool HasPendingCameraMotion => ComputeHasPendingCameraMotion();
        internal int RectTransformCount => _rectTransforms.Count;
        internal int CanvasCount => _canvases.Count;
        internal Vector3 BoundsSize => _hasFramedBounds ? _framedBounds.size : Vector3.zero;
        internal PreviewModeOverride ModeOverride => PreviewModeOverride.Force2D;
        internal PreviewModeContext ModeContext => PreviewModeResolver.Resolve(PreviewModeOverride.Force2D);

        internal static bool ShouldCreateCanvasWrapperForTests(bool hasCanvas)
        {
            return !hasCanvas;
        }

        internal static float ComputeOrthoSizeForBoundsForTests(Bounds bounds, float aspect)
        {
            float safeAspect = Mathf.Max(0.01f, aspect);
            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / safeAspect);
            return Mathf.Max(halfHeight * BoundsPaddingFactor, MinOrthographicSize);
        }

        internal static PreviewGridProfile BuildUiGridProfileForTests(Bounds bounds, float orthographicSize, float aspect)
        {
            float safeAspect = Mathf.Max(0.01f, aspect);
            float contentHalf = Mathf.Max(bounds.extents.x, bounds.extents.y, 1f);
            float viewHalfHeight = Mathf.Max(orthographicSize * 1.35f, 1f);
            float viewHalfWidth = viewHalfHeight * safeAspect;
            float halfSize = Mathf.Clamp(Mathf.Max(contentHalf * 1.3f, viewHalfHeight, viewHalfWidth), 20f, MaxUiGridHalfSize);
            float step = Mathf.Clamp(CalculateStepSize(halfSize), PreviewSettings.MinSharedGridStep, PreviewSettings.MaxSharedGridStep);
            float alpha = Mathf.Clamp(Mathf.Max(PreviewSettings.SharedGridAlpha * 0.35f, MinUiGridAlpha), PreviewSettings.MinSharedGridAlpha, PreviewSettings.MaxSharedGridAlpha);
            return new PreviewGridProfile(PreviewSettings.SharedGridDefaultEnabled, halfSize, step, alpha, PreviewGridStyle.Classic);
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
            get => false;
            set { }
        }

        PreviewModeOverride IPreviewToolbarCommonSession.ModeOverride => ModeOverride;
        PreviewModeContext IPreviewToolbarCommonSession.ModeContext => ModeContext;

        void IPreviewToolbarCommonSession.CycleModeOverride()
        {
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
            _preview.camera.orthographic = true;
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 2000f;

            BuildPreviewRoot(prefab);
            CollectPreviewContent();
            NormalizeRectTransformsToUiPlane();
            ConfigureCanvases();
            ForceUiLayoutUpdate();
            CenterPreviewRootToContent();
            ForceUiLayoutUpdate();
            FrameCameraToContent();
            _initialFitPending = true;

            _gridEnabled = PreviewSettings.SharedGridDefaultEnabled;
            _boundsOverlayEnabled = PreviewSettings.SharedBoundsRulerDefaultEnabled;

            bool shouldRestoreState = !isSwitchingToDifferentPrefab && isTransientRebuildOfSameSelection;
            if (shouldRestoreState && TryRestoreSessionState(assetPath, out SessionStateSnapshot restored))
            {
                _pivot = restored.Pivot;
                _targetPivot = restored.TargetPivot;
                _orthographicSize = Mathf.Clamp(restored.OrthographicSize, MinOrthographicSize, MaxOrthographicSize);
                _targetOrthographicSize = Mathf.Clamp(restored.TargetOrthographicSize, MinOrthographicSize, MaxOrthographicSize);
                _gridEnabled = restored.GridEnabled;
                _boundsOverlayEnabled = restored.BoundsOverlayEnabled;
                _initialFitPending = false;
            }

            _prefabInstanceId = instanceId;
            _prefabAssetPath = assetPath;
            s_lastSetupAssetPath = assetPath;
            _lastInteractionUpdateTime = -1d;
        }

        internal void Cleanup(bool cacheState)
        {
            if (cacheState)
                CacheCurrentSessionState();

            _rectTransforms.Clear();
            _canvases.Clear();
            _layoutRoots.Clear();
            _prefabInstanceId = 0;
            _prefabAssetPath = null;
            _hasFramedBounds = false;
            _hasUiRulerSize = false;
            _lastInteractionUpdateTime = -1d;
            _initialFitPending = false;

            if (_previewRoot != null)
                UnityEngine.Object.DestroyImmediate(_previewRoot);
            _previewRoot = null;

            if (_canvasWrapper != null)
                UnityEngine.Object.DestroyImmediate(_canvasWrapper);
            _canvasWrapper = null;

            _preview?.Cleanup();
            _preview = null;
        }

        internal bool HandleInput(Rect previewRect, Event evt)
        {
            if (!IsReady || evt == null)
                return false;

            if (GUIUtility.hotControl != 0)
                return false;

            bool pointerInPreview = previewRect.Contains(evt.mousePosition);
            if (!pointerInPreview && evt.type != EventType.MouseUp)
                return false;

            switch (evt.type)
            {
                case EventType.MouseDrag:
                    if (evt.button == 0 || evt.button == 2)
                    {
                        Pan(evt.delta, previewRect);
                        evt.Use();
                        return true;
                    }

                    break;

                case EventType.ScrollWheel:
                    {
                        float nextSize = _targetOrthographicSize * (1f + evt.delta.y * ZoomFactorPerScrollUnit);
                        _targetOrthographicSize = Mathf.Clamp(nextSize, MinOrthographicSize, MaxOrthographicSize);
                        evt.Use();
                        return true;
                    }
            }

            return false;
        }

        internal bool TickInteraction()
        {
            if (!IsReady)
                return false;

            float now = (float)EditorApplication.timeSinceStartup;
            float dt = _lastInteractionUpdateTime < 0d
                ? 1f / 60f
                : Mathf.Clamp(now - (float)_lastInteractionUpdateTime, 0f, 0.05f);
            _lastInteractionUpdateTime = now;

            float panSmoothing = Mathf.Max(0.0001f, PreviewSettings.PanSmoothing);
            float zoomSmoothing = Mathf.Max(0.0001f, PreviewSettings.OrbitSmoothing);
            float panLerp = 1f - Mathf.Exp(-panSmoothing * dt);
            float zoomLerp = 1f - Mathf.Exp(-zoomSmoothing * dt);

            Vector3 beforePivot = _pivot;
            float beforeSize = _orthographicSize;
            _pivot = Vector3.Lerp(_pivot, _targetPivot, panLerp);
            _orthographicSize = Mathf.Lerp(_orthographicSize, _targetOrthographicSize, zoomLerp);

            return (beforePivot - _pivot).sqrMagnitude > SizeEpsilon * SizeEpsilon
                   || Mathf.Abs(beforeSize - _orthographicSize) > SizeEpsilon;
        }

        internal void Draw(Rect previewRect, GUIStyle background)
        {
            if (!IsReady || Event.current.type != EventType.Repaint)
                return;

            _preview.camera.backgroundColor = PreviewSettings.BackgroundColor;
            ForceUiLayoutUpdate();
            RefreshFramingBounds();
            UpdateCameraTransform(previewRect);

            _preview.BeginPreview(previewRect, background ?? GUIStyle.none);
            DrawBackgroundChecker(previewRect);

            PreviewRenderCompatibilityUtility.RenderPreviewWithCameraPath(_preview);

            Texture previewTexture = _preview.EndPreview();
            if (previewTexture != null)
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture, null, ScaleMode.StretchToFill);

            if (_boundsOverlayEnabled && _hasFramedBounds)
            {
                Vector2 rulerSize = _hasUiRulerSize ? _uiRulerSize : new Vector2(_framedBounds.size.x, _framedBounds.size.y);
                PreviewBoundsVisualizer.DrawLabels2D(previewRect, _preview.camera, _framedBounds, rulerSize.x, rulerSize.y);
            }
        }

        internal void SetGridEnabled(bool enabled)
        {
            _gridEnabled = enabled;
        }

        internal void SetBoundsOverlayEnabled(bool enabled)
        {
            _boundsOverlayEnabled = enabled;
        }

        private void BuildPreviewRoot(GameObject prefab)
        {
            _previewRoot = UnityEngine.Object.Instantiate(prefab);
            _previewRoot.name = "UiPreviewRoot";
            _previewRoot.hideFlags = HideFlags.HideAndDontSave;

            bool hasCanvas = _previewRoot.GetComponentInChildren<Canvas>(true) != null;
            if (ShouldCreateCanvasWrapperForTests(hasCanvas))
            {
                _canvasWrapper = new GameObject("UiPreviewCanvasWrapper", typeof(RectTransform), typeof(Canvas));
                _canvasWrapper.hideFlags = HideFlags.HideAndDontSave;
                _previewRoot.transform.SetParent(_canvasWrapper.transform, worldPositionStays: false);
                _preview.AddSingleGO(_canvasWrapper);
            }
            else
            {
                _preview.AddSingleGO(_previewRoot);
            }
        }

        private void CollectPreviewContent()
        {
            GameObject root = _canvasWrapper != null ? _canvasWrapper : _previewRoot;
            if (root == null)
                return;

            root.GetComponentsInChildren(true, _rectTransforms);
            root.GetComponentsInChildren(true, _canvases);

            for (int i = 0; i < _rectTransforms.Count; i++)
            {
                RectTransform rectTransform = _rectTransforms[i];
                if (rectTransform == null)
                    continue;

                if (rectTransform.parent == null || rectTransform.parent.GetComponentInParent<RectTransform>() == null)
                    _layoutRoots.Add(rectTransform);
            }
        }

        private void ConfigureCanvases()
        {
            for (int i = 0; i < _canvases.Count; i++)
            {
                Canvas canvas = _canvases[i];
                if (canvas == null)
                    continue;

                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = _preview.camera;
                if (canvas.planeDistance <= 0f)
                    canvas.planeDistance = 1f;
            }
        }

        private void NormalizeRectTransformsToUiPlane()
        {
            for (int i = 0; i < _rectTransforms.Count; i++)
            {
                RectTransform rectTransform = _rectTransforms[i];
                if (rectTransform == null)
                    continue;

                Vector3 localPosition = rectTransform.localPosition;
                if (Mathf.Abs(localPosition.z) <= UiPlaneEpsilon)
                    continue;

                localPosition.z = 0f;
                rectTransform.localPosition = localPosition;
            }
        }

        private void ForceUiLayoutUpdate()
        {
            for (int i = 0; i < _canvases.Count; i++)
            {
                Canvas canvas = _canvases[i];
                if (canvas == null)
                    continue;

                canvas.worldCamera = _preview.camera;
            }

            Canvas.ForceUpdateCanvases();

            if (LayoutRebuilderForceRebuildMethod == null)
                return;

            for (int i = 0; i < _layoutRoots.Count; i++)
            {
                RectTransform rectTransform = _layoutRoots[i];
                if (rectTransform == null)
                    continue;

                try
                {
                    LayoutRebuilderForceRebuildMethod.Invoke(null, new object[] { rectTransform });
                }
                catch
                {
                    // Keep preview resilient when UI package versions differ.
                }
            }

            Canvas.ForceUpdateCanvases();
        }

        private void FrameCameraToContent()
        {
            if (!TryComputeFramingBounds(out Bounds bounds))
                bounds = new Bounds(Vector3.zero, Vector3.one * 2f);

            _framedBounds = bounds;
            _hasFramedBounds = true;
            _pivot = bounds.center;
            _targetPivot = _pivot;
            _orthographicSize = Mathf.Clamp(ComputeOrthoSizeForBoundsForTests(bounds, 1f), MinOrthographicSize, MaxOrthographicSize);
            _targetOrthographicSize = _orthographicSize;
        }

        private void RefreshFramingBounds()
        {
            _hasUiRulerSize = false;
            if (TryComputeFramingBounds(out Bounds bounds))
            {
                _framedBounds = bounds;
                _hasFramedBounds = true;
                _uiRulerSize = new Vector2(bounds.size.x, bounds.size.y);
                _hasUiRulerSize = true;
            }
        }

        private bool TryComputeFramingBounds(out Bounds bounds)
        {
            if (TryComputeRenderableBounds(out bounds))
                return true;

            return TryComputeAllRectTransformBounds(out bounds);
        }

        private bool TryComputeRenderableBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _rectTransforms.Count; i++)
            {
                RectTransform rectTransform = _rectTransforms[i];
                if (!IsRectTransformRenderableForBounds(rectTransform))
                    continue;

                rectTransform.GetWorldCorners(RectWorldCorners);
                for (int c = 0; c < RectWorldCorners.Length; c++)
                {
                    Vector3 corner = RectWorldCorners[c];
                    if (!hasBounds)
                    {
                        bounds = new Bounds(corner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(corner);
                    }
                }
            }

            return hasBounds;
        }

        private bool TryComputeAllRectTransformBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            for (int i = 0; i < _rectTransforms.Count; i++)
            {
                RectTransform rectTransform = _rectTransforms[i];
                if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                    continue;

                rectTransform.GetWorldCorners(RectWorldCorners);
                for (int c = 0; c < RectWorldCorners.Length; c++)
                {
                    Vector3 corner = RectWorldCorners[c];
                    if (!hasBounds)
                    {
                        bounds = new Bounds(corner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(corner);
                    }
                }
            }

            return hasBounds;
        }

        private void UpdateCameraTransform(Rect previewRect)
        {
            Camera camera = _preview.camera;
            camera.orthographic = true;
            camera.transform.position = new Vector3(_pivot.x, _pivot.y, _pivot.z - DefaultCameraDistance);
            camera.transform.rotation = Quaternion.identity;

            float aspect = Mathf.Max(0.01f, previewRect.width / Mathf.Max(1f, previewRect.height));
            float fittedSize = ComputeOrthoSizeForBoundsForTests(_framedBounds, aspect);
            if (_initialFitPending)
            {
                _orthographicSize = fittedSize;
                _targetOrthographicSize = fittedSize;
                _initialFitPending = false;
            }

            camera.orthographicSize = Mathf.Clamp(_orthographicSize, MinOrthographicSize, MaxOrthographicSize);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5000f;
        }

        private void DrawBackgroundChecker(Rect previewRect)
        {
            if (!_gridEnabled)
                return;

            EnsureCheckerResources();
            if (s_checkerTexture == null || s_checkerMaterial == null || s_checkerQuadMesh == null)
                return;

            float aspect = Mathf.Max(0.01f, previewRect.width / Mathf.Max(1f, previewRect.height));
            float halfHeight = _preview.camera.orthographicSize * 1.25f;
            float halfWidth = halfHeight * aspect;
            Vector3 center = new Vector3(_pivot.x, _pivot.y, _pivot.z + CheckerDepthOffset);
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(halfWidth * 2f, halfHeight * 2f, 1f));

            float tilesX = Mathf.Max(1f, (halfWidth * 2f) / CheckerTileWorldSize);
            float tilesY = Mathf.Max(1f, (halfHeight * 2f) / CheckerTileWorldSize);
            float offsetX = (center.x / CheckerTileWorldSize) - (tilesX * 0.5f);
            float offsetY = (center.y / CheckerTileWorldSize) - (tilesY * 0.5f);

            Vector2 scale = new Vector2(tilesX, tilesY);
            Vector2 offset = new Vector2(offsetX, offsetY);
            s_checkerMaterial.mainTextureScale = scale;
            s_checkerMaterial.mainTextureOffset = offset;
            if (s_checkerMaterial.HasProperty("_MainTex"))
            {
                s_checkerMaterial.SetTextureScale("_MainTex", scale);
                s_checkerMaterial.SetTextureOffset("_MainTex", offset);
            }
            _preview.DrawMesh(s_checkerQuadMesh, matrix, s_checkerMaterial, 0);
        }

        private void Pan(Vector2 delta, Rect previewRect)
        {
            Camera camera = _preview != null ? _preview.camera : null;
            if (camera == null)
                return;

            float width = Mathf.Max(1f, previewRect.width);
            float height = Mathf.Max(1f, previewRect.height);
            float worldHeight = camera.orthographicSize * PanScaleFactor;
            float worldWidth = worldHeight * (width / height);
            float panRight = -delta.x / width * worldWidth;
            float panUp = delta.y / height * worldHeight;
            _targetPivot += new Vector3(panRight, panUp, 0f);
        }

        private bool ComputeHasPendingCameraMotion()
        {
            return (_pivot - _targetPivot).sqrMagnitude > SizeEpsilon * SizeEpsilon
                   || Mathf.Abs(_orthographicSize - _targetOrthographicSize) > SizeEpsilon;
        }

        private void CacheCurrentSessionState()
        {
            if (string.IsNullOrEmpty(_prefabAssetPath))
                return;

            SessionStateByAssetPath[_prefabAssetPath] = new SessionStateSnapshot
            {
                Pivot = _pivot,
                TargetPivot = _targetPivot,
                OrthographicSize = _orthographicSize,
                TargetOrthographicSize = _targetOrthographicSize,
                GridEnabled = _gridEnabled,
                BoundsOverlayEnabled = _boundsOverlayEnabled,
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

        internal static void ClearSessionStateCache()
        {
            SessionStateByAssetPath.Clear();
            s_lastSetupAssetPath = null;
        }

        private static MethodInfo ResolveLayoutRebuilderForceRebuildMethod()
        {
            Type layoutRebuilderType = Type.GetType("UnityEngine.UI.LayoutRebuilder, UnityEngine.UI");
            return layoutRebuilderType?.GetMethod(
                "ForceRebuildLayoutImmediate",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(RectTransform) },
                null);
        }

        private void CenterPreviewRootToContent()
        {
            if (!TryComputeRenderableBounds(out Bounds bounds))
                return;

            Vector3 offset = -bounds.center;
            Transform rootTransform = _canvasWrapper != null ? _canvasWrapper.transform : _previewRoot.transform;
            rootTransform.position += offset;
        }

        private bool IsRectTransformRenderableForBounds(RectTransform rectTransform)
        {
            if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                return false;

            if (rectTransform.GetComponent<CanvasRenderer>() != null)
                return true;

            if (UiGraphicType != null && rectTransform.GetComponent(UiGraphicType) != null)
                return true;

            return false;
        }

        private static float CalculateStepSize(float halfSize)
        {
            float targetLines = 12f;
            float rawStep = Mathf.Max(0.05f, halfSize * 2f / targetLines);
            float exponent = Mathf.Floor(Mathf.Log10(rawStep));
            float magnitude = Mathf.Pow(10f, exponent);
            float normalized = rawStep / magnitude;

            float snapped;
            if (normalized <= 1f)
                snapped = 1f;
            else if (normalized <= 2f)
                snapped = 2f;
            else if (normalized <= 5f)
                snapped = 5f;
            else
                snapped = 10f;

            return snapped * magnitude;
        }

        private static void EnsureCheckerResources()
        {
            if (s_checkerTexture == null)
            {
                s_checkerTexture = PreviewInstallLayout.LoadFirstAssetAtRelativePath<Texture2D>(CheckerTextureRelativePath);
                if (s_checkerTexture == null)
                    s_checkerTexture = CreateCheckerTexture();
            }

            if (s_checkerMaterial == null)
            {
                Shader shader = PreviewInstallLayout.LoadFirstAssetAtRelativePath<Shader>(CheckerShaderRelativePath);
                if (shader == null)
                    shader = Shader.Find(CheckerShaderName);
                if (shader == null)
                    shader = Shader.Find("Unlit/Transparent");
                if (shader == null)
                    shader = Shader.Find("Unlit/Texture");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    return;

                s_checkerMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = s_checkerTexture,
                    renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry,
                };
            }

            if (s_checkerMaterial != null)
            {
                s_checkerMaterial.mainTexture = s_checkerTexture;
                if (s_checkerMaterial.HasProperty("_MainTex"))
                    s_checkerMaterial.SetTexture("_MainTex", s_checkerTexture);
                s_checkerMaterial.mainTexture.wrapMode = TextureWrapMode.Repeat;
                s_checkerMaterial.mainTexture.filterMode = FilterMode.Bilinear;
                s_checkerMaterial.color = CheckerTintColor;
                if (s_checkerMaterial.HasProperty("_Color"))
                    s_checkerMaterial.SetColor("_Color", CheckerTintColor);
                if (s_checkerMaterial.HasProperty("_BaseColor"))
                    s_checkerMaterial.SetColor("_BaseColor", CheckerTintColor);
                if (s_checkerMaterial.HasProperty("_TintColor"))
                    s_checkerMaterial.SetColor("_TintColor", CheckerTintColor);
                if (s_checkerMaterial.HasProperty("_BackgroundColor"))
                    s_checkerMaterial.SetColor("_BackgroundColor", PreviewSettings.BackgroundColor);
                s_checkerMaterial.SetPass(0);
                if (s_checkerMaterial.HasProperty("_Cull"))
                    s_checkerMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                if (s_checkerMaterial.HasProperty("_ZWrite"))
                    s_checkerMaterial.SetInt("_ZWrite", 0);
            }

            if (s_checkerQuadMesh == null)
                s_checkerQuadMesh = CreateCheckerQuadMesh();
        }

        private static Texture2D CreateCheckerTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UiPreviewChecker",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            Color light = new Color(0.115f, 0.115f, 0.115f, 1f);
            Color dark = new Color(0.10f, 0.10f, 0.10f, 1f);
            int cell = size / 4;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isLight = ((x / cell) + (y / cell)) % 2 == 0;
                    texture.SetPixel(x, y, isLight ? light : dark);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static Mesh CreateCheckerQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "UiPreviewCheckerQuad",
                hideFlags = HideFlags.HideAndDontSave,
            };

            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 0, 2, 3,
                0, 2, 1, 0, 3, 2,
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
