using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PreviewHookSafetyTests
    {
        private const string TempTestRoot = "Assets/__Temp_ParticleThumbnailAndPreviewPreviewHookTests";
        private Object[] _previousSelection;

        [SetUp]
        public void SetUp()
        {
            _previousSelection = Selection.objects;
            AssetDatabase.DeleteAsset(TempTestRoot);
            AssetDatabase.CreateFolder("Assets", "__Temp_ParticleThumbnailAndPreviewPreviewHookTests");
            Selection.objects = System.Array.Empty<Object>();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _previousSelection ?? System.Array.Empty<Object>();
            AssetDatabase.DeleteAsset(TempTestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ComputeIsUnsafeTransitionForTests_ReturnsTrueWhenAnyTransitionFlagIsSet()
        {
            Assert.IsFalse(PreviewEditorTransitionGuard.ComputeIsUnsafeTransitionForTests(false, false, false));
            Assert.IsTrue(PreviewEditorTransitionGuard.ComputeIsUnsafeTransitionForTests(true, false, false));
            Assert.IsTrue(PreviewEditorTransitionGuard.ComputeIsUnsafeTransitionForTests(false, true, false));
            Assert.IsTrue(PreviewEditorTransitionGuard.ComputeIsUnsafeTransitionForTests(false, false, true));
        }

        [Test]
        public void ShouldSuppressCompetingPreview_DoesNotFallbackToSelectedPrefabWhenInspectedTargetIsUnsupported()
        {
            GameObject previewableRoot = new GameObject("PreviewableRoot");
            previewableRoot.AddComponent<ParticleSystem>();
            string prefabPath = $"{TempTestRoot}/PreviewableRoot.prefab";
            GameObject previewablePrefab = PrefabUtility.SaveAsPrefabAsset(previewableRoot, prefabPath);
            Object.DestroyImmediate(previewableRoot);

            GameObject unsupportedSceneObject = new GameObject("UnsupportedSceneObject");
            Selection.activeObject = previewablePrefab;

            bool shouldSuppress = PrefabPreviewTargetGate.ShouldSuppressCompetingPreview(
                new Object[] { unsupportedSceneObject },
                previewActive: true);

            Object.DestroyImmediate(unsupportedSceneObject);
            Assert.IsFalse(shouldSuppress);
        }
    }
}
