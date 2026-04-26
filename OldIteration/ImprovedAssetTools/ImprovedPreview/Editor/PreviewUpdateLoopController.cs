#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	internal static class PreviewUpdateLoopController
	{
		public static void Start(ref bool isRegistered, ref double lastUpdateTime, EditorApplication.CallbackFunction updateHandler)
		{
			if (isRegistered || updateHandler == null)
				return;

			isRegistered = true;
			lastUpdateTime = -1d;
			EditorApplication.update -= updateHandler;
			EditorApplication.update += updateHandler;
		}

		public static void Stop(ref bool isRegistered, ref double lastUpdateTime, EditorApplication.CallbackFunction updateHandler)
		{
			if (!isRegistered || updateHandler == null)
				return;

			EditorApplication.update -= updateHandler;
			isRegistered = false;
			lastUpdateTime = -1d;
		}

		public static bool TryGetDeterministicDeltaTime(
			ref double lastUpdateTime,
			double now,
			float maxDeltaSeconds,
			out float deltaTime)
		{
			deltaTime = 0f;
			if (lastUpdateTime < 0d)
			{
				lastUpdateTime = now;
				return false;
			}

			deltaTime = Mathf.Clamp((float)(now - lastUpdateTime), 0f, maxDeltaSeconds);
			lastUpdateTime = now;
			return deltaTime > 0f;
		}
	}
}
#endif
