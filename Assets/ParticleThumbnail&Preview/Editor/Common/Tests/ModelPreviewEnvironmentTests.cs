using NUnit.Framework;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class ModelPreviewEnvironmentTests
    {
        [Test]
        public void ModelLightIntensityClamp_RespectsBounds()
        {
            float below = ParticlePreviewSettings.ClampModelLightIntensityForTests(-10f);
            float above = ParticlePreviewSettings.ClampModelLightIntensityForTests(999f);
            float mid = ParticlePreviewSettings.ClampModelLightIntensityForTests(2.5f);

            Assert.That(below, Is.EqualTo(ParticlePreviewSettings.MinModelLightIntensity));
            Assert.That(above, Is.EqualTo(ParticlePreviewSettings.MaxModelLightIntensity));
            Assert.That(mid, Is.EqualTo(2.5f));
        }

        [Test]
        public void ModelSkyboxResolution_ToggleAndCubemapAndModeAreRespected()
        {
            Assert.That(ModelPrefabPreviewSession.ComputeSkyboxEnabledForTests(toggleEnabled: true, hasCubemap: true, effective2D: false), Is.True);
            Assert.That(ModelPrefabPreviewSession.ComputeSkyboxEnabledForTests(toggleEnabled: true, hasCubemap: false, effective2D: false), Is.False);
            Assert.That(ModelPrefabPreviewSession.ComputeSkyboxEnabledForTests(toggleEnabled: true, hasCubemap: true, effective2D: true), Is.False);
        }

        [Test]
        public void ModelLightingResolution_DisablesInEffective2D()
        {
            Assert.That(ModelPrefabPreviewSession.ComputeLightingEnabledForTests(toggleEnabled: true, effective2D: false), Is.True);
            Assert.That(ModelPrefabPreviewSession.ComputeLightingEnabledForTests(toggleEnabled: true, effective2D: true), Is.False);
            Assert.That(ModelPrefabPreviewSession.ComputeLightingEnabledForTests(toggleEnabled: false, effective2D: false), Is.False);
        }

        [Test]
        public void ModelEnvironmentDefaults_AreInitialized()
        {
            Assert.That(ParticlePreviewSettings.D_ModelDefaultLightingEnabled, Is.True);
            Assert.That(ParticlePreviewSettings.D_ModelDefaultSkyboxEnabled, Is.True);
            Assert.That(ParticlePreviewSettings.D_ModelKeyLightIntensity, Is.GreaterThan(0f));
            Assert.That(ParticlePreviewSettings.D_ModelFillLightIntensity, Is.GreaterThan(0f));
            Assert.That(ParticlePreviewSettings.D_ModelRimLightIntensity, Is.GreaterThanOrEqualTo(0f));
            Assert.That(ParticlePreviewSettings.D_ModelAmbientColor, Is.Not.EqualTo(default(Color)));
        }
    }
}
