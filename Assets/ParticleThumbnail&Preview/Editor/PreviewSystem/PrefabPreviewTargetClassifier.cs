using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
// Classifies prefab targets into supported preview categories so the host can choose the proper rendering pipeline.

namespace ParticleThumbnailAndPreview.Editor
{
    internal enum PrefabPreviewTargetKind
    {
        Unsupported = 0,
        Particle = 1,
        Model = 2,
        Sprite = 3,
    }

    internal static class PrefabPreviewTargetClassifier
    {
        private static readonly System.Type TmpTextMeshProType = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
        private static readonly System.Type TmpTextMeshProUiType = System.Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        public static bool IsSupportedTarget(UnityObject[] targets)
        {
            return Classify(targets) != PrefabPreviewTargetKind.Unsupported;
        }

        public static PrefabPreviewTargetKind Classify(UnityObject[] targets)
        {
            if (targets == null || targets.Length != 1)
                return PrefabPreviewTargetKind.Unsupported;

            return Classify(ResolvePrefabAsset(targets[0]));
        }

        public static PrefabPreviewTargetKind Classify(GameObject prefabAsset)
        {
            if (!IsPersistentPrefabAsset(prefabAsset))
                return PrefabPreviewTargetKind.Unsupported;

            bool hasModelRenderer = HasSupportedModelRenderer(prefabAsset);
            bool hasSpriteRenderer = HasSupportedSpriteRenderer(prefabAsset);
            bool rootHasParticleSystem = prefabAsset.GetComponent<ParticleSystem>() != null;

            if (hasModelRenderer)
                return PrefabPreviewTargetKind.Model;

            if (hasSpriteRenderer)
                return PrefabPreviewTargetKind.Sprite;

            if (rootHasParticleSystem)
                return PrefabPreviewTargetKind.Particle;

            return PrefabPreviewTargetKind.Unsupported;
        }

        public static GameObject ResolvePrefabAsset(UnityObject target)
        {
            if (target == null)
                return null;

            GameObject go = target as GameObject;
            if (go == null && target is Component component)
                go = component.gameObject;
            if (go == null)
                return null;

            if (IsPersistentPrefabAsset(go))
                return go;

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            return IsPersistentPrefabAsset(source) ? source : null;
        }

        private static bool IsPersistentPrefabAsset(GameObject prefab)
        {
            if (prefab == null)
                return false;

            if (!EditorUtility.IsPersistent(prefab))
                return false;

            if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
                return false;

            PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefab);
            if (assetType != PrefabAssetType.Regular && assetType != PrefabAssetType.Variant)
                return false;

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            return assetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSupportedModelRenderer(GameObject prefab)
        {
            if (prefab == null)
                return false;

            MeshRenderer[] meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer renderer = meshRenderers[i];
                if (renderer == null)
                    continue;

                if (IsTmpRenderer(renderer))
                    continue;

                return true;
            }

            SkinnedMeshRenderer[] skinnedRenderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[i];
                if (renderer == null)
                    continue;

                if (IsTmpRenderer(renderer))
                    continue;

                return true;
            }

            return false;
        }

        private static bool HasSupportedSpriteRenderer(GameObject prefab)
        {
            if (prefab == null)
                return false;

            SpriteRenderer[] spriteRenderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                    return true;
            }

            return false;
        }

        private static bool IsTmpRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            Component component = renderer as Component;
            if (component == null)
                return false;

            if (TmpTextMeshProType != null && component.GetComponent(TmpTextMeshProType) != null)
                return true;

            if (TmpTextMeshProUiType != null && component.GetComponent(TmpTextMeshProUiType) != null)
                return true;

            return false;
        }
    }
}
