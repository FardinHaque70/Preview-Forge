using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
	// Dormant fallback for Unity built-in preview edge cases. The normal path is Unity's CustomPreview system.
	internal static class PreviewHarmonyPatcher
	{
		private const string HarmonyId = "com.noodlehammer.preview-forge.preview";
		private const string HarmonyTypeName = "HarmonyLib.Harmony";
		private const string HarmonyMethodTypeName = "HarmonyLib.HarmonyMethod";
		private const string HarmonyAssemblyName = "0Harmony";
		private const string HarmonyRelativePath = "Editor/PreviewSystem/Plugins/ThirdParty/0Harmony.dll";
		private const string CompatibilityRemediationHint =
			" Preview Forge will continue using Unity's standard custom preview path and will not suppress third-party preview providers.";

		private static readonly HashSet<string> LoggedWarnings = new();
		private static bool _patchesApplied;
		private static bool _harmonyRuntimeUnsupported;

		private static object _harmonyInstance;
		private static Type _harmonyType;
		private static Type _harmonyMethodType;
		private static MethodInfo _harmonyPatchMethod;
		private static MethodInfo _harmonyUnpatchAllMethod;
		private static ConstructorInfo _harmonyMethodCtor;
		private static ConstructorInfo _harmonyMethodCtorFromTypeName;

		internal static bool IsActive => _patchesApplied;
		internal static bool IsRuntimeUnsupported => _harmonyRuntimeUnsupported;

		internal static bool TryApplyBuiltInPreviewFallback()
		{
			if (_patchesApplied)
				return true;

			if (_harmonyRuntimeUnsupported || PreviewEditorTransitionGuard.IsUnsafeTransition())
				return false;

			try
			{
				if (!TryInitializeHarmonyApi())
					return false;

				Type gameObjectInspectorType = PreviewForgeEditorCompatibility.ResolveEditorType("UnityEditor.GameObjectInspector");
				MethodInfo hasPreviewGuiMethod = PreviewForgeEditorCompatibility.GetInstanceMethod(
					gameObjectInspectorType,
					"HasPreviewGUI",
					Type.EmptyTypes);

				if (hasPreviewGuiMethod == null)
				{
					LogWarningOnce(
						"missing-gameobjectinspector-haspreview",
						$"[PreviewForge] GameObjectInspector.HasPreviewGUI was not found in Unity {Application.unityVersion}; fallback patch skipped.{CompatibilityRemediationHint}");
					return false;
				}

				if (!TryPatchWithPrefix(hasPreviewGuiMethod, nameof(GameObjectInspectorHasPreviewPrefix)))
					return false;

				AssemblyReloadEvents.beforeAssemblyReload -= Unpatch;
				AssemblyReloadEvents.beforeAssemblyReload += Unpatch;
				_patchesApplied = true;
				return true;
			}
			catch (Exception exception)
			{
				LogWarningOnce("patch-apply-failed", $"[PreviewForge] Harmony fallback patch failed: {DescribeException(exception)}{CompatibilityRemediationHint}");
				return false;
			}
		}

		internal static void Unpatch()
		{
			AssemblyReloadEvents.beforeAssemblyReload -= Unpatch;
			TryUnpatchHarmony();
			_harmonyInstance = null;
			_patchesApplied = false;
		}

		private static bool TryInitializeHarmonyApi()
		{
			if (_harmonyInstance != null && _harmonyPatchMethod != null)
				return true;

			_harmonyType = ResolveHarmonyType(HarmonyTypeName);
			_harmonyMethodType = ResolveHarmonyType(HarmonyMethodTypeName);
			if (_harmonyType == null || _harmonyMethodType == null)
				return false;

			_harmonyMethodCtor = _harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
			_harmonyMethodCtorFromTypeName = _harmonyMethodType.GetConstructor(new[] { typeof(Type), typeof(string), typeof(Type[]) });
			_harmonyPatchMethod = _harmonyType.GetMethod(
				"Patch",
				BindingFlags.Instance | BindingFlags.Public,
				null,
				new[] { typeof(MethodBase), _harmonyMethodType, _harmonyMethodType, _harmonyMethodType, _harmonyMethodType },
				null);
			_harmonyUnpatchAllMethod = ResolveHarmonyUnpatchAllMethod();

			ConstructorInfo harmonyCtor = _harmonyType.GetConstructor(new[] { typeof(string) });
			if ((_harmonyMethodCtor == null && _harmonyMethodCtorFromTypeName == null)
				|| _harmonyPatchMethod == null
				|| _harmonyUnpatchAllMethod == null
				|| harmonyCtor == null)
			{
				LogWarningOnce("harmony-api-mismatch", "[PreviewForge] Harmony API surface does not match expected signatures.");
				return false;
			}

			_harmonyInstance = harmonyCtor.Invoke(new object[] { HarmonyId });
			return true;
		}

		private static MethodInfo ResolveHarmonyUnpatchAllMethod()
		{
			if (_harmonyType == null)
				return null;

			MethodInfo[] methods = _harmonyType.GetMethods(
				BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			for (int i = 0; i < methods.Length; i++)
			{
				MethodInfo method = methods[i];
				if (method == null || !string.Equals(method.Name, "UnpatchAll", StringComparison.Ordinal))
					continue;

				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length == 0)
					return method;

				if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
					return method;
			}

			return null;
		}

		private static void TryUnpatchHarmony()
		{
			if (_harmonyUnpatchAllMethod == null)
				return;

			try
			{
				ParameterInfo[] parameters = _harmonyUnpatchAllMethod.GetParameters();
				object[] args;
				if (parameters.Length == 0)
				{
					args = Array.Empty<object>();
				}
				else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
				{
					args = new object[] { HarmonyId };
				}
				else
				{
					return;
				}

				object target = _harmonyUnpatchAllMethod.IsStatic ? null : _harmonyInstance;
				if (!_harmonyUnpatchAllMethod.IsStatic && target == null)
					return;

				_harmonyUnpatchAllMethod.Invoke(target, args);
			}
			catch (Exception exception)
			{
				LogWarningOnce("unpatch-failed", $"[PreviewForge] Harmony unpatch failed: {DescribeException(exception)}");
			}
		}

		private static Type ResolveHarmonyType(string fullTypeName)
		{
			Type type = Type.GetType(fullTypeName + ", " + HarmonyAssemblyName, false);
			if (type != null)
				return type;

			try
			{
				string fullPath = TryResolveHarmonyAssemblyPath();
				if (string.IsNullOrEmpty(fullPath))
					return null;

				Assembly assembly = PreviewForgeEditorCompatibility.LoadAssemblyFromPath(fullPath);
				return assembly?.GetType(fullTypeName, false);
			}
			catch (Exception exception)
			{
				LogWarningOnce("harmony-load-failed", $"[PreviewForge] Failed to load Harmony assembly: {exception.Message}");
				return null;
			}
		}

		private static string TryResolveHarmonyAssemblyPath()
		{
			string installPath = PreviewInstallLayout.TryResolveExistingAbsolutePath(HarmonyRelativePath);
			if (!string.IsNullOrEmpty(installPath))
				return installPath;

			string[] harmonyGuids = AssetDatabase.FindAssets("0Harmony");
			for (int i = 0; i < harmonyGuids.Length; i++)
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(harmonyGuids[i]);
				if (string.IsNullOrEmpty(assetPath)
					|| !assetPath.EndsWith("/0Harmony.dll", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				bool isKnownToolPath =
					assetPath.IndexOf("/Noodle Hammer/Preview Forge/", StringComparison.OrdinalIgnoreCase) >= 0
					|| assetPath.IndexOf("/com.noodlehammer.preview-forge/", StringComparison.OrdinalIgnoreCase) >= 0;

				if (isKnownToolPath)
				{
					string fullPath = Path.GetFullPath(assetPath);
					if (File.Exists(fullPath))
						return fullPath;
				}
			}

			return null;
		}

		private static bool TryPatchWithPrefix(MethodInfo targetMethod, string prefixMethodName)
		{
			if (targetMethod == null || string.IsNullOrEmpty(prefixMethodName) || _harmonyPatchMethod == null)
				return false;

			MethodInfo prefixMethod = typeof(PreviewHarmonyPatcher).GetMethod(
				prefixMethodName,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (prefixMethod == null)
				return false;

			try
			{
				object prefix = CreateHarmonyMethod(prefixMethod, prefixMethodName);
				if (prefix == null)
					return false;

				_harmonyPatchMethod.Invoke(
					_harmonyInstance,
					new[] { (object)targetMethod, prefix, null, null, null });
				return true;
			}
			catch (Exception patchException)
			{
				if (IsRuntimeDetourBlocked(patchException))
				{
					_harmonyRuntimeUnsupported = true;
					LogWarningOnce(
						"harmony-runtime-detour-blocked",
						$"[PreviewForge] Harmony runtime detours are blocked on this editor runtime. Falling back to Unity's standard custom preview path.{CompatibilityRemediationHint}");
					return false;
				}

				LogWarningOnce(
					"patch-method-failed-" + targetMethod.DeclaringType?.FullName + "." + targetMethod.Name,
					$"[PreviewForge] Failed to patch '{targetMethod.DeclaringType?.FullName}.{targetMethod.Name}': {DescribeException(patchException)}{CompatibilityRemediationHint}");
				return false;
			}
		}

		private static object CreateHarmonyMethod(MethodInfo methodInfo, string methodName)
		{
			try
			{
				if (_harmonyMethodCtorFromTypeName != null)
					return _harmonyMethodCtorFromTypeName.Invoke(new object[] { typeof(PreviewHarmonyPatcher), methodName, null });

				if (_harmonyMethodCtor != null)
					return _harmonyMethodCtor.Invoke(new object[] { methodInfo });
			}
			catch (Exception exception)
			{
				LogWarningOnce(
					"create-harmonymethod-failed-" + methodName,
					$"[PreviewForge] Failed to create HarmonyMethod for '{methodName}': {DescribeException(exception)}");
			}

			return null;
		}

		private static bool GameObjectInspectorHasPreviewPrefix(UnityEditor.Editor __instance, ref bool __result)
		{
			if (PreviewEditorTransitionGuard.IsUnsafeTransition())
				return true;

			try
			{
				if (!PrefabPreviewTargetGate.ShouldSuppressCompetingPreview(__instance != null ? __instance.targets : null))
					return true;

				__result = false;
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static void LogWarningOnce(string key, string message)
		{
			if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
				return;

			if (!LoggedWarnings.Add(key))
				return;

			Debug.LogWarning(message);
		}

		private static string DescribeException(Exception exception)
		{
			if (exception == null)
				return "Unknown exception";

			Exception current = exception;
			while (current is TargetInvocationException tie && tie.InnerException != null)
				current = tie.InnerException;

			return $"{current.GetType().Name}: {current.Message}\n{current}";
		}

		private static bool IsRuntimeDetourBlocked(Exception exception)
		{
			Exception current = exception;
			while (current != null)
			{
				string message = current.Message;
				if (!string.IsNullOrEmpty(message)
					&& message.IndexOf("mprotect returned EACCES", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}

				current = current.InnerException;
			}

			return false;
		}
	}
}
