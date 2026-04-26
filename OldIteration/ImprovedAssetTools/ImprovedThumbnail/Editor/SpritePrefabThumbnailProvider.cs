using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class SpritePrefabThumbnailProvider : ThumbnailProviderBase
{
    public override string Id => "sprite-prefab";
    public override ThumbnailAssetKind AssetKind => ThumbnailAssetKind.SpritePrefab;
    public override int Priority => ImprovedThumbnailSettings.SpriteThumbnailProviderPriority;

    protected override ThumbnailSupportInfo EvaluateSupport(Object asset, string guid, string assetPath)
    {
        if (!ImprovedThumbnailSettings.EnableSpriteThumbnailProvider)
            return ThumbnailSupportInfo.Unsupported;

        GameObject prefab = asset as GameObject;
        if (prefab == null)
            return ThumbnailSupportInfo.Unsupported;

        bool supported = IsSpriteOnlyPrefab(prefab);

        return supported
            ? new ThumbnailSupportInfo(true, ThumbnailAssetKind.SpritePrefab, Priority)
            : ThumbnailSupportInfo.Unsupported;
    }

    protected override ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        int thumbSize = ImprovedThumbnailSettings.GetThumbnailSize(context.Surface);
        renderContext.EnsureInfrastructure(
            thumbSize,
            ImprovedThumbnailSettings.RenderBackgroundColor,
            ImprovedThumbnailSettings.ThumbnailCameraFov,
            ImprovedThumbnailSettings.SpriteThumbnailLightIntensity,
            0f);

        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(context.Prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            renderContext.Preview.AddSingleGO(instance);
            renderContext.SetInstance(instance);

            Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>(true);
            SpriteRenderer[] spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            List<SpriteRenderer> visibleSpriteRenderers = CollectVisibleSpriteRenderers(spriteRenderers);
            if (visibleSpriteRenderers.Count == 0)
                return null;

            ThumbnailPrefabUtility.SetRendererEnabled(allRenderers, false);
            SetSpriteRendererEnabled(visibleSpriteRenderers, false);
            Bounds bounds = ComputeSpriteBounds(visibleSpriteRenderers);
            FrameCamera(renderContext.Preview.camera, renderContext.Preview.lights[0], bounds, ImprovedThumbnailSettings.ThumbnailBoundsPadding);

            renderContext.Preview.BeginPreview(new Rect(0f, 0f, thumbSize, thumbSize), GUIStyle.none);
            SetSpriteRendererEnabled(visibleSpriteRenderers, true);
            try
            {
                // 2D sprite thumbnails do not need skybox/HDRI environment setup.
                renderContext.RenderPreviewCamera(applyThumbnailEnvironment: false);
            }
            finally
            {
                SetSpriteRendererEnabled(visibleSpriteRenderers, false);
                ThumbnailPrefabUtility.SetRendererEnabled(allRenderers, false);
            }

            return new ThumbnailFrameSet
            {
                StaticFrame = renderContext.CapturePreview(thumbSize)
            };
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
        }
    }

    private static bool IsSpriteOnlyPrefab(GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (ThumbnailPrefabUtility.HasParticleSystems(prefab))
            return false;

        bool hasSpriteRenderer = false;
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer is SpriteRenderer)
            {
                hasSpriteRenderer = true;
                continue;
            }

            return false;
        }

        return hasSpriteRenderer;
    }

    private static List<SpriteRenderer> CollectVisibleSpriteRenderers(SpriteRenderer[] spriteRenderers)
    {
        List<SpriteRenderer> visibleSpriteRenderers = new List<SpriteRenderer>();
        if (spriteRenderers == null)
            return visibleSpriteRenderers;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;
            if (!spriteRenderer.gameObject.activeInHierarchy)
                continue;
            if (!spriteRenderer.enabled)
                continue;
            if (spriteRenderer.sprite == null)
                continue;

            visibleSpriteRenderers.Add(spriteRenderer);
        }

        return visibleSpriteRenderers;
    }

    private static void SetSpriteRendererEnabled(List<SpriteRenderer> spriteRenderers, bool enabled)
    {
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer != null)
                spriteRenderer.enabled = enabled;
        }
    }

    private static Bounds ComputeSpriteBounds(List<SpriteRenderer> spriteRenderers)
    {
        Bounds bounds = default;
        bool found = false;

        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                continue;

            if (!found)
            {
                bounds = spriteRenderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(spriteRenderer.bounds);
            }
        }

        return found ? bounds : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private static void FrameCamera(Camera camera, Light mainLight, Bounds bounds, float boundsPadding)
    {
        float yaw = ImprovedThumbnailSettings.ThumbnailCameraYaw;
        Quaternion cameraRotation = ImprovedThumbnailSettings.SpriteThumbnailFrontView
            ? Quaternion.identity
            : Quaternion.Euler(0f, yaw, 0f);

        Vector3 forward = cameraRotation * Vector3.forward;
        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents * (1f + boundsPadding);

        float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanHalf = Mathf.Tan(halfFov);
        float maxRight = 0f;
        float maxUp = 0f;
        float maxDepth = 0f;

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
        {
            Vector3 corner = new Vector3(extents.x * sx, extents.y * sy, extents.z * sz);
            maxRight = Mathf.Max(maxRight, Mathf.Abs(Vector3.Dot(corner, right)));
            maxUp = Mathf.Max(maxUp, Mathf.Abs(Vector3.Dot(corner, up)));
            maxDepth = Mathf.Max(maxDepth, Mathf.Abs(Vector3.Dot(corner, forward)));
        }

        float distance = Mathf.Max(maxRight / tanHalf, maxUp / tanHalf, 0.3f);
        camera.transform.position = center - forward * distance;
        camera.transform.rotation = cameraRotation;
        camera.nearClipPlane = Mathf.Max(0.01f, distance - maxDepth - 0.5f);
        camera.farClipPlane = distance + maxDepth + 1f;
        mainLight.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }
}

}
