using NUnit.Framework;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PreviewToolbarCommonFeaturesTests
    {
        [Test]
        public void GridToggle_UpdatesSessionStateAndRequestsRepaint()
        {
            var session = new FakeCommonSession();
            int repaintCount = 0;
            PreviewToolbarCommonFeatureBinding binding = PreviewToolbarCommonFeatures.CreateGridToggle(
                session,
                () => repaintCount++,
                iconNames: null);

            binding.Item.OnToggleChanged?.Invoke(false);

            Assert.IsFalse(session.GridEnabled);
            Assert.AreEqual(1, repaintCount);
        }

        [Test]
        public void BoundsToggle_UpdatesSessionStateAndRequestsRepaint()
        {
            var session = new FakeCommonSession();
            int repaintCount = 0;
            PreviewToolbarCommonFeatureBinding binding = PreviewToolbarCommonFeatures.CreateBoundsToggle(
                session,
                () => repaintCount++,
                iconNames: null);

            binding.Item.OnToggleChanged?.Invoke(false);

            Assert.IsFalse(session.BoundsOverlayEnabled);
            Assert.AreEqual(1, repaintCount);
        }

        [Test]
        public void ColliderToggle_UpdatesSessionStateAndRequestsRepaint()
        {
            var session = new FakeCommonSession();
            int repaintCount = 0;
            PreviewToolbarCommonFeatureBinding binding = PreviewToolbarCommonFeatures.CreateColliderToggle(
                session,
                () => repaintCount++,
                iconNames: null);

            binding.Item.OnToggleChanged?.Invoke(true);

            Assert.IsTrue(session.ColliderOverlayEnabled);
            Assert.AreEqual(1, repaintCount);
        }

        [Test]
        public void ModeButton_CyclesSessionModeAndRefreshReflectsEffectiveMode()
        {
            var session = new FakeCommonSession
            {
                ModeOverride = PreviewModeOverride.Force3D,
                ModeContext = new PreviewModeContext(
                    PreviewModeOverride.Force3D,
                    PreviewRenderPipelineKind.BuiltIn,
                    isEditorDefaultBehavior2D: false,
                    isUrp2DRenderer: false,
                    effective2D: false),
            };

            PreviewToolbarCommonFeatureBinding binding = PreviewToolbarCommonFeatures.CreateModeButton(
                session,
                requestRepaint: null);

            PreviewToolbarCommonFeatures.Refresh(session, binding);
            Assert.AreEqual("3D", binding.Item.FallbackText);
            Assert.IsFalse(binding.Item.IsActive);

            binding.Item.OnClick?.Invoke();

            Assert.AreEqual(1, session.CycleModeOverrideCallCount);
            PreviewToolbarCommonFeatures.Refresh(session, binding);
            Assert.AreEqual("2D", binding.Item.FallbackText);
            Assert.IsTrue(binding.Item.IsActive);
        }

        private sealed class FakeCommonSession : IPreviewToolbarCommonSession
        {
            public bool BoundsOverlayEnabled { get; set; } = true;
            public bool GridEnabled { get; set; } = true;
            public bool ColliderOverlayEnabled { get; set; }
            public PreviewModeOverride ModeOverride { get; set; } = PreviewModeOverride.Force2D;
            public PreviewModeContext ModeContext { get; set; } = new(
                PreviewModeOverride.Force2D,
                PreviewRenderPipelineKind.BuiltIn,
                isEditorDefaultBehavior2D: false,
                isUrp2DRenderer: false,
                effective2D: true);
            public int CycleModeOverrideCallCount { get; private set; }

            public void CycleModeOverride()
            {
                CycleModeOverrideCallCount++;
                ModeOverride = ModeOverride == PreviewModeOverride.Force2D
                    ? PreviewModeOverride.Force3D
                    : PreviewModeOverride.Force2D;
                ModeContext = new PreviewModeContext(
                    ModeOverride,
                    PreviewRenderPipelineKind.BuiltIn,
                    isEditorDefaultBehavior2D: false,
                    isUrp2DRenderer: false,
                    effective2D: ModeOverride == PreviewModeOverride.Force2D);
            }
        }
    }
}
