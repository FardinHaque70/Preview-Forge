using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;
// Installs and maintains Harmony integration patches so preview behavior stays stable when Unity internals or registration paths differ.

namespace NoodleHammer.PreviewForge.Editor
{
    [InitializeOnLoad]
    internal static class PreviewHarmonyPatcher
    {
        private const string HarmonyId = "com.noodlehammer.preview-forge.preview";
        private const string InspectorWindowTypeName = "UnityEditor.InspectorWindow";
        private const string PropertyEditorTypeName = "UnityEditor.PropertyEditor";
        private const string PrefabPreviewTypeName = "NoodleHammer.PreviewForge.Editor.PrefabPreviewEditor";
        private const string ModelImporterPreviewTypeName = "NoodleHammer.PreviewForge.Editor.ModelImporterPreviewEditor";
        private const string HarmonyTypeName = "HarmonyLib.Harmony";
        private const string HarmonyMethodTypeName = "HarmonyLib.HarmonyMethod";
        private const string HarmonyAssemblyName = "0Harmony";
        private const string HarmonyRelativePath = "Editor/PreviewSystem/Plugins/ThirdParty/0Harmony.dll";
        private const int MaxRetryAttempts = 12;
        private const string CompatibilityRemediationHint =
            " If another tool (for example Odin Inspector) still overrides GameObject previews, disable this package's custom previews in Project Settings > Preview Forge, then re-enable after adjusting tool registration order.";

        private static readonly string[] EditorAssemblyPreferenceOrder =
        {
            "UnityEditor.CoreModule",
            "UnityEditor",
        };

        private static readonly HashSet<string> LoggedWarnings = new();
        private static bool _patchesApplied;
        private static bool _retryScheduled;
        private static bool _harmonyRuntimeUnsupported;
        private static int _retryAttempts;

        private static object _harmonyInstance;
        private static Type _harmonyType;
        private static Type _harmonyMethodType;
        private static MethodInfo _harmonyPatchMethod;
        private static MethodInfo _harmonyUnpatchAllMethod;
        private static ConstructorInfo _harmonyMethodCtor;
        private static ConstructorInfo _harmonyMethodCtorFromTypeName;
        private static PropertyInfo _activeEditorWindowsProperty;
        private static Type _propertyEditorType;
        private static MethodInfo _propertyEditorRebuildContentsContainersMethod;

        static PreviewHarmonyPatcher()
        {
            ApplyPatches();
            AssemblyReloadEvents.beforeAssemblyReload -= Unpatch;
            AssemblyReloadEvents.beforeAssemblyReload += Unpatch;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
        }

        private static void ApplyPatches()
        {
            if (_patchesApplied || _harmonyRuntimeUnsupported)
                return;

            try
            {
                if (!TryInitializeHarmonyApi())
                {
                    ScheduleRetry();
                    return;
                }

                Type gameObjectInspectorType = ResolveEditorTypeAcrossAssemblies("UnityEditor.GameObjectInspector");
                if (gameObjectInspectorType == null)
                {
                    LogWarningOnce(
                        "missing-gameobjectinspector",
                        $"[PreviewForge] UnityEditor.GameObjectInspector was not found in Unity {Application.unityVersion}; Harmony patch skipped.{CompatibilityRemediationHint}");
                    ScheduleRetry();
                    return;
                }

                MethodInfo hasPreviewGuiMethod = GetInstanceMethod(gameObjectInspectorType, "HasPreviewGUI");
                if (hasPreviewGuiMethod == null)
                {
                    LogWarningOnce(
                        "missing-gameobjectinspector-haspreview",
                        $"[PreviewForge] GameObjectInspector.HasPreviewGUI was not found in Unity {Application.unityVersion}; Harmony patch skipped.{CompatibilityRemediationHint}");
                    ScheduleRetry();
                    return;
                }

                if (!TryPatchWithPrefix(hasPreviewGuiMethod, nameof(GameObjectInspectorHasPreviewPrefix)))
                {
                    if (!_harmonyRuntimeUnsupported)
                        ScheduleRetry();
                    return;
                }

                PatchInspectorRedrawFromNative();
                SuppressCompetingCustomPreviews();
                _patchesApplied = true;
                _retryScheduled = false;
                _retryAttempts = 0;
            }
            catch (Exception exception)
            {
                LogWarningOnce("patch-apply-failed", $"[PreviewForge] Harmony patch failed: {DescribeException(exception)}{CompatibilityRemediationHint}");
                if (!_harmonyRuntimeUnsupported)
                    ScheduleRetry();
            }
        }

