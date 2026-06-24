using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
// Keeps this custom preview selected in inspector hosts when possible, with resilient fallbacks across Unity internal API variations.

namespace NoodleHammer.PreviewForge.Editor
{
    [InitializeOnLoad]
    internal static class CustomPreviewAutoSelector
    {
        private enum PreviewSelectionState
        {
            Unknown,
            Selected,
            NotSelected,
        }

        private enum PreviewReplacementKind
        {
            None,
            UnityBuiltInOrDefault,
            CustomProvider,
            Unknown,
        }

        private const string PrefabPreviewTypeName = "NoodleHammer.PreviewForge.Editor.PrefabPreviewEditor";
        private const string ModelImporterPreviewTypeName = "NoodleHammer.PreviewForge.Editor.ModelImporterPreviewEditor";
        private const int MaxRetryFrames = 3;
        private const int ModelImporterRearmFrames = 2;
        private const int PostApplyVerificationFrames = 20;
        private const int MaxApplyAttemptsPerSelection = 2;
        private const int MaxLateHostActivationAttemptsPerSelection = 3;
        private const double LateHostActivationRetryCooldownSeconds = 0.25d;

        private static readonly Type PropertyEditorType = PreviewForgeEditorCompatibility.ResolveEditorType("UnityEditor.PropertyEditor");

        // Prefer the internal method Unity uses to select previews; this also
        // routes internal input/cursor ownership correctly instead of just
        // flipping the backing field.
        private static readonly MethodInfo SelectPreviewMethod =
            PreviewForgeEditorCompatibility.GetInstanceMethod(PropertyEditorType, "SelectPreview")          // Unity 6+
            ?? PreviewForgeEditorCompatibility.GetInstanceMethod(PropertyEditorType, "SetSelectedPreview")  // older spellings
            ?? PreviewForgeEditorCompatibility.GetInstanceMethod(PropertyEditorType, "OnPreviewSelected");

