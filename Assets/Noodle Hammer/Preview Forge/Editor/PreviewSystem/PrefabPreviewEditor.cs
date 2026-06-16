using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
// Serves as the Unity custom preview entry point and routes each supported prefab target to the correct preview implementation.

namespace NoodleHammer.PreviewForge.Editor
{
    [CustomPreview(typeof(GameObject))]
    [CanEditMultipleObjects]
    public sealed class PrefabPreviewEditor : ObjectPreview
    {
        private static readonly System.Reflection.MethodInfo ObjectPreviewRepaintMethod =
            typeof(ObjectPreview).GetMethod(
                "Repaint",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static readonly Type PropertyEditorType = PreviewForgeEditorCompatibility.ResolveEditorType("UnityEditor.PropertyEditor");
        private static readonly System.Reflection.MethodInfo PropertyEditorGetInspectedObjectMethod =
            PreviewForgeEditorCompatibility.GetInstanceMethod(PropertyEditorType, "GetInspectedObject");

        private readonly Dictionary<PrefabPreviewTargetKind, IPrefabPreviewImplementation> _implementations = new();
        private IPrefabPreviewImplementation _activeImplementation;
        private PrefabPreviewTargetKind _activeKind = PrefabPreviewTargetKind.Unsupported;
        private string _activePrefabAssetPath;
        private double _lastSuccessfulResolveTime = -1d;
        private readonly int _hostId = GetNextHostId();
        private bool _eventHandlersRegistered;
        private bool _targetSupportCacheDirty = true;
        private bool _cachedSelectionSupported;
        private GameObject _cachedSelectionPrefab;
        private PrefabPreviewTargetKind _cachedSelectionKind = PrefabPreviewTargetKind.Unsupported;
        private Object _cachedOwningPropertyEditor;
        private int _cachedSelectionCount = -1;
        private int _cachedSelectionActiveInstanceId;
        private string _lastResolveLogKey;
        private double _lastResolveLogTime = -1d;

        private const double TransientTargetLossGraceSeconds = 1.0d;
        private const double ResolveLogRepeatSeconds = 1.0d;
        private static int s_nextHostId;

        public override void Initialize(Object[] targets)
        {
            base.Initialize(targets);
            RegisterEventHandlers();
            MarkTargetSupportCacheDirty();
            PreviewParticleTrace.Log("PrefabHost", $"host=#{_hostId} Initialize targets={targets?.Length ?? 0} target='{DescribeTargetForTrace(target)}'");
            PreviewDiagnostics.Log("Host", $"#{_hostId} Initialize targets={targets?.Length ?? 0}");
        }

        public override void Cleanup()
        {
            PreviewParticleTrace.Log("PrefabHost", $"host=#{_hostId} Cleanup activeKind={_activeKind} asset='{_activePrefabAssetPath}' target='{DescribeTargetForTrace(target)}'");
            CleanupActiveImplementation();
            _activeImplementation = null;
            _activeKind = PrefabPreviewTargetKind.Unsupported;
            _activePrefabAssetPath = null;
            _lastSuccessfulResolveTime = -1d;
            _lastResolveLogKey = null;
            _lastResolveLogTime = -1d;
            UnregisterEventHandlers();
            ClearTargetSupportCache();
            PreviewDiagnostics.Log("Host", $"#{_hostId} Cleanup");
            base.Cleanup();
        }

        public override bool HasPreviewGUI()
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
            {
                CleanupActiveImplementation(clearSessionCache: true);
                LogResolveState("unsafe-transition");
                return false;
            }

            if (!PreviewSettings.AnyPrefabCustomPreviewActive)
            {
                CleanupActiveImplementation();
                LogResolveState("settings-inactive");
                return false;
            }

            bool hasMultiSelection = Selection.count > 1;
            bool hasMultiTargets = m_Targets != null && m_Targets.Length > 1;
            if (hasMultiSelection || hasMultiTargets)
            {
                CleanupActiveImplementation();
                LogResolveState($"multi-selection selection={Selection.count} targets={m_Targets?.Length ?? 0}");
                return false;
            }

            if (!TryResolveSupportedPrefabTarget(out GameObject prefab, out PrefabPreviewTargetKind kind))
            {
                CleanupActiveImplementation();
                LogResolveState($"unresolved m_Targets={m_Targets?.Length ?? 0} selection={Selection.objects?.Length ?? 0}");
                return false;
            }

            IPrefabPreviewImplementation implementation = ResolveImplementation(kind);
            if (implementation == null)
            {
                CleanupActiveImplementation();
                LogResolveState($"implementation-disabled kind={kind}");
                return false;
            }

            if (_activeKind != kind || !ReferenceEquals(_activeImplementation, implementation))
            {
                if (kind == PrefabPreviewTargetKind.Particle || _activeKind == PrefabPreviewTargetKind.Particle)
                    PreviewParticleTrace.Log("PrefabHost", $"host=#{_hostId} switch {_activeKind}->{kind} prefab='{PreviewParticleTrace.Asset(prefab)}'");
                LogResolveState($"switch {_activeKind} -> {kind} prefab='{prefab.name}'", force: true);
                CleanupActiveImplementation();
                _activeImplementation = implementation;
                _activeKind = kind;
            }

            _activePrefabAssetPath = AssetDatabase.GetAssetPath(prefab);
            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            if (_activeImplementation == null)
                return GUIContent.none;

            GameObject prefab = target as GameObject;
            return _activeImplementation.GetPreviewTitle(prefab);
        }

