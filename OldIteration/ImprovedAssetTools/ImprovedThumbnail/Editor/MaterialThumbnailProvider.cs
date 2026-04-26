using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class MaterialThumbnailProvider : ThumbnailProviderBase
{
    public override string Id => "material-asset";
    public override ThumbnailAssetKind AssetKind => ThumbnailAssetKind.MaterialAsset;
    public override int Priority => ImprovedThumbnailSettings.MaterialThumbnailProviderPriority;

    protected override bool SupportsAssetPath(string assetPath)
    {
        return ThumbnailAssetPathUtility.IsMaterialAssetPath(assetPath);
    }

    protected override UnityEngine.Object LoadAsset(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(assetPath);
    }

    protected override ThumbnailSupportInfo EvaluateSupport(UnityEngine.Object asset, string guid, string assetPath)
    {
        if (!ImprovedThumbnailSettings.EnableMaterialThumbnailProvider)
            return ThumbnailSupportInfo.Unsupported;

        Material material = asset as Material;
        if (material == null)
            return ThumbnailSupportInfo.Unsupported;

        return new ThumbnailSupportInfo(true, ThumbnailAssetKind.MaterialAsset, Priority);
    }

    protected override ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        Material material = context.Asset as Material;
        if (material == null)
            return null;

        int thumbnailSize = ImprovedThumbnailSettings.GetThumbnailSize(context.Surface);
        renderContext.EnsureInfrastructure(
            thumbnailSize,
            ImprovedThumbnailSettings.RenderBackgroundColor,
            ImprovedThumbnailSettings.ThumbnailCameraFov,
            ImprovedThumbnailSettings.GeneralThumbnailLightIntensity,
            0f);

        bool useQuadPreview = ShouldUseQuadPreviewMesh();
        GameObject previewMeshObject = null;
        Mesh previewMeshAsset = null;
        GameObject gridInstance = null;
        try
        {
            previewMeshObject = CreatePreviewMeshObject(useQuadPreview, out previewMeshAsset);
            if (previewMeshObject == null)
                return null;

            Renderer renderer = previewMeshObject.GetComponent<Renderer>();
            if (renderer == null)
                return null;

            MaterialRenderResolution renderResolution = MaterialRenderCompatibilityUtility.ResolveMaterial(material);
            Material renderMaterial = renderResolution.RenderMaterial != null
                ? renderResolution.RenderMaterial
                : material;
            renderer.sharedMaterial = renderMaterial;

            renderContext.Preview.AddSingleGO(previewMeshObject);
            renderContext.SetInstance(previewMeshObject);

            Renderer[] renderers = previewMeshObject.GetComponentsInChildren<Renderer>(true);
            ThumbnailPrefabUtility.SetRendererEnabled(renderers, false);
            Bounds bounds = renderer.bounds;
            FrameCamera(renderContext.Preview.camera, renderContext.Preview.lights[0], bounds, ImprovedThumbnailSettings.ThumbnailBoundsPadding);

            gridInstance = ThumbnailPrefabUtility.CreatePreviewGroundGrid(renderContext.Preview.camera, bounds);
            if (gridInstance != null)
                renderContext.Preview.AddSingleGO(gridInstance);

            Texture2D frameTexture = RenderStaticFrame(
                renderContext,
                renderers,
                thumbnailSize,
                applyThumbnailEnvironment: true);
            if (frameTexture == null)
                return null;

            return new ThumbnailFrameSet
            {
                StaticFrame = frameTexture
            };
        }
        finally
        {
            if (gridInstance != null)
                Object.DestroyImmediate(gridInstance);
            if (previewMeshObject != null)
                Object.DestroyImmediate(previewMeshObject);
            if (previewMeshAsset != null)
                Object.DestroyImmediate(previewMeshAsset);
        }
    }

    private static bool ShouldUseQuadPreviewMesh()
    {
        return MaterialRenderCompatibilityUtility.IsUrp2D();
    }

    private static GameObject CreatePreviewMeshObject(bool useQuad, out Mesh generatedMesh)
    {
        generatedMesh = null;
        if (!useQuad)
        {
            GameObject previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            previewSphere.hideFlags = HideFlags.HideAndDontSave;
            previewSphere.name = "ThumbnailMaterialSphere";
            previewSphere.transform.position = Vector3.zero;
            previewSphere.transform.rotation = Quaternion.identity;
            previewSphere.transform.localScale = Vector3.one;

            Collider collider = previewSphere.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            return previewSphere;
        }

        var previewQuad = new GameObject("ThumbnailMaterialQuad")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        previewQuad.transform.position = Vector3.zero;
        previewQuad.transform.rotation = Quaternion.identity;
        previewQuad.transform.localScale = Vector3.one;

        MeshFilter meshFilter = previewQuad.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = previewQuad.AddComponent<MeshRenderer>();
        generatedMesh = CreateDoubleSidedQuadMesh(1f);
        meshFilter.sharedMesh = generatedMesh;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        return previewQuad;
    }

    private static Mesh CreateDoubleSidedQuadMesh(float size)
    {
        float halfSize = Mathf.Max(0.01f, size) * 0.5f;
        var mesh = new Mesh
        {
            name = "___ThumbnailMaterialQuad___",
            hideFlags = HideFlags.HideAndDontSave
        };

        var vertices = new[]
        {
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3(halfSize, -halfSize, 0f),
            new Vector3(halfSize, halfSize, 0f),
            new Vector3(-halfSize, halfSize, 0f),
            new Vector3(-halfSize, -halfSize, 0f),
            new Vector3(halfSize, -halfSize, 0f),
            new Vector3(halfSize, halfSize, 0f),
            new Vector3(-halfSize, halfSize, 0f),
        };

        var normals = new[]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.back,
            Vector3.back,
            Vector3.back,
            Vector3.back,
        };

        var uvs = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };

        var triangles = new[]
        {
            0, 1, 2, 0, 2, 3,
            6, 5, 4, 7, 6, 4
        };

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Texture2D RenderStaticFrame(
        ThumbnailRenderContext renderContext,
        Renderer[] renderers,
        int thumbnailSize,
        bool applyThumbnailEnvironment)
    {
        renderContext.Preview.BeginPreview(new Rect(0f, 0f, thumbnailSize, thumbnailSize), GUIStyle.none);
        ThumbnailPrefabUtility.SetRendererEnabled(renderers, true);
        try
        {
            renderContext.RenderPreviewCamera(applyThumbnailEnvironment);
        }
        finally
        {
            ThumbnailPrefabUtility.SetRendererEnabled(renderers, false);
        }

        return renderContext.CapturePreview(thumbnailSize);
    }

    private static void FrameCamera(Camera camera, Light mainLight, Bounds bounds, float boundsPadding)
    {
        Quaternion cameraRotation = Quaternion.Euler(ImprovedThumbnailSettings.ThumbnailCameraPitch, ImprovedThumbnailSettings.ThumbnailCameraYaw, 0f);

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
        mainLight.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
    }
}

}
