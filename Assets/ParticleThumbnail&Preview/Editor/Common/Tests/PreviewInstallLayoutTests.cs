using NUnit.Framework;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PreviewInstallLayoutTests
    {
        [Test]
        public void BuildAssetPaths_ResolvesAssetsAndPackageRootsInOrder()
        {
            string[] paths = PreviewInstallLayout.BuildAssetPaths("Editor/Common/PreviewAssets/Skybox/PreviewSkybox.mat");

            Assert.AreEqual(2, paths.Length);
            Assert.AreEqual(
                "Assets/ParticleThumbnail&Preview/Editor/Common/PreviewAssets/Skybox/PreviewSkybox.mat",
                paths[0]);
            Assert.AreEqual(
                "Packages/com.fardinhaque.particle-thumbnail-preview/ParticleThumbnail&Preview/Editor/Common/PreviewAssets/Skybox/PreviewSkybox.mat",
                paths[1]);
        }

        [Test]
        public void BuildAssetPaths_TrimsLeadingSlashFromRelativePath()
        {
            string[] paths = PreviewInstallLayout.BuildAssetPaths("/Editor/Common/PreviewAssets/ToolbarIcons/Particle_Info_Round_White.png");

            Assert.AreEqual(
                "Assets/ParticleThumbnail&Preview/Editor/Common/PreviewAssets/ToolbarIcons/Particle_Info_Round_White.png",
                paths[0]);
        }
    }
}