        public override string GetInfoString()
        {
            return string.Empty;
        }

        public override void OnPreviewSettings()
        {
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
                PreviewSettingsProvider.OpenSettings();
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            DrawPreviewInternal(rect, background, isInteractive: false);
        }

        public override void OnInteractivePreviewGUI(Rect rect, GUIStyle background)
        {
            DrawPreviewInternal(rect, background, isInteractive: true);
        }

        private void DrawPreviewInternal(Rect rect, GUIStyle background, bool isInteractive)
        {
            if (!HasPreviewGUI() || _activeImplementation == null)
            {
                if (isInteractive)
                    base.OnInteractivePreviewGUI(rect, background);
                else
                    base.OnPreviewGUI(rect, background);
                return;
            }

            if (!TryResolveSupportedPrefabTarget(out GameObject prefab, out PrefabPreviewTargetKind kind))
            {
                if (isInteractive)
                    base.OnInteractivePreviewGUI(rect, background);
                else
                    base.OnPreviewGUI(rect, background);
                return;
            }

            if (kind != _activeKind)
            {
                if (isInteractive)
                    base.OnInteractivePreviewGUI(rect, background);
                else
                    base.OnPreviewGUI(rect, background);
                return;
            }

            bool ready = _activeImplementation.EnsureReady(prefab);
            if (!ready)
            {
                if (isInteractive)
                    base.OnInteractivePreviewGUI(rect, background);
                else
                    base.OnPreviewGUI(rect, background);
                return;
            }

            _activePrefabAssetPath = AssetDatabase.GetAssetPath(prefab);
            _lastSuccessfulResolveTime = EditorApplication.timeSinceStartup;
            _activeImplementation.Draw(rect, background, isInteractive);
        }

        private void EnsureImplementationInstance(PrefabPreviewTargetKind kind)
        {
            if (kind == PrefabPreviewTargetKind.Unsupported || _implementations.ContainsKey(kind))
                return;

            IPrefabPreviewImplementation implementation = kind switch
            {
                PrefabPreviewTargetKind.Particle => new ParticlePreviewParticleImplementation(),
                PrefabPreviewTargetKind.Model => new ModelPrefabPreviewImplementation(),
                PrefabPreviewTargetKind.Sprite => new SpritePrefabPreviewImplementation(),
                _ => null,
            };

            if (implementation == null)
                return;

            implementation.SetRepaintCallback(RequestPreviewRepaint);
            _implementations[kind] = implementation;
            if (kind == PrefabPreviewTargetKind.Particle)
                PreviewParticleTrace.Log("PrefabHost", $"host=#{_hostId} create-implementation kind={kind}");
        }

