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
            if (!previewActive || PreviewEditorTransitionGuard.IsUnsafeTransition())
                return false;

            PrefabPreviewTargetKind kind = GetTargetKind(targets);
            return kind != PrefabPreviewTargetKind.Unsupported && IsKindEnabled(kind);
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

        private static bool IsKindEnabled(PrefabPreviewTargetKind kind)
        {
            return kind switch
            {
                PrefabPreviewTargetKind.Particle => PreviewSettings.ParticlePrefabPreviewActive,
                PrefabPreviewTargetKind.Model => PreviewSettings.ModelPreviewActive,
                PrefabPreviewTargetKind.Sprite => PreviewSettings.SpritePrefabPreviewActive,
                _ => false,
            };
        }
    }
}
