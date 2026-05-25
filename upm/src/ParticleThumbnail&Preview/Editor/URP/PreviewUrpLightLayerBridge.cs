using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class PreviewUrpLightLayerBridgeBootstrap
    {
        static PreviewUrpLightLayerBridgeBootstrap()
        {
            PreviewLightLayerBridgeRegistry.Register(new PreviewUrpLightLayerBridge());
        }
    }

    internal sealed class PreviewUrpLightLayerBridge : IPreviewLightLayerBridge
    {
        public PreviewRenderPipelineKind PipelineKind => PreviewRenderPipelineKind.Urp3D;
        public string BridgeName => "URP";

        public void ApplyLightLayers(Light light, uint renderingLayerMask)
        {
            if (light == null)
                return;

            UniversalAdditionalLightData additionalLightData = light.GetUniversalAdditionalLightData();
            RenderingLayerMask renderingLayers = renderingLayerMask;
            additionalLightData.renderingLayers = renderingLayers;
            additionalLightData.shadowRenderingLayers = renderingLayers;
            additionalLightData.customShadowLayers = false;
        }
    }
}