        private IPrefabPreviewImplementation ResolveImplementation(PrefabPreviewTargetKind kind)
        {
            EnsureImplementationInstance(kind);
            if (!_implementations.TryGetValue(kind, out IPrefabPreviewImplementation implementation))
                return null;

            if (kind == PrefabPreviewTargetKind.Model && !PreviewSettings.ModelPreviewActive)
                return null;

            if (kind == PrefabPreviewTargetKind.Sprite && !PreviewSettings.SpritePrefabPreviewActive)
                return null;

            if (kind == PrefabPreviewTargetKind.Particle && !PreviewSettings.ParticlePrefabPreviewActive)
                return null;

            return implementation;
        }

        private bool TryResolveSupportedPrefabTarget(out GameObject prefab, out PrefabPreviewTargetKind kind)
        {
            prefab = null;
            kind = PrefabPreviewTargetKind.Unsupported;

            if (TryGetCachedSupportedSelectionTarget(out prefab, out kind))
                return true;

            return TryResolveFromTransientLossFallback(out prefab, out kind);
        }

        private bool TryGetCachedSupportedSelectionTarget(out GameObject prefab, out PrefabPreviewTargetKind kind)
        {
            prefab = null;
            kind = PrefabPreviewTargetKind.Unsupported;

            int currentSelectionCount = Selection.count;
            Object activeSelectionObject = Selection.activeObject;
            int currentSelectionActiveInstanceId = activeSelectionObject != null ? activeSelectionObject.GetInstanceID() : 0;
            bool selectionSignatureChanged = currentSelectionCount != _cachedSelectionCount
                                             || currentSelectionActiveInstanceId != _cachedSelectionActiveInstanceId;

            if (_targetSupportCacheDirty || selectionSignatureChanged)
                RebuildSelectionSupportCache(currentSelectionCount, currentSelectionActiveInstanceId);

            if (!_cachedSelectionSupported || _cachedSelectionPrefab == null || _cachedSelectionKind == PrefabPreviewTargetKind.Unsupported)
                return false;

            prefab = _cachedSelectionPrefab;
            kind = _cachedSelectionKind;
            return true;
        }

        private void RebuildSelectionSupportCache(int currentSelectionCount, int currentSelectionActiveInstanceId)
        {
            _cachedSelectionCount = currentSelectionCount;
            _cachedSelectionActiveInstanceId = currentSelectionActiveInstanceId;
            _targetSupportCacheDirty = false;
            _cachedSelectionSupported = false;
            _cachedSelectionPrefab = null;
            _cachedSelectionKind = PrefabPreviewTargetKind.Unsupported;

            if (!TryGetSingleSelectedPersistentPrefabAsset(out GameObject selectedPrefab))
                return;

            PrefabPreviewTargetKind selectedKind = PrefabPreviewTargetClassifier.Classify(selectedPrefab);
            if (selectedKind == PrefabPreviewTargetKind.Unsupported)
                return;

            _cachedSelectionSupported = true;
            _cachedSelectionPrefab = selectedPrefab;
            _cachedSelectionKind = selectedKind;
        }

        private static bool TryGetSingleSelectedPersistentPrefabAsset(out GameObject prefab)
        {
            prefab = null;
            if (Selection.count != 1)
                return false;

            Object selectedObject = Selection.activeObject;
            if (selectedObject == null)
                return false;

            GameObject selectedGameObject = selectedObject as GameObject;
            if (selectedGameObject == null)
                return false;

            if (!EditorUtility.IsPersistent(selectedGameObject) || !PrefabUtility.IsPartOfPrefabAsset(selectedGameObject))
                return false;

            prefab = selectedGameObject;
            return true;
        }

        private void CleanupActiveImplementation()
        {
            CleanupActiveImplementation(clearSessionCache: false);
        }

