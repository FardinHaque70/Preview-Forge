using NUnit.Framework;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PreviewModeResolverTests
    {
        [Test]
        public void ResolveEffective2DForTests_Force2D_AlwaysTrue()
        {
            bool result = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Force2D,
                isUrp2DRenderer: false,
                isEditorDefaultBehavior2D: false);

            Assert.IsTrue(result);
        }

        [Test]
        public void ResolveEffective2DForTests_Force3D_AlwaysFalse()
        {
            bool result = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Force3D,
                isUrp2DRenderer: true,
                isEditorDefaultBehavior2D: true);

            Assert.IsFalse(result);
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public void ResolveEffective2DForTests_Auto_UsesUrp2DOrEditor2D(bool isUrp2DRenderer, bool isEditorDefaultBehavior2D, bool expected)
        {
            bool result = PreviewModeResolver.ResolveEffective2DForTests(
                PreviewModeOverride.Auto,
                isUrp2DRenderer,
                isEditorDefaultBehavior2D);

            Assert.AreEqual(expected, result);
        }
    }
}
