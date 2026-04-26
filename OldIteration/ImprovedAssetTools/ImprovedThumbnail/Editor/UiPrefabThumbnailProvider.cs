using System;
using System.Reflection;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class UiPrefabThumbnailProvider : ThumbnailProviderBase
{
    private const int UiPreviewLayer = 30;
    private static readonly Type s_tmpUiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
    private static readonly Type s_graphicType = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI");
    private static readonly Type s_canvasScalerType = Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI");
    private static readonly Type s_graphicRaycasterType = Type.GetType("UnityEngine.UI.GraphicRaycaster, UnityEngine.UI");
    private static readonly Type s_layoutRebuilderType = Type.GetType("UnityEngine.UI.LayoutRebuilder, UnityEngine.UI");
    private static readonly MethodInfo s_markLayoutForRebuildMethod = s_layoutRebuilderType?.GetMethod(
        "MarkLayoutForRebuild",
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(RectTransform) },
        null);
    private static readonly MethodInfo s_forceRebuildLayoutImmediateMethod = s_layoutRebuilderType?.GetMethod(
        "ForceRebuildLayoutImmediate",
        BindingFlags.Public | BindingFlags.Static,
        null,
        new[] { typeof(RectTransform) },
        null);
    private static readonly MethodInfo s_setAllDirtyMethod = s_graphicType?.GetMethod(
        "SetAllDirty",
        BindingFlags.Public | BindingFlags.Instance);

    public override string Id => "ui-prefab";
    public override ThumbnailAssetKind AssetKind => ThumbnailAssetKind.UiPrefab;
    public override int Priority => ImprovedThumbnailSettings.UiThumbnailProviderPriority;

    protected override ThumbnailSupportInfo EvaluateSupport(UnityEngine.Object asset, string guid, string assetPath)
    {
        if (!ImprovedThumbnailSettings.EnableUiThumbnailProvider)
            return ThumbnailSupportInfo.Unsupported;

        if (!HasUnityUiSupport)
            return ThumbnailSupportInfo.Unsupported;

        GameObject prefab = asset as GameObject;
        if (prefab == null)
            return ThumbnailSupportInfo.Unsupported;

        if (ThumbnailPrefabUtility.HasRootParticleSystem(prefab))
            return ThumbnailSupportInfo.Unsupported;

        bool hasRectTransform = prefab.GetComponentInChildren<RectTransform>(true) != null;
        bool hasGraphic = s_graphicType != null && prefab.GetComponentInChildren(s_graphicType, true) != null;
        bool hasSprite = prefab.GetComponentInChildren<SpriteRenderer>(true) != null;
        bool hasMesh = prefab.GetComponentInChildren<MeshRenderer>(true) != null
            || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        bool hasTmp = HasTextMeshProUi(prefab);

        bool supported = hasRectTransform && hasGraphic && !hasSprite && !hasMesh;
        return supported
            ? new ThumbnailSupportInfo(true, hasTmp ? ThumbnailAssetKind.TmpUiPrefab : ThumbnailAssetKind.UiPrefab, Priority)
            : ThumbnailSupportInfo.Unsupported;
    }

    protected override ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        if (!HasUnityUiSupport)
            return null;

        int thumbnailSize = ImprovedThumbnailSettings.GetThumbnailSize(context.Surface);

        GameObject wrapper = null;
        GameObject instance = null;
        GameObject cameraObject = null;
        RenderTexture renderTexture = null;
        Texture2D output = null;

        try
        {
            wrapper = CreateWrapper();
            instance = UnityEngine.Object.Instantiate(context.Prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetParent(wrapper.transform, false);

            AssignLayerRecursively(wrapper, UiPreviewLayer);
            ThumbnailPrefabUtility.ForceActivateHierarchy(wrapper);
            AssignLayerRecursively(wrapper, UiPreviewLayer);

            RectTransform wrapperRect = wrapper.GetComponent<RectTransform>();
            Component[] graphics = GetGraphics(wrapper);
            if (!HasDrawableGraphics(graphics))
                return null;

            ForceUiRefresh(wrapperRect, graphics);

            Bounds bounds = ComputeGraphicBounds(graphics, wrapper.transform);

            cameraObject = new GameObject("UIThumbnailCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ImprovedThumbnailSettings.RenderBackgroundColor;
            camera.orthographic = true;
            camera.cullingMask = 1 << UiPreviewLayer;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            FrameCamera(camera, bounds);

            Canvas canvas = wrapper.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            ForceUiRefresh(wrapperRect, graphics);

            renderTexture = new RenderTexture(thumbnailSize, thumbnailSize, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            renderTexture.Create();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                output = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                output.ReadPixels(new Rect(0f, 0f, thumbnailSize, thumbnailSize), 0, 0);
                output.Apply(false, false);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
            }

            return new ThumbnailFrameSet
            {
                StaticFrame = output
            };
        }
        finally
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }

            if (cameraObject != null)
                UnityEngine.Object.DestroyImmediate(cameraObject);

            if (wrapper != null)
                UnityEngine.Object.DestroyImmediate(wrapper);

            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static GameObject CreateWrapper()
    {
        GameObject wrapper = new GameObject("UIThumbnailRoot", typeof(RectTransform), typeof(Canvas))
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        if (s_canvasScalerType != null)
            wrapper.AddComponent(s_canvasScalerType);

        if (s_graphicRaycasterType != null)
            wrapper.AddComponent(s_graphicRaycasterType);

        RectTransform rectTransform = wrapper.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(1024f, 1024f);
        rectTransform.position = Vector3.zero;
        rectTransform.rotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one * 0.01f;

        Canvas canvas = wrapper.GetComponent<Canvas>();
        canvas.pixelPerfect = false;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
        canvas.sortingOrder = 0;

        Component scaler = s_canvasScalerType == null ? null : wrapper.GetComponent(s_canvasScalerType);
        if (scaler != null)
        {
            SetEnumMember(scaler, "uiScaleMode", "ConstantPixelSize");
            SetNumericMember(scaler, "scaleFactor", 1f);
            SetNumericMember(scaler, "referencePixelsPerUnit", 100f);
        }

        return wrapper;
    }

    private static Component[] GetGraphics(GameObject root)
    {
        return root == null || s_graphicType == null
            ? Array.Empty<Component>()
            : root.GetComponentsInChildren(s_graphicType, true);
    }

    private static bool HasDrawableGraphics(Component[] graphics)
    {
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] is Behaviour behaviour && behaviour.enabled)
                return true;
        }

        return false;
    }

    private static void ForceUiRefresh(RectTransform root, Component[] graphics)
    {
        if (root == null)
            return;

        for (int i = 0; i < graphics.Length; i++)
        {
            Component graphic = graphics[i];
            if (graphic == null)
                continue;

            s_setAllDirtyMethod?.Invoke(graphic, null);

            RectTransform graphicRectTransform = graphic.GetComponent<RectTransform>();
            if (graphicRectTransform != null)
                s_markLayoutForRebuildMethod?.Invoke(null, new object[] { graphicRectTransform });
        }

        s_forceRebuildLayoutImmediateMethod?.Invoke(null, new object[] { root });
        Canvas.ForceUpdateCanvases();
    }

    private static Bounds ComputeGraphicBounds(Component[] graphics, Transform fallbackTransform)
    {
        Bounds bounds = default;
        bool found = false;

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] is not Behaviour behaviour || !behaviour.enabled)
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

        return new Bounds(fallbackTransform.position, Vector3.one * 0.1f);
    }

    private static void FrameCamera(Camera camera, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents * (1f + ImprovedThumbnailSettings.ThumbnailBoundsPadding);

        camera.transform.position = new Vector3(center.x, center.y, center.z - 10f);
        camera.transform.rotation = Quaternion.identity;
        camera.orthographicSize = Mathf.Max(extents.y, extents.x, 0.1f);
    }

    private static void AssignLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = layer;
    }

    private static bool HasTextMeshProUi(GameObject prefab)
    {
        if (prefab == null || s_tmpUiType == null)
            return false;

        return prefab.GetComponentInChildren(s_tmpUiType, true) != null;
    }

    private static bool HasUnityUiSupport =>
        s_graphicType != null &&
        s_canvasScalerType != null &&
        s_graphicRaycasterType != null &&
        s_layoutRebuilderType != null &&
        s_setAllDirtyMethod != null &&
        s_markLayoutForRebuildMethod != null &&
        s_forceRebuildLayoutImmediateMethod != null;

    private static void SetNumericMember(Component target, string memberName, float value)
    {
        if (target == null)
            return;

        Type targetType = target.GetType();
        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
            return;
        }

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private static void SetEnumMember(Component target, string memberName, string enumValueName)
    {
        if (target == null)
            return;

        Type targetType = target.GetType();
        PropertyInfo property = targetType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum)
        {
            object enumValue = Enum.Parse(property.PropertyType, enumValueName);
            property.SetValue(target, enumValue);
            return;
        }

        FieldInfo field = targetType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum)
        {
            object enumValue = Enum.Parse(field.FieldType, enumValueName);
            field.SetValue(target, enumValue);
        }
    }
}

}
