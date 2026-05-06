using NUnit.Framework;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class ModelPreviewEnvironmentTests
    {
        [Test]
        public void ClassifyPipelineKindForTests_BuiltInWhenPipelineNameMissing()
        {
            PreviewRenderPipelineKind kind = PreviewRenderCompatibilityUtility.ClassifyPipelineKindForTests(null, null);
            Assert.AreEqual(PreviewRenderPipelineKind.BuiltIn, kind);
        }

        [Test]
        public void ClassifyPipelineKindForTests_RecognizesUrp2DAndUrp3D()
        {
            PreviewRenderPipelineKind urp2D = PreviewRenderCompatibilityUtility.ClassifyPipelineKindForTests(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                "Renderer2DData");
            PreviewRenderPipelineKind urp3D = PreviewRenderCompatibilityUtility.ClassifyPipelineKindForTests(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                "ForwardRendererData");

            Assert.AreEqual(PreviewRenderPipelineKind.Urp2D, urp2D);
            Assert.AreEqual(PreviewRenderPipelineKind.Urp3D, urp3D);
        }

        [Test]
        public void ClassifyPipelineKindForTests_RecognizesHdrp()
        {
            PreviewRenderPipelineKind kind = PreviewRenderCompatibilityUtility.ClassifyPipelineKindForTests(
                "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset",
                null);
            Assert.AreEqual(PreviewRenderPipelineKind.Hdrp, kind);
        }

        [Test]
        public void ShouldPreferSrpRenderForTests_IsFalseForBuiltIn_TrueForSrp()
        {
            Assert.IsFalse(PreviewRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(PreviewRenderPipelineKind.BuiltIn));
            Assert.IsTrue(PreviewRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(PreviewRenderPipelineKind.Urp3D));
            Assert.IsTrue(PreviewRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(PreviewRenderPipelineKind.Urp2D));
            Assert.IsTrue(PreviewRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(PreviewRenderPipelineKind.Hdrp));
            Assert.IsTrue(PreviewRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(PreviewRenderPipelineKind.UnknownSrp));
        }

        [Test]
        public void ComputeLightingControlsSupportedForTests_DisablesUrp2D()
        {
            Assert.IsFalse(ModelPrefabPreviewSession.ComputeLightingControlsSupportedForTests(isUrp2DRenderer: true));
            Assert.IsTrue(ModelPrefabPreviewSession.ComputeLightingControlsSupportedForTests(isUrp2DRenderer: false));
        }

        [Test]
        public void ComputeLightingEnabledForTests_DisablesUrp2DEvenWhenOtherInputsAllowIt()
        {
            Assert.IsFalse(ModelPrefabPreviewSession.ComputeLightingEnabledForTests(
                toggleEnabled: true,
                effective2D: false,
                isUrp2DRenderer: true));
        }

        [Test]
        public void ParticleLightingSupportedForTests_DisablesUrp2D()
        {
            Assert.IsFalse(ParticlePrefabPreviewSession.ComputeLightingSupportedForTests(PreviewRenderPipelineKind.Urp2D));
            Assert.IsTrue(ParticlePrefabPreviewSession.ComputeLightingSupportedForTests(PreviewRenderPipelineKind.Urp3D));
            Assert.IsTrue(ParticlePrefabPreviewSession.ComputeLightingSupportedForTests(PreviewRenderPipelineKind.BuiltIn));
        }

        [Test]
        public void ComputeSkyboxSupportedForTests_DisablesUrp2D()
        {
            Assert.IsFalse(ModelPrefabPreviewSession.ComputeSkyboxSupportedForTests(isUrp2DRenderer: true));
            Assert.IsTrue(ModelPrefabPreviewSession.ComputeSkyboxSupportedForTests(isUrp2DRenderer: false));
        }

        [Test]
        public void ComputeSkyboxEnabledForTests_DisablesUrp2DEvenWhenOtherInputsAllowIt()
        {
            Assert.IsFalse(ModelPrefabPreviewSession.ComputeSkyboxEnabledForTests(
                toggleEnabled: true,
                hasCubemap: true,
                effective2D: false,
                isUrp2DRenderer: true));
        }

        [Test]
        public void SpritePreviewInitialOrthoSizeForTests_UsesLargestScreenAxis()
        {
            Bounds bounds = new Bounds(Vector3.zero, new UnityEngine.Vector3(8f, 2f, 0.1f));
            float size = SpritePrefabPreviewSession.ComputeInitialOrthoSizeForBoundsForTests(bounds, aspect: 2f);
            Assert.Greater(size, 1.9f);
        }

        [Test]
        public void SpriteColliderPreviewTintForTests_PreservesAlphaAndGraysRgb()
        {
            Color tinted = SpritePrefabPreviewSession.ComputeColliderPreviewTintForTests(new Color(0.2f, 0.8f, 0.4f, 0.35f));
            Assert.AreEqual(tinted.r, tinted.g, 0.0001f);
            Assert.AreEqual(tinted.g, tinted.b, 0.0001f);
            Assert.AreEqual(0.35f, tinted.a, 0.0001f);
        }

    }
}
