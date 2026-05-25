using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
// Keeps this custom preview selected in inspector hosts when possible, with resilient fallbacks across Unity internal API variations.

namespace ParticleThumbnailAndPreview.Editor
{
    [InitializeOnLoad]
    internal static class CustomPreviewAutoSelector
    {
        private const string PrefabPreviewTypeName = "ParticleThumbnailAndPreview.Editor.PrefabPreviewEditor";
        private const string ModelImporterPreviewTypeName = "ParticleThumbnailAndPreview.Editor.ModelImporterPreviewEditor";
        private const int MaxRetryFrames = 30;
        private const int ModelImporterRearmFrames = 12;

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

        private static readonly MethodInfo GetInspectedObjectMethod = GetInstanceMethod(PropertyEditorType, "GetInspectedObject");
        private static readonly FieldInfo PreviewsField = GetInstanceField(PropertyEditorType, "m_Previews");
        private static readonly FieldInfo PropertyEditorTrackerField = GetInstanceField(PropertyEditorType, "m_Tracker");
        // Only used as a last-resort fallback when the internal method is absent.
        private static readonly FieldInfo SelectedPreviewField = GetInstanceField(PropertyEditorType, "m_SelectedPreview");
        private static readonly PropertyInfo ActiveEditorTrackerActiveEditorsProperty =
            typeof(ActiveEditorTracker).GetProperty("activeEditors", BindingFlags.Instance | BindingFlags.Public);
        private static readonly Type AssetImporterEditorType =
            ResolveEditorTypeAcrossAssemblies("UnityEditor.AssetImporters.AssetImporterEditor");
        private static readonly PropertyInfo AssetImporterEditorShowImportedObjectProperty =
            AssetImporterEditorType?.GetProperty(
                "showImportedObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static int _framesRemaining;
        private static bool _autoSelectPending;
        private static string _appliedSelectionKey;
        private static int _modelImporterRearmFramesRemaining;
        private static string _modelImporterRearmSelectionKey;
        private static bool _updateHookRegistered;
        private static readonly HashSet<string> LoggedAppliedSelectionKeys = new();

        private readonly struct AutoSelectRequest
        {
            internal AutoSelectRequest(string previewTypeName, string selectionKey)
            {
                PreviewTypeName = previewTypeName;
                SelectionKey = selectionKey;
            }

            internal string PreviewTypeName { get; }
            internal string SelectionKey { get; }
        }

        static CustomPreviewAutoSelector()
        {
            Selection.selectionChanged -= ScheduleAutoSelect;
            PreviewSettings.SettingsChanged -= ScheduleAutoSelect;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            Selection.selectionChanged += ScheduleAutoSelect;
            PreviewSettings.SettingsChanged += ScheduleAutoSelect;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            ScheduleAutoSelect();
        }

        internal static void ScheduleAutoSelect()
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
            {
                ResetPendingAutoSelectState();
                return;
            }

            if (!TryResolveAutoSelectRequest(Selection.objects, out AutoSelectRequest request))
            {
                ResetPendingAutoSelectState();
                return;
            }

            string selectionKey = request.SelectionKey;
            if (string.Equals(request.PreviewTypeName, ModelImporterPreviewTypeName, StringComparison.Ordinal))
            {
                _modelImporterRearmSelectionKey = selectionKey;
                _modelImporterRearmFramesRemaining = ModelImporterRearmFrames;
            }
            else
            {
                _modelImporterRearmSelectionKey = null;
                _modelImporterRearmFramesRemaining = 0;
            }

            if (!string.IsNullOrEmpty(selectionKey)
                && string.Equals(selectionKey, _appliedSelectionKey, StringComparison.Ordinal)
                && HasSelectedPreviewInOpenPropertyEditors(request.PreviewTypeName))
            {
                LogDiagnostic($"Skip duplicate auto-select for same target: {selectionKey}");
                ResetPendingAutoSelectState();
                return;
            }

            _autoSelectPending = true;
            _framesRemaining = MaxRetryFrames;
            EnsureUpdateHook(shouldSubscribe: true);
        }

