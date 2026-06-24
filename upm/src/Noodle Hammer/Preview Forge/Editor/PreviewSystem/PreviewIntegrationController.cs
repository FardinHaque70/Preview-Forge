using UnityEditor;

namespace NoodleHammer.PreviewForge.Editor
{
	[InitializeOnLoad]
	internal static class PreviewIntegrationController
	{
		private static bool s_applyRetryQueued;

		static PreviewIntegrationController()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
			AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
			PreviewSettings.SettingsChanged -= OnSettingsChanged;
			PreviewSettings.SettingsChanged += OnSettingsChanged;
			ApplyBuiltInFallbackIfNeeded();
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
			{
				PreviewHarmonyPatcher.Unpatch();
				return;
			}

			ApplyBuiltInFallbackIfNeeded();
		}

		private static void ApplyBuiltInFallbackIfNeeded()
		{
			if (!PreviewSettings.AnyPrefabCustomPreviewActive)
				return;

			if (PreviewEditorTransitionGuard.IsUnsafeTransition())
			{
				QueueApplyRetry();
				return;
			}

			PreviewHarmonyPatcher.TryApplyBuiltInPreviewFallback();
		}

		private static void QueueApplyRetry()
		{
			if (s_applyRetryQueued)
				return;

			s_applyRetryQueued = true;
			EditorApplication.delayCall += RetryApplyBuiltInFallback;
		}

		private static void RetryApplyBuiltInFallback()
		{
			s_applyRetryQueued = false;
			ApplyBuiltInFallbackIfNeeded();
		}
	}
}