        private void CleanupActiveImplementation(bool clearSessionCache)
        {
            if (_activeImplementation == null)
                return;

            bool selectionIsEmpty = clearSessionCache;
            if (_activeKind == PrefabPreviewTargetKind.Particle)
            {
                PreviewParticleTrace.Log(
                    "PrefabHost",
                    $"host=#{_hostId} cleanup-active kind={_activeKind} selectionEmpty={IsSelectionEmpty()} clearSessionCache={clearSessionCache} clearSessionState={selectionIsEmpty} asset='{_activePrefabAssetPath}'");
            }
            _activeImplementation.Cleanup(selectionIsEmpty);
            PreviewDiagnostics.Log(
                "Host",
                $"#{_hostId} cleanup-active kind={_activeKind} selectionEmpty={selectionIsEmpty} clearSessionCache={clearSessionCache} asset='{_activePrefabAssetPath}'");
            _activeImplementation = null;
            _activeKind = PrefabPreviewTargetKind.Unsupported;
            _activePrefabAssetPath = null;
            _lastSuccessfulResolveTime = -1d;
        }

        private static bool IsSelectionEmpty()
        {
            Object[] selection = Selection.objects;
            return selection == null || selection.Length == 0;
        }

        private static string DescribeTargetForTrace(Object obj)
        {
            if (obj == null)
                return "<null>";

            string path = AssetDatabase.GetAssetPath(obj);
            return string.IsNullOrEmpty(path) ? $"{obj.GetType().Name}:{obj.name}" : path;
        }

        private static bool TryResolveFromTargets(Object[] targets, out GameObject prefab, out PrefabPreviewTargetKind kind)
        {
            prefab = null;
            kind = PrefabPreviewTargetKind.Unsupported;

            if (targets == null || targets.Length != 1)
                return false;

            prefab = PrefabPreviewTargetClassifier.ResolvePrefabAsset(targets[0]);
            if (prefab == null)
                return false;

            kind = PrefabPreviewTargetClassifier.Classify(prefab);
            if (kind == PrefabPreviewTargetKind.Unsupported)
            {
                prefab = null;
                return false;
            }

            return true;
        }

        private bool TryResolveFromTransientLossFallback(out GameObject prefab, out PrefabPreviewTargetKind kind)
        {
            prefab = null;
            kind = PrefabPreviewTargetKind.Unsupported;

            if (_activeImplementation == null || _activeKind == PrefabPreviewTargetKind.Unsupported)
                return false;

            if (string.IsNullOrEmpty(_activePrefabAssetPath))
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (_lastSuccessfulResolveTime < 0d || now - _lastSuccessfulResolveTime > TransientTargetLossGraceSeconds)
                return false;

            if (TryResolveFromTargets(Selection.objects, out GameObject selectedPrefab, out PrefabPreviewTargetKind selectedKind))
            {
                string selectedPath = AssetDatabase.GetAssetPath(selectedPrefab);
                if (!string.Equals(selectedPath, _activePrefabAssetPath, StringComparison.Ordinal)
                    || selectedKind != _activeKind)
                {
                    return false;
                }
            }

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_activePrefabAssetPath);
            if (prefab == null)
                return false;

            kind = PrefabPreviewTargetClassifier.Classify(prefab);
            if (kind != _activeKind || kind == PrefabPreviewTargetKind.Unsupported)
            {
                prefab = null;
                kind = PrefabPreviewTargetKind.Unsupported;
                return false;
            }

            PreviewDiagnostics.Log(
                "Resolve",
                $"#{_hostId} transient-fallback kind={kind} asset='{_activePrefabAssetPath}' elapsed={(now - _lastSuccessfulResolveTime):F3}s");
            return true;
        }

        private static int GetNextHostId()
        {
            s_nextHostId++;
            return s_nextHostId;
        }

        private void RegisterEventHandlers()
        {
            if (_eventHandlersRegistered)
                return;

            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            PreviewSettings.SettingsChanged += OnSettingsChanged;
            _eventHandlersRegistered = true;
        }

        private void UnregisterEventHandlers()
        {
            if (!_eventHandlersRegistered)
                return;

            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            PreviewSettings.SettingsChanged -= OnSettingsChanged;
            _eventHandlersRegistered = false;
        }