        internal static void NotifyModelImporterPreviewCandidate(string modelAssetPath)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
            {
                ResetPendingAutoSelectState();
                return;
            }

            if (!PreviewSettings.ThreeDAssetPreviewActive || string.IsNullOrEmpty(modelAssetPath))
                return;

            string selectionKey = "importer:" + modelAssetPath;
            if (string.Equals(_modelImporterRearmSelectionKey, selectionKey, StringComparison.Ordinal))
            {
                // This callback is hit from HasPreviewGUI; avoid expensive rearm work
                // unless we are already in an active retry window.
                if (!_autoSelectPending && _modelImporterRearmFramesRemaining <= 0)
                    return;
                EnsureUpdateHook(shouldSubscribe: true);
                return;
            }

            _modelImporterRearmSelectionKey = selectionKey;
            _modelImporterRearmFramesRemaining = ModelImporterRearmFrames;
            _autoSelectPending = true;
            _framesRemaining = Math.Max(_framesRemaining, MaxRetryFrames / 2);
            EnsureUpdateHook(shouldSubscribe: true);
        }

        private static void OnEditorUpdate()
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
            {
                ResetPendingAutoSelectState();
                return;
            }

            UnityObject[] propertyEditors = GetOpenPropertyEditors();
            if (!TryResolveAutoSelectRequest(Selection.objects, out AutoSelectRequest request))
            {
                ResetPendingAutoSelectState();
                return;
            }

            bool isModelImporterRequest = string.Equals(request.PreviewTypeName, ModelImporterPreviewTypeName, StringComparison.Ordinal);
            bool keepPassiveModelImporterWatch = isModelImporterRequest;

            bool shouldRearmModelImporter = ShouldRearmModelImporterSelection(request);
            bool modelTabActive = true;
            if (isModelImporterRequest)
            {
                modelTabActive = IsModelImporterModelTabActive(request.SelectionKey, propertyEditors, out bool modelTabResolved);
                if (modelTabResolved && !modelTabActive)
                {
                    _autoSelectPending = false;
                    _framesRemaining = 0;
                    EnsureUpdateHook(shouldSubscribe: keepPassiveModelImporterWatch);
                    return;
                }
            }

            if ((shouldRearmModelImporter || isModelImporterRequest)
                && !HasSelectedPreviewInOpenPropertyEditors(ModelImporterPreviewTypeName, propertyEditors))
            {
                _autoSelectPending = true;
                _framesRemaining = Math.Max(_framesRemaining, MaxRetryFrames / 2);
            }

            bool applied = false;
            if (_autoSelectPending)
                applied = TrySelectInOpenPropertyEditors(request.PreviewTypeName, propertyEditors);
            if (_autoSelectPending)
                _framesRemaining--;

