using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
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
        private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private static readonly Type AdditionalLightDataType = typeof(UniversalAdditionalLightData);
        private static readonly PropertyInfo RenderingLayersProperty = AdditionalLightDataType.GetProperty("renderingLayers", PublicInstance);
        private static readonly PropertyInfo ShadowRenderingLayersProperty = AdditionalLightDataType.GetProperty("shadowRenderingLayers", PublicInstance);
        private static readonly PropertyInfo LightLayerMaskProperty = AdditionalLightDataType.GetProperty("lightLayerMask", PublicInstance);
        private static readonly PropertyInfo ShadowLayerMaskProperty = AdditionalLightDataType.GetProperty("shadowLayerMask", PublicInstance);
        private static readonly PropertyInfo CustomShadowLayersProperty = AdditionalLightDataType.GetProperty("customShadowLayers", PublicInstance);

        public PreviewRenderPipelineKind PipelineKind => PreviewRenderPipelineKind.Urp3D;
        public string BridgeName => "URP";

        public void ApplyLightLayers(Light light, uint renderingLayerMask)
        {
            if (light == null)
                return;

            UniversalAdditionalLightData additionalLightData = light.GetUniversalAdditionalLightData();
            SetPropertyIfPresent(additionalLightData, CustomShadowLayersProperty, false);

            if (TrySetPropertyPair(additionalLightData, RenderingLayersProperty, ShadowRenderingLayersProperty, renderingLayerMask))
                return;

            if (TrySetEnumPropertyPair(additionalLightData, LightLayerMaskProperty, ShadowLayerMaskProperty, renderingLayerMask))
                return;

            throw new MissingMemberException(
                AdditionalLightDataType.FullName,
                "Expected either renderingLayers/shadowRenderingLayers or lightLayerMask/shadowLayerMask on UniversalAdditionalLightData.");
        }

        private static bool TrySetPropertyPair(object target, PropertyInfo primaryProperty, PropertyInfo secondaryProperty, uint renderingLayerMask)
        {
            if (primaryProperty == null || secondaryProperty == null)
                return false;

            primaryProperty.SetValue(target, renderingLayerMask);
            secondaryProperty.SetValue(target, renderingLayerMask);
            return true;
        }

        private static bool TrySetEnumPropertyPair(object target, PropertyInfo primaryProperty, PropertyInfo secondaryProperty, uint renderingLayerMask)
        {
            if (primaryProperty == null || secondaryProperty == null)
                return false;

            object enumValue = Enum.ToObject(primaryProperty.PropertyType, unchecked((int) renderingLayerMask));
            primaryProperty.SetValue(target, enumValue);
            secondaryProperty.SetValue(target, enumValue);
            return true;
        }

        private static void SetPropertyIfPresent(object target, PropertyInfo property, object value)
        {
            if (property == null)
                return;

            property.SetValue(target, value);
        }
    }
}
