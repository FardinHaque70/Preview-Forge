using NUnit.Framework;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class ModelPreviewEnvironmentTests
    {
        [Test]
        public void ClassifyPipelineKindForTests_BuiltInWhenPipelineNameMissing()
        {
            ParticleRenderPipelineKind kind = ParticleRenderCompatibilityUtility.ClassifyPipelineKindForTests(null, null);
            Assert.AreEqual(ParticleRenderPipelineKind.BuiltIn, kind);
        }

        [Test]
        public void ClassifyPipelineKindForTests_RecognizesUrp2DAndUrp3D()
        {
            ParticleRenderPipelineKind urp2D = ParticleRenderCompatibilityUtility.ClassifyPipelineKindForTests(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                "Renderer2DData");
            ParticleRenderPipelineKind urp3D = ParticleRenderCompatibilityUtility.ClassifyPipelineKindForTests(
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                "ForwardRendererData");

            Assert.AreEqual(ParticleRenderPipelineKind.Urp2D, urp2D);
            Assert.AreEqual(ParticleRenderPipelineKind.Urp3D, urp3D);
        }

        [Test]
        public void ClassifyPipelineKindForTests_RecognizesHdrp()
        {
            ParticleRenderPipelineKind kind = ParticleRenderCompatibilityUtility.ClassifyPipelineKindForTests(
                "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset",
                null);
            Assert.AreEqual(ParticleRenderPipelineKind.Hdrp, kind);
        }

        [Test]
        public void ShouldPreferSrpRenderForTests_IsFalseForBuiltIn_TrueForSrp()
        {
            Assert.IsFalse(ParticleRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(ParticleRenderPipelineKind.BuiltIn));
            Assert.IsTrue(ParticleRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(ParticleRenderPipelineKind.Urp3D));
            Assert.IsTrue(ParticleRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(ParticleRenderPipelineKind.Urp2D));
            Assert.IsTrue(ParticleRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(ParticleRenderPipelineKind.Hdrp));
            Assert.IsTrue(ParticleRenderCompatibilityUtility.ShouldPreferSrpRenderForTests(ParticleRenderPipelineKind.UnknownSrp));
        }
    }
}
