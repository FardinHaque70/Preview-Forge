#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	public sealed class UIPrefabPreview : CustomPreviewBase
	{
		private static readonly Type s_graphicType = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI");
		private static readonly Type s_tmpUiType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
		private static readonly Type s_tmpWorldType = Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");

		private readonly List<Graphic> _graphics = new();
		private readonly List<Canvas> _canvases = new();
		private readonly List<Renderer> _renderers = new();
		private readonly List<bool> _renderersInitiallyEnabled = new();

		private Canvas _syntheticCanvas;
		private RectTransform _contentRoot;
		private bool _hasFramedBounds;
		private Bounds _framedBounds;
		private bool _showStats = true;
		private string[] _statsLines;

		public override PreviewAssetTypeKey PreviewTypeKey => PreviewAssetTypeKey.UiPrefab;

		public override bool Supports(GameObject prefab)
		{
			if (prefab == null) return false;
			if (!EditorUtility.IsPersistent(prefab)) return false;
			if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) return false;

			return HasUiRenderableContent(prefab);
		}

		protected override bool ShouldDrawSharedToolbarInPreview() => false;
		protected override bool ShouldUse2DCompatibilityMode() => true;
		protected override bool ShouldForce2DCompatibilityMode() => true;

		// We want the same preview background + XY grid in UI previews.
		protected override bool ShouldDrawGridIn2DMode() => true;
		protected override bool ShouldShowLightsToolbarButton() => false;
		protected override bool SupportsPreviewLightRig() => false;
		protected override bool ShouldShowSkyboxToolbarButton() => false;
		protected override bool ShouldShowGridToolbarButton() => false;
		protected override Color? GetRenderBackgroundColorOverride() => ImprovedPreviewSettings.BgColor;

		protected override void OnSetup(GameObject prefab)
		{
			_graphics.Clear();
			_canvases.Clear();
			_renderers.Clear();
			_renderersInitiallyEnabled.Clear();
			_statsLines = null;
			_hasFramedBounds = false;
			_syntheticCanvas = null;
			SetSkyboxEnabled(false);

			_contentRoot = PreviewRoot.GetComponent<RectTransform>();
			if (_contentRoot == null)
				_contentRoot = PreviewRoot.GetComponentInChildren<RectTransform>(true);

			// Make whole hierarchy preview-only.
			SetLayerRecursively(PreviewRoot, PreviewLayer);

			RefreshUiComponentCaches();
			EnsureCanvasForLooseUiElement();
			RefreshUiComponentCaches();

			NormalizeAllCanvasesForPreview();
			CenterContentRoot();
			ForceUiRefresh();

			AutoFrame();
			ComputeStats();

			// Match the rest of the preview system: only enable while rendering.
			for (int i = 0; i < _renderers.Count; i++)
			{
				if (_renderers[i] != null)
					_renderers[i].enabled = false;
			}
		}

		protected override void OnCleanup()
		{
			if (_syntheticCanvas != null)
			{
				UnityEngine.Object.DestroyImmediate(_syntheticCanvas.gameObject);
				_syntheticCanvas = null;
			}

			_graphics.Clear();
			_canvases.Clear();
			_renderers.Clear();
			_renderersInitiallyEnabled.Clear();
			_contentRoot = null;
			_statsLines = null;
			_hasFramedBounds = false;
		}

		protected override void OnBeforeRender()
		{
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;

				if (i < _renderersInitiallyEnabled.Count && _renderersInitiallyEnabled[i])
					renderer.enabled = true;
			}

			ForceUiRefresh();
		}

		protected override void OnAfterRender()
		{
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;

				if (i < _renderersInitiallyEnabled.Count && _renderersInitiallyEnabled[i])
					renderer.enabled = false;
			}
		}

		protected override void DrawExtraToolbar(ref Rect previewRect)
		{ }

		protected override void DrawOverlay(Rect previewRect)
		{
			if (!_showStats || _statsLines == null || _statsLines.Length == 0)
				return;

			const float padding = 8f;
			const float lineHeight = 14f;

			GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
			{
				fontSize = 10,
				alignment = TextAnchor.MiddleLeft,
				normal = {textColor = new Color(0.88f, 0.88f, 0.88f, 1f)}
			};

			float startY = previewRect.yMax - padding - _statsLines.Length * lineHeight;
			for (int i = 0; i < _statsLines.Length; i++)
			{
				Rect lineRect = new Rect(previewRect.x + padding, startY, previewRect.width - padding * 2f, lineHeight);
				GUI.Label(lineRect, _statsLines[i], style);
				startY += lineHeight;
			}
		}

		private void EnsureCanvasForLooseUiElement()
		{
			bool hasCanvas = false;
			for (int i = 0; i < _canvases.Count; i++)
			{
				if (_canvases[i] != null)
				{
					hasCanvas = true;
					break;
				}
			}

			if (hasCanvas || _contentRoot == null)
				return;

			GameObject canvasGo = new GameObject("___UIPreviewCanvas___")
			{
				hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable,
				layer = PreviewLayer
			};

			RectTransform canvasRect = canvasGo.AddComponent<RectTransform>();
			canvasRect.position = WorldOffset;
			canvasRect.rotation = Quaternion.identity;
			canvasRect.localScale = Vector3.one * 0.01f;
			canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
			canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
			canvasRect.pivot = new Vector2(0.5f, 0.5f);
			canvasRect.sizeDelta = new Vector2(1920f, 1080f);

			_syntheticCanvas = canvasGo.AddComponent<Canvas>();
			_syntheticCanvas.renderMode = RenderMode.WorldSpace;
			_syntheticCanvas.worldCamera = PreviewCam;
			_syntheticCanvas.planeDistance = 1f;
			_syntheticCanvas.sortingOrder = 0;
			_syntheticCanvas.overrideSorting = false;

			canvasGo.AddComponent<CanvasScaler>();
			canvasGo.AddComponent<GraphicRaycaster>();

			_contentRoot.SetParent(canvasRect, true);

			// Center loose element so its anchored offsets do not throw framing off.
			_contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
			_contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
			_contentRoot.pivot = new Vector2(0.5f, 0.5f);
			_contentRoot.anchoredPosition3D = Vector3.zero;
			_contentRoot.localRotation = Quaternion.identity;
			_contentRoot.localScale = Vector3.one;

			SetLayerRecursively(canvasGo, PreviewLayer);
		}

		private void RefreshUiComponentCaches()
		{
			_graphics.Clear();
			_canvases.Clear();
			_renderers.Clear();
			_renderersInitiallyEnabled.Clear();

			PreviewRoot.GetComponentsInChildren(true, _graphics);
			PreviewRoot.GetComponentsInChildren(true, _canvases);
			PreviewRoot.GetComponentsInChildren(true, _renderers);

			if (_syntheticCanvas != null)
			{
				if (!_canvases.Contains(_syntheticCanvas))
					_canvases.Add(_syntheticCanvas);

				Graphic[] syntheticGraphics = _syntheticCanvas.GetComponentsInChildren<Graphic>(true);
				for (int i = 0; i < syntheticGraphics.Length; i++)
				{
					if (!_graphics.Contains(syntheticGraphics[i]))
						_graphics.Add(syntheticGraphics[i]);
				}

				Renderer[] syntheticRenderers = _syntheticCanvas.GetComponentsInChildren<Renderer>(true);
				for (int i = 0; i < syntheticRenderers.Length; i++)
				{
					if (!_renderers.Contains(syntheticRenderers[i]))
						_renderers.Add(syntheticRenderers[i]);
				}
			}

			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				bool isInitiallyEnabled = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
				_renderersInitiallyEnabled.Add(isInitiallyEnabled);
			}
		}

		private void NormalizeAllCanvasesForPreview()
		{
			for (int i = 0; i < _canvases.Count; i++)
			{
				Canvas canvas = _canvases[i];
				if (canvas == null)
					continue;

				canvas.renderMode = RenderMode.WorldSpace;
				canvas.worldCamera = PreviewCam;
				canvas.planeDistance = 1f;
				canvas.sortingOrder = 0;
				canvas.overrideSorting = false;

				RectTransform rect = canvas.transform as RectTransform;
				if (rect != null)
				{
					rect.position = WorldOffset;
					rect.rotation = Quaternion.identity;
					rect.localScale = Vector3.one * 0.01f;

					if (rect.rect.width <= 0.001f || rect.rect.height <= 0.001f)
						rect.sizeDelta = new Vector2(1920f, 1080f);
				}

				SetLayerRecursively(canvas.gameObject, PreviewLayer);
			}
		}

		private void CenterContentRoot()
		{
			if (_contentRoot == null)
				return;

			_contentRoot.position = WorldOffset;
			_contentRoot.rotation = Quaternion.identity;
		}

		private void ForceUiRefresh()
		{
			for (int i = 0; i < _graphics.Count; i++)
			{
				Graphic graphic = _graphics[i];
				if (graphic == null)
					continue;

				CanvasRenderer canvasRenderer = graphic.GetComponent<CanvasRenderer>();
				if (canvasRenderer == null)
					continue;

				graphic.SetAllDirty();
				canvasRenderer.cull = false;
			}

			Canvas.ForceUpdateCanvases();
		}

		private void AutoFrame()
		{
			if (!TryGetUiContentBounds(out Bounds bounds))
				return;

			_framedBounds = bounds;
			_hasFramedBounds = true;

			SetPivot(bounds.center);

			float size = Mathf.Max(bounds.extents.x, bounds.extents.y, 0.05f);

			// In 2D compatibility mode: orthographicSize = distance * 0.5
			SetCameraDistanceWithIntroZoom(Mathf.Max(
				size * ImprovedPreviewSettings.UiPreviewDistanceFactor,
				ImprovedPreviewSettings.UiPreviewMinimumDistance));
			SetOrbit(Vector2.zero);
		}

		private bool TryGetUiContentBounds(out Bounds bounds)
		{
			bounds = default;
			bool found = false;

			// Frame actual UI graphics, not the whole canvas.
			for (int i = 0; i < _graphics.Count; i++)
			{
				Graphic graphic = _graphics[i];
				if (graphic == null || !graphic.gameObject.activeInHierarchy)
					continue;

				RectTransform rectTransform = graphic.rectTransform;
				if (rectTransform == null)
					continue;

				Vector3[] corners = new Vector3[4];
				rectTransform.GetWorldCorners(corners);

				Bounds graphicBounds = new Bounds(corners[0], Vector3.zero);
				for (int c = 1; c < 4; c++)
					graphicBounds.Encapsulate(corners[c]);

				if (!found)
				{
					bounds = graphicBounds;
					found = true;
				}
				else
				{
					bounds.Encapsulate(graphicBounds);
				}
			}

			if (found)
				return true;

			if (_contentRoot != null)
			{
				Vector3[] corners = new Vector3[4];
				_contentRoot.GetWorldCorners(corners);

				bounds = new Bounds(corners[0], Vector3.zero);
				for (int i = 1; i < 4; i++)
					bounds.Encapsulate(corners[i]);

				return true;
			}

			return false;
		}

		private void ComputeStats()
		{
			int maskCount = 0;
			HashSet<Material> uniqueMaterials = new HashSet<Material>();

			for (int i = 0; i < _graphics.Count; i++)
			{
				Graphic graphic = _graphics[i];
				if (graphic == null)
					continue;

				if (graphic.material != null)
					uniqueMaterials.Add(graphic.material);

				if (graphic.GetComponent<Mask>() != null || graphic.GetComponent<RectMask2D>() != null)
					maskCount++;
			}

			int rectCount = PreviewRoot != null ? PreviewRoot.GetComponentsInChildren<RectTransform>(true).Length : 0;

			var lines = new List<string>
			{
				$"UI Graphics: {_graphics.Count}",
				$"RectTransforms: {rectCount}",
				$"Masks: {maskCount}",
				$"Materials: {uniqueMaterials.Count}"
			};

			if (_hasFramedBounds)
			{
				Vector3 size = _framedBounds.size;
				lines.Add($"Bounds: {size.x:F2} x {size.y:F2}");
			}

			_statsLines = lines.ToArray();
		}

		private static bool HasUiRenderableContent(GameObject prefab)
		{
			if (prefab == null)
				return false;

			// Mixed-content prefabs (UI + mesh/sprite/particle/3D TMP/etc.) should use
			// the regular 3D preview.
			if (prefab.GetComponentInChildren<Renderer>(true) != null)
				return false;
			if (s_tmpWorldType != null && prefab.GetComponentInChildren(s_tmpWorldType, true) != null)
				return false;

			bool hasRectTransform = prefab.GetComponentInChildren<RectTransform>(true) != null;
			bool hasGraphic = s_graphicType != null && prefab.GetComponentInChildren(s_graphicType, true) != null;
			bool hasTmpUi = s_tmpUiType != null && prefab.GetComponentInChildren(s_tmpUiType, true) != null;
			if (!hasRectTransform || (!hasGraphic && !hasTmpUi))
				return false;

			// Pure UI hierarchy only: every transform in the prefab must be RectTransform.
			// This keeps world/3D content from being routed to UIPrefabPreview.
			Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < transforms.Length; i++)
			{
				if (transforms[i] is not RectTransform)
					return false;
			}

			// A prefab may intentionally omit Canvas (e.g. reusable UI element) and rely
			// on the preview to synthesize one. Require pure UI renderable content, not Canvas.
			return true;
		}

		private static void SetLayerRecursively(GameObject root, int layer)
		{
			if (root == null)
				return;

			Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
			for (int i = 0; i < transforms.Length; i++)
				transforms[i].gameObject.layer = layer;
		}
	}
}
#endif