        private void OnSelectionChanged()
        {
            MarkTargetSupportCacheDirty();
        }

        private void OnProjectChanged()
        {
            MarkTargetSupportCacheDirty();
        }

        private void OnSettingsChanged()
        {
            CleanupActiveImplementation(clearSessionCache: true);
            MarkTargetSupportCacheDirty();
            RequestPreviewRepaint();
        }

        private void MarkTargetSupportCacheDirty()
        {
            _targetSupportCacheDirty = true;
            _cachedOwningPropertyEditor = null;
        }

        private void ClearTargetSupportCache()
        {
            _targetSupportCacheDirty = true;
            _cachedSelectionSupported = false;
            _cachedSelectionPrefab = null;
            _cachedSelectionKind = PrefabPreviewTargetKind.Unsupported;
            _cachedOwningPropertyEditor = null;
            _cachedSelectionCount = -1;
            _cachedSelectionActiveInstanceId = 0;
        }

        private void RequestPreviewRepaint()
        {
            bool repainted = TryInvokeObjectPreviewRepaint();
            if (RepaintOwningPropertyEditors())
                repainted = true;

            if (!repainted)
                TryInvokeObjectPreviewRepaint();
        }

        private void LogResolveState(string message, bool force = false)
        {
            if (!PreviewSettings.EnableDiagnostics)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (!force
                && string.Equals(_lastResolveLogKey, message, StringComparison.Ordinal)
                && _lastResolveLogTime >= 0d
                && now - _lastResolveLogTime < ResolveLogRepeatSeconds)
            {
                return;
            }

            _lastResolveLogKey = message;
            _lastResolveLogTime = now;
            PreviewDiagnostics.Log("Resolve", $"#{_hostId} {message}");
        }

        private bool TryInvokeObjectPreviewRepaint()
        {
            if (ObjectPreviewRepaintMethod == null)
                return false;

            try
            {
                ObjectPreviewRepaintMethod.Invoke(this, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool RepaintOwningPropertyEditors()
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition() || PropertyEditorType == null)
                return false;

            Object targetObject = target;
            if (targetObject == null)
                return false;

            if (TryGetCachedOwningPropertyEditor(targetObject, out Object cachedPropertyEditor)
                && cachedPropertyEditor is EditorWindow cachedWindow)
            {
                cachedWindow.Repaint();
                return true;
            }

            Object[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);
            bool repainted = false;
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                Object propertyEditor = propertyEditors[i];
                if (!ShouldRepaintPropertyEditor(propertyEditor, targetObject))
                    continue;

                if (propertyEditor is EditorWindow window)
                {
                    _cachedOwningPropertyEditor = propertyEditor;
                    window.Repaint();
                    repainted = true;
                }
            }

            return repainted;
        }

        private bool TryGetCachedOwningPropertyEditor(Object targetObject, out Object propertyEditor)
        {
            propertyEditor = _cachedOwningPropertyEditor;
            if (propertyEditor == null)
                return false;

            if (ShouldRepaintPropertyEditor(propertyEditor, targetObject))
                return true;

            _cachedOwningPropertyEditor = null;
            propertyEditor = null;
            return false;
        }

        private static bool ShouldRepaintPropertyEditor(Object propertyEditor, Object targetObject)
        {
            if (propertyEditor == null || targetObject == null)
                return false;

            if (PropertyEditorGetInspectedObjectMethod == null)
                return true;

            try
            {
                Object inspected = PropertyEditorGetInspectedObjectMethod.Invoke(propertyEditor, null) as Object;
                if (inspected == null)
                    return false;

                if (ReferenceEquals(inspected, targetObject))
                    return true;

                if (targetObject is GameObject targetPrefab)
                {
                    if (inspected is GameObject inspectedGo)
                    {
                        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(inspectedGo);
                        if (ReferenceEquals(source, targetPrefab))
                            return true;
                    }
                    else if (inspected is Component inspectedComponent && inspectedComponent.gameObject != null)
                    {
                        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(inspectedComponent.gameObject);
                        if (ReferenceEquals(source, targetPrefab))
                            return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

    }
}
