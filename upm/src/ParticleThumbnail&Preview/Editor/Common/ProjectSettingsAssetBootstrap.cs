using UnityEditor;

namespace ParticleThumbnailAndPreview.Editor
{
	[InitializeOnLoad]
	internal static class ProjectSettingsAssetBootstrap
	{
		static ProjectSettingsAssetBootstrap()
		{
			EnsureSettingsAssets();
			EditorApplication.delayCall += EnsureSettingsAssets;
		}

		private static void EnsureSettingsAssets()
		{
			EditorApplication.delayCall -= EnsureSettingsAssets;

			_ = PreviewSettingsStorage.instance;
			_ = ParticleThumbnailSettingsStorage.instance;
		}
	}
}