        private static void Unpatch()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Unpatch;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
            EditorApplication.delayCall -= RetryApply;
            _retryScheduled = false;
            _retryAttempts = 0;
            TryUnpatchHarmony();
            _harmonyInstance = null;
            _patchesApplied = false;
        }

        private static void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            if (_patchesApplied)
                return;

            ScheduleRetry();
        }

        private static void ScheduleRetry()
        {
            if (_retryScheduled || _patchesApplied)
                return;

            if (_retryAttempts >= MaxRetryAttempts)
            {
                LogWarningOnce(
                    "patch-retry-limit-reached",
                    $"[PreviewForge] Harmony patch retry limit reached ({MaxRetryAttempts}) on Unity {Application.unityVersion}. Further retries are disabled for this domain reload.{CompatibilityRemediationHint}");
                return;
            }

            _retryScheduled = true;
            _retryAttempts++;
            EditorApplication.delayCall += RetryApply;
        }

        private static void RetryApply()
        {
            EditorApplication.delayCall -= RetryApply;
            _retryScheduled = false;
            ApplyPatches();
        }

        private static bool TryInitializeHarmonyApi()
        {
            if (_harmonyInstance != null && _harmonyPatchMethod != null)
                return true;

            _harmonyType = ResolveHarmonyType(HarmonyTypeName);
            _harmonyMethodType = ResolveHarmonyType(HarmonyMethodTypeName);
            if (_harmonyType == null || _harmonyMethodType == null)
            {
                LogWarningOnce(
                    "harmony-type-missing",
                    $"[PreviewForge] Failed to resolve Harmony types from '{HarmonyAssemblyName}'.");
                return false;
            }

            _harmonyMethodCtor = _harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
            _harmonyMethodCtorFromTypeName = _harmonyMethodType.GetConstructor(new[] { typeof(Type), typeof(string), typeof(Type[]) });
            _harmonyPatchMethod = _harmonyType.GetMethod(
                "Patch",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(MethodBase), _harmonyMethodType, _harmonyMethodType, _harmonyMethodType, _harmonyMethodType },
                null);
            _harmonyUnpatchAllMethod = ResolveHarmonyUnpatchAllMethod();

            ConstructorInfo harmonyCtor = _harmonyType.GetConstructor(new[] { typeof(string) });
            if ((_harmonyMethodCtor == null && _harmonyMethodCtorFromTypeName == null)
                || _harmonyPatchMethod == null
                || _harmonyUnpatchAllMethod == null
                || harmonyCtor == null)
            {
                LogWarningOnce(
                    "harmony-api-mismatch",
                    "[PreviewForge] Harmony API surface does not match expected signatures.");
                return false;
            }

            _harmonyInstance = harmonyCtor.Invoke(new object[] { HarmonyId });
            return true;
        }

        private static MethodInfo ResolveHarmonyUnpatchAllMethod()
        {
            if (_harmonyType == null)
                return null;

            MethodInfo[] methods = _harmonyType.GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method == null || !string.Equals(method.Name, "UnpatchAll", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0)
                    return method;

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    return method;
            }

            return null;
        }

        private static void TryUnpatchHarmony()
        {
            if (_harmonyUnpatchAllMethod == null)
                return;

            try
            {
                ParameterInfo[] parameters = _harmonyUnpatchAllMethod.GetParameters();
                object[] args;
                if (parameters.Length == 0)
                {
                    args = Array.Empty<object>();
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                {
                    args = new object[] { HarmonyId };
                }
                else
                {
                    return;
                }

                object target = _harmonyUnpatchAllMethod.IsStatic ? null : _harmonyInstance;
                if (!_harmonyUnpatchAllMethod.IsStatic && target == null)
                    return;

                _harmonyUnpatchAllMethod.Invoke(target, args);
            }
            catch (Exception exception)
            {
                LogWarningOnce("unpatch-failed", $"[PreviewForge] Harmony unpatch failed: {DescribeException(exception)}");
            }
        }

        private static Type ResolveHarmonyType(string fullTypeName)
        {
            Type type = Type.GetType(fullTypeName + ", " + HarmonyAssemblyName, false);
            if (type != null)
                return type;

            try
            {
                string fullPath = TryResolveHarmonyAssemblyPath();
                if (string.IsNullOrEmpty(fullPath))
                    return null;

                Assembly assembly = Assembly.LoadFrom(fullPath);
                return assembly.GetType(fullTypeName, false);
            }
            catch (Exception exception)
            {
                LogWarningOnce("harmony-load-failed", $"[PreviewForge] Failed to load Harmony assembly: {exception.Message}");
                return null;
            }
        }

        private static string TryResolveHarmonyAssemblyPath()
        {
            string installPath = PreviewInstallLayout.TryResolveExistingAbsolutePath(HarmonyRelativePath);
            if (!string.IsNullOrEmpty(installPath))
                return installPath;

            string[] harmonyGuids = AssetDatabase.FindAssets("0Harmony");
            for (int i = 0; i < harmonyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(harmonyGuids[i]);
                if (string.IsNullOrEmpty(assetPath)
                    || !assetPath.EndsWith("/0Harmony.dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isKnownToolPath =
                    assetPath.IndexOf("/Noodle Hammer/Preview Forge/", StringComparison.OrdinalIgnoreCase) >= 0
                    || assetPath.IndexOf("/com.noodlehammer.preview-forge/", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isKnownToolPath)
                {
                    string fullPath = Path.GetFullPath(assetPath);
                    if (File.Exists(fullPath))
                        return fullPath;
                }
            }

            return null;
        }

        private static bool TryPatchWithPrefix(MethodInfo targetMethod, string prefixMethodName)
        {
            if (targetMethod == null || string.IsNullOrEmpty(prefixMethodName) || _harmonyPatchMethod == null)
                return false;

            MethodInfo prefixMethod = typeof(PreviewHarmonyPatcher).GetMethod(
                prefixMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prefixMethod == null)
                return false;

            try
            {
                object prefix = CreateHarmonyMethod(prefixMethod, prefixMethodName);
                if (prefix == null)
                    return false;

                _harmonyPatchMethod.Invoke(
                    _harmonyInstance,
                    new[] { (object)targetMethod, prefix, null, null, null });
                return true;
            }
            catch (Exception patchException)
            {
                if (IsRuntimeDetourBlocked(patchException))
                {
                    _harmonyRuntimeUnsupported = true;
                    LogWarningOnce(
                        "harmony-runtime-detour-blocked",
                        $"[PreviewForge] Harmony runtime detours are blocked on this editor runtime (mprotect EACCES). Falling back to internal auto-selection without preview suppression.{CompatibilityRemediationHint}");
                    return false;
                }

                LogWarningOnce(
                    "patch-method-failed-" + targetMethod.DeclaringType?.FullName + "." + targetMethod.Name,
                    $"[PreviewForge] Failed to patch '{targetMethod.DeclaringType?.FullName}.{targetMethod.Name}': {DescribeException(patchException)}{CompatibilityRemediationHint}");
                return false;
            }
        }

        private static object CreateHarmonyMethod(MethodInfo methodInfo, string methodName)
        {
            try
            {
                if (_harmonyMethodCtorFromTypeName != null)
                    return _harmonyMethodCtorFromTypeName.Invoke(new object[] { typeof(PreviewHarmonyPatcher), methodName, null });

                if (_harmonyMethodCtor != null)
                    return _harmonyMethodCtor.Invoke(new object[] { methodInfo });
            }
            catch (Exception exception)
            {
                LogWarningOnce(
                    "create-harmonymethod-failed-" + methodName,
                    $"[PreviewForge] Failed to create HarmonyMethod for '{methodName}': {DescribeException(exception)}");
            }

            return null;
        }

        private static void PatchInspectorRedrawFromNative()
        {
            Type inspectorWindowType = ResolveEditorTypeAcrossAssemblies(InspectorWindowTypeName);
            MethodInfo redrawFromNativeMethod = GetStaticMethod(inspectorWindowType, "RedrawFromNative");
            if (redrawFromNativeMethod == null)
            {
                LogWarningOnce(
                    "missing-inspector-redraw-from-native",
                    $"[PreviewForge] {InspectorWindowTypeName}.RedrawFromNative was not found in Unity {Application.unityVersion}; inspector redraw safety patch skipped.");
                return;
            }

            _propertyEditorType = ResolveEditorTypeAcrossAssemblies(PropertyEditorTypeName);
            _activeEditorWindowsProperty = typeof(EditorWindow).GetProperty(
                "activeEditorWindows",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            _propertyEditorRebuildContentsContainersMethod = GetInstanceMethod(_propertyEditorType, "RebuildContentsContainers");

            if (_activeEditorWindowsProperty == null || _propertyEditorRebuildContentsContainersMethod == null)
            {
                LogWarningOnce(
                    "missing-inspector-redraw-dependencies",
                    $"[PreviewForge] Unity inspector redraw internals changed in Unity {Application.unityVersion}; inspector redraw safety patch skipped.");
                return;
            }

            TryPatchWithPrefix(redrawFromNativeMethod, nameof(InspectorWindowRedrawFromNativePrefix));
        }

        private static void SuppressCompetingCustomPreviews()
        {
            if (_harmonyInstance == null)
                return;

            try
            {
                foreach (Type previewType in TypeCache.GetTypesWithAttribute<CustomPreviewAttribute>())
                {
                    if (previewType == null)
                        continue;

                    if (string.Equals(previewType.FullName, PrefabPreviewTypeName, StringComparison.Ordinal)
                        || string.Equals(previewType.FullName, ModelImporterPreviewTypeName, StringComparison.Ordinal))
                        continue;

                    if (!IsCustomPreviewForGameObject(previewType))
                        continue;

                    MethodInfo hasPreviewGuiMethod = GetInstanceMethod(previewType, "HasPreviewGUI");
                    if (hasPreviewGuiMethod == null)
                        continue;

                    TryPatchWithPrefix(hasPreviewGuiMethod, nameof(CustomPreviewHasPreviewPrefix));
                }
            }
            catch (Exception exception)
            {
                LogWarningOnce(
                    "enumerate-competing-previews-failed",
                    $"[PreviewForge] Failed to enumerate competing previews: {exception.Message}{CompatibilityRemediationHint}");
            }
        }

        private static bool IsCustomPreviewForGameObject(Type previewType)
        {
            object[] attributes = previewType.GetCustomAttributes(typeof(CustomPreviewAttribute), false);
            for (int i = 0; i < attributes.Length; i++)
            {
                object attribute = attributes[i];
                if (attribute == null)
                    continue;

                Type attributeType = attribute.GetType();
                FieldInfo field = attributeType.GetField("m_Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.GetValue(attribute) as Type == typeof(GameObject))
                    return true;

                PropertyInfo property = attributeType.GetProperty("m_Type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.PropertyType == typeof(Type))
                {
                    Type propertyValue = property.GetValue(attribute, null) as Type;
                    if (propertyValue == typeof(GameObject))
                        return true;
                }
            }

            return false;
        }

        private static Type ResolveEditorTypeAcrossAssemblies(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return null;

            for (int i = 0; i < EditorAssemblyPreferenceOrder.Length; i++)
            {
                Type preferredType = Type.GetType(fullTypeName + ", " + EditorAssemblyPreferenceOrder[i], false);
                if (preferredType != null)
                    return preferredType;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type resolvedType = assemblies[i].GetType(fullTypeName, false);
                if (resolvedType != null)
                    return resolvedType;
            }

            return null;
        }

        private static MethodInfo GetInstanceMethod(Type type, string methodName)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
                return null;

            return type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }

        private static MethodInfo GetStaticMethod(Type type, string methodName)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
                return null;

            return type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }

        private static void LogWarningOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
                return;

            if (!LoggedWarnings.Add(key))
                return;

            Debug.LogWarning(message);
        }

        private static string DescribeException(Exception exception)
        {
            if (exception == null)
                return "Unknown exception";

            Exception current = exception;
            while (current is TargetInvocationException tie && tie.InnerException != null)
                current = tie.InnerException;

            return $"{current.GetType().Name}: {current.Message}\n{current}";
        }

        private static bool IsRuntimeDetourBlocked(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                string message = current.Message;
                if (!string.IsNullOrEmpty(message)
                    && message.IndexOf("mprotect returned EACCES", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static bool GameObjectInspectorHasPreviewPrefix(UnityEditor.Editor __instance, ref bool __result)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
                return true;

            try
            {
                if (!PrefabPreviewTargetGate.ShouldSuppressCompetingPreview(__instance != null ? __instance.targets : null))
                    return true;

                __result = false;
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool CustomPreviewHasPreviewPrefix(object __instance, ref bool __result)
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
                return true;

            try
            {
                UnityObject[] targets = PrefabPreviewTargetGate.TryGetObjectPreviewTargets(__instance);
                if (!PrefabPreviewTargetGate.ShouldSuppressCompetingPreview(targets))
                    return true;

                __result = false;
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static bool InspectorWindowRedrawFromNativePrefix()
        {
            if (PreviewEditorTransitionGuard.IsUnsafeTransition())
                return true;

            EditorWindow[] snapshot;
            try
            {
                snapshot = GetActiveEditorWindowsSnapshot();
            }
            catch (Exception exception)
            {
                LogWarningOnce(
                    "inspector-redraw-prefix-failed",
                    $"[PreviewForge] Inspector redraw safety patch failed: {DescribeException(exception)}");
                return true;
            }

            if (snapshot == null)
                return true;

            int beforeCount = snapshot.Length;
            for (int i = 0; i < snapshot.Length; i++)
            {
                EditorWindow editorWindow = snapshot[i];
                if (editorWindow == null)
                    continue;

                if (_propertyEditorType == null || !_propertyEditorType.IsInstanceOfType(editorWindow))
                    continue;

                if (TryInvokePropertyEditorRebuild(editorWindow, out Exception rebuildException))
                    continue;

                if (IsRecoverableInspectorRebuildException(rebuildException))
                {
                    LogWarningOnce(
                        "inspector-redraw-recoverable-fallback",
                        $"[PreviewForge] Inspector redraw hit a transient null-target rebuild state during editor transition. Falling back to Unity native redraw path for safety. Details: {DescribeException(rebuildException)}");
                }
                else
                {
                    LogWarningOnce(
                        "inspector-redraw-rebuild-failed",
                        $"[PreviewForge] Inspector redraw rebuild failed. Falling back to Unity native redraw path for safety. Details: {DescribeException(rebuildException)}");
                }

                return true;
            }

            int afterCount = GetActiveEditorWindowCount();
            if (afterCount >= 0 && afterCount != beforeCount)
            {
                PreviewDiagnostics.Log(
                    "HarmonyPatcher",
                    $"Inspector redraw used a stable editor-window snapshot because Unity changed activeEditorWindows during redraw before={beforeCount} after={afterCount}");
            }

            return false;
        }

        private static bool TryInvokePropertyEditorRebuild(EditorWindow editorWindow, out Exception exception)
        {
            exception = null;
            try
            {
                _propertyEditorRebuildContentsContainersMethod.Invoke(editorWindow, null);
                return true;
            }
            catch (TargetInvocationException invocationException) when (invocationException.InnerException != null)
            {
                exception = invocationException.InnerException;
                return false;
            }
            catch (Exception caught)
            {
                exception = caught;
                return false;
            }
        }

        private static bool IsRecoverableInspectorRebuildException(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                if (current is ArgumentNullException || current is NullReferenceException)
                    return true;

                Type currentType = current.GetType();
                string fullTypeName = currentType.FullName ?? string.Empty;
                if (fullTypeName.IndexOf("SerializedObjectNotCreatableException", StringComparison.Ordinal) >= 0)
                    return true;

                string message = current.Message ?? string.Empty;
                if (message.IndexOf("Object at index 0 is null", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("componentOrGameObject", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
        }

        private static EditorWindow[] GetActiveEditorWindowsSnapshot()
        {
            if (_activeEditorWindowsProperty == null)
                return null;

            object value = _activeEditorWindowsProperty.GetValue(null, null);
            if (value is IList<EditorWindow> typedWindows)
            {
                EditorWindow[] snapshot = new EditorWindow[typedWindows.Count];
                typedWindows.CopyTo(snapshot, 0);
                return snapshot;
            }

            if (value is System.Collections.IList windows)
            {
                EditorWindow[] snapshot = new EditorWindow[windows.Count];
                for (int i = 0; i < windows.Count; i++)
                    snapshot[i] = windows[i] as EditorWindow;

                return snapshot;
            }

            return null;
        }

        private static int GetActiveEditorWindowCount()
        {
            if (_activeEditorWindowsProperty == null)
                return -1;

            object value = _activeEditorWindowsProperty.GetValue(null, null);
            if (value is IList<EditorWindow> typedWindows)
                return typedWindows.Count;

            if (value is System.Collections.IList windows)
                return windows.Count;

            return -1;
        }

        private static string DescribeActiveEditorWindowTypes()
        {
            EditorWindow[] snapshot = GetActiveEditorWindowsSnapshot();
            return DescribeEditorWindowTypes(snapshot);
        }

        private static string DescribeEditorWindowTypes(EditorWindow[] windows)
        {
            if (windows == null)
                return "<null>";

            if (windows.Length == 0)
                return "<empty>";

            int count = Math.Min(windows.Length, 24);
            string[] parts = new string[count];
            for (int i = 0; i < count; i++)
            {
                EditorWindow window = windows[i];
                parts[i] = window != null ? window.GetType().FullName : "<null>";
            }

            string suffix = windows.Length > count ? ", ..." : string.Empty;
            return "[" + string.Join(", ", parts) + suffix + "]";
        }
    }

    [InitializeOnLoad]
    internal static class PreviewCompatibilityBootstrap
    {
        static PreviewCompatibilityBootstrap()
        {
            // Force static constructor execution explicitly so patch bootstrap remains
            // resilient even when script load ordering changes in newer Unity versions.
            RuntimeHelpers.RunClassConstructor(typeof(PreviewHarmonyPatcher).TypeHandle);
        }
    }
}