        private static readonly MethodInfo GetInspectedObjectMethod = PreviewForgeEditorCompatibility.GetInstanceMethod(PropertyEditorType, "GetInspectedObject");
        private static readonly FieldInfo PreviewsField = PreviewForgeEditorCompatibility.GetInstanceField(PropertyEditorType, "m_Previews");
        private static readonly FieldInfo PropertyEditorTrackerField = PreviewForgeEditorCompatibility.GetInstanceField(PropertyEditorType, "m_Tracker");
        // Only used as a last-resort fallback when the internal method is absent.
        private static readonly FieldInfo SelectedPreviewField = PreviewForgeEditorCompatibility.GetInstanceField(PropertyEditorType, "m_SelectedPreview");
        private static readonly PropertyInfo ActiveEditorTrackerActiveEditorsProperty =
            typeof(ActiveEditorTracker).GetProperty("activeEditors", BindingFlags.Instance | BindingFlags.Public);
        private static readonly Type AssetImporterEditorType =
            PreviewForgeEditorCompatibility.ResolveEditorType("UnityEditor.AssetImporters.AssetImporterEditor");
        private static readonly PropertyInfo AssetImporterEditorShowImportedObjectProperty =
            AssetImporterEditorType?.GetProperty(
                "showImportedObject",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static int _framesRemaining;
        private static int _postApplyVerificationFramesRemaining;
        private static int _applyAttemptsForSelection;
        private static bool _autoSelectPending;
        private static bool _recoveryAttemptedForSelection;
        private static string _activeSelectionKey;
        private static string _appliedSelectionKey;
        private static string _lateHostActivationAttemptedKey;
        private static int _lateHostActivationAttemptsForSelection;
        private static double _lastLateHostActivationAttemptTime = -1d;
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

        private readonly struct PreviewSelectionProbe
        {
            internal PreviewSelectionProbe(
                PreviewSelectionState state,
                bool hasPreview,
                PreviewReplacementKind replacementKind,
                string selectedPreviewTypeName)
            {
                State = state;
                HasPreview = hasPreview;
                ReplacementKind = replacementKind;
                SelectedPreviewTypeName = selectedPreviewTypeName;
            }

            internal PreviewSelectionState State { get; }
            internal bool HasPreview { get; }
            internal PreviewReplacementKind ReplacementKind { get; }
            internal string SelectedPreviewTypeName { get; }

            internal bool CanRecoverUnityBuiltInReplacement =>
                State == PreviewSelectionState.NotSelected
                && HasPreview
                && ReplacementKind == PreviewReplacementKind.UnityBuiltInOrDefault;

            internal static PreviewSelectionProbe Unknown =>
                new(PreviewSelectionState.Unknown, hasPreview: false, PreviewReplacementKind.Unknown, selectedPreviewTypeName: null);
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
                ClearParticleSessionStateIfSelectionChanged(null);
                ResetPendingAutoSelectState();
                return;
            }

            string selectionKey = request.SelectionKey;
            TrackSelectionKey(selectionKey);
            bool isPrefabPreviewRequest = string.Equals(request.PreviewTypeName, PrefabPreviewTypeName, StringComparison.Ordinal);
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

            if (isPrefabPreviewRequest
                && _applyAttemptsForSelection > 0
                && string.Equals(selectionKey, _appliedSelectionKey, StringComparison.Ordinal))
            {
                LogDiagnostic($"Preview Forge ignored duplicate prefab preview auto-select for: {selectionKey}");
                _autoSelectPending = false;
                _framesRemaining = 0;
                EnsureUpdateHook(shouldSubscribe:
                    _postApplyVerificationFramesRemaining > 0
                    || _modelImporterRearmFramesRemaining > 0);
                return;
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
            if (!PreviewSettings.ThreeDAssetPreviewActive || string.IsNullOrEmpty(modelAssetPath))
                return;

            NotifyPreviewHostAvailable(ModelImporterPreviewTypeName, "importer:" + modelAssetPath);
        }

        internal static void NotifyPrefabPreviewHostAvailable(string prefabAssetPath)
        {
            if (!PreviewSettings.AnyPrefabCustomPreviewActive || string.IsNullOrEmpty(prefabAssetPath))
                return;

            NotifyPreviewHostAvailable(PrefabPreviewTypeName, "prefab:" + prefabAssetPath);
        }

        internal static void NotifyPreviewHostAvailable(string previewTypeName, string selectionKey)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
            {
                LogDiagnostic($"host-available reset unsafe-transition type={ShortPreviewTypeName(previewTypeName)} key='{selectionKey}'");
                ResetPendingAutoSelectState();
                return;
            }

            if (string.IsNullOrEmpty(previewTypeName) || string.IsNullOrEmpty(selectionKey))
            {
                LogDiagnostic($"host-available ignored invalid type={ShortPreviewTypeName(previewTypeName)} key='{selectionKey}'");
                return;
            }

            if (!TryResolveAutoSelectRequest(Selection.objects, out AutoSelectRequest request))
            {
                LogDiagnostic($"host-available ignored unresolved-selection type={ShortPreviewTypeName(previewTypeName)} key='{selectionKey}' selectionCount={Selection.objects?.Length ?? 0}");
                return;
            }

            if (!string.Equals(request.PreviewTypeName, previewTypeName, StringComparison.Ordinal)
                || !string.Equals(request.SelectionKey, selectionKey, StringComparison.Ordinal))
            {
                LogDiagnostic($"host-available ignored selection-mismatch requestedType={ShortPreviewTypeName(previewTypeName)} requestedKey='{selectionKey}' resolvedType={ShortPreviewTypeName(request.PreviewTypeName)} resolvedKey='{request.SelectionKey}'");
                return;
            }

            TrackSelectionKey(selectionKey);

            string lateActivationKey = previewTypeName + "|" + selectionKey;
            bool isHandledLateActivation = string.Equals(_lateHostActivationAttemptedKey, lateActivationKey, StringComparison.Ordinal);
            PreviewSelectionProbe handledProbe = PreviewSelectionProbe.Unknown;
            if (isHandledLateActivation
                && !ShouldRetryHandledLateHostActivation(previewTypeName, selectionKey, out handledProbe))
            {
                LogDiagnostic($"host-available ignored already-handled type={ShortPreviewTypeName(previewTypeName)} key='{selectionKey}' verify={_postApplyVerificationFramesRemaining} pending={_autoSelectPending} attempts={_applyAttemptsForSelection} lateAttempts={_lateHostActivationAttemptsForSelection} {DescribeProbe(handledProbe)}");
                return;
            }

            if (isHandledLateActivation)
                LogDiagnostic($"host-available retrying delayed unity-default replacement type={ShortPreviewTypeName(previewTypeName)} key='{selectionKey}' lateAttempts={_lateHostActivationAttemptsForSelection} {DescribeProbe(handledProbe)}");

            PreviewPropertyEditorCache.Invalidate();
            UnityObject[] propertyEditors = GetOpenPropertyEditors();
            PreviewSelectionProbe selectionProbe = GetPreviewSelectionProbeInOpenPropertyEditors(previewTypeName, propertyEditors);
            LogDiagnostic($"host-available probe type={ShortPreviewTypeName(previewTypeName)} key='{selectionKey}' editors={propertyEditors.Length} {DescribeProbe(selectionProbe)}");
            if (selectionProbe.State == PreviewSelectionState.Selected)
            {
                BeginLateHostActivationWindow(lateActivationKey);
                _autoSelectPending = false;
                _framesRemaining = 0;
                _postApplyVerificationFramesRemaining = Math.Max(_postApplyVerificationFramesRemaining, PostApplyVerificationFrames);
                LogDiagnostic($"Preview Forge is watching late preview host activation for: {selectionKey} type={previewTypeName}");
                EnsureUpdateHook(shouldSubscribe: true);
                return;
            }

            if (selectionProbe.State == PreviewSelectionState.NotSelected
                && selectionProbe.ReplacementKind == PreviewReplacementKind.CustomProvider)
            {
                _lateHostActivationAttemptedKey = lateActivationKey;
                LogDiagnostic($"Preview Forge yielded late preview selection because another preview provider is active: {selectionKey} selected={selectionProbe.SelectedPreviewTypeName}");
                return;
            }

            BeginLateHostActivationWindow(lateActivationKey);
            if (string.Equals(previewTypeName, ModelImporterPreviewTypeName, StringComparison.Ordinal))
            {
                _modelImporterRearmSelectionKey = selectionKey;
                _modelImporterRearmFramesRemaining = Math.Max(_modelImporterRearmFramesRemaining, ModelImporterRearmFrames);
            }

            _autoSelectPending = true;
            _framesRemaining = Math.Max(_framesRemaining, MaxRetryFrames);
            LogDiagnostic($"Preview Forge scheduled late preview selection for: {selectionKey} type={previewTypeName}");
            EnsureUpdateHook(shouldSubscribe: true);
        }

