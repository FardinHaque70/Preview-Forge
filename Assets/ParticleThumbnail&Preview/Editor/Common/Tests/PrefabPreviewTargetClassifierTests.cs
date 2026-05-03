using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PrefabPreviewTargetClassifierTests
    {
        private const string TempTestRoot = "Assets/__Temp_ParticleThumbnailAndPreviewTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TempTestRoot);
            AssetDatabase.CreateFolder("Assets", "__Temp_ParticleThumbnailAndPreviewTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempTestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Classify_ParticleRootPrefab_ReturnsParticle()
        {
            GameObject root = new GameObject("ParticleRoot");
            root.AddComponent<ParticleSystem>();
            string prefabPath = $"{TempTestRoot}/ParticleRoot.prefab";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            PrefabPreviewTargetKind kind = PrefabPreviewTargetClassifier.Classify(prefab);
            Assert.AreEqual(PrefabPreviewTargetKind.Particle, kind);
        }

        [Test]
        public void Classify_ModelRendererPrefab_ReturnsModel()
        {
            GameObject root = new GameObject("ModelRoot");
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);
            string prefabPath = $"{TempTestRoot}/ModelRoot.prefab";

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            PrefabPreviewTargetKind kind = PrefabPreviewTargetClassifier.Classify(prefab);
            Assert.AreEqual(PrefabPreviewTargetKind.Model, kind);
        }

        [Test]
        public void Classify_NullOrMultiTargetArray_ReturnsUnsupported()
        {
            Assert.AreEqual(PrefabPreviewTargetKind.Unsupported, PrefabPreviewTargetClassifier.Classify((Object[])null));
            Assert.AreEqual(PrefabPreviewTargetKind.Unsupported, PrefabPreviewTargetClassifier.Classify(new Object[0]));

            GameObject a = new GameObject("A");
            GameObject b = new GameObject("B");
            Assert.AreEqual(
                PrefabPreviewTargetKind.Unsupported,
                PrefabPreviewTargetClassifier.Classify(new Object[] { a, b }));
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }
    }
}
