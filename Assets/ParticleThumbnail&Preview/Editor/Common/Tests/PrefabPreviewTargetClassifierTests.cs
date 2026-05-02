using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public class PrefabPreviewTargetClassifierTests
    {
        private string _rootFolder;

        [SetUp]
        public void SetUp()
        {
            string folderName = "TempPrefabPreviewTests_" + System.Guid.NewGuid().ToString("N");
            string guid = AssetDatabase.CreateFolder("Assets", folderName);
            _rootFolder = AssetDatabase.GUIDToAssetPath(guid);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_rootFolder))
                AssetDatabase.DeleteAsset(_rootFolder);

            _rootFolder = null;
        }

        [Test]
        public void RootParticleOnlyPrefab_IsParticle()
        {
            GameObject root = new GameObject("RootParticle");
            root.AddComponent<ParticleSystem>();
            string path = CreatePrefab(root, "RootParticle.prefab");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(PrefabPreviewTargetClassifier.Classify(prefab), Is.EqualTo(PrefabPreviewTargetKind.Particle));
        }

        [Test]
        public void MeshPrefab_IsModel()
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "MeshRoot";
            string path = CreatePrefab(root, "MeshRoot.prefab");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(PrefabPreviewTargetClassifier.Classify(prefab), Is.EqualTo(PrefabPreviewTargetKind.Model));
        }

        [Test]
        public void MixedParticleAndMeshPrefab_IsModel()
        {
            GameObject root = new GameObject("MixedRoot");
            root.AddComponent<ParticleSystem>();
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);

            string path = CreatePrefab(root, "MixedRoot.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(PrefabPreviewTargetClassifier.Classify(prefab), Is.EqualTo(PrefabPreviewTargetKind.Model));
        }

        [Test]
        public void TmpOnlyMeshPrefab_IsUnsupportedForModel()
        {
            System.Type tmpType = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            Assume.That(tmpType, Is.Not.Null, "TMP is not installed in this project.");

            GameObject root = new GameObject("TmpOnly");
            root.AddComponent<MeshFilter>();
            root.AddComponent<MeshRenderer>();
            root.AddComponent(tmpType);

            string path = CreatePrefab(root, "TmpOnly.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(PrefabPreviewTargetClassifier.Classify(prefab), Is.EqualTo(PrefabPreviewTargetKind.Unsupported));
        }

        private string CreatePrefab(GameObject instance, string fileName)
        {
            string path = _rootFolder + "/" + fileName;
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return path;
        }
    }
}
