using UnityEditor;

namespace NoodleHammer.PreviewForge.Editor
{
	[InitializeOnLoad]
	internal static class PreviewIntegrationController
	{
		static PreviewIntegrationController()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			PreviewSettings.SettingsChanged -= OnSettingsChanged;
			PreviewSettings.SettingsChanged += OnSettingsChanged;
		}

		private static void OnBeforeAssemblyReload()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			PreviewSettings.SettingsChanged -= OnSettingsChanged;
			PreviewHarmonyPatcher.Unpatch();
		}

		private static void OnSettingsChanged()
		{
			if (!PreviewSettings.AnyPrefabCustomPreviewActive)
				PreviewHarmonyPatcher.Unpatch();
		}
	}
}
