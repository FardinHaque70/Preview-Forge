using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
// Serves as the Unity custom preview entry point and routes each supported prefab target to the correct preview implementation.

namespace ParticleThumbnailAndPreview.Editor
{
    [CustomPreview(typeof(GameObject))]
    [CanEditMultipleObjects]
    public sealed class ParticlePrefabPreviewEditor : ObjectPreview
    {
        private static readonly string[] EditorAssemblyPreferenceOrder =
        {
            "UnityEditor.CoreModule",
            "UnityEditor",
        };

        private static readonly System.Reflection.MethodInfo ObjectPreviewRepaintMethod =
            typeof(ObjectPreview).GetMethod(
                "Repaint",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private static readonly Type PropertyEditorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.PropertyEditor");
        private static readonly System.Reflection.MethodInfo PropertyEditorGetInspectedObjectMethod =
            GetInstanceMethod(PropertyEditorType, "GetInspectedObject");

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
            EnsureImplementationInstances();
            RegisterEventHandlers();
            MarkTargetSupportCacheDirty();
            PreviewDiagnostics.Log("Host", $"#{_hostId} Initialize targets={targets?.Length ?? 0}");
        }

        public override void Cleanup()
        {
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
            if (!ParticlePreviewSettings.Active)
            {
                CleanupActiveImplementation();
                LogResolveState("settings-inactive");
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
                ParticlePreviewSettingsProvider.OpenSettings();
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

        private void EnsureImplementationInstances()
        {
            if (_implementations.Count > 0)
                return;

            var particle = new ParticlePreviewParticleImplementation();
            particle.SetRepaintCallback(RequestPreviewRepaint);
            _implementations[particle.Kind] = particle;

            var model = new ModelPrefabPreviewImplementation();
            model.SetRepaintCallback(RequestPreviewRepaint);
            _implementations[model.Kind] = model;
        }

        private IPrefabPreviewImplementation ResolveImplementation(PrefabPreviewTargetKind kind)
        {
            EnsureImplementationInstances();
            if (!_implementations.TryGetValue(kind, out IPrefabPreviewImplementation implementation))
                return null;

            if (kind == PrefabPreviewTargetKind.Model && !ParticlePreviewSettings.ModelPreviewActive)
                return null;

            if (kind == PrefabPreviewTargetKind.Particle && !ParticlePreviewSettings.Active)
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
            if (_activeImplementation == null)
                return;

            bool selectionIsEmpty = IsSelectionEmpty();
            _activeImplementation.Cleanup(selectionIsEmpty);
            PreviewDiagnostics.Log("Host", $"#{_hostId} cleanup-active kind={_activeKind} selectionEmpty={selectionIsEmpty} asset='{_activePrefabAssetPath}'");
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
            ParticlePreviewSettings.SettingsChanged += OnSettingsChanged;
            _eventHandlersRegistered = true;
        }

        private void UnregisterEventHandlers()
        {
            if (!_eventHandlersRegistered)
                return;

            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            ParticlePreviewSettings.SettingsChanged -= OnSettingsChanged;
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
            MarkTargetSupportCacheDirty();
        }

        private void MarkTargetSupportCacheDirty()
        {
            _targetSupportCacheDirty = true;
        }

        private void ClearTargetSupportCache()
        {
            _targetSupportCacheDirty = true;
            _cachedSelectionSupported = false;
            _cachedSelectionPrefab = null;
            _cachedSelectionKind = PrefabPreviewTargetKind.Unsupported;
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
            if (!ParticlePreviewSettings.EnableDiagnostics)
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
            if (PropertyEditorType == null)
                return false;

            Object targetObject = target;
            if (targetObject == null)
                return false;

            Object[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);
            bool repainted = false;
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                Object propertyEditor = propertyEditors[i];
                if (!ShouldRepaintPropertyEditor(propertyEditor, targetObject))
                    continue;

                if (propertyEditor is EditorWindow window)
                {
                    window.Repaint();
                    repainted = true;
                }
            }

            return repainted;
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

        private static Type ResolveEditorTypeAcrossAssemblies(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            for (int i = 0; i < EditorAssemblyPreferenceOrder.Length; i++)
            {
                Type preferred = Type.GetType(fullTypeName + ", " + EditorAssemblyPreferenceOrder[i], false);
                if (preferred != null)
                    return preferred;
            }

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type resolved = assemblies[i].GetType(fullTypeName, false);
                if (resolved != null)
                    return resolved;
            }

            return null;
        }

        private static System.Reflection.MethodInfo GetInstanceMethod(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            return type.GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }
    }
}
