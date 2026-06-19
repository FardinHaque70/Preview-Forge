using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
	internal static class PreviewParticleTrace
	{
		internal static bool Enabled => PreviewSettings.EnableDiagnostics;
		internal static bool IntensityMapEnabled => PreviewSettings.EnableDiagnostics;

		internal static void Log(string source, string message)
		{
			if (!Enabled)
				return;

			Debug.Log($"[PreviewForge][ParticleTrace] frame={Time.frameCount} t={EditorApplication.timeSinceStartup:F3} source={source} {message}");
		}

		internal static void LogIntensityMap(string message)
		{
			if (!IntensityMapEnabled)
				return;

			Debug.Log($"[PreviewForge][ParticleTrace] frame={Time.frameCount} t={EditorApplication.timeSinceStartup:F3} source=ParticleIntensityMap {message}");
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