        private static void BeginLateHostActivationWindow(string lateActivationKey)
        {
            _lateHostActivationAttemptedKey = lateActivationKey;
            _lateHostActivationAttemptsForSelection++;
            _lastLateHostActivationAttemptTime = EditorApplication.timeSinceStartup;
            _applyAttemptsForSelection = 0;
            _recoveryAttemptedForSelection = false;
        }

        private static bool ShouldRetryHandledLateHostActivation(
            string previewTypeName,
            string selectionKey,
            out PreviewSelectionProbe selectionProbe)
        {
            selectionProbe = PreviewSelectionProbe.Unknown;

            if (_autoSelectPending || _postApplyVerificationFramesRemaining > 0)
                return false;

            if (_lateHostActivationAttemptsForSelection >= MaxLateHostActivationAttemptsPerSelection)
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (_lastLateHostActivationAttemptTime >= 0d
                && now - _lastLateHostActivationAttemptTime < LateHostActivationRetryCooldownSeconds)
                return false;

            PreviewPropertyEditorCache.Invalidate();
            selectionProbe = GetPreviewSelectionProbeInOpenPropertyEditors(previewTypeName, GetOpenPropertyEditors());
            if (selectionProbe.State == PreviewSelectionState.NotSelected
                && selectionProbe.ReplacementKind == PreviewReplacementKind.UnityBuiltInOrDefault)
            {
                return true;
            }

            if (selectionProbe.State == PreviewSelectionState.NotSelected
                && selectionProbe.ReplacementKind == PreviewReplacementKind.CustomProvider)
            {
                LogDiagnostic($"Preview Forge yielded delayed late preview retry because another preview provider is active: {selectionKey} selected={selectionProbe.SelectedPreviewTypeName}");
            }

            return false;
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
            TrackSelectionKey(request.SelectionKey);

            bool isModelImporterRequest = string.Equals(request.PreviewTypeName, ModelImporterPreviewTypeName, StringComparison.Ordinal);
            bool isPrefabPreviewRequest = string.Equals(request.PreviewTypeName, PrefabPreviewTypeName, StringComparison.Ordinal);

            bool shouldRearmModelImporter = ShouldRearmModelImporterSelection(request);
            PreviewSelectionProbe previewSelectionProbe = GetPreviewSelectionProbeInOpenPropertyEditors(request.PreviewTypeName, propertyEditors);
            PreviewSelectionState previewSelectionState = previewSelectionProbe.State;
            if (_autoSelectPending || _postApplyVerificationFramesRemaining > 0 || PreviewSettings.EnableDiagnostics)
            {
                LogDiagnostic(
                    $"update type={ShortPreviewTypeName(request.PreviewTypeName)} key='{request.SelectionKey}' editors={propertyEditors.Length} {DescribeProbe(previewSelectionProbe)} pending={_autoSelectPending} frames={_framesRemaining} verify={_postApplyVerificationFramesRemaining} attempts={_applyAttemptsForSelection} recovery={_recoveryAttemptedForSelection} late='{_lateHostActivationAttemptedKey}' modelRearm={_modelImporterRearmFramesRemaining}");
            }

            if (_postApplyVerificationFramesRemaining > 0
                && previewSelectionState == PreviewSelectionState.NotSelected
                && _applyAttemptsForSelection < MaxApplyAttemptsPerSelection)
            {
                if (isPrefabPreviewRequest)
                {
                    if (previewSelectionProbe.CanRecoverUnityBuiltInReplacement && !_recoveryAttemptedForSelection)
                    {
                        _recoveryAttemptedForSelection = true;
                        LogDiagnostic($"Preview Forge recovered the prefab preview after Unity selected its built-in/default preview: {request.SelectionKey}");
                        _autoSelectPending = true;
                        _framesRemaining = Math.Max(_framesRemaining, 1);
                    }
                    else
                    {
                        string selectedPreviewType = string.IsNullOrEmpty(previewSelectionProbe.SelectedPreviewTypeName)
                            ? "unknown preview"
                            : previewSelectionProbe.SelectedPreviewTypeName;
                        LogDiagnostic($"Preview Forge yielded because another preview provider replaced the prefab preview: {request.SelectionKey} selected={selectedPreviewType}");
                        _postApplyVerificationFramesRemaining = 0;
                        _autoSelectPending = false;
                        _framesRemaining = 0;
                    }
                }
                else
                {
                    _autoSelectPending = true;
                    _framesRemaining = Math.Max(_framesRemaining, 1);
                }
            }

            bool modelTabActive = true;
            if (isModelImporterRequest)
            {
                modelTabActive = IsModelImporterModelTabActive(request.SelectionKey, propertyEditors, out bool modelTabResolved);
                if (modelTabResolved && !modelTabActive)
                {
                    _autoSelectPending = false;
                    _framesRemaining = 0;
                    EnsureUpdateHook(shouldSubscribe: _modelImporterRearmFramesRemaining > 0);
                    return;
                }
            }

            if ((shouldRearmModelImporter || isModelImporterRequest)
                && previewSelectionState == PreviewSelectionState.NotSelected)
            {
                _autoSelectPending = true;
                _framesRemaining = Math.Max(_framesRemaining, MaxRetryFrames / 2);
            }

            bool applied = false;
            if (_autoSelectPending && _applyAttemptsForSelection < MaxApplyAttemptsPerSelection)
                applied = TrySelectInOpenPropertyEditors(request.PreviewTypeName, propertyEditors);
            if (_autoSelectPending)
                _framesRemaining--;

            if (applied)
            {
                CompleteAppliedSelection(request, isPrefabPreviewRequest, reason: "update");
                return;
            }

            if (_postApplyVerificationFramesRemaining > 0)
                _postApplyVerificationFramesRemaining--;

            if (_framesRemaining <= 0 || _applyAttemptsForSelection >= MaxApplyAttemptsPerSelection)
            {
                if (_autoSelectPending)
                    LogDiagnostic($"stop pending type={ShortPreviewTypeName(request.PreviewTypeName)} key='{request.SelectionKey}' frames={_framesRemaining} attempts={_applyAttemptsForSelection} state={previewSelectionState}");
                _autoSelectPending = false;
                _framesRemaining = 0;
            }

            EnsureUpdateHook(shouldSubscribe:
                _autoSelectPending
                || _postApplyVerificationFramesRemaining > 0
                || _modelImporterRearmFramesRemaining > 0);
        }

