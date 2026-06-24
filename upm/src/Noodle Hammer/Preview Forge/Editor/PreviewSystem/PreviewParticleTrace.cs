using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
	internal static class PreviewParticleTrace
	{
		internal static bool Enabled => false;
		internal static bool IntensityMapEnabled => false;

		internal static void Log(string source, string message)
		{
		}

		internal static void LogIntensityMap(string message)
		{
		}

		internal static string Asset(GameObject prefab)
		{
			if (prefab == null)
				return "<null>";

			string path = AssetDatabase.GetAssetPath(prefab);
			return string.IsNullOrEmpty(path) ? prefab.name : path;
		}
	}
}
