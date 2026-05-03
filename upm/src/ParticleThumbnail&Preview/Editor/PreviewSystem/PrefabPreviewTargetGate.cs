using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
// Safely inspects Unity inspector target context and gates preview activation to supported, conflict-safe target scenarios.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PrefabPreviewTargetGate
    {
        public static bool IsSupportedTarget(UnityObject[] targets)
        {
            return PrefabPreviewTargetClassifier.IsSupportedTarget(targets);
        }

        public static bool IsSupportedTarget(GameObject prefab)
        {
            return PrefabPreviewTargetClassifier.Classify(prefab) != PrefabPreviewTargetKind.Unsupported;
        }

        public static PrefabPreviewTargetKind GetTargetKind(UnityObject[] targets)
        {
            return PrefabPreviewTargetClassifier.Classify(targets);
        }

        public static PrefabPreviewTargetKind GetTargetKind(GameObject prefab)
        {
            return PrefabPreviewTargetClassifier.Classify(prefab);
        }

        public static bool ShouldSuppressCompetingPreview(UnityObject[] targets)
        {
            bool anyPreviewEnabled = PreviewSettings.AnyPrefabCustomPreviewActive;
            return ShouldSuppressCompetingPreview(targets, anyPreviewEnabled);
        }

        internal static bool ShouldSuppressCompetingPreview(UnityObject[] targets, bool previewActive)
        {
            if (!previewActive)
                return false;

            PrefabPreviewTargetKind kind = GetTargetKind(targets);
            if (kind != PrefabPreviewTargetKind.Unsupported)
                return IsKindEnabled(kind);

            if (!IsInspectorSourceBackedSupportedPrefab(targets))
                return false;

            GameObject resolved = PrefabPreviewTargetClassifier.ResolvePrefabAsset(targets != null && targets.Length > 0 ? targets[0] : null);
            PrefabPreviewTargetKind sourceKind = GetTargetKind(resolved);
            return IsKindEnabled(sourceKind);
        }

        public static UnityObject[] TryGetObjectPreviewTargets(object objectPreviewInstance)
        {
            if (objectPreviewInstance == null)
                return null;

            try
            {
                Type type = objectPreviewInstance.GetType();
                while (type != null)
                {
                    FieldInfo targetsField = type.GetField(
                        "m_Targets",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (targetsField != null)
                        return targetsField.GetValue(objectPreviewInstance) as UnityObject[];

                    type = type.BaseType;
                }
            }
            catch
            {
                // Keep gate safe: return null when Unity internals differ.
            }

            return null;
        }

        private static bool IsInspectorSourceBackedSupportedPrefab(UnityObject[] targets)
        {
            if (targets == null || targets.Length != 1)
                return false;

            if (TryResolveSourceSupportedPrefab(targets[0], out _))
                return true;

            if (Selection.activeObject is GameObject selectedPrefab && IsSupportedTarget(selectedPrefab))
                return true;

            return false;
        }

        private static bool TryResolveSourceSupportedPrefab(UnityObject target, out GameObject prefabAsset)
        {
            prefabAsset = PrefabPreviewTargetClassifier.ResolvePrefabAsset(target);
            return prefabAsset != null && IsSupportedTarget(prefabAsset);
        }

        private static bool IsKindEnabled(PrefabPreviewTargetKind kind)
        {
            return kind switch
            {
                PrefabPreviewTargetKind.Particle => PreviewSettings.ParticlePrefabPreviewActive,
                PrefabPreviewTargetKind.Model => PreviewSettings.ModelPreviewActive,
                _ => false,
            };
        }
    }
}
