using System.Collections.Generic;
using UnityEditor;
// Maintains a minimal editor update registration loop for active preview refresh and simulation work while avoiding unnecessary overhead.

namespace ParticleThumbnailAndPreview.Editor
{
    internal static class PreviewUpdateLoop
    {
        private static readonly HashSet<EditorApplication.CallbackFunction> RegisteredCallbacks = new();

        static PreviewUpdateLoop()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        internal static void EnsureRegistered(ref bool isRegistered, EditorApplication.CallbackFunction updateAction)
        {
            if (isRegistered || updateAction == null)
                return;

            EditorApplication.update -= updateAction;
            EditorApplication.update += updateAction;
            RegisteredCallbacks.Add(updateAction);
            isRegistered = true;
        }

        internal static void EnsureUnregistered(ref bool isRegistered, EditorApplication.CallbackFunction updateAction)
        {
            if (!isRegistered || updateAction == null)
                return;

            EditorApplication.update -= updateAction;
            RegisteredCallbacks.Remove(updateAction);
            isRegistered = false;
        }

        private static void OnBeforeAssemblyReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            if (RegisteredCallbacks.Count == 0)
                return;

            foreach (EditorApplication.CallbackFunction callback in RegisteredCallbacks)
                EditorApplication.update -= callback;

            RegisteredCallbacks.Clear();
        }
    }
}
