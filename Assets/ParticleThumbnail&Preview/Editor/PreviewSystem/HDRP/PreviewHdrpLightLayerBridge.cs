using System;
using System.Reflection;
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
        private static readonly BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        private static readonly Type AdditionalLightDataType = typeof(HDAdditionalLightData);
        private static readonly MethodInfo SetLightLayerMethod = ResolveSetLightLayerMethod();
        private static readonly PropertyInfo LinkShadowLayersProperty = AdditionalLightDataType.GetProperty("linkShadowLayers", PublicInstance);
        private static readonly PropertyInfo LightLayersMaskProperty = AdditionalLightDataType.GetProperty("lightlayersMask", PublicInstance);
        private static readonly PropertyInfo ShadowLayerMaskProperty = AdditionalLightDataType.GetProperty("shadowLayerMask", PublicInstance);

        public PreviewRenderPipelineKind PipelineKind => PreviewRenderPipelineKind.Hdrp;
        public string BridgeName => "HDRP";

        public void ApplyLightLayers(Light light, uint renderingLayerMask)
        {
            if (light == null)
                return;

            HDAdditionalLightData additionalLightData = light.GetComponent<HDAdditionalLightData>();
            if (additionalLightData == null)
                additionalLightData = light.gameObject.AddComponent<HDAdditionalLightData>();

            SetPropertyIfPresent(additionalLightData, LinkShadowLayersProperty, false);

            if (TryInvokeSetLightLayer(additionalLightData, renderingLayerMask))
                return;

            if (TrySetEnumPropertyPair(additionalLightData, LightLayersMaskProperty, ShadowLayerMaskProperty, renderingLayerMask))
                return;

            throw new MissingMemberException(
                AdditionalLightDataType.FullName,
                "Expected HDRP SetLightLayer(...) or lightlayersMask/shadowLayerMask support on HDAdditionalLightData.");
        }

        private static MethodInfo ResolveSetLightLayerMethod()
        {
            MethodInfo[] methods = AdditionalLightDataType.GetMethods(PublicInstance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "SetLightLayer")
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType.IsEnum && parameters[1].ParameterType == parameters[0].ParameterType)
                    return method;
            }

            return null;
        }

        private static bool TryInvokeSetLightLayer(object target, uint renderingLayerMask)
        {
            if (SetLightLayerMethod == null)
                return false;

            Type enumType = SetLightLayerMethod.GetParameters()[0].ParameterType;
            object enumValue = Enum.ToObject(enumType, unchecked((int) renderingLayerMask));
            SetLightLayerMethod.Invoke(target, new[] { enumValue, enumValue });
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