        private static void CompleteAppliedSelection(AutoSelectRequest request, bool isPrefabPreviewRequest, string reason)
        {
            _appliedSelectionKey = request.SelectionKey;
            _applyAttemptsForSelection++;
            _postApplyVerificationFramesRemaining = PostApplyVerificationFrames;
            if (!string.IsNullOrEmpty(_appliedSelectionKey) && LoggedAppliedSelectionKeys.Add(_appliedSelectionKey))
                LogDiagnostic($"Applied preview auto-select for: {_appliedSelectionKey} type={request.PreviewTypeName} reason={reason}");

            _autoSelectPending = false;
            _framesRemaining = 0;
            EnsureUpdateHook(shouldSubscribe:
                _postApplyVerificationFramesRemaining > 0
                || _modelImporterRearmFramesRemaining > 0);
        }

        private static void TrackSelectionKey(string selectionKey)
        {
            if (string.Equals(_activeSelectionKey, selectionKey, StringComparison.Ordinal))
                return;

            ClearParticleSessionStateIfSelectionChanged(selectionKey);
            _activeSelectionKey = selectionKey;
            _applyAttemptsForSelection = 0;
            _recoveryAttemptedForSelection = false;
            _lateHostActivationAttemptedKey = null;
            _lateHostActivationAttemptsForSelection = 0;
            _lastLateHostActivationAttemptTime = -1d;
            _postApplyVerificationFramesRemaining = 0;
        }

