#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
    /// <summary>
    /// Harmony bootstrapper for Unity 2021/2022/2023/6000.
    ///
    /// This class intentionally does not own preview/tab/picker behavior.
    /// It only initializes the precompiled Harmony patcher assembly via reflection,
    /// keeping Harmony references out of normal asmdef compile graphs.
    /// </summary>
    [InitializeOnLoad]
    internal static class UnityVersionCompatibilityPatcher
    {
        private const string HarmonyAssemblyName = "ImprovedAssetTools.HarmonyPatcher";
        private const string HarmonyPatcherTypeName = "FardinHaque.ImprovedAssetTools.Editor.GameObjectInspectorPatcher";

        private static readonly HashSet<string> s_loggedWarnings = new HashSet<string>();

        private static bool s_initialized;
        private static bool s_assemblyLoadSubscribed;

        static UnityVersionCompatibilityPatcher()
        {
            TryInitializeHarmonyPatcher();

            // Retry once the domain finishes loading delayed editor assemblies.
            EditorApplication.delayCall += TryInitializeOnDelay;

            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        private static void TryInitializeOnDelay()
        {
            if (s_initialized)
                return;

            TryInitializeHarmonyPatcher();
        }

        private static void BeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            EditorApplication.delayCall -= TryInitializeOnDelay;
            RemoveAssemblyLoadSubscription();
            s_initialized = false;
        }

        private static void TryInitializeHarmonyPatcher()
        {
            if (s_initialized)
                return;

            try
            {
                if (TryResolveHarmonyPatcherType(out Type patcherType))
                {
                    RuntimeHelpers.RunClassConstructor(patcherType.TypeHandle);
                    s_initialized = true;
                    RemoveAssemblyLoadSubscription();
                    return;
                }

                EnsureAssemblyLoadSubscription();
            }
            catch (Exception exception)
            {
                LogWarningOnce(
                    "harmony-bootstrap-failed",
                    $"[ImprovedPreview] Harmony bootstrap failed on Unity {Application.unityVersion}: {exception.Message}");
            }
        }

        private static bool TryResolveHarmonyPatcherType(out Type patcherType)
        {
            patcherType = Type.GetType(HarmonyPatcherTypeName + ", " + HarmonyAssemblyName, false);
            if (patcherType != null)
                return true;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (!string.Equals(assembly.GetName().Name, HarmonyAssemblyName, StringComparison.Ordinal))
                    continue;

                patcherType = assembly.GetType(HarmonyPatcherTypeName, false);
                if (patcherType != null)
                    return true;
            }

            try
            {
                Assembly loaded = Assembly.Load(HarmonyAssemblyName);
                patcherType = loaded != null ? loaded.GetType(HarmonyPatcherTypeName, false) : null;
                return patcherType != null;
            }
            catch
            {
                patcherType = null;
                return false;
            }
        }

        private static void EnsureAssemblyLoadSubscription()
        {
            if (s_assemblyLoadSubscribed)
                return;

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoaded;
            s_assemblyLoadSubscribed = true;
        }

        private static void RemoveAssemblyLoadSubscription()
        {
            if (!s_assemblyLoadSubscribed)
                return;

            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoaded;
            s_assemblyLoadSubscribed = false;
        }

        private static void OnAssemblyLoaded(object sender, AssemblyLoadEventArgs args)
        {
            if (s_initialized)
            {
                RemoveAssemblyLoadSubscription();
                return;
            }

            if (args.LoadedAssembly == null)
                return;

            if (!string.Equals(args.LoadedAssembly.GetName().Name, HarmonyAssemblyName, StringComparison.Ordinal))
                return;

            TryInitializeHarmonyPatcher();
        }

        private static void LogWarningOnce(string key, string message)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
                return;

            if (!s_loggedWarnings.Add(key))
                return;

            Debug.LogWarning(message);
        }
    }
}
#endif
