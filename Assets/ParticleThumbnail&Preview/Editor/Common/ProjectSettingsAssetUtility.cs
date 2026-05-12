using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ParticleThumbnailAndPreview.Editor
{
	// Loads project-owned settings assets from Assets so Asset Store and UPM installs share the same writable settings location.
	internal static class ProjectSettingsAssetUtility
	{
		internal static T LoadOrCreate<T>(string assetPath, Action<T> initialize) where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
			if (asset != null)
				return asset;

			EnsureAssetFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
			bool replaceExistingAsset = File.Exists(assetPath);
			asset = ScriptableObject.CreateInstance<T>();
			initialize?.Invoke(asset);
			if (replaceExistingAsset)
				AssetDatabase.DeleteAsset(assetPath);

			AssetDatabase.CreateAsset(asset, assetPath);
			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
			return asset;
		}

		internal static void Save(UnityObject asset)
		{
			if (asset == null)
				return;

			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
		}

		internal static bool TryReadBool(string legacyPath, string key, out bool value)
		{
			value = false;
			if (!TryReadValue(legacyPath, key, out string raw))
				return false;

			if (raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase))
			{
				value = true;
				return true;
			}

			if (raw == "0" || string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase))
			{
				value = false;
				return true;
			}

			return false;
		}

		internal static bool TryReadInt(string legacyPath, string key, out int value)
		{
			value = 0;
			return TryReadValue(legacyPath, key, out string raw)
			       && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		internal static bool TryReadFloat(string legacyPath, string key, out float value)
		{
			value = 0f;
			return TryReadValue(legacyPath, key, out string raw)
			       && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}

		internal static bool TryReadColor(string legacyPath, string key, out Color value)
		{
			value = default;
			if (!TryReadValue(legacyPath, key, out string raw))
				return false;

			return TryReadNamedFloat(raw, "r", out value.r)
			       && TryReadNamedFloat(raw, "g", out value.g)
			       && TryReadNamedFloat(raw, "b", out value.b)
			       && TryReadNamedFloat(raw, "a", out value.a);
		}

		internal static bool TryReadVector2(string legacyPath, string key, out Vector2 value)
		{
			value = default;
			if (!TryReadValue(legacyPath, key, out string raw))
				return false;

			return TryReadNamedFloat(raw, "x", out value.x)
			       && TryReadNamedFloat(raw, "y", out value.y);
		}

		internal static bool TryReadObject<T>(string legacyPath, string key, out T value) where T : UnityObject
		{
			value = null;
			if (!TryReadValue(legacyPath, key, out string raw))
				return false;

			if (!TryReadNamedString(raw, "guid", out string guid) || string.IsNullOrEmpty(guid))
				return false;

			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(assetPath))
				return false;

			value = AssetDatabase.LoadAssetAtPath<T>(assetPath);
			return value != null;
		}

		private static void EnsureAssetFolder(string folderPath)
		{
			if (string.IsNullOrEmpty(folderPath))
				return;

			folderPath = folderPath.Replace('\\', '/');
			if (AssetDatabase.IsValidFolder(folderPath))
				return;

			if (Directory.Exists(folderPath))
			{
				AssetDatabase.ImportAsset(folderPath);
				if (AssetDatabase.IsValidFolder(folderPath))
					return;
			}

			string[] parts = folderPath.Split('/');
			string current = parts[0];
			for (int i = 1; i < parts.Length; i++)
			{
				string next = current + "/" + parts[i];
				if (AssetDatabase.IsValidFolder(next))
				{
					current = next;
					continue;
				}

				if (Directory.Exists(next))
				{
					AssetDatabase.ImportAsset(next);
					current = next;
					continue;
				}

				string createdGuid = AssetDatabase.CreateFolder(current, parts[i]);
				string createdPath = AssetDatabase.GUIDToAssetPath(createdGuid);
				if (!string.IsNullOrEmpty(createdPath))
					next = createdPath;

				current = next;
			}
		}

		private static bool TryReadValue(string legacyPath, string key, out string value)
		{
			value = null;
			if (!File.Exists(legacyPath))
				return false;

			string prefix = key + ":";
			string[] lines = File.ReadAllLines(legacyPath);
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (!line.StartsWith(prefix, StringComparison.Ordinal))
					continue;

				value = line.Substring(prefix.Length).Trim();
				return true;
			}

			return false;
		}

		private static bool TryReadNamedFloat(string raw, string name, out float value)
		{
			value = 0f;
			return TryReadNamedString(raw, name, out string rawValue)
			       && float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
		}

		private static bool TryReadNamedString(string raw, string name, out string value)
		{
			value = null;
			string token = name + ":";
			int tokenIndex = raw.IndexOf(token, StringComparison.Ordinal);
			if (tokenIndex < 0)
				return false;

			int valueStart = tokenIndex + token.Length;
			while (valueStart < raw.Length && char.IsWhiteSpace(raw[valueStart]))
				valueStart++;

			int valueEnd = raw.IndexOf(',', valueStart);
			if (valueEnd < 0)
			{
				valueEnd = raw.IndexOf('}', valueStart);
				if (valueEnd < 0)
					valueEnd = raw.Length;
			}

			value = raw.Substring(valueStart, valueEnd - valueStart).Trim();
			return true;
		}
	}
}
