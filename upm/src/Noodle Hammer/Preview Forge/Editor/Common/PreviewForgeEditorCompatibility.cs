using System;
using System.Reflection;

namespace NoodleHammer.PreviewForge.Editor
{
	internal static class PreviewForgeEditorCompatibility
	{
		private static readonly string[] EditorAssemblyPreferenceOrder =
		{
			"UnityEditor.CoreModule",
			"UnityEditor",
		};

		private static readonly Type CurrentAssembliesType =
			Type.GetType("UnityEngine.Assemblies.CurrentAssemblies, UnityEngine.CoreModule", false);

		private static readonly MethodInfo GetLoadedAssembliesMethod =
			CurrentAssembliesType?.GetMethod("GetLoadedAssemblies", BindingFlags.Static | BindingFlags.Public);

		private static readonly MethodInfo LoadFromPathMethod =
			CurrentAssembliesType?.GetMethod("LoadFromPath", BindingFlags.Static | BindingFlags.Public);

		internal static Type ResolveEditorType(string fullTypeName)
		{
			if (string.IsNullOrEmpty(fullTypeName))
				return null;

			for (int i = 0; i < EditorAssemblyPreferenceOrder.Length; i++)
			{
				Type preferred = Type.GetType(fullTypeName + ", " + EditorAssemblyPreferenceOrder[i], false);
				if (preferred != null)
					return preferred;
			}

			Assembly[] assemblies = GetLoadedAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				Assembly assembly = assemblies[i];
				if (assembly == null)
					continue;

				try
				{
					Type resolved = assembly.GetType(fullTypeName, false);
					if (resolved != null)
						return resolved;
				}
				catch
				{
					// Unity 6.5+ can keep unloaded assemblies around in .NET APIs;
					// skip assemblies that cannot safely answer reflection queries.
				}
			}

			return null;
		}

		internal static Assembly LoadAssemblyFromPath(string fullPath)
		{
			if (string.IsNullOrEmpty(fullPath))
				return null;

			if (LoadFromPathMethod != null)
			{
				try
				{
					return LoadFromPathMethod.Invoke(null, new object[] { fullPath }) as Assembly;
				}
				catch
				{
					// Fall back for older Unity versions or unexpected beta API changes.
				}
			}

			return Assembly.LoadFrom(fullPath);
		}

		internal static MethodInfo GetInstanceMethod(Type type, string name)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			return type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		internal static MethodInfo GetInstanceMethod(Type type, string name, Type[] parameterTypes)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			return type.GetMethod(
				name,
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				parameterTypes ?? Type.EmptyTypes,
				null);
		}

		internal static MethodInfo GetStaticMethod(Type type, string name, Type[] parameterTypes = null)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			return type.GetMethod(
				name,
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
				null,
				parameterTypes ?? Type.EmptyTypes,
				null);
		}

		internal static ulong GetObjectId(UnityEngine.Object unityObject)
		{
			if (unityObject == null)
				return 0UL;

#if UNITY_6000_5_OR_NEWER
			return UnityEngine.EntityId.ToULong(unityObject.GetEntityId());
#else
			return unchecked((ulong) unityObject.GetInstanceID());
#endif
		}

		internal static FieldInfo GetInstanceField(Type type, string name)
		{
			if (type == null || string.IsNullOrEmpty(name))
				return null;

			return type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		private static Assembly[] GetLoadedAssemblies()
		{
			if (GetLoadedAssembliesMethod != null)
			{
				try
				{
					if (GetLoadedAssembliesMethod.Invoke(null, null) is System.Collections.IEnumerable enumerable)
					{
						var assemblies = new System.Collections.Generic.List<Assembly>();
						foreach (object item in enumerable)
						{
							if (item is Assembly assembly)
								assemblies.Add(assembly);
						}

						return assemblies.ToArray();
					}
				}
				catch
				{
					// Fall back to AppDomain on older or transitional Unity versions.
				}
			}

			return AppDomain.CurrentDomain.GetAssemblies();
		}
	}
}
