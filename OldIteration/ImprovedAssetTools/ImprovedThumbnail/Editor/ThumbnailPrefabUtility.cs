using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public static class ThumbnailPrefabUtility
{
    private const float GroundGridReferenceDepth = 8f;
    private static Mesh s_groundGridMesh;
    private static Material s_groundGridMaterial;
    private static float s_groundGridHalfSize = -1f;
    private static float s_groundGridStep = -1f;
    private static float s_groundGridAlpha = -1f;

    public static bool HasRootParticleSystem(GameObject root)
    {
        return root != null && root.GetComponent<ParticleSystem>() != null;
    }

    public static bool HasParticleSystems(GameObject root)
    {
        return root != null && root.GetComponentInChildren<ParticleSystem>(true) != null;
    }

    public static void ForceActivateHierarchy(GameObject root)
    {
        if (root == null)
            return;

        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            transform.gameObject.SetActive(true);
            transform.gameObject.layer = 0;
        }
    }

    public static void SetRendererEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    public static void SetSpriteRendererEnabled(SpriteRenderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    public static void SetParticleSystemRendererEnabled(ParticleSystemRenderer[] renderers, bool enabled)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = enabled;
        }
    }

    public static GameObject CreatePreviewGroundGrid(Camera camera, Bounds bounds)
    {
        EnsureGroundGridResources();
        if (s_groundGridMesh == null || s_groundGridMaterial == null)
            return null;

        GameObject gridObject = new GameObject("ThumbnailGroundGrid");
        gridObject.hideFlags = HideFlags.HideAndDontSave;
        gridObject.layer = 0;
        Vector3 surfaceOrigin = new Vector3(bounds.center.x, bounds.min.y - 0.001f, bounds.center.z);
        gridObject.transform.position = surfaceOrigin;
        gridObject.transform.rotation = Quaternion.identity;
        gridObject.transform.localScale = Vector3.one * ComputeGridDepthScale(camera, surfaceOrigin);

        MeshFilter meshFilter = gridObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = s_groundGridMesh;

        MeshRenderer meshRenderer = gridObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = s_groundGridMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        return gridObject;
    }

    private static float ComputeGridDepthScale(Camera camera, Vector3 surfaceOrigin)
    {
        if (camera == null)
            return 1f;

        float depth = Vector3.Dot(surfaceOrigin - camera.transform.position, camera.transform.forward);
        if (depth <= 0.001f)
            return 1f;

        float scale = depth / GroundGridReferenceDepth;
        return Mathf.Clamp(scale, 0.2f, 20f);
    }

    private static void EnsureGroundGridResources()
    {
        float halfSize = ImprovedThumbnailSettings.ThumbnailGroundGridHalfSize;
        float step = ImprovedThumbnailSettings.ThumbnailGroundGridStep;
        float alpha = ImprovedThumbnailSettings.ThumbnailGroundGridAlpha;

        if (s_groundGridMaterial == null)
            s_groundGridMaterial = CreateGroundGridMaterial();

        if (s_groundGridMesh == null)
            s_groundGridMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };

        if (Mathf.Approximately(halfSize, s_groundGridHalfSize)
            && Mathf.Approximately(step, s_groundGridStep)
            && Mathf.Approximately(alpha, s_groundGridAlpha))
            return;

        s_groundGridHalfSize = halfSize;
        s_groundGridStep = step;
        s_groundGridAlpha = alpha;
        RebuildGroundGridMesh(s_groundGridMesh, halfSize, step, alpha);
    }

    private static Material CreateGroundGridMaterial()
    {
        Material material = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 2999,
        };

        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", 0);
        material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        return material;
    }

    private static void RebuildGroundGridMesh(Mesh mesh, float halfSize, float step, float alpha)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(halfSize / step));
        List<Vector3> vertices = new List<Vector3>((count * 2 + 1) * 8);
        List<Color> colors = new List<Color>((count * 2 + 1) * 8);

        Color baseColor = EditorGUIUtility.isProSkin
            ? new Color(1f, 1f, 1f, alpha)
            : new Color(0f, 0f, 0f, alpha);

        for (int i = -count; i <= count; i++)
        {
            float position = i * step;
            float fade = Mathf.Pow(1f - Mathf.Clamp01(Mathf.Abs(position) / halfSize), 2f);
            Color peak = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);
            Color zero = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

            vertices.Add(new Vector3(-halfSize, 0f, position)); colors.Add(zero);
            vertices.Add(new Vector3(0f, 0f, position)); colors.Add(peak);
            vertices.Add(new Vector3(0f, 0f, position)); colors.Add(peak);
            vertices.Add(new Vector3(halfSize, 0f, position)); colors.Add(zero);

            vertices.Add(new Vector3(position, 0f, -halfSize)); colors.Add(zero);
            vertices.Add(new Vector3(position, 0f, 0f)); colors.Add(peak);
            vertices.Add(new Vector3(position, 0f, 0f)); colors.Add(peak);
            vertices.Add(new Vector3(position, 0f, halfSize)); colors.Add(zero);
        }

        int[] indices = new int[vertices.Count];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
    }
}

}
