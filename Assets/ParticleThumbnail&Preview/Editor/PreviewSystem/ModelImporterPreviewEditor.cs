using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
// Model importer preview host based on ObjectPreview to avoid unsupported nested AssetImporterEditor wrapping.

namespace ParticleThumbnailAndPreview.Editor
{
    [CustomPreview(typeof(GameObject))]
    [CanEditMultipleObjects]
    public sealed class ModelImporterPreviewEditor : ObjectPreview
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
        private static readonly System.Reflection.FieldInfo PropertyEditorPreviewsField =
            GetInstanceField(PropertyEditorType, "m_Previews");
        private static readonly System.Reflection.FieldInfo PropertyEditorTrackerField =
            GetInstanceField(PropertyEditorType, "m_Tracker");
        private static readonly System.Reflection.PropertyInfo ActiveEditorTrackerActiveEditorsProperty =
            typeof(ActiveEditorTracker).GetProperty("activeEditors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        private static readonly Type AssetImporterEditorType =
            ResolveEditorTypeAcrossAssemblies("UnityEditor.AssetImporters.AssetImporterEditor");
        private static readonly System.Reflection.PropertyInfo AssetImporterEditorShowImportedObjectProperty =
            AssetImporterEditorType?.GetProperty(
                "showImportedObject",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        private const double TransientTargetLossGraceSeconds = 1.0d;
        private const double ModelTabResolveGraceSeconds = 0.85d;
        private static int s_nextHostId;

        private ModelImporterPreviewImplementation _previewImplementation;
        private GameObject _activeModelRoot;
        private AnimationClip _activeAnimationClip;
        private string _activeModelAssetPath;
        private double _lastSuccessfulResolveTime = -1d;
        private bool _eventHandlersRegistered;
        private bool _targetSupportCacheDirty = true;
        private GameObject _cachedSelectionModelRoot;
        private int _cachedSelectionCount = -1;
        private int _cachedSelectionActiveInstanceId;
        private Object _cachedOwningPropertyEditor;
        private double _lastSelectionChangedTime = -1d;
        private readonly int _hostId = GetNextHostId();
        private readonly HashSet<string> _seenResolveMessages = new(StringComparer.Ordinal);

        #region Lifecycle
        public override void Initialize(Object[] targets)
        {
            base.Initialize(targets);
            EnsurePreviewImplementation();
            RegisterEventHandlers();
            MarkTargetSupportCacheDirty();
            _lastSelectionChangedTime = EditorApplication.timeSinceStartup;
            LogResolveState($"initialize targets={targets?.Length ?? 0} target={DescribeObject(target)}", force: true);
        }

        public override void Cleanup()
        {
            CleanupPreview(clearSessionCache: IsSelectionEmpty());
            _activeModelAssetPath = null;
            _lastSuccessfulResolveTime = -1d;
            ClearTargetSupportCache();
            UnregisterEventHandlers();
            _seenResolveMessages.Clear();
            LogResolveState("cleanup", force: true);
            base.Cleanup();
        }
        #endregion

        #region Preview Host
        public override bool HasPreviewGUI()
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
            {
                CleanupPreview(clearSessionCache: true);
                LogResolveState("unsafe-transition", force: true);
                return false;
            }

            if (!PreviewSettings.ThreeDAssetPreviewActive)
            {
                CleanupPreview(clearSessionCache: false);
                LogResolveState("settings-inactive");
                return false;
            }

            bool hasMultiSelection = Selection.count > 1;
            bool hasMultiTargets = m_Targets != null && m_Targets.Length > 1;
            if (hasMultiSelection || hasMultiTargets)
            {
                CleanupPreview(clearSessionCache: false);
                LogResolveState($"multi-selection selection={Selection.count} targets={m_Targets?.Length ?? 0}");
                return false;
            }

            if (IsDirectPreviewTargetExplicitlyUnsupported())
            {
                CleanupPreview(clearSessionCache: false);
                LogResolveState(
                    $"direct-target-unsupported target={DescribeObject(target)} m_Targets={DescribeTargetArray(m_Targets)}");
                return false;
            }

            if (!TryResolveActiveModelContext(out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip))
            {
                CleanupPreview(clearSessionCache: false);
                LogResolveState(
                    $"resolve-failed target={DescribeObject(target)} m_Targets={DescribeTargetArray(m_Targets)} selection={DescribeTargetArray(Selection.objects)}");
                return false;
            }

            bool modelTabActive = IsModelImporterModelTabActive(modelAssetPath, out bool modelTabResolved);
            if (!modelTabActive && !CanUseUnresolvedModelTabGrace(modelRoot, modelAssetPath, modelTabResolved))
            {
                CleanupPreview(clearSessionCache: false);
                string reason = modelTabResolved ? "non-model-tab-default" : "model-tab-unresolved-default";
                LogResolveState($"{reason} asset='{modelAssetPath}'");
                return false;
            }

            if (!IsModelContextConfirmed(modelRoot, modelAssetPath, animationClip))
            {
                CleanupPreview(clearSessionCache: false);
                LogResolveState(
                    $"context-unconfirmed asset='{modelAssetPath}' root={(modelRoot != null ? modelRoot.name : "<null>")} clip={(animationClip != null ? animationClip.name : "<none>")}");
                return false;
            }

            EnsurePreviewImplementation();
            if (_previewImplementation == null || !_previewImplementation.EnsureReady(modelRoot, animationClip))
            {
                CleanupPreview(clearSessionCache: false);
                LogResolveState(
                    $"implementation-not-ready asset='{modelAssetPath}' root={(modelRoot != null ? modelRoot.name : "<null>")} clip={(animationClip != null ? animationClip.name : "<none>")}",
                    force: true);
                return false;
            }

            _activeModelRoot = modelRoot;
            _activeAnimationClip = animationClip;
            _activeModelAssetPath = modelAssetPath;
            LogResolveState(
                $"active asset='{modelAssetPath}' root={modelRoot.name} clip={(animationClip != null ? animationClip.name : "<none>")} modelTab=true");
            CustomPreviewAutoSelector.NotifyModelImporterPreviewCandidate(modelAssetPath);
            return true;
        }

        public override GUIContent GetPreviewTitle()
        {
            if (_previewImplementation != null && _activeModelRoot != null)
                return _previewImplementation.GetPreviewTitle();

            return GUIContent.none;
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
            if (!HasPreviewGUI() || _previewImplementation == null)
            {
                if (isInteractive)
                    base.OnInteractivePreviewGUI(rect, background);
                else
                    base.OnPreviewGUI(rect, background);
                return;
            }

            _lastSuccessfulResolveTime = EditorApplication.timeSinceStartup;
            _previewImplementation.Draw(rect, background, isInteractive);
        }
        #endregion

        private bool CanUseUnresolvedModelTabGrace(GameObject modelRoot, string modelAssetPath, bool modelTabResolved)
        {
            if (modelTabResolved)
                return false;

            if (modelRoot == null || string.IsNullOrEmpty(modelAssetPath))
                return false;

            if (!TryResolveModelPreviewContext(Selection.activeObject, out GameObject selectedRoot, out string selectedAssetPath, out _))
                return false;

            if (!ReferenceEquals(selectedRoot, modelRoot)
                && !string.Equals(selectedAssetPath, modelAssetPath, StringComparison.Ordinal))
            {
                return false;
            }

            double now = EditorApplication.timeSinceStartup;
            return _lastSelectionChangedTime >= 0d
                   && now - _lastSelectionChangedTime <= ModelTabResolveGraceSeconds;
        }

        #region Resolution
        private bool TryResolveActiveModelContext(out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip)
        {
            modelRoot = null;
            modelAssetPath = null;
            animationClip = null;

            if (TryResolveFromTargets(m_Targets, out modelRoot, out modelAssetPath, out animationClip))
                return true;

            if (TryResolveFromTargets(new[] { target }, out modelRoot, out modelAssetPath, out animationClip))
                return true;

            if (TryGetCachedSupportedSelectionTarget(out modelRoot, out modelAssetPath, out animationClip))
                return true;

            return TryResolveFromTransientLossFallback(out modelRoot, out modelAssetPath, out animationClip);
        }

        private bool IsDirectPreviewTargetExplicitlyUnsupported()
        {
            if (TryIsExplicitlyUnsupportedTarget(target))
                return true;

            if (m_Targets != null && m_Targets.Length == 1 && TryIsExplicitlyUnsupportedTarget(m_Targets[0]))
                return true;

            return false;
        }

        private static bool TryIsExplicitlyUnsupportedTarget(Object candidate)
        {
            if (candidate == null)
                return false;

            return !TryResolveModelPreviewContext(candidate, out _, out _, out _);
        }

        private bool TryGetCachedSupportedSelectionTarget(out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip)
        {
            modelRoot = null;
            modelAssetPath = null;
            animationClip = null;

            int currentSelectionCount = Selection.count;
            Object activeSelectionObject = Selection.activeObject;
            int currentSelectionActiveInstanceId = activeSelectionObject != null ? activeSelectionObject.GetInstanceID() : 0;
            bool selectionSignatureChanged = currentSelectionCount != _cachedSelectionCount
                                             || currentSelectionActiveInstanceId != _cachedSelectionActiveInstanceId;

            if (_targetSupportCacheDirty || selectionSignatureChanged)
                RebuildSelectionSupportCache(currentSelectionCount, currentSelectionActiveInstanceId);

            if (_cachedSelectionModelRoot == null)
                return false;

            modelRoot = _cachedSelectionModelRoot;
            modelAssetPath = AssetDatabase.GetAssetPath(modelRoot);
            return !string.IsNullOrEmpty(modelAssetPath);
        }

        private void RebuildSelectionSupportCache(int currentSelectionCount, int currentSelectionActiveInstanceId)
        {
            _cachedSelectionCount = currentSelectionCount;
            _cachedSelectionActiveInstanceId = currentSelectionActiveInstanceId;
            _targetSupportCacheDirty = false;
            _cachedSelectionModelRoot = null;

            if (Selection.count != 1)
                return;

            if (!TryResolveModelPreviewContext(Selection.activeObject, out GameObject selectedRoot, out _, out _))
                return;

            _cachedSelectionModelRoot = selectedRoot;
        }

        private static bool TryResolveFromTargets(Object[] targets, out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip)
        {
            modelRoot = null;
            modelAssetPath = null;
            animationClip = null;

            if (targets == null || targets.Length != 1)
                return false;

            if (!TryResolveModelPreviewContext(targets[0], out modelRoot, out modelAssetPath, out animationClip))
                return false;

            return true;
        }

        private bool TryResolveFromTransientLossFallback(out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip)
        {
            modelRoot = null;
            modelAssetPath = null;
            animationClip = _activeAnimationClip;

            if (_activeModelRoot == null || string.IsNullOrEmpty(_activeModelAssetPath))
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (_lastSuccessfulResolveTime < 0d || now - _lastSuccessfulResolveTime > TransientTargetLossGraceSeconds)
                return false;

            modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(_activeModelAssetPath);
            if (modelRoot == null)
            {
                LogResolveState($"transient-fallback-load-failed asset='{_activeModelAssetPath}'", force: true);
                return false;
            }

            modelAssetPath = _activeModelAssetPath;
            LogResolveState(
                $"transient-fallback asset='{modelAssetPath}' clip={(animationClip != null ? animationClip.name : "<none>")}");
            return true;
        }

        private bool IsModelContextConfirmed(GameObject modelRoot, string modelAssetPath, AnimationClip animationClip)
        {
            if (modelRoot == null || string.IsNullOrEmpty(modelAssetPath))
                return false;

            if (!TryGetOwningPropertyEditorInspectedObject(out Object inspectedObject, out bool ownerResolved))
                return CanUseTransientContextFallback(modelRoot, modelAssetPath, animationClip, ownerResolved);

            if (!TryResolveModelPreviewContextFromInspectedContext(inspectedObject, out GameObject inspectedRoot, out string inspectedAssetPath, out AnimationClip inspectedClip))
                return false;

            if (!ReferenceEquals(inspectedRoot, modelRoot))
                return false;

            if (!string.Equals(inspectedAssetPath, modelAssetPath, StringComparison.Ordinal))
                return false;

            if (animationClip == null)
                return inspectedClip == null;

            if (inspectedClip == null)
                return true;

            return ReferenceEquals(inspectedClip, animationClip);
        }

        private bool CanUseTransientContextFallback(GameObject modelRoot, string modelAssetPath, AnimationClip animationClip, bool ownerResolved)
        {
            if (!ownerResolved)
                return false;

            if (_activeModelRoot == null || string.IsNullOrEmpty(_activeModelAssetPath))
                return false;

            if (!ReferenceEquals(_activeModelRoot, modelRoot)
                && !string.Equals(_activeModelAssetPath, modelAssetPath, StringComparison.Ordinal))
            {
                return false;
            }

            if (!ReferenceEquals(_activeAnimationClip, animationClip))
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (_lastSuccessfulResolveTime < 0d || now - _lastSuccessfulResolveTime > TransientTargetLossGraceSeconds)
                return false;

            return true;
        }

        private bool TryGetOwningPropertyEditorInspectedObject(out Object inspectedObject, out bool ownerResolved)
        {
            inspectedObject = null;
            ownerResolved = false;

            if (PropertyEditorType == null || PropertyEditorPreviewsField == null)
                return false;

            if (!TryGetOwningPropertyEditor(out Object propertyEditor))
                return false;

            ownerResolved = true;
            if (PropertyEditorGetInspectedObjectMethod == null)
                return false;

            try
            {
                inspectedObject = PropertyEditorGetInspectedObjectMethod.Invoke(propertyEditor, null) as Object;
            }
            catch
            {
                inspectedObject = null;
            }

            return inspectedObject != null;
        }

        private bool TryGetOwningPropertyEditor(out Object propertyEditor)
        {
            propertyEditor = null;
            if (PropertyEditorType == null || PropertyEditorPreviewsField == null)
                return false;

            if (TryGetCachedOwningPropertyEditor(out propertyEditor))
                return true;

            Object[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                Object candidatePropertyEditor = propertyEditors[i];
                if (candidatePropertyEditor == null)
                    continue;

                IList previews = null;
                try
                {
                    previews = PropertyEditorPreviewsField.GetValue(candidatePropertyEditor) as IList;
                }
                catch
                {
                    continue;
                }

                if (!ContainsPreviewInstance(previews, this))
                    continue;

                propertyEditor = candidatePropertyEditor;
                _cachedOwningPropertyEditor = propertyEditor;
                return true;
            }

            LogResolveState("owner-property-editor-not-found");
            return false;
        }

        private bool TryGetCachedOwningPropertyEditor(out Object propertyEditor)
        {
            propertyEditor = _cachedOwningPropertyEditor;
            if (propertyEditor == null || PropertyEditorPreviewsField == null)
                return false;

            try
            {
                IList previews = PropertyEditorPreviewsField.GetValue(propertyEditor) as IList;
                if (ContainsPreviewInstance(previews, this))
                    return true;
            }
            catch
            {
                // If Unity internals changed, clear cache and fall back to scan.
            }

            _cachedOwningPropertyEditor = null;
            propertyEditor = null;
            return false;
        }

        private bool IsModelImporterModelTabActive(string modelAssetPath, out bool resolved)
        {
            resolved = false;
            if (string.IsNullOrEmpty(modelAssetPath))
                return false;

            if (!TryGetOwningPropertyEditor(out Object owningPropertyEditor))
                return false;

            if (PropertyEditorTrackerField == null || ActiveEditorTrackerActiveEditorsProperty == null)
                return false;

            ActiveEditorTracker tracker = null;
            try
            {
                tracker = PropertyEditorTrackerField.GetValue(owningPropertyEditor) as ActiveEditorTracker;
            }
            catch
            {
                return false;
            }

            if (tracker == null)
                return false;

            UnityEditor.Editor[] activeEditors = null;
            try
            {
                activeEditors = ActiveEditorTrackerActiveEditorsProperty.GetValue(tracker, null) as UnityEditor.Editor[];
            }
            catch
            {
                return false;
            }

            if (activeEditors == null || activeEditors.Length == 0)
                return false;

            for (int i = 0; i < activeEditors.Length; i++)
            {
                UnityEditor.Editor editor = activeEditors[i];
                if (editor == null || !(editor.target is ModelImporter modelImporter))
                    continue;

                if (!string.Equals(modelImporter.assetPath, modelAssetPath, StringComparison.Ordinal))
                    continue;

                resolved = true;
                return TryGetShowImportedObject(editor, out bool showImportedObject) && showImportedObject;
            }

            return false;
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

        private static bool ContainsPreviewInstance(IList previews, ObjectPreview previewInstance)
        {
            if (previews == null || previewInstance == null)
                return false;

            for (int i = 0; i < previews.Count; i++)
            {
                if (ReferenceEquals(previews[i], previewInstance))
                    return true;
            }

            return false;
        }

        private static bool TryResolveModelPreviewContext(Object candidate, out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip)
        {
            modelRoot = null;
            modelAssetPath = null;
            animationClip = null;

            if (candidate == null)
                return false;

            if (candidate is ModelImporter importer)
            {
                string importerPath = importer.assetPath;
                if (string.IsNullOrEmpty(importerPath))
                    return false;

                GameObject mainFromImporter = AssetDatabase.LoadMainAssetAtPath(importerPath) as GameObject;
                if (!IsTopLevelModelRoot(mainFromImporter, out modelAssetPath))
                    return false;

                modelRoot = mainFromImporter;
                return true;
            }

            if (candidate is AnimationClip)
                return false;

            GameObject gameObjectCandidate = candidate as GameObject;
            if (gameObjectCandidate == null && candidate is Component component)
                gameObjectCandidate = component.gameObject;
            if (!IsTopLevelModelRoot(gameObjectCandidate, out modelAssetPath))
                return false;

            modelRoot = gameObjectCandidate;
            return true;
        }

        private static bool TryResolveModelPreviewContextFromInspectedContext(Object inspectedObject, out GameObject modelRoot, out string modelAssetPath, out AnimationClip animationClip)
        {
            return TryResolveModelPreviewContext(inspectedObject, out modelRoot, out modelAssetPath, out animationClip);
        }

        private static bool IsTopLevelModelRoot(GameObject gameObject, out string assetPath)
        {
            assetPath = null;
            if (gameObject == null)
                return false;

            if (!EditorUtility.IsPersistent(gameObject))
                return false;

            if (!PrefabUtility.IsPartOfModelPrefab(gameObject))
                return false;

            assetPath = AssetDatabase.GetAssetPath(gameObject);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter))
                return false;

            GameObject mainObject = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
            return ReferenceEquals(mainObject, gameObject);
        }

