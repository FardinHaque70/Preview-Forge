using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
	// Tracks scriptable project settings so undo/redo persists them back to disk and refreshes dependent systems.
	internal static class ProjectSettingsUndoUtility
	{
		private sealed class TrackedSettingsAsset
		{
			internal ScriptableObject Storage;
			internal Action PersistAndNotify;
			internal string LastSnapshot;
		}

		private static readonly Dictionary<int, TrackedSettingsAsset> TrackedAssets = new Dictionary<int, TrackedSettingsAsset>();
		private static bool s_callbacksRegistered;

		internal static SerializedObject CreateSerializedObject(ScriptableObject storage, Action persistAndNotify)
		{
			Track(storage, persistAndNotify);

			SerializedObject serializedObject = new SerializedObject(storage);
			serializedObject.Update();
			return serializedObject;
		}

		internal static void ApplyModifiedProperties(SerializedObject serializedObject)
		{
			if (serializedObject == null || !serializedObject.ApplyModifiedProperties())
				return;

			ScriptableObject storage = serializedObject.targetObject as ScriptableObject;
			if (storage == null)
				return;

			PersistAndNotify(storage);
		}

		internal static void ResetToDefaultsWithUndo(
			ScriptableObject storage,
			string undoName,
			Action resetToDefaults,
			Action persistAndNotify)
		{
			if (storage == null || resetToDefaults == null || persistAndNotify == null)
				return;

			Track(storage, persistAndNotify);
			Undo.RegisterCompleteObjectUndo(storage, undoName);
			resetToDefaults();
			EditorUtility.SetDirty(storage);
			PersistAndNotify(storage);
		}

		private static void Track(ScriptableObject storage, Action persistAndNotify)
		{
			if (storage == null || persistAndNotify == null)
				return;

			RegisterCallbacks();

			int instanceId = storage.GetInstanceID();
			if (TrackedAssets.TryGetValue(instanceId, out TrackedSettingsAsset trackedAsset))
			{
				trackedAsset.PersistAndNotify = persistAndNotify;
				return;
			}

			TrackedAssets[instanceId] = new TrackedSettingsAsset
			{
				Storage = storage,
				PersistAndNotify = persistAndNotify,
				LastSnapshot = CaptureSnapshot(storage),
			};
		}

		private static void RegisterCallbacks()
		{
			if (s_callbacksRegistered)
				return;

			Undo.undoRedoPerformed += HandleUndoRedoPerformed;
			AssemblyReloadEvents.beforeAssemblyReload += UnregisterCallbacks;
			s_callbacksRegistered = true;
		}

		private static void UnregisterCallbacks()
		{
			if (!s_callbacksRegistered)
				return;

			Undo.undoRedoPerformed -= HandleUndoRedoPerformed;
			AssemblyReloadEvents.beforeAssemblyReload -= UnregisterCallbacks;
			TrackedAssets.Clear();
			s_callbacksRegistered = false;
		}

		private static void HandleUndoRedoPerformed()
		{
			if (TrackedAssets.Count == 0)
				return;

			List<int> staleKeys = null;
			foreach (KeyValuePair<int, TrackedSettingsAsset> entry in TrackedAssets)
			{
				TrackedSettingsAsset trackedAsset = entry.Value;
				if (trackedAsset.Storage == null)
				{
					if (staleKeys == null)
						staleKeys = new List<int>();

					staleKeys.Add(entry.Key);
					continue;
				}

				string currentSnapshot = CaptureSnapshot(trackedAsset.Storage);
				if (currentSnapshot == trackedAsset.LastSnapshot)
					continue;

				trackedAsset.PersistAndNotify?.Invoke();
				trackedAsset.LastSnapshot = CaptureSnapshot(trackedAsset.Storage);
			}

			if (staleKeys == null)
				return;

			for (int i = 0; i < staleKeys.Count; i++)
				TrackedAssets.Remove(staleKeys[i]);
		}

		private static void PersistAndNotify(ScriptableObject storage)
		{
			int instanceId = storage.GetInstanceID();
			if (!TrackedAssets.TryGetValue(instanceId, out TrackedSettingsAsset trackedAsset))
				return;

			trackedAsset.PersistAndNotify?.Invoke();
			trackedAsset.LastSnapshot = CaptureSnapshot(storage);
		}

		private static string CaptureSnapshot(ScriptableObject storage)
		{
			return EditorJsonUtility.ToJson(storage);
		}
	}
}
