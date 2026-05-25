using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class PreviewHdrpLightLayerBridgeBootstrap
    {
        static PreviewHdrpLightLayerBridgeBootstrap()
        {
            PreviewLightLayerBridgeRegistry.Register(new PreviewHdrpLightLayerBridge());
        }
    }

    internal sealed class PreviewHdrpLightLayerBridge : IPreviewLightLayerBridge
    {
        public PreviewRenderPipelineKind PipelineKind => PreviewRenderPipelineKind.Hdrp;
        public string BridgeName => "HDRP";

        public void ApplyLightLayers(Light light, uint renderingLayerMask)
        {
            if (light == null)
                return;

            HDAdditionalLightData additionalLightData = light.GetComponent<HDAdditionalLightData>();
            if (additionalLightData == null)
                additionalLightData = light.gameObject.AddComponent<HDAdditionalLightData>();

            LightLayerEnum lightLayers = (LightLayerEnum) renderingLayerMask;
            additionalLightData.lightlayersMask = lightLayers;
            additionalLightData.shadowLayerMask = lightLayers;
            additionalLightData.linkShadowLayers = false;
        }
    }
}