        #endregion

        #region Repaint
        private void EnsurePreviewImplementation()
        {
            if (_previewImplementation != null)
                return;

            _previewImplementation = new ModelImporterPreviewImplementation();
            _previewImplementation.SetRepaintCallback(RequestPreviewRepaint);
        }

        private void RequestPreviewRepaint()
        {
            bool repainted = TryInvokeObjectPreviewRepaint();
            if (!PreviewEditorTransitionGuard.IsUnsafeTransition() && RepaintOwningPropertyEditors())
                repainted = true;

            if (!repainted)
                TryInvokeObjectPreviewRepaint();
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

            Object[] propertyEditors = Resources.FindObjectsOfTypeAll(PropertyEditorType);
            bool repainted = false;
            for (int i = 0; i < propertyEditors.Length; i++)
            {
                Object propertyEditor = propertyEditors[i];
                if (!ShouldRepaintPropertyEditor(propertyEditor))
                    continue;

                if (propertyEditor is EditorWindow window)
                {
                    window.Repaint();
                    repainted = true;
                }
            }

            return repainted;
        }

        private bool ShouldRepaintPropertyEditor(Object propertyEditor)
        {
            if (propertyEditor == null || PropertyEditorPreviewsField == null)
                return false;

            IList previews = null;
            try
            {
                previews = PropertyEditorPreviewsField.GetValue(propertyEditor) as IList;
            }
            catch
            {
                return false;
            }

            return ContainsPreviewInstance(previews, this);
        }