            if (applied || _framesRemaining <= 0)
            {
                if (applied)
                {
                    _appliedSelectionKey = request.SelectionKey;
                    if (!string.IsNullOrEmpty(_appliedSelectionKey) && LoggedAppliedSelectionKeys.Add(_appliedSelectionKey))
                        LogDiagnostic($"Applied preview auto-select for: {_appliedSelectionKey} type={request.PreviewTypeName}");
                }

                _autoSelectPending = false;
                _framesRemaining = 0;
                EnsureUpdateHook(shouldSubscribe: keepPassiveModelImporterWatch || _modelImporterRearmFramesRemaining > 0);
            }
        }

        private static bool ShouldRearmModelImporterSelection(AutoSelectRequest request)
        {
            if (_modelImporterRearmFramesRemaining <= 0)
                return false;

            if (!string.Equals(request.PreviewTypeName, ModelImporterPreviewTypeName, StringComparison.Ordinal))
            {
                _modelImporterRearmFramesRemaining = 0;
                _modelImporterRearmSelectionKey = null;
                return false;
            }

            if (!string.Equals(_modelImporterRearmSelectionKey, request.SelectionKey, StringComparison.Ordinal))
            {
                _modelImporterRearmFramesRemaining = 0;
                _modelImporterRearmSelectionKey = null;
                return false;
            }

            _modelImporterRearmFramesRemaining--;
            return true;
        }

        private static void EnsureUpdateHook(bool shouldSubscribe)
        {
            if (shouldSubscribe)
            {
                PreviewUpdateLoop.EnsureRegistered(ref _updateHookRegistered, OnEditorUpdate);
                return;
            }

            PreviewUpdateLoop.EnsureUnregistered(ref _updateHookRegistered, OnEditorUpdate);
        }

        private static void OnBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            Selection.selectionChanged -= ScheduleAutoSelect;
            PreviewSettings.SettingsChanged -= ScheduleAutoSelect;
            ResetPendingAutoSelectState();
            LoggedAppliedSelectionKeys.Clear();
        }

        private static void ResetPendingAutoSelectState()
        {
            _autoSelectPending = false;
            _framesRemaining = 0;
            _appliedSelectionKey = null;
            _modelImporterRearmFramesRemaining = 0;
            _modelImporterRearmSelectionKey = null;
            EnsureUpdateHook(shouldSubscribe: false);
        }

        // ── Core selection logic ─────────────────────────────────────────────

        private static bool TrySelectInOpenPropertyEditors(string previewTypeName, UnityObject[] propertyEditors = null)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
                return false;

            if (PropertyEditorType == null || PreviewsField == null)
                return false;

            if (SelectPreviewMethod == null && SelectedPreviewField == null)
                return false;

            if (string.IsNullOrEmpty(previewTypeName))
                return false;

            propertyEditors ??= GetOpenPropertyEditors();
            bool applied = false;

            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null || !ShouldApplyToPropertyEditor(propertyEditor, previewTypeName))
                    continue;

                try
                {
                    if (!TryGetInspectedObject(propertyEditor, out UnityObject _))
                        continue;

                    IList previews = PreviewsField.GetValue(propertyEditor) as IList;
                    object ourPreview = FindPreviewByTypeName(previews, previewTypeName);

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

        private static bool HasSelectedPreviewInOpenPropertyEditors(string previewTypeName, UnityObject[] propertyEditors = null)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
                return false;

            if (PropertyEditorType == null || PreviewsField == null || string.IsNullOrEmpty(previewTypeName))
                return false;

            propertyEditors ??= GetOpenPropertyEditors();
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null || !ShouldApplyToPropertyEditor(propertyEditor, previewTypeName))
                    continue;

                try
                {
                    if (!TryGetInspectedObject(propertyEditor, out UnityObject _))
                        continue;

                    IList previews = PreviewsField.GetValue(propertyEditor) as IList;
                    object ourPreview = FindPreviewByTypeName(previews, previewTypeName);
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

        // ── Target Resolution ─────────────────────────────────────────────────

        private static bool TryResolveAutoSelectRequest(UnityObject[] targets, out AutoSelectRequest request)
        {
            if (TryResolvePrefabAutoSelectRequest(targets, out request))
                return true;

            if (TryResolveModelImporterAutoSelectRequest(targets, out request))
                return true;

            request = default;
            return false;
        }

        private static bool TryResolvePrefabAutoSelectRequest(UnityObject[] targets, out AutoSelectRequest request)
        {
            request = default;
            if (!PreviewSettings.AnyPrefabCustomPreviewActive)
                return false;

            if (!PrefabPreviewTargetGate.ShouldSuppressCompetingPreview(targets))
                return false;

            if (!TryGetPrefabAssetPath(targets != null && targets.Length == 1 ? targets[0] : null, out string assetPath))
                return false;

            request = new AutoSelectRequest(PrefabPreviewTypeName, "prefab:" + assetPath);
            return true;
        }

        private static bool TryResolveModelImporterAutoSelectRequest(UnityObject[] targets, out AutoSelectRequest request)
        {
            request = default;
            if (!PreviewSettings.ThreeDAssetPreviewActive)
                return false;

            if (!TryGetModelImporterAssetPath(targets, out string assetPath))
                return false;

            request = new AutoSelectRequest(ModelImporterPreviewTypeName, "importer:" + assetPath);
            return true;
        }

        private static bool TryGetPrefabAssetPath(UnityObject target, out string assetPath)
        {
            assetPath = null;
            GameObject prefabAsset = PrefabPreviewTargetClassifier.ResolvePrefabAsset(target);
            if (!PrefabPreviewTargetGate.IsSupportedTarget(prefabAsset))
                return false;

            assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            return !string.IsNullOrEmpty(assetPath);
        }

        private static bool TryGetModelImporterAssetPath(UnityObject[] targets, out string assetPath)
        {
            assetPath = null;
            if (targets == null || targets.Length != 1 || targets[0] == null)
                return false;

            if (targets[0] is ModelImporter importer)
            {
                assetPath = importer.assetPath;
                return !string.IsNullOrEmpty(assetPath);
            }

            if (targets[0] is AnimationClip)
            {
                return false;
            }

            GameObject gameObject = targets[0] as GameObject;
            if (gameObject == null && targets[0] is Component component)
                gameObject = component.gameObject;
            if (gameObject == null)
                return false;

            if (!EditorUtility.IsPersistent(gameObject) || !PrefabUtility.IsPartOfModelPrefab(gameObject))
                return false;

            assetPath = AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter))
                return false;

            if (IsAnimationVariantAssetPath(assetPath) || gameObject.name.IndexOf('@') >= 0)
                return false;

            GameObject mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
            return ReferenceEquals(mainObject, gameObject);
        }

        private static bool IsAnimationVariantAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            int lastSlash = assetPath.LastIndexOf('/');
            string fileName = lastSlash >= 0 ? assetPath.Substring(lastSlash + 1) : assetPath;
            int dotIndex = fileName.LastIndexOf('.');
            string fileStem = dotIndex > 0 ? fileName.Substring(0, dotIndex) : fileName;
            return fileStem.IndexOf('@') >= 0;
        }

        private static bool IsModelImporterModelTabActive(string selectionKey, UnityObject[] propertyEditors, out bool resolved)
        {
            resolved = false;
            if (!TryGetModelImporterAssetPathFromSelectionKey(selectionKey, out string modelAssetPath))
                return false;

            if (PropertyEditorTrackerField == null || ActiveEditorTrackerActiveEditorsProperty == null)
                return false;

            propertyEditors ??= GetOpenPropertyEditors();
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null)
                    continue;

                ActiveEditorTracker tracker = null;
                try
                {
                    tracker = PropertyEditorTrackerField.GetValue(propertyEditor) as ActiveEditorTracker;
                }
                catch
                {
                    continue;
                }

                if (tracker == null)
                    continue;

                UnityEditor.Editor[] activeEditors = null;
                try
                {
                    activeEditors = ActiveEditorTrackerActiveEditorsProperty.GetValue(tracker, null) as UnityEditor.Editor[];
                }
                catch
                {
                    continue;
                }

                if (activeEditors == null || activeEditors.Length == 0)
                    continue;

                for (int editorIndex = 0; editorIndex < activeEditors.Length; editorIndex++)
                {
                    UnityEditor.Editor activeEditor = activeEditors[editorIndex];
                    if (activeEditor == null || !(activeEditor.target is ModelImporter modelImporter))
                        continue;

                    if (!string.Equals(modelImporter.assetPath, modelAssetPath, StringComparison.Ordinal))
                        continue;

                    resolved = true;
                    return TryGetShowImportedObject(activeEditor, out bool showImportedObject) && showImportedObject;
                }
            }

            return false;
        }

        private static bool TryGetModelImporterAssetPathFromSelectionKey(string selectionKey, out string assetPath)
        {
            assetPath = null;
            if (string.IsNullOrEmpty(selectionKey))
                return false;

            const string prefix = "importer:";
            if (!selectionKey.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            assetPath = selectionKey.Substring(prefix.Length);
            return !string.IsNullOrEmpty(assetPath);
        }

        private static bool TryGetShowImportedObject(UnityEditor.Editor importerEditor, out bool showImportedObject)
        {
            showImportedObject = false;
            if (importerEditor == null || AssetImporterEditorType == null || AssetImporterEditorShowImportedObjectProperty == null)
                return false;

            if (!AssetImporterEditorType.IsInstanceOfType(importerEditor))
                return false;

            try
            {
                object rawValue = AssetImporterEditorShowImportedObjectProperty.GetValue(importerEditor, null);
                if (rawValue is bool value)
                {
                    showImportedObject = value;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool TryGetInspectedObject(UnityObject propertyEditor, out UnityObject inspectedObject)
        {
            inspectedObject = null;
            if (propertyEditor == null)
                return false;

            if (GetInspectedObjectMethod == null)
                return true;

            try
            {
                inspectedObject = GetInspectedObjectMethod.Invoke(propertyEditor, null) as UnityObject;
            }
            catch
            {
                return false;
            }

            return inspectedObject != null;
        }

        private static bool ShouldApplyToPropertyEditor(UnityObject propertyEditor, string previewTypeName)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition()
                || propertyEditor == null
                || string.IsNullOrEmpty(previewTypeName))
                return false;

            if (GetInspectedObjectMethod != null)
            {
                try
                {
                    UnityObject inspected = GetInspectedObjectMethod.Invoke(propertyEditor, null) as UnityObject;
                    if (inspected != null)
                        return IsTargetCompatibleWithPreviewType(inspected, previewTypeName);
                }
                catch
                {
                    return false;
                }

                return false;
            }

            UnityObject selectionTarget = Selection.activeObject;
            return IsTargetCompatibleWithPreviewType(selectionTarget, previewTypeName);
        }

        private static bool IsTargetCompatibleWithPreviewType(UnityObject target, string previewTypeName)
        {
            if (target == null || string.IsNullOrEmpty(previewTypeName))
                return false;

            if (string.Equals(previewTypeName, PrefabPreviewTypeName, StringComparison.Ordinal))
                return PrefabPreviewTargetGate.ShouldSuppressCompetingPreview(new[] { target });

            if (string.Equals(previewTypeName, ModelImporterPreviewTypeName, StringComparison.Ordinal))
                return TryGetModelImporterAssetPath(new[] { target }, out _);

            return false;
        }

        private static object FindPreviewByTypeName(IList previews, string fullTypeName)
        {
            if (previews == null || string.IsNullOrEmpty(fullTypeName))
                return null;

            for (int i = 0; i < previews.Count; i++)
            {
                object preview = previews[i];
                if (preview == null)
                    continue;

                Type type = preview.GetType();
                if (type != null && string.Equals(type.FullName, fullTypeName, StringComparison.Ordinal))
                    return preview;
            }

            return null;
        }

        private static UnityObject[] GetOpenPropertyEditors()
        {
            if (PropertyEditorType == null)
                return Array.Empty<UnityObject>();

            UnityObject[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);
            return propertyEditors ?? Array.Empty<UnityObject>();
        }

        private static void LogDiagnostic(string message)
        {
            if (!PreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(message))
                return;
            Debug.Log("[ParticleThumbnailPreview][AutoSelector] " + message);
        }

        private static Type ResolveEditorTypeAcrossAssemblies(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            for (int i = 0; i < EditorAssemblyPreferenceOrder.Length; i++)
            {
                Type type = Type.GetType(fullTypeName + ", " + EditorAssemblyPreferenceOrder[i], false);
                if (type != null)
                    return type;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullTypeName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static MethodInfo GetInstanceMethod(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;
            return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static FieldInfo GetInstanceField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
