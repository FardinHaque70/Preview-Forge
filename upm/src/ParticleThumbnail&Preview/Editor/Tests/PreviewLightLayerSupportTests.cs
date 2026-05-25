using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public sealed class PreviewLightLayerSupportTests
    {
        private const uint EverythingMask = uint.MaxValue;
        private const string UrpBridgeAssemblyName = "ParticleThumbnailAndPreview.Editor.URP";
        private const string UrpBridgeTypeName = "ParticleThumbnailAndPreview.Editor.PreviewUrpLightLayerBridge";
        private const string UrpLightExtensionsTypeName = "UnityEngine.Rendering.Universal.LightExtensions, Unity.RenderPipelines.Universal.Runtime";
        private const string HdrpBridgeAssemblyName = "ParticleThumbnailAndPreview.Editor.HDRP";
        private const string HdrpBridgeTypeName = "ParticleThumbnailAndPreview.Editor.PreviewHdrpLightLayerBridge";
        private const string HdrpAdditionalLightDataTypeName = "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData, Unity.RenderPipelines.HighDefinition.Runtime";

        [Test]
        public void ResolvePreviewLightRenderingLayerMask_SrpKindsReturnEverything()
        {
            Assert.That(PreviewLightingSystem.ResolvePreviewLightRenderingLayerMask(PreviewRenderPipelineKind.Urp3D), Is.EqualTo(EverythingMask));
            Assert.That(PreviewLightingSystem.ResolvePreviewLightRenderingLayerMask(PreviewRenderPipelineKind.Urp2D), Is.EqualTo(EverythingMask));
            Assert.That(PreviewLightingSystem.ResolvePreviewLightRenderingLayerMask(PreviewRenderPipelineKind.Hdrp), Is.EqualTo(EverythingMask));
            Assert.That(PreviewLightingSystem.ResolvePreviewLightRenderingLayerMask(PreviewRenderPipelineKind.UnknownSrp), Is.EqualTo(EverythingMask));
        }

        [Test]
        public void DescribeLightLayerSupport_BuiltIn_DoesNotRequireBridge()
        {
            PreviewLightLayerSupportStatus status = PreviewLightingSystem.DescribeLightLayerSupport(PreviewRenderPipelineKind.BuiltIn);

            Assert.That(status.BridgeRequired, Is.False);
            Assert.That(status.BridgeRegistered, Is.False);
            Assert.That(status.FullBridgeSupport, Is.False);
            Assert.That(status.UsesGenericFallback, Is.False);
            Assert.That(status.LightingDisabledByPipeline, Is.False);
        }

        [Test]
        public void DescribeLightLayerSupport_UnknownSrp_UsesGenericFallback()
        {
            PreviewLightLayerSupportStatus status = PreviewLightingSystem.DescribeLightLayerSupport(PreviewRenderPipelineKind.UnknownSrp);

            Assert.That(status.BridgeRequired, Is.False);
            Assert.That(status.BridgeRegistered, Is.False);
            Assert.That(status.FullBridgeSupport, Is.False);
            Assert.That(status.UsesGenericFallback, Is.True);
            Assert.That(status.LightingDisabledByPipeline, Is.False);
        }

        [Test]
        public void DescribeLightLayerSupport_Urp2D_ReportsLightingDisabled()
        {
            PreviewLightLayerSupportStatus status = PreviewLightingSystem.DescribeLightLayerSupport(PreviewRenderPipelineKind.Urp2D);

            Assert.That(status.LightingDisabledByPipeline, Is.True);
            Assert.That(status.UsesGenericFallback, Is.False);
        }

        [Test]
        public void UrpBridge_IsRegistered_WhenUrpPackagePresent()
        {
            Assert.That(PreviewLightLayerBridgeRegistry.IsBridgeRegisteredForTests(PreviewRenderPipelineKind.Urp3D), Is.True);
        }

        [Test]
        public void UrpBridge_ApplyLightLayers_SyncsUniversalAdditionalLightData()
        {
            Type bridgeType = Type.GetType($"{UrpBridgeTypeName}, {UrpBridgeAssemblyName}");
            Assert.That(bridgeType, Is.Not.Null, "Expected URP bridge assembly to be loaded.");

            object bridge = Activator.CreateInstance(bridgeType, nonPublic: true);
            MethodInfo applyMethod = bridgeType.GetMethod("ApplyLightLayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);

            GameObject lightRoot = new GameObject("PreviewLightLayerUrpTest");
            try
            {
                Light light = lightRoot.AddComponent<Light>();
                light.renderingLayerMask = unchecked((int) EverythingMask);
                applyMethod.Invoke(bridge, new object[] { light, EverythingMask });

                object additionalLightData = GetUniversalAdditionalLightData(light);
                Assert.That(additionalLightData, Is.Not.Null);
                Assert.That(ReadMaskProperty(additionalLightData, "renderingLayers"), Is.EqualTo(EverythingMask));
                Assert.That(ReadMaskProperty(additionalLightData, "shadowRenderingLayers"), Is.EqualTo(EverythingMask));
                Assert.That((bool) GetPropertyValue(additionalLightData, "customShadowLayers"), Is.False);
                Assert.That(unchecked((uint) light.renderingLayerMask), Is.EqualTo(ReadMaskProperty(additionalLightData, "renderingLayers")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightRoot);
            }
        }

        [Test]
        public void HdrpBridge_RegistersWhenPackageIsPresent()
        {
            Type hdrpAdditionalLightDataType = Type.GetType(HdrpAdditionalLightDataTypeName);
            if (hdrpAdditionalLightDataType == null)
                Assert.Ignore("HDRP runtime package is not installed in this validation project.");

            Assert.That(PreviewLightLayerBridgeRegistry.IsBridgeRegisteredForTests(PreviewRenderPipelineKind.Hdrp), Is.True);
        }

        [Test]
        public void HdrpBridge_ApplyLightLayers_SetsEverythingWhenPackageIsPresent()
        {
            Type hdrpAdditionalLightDataType = Type.GetType(HdrpAdditionalLightDataTypeName);
            if (hdrpAdditionalLightDataType == null)
                Assert.Ignore("HDRP runtime package is not installed in this validation project.");

            Type bridgeType = Type.GetType($"{HdrpBridgeTypeName}, {HdrpBridgeAssemblyName}");
            Assert.That(bridgeType, Is.Not.Null, "Expected HDRP bridge assembly to be loaded.");

            object bridge = Activator.CreateInstance(bridgeType, nonPublic: true);
            MethodInfo applyMethod = bridgeType.GetMethod("ApplyLightLayers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);

            GameObject lightRoot = new GameObject("PreviewLightLayerHdrpTest");
            try
            {
                Light light = lightRoot.AddComponent<Light>();
                light.renderingLayerMask = unchecked((int) EverythingMask);
                applyMethod.Invoke(bridge, new object[] { light, EverythingMask });

                Component additionalLightData = light.GetComponent(hdrpAdditionalLightDataType);
                Assert.That(additionalLightData, Is.Not.Null);
                Assert.That(ReadMaskProperty(additionalLightData, "lightlayersMask"), Is.EqualTo(EverythingMask));
                Assert.That(ReadMaskProperty(additionalLightData, "shadowLayerMask"), Is.EqualTo(EverythingMask));
                Assert.That((bool) GetPropertyValue(additionalLightData, "linkShadowLayers"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lightRoot);
            }
        }

        private static object GetUniversalAdditionalLightData(Light light)
        {
            Type lightExtensionsType = Type.GetType(UrpLightExtensionsTypeName);
            Assert.That(lightExtensionsType, Is.Not.Null, "Expected URP LightExtensions type.");

            MethodInfo method = lightExtensionsType.GetMethod(
                "GetUniversalAdditionalLightData",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Light) },
                null);

            Assert.That(method, Is.Not.Null, "Expected GetUniversalAdditionalLightData method.");
            return method.Invoke(null, new object[] { light });
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}' on type '{target.GetType().FullName}'.");
            return property.GetValue(target);
        }

        private static uint ReadMaskProperty(object target, string propertyName)
        {
            return ConvertMaskToUInt(GetPropertyValue(target, propertyName));
        }

        private static uint ConvertMaskToUInt(object value)
        {
            if (value == null)
                return 0u;

            return value switch
            {
                uint uintValue => uintValue,
                int intValue => unchecked((uint) intValue),
                Enum enumValue => unchecked((uint) Convert.ToInt32(enumValue)),
                _ => TryReadValueProperty(value),
            };
        }

        private static uint TryReadValueProperty(object value)
        {
            PropertyInfo property = value.GetType().GetProperty("value", BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                Assert.Fail($"Expected a mask-like value type but found '{value.GetType().FullName}'.");

            object rawValue = property.GetValue(value);
            if (rawValue is uint uintValue)
                return uintValue;

            if (rawValue is int intValue)
                return unchecked((uint) intValue);

            Assert.Fail($"Unsupported mask backing value '{rawValue?.GetType().FullName ?? "<null>"}'.");
            return 0u;
        }
    }
}