        #endregion

        #region Events and Cleanup
        private void CleanupPreview(bool clearSessionCache)
        {
            if (_previewImplementation == null)
                return;

            _previewImplementation.Cleanup(clearSessionCache);
            _activeModelRoot = null;
            _activeAnimationClip = null;
            _activeModelAssetPath = null;
            _lastSuccessfulResolveTime = -1d;
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
            _lastSelectionChangedTime = EditorApplication.timeSinceStartup;
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
            _cachedOwningPropertyEditor = null;
        }

        private void ClearTargetSupportCache()
        {
            _targetSupportCacheDirty = true;
            _cachedSelectionModelRoot = null;
            _cachedSelectionCount = -1;
            _cachedSelectionActiveInstanceId = 0;
            _cachedOwningPropertyEditor = null;
        }

        private static bool IsSelectionEmpty()
        {
            Object[] selection = Selection.objects;
            return selection == null || selection.Length == 0;
        }

        private void LogResolveState(string message, bool force = false)
        {
            if (!PreviewSettings.EnableDiagnostics)
                return;

            if (!force && _seenResolveMessages.Contains(message))
                return;

            if (!force)
                _seenResolveMessages.Add(message);
            PreviewDiagnostics.Log("ModelImporterResolve", $"#{_hostId} {message}");
        }

        private static int GetNextHostId()
        {
            s_nextHostId++;
            return s_nextHostId;
        }

        private static string DescribeTargetArray(Object[] objects)
        {
            if (objects == null)
                return "<null>";
            if (objects.Length == 0)
                return "[]";

            int count = Mathf.Min(objects.Length, 4);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
                parts[i] = DescribeObject(objects[i]);
            string suffix = objects.Length > count ? ",..." : string.Empty;
            return "[" + string.Join(", ", parts) + suffix + "]";
        }

        private static string DescribeObject(Object obj)
        {
            if (obj == null)
                return "<null>";

            string path = AssetDatabase.GetAssetPath(obj);
            string typeName = obj.GetType().Name;
            if (string.IsNullOrEmpty(path))
                return $"{typeName}:{obj.name}";

            return $"{typeName}:{obj.name}@{path}";
        }
        #endregion

        #region Reflection
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

        private static System.Reflection.FieldInfo GetInstanceField(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name))
                return null;

            return type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        }
        #endregion
    }
}
