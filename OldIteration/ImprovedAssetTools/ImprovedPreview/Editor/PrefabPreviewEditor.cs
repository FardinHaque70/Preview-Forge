#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	/// <summary>
	/// Custom GameObject preview host that plugs into Unity's preview system without
	/// taking ownership of the GameObject inspector itself.
	///
	/// This keeps the normal inspector (or a third-party inspector such as Odin)
	/// in control, while exposing Improved Preview as a selectable preview option
	/// in Unity's preview dropdown when multiple previews are available.
	/// </summary>
	[CustomPreview(typeof(GameObject))]
	[CanEditMultipleObjects]
	public sealed class PrefabPreviewEditor : ObjectPreview
	{
		private static readonly Func<CustomPreviewBase>[] Factories =
		{
			() => new ParticlePrefabPreview(),
			() => new UIPrefabPreview(),
			() => new ModelPrefabPreview(),
			() => new NonVisualPrefabPreview(),
		};

		// Resolved once at class load time; null only if Unity renames the internal type.
		private static readonly System.Type s_InspectorWindowType =
			System.Type.GetType("UnityEditor.InspectorWindow, UnityEditor");

		private CustomPreviewBase _impl;
		private int _cachedImplTargetId;
		private string _cachedImplAssetPath;
		private bool _initialized;

		public override void Initialize(UnityEngine.Object[] targets)
		{
			base.Initialize(targets);
			_initialized = true;
			RebuildImplIfNeeded(force: false, reason: "Initialize");
		}

		public override void Cleanup()
		{
			DisableImpl();
			_initialized = false;
			base.Cleanup();
		}

		public override bool HasPreviewGUI()
		{
			if (!ImprovedPreviewSettings.Active)
				return false;

			if (!HasSingleValidTarget())
				return false;

			RebuildImplIfNeeded(force: false, reason: "HasPreviewGUI");
			return _impl != null;
		}

		public override GUIContent GetPreviewTitle()
		{
			return new GUIContent("Improved Preview");
		}

		public override void OnPreviewSettings()
		{
			if (!ImprovedPreviewSettings.Active || !HasSingleValidTarget())
				return;

			if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
				ImprovedPreviewSettings.SelectSettingsAsset();
		}

		public override void OnPreviewGUI(Rect r, GUIStyle background)
		{
			if (!ImprovedPreviewSettings.Active)
			{
				base.OnPreviewGUI(r, background);
				return;
			}

			if (!TryGetSingleTargetGameObject(out GameObject prefab))
			{
				base.OnPreviewGUI(r, background);
				return;
			}

			RebuildImplIfNeeded(force: false, reason: "OnPreviewGUI");
			if (_impl == null)
			{
				base.OnPreviewGUI(r, background);
				return;
			}

			_impl.OnPreviewGUI(r, prefab);
		}

		public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
		{
			if (!ImprovedPreviewSettings.Active)
			{
				base.OnInteractivePreviewGUI(r, background);
				return;
			}

			if (!TryGetSingleTargetGameObject(out GameObject prefab))
			{
				base.OnInteractivePreviewGUI(r, background);
				return;
			}

			RebuildImplIfNeeded(force: false, reason: "OnInteractivePreviewGUI");
			if (_impl == null)
			{
				base.OnInteractivePreviewGUI(r, background);
				return;
			}

			_impl.OnPreviewGUI(r, prefab);
		}

		public override string GetInfoString() => string.Empty;

		private void RebuildImplIfNeeded(bool force, string reason)
		{
			if (!_initialized)
				return;

			if (!TryGetSingleTargetGameObject(out GameObject prefab))
			{
				DisableImpl();
				return;
			}

			int targetId = prefab.GetInstanceID();
			string targetAssetPath = AssetDatabase.GetAssetPath(prefab);
			bool hasStableAssetPath = !string.IsNullOrEmpty(targetAssetPath);
			if (!force && _impl != null)
			{
				// Unity 6 can transiently swap the target object instance for the same
				// prefab asset during first inspector selection. Prefer asset-path
				// identity so we do not tear down and restart intro camera motion.
				if (hasStableAssetPath && string.Equals(_cachedImplAssetPath, targetAssetPath, StringComparison.Ordinal))
					return;

				if (!hasStableAssetPath && string.IsNullOrEmpty(_cachedImplAssetPath) && _cachedImplTargetId == targetId)
					return;
			}

			if (ImprovedPreviewSettings.EnableDiagnostics)
			{
				ImprovedPreviewSettings.LogDiagnostic(
					$"[PrefabPreviewEditor] Rebuild reason={reason ?? "unknown"} force={force} " +
					$"target='{prefab.name}' id={targetId} asset='{targetAssetPath}' " +
					$"cachedId={_cachedImplTargetId} cachedAsset='{_cachedImplAssetPath ?? "<null>"}' " +
					$"impl={(_impl != null ? _impl.GetType().Name : "<null>")}");
			}

			DisableImpl();

			foreach (Func<CustomPreviewBase> factory in Factories)
			{
				CustomPreviewBase candidate = factory();
				if (!ImprovedPreviewSettings.IsPreviewTypeEnabled(candidate.PreviewTypeKey))
					continue;

				if (!candidate.Supports(prefab))
					continue;

				_impl = candidate;
				_impl.Enable(RequestPreviewRepaint);
				_cachedImplTargetId = targetId;
				_cachedImplAssetPath = hasStableAssetPath ? targetAssetPath : null;
				if (ImprovedPreviewSettings.EnableDiagnostics)
				{
					ImprovedPreviewSettings.LogDiagnostic(
						$"[PrefabPreviewEditor] Selected impl={_impl.GetType().Name} " +
						$"previewType={_impl.PreviewTypeKey} target='{prefab.name}' asset='{targetAssetPath}'");
				}
				return;
			}
		}

		private void DisableImpl()
		{
			if (ImprovedPreviewSettings.EnableDiagnostics && _impl != null)
				ImprovedPreviewSettings.LogDiagnostic($"[PrefabPreviewEditor] Disable impl={_impl.GetType().Name}");

			if (_impl != null)
			{
				_impl.Disable();
				_impl = null;
			}

			_cachedImplTargetId = 0;
			_cachedImplAssetPath = null;
		}

		private bool HasSingleValidTarget()
		{
			if (m_Targets == null || m_Targets.Length != 1)
				return false;

			return m_Targets[0] != null;
		}

		private bool TryGetSingleTargetGameObject(out GameObject prefab)
		{
			prefab = null;
			if (!HasSingleValidTarget())
				return false;

			prefab = target as GameObject;
			return prefab != null;
		}

		private void RequestPreviewRepaint()
		{
			// Repaint only Inspector windows — not Scene, Game, Console, Project, etc.
			// This replaces InternalEditorUtility.RepaintAllViews() which flushed every
			// open Unity window on every playback tick and starved input in other windows.
			//
			// s_InspectorWindowType is resolved once via reflection at class load time.
			// There is usually only one Inspector window open; iterating the small list
			// is negligible. Repainting the Inspector triggers OnPreviewGUI which in turn
			// advances the simulation and renders the next frame.
			if (s_InspectorWindowType != null)
			{
				UnityEngine.Object[] inspectors = Resources.FindObjectsOfTypeAll(s_InspectorWindowType);
				if (inspectors.Length > 0)
				{
					foreach (UnityEngine.Object w in inspectors)
						(w as EditorWindow)?.Repaint();
					return;
				}
			}

			// Fallback: inspector type not found (should not happen in normal use).
			InternalEditorUtility.RepaintAllViews();
		}
	}
}
#endif