        private static void ClearParticleSessionStateIfSelectionChanged(string nextSelectionKey)
        {
            if (string.Equals(_activeSelectionKey, nextSelectionKey, StringComparison.Ordinal))
                return;

            ParticlePrefabPreviewSession.ClearSessionStateCache();
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
            _postApplyVerificationFramesRemaining = 0;
            _applyAttemptsForSelection = 0;
            _recoveryAttemptedForSelection = false;
            _lateHostActivationAttemptedKey = null;
            _lateHostActivationAttemptsForSelection = 0;
            _lastLateHostActivationAttemptTime = -1d;
            _activeSelectionKey = null;
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
            int inspectedEditors = 0;
            int compatibleEditors = 0;
            int foundPreviewCount = 0;

            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null || !ShouldApplyToPropertyEditor(propertyEditor, previewTypeName))
                    continue;

                compatibleEditors++;
                try
                {
                    if (!TryGetInspectedObject(propertyEditor, out UnityObject _))
                        continue;

                    inspectedEditors++;
                    IList previews = PreviewsField.GetValue(propertyEditor) as IList;
                    object ourPreview = FindPreviewByTypeName(previews, previewTypeName);

                    if (ourPreview == null)
                        continue;

                    foundPreviewCount++;
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

            LogDiagnostic($"select-attempt type={ShortPreviewTypeName(previewTypeName)} editors={propertyEditors.Length} compatible={compatibleEditors} inspected={inspectedEditors} foundPreview={foundPreviewCount} applied={applied}");
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
            return GetPreviewSelectionProbeInOpenPropertyEditors(previewTypeName, propertyEditors).State == PreviewSelectionState.Selected;
        }

        private static PreviewSelectionProbe GetPreviewSelectionProbeInOpenPropertyEditors(string previewTypeName, UnityObject[] propertyEditors = null)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
                return PreviewSelectionProbe.Unknown;

            if (PropertyEditorType == null || PreviewsField == null || string.IsNullOrEmpty(previewTypeName))
                return PreviewSelectionProbe.Unknown;

            propertyEditors ??= GetOpenPropertyEditors();
            bool foundSupportedPropertyEditor = false;
            bool foundPreview = false;
            PreviewSelectionProbe replacementProbe = PreviewSelectionProbe.Unknown;
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                UnityObject propertyEditor = propertyEditors[i];
                if (propertyEditor == null || !ShouldApplyToPropertyEditor(propertyEditor, previewTypeName))
                    continue;

