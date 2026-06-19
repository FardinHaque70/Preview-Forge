using System;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class PreviewPropertyEditorCache
    {
        private const double CacheLifetimeSeconds = 0.25d;

        private static Type s_cachedType;
        private static UnityObject[] s_cachedEditors = Array.Empty<UnityObject>();
        private static double s_cachedAt = -1d;
        private static bool s_dirty = true;

        static PreviewPropertyEditorCache()
        {
            Selection.selectionChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            PreviewSettings.SettingsChanged -= Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;

            Selection.selectionChanged += Invalidate;
            EditorApplication.projectChanged += Invalidate;
            PreviewSettings.SettingsChanged += Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        internal static UnityObject[] GetOpenPropertyEditors(Type propertyEditorType)
        {
            if (propertyEditorType == null)
                return Array.Empty<UnityObject>();

            double now = EditorApplication.timeSinceStartup;
            bool expired = s_cachedAt < 0d || now - s_cachedAt > CacheLifetimeSeconds;
            if (s_dirty || expired || s_cachedType != propertyEditorType)
            {
                s_cachedType = propertyEditorType;
                s_cachedEditors = Resources.FindObjectsOfTypeAll(propertyEditorType) ?? Array.Empty<UnityObject>();
                s_cachedAt = now;
                s_dirty = false;
            }

            return s_cachedEditors;
        }

        internal static void Invalidate()
        {
            s_dirty = true;
            s_cachedAt = -1d;
            s_cachedEditors = Array.Empty<UnityObject>();
        }

        private static void OnBeforeAssemblyReload()
        {
            Selection.selectionChanged -= Invalidate;
            EditorApplication.projectChanged -= Invalidate;
            PreviewSettings.SettingsChanged -= Invalidate;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            Invalidate();
        }
    }
}
