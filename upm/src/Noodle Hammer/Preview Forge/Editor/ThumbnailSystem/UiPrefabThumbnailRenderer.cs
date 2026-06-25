using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
// Renders Canvas and loose RectTransform-based UI prefabs into framed orthographic thumbnails.

namespace NoodleHammer.PreviewForge.Editor
{
    internal readonly struct UiGraphicFrameData
    {
        public readonly Vector3 Center;
        public readonly Vector3 Right;
        public readonly Vector3 Up;
        public readonly Vector3 Forward;
        public readonly Vector2 PlaneExtents;
        public readonly float DepthExtent;

        public UiGraphicFrameData(
            Vector3 center,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            Vector2 planeExtents,
            float depthExtent)
        {
            Center = center;
            Right = right;
            Up = up;
            Forward = forward;
            PlaneExtents = planeExtents;
            DepthExtent = depthExtent;
        }
    }

    internal sealed class UiPrefabThumbnailRenderer : IPrefabThumbnailRenderer
    {
        private const int UiPreviewLayer = 30;
        private const int ScanRenderSize = 64;
        private const byte VisibleAlphaThreshold = 6;
        private const byte BackgroundColorTolerance = 6;
        private const float TargetFillRatio = 0.82f;
        private const float FillAdjustmentEpsilon = 0.03f;
        private const float CenterAdjustmentEpsilon = 0.02f;
        private const float MinRefineScaleRatio = 0.2f;
        private const float MaxRefineScaleRatio = 2.0f;
        private static readonly Color32 ScanBackgroundColor = new Color32(255, 0, 255, 255);
        private static readonly Type GraphicType = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI");
        private static readonly Type CanvasScalerType = Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI");
        private static readonly Type LayoutRebuilderType = Type.GetType("UnityEngine.UI.LayoutRebuilder, UnityEngine.UI");
        private static readonly MethodInfo SetAllDirtyMethod = GraphicType?.GetMethod("SetAllDirty", BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo MarkLayoutForRebuildMethod = LayoutRebuilderType?.GetMethod(
            "MarkLayoutForRebuild",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(RectTransform) },
            null);
        private static readonly MethodInfo ForceRebuildLayoutImmediateMethod = LayoutRebuilderType?.GetMethod(
            "ForceRebuildLayoutImmediate",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(RectTransform) },
            null);

        public PrefabThumbnailAssetKind Kind => PrefabThumbnailAssetKind.UiPrefab;
        public int Priority => 200;

        public PrefabThumbnailSupportInfo GetSupportInfo(GameObject prefab, string guid, string assetPath)
        {
            if (!HasUnityUiSupport || prefab == null)
                return PrefabThumbnailSupportInfo.Unsupported;

            if (PrefabThumbnailDetection.IsParticlePrefab(prefab))
                return PrefabThumbnailSupportInfo.Unsupported;

            bool hasRectTransform = prefab.GetComponentInChildren<RectTransform>(true) != null;
            bool hasSprite = prefab.GetComponentInChildren<SpriteRenderer>(true) != null;
            bool hasMesh = prefab.GetComponentInChildren<MeshRenderer>(true) != null
                || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;

            if (!hasRectTransform || hasSprite || hasMesh)
                return PrefabThumbnailSupportInfo.Unsupported;

            Component[] graphics = GetGraphics(prefab);
            if (!HasDrawableGraphics(graphics, prefab.transform))
                return PrefabThumbnailSupportInfo.Unsupported;

            return new PrefabThumbnailSupportInfo(true, PrefabThumbnailAssetKind.UiPrefab, Priority);
        }

