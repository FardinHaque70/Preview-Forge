using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace FardinHaque.ImprovedAssetTools.Editor
{


public sealed class GeneralPrefabThumbnailProvider : ThumbnailProviderBase
{
    public override string Id => "general-prefab";
    public override ThumbnailAssetKind AssetKind => ThumbnailAssetKind.GeneralPrefab;
    public override int Priority => ImprovedThumbnailSettings.GeneralThumbnailProviderPriority;

    protected override ThumbnailSupportInfo EvaluateSupport(Object asset, string guid, string assetPath)
    {
        if (!ImprovedThumbnailSettings.EnableGeneralThumbnailProvider)
            return ThumbnailSupportInfo.Unsupported;

        GameObject prefab = asset as GameObject;
        if (prefab == null)
            return ThumbnailSupportInfo.Unsupported;

        if (ThumbnailPrefabUtility.HasRootParticleSystem(prefab))
            return ThumbnailSupportInfo.Unsupported;

        bool hasMesh = prefab.GetComponentInChildren<MeshRenderer>(true) != null
            || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
        bool hasSprite = prefab.GetComponentInChildren<SpriteRenderer>(true) != null;
        bool supported = hasMesh || (hasSprite && !IsSpriteOnlyPrefab(prefab));

        if (!supported)
            return ThumbnailSupportInfo.Unsupported;

        ThumbnailAssetKind kind = PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant
            ? ThumbnailAssetKind.PrefabVariant
            : ThumbnailAssetKind.GeneralPrefab;

        return new ThumbnailSupportInfo(true, kind, Priority);
    }

    protected override ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        return RenderMeshThumbnail(
            context,
            renderContext,
            ImprovedThumbnailSettings.GeneralThumbnailYaw,
            ImprovedThumbnailSettings.GeneralThumbnailPitch,
            ImprovedThumbnailSettings.GeneralThumbnailLightIntensity,
            ImprovedThumbnailSettings.ThumbnailBoundsPadding,
            0f);
    }

    public static ThumbnailFrameSet RenderMeshThumbnail(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        return RenderMeshThumbnail(
            context,
            renderContext,
            ImprovedThumbnailSettings.GeneralThumbnailYaw,
            ImprovedThumbnailSettings.GeneralThumbnailPitch,
            ImprovedThumbnailSettings.GeneralThumbnailLightIntensity,
            ImprovedThumbnailSettings.ThumbnailBoundsPadding,
            0f);
    }

    public static ThumbnailFrameSet RenderMeshThumbnail(
        ThumbnailProviderContext context,
        ThumbnailRenderContext renderContext,
        float yaw,
        float pitch,
        float lightIntensity,
        float boundsPadding,
        float verticalBias)
    {
        int thumbSize = ImprovedThumbnailSettings.GetThumbnailSize(context.Surface);
        renderContext.EnsureInfrastructure(
            thumbSize,
            ImprovedThumbnailSettings.RenderBackgroundColor,
            ImprovedThumbnailSettings.ThumbnailCameraFov,
            lightIntensity,
            0f);

        GameObject instance = null;
        GameObject gridInstance = null;
        try
        {
            instance = Object.Instantiate(context.Prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            renderContext.Preview.AddSingleGO(instance);
            renderContext.SetInstance(instance);
            bool showParticles = false;

            // Suppress particle system renderers unless the setting allows them
            ParticleSystemRenderer[] particleRenderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            if (!showParticles)
                ThumbnailPrefabUtility.SetParticleSystemRendererEnabled(particleRenderers, false);

            Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>(true);
            List<Renderer> renderers = CollectVisibleRenderers(allRenderers, showParticles);
            if (!HasSupportedRenderer(renderers))
                return null;

            // Suppress canvas UI children for mesh/model thumbnail captures
            Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i] != null) canvases[i].enabled = false;

            ThumbnailPrefabUtility.SetRendererEnabled(allRenderers, false);
            SetRendererEnabled(renderers, false);
            Bounds bounds = ComputeRendererBounds(renderers);
            FrameCamera(renderContext.Preview.camera, renderContext.Preview.lights[0], bounds, boundsPadding, yaw, pitch, verticalBias);
            gridInstance = ThumbnailPrefabUtility.CreatePreviewGroundGrid(renderContext.Preview.camera, bounds);
            if (gridInstance != null)
                renderContext.Preview.AddSingleGO(gridInstance);

            renderContext.Preview.BeginPreview(new Rect(0f, 0f, thumbSize, thumbSize), GUIStyle.none);
            SetRendererEnabled(renderers, true);
            try
            {
                renderContext.RenderPreviewCamera();
            }
            finally
            {
                SetRendererEnabled(renderers, false);
                ThumbnailPrefabUtility.SetRendererEnabled(allRenderers, false);
            }

            return new ThumbnailFrameSet
            {
                StaticFrame = renderContext.CapturePreview(thumbSize)
            };
        }
        finally
        {
            if (gridInstance != null)
                Object.DestroyImmediate(gridInstance);
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

    private static List<Renderer> CollectVisibleRenderers(Renderer[] renderers, bool showParticles)
    {
        List<Renderer> visibleRenderers = new List<Renderer>();
        if (renderers == null)
            return visibleRenderers;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
                continue;
            if (!renderer.enabled)
                continue;
            if (renderer is ParticleSystemRenderer && !showParticles)
                continue;

            visibleRenderers.Add(renderer);
        }

        return visibleRenderers;
    }

    private static void SetRendererEnabled(List<Renderer> renderers, bool enabled)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    private static bool HasSupportedRenderer(List<Renderer> renderers)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer)
                return true;
        }

        return false;
    }

    private static Bounds ComputeRendererBounds(List<Renderer> renderers)
    {
        Bounds bounds = default;
        bool found = false;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                skinnedMeshRenderer.updateWhenOffscreen = true;

            Bounds worldBounds;
            if (renderer is SkinnedMeshRenderer)
            {
                worldBounds = renderer.bounds;
            }
            else
            {
                Bounds localBounds = renderer.localBounds;
                worldBounds = TransformBounds(localBounds, renderer.transform.localToWorldMatrix);
            }

            if (!found)
            {
                bounds = worldBounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(worldBounds);
            }
        }

        return found ? bounds : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
    {
        Vector3 center = local.center;
        Vector3 extents = local.extents;
        Bounds worldBounds = new Bounds(matrix.MultiplyPoint3x4(center), Vector3.zero);

        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            worldBounds.Encapsulate(matrix.MultiplyPoint3x4(center + new Vector3(extents.x * sx, extents.y * sy, extents.z * sz)));

        return worldBounds;
    }

    private static void FrameCamera(Camera camera, Light mainLight, Bounds bounds, float boundsPadding, float yaw, float pitch, float verticalBias)
    {
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 forward = cameraRotation * Vector3.forward;
        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;
        Vector3 center = bounds.center + Vector3.up * (bounds.size.y * verticalBias);
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
        mainLight.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
    }
}

}
