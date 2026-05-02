using NUnit.Framework;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PreviewModeResolverTests
    {
        [Test]
        public void AutoMode_Urp2D_ResolvesEffective2D()
        {
            bool effective2D = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Auto,
                isUrp2DRenderer: true,
                isEditorDefaultBehavior2D: false);

            Assert.That(effective2D, Is.True);
        }

        [Test]
        public void AutoMode_Editor2DBehavior_ResolvesEffective2D()
        {
            bool effective2D = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Auto,
                isUrp2DRenderer: false,
                isEditorDefaultBehavior2D: true);

            Assert.That(effective2D, Is.True);
        }

        [Test]
        public void ForceOverrides_AreRespected()
        {
            bool forced2D = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Force2D,
                isUrp2DRenderer: false,
                isEditorDefaultBehavior2D: false);
            bool forced3D = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Force3D,
                isUrp2DRenderer: true,
                isEditorDefaultBehavior2D: true);

            Assert.That(forced2D, Is.True);
            Assert.That(forced3D, Is.False);
        }

        [Test]
        public void ModelInitialDistanceComputation_DiffersBetween2DAnd3D()
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 1f));
            float twoD = ModelPrefabPreviewSession.ComputeInitialDistanceForBoundsForTests(bounds, effective2D: true, cameraFov: 30f);
            float threeD = ModelPrefabPreviewSession.ComputeInitialDistanceForBoundsForTests(bounds, effective2D: false, cameraFov: 30f);

            Assert.That(twoD, Is.GreaterThan(0f));
            Assert.That(threeD, Is.GreaterThan(0f));
            Assert.That(twoD, Is.Not.EqualTo(threeD));
        }
    }
}