        public Texture2D Render(string assetPath, PrefabThumbnailSurface surface)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null || !HasUnityUiSupport)
                return null;

            int thumbnailSize = PrefabThumbnailSettings.GetRenderSize(surface);
            GameObject wrapper = null;
            GameObject instance = null;
            GameObject cameraObject = null;
            RenderTexture renderTexture = null;
            RenderTexture scanRenderTexture = null;
            Texture2D scanTexture = null;
            Texture2D output = null;

            try
            {
                wrapper = CreateWrapper();
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.SetParent(wrapper.transform, false);

                AssignLayerRecursively(wrapper, UiPreviewLayer);

                RectTransform wrapperRect = wrapper.GetComponent<RectTransform>();
                Component[] graphics = GetGraphics(wrapper);
                if (!HasDrawableGraphics(graphics, wrapper.transform))
                    return null;

                cameraObject = new GameObject("UiThumbnailCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = UiPreviewLayer,
                };

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = PrefabThumbnailSettings.BackgroundColor;
                camera.orthographic = true;
                camera.cullingMask = 1 << UiPreviewLayer;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;

                NormalizeCanvasesForPreview(wrapper, camera);
                ForceUiRefresh(wrapperRect, graphics, wrapper.transform);

                if (!TryComputeFrameData(graphics, wrapper.transform, out UiGraphicFrameData frameData))
                    return null;

                FrameCamera(camera, frameData, 1f);
                scanRenderTexture = new RenderTexture(ScanRenderSize, ScanRenderSize, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                scanRenderTexture.Create();

                scanTexture = new Texture2D(ScanRenderSize, ScanRenderSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                TightenCameraFrame(camera, frameData, scanRenderTexture, scanTexture);

                renderTexture = new RenderTexture(thumbnailSize, thumbnailSize, 24, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderTexture.Create();

                output = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                // Keep the renderer output transparent so the shared shell owns thumbnail presentation.
                RenderIntoTexture(camera, renderTexture, Color.clear, output);

                return output;
            }
            finally
            {
                if (scanTexture != null)
                    UnityEngine.Object.DestroyImmediate(scanTexture);

                if (scanRenderTexture != null)
                {
                    scanRenderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(scanRenderTexture);
                }

                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (cameraObject != null)
                    UnityEngine.Object.DestroyImmediate(cameraObject);

                if (wrapper != null)
                    UnityEngine.Object.DestroyImmediate(wrapper);
                else if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static GameObject CreateWrapper()
        {
            GameObject wrapper = new GameObject("UiThumbnailRoot", typeof(RectTransform), typeof(Canvas))
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = UiPreviewLayer,
            };

            if (CanvasScalerType != null)
                wrapper.AddComponent(CanvasScalerType);

            RectTransform rectTransform = wrapper.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(1024f, 1024f);
            rectTransform.position = Vector3.zero;
            rectTransform.rotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one * 0.01f;

            Canvas canvas = wrapper.GetComponent<Canvas>();
            canvas.pixelPerfect = false;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            canvas.sortingOrder = 0;

            return wrapper;
        }

        private static Component[] GetGraphics(GameObject root)
        {
            return root == null || GraphicType == null
                ? Array.Empty<Component>()
                : root.GetComponentsInChildren(GraphicType, true);
        }

        private static bool HasDrawableGraphics(Component[] graphics, Transform previewRoot)
        {
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is Behaviour behaviour && IsDrawableGraphicForPreview(behaviour, previewRoot))
                    return true;
            }

            return false;
        }

        private static void NormalizeCanvasesForPreview(GameObject root, Camera camera)
        {
            if (root == null)
                return;

            Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 0;
                canvas.overrideSorting = false;
            }
        }

        private static void ForceUiRefresh(RectTransform root, Component[] graphics, Transform previewRoot)
        {
            if (root == null)
                return;

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is not Behaviour behaviour || !IsDrawableGraphicForPreview(behaviour, previewRoot))
                    continue;

                SetAllDirtyMethod?.Invoke(behaviour, null);
                RectTransform graphicRectTransform = behaviour.GetComponent<RectTransform>();
                if (graphicRectTransform != null)
                    MarkLayoutForRebuildMethod?.Invoke(null, new object[] { graphicRectTransform });
            }

            ForceRebuildLayoutImmediateMethod?.Invoke(null, new object[] { root });
            Canvas.ForceUpdateCanvases();
        }

        internal static Bounds ComputeGraphicBoundsForTests(Component[] graphics, Transform fallbackTransform)
        {
            return ComputeGraphicBounds(graphics, fallbackTransform);
        }

        internal static bool TryComputeFrameDataForTests(Component[] graphics, Transform fallbackTransform, out UiGraphicFrameData frameData)
        {
            return TryComputeFrameData(graphics, fallbackTransform, out frameData);
        }

        private static Bounds ComputeGraphicBounds(Component[] graphics, Transform fallbackTransform)
        {
            Bounds bounds = default;
            bool found = false;

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is not Behaviour behaviour || !IsDrawableGraphicForPreview(behaviour, fallbackTransform))
                    continue;

                RectTransform rectTransform = behaviour.GetComponent<RectTransform>();
                if (rectTransform == null)
                    continue;

                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                for (int j = 0; j < corners.Length; j++)
                {
                    if (!found)
                    {
                        bounds = new Bounds(corners[j], Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(corners[j]);
                    }
                }
            }

            if (found)
                return bounds;

            return new Bounds(fallbackTransform != null ? fallbackTransform.position : Vector3.zero, Vector3.one * 0.1f);
        }

        internal static float ComputeOrthographicSizeForBoundsForTests(Bounds bounds)
        {
            return ComputeOrthographicSizeForPlaneExtentsForTests(new Vector2(bounds.extents.x, bounds.extents.y), 1f);
        }

        internal static float ComputeOrthographicSizeForPlaneExtentsForTests(Vector2 planeExtents, float aspect)
        {
            float safeAspect = Mathf.Max(0.01f, aspect);
            Vector2 paddedExtents = planeExtents * (1f + PrefabThumbnailSettings.BoundsPadding);
            return Mathf.Max(paddedExtents.y, paddedExtents.x / safeAspect, 0.1f);
        }

        internal static bool TryComputeVisiblePixelRectForTests(Color32[] pixels, int width, int height, out RectInt pixelRect)
        {
            return TryComputeVisiblePixelRect(pixels, width, height, default, out pixelRect);
        }

        internal static bool TryComputeVisiblePixelRectForTests(Color32[] pixels, int width, int height, Color32 clearPixel, out RectInt pixelRect)
        {
            return TryComputeVisiblePixelRect(pixels, width, height, clearPixel, out pixelRect);
        }

        internal static void ComputeRefinedFrameForTests(
            Vector2 planeExtents,
            float currentOrthographicSize,
            RectInt visiblePixelRect,
            int width,
            int height,
            out float refinedOrthographicSize,
            out Vector2 normalizedCenterOffset)
        {
            ComputeRefinedFrame(planeExtents, currentOrthographicSize, visiblePixelRect, width, height, out refinedOrthographicSize, out normalizedCenterOffset);
        }

        private static bool TryComputeFrameData(Component[] graphics, Transform fallbackTransform, out UiGraphicFrameData frameData)
        {
            frameData = default;

            Vector3 referenceForward = fallbackTransform != null ? fallbackTransform.forward : Vector3.forward;
            Vector3 referenceUp = fallbackTransform != null ? fallbackTransform.up : Vector3.up;
            Vector3 referenceRight = fallbackTransform != null ? fallbackTransform.right : Vector3.right;

            Vector3 forwardSum = Vector3.zero;
            Vector3 upSum = Vector3.zero;
            Vector3 rightSum = Vector3.zero;
            bool found = false;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is not Behaviour behaviour || !IsDrawableGraphicForPreview(behaviour, fallbackTransform))
                    continue;

                RectTransform rectTransform = behaviour.GetComponent<RectTransform>();
                if (rectTransform == null)
                    continue;

                Vector3 rectForward = AlignAxis(rectTransform.forward, referenceForward);
                Vector3 rectUp = AlignAxis(rectTransform.up, referenceUp);
                Vector3 rectRight = AlignAxis(rectTransform.right, referenceRight);
                forwardSum += rectForward;
                upSum += rectUp;
                rightSum += rectRight;

                found = true;
            }

            if (!found)
                return false;

            Vector3 forward = NormalizeOrFallback(forwardSum, referenceForward, Vector3.forward);
            Vector3 right = Vector3.ProjectOnPlane(rightSum, forward);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.ProjectOnPlane(referenceRight, forward);
            if (right.sqrMagnitude < 0.0001f)
                right = Vector3.Cross(referenceUp, forward);
            right = NormalizeOrFallback(right, referenceRight, Vector3.right);

            Vector3 up = Vector3.ProjectOnPlane(upSum, forward);
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.Cross(forward, right);
            up = NormalizeOrFallback(up, referenceUp, Vector3.up);
            right = NormalizeOrFallback(Vector3.Cross(up, forward), referenceRight, Vector3.right);
            up = NormalizeOrFallback(Vector3.Cross(forward, right), referenceUp, Vector3.up);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] is not Behaviour behaviour || !IsDrawableGraphicForPreview(behaviour, fallbackTransform))
                    continue;

                RectTransform rectTransform = behaviour.GetComponent<RectTransform>();
                if (rectTransform == null)
                    continue;

                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                for (int j = 0; j < corners.Length; j++)
                {
                    Vector3 corner = corners[j];
                    float x = Vector3.Dot(corner, right);
                    float y = Vector3.Dot(corner, up);
                    float z = Vector3.Dot(corner, forward);

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                    minZ = Mathf.Min(minZ, z);
                    maxZ = Mathf.Max(maxZ, z);
                }
            }

            if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(minY) || !float.IsFinite(maxY))
                return false;

            Vector3 center =
                right * ((minX + maxX) * 0.5f) +
                up * ((minY + maxY) * 0.5f) +
                forward * ((minZ + maxZ) * 0.5f);
            Vector2 planeExtents = new Vector2(
                Mathf.Max(0.05f, (maxX - minX) * 0.5f),
                Mathf.Max(0.05f, (maxY - minY) * 0.5f));
            float depthExtent = Mathf.Max(0.01f, (maxZ - minZ) * 0.5f);

            frameData = new UiGraphicFrameData(center, right, up, forward, planeExtents, depthExtent);
            return true;
        }

        private static void FrameCamera(Camera camera, UiGraphicFrameData frameData, float aspect)
        {
            float orthographicSize = ComputeOrthographicSizeForPlaneExtentsForTests(frameData.PlaneExtents, aspect);
            ApplyCameraFrame(camera, frameData.Center, frameData.Forward, frameData.Up, orthographicSize, frameData.DepthExtent, frameData.PlaneExtents);
        }

        private static void TightenCameraFrame(Camera camera, UiGraphicFrameData frameData, RenderTexture scanRenderTexture, Texture2D scanTexture)
        {
            if (camera == null || scanRenderTexture == null || scanTexture == null)
                return;

            RenderIntoTexture(camera, scanRenderTexture, ScanBackgroundColor, scanTexture);
            Color32[] pixels = scanTexture.GetPixels32();
            if (!TryComputeVisiblePixelRect(pixels, scanTexture.width, scanTexture.height, ScanBackgroundColor, out RectInt visiblePixelRect))
                return;

            ComputeRefinedFrame(frameData.PlaneExtents, camera.orthographicSize, visiblePixelRect, scanTexture.width, scanTexture.height, out float refinedOrthographicSize, out Vector2 normalizedCenterOffset);

            if (Mathf.Abs(refinedOrthographicSize - camera.orthographicSize) <= 0.0001f
                && Mathf.Abs(normalizedCenterOffset.x) <= CenterAdjustmentEpsilon
                && Mathf.Abs(normalizedCenterOffset.y) <= CenterAdjustmentEpsilon)
            {
                return;
            }

            float horizontalSpan = camera.orthographicSize * 2f;
            float verticalSpan = camera.orthographicSize * 2f;
            Vector3 refinedCenter =
                frameData.Center +
                frameData.Right * (normalizedCenterOffset.x * horizontalSpan) +
                frameData.Up * (normalizedCenterOffset.y * verticalSpan);

            ApplyCameraFrame(camera, refinedCenter, frameData.Forward, frameData.Up, refinedOrthographicSize, frameData.DepthExtent, frameData.PlaneExtents);
        }

        private static void RenderIntoTexture(Camera camera, RenderTexture target, Color clearColor, Texture2D destinationTexture)
        {
            if (camera == null || target == null || destinationTexture == null)
                return;

            Color previousBackgroundColor = camera.backgroundColor;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.backgroundColor = clearColor;
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                destinationTexture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                destinationTexture.Apply(false, false);
            }
            finally
            {
                camera.backgroundColor = previousBackgroundColor;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }
        }

        private static bool TryComputeVisiblePixelRect(Color32[] pixels, int width, int height, Color32 clearPixel, out RectInt pixelRect)
        {
            pixelRect = default;
            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
                return false;

            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            int pixelIndex = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, pixelIndex++)
                {
                    if (!IsVisiblePixel(pixels[pixelIndex], clearPixel))
                        continue;

                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                return false;

            pixelRect = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        private static bool IsVisiblePixel(Color32 pixel, Color32 clearPixel)
        {
            int diffR = Mathf.Abs(pixel.r - clearPixel.r);
            int diffG = Mathf.Abs(pixel.g - clearPixel.g);
            int diffB = Mathf.Abs(pixel.b - clearPixel.b);
            int diffA = Mathf.Abs(pixel.a - clearPixel.a);
            bool matchesClearColor =
                diffR <= BackgroundColorTolerance &&
                diffG <= BackgroundColorTolerance &&
                diffB <= BackgroundColorTolerance &&
                diffA <= BackgroundColorTolerance;

            if (matchesClearColor)
                return false;

            return pixel.a > VisibleAlphaThreshold;
        }

        private static void ComputeRefinedFrame(
            Vector2 planeExtents,
            float currentOrthographicSize,
            RectInt visiblePixelRect,
            int width,
            int height,
            out float refinedOrthographicSize,
            out Vector2 normalizedCenterOffset)
        {
            refinedOrthographicSize = currentOrthographicSize;
            normalizedCenterOffset = Vector2.zero;

            if (width <= 0 || height <= 0 || currentOrthographicSize <= 0f || visiblePixelRect.width <= 0 || visiblePixelRect.height <= 0)
                return;

            float fillX = Mathf.Clamp01(visiblePixelRect.width / (float) width);
            float fillY = Mathf.Clamp01(visiblePixelRect.height / (float) height);
            float dominantFill = Mathf.Max(fillX, fillY);
            if (dominantFill <= 0.0001f)
                return;

            float scaleRatio = Mathf.Clamp(dominantFill / TargetFillRatio, MinRefineScaleRatio, MaxRefineScaleRatio);
            if (Mathf.Abs(dominantFill - TargetFillRatio) > FillAdjustmentEpsilon)
                refinedOrthographicSize = Mathf.Max(0.05f, currentOrthographicSize * scaleRatio);

            float centerX = (visiblePixelRect.xMin + visiblePixelRect.width * 0.5f) / width;
            float centerY = (visiblePixelRect.yMin + visiblePixelRect.height * 0.5f) / height;
            normalizedCenterOffset = new Vector2(centerX - 0.5f, centerY - 0.5f);
        }

        private static void ApplyCameraFrame(
            Camera camera,
            Vector3 center,
            Vector3 forward,
            Vector3 up,
            float orthographicSize,
            float depthExtent,
            Vector2 planeExtents)
        {
            float clipPadding = Mathf.Max(0.5f, Mathf.Max(planeExtents.x, planeExtents.y) * 0.25f);
            float distance = Mathf.Max(5f, depthExtent + clipPadding + 1f);

            camera.transform.position = center - forward * distance;
            camera.transform.rotation = Quaternion.LookRotation(forward, up);
            camera.orthographicSize = orthographicSize;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = distance + depthExtent + clipPadding;
        }

        private static Vector3 AlignAxis(Vector3 axis, Vector3 referenceAxis)
        {
            if (axis.sqrMagnitude < 0.0001f)
                return referenceAxis;

            if (referenceAxis.sqrMagnitude > 0.0001f && Vector3.Dot(axis, referenceAxis) < 0f)
                return -axis;

            return axis;
        }

        private static Vector3 NormalizeOrFallback(Vector3 candidate, Vector3 fallback, Vector3 defaultAxis)
        {
            if (candidate.sqrMagnitude >= 0.0001f)
                return candidate.normalized;

            if (fallback.sqrMagnitude >= 0.0001f)
                return fallback.normalized;

            return defaultAxis;
        }

        private static bool IsDrawableGraphicForPreview(Behaviour behaviour, Transform previewRoot)
        {
            if (behaviour == null || !behaviour.enabled)
                return false;

            Transform current = behaviour.transform;
            while (current != null)
            {
                if (!current.gameObject.activeSelf)
                    return false;

                if (current == previewRoot)
                    return true;

                current = current.parent;
            }

            return previewRoot == null;
        }

        private static void AssignLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                transforms[i].gameObject.layer = layer;
        }

        private static bool HasUnityUiSupport =>
            GraphicType != null &&
            CanvasScalerType != null &&
            LayoutRebuilderType != null &&
            SetAllDirtyMethod != null &&
            MarkLayoutForRebuildMethod != null &&
            ForceRebuildLayoutImmediateMethod != null;
    }
}
