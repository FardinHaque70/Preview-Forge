using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class ParticlePreviewAutoSelector
    {
        private const string ThisPreviewTypeName = "ParticleThumbnailAndPreview.Editor.ParticlePrefabPreviewEditor";
        private const int MaxRetryFrames = 30;
        private const bool EnableDiagnostics = false;

        private static readonly string[] EditorAssemblyPreferenceOrder =
        {
            "UnityEditor.CoreModule",
            "UnityEditor",
        };

        private static readonly Type PropertyEditorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.PropertyEditor");

        // Prefer the internal method Unity uses to select previews; this also
        // routes internal input/cursor ownership correctly instead of just
        // flipping the backing field.
        private static readonly MethodInfo SelectPreviewMethod =
            GetInstanceMethod(PropertyEditorType, "SelectPreview")          // Unity 6+
            ?? GetInstanceMethod(PropertyEditorType, "SetSelectedPreview")  // older spellings
            ?? GetInstanceMethod(PropertyEditorType, "OnPreviewSelected");

        private static readonly MethodInfo GetInspectedObjectMethod  = GetInstanceMethod(PropertyEditorType, "GetInspectedObject");
        private static readonly FieldInfo  PreviewsField             = GetInstanceField(PropertyEditorType, "m_Previews");

        // Only used as a last-resort fallback when the internal method is absent.
        private static readonly FieldInfo SelectedPreviewField = GetInstanceField(PropertyEditorType, "m_SelectedPreview");

        private static int    _framesRemaining;
        private static bool   _autoSelectPending;
        private static readonly Dictionary<int, int> CreatePreviewablesAttemptsByEditor = new();
        private static string _scheduledSelectionKey;
        private static string _appliedSelectionKey;

        static ParticlePreviewAutoSelector()
        {
            Selection.selectionChanged          += ScheduleAutoSelect;
            ParticlePreviewSettings.SettingsChanged += ScheduleAutoSelect;
            ScheduleAutoSelect();
        }

        internal static void ScheduleAutoSelect()
        {
            EditorApplication.update -= OnEditorUpdate;
            CreatePreviewablesAttemptsByEditor.Clear();

            if (!ParticlePreviewSettings.Active || !ParticlePreviewTargetGate.IsSupportedTarget(Selection.objects))
            {
                _autoSelectPending        = false;
                _framesRemaining          = 0;
                _scheduledSelectionKey    = null;
                return;
            }

            string selectionKey = BuildSelectionKey(Selection.objects);
            if (!string.IsNullOrEmpty(selectionKey)
                && string.Equals(selectionKey, _appliedSelectionKey, StringComparison.Ordinal)
                && HasSelectedPreviewInOpenPropertyEditors())
            {
                LogDiagnostic($"Skip duplicate auto-select for same target: {selectionKey}");
                _autoSelectPending     = false;
                _framesRemaining       = 0;
                _scheduledSelectionKey = selectionKey;
                return;
            }

            _autoSelectPending     = true;
            _framesRemaining       = MaxRetryFrames;
            _scheduledSelectionKey = selectionKey;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (!_autoSelectPending)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            if (!ParticlePreviewSettings.Active || !ParticlePreviewTargetGate.IsSupportedTarget(Selection.objects))
            {
                _autoSelectPending = false;
                _framesRemaining   = 0;
                _scheduledSelectionKey = null;
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            bool applied = TrySelectInOpenPropertyEditors();
            _framesRemaining--;

            if (applied || _framesRemaining <= 0)
            {
                if (applied)
                {
                    _appliedSelectionKey = _scheduledSelectionKey;
                    LogDiagnostic($"Applied preview auto-select for: {_appliedSelectionKey}");
                }

                _autoSelectPending = false;
                _framesRemaining   = 0;
                EditorApplication.update -= OnEditorUpdate;
            }
        }

        // ── Core selection logic ─────────────────────────────────────────────

        private static bool TrySelectInOpenPropertyEditors()
        {
            if (PropertyEditorType == null || PreviewsField == null)
                return false;

            if (SelectPreviewMethod == null && SelectedPreviewField == null)
                return false;

            bool applied = false;
            UnityObject[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);

            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null || !ShouldApplyToPropertyEditor(propertyEditor))
                    continue;

                try
                {
                    if (!TryGetInspectedObject(propertyEditor, out UnityObject _))
                        continue;

                    IList previews    = PreviewsField.GetValue(propertyEditor) as IList;
                    object ourPreview = FindPreviewByTypeName(previews, ThisPreviewTypeName);

                    if (ourPreview == null)
                        continue;

                    bool didSelect = TrySelectPreview(propertyEditor, ourPreview);
                    if (!didSelect)
                        continue;

                    (propertyEditor as EditorWindow)?.Repaint();
                    applied = true;
                }
                catch
                {
                    // Stay resilient to Unity internals changing.
                }
            }

            return applied;
        }

        /// <summary>
        /// Tries to select the preview via Unity's internal method first.
        /// Falls back to direct field assignment only when the method is absent.
        /// </summary>
        private static bool TrySelectPreview(UnityObject propertyEditor, object preview)
        {
            if (propertyEditor == null || preview == null)
                return false;

            if (IsPreviewAlreadySelected(propertyEditor, preview))
                return true;

            // ── Path 1: call Unity's internal SelectPreview / SetSelectedPreview ──
            if (SelectPreviewMethod != null)
            {
                try
                {
                    SelectPreviewMethod.Invoke(propertyEditor, new[] { preview });
                    return true;
                }
                catch
                {
                    // Method signature mismatch — fall through to field path.
                }
            }

            // ── Path 2: direct field assignment (last resort) ─────────────────
            if (SelectedPreviewField != null)
            {
                try
                {
                    object current = SelectedPreviewField.GetValue(propertyEditor);
                    if (ReferenceEquals(current, preview))
                        return true; // already selected

                    SelectedPreviewField.SetValue(propertyEditor, preview);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool HasSelectedPreviewInOpenPropertyEditors()
        {
            if (PropertyEditorType == null || PreviewsField == null)
                return false;

            UnityObject[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null || !ShouldApplyToPropertyEditor(propertyEditor))
                    continue;

                try
                {
                    if (!TryGetInspectedObject(propertyEditor, out UnityObject _))
                        continue;

                    IList previews = PreviewsField.GetValue(propertyEditor) as IList;
                    object ourPreview = FindPreviewByTypeName(previews, ThisPreviewTypeName);
                    if (ourPreview == null)
                        continue;

                    if (IsPreviewAlreadySelected(propertyEditor, ourPreview))
                        return true;
                }
                catch
                {
                    // Stay resilient to Unity internals changing.
                }
            }

            return false;
        }

        private static bool IsPreviewAlreadySelected(UnityObject propertyEditor, object preview)
        {
            if (propertyEditor == null || preview == null || SelectedPreviewField == null)
                return false;

            try
            {
                object current = SelectedPreviewField.GetValue(propertyEditor);
                if (ReferenceEquals(current, preview))
                    return true;

                Type currentType = current?.GetType();
                Type previewType = preview.GetType();
                return currentType != null
                    && previewType != null
                    && string.Equals(currentType.FullName, previewType.FullName, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // ── Helpers (unchanged from original) ────────────────────────────────

        private static bool TryGetInspectedObject(UnityObject propertyEditor, out UnityObject inspectedObject)
        {
            inspectedObject = null;
            if (GetInspectedObjectMethod == null || propertyEditor == null)
                return true;
            try
            {
                inspectedObject = GetInspectedObjectMethod.Invoke(propertyEditor, null) as UnityObject;
            }
            catch { return true; }
            return inspectedObject != null;
        }

        private static bool ShouldApplyToPropertyEditor(UnityObject propertyEditor)
        {
            if (propertyEditor == null)
                return false;
            if (GetInspectedObjectMethod != null)
            {
                try
                {
                    UnityObject inspected = GetInspectedObjectMethod.Invoke(propertyEditor, null) as UnityObject;
                    if (inspected != null)
                        return ParticlePreviewTargetGate.ShouldSuppressCompetingPreview(new[] { inspected });
                }
                catch { }
            }
            return ParticlePreviewTargetGate.IsSupportedTarget(Selection.objects);
        }

        private static string BuildSelectionKey(UnityObject[] targets)
        {
            if (targets == null || targets.Length != 1 || targets[0] == null)
                return null;

            if (TryGetPrefabAssetPath(targets[0], out string assetPath))
                return "asset:" + assetPath;

            return null;
        }

        private static bool TryGetPrefabAssetPath(UnityObject target, out string assetPath)
        {
            assetPath = null;
            if (target == null)
                return false;

            GameObject go = target as GameObject;
            if (go == null && target is Component component)
                go = component.gameObject;
            if (go == null)
                return false;

            if (ParticlePreviewTargetGate.IsSupportedTarget(go))
            {
                assetPath = AssetDatabase.GetAssetPath(go);
                return !string.IsNullOrEmpty(assetPath);
            }

            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (!ParticlePreviewTargetGate.IsSupportedTarget(source))
                return false;

            assetPath = AssetDatabase.GetAssetPath(source);
            return !string.IsNullOrEmpty(assetPath);
        }

        private static object FindPreviewByTypeName(IList previews, string fullTypeName)
        {
            if (previews == null || string.IsNullOrEmpty(fullTypeName))
                return null;
            for (int i = 0; i < previews.Count; i++)
            {
                object preview = previews[i];
                if (preview == null) continue;
                Type t = preview.GetType();
                if (t != null && string.Equals(t.FullName, fullTypeName, StringComparison.Ordinal))
                    return preview;
            }
            return null;
        }

        private static void LogDiagnostic(string message)
        {
            if (!EnableDiagnostics || string.IsNullOrEmpty(message)) return;
            Debug.Log("[ParticlePreview][AutoSelector] " + message);
        }

        private static Type ResolveEditorTypeAcrossAssemblies(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return null;
            for (int i = 0; i < EditorAssemblyPreferenceOrder.Length; i++)
            {
                Type t = Type.GetType(fullTypeName + ", " + EditorAssemblyPreferenceOrder[i], false);
                if (t != null) return t;
            }
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type t = assemblies[i].GetType(fullTypeName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static MethodInfo GetInstanceMethod(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static FieldInfo GetInstanceField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