                try
                {
                    if (!TryGetInspectedObject(propertyEditor, out UnityObject _))
                        continue;

                    foundSupportedPropertyEditor = true;
                    IList previews = PreviewsField.GetValue(propertyEditor) as IList;
                    object ourPreview = FindPreviewByTypeName(previews, previewTypeName);
                    if (ourPreview == null)
                        continue;

                    foundPreview = true;
                    PreviewSelectionProbe selectionProbe = GetPreviewSelectionProbe(propertyEditor, ourPreview);
                    PreviewSelectionState selectionState = selectionProbe.State;
                    if (selectionState == PreviewSelectionState.Selected)
                        return selectionProbe;

                    if (selectionState == PreviewSelectionState.Unknown)
                        return PreviewSelectionProbe.Unknown;

                    if (selectionProbe.ReplacementKind == PreviewReplacementKind.CustomProvider)
                        return selectionProbe;

                    replacementProbe = selectionProbe;
                }
                catch
                {
                    // Stay resilient to Unity internals changing.
                }
            }

            if (!foundSupportedPropertyEditor || !foundPreview)
                return PreviewSelectionProbe.Unknown;

            return replacementProbe.State == PreviewSelectionState.NotSelected
                ? replacementProbe
                : new PreviewSelectionProbe(PreviewSelectionState.NotSelected, hasPreview: true, PreviewReplacementKind.Unknown, selectedPreviewTypeName: null);
        }

        private static bool IsPreviewAlreadySelected(UnityObject propertyEditor, object preview)
        {
            return GetPreviewSelectionProbe(propertyEditor, preview).State == PreviewSelectionState.Selected;
        }

        private static PreviewSelectionProbe GetPreviewSelectionProbe(UnityObject propertyEditor, object preview)
        {
            if (propertyEditor == null || preview == null || SelectedPreviewField == null)
                return PreviewSelectionProbe.Unknown;

            try
            {
                object current = SelectedPreviewField.GetValue(propertyEditor);
                if (ReferenceEquals(current, preview))
                    return new PreviewSelectionProbe(PreviewSelectionState.Selected, hasPreview: true, PreviewReplacementKind.None, preview.GetType().FullName);

                Type currentType = current?.GetType();
                Type previewType = preview.GetType();
                bool typeMatches = currentType != null
                    && previewType != null
                    && string.Equals(currentType.FullName, previewType.FullName, StringComparison.Ordinal);

                if (typeMatches)
                    return new PreviewSelectionProbe(PreviewSelectionState.Selected, hasPreview: true, PreviewReplacementKind.None, previewType.FullName);

                PreviewReplacementKind replacementKind = ClassifyReplacementPreview(currentType);
                return new PreviewSelectionProbe(PreviewSelectionState.NotSelected, hasPreview: true, replacementKind, currentType?.FullName);
            }
            catch
            {
                return PreviewSelectionProbe.Unknown;
            }
        }

        private static PreviewReplacementKind ClassifyReplacementPreview(Type selectedPreviewType)
        {
            if (selectedPreviewType == null)
                return PreviewReplacementKind.UnityBuiltInOrDefault;

            string assemblyName = selectedPreviewType.Assembly.GetName().Name ?? string.Empty;
            string typeName = selectedPreviewType.FullName ?? selectedPreviewType.Name;
            if (assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal)
                || typeName.StartsWith("UnityEditor.", StringComparison.Ordinal)
                || typeName.StartsWith("UnityEditorInternal.", StringComparison.Ordinal))
                return PreviewReplacementKind.UnityBuiltInOrDefault;

            return PreviewReplacementKind.CustomProvider;
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

            return PreviewPropertyEditorCache.GetOpenPropertyEditors(PropertyEditorType);
        }

        private static void LogDiagnostic(string message)
        {
            if (!PreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(message))
                return;

            PreviewDiagnostics.Log("AutoSelector", message);
        }

        private static string DescribeProbe(PreviewSelectionProbe probe)
        {
            string selectedPreview = string.IsNullOrEmpty(probe.SelectedPreviewTypeName)
                ? "<none>"
                : probe.SelectedPreviewTypeName;
            return $"state={probe.State} hasPreview={probe.HasPreview} replacement={probe.ReplacementKind} selected='{selectedPreview}'";
        }

        private static string ShortPreviewTypeName(string previewTypeName)
        {
            if (string.IsNullOrEmpty(previewTypeName))
                return "<null>";

            int dotIndex = previewTypeName.LastIndexOf('.');
            return dotIndex >= 0 && dotIndex + 1 < previewTypeName.Length
                ? previewTypeName.Substring(dotIndex + 1)
                : previewTypeName;
        }

    }
}
