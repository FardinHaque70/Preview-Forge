#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public readonly struct MaterialRenderResolution
{
    public readonly Material RenderMaterial;
    public readonly bool UsesFallback;
    public readonly bool PreflightUnsupported;
    public readonly string Reason;

    public MaterialRenderResolution(
        Material renderMaterial,
        bool usesFallback,
        bool preflightUnsupported,
        string reason)
    {
        RenderMaterial = renderMaterial;
        UsesFallback = usesFallback;
        PreflightUnsupported = preflightUnsupported;
        Reason = reason;
    }
}

public static class MaterialRenderCompatibilityUtility
{
    private static Material s_errorFallbackMaterial;
    private static Material s_magentaFallbackMaterial;
    private const string PreviewTypeTagName = "PreviewType";
    private const string PreviewTypePlaneValue = "plane";

    public static MaterialRenderResolution ResolveMaterial(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return BuildUnsupportedResolution(sourceMaterial, "Material is null.");
        }

        Shader shader = sourceMaterial.shader;
        if (shader == null)
        {
            return BuildUnsupportedResolution(sourceMaterial, "Material has no shader.");
        }

        if (!shader.isSupported)
        {
            return BuildUnsupportedResolution(sourceMaterial, $"Shader '{shader.name}' is unsupported in the active render pipeline.");
        }

        int passCount = sourceMaterial.passCount;
        if (passCount <= 0)
        {
            return BuildUnsupportedResolution(sourceMaterial, $"Shader '{shader.name}' has no renderable passes.");
        }

        return new MaterialRenderResolution(
            sourceMaterial,
            usesFallback: false,
            preflightUnsupported: false,
            reason: string.Empty);
    }

    public static bool IsUrp2D()
    {
        RenderPipelineAsset renderPipeline = GraphicsSettings.currentRenderPipeline;
        if (renderPipeline == null)
            return false;

        string fullName = renderPipeline.GetType().FullName ?? string.Empty;
        bool isUrp = fullName.IndexOf("UniversalRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0
                  || fullName.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!isUrp)
            return false;

        var serializedPipeline = new SerializedObject(renderPipeline);
        SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
        SerializedProperty defaultIndexProp = serializedPipeline.FindProperty("m_DefaultRendererIndex");
        if (rendererList == null || !rendererList.isArray || rendererList.arraySize <= 0)
            return false;

        int defaultIndex = defaultIndexProp != null
            ? Mathf.Clamp(defaultIndexProp.intValue, 0, rendererList.arraySize - 1)
            : 0;
        SerializedProperty rendererEntry = rendererList.GetArrayElementAtIndex(defaultIndex);
        UnityEngine.Object rendererData = rendererEntry?.objectReferenceValue;
        if (rendererData == null)
            return false;

        Type rendererType = rendererData.GetType();
        string typeName = rendererType.FullName ?? rendererType.Name;
        return typeName.IndexOf("Renderer2DData", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool HasPreviewTypeTag(Material material, string previewType)
    {
        if (material == null || string.IsNullOrEmpty(previewType))
            return false;

        string previewTypeTag = material.GetTag(PreviewTypeTagName, false, string.Empty);
        return string.Equals(previewTypeTag, previewType, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasPlanePreviewTypeTag(Material material)
    {
        return HasPreviewTypeTag(material, PreviewTypePlaneValue);
    }

    public static bool TryGetFallbackMaterial(out Material fallbackMaterial)
    {
        fallbackMaterial = null;

        if (s_errorFallbackMaterial == null)
            s_errorFallbackMaterial = CreateInternalErrorFallbackMaterial();
        if (s_errorFallbackMaterial != null)
        {
            fallbackMaterial = s_errorFallbackMaterial;
            return true;
        }

        if (s_magentaFallbackMaterial == null)
            s_magentaFallbackMaterial = CreateMagentaFallbackMaterial();
        if (s_magentaFallbackMaterial != null)
        {
            fallbackMaterial = s_magentaFallbackMaterial;
            return true;
        }

        return false;
    }

    private static MaterialRenderResolution BuildUnsupportedResolution(Material sourceMaterial, string reason)
    {
        if (TryGetFallbackMaterial(out Material fallbackMaterial) && fallbackMaterial != null)
        {
            return new MaterialRenderResolution(
                fallbackMaterial,
                usesFallback: true,
                preflightUnsupported: true,
                reason: reason);
        }

        return new MaterialRenderResolution(
            sourceMaterial,
            usesFallback: false,
            preflightUnsupported: true,
            reason: reason);
    }

    private static Material CreateInternalErrorFallbackMaterial()
    {
        Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
        if (errorShader == null || !errorShader.isSupported)
            return null;

        return new Material(errorShader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "___ImprovedPreviewInternalErrorFallback___"
        };
    }

    private static Material CreateMagentaFallbackMaterial()
    {
        Shader unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader == null || !unlitShader.isSupported)
            return null;

        var material = new Material(unlitShader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            name = "___ImprovedPreviewMagentaFallback___"
        };

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(1f, 0f, 1f, 1f));

        return material;
    }
}

}
#endif
