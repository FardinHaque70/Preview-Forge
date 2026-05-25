using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor.Tests
{
    public sealed class PrefabPreviewTargetClassifierTests
    {
        private const string AssemblyName = "ParticleThumbnailAndPreview.Editor";
        private const string UiPrefabPath = "Assets/Tests/UI/Image.prefab";
        private const string ModelPrefabPath = "Assets/Tests/Sphere.prefab";
        private const string SpritePrefabPath = "Assets/Tests/Circle.prefab";
        private const string ParticlePrefabPath = "Assets/Tests/Particle System.prefab";
        private const string GeneratedMixedPrefabPath = "Assets/ParticleThumbnail&Preview/Editor/Tests/Mixed Model Ui Generated.prefab";

        private static readonly Type TargetClassifierType =
            Type.GetType($"ParticleThumbnailAndPreview.Editor.PrefabPreviewTargetClassifier, {AssemblyName}");

        private static readonly Type TargetGateType =
            Type.GetType($"ParticleThumbnailAndPreview.Editor.PrefabPreviewTargetGate, {AssemblyName}");

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(GeneratedMixedPrefabPath);
        }

        [Test]
        public void Classify_ModelPrefab_ReturnsModel()
        {
            Assert.That(ClassifyName(LoadPrefab(ModelPrefabPath)), Is.EqualTo("Model"));
        }

        [Test]
        public void Classify_SpritePrefab_ReturnsSprite()
        {
            Assert.That(ClassifyName(LoadPrefab(SpritePrefabPath)), Is.EqualTo("Sprite"));
        }

        [Test]
        public void Classify_ParticlePrefab_ReturnsParticle()
        {
            Assert.That(ClassifyName(LoadPrefab(ParticlePrefabPath)), Is.EqualTo("Particle"));
        }

        [Test]
        public void Classify_UiPrefab_ReturnsUnsupported()
        {
            GameObject uiPrefab = LoadPrefab(UiPrefabPath);

            Assert.That(ClassifyName(uiPrefab), Is.EqualTo("Unsupported"));
            Assert.That(IsSupportedTarget(uiPrefab), Is.False);
        }

        [Test]
        public void Classify_MixedModelAndUiPrefab_ReturnsModel()
        {
            GameObject prefab = CreateMixedModelAndUiPrefab();

            Assert.That(ClassifyName(prefab), Is.EqualTo("Model"));
        }

        [Test]
        public void ShouldSuppressCompetingPreview_UiPrefab_ReturnsFalse()
        {
            GameObject uiPrefab = LoadPrefab(UiPrefabPath);
            bool shouldSuppress = (bool)InvokeMethod(
                TargetGateType,
                "ShouldSuppressCompetingPreview",
                new object[] { new UnityEngine.Object[] { uiPrefab } });

            Assert.That(shouldSuppress, Is.False);
        }

        private static GameObject CreateMixedModelAndUiPrefab()
        {
            AssetDatabase.DeleteAsset(GeneratedMixedPrefabPath);

            GameObject root = new GameObject("MixedModelAndUiRoot");
            try
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "ModelChild";
                cube.transform.SetParent(root.transform, false);

                GameObject uiRoot = new GameObject("UiChild", typeof(RectTransform), typeof(Canvas));
                uiRoot.transform.SetParent(root.transform, false);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, GeneratedMixedPrefabPath);
                Assert.That(prefab, Is.Not.Null, "Expected mixed model/UI prefab to save successfully.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static bool IsSupportedTarget(GameObject prefab)
        {
            return (bool)InvokeMethod(TargetClassifierType, "IsSupportedTarget", new object[] { new UnityEngine.Object[] { prefab } });
        }

        private static string ClassifyName(GameObject prefab)
        {
            object kind = InvokeMethod(TargetClassifierType, "Classify", new object[] { prefab });
            return kind.ToString();
        }

        private static GameObject LoadPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"Expected prefab fixture at '{path}'.");
            return prefab;
        }

        private static object InvokeMethod(Type type, string methodName, object[] args)
        {
            Assert.That(type, Is.Not.Null, $"Expected reflected type for {methodName}.");

            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Array.ConvertAll(args, arg => arg.GetType()),
                null);

            Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' on type '{type.FullName}'.");
            return method.Invoke(null, args);
        }
    }
}
