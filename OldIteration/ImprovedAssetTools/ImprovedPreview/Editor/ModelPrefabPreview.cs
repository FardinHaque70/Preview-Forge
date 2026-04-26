#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	// =========================================================================
	// ModelPrefabPreview
	// Previews prefabs that contain model/mesh-style renderable content.
	// Camera framing, animation, visual modes, and bounds/light overlays are
	// split into partial files to keep this core file focused on orchestration.
	// =========================================================================

	enum PreviewVisualMode
	{
		None,
		Normals,
		UvChecker,
		VertexColor,
		Overdraw
	}

	public partial class ModelPrefabPreview : CustomPreviewBase
	{
		private readonly List<Renderer> _renderers = new();
		private readonly List<bool> _renderersInitiallyEnabled = new();

		private Bounds _framedBounds;
		private bool _hasFramedBounds;
		private bool _needsInitial2DCheck = true;
		private bool _hasMeshRendererContent;

		private bool _showBounds;
		private bool _showStats = true;
		private string[] _statsLines;
		private GUIStyle _statsStyle;

		private Material _boundsMat;
		private Mesh _boundsMesh;

		private LightWidgetCacheState _lightWidgetCache;
		private Vector2 _lightWidgetLastMouse;

		// Visual mode state
		private PreviewVisualMode _visualMode = PreviewVisualMode.None;
		private PreviewVisualMode _lastVisualMode = PreviewVisualMode.Normals;
		private Material _normalsMat;
		private Material _uvCheckerMat;
		private Material _vertexColorMat;
		private Material _overdrawMat;
		private readonly List<Material[]> _savedMaterials = new();
		private CameraClearFlags _savedClearFlags;
		private Color _savedBgColor;

		public override PreviewAssetTypeKey PreviewTypeKey => PreviewAssetTypeKey.ModelPrefab;

		public override bool Supports(GameObject prefab)
		{
			if (prefab == null) return false;
			if (!EditorUtility.IsPersistent(prefab)) return false;
			if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) return false;

			if (prefab.GetComponent<ParticleSystem>() != null) return false;

			bool hasMeshContent = prefab.GetComponentInChildren<MeshRenderer>(true) != null
			                      || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
			if (hasMeshContent)
				return true;

			bool hasSpriteContent = prefab.GetComponentInChildren<SpriteRenderer>(true) != null;
			if (!hasSpriteContent)
				return false;

			return true;
		}

		protected override void OnSetup(GameObject prefab)
		{
			_renderers.Clear();
			_renderersInitiallyEnabled.Clear();

			PreviewRoot.GetComponentsInChildren(true, _renderers);
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				bool isInitiallyEnabled = renderer != null
				                          && renderer.enabled
				                          && renderer.gameObject.activeInHierarchy;
				_renderersInitiallyEnabled.Add(isInitiallyEnabled);
			}

			_hasMeshRendererContent = HasMeshRendererContent(_renderers);

			_needsInitial2DCheck = true;
			ResetLightRigDirection();
			_showStats = ImprovedPreviewSettings.ModelPreviewDefaultStats;
			_showBounds = ImprovedPreviewSettings.ModelPreviewDefaultBounds;
			_visualMode = GetConfiguredDefaultVisualMode();
			_lastVisualMode = _visualMode != PreviewVisualMode.None
				? _visualMode
				: PreviewVisualMode.Normals;

			_boundsMat = CreateBoundsMaterial();
			_boundsMesh = new Mesh {hideFlags = HideFlags.HideAndDontSave};

			if (TryGetModelRootRotationCompensation(prefab, out Quaternion modelRootRotation))
				PreviewRoot.transform.rotation = modelRootRotation;

			AutoFrame();
			ComputePreviewStats();
			DiscoverAnimationClips(prefab);

			foreach (Renderer renderer in _renderers)
			{
				if (renderer != null)
					renderer.enabled = false;
			}

			SetTurntableActive(ImprovedPreviewSettings.ModelPreviewDefaultTurntable);
		}

		protected override void OnCleanup()
		{
			_renderers.Clear();
			_renderersInitiallyEnabled.Clear();

			_needsInitial2DCheck = false;
			_hasFramedBounds = false;
			_hasMeshRendererContent = false;
			_visualMode = PreviewVisualMode.None;
			_statsLines = null;

			DestroyPlayableGraph();

			_isModelAsset = false;
			_animClips = null;
			_animClipNames = null;
			_currentClipIndex = 0;
			_previewAnimator = null;

			if (_boundsMesh != null)
				UnityEngine.Object.DestroyImmediate(_boundsMesh);
			if (_boundsMat != null)
				UnityEngine.Object.DestroyImmediate(_boundsMat);

			DestroyMaterial(ref _normalsMat);
			DestroyMaterial(ref _uvCheckerMat);
			DestroyMaterial(ref _vertexColorMat);
			DestroyMaterial(ref _overdrawMat);

			PreviewLightWidgetSystem.DisposeTexture(ref _lightWidgetCache);

			_boundsMesh = null;
			_boundsMat = null;
			_savedMaterials.Clear();
		}

		protected override bool ShouldDrawSharedToolbarInPreview() => false;
		protected override bool SupportsPreviewLightRig() => _hasMeshRendererContent;
		protected override bool ShouldShowLightsToolbarButton() => _hasMeshRendererContent && base.ShouldShowLightsToolbarButton();

		protected override void OnManualCameraInteraction()
		{
			// Any manual orbit or pan immediately turns off the turntable so it doesn't
			// fight the user's camera control. They can re-enable it from the toolbar.
			SetTurntableActive(false);
		}

		protected override void DrawExtraToolbar(ref Rect previewRect)
		{
			if (IsTwoDimensionalRendererCompatibilityModeActive())
				return;

			PreviewPipelineContext pipelineContext = PreviewPipelineContextResolver.GetCurrentContext();
			PreviewFeatureGuardResult guard = PreviewFeatureGuard.Evaluate(
				pipelineContext,
				ShouldUse2DCompatibilityMode(),
				ShouldForce2DCompatibilityMode(),
				ShouldDrawGridIn2DMode(),
				new PreviewFeatureState(
					IsSkyboxEnabled(),
					reflectionRequested: true,
					IsGridEnabled(),
					IsLightingEnabled()));

			bool showWarnings = ImprovedPreviewSettings.ShowCapabilityWarnings;

			const float barHeight = 40f;
			const float buttonHeight = 29f;
			const float sidePadding = 6f;
			const float buttonGap = 4f;

			Rect bar = new Rect(previewRect.x, previewRect.y, previewRect.width, barHeight);
			previewRect = new Rect(previewRect.x, previewRect.y + barHeight, previewRect.width, previewRect.height - barHeight);
			ImprovedEditorTheme.DrawToolbarBackground(bar);

			bool showMeshVisualModeControl = _hasMeshRendererContent;
			int buttonCount = showMeshVisualModeControl ? 4 : 3; // Stats, Turntable, BB, optional visual mode
			if (ShouldShowLightsToolbarButton()) buttonCount++;
			if (ShouldShowGridToolbarButton()) buttonCount++;
			if (ShouldShowSkyboxToolbarButton()) buttonCount++;

			float availableWidth = bar.width - sidePadding * 2f - (buttonCount - 1) * buttonGap;
			float buttonWidth = Mathf.Max(1f, availableWidth / buttonCount);

			float x = bar.x + sidePadding;
			float y = Mathf.Round(bar.center.y - buttonHeight * 0.5f);

			if (DrawPreviewToolbarButton(
				    new Rect(x, y, buttonWidth, buttonHeight),
				    TurntableActive,
				    "Auto",
				    "Toggle turntable auto-rotation",
				    "RotateTool",
				    "d_RotateTool"))
			{
				ToggleTurntable();
				RequestPreviewRepaint();
			}

			x += buttonWidth + buttonGap;

			if (DrawPreviewToolbarButton(
				    new Rect(x, y, buttonWidth, buttonHeight),
				    _showStats,
				    "Stats",
				    "Toggle preview statistics overlay",
				    "d_Search Icon",
				    "Search Icon",
				    "d_console.infoicon.sml",
				    "console.infoicon.sml"))
			{
				_showStats = !_showStats;
				RequestPreviewRepaint();
			}

			x += buttonWidth + buttonGap;

			if (ShouldShowLightsToolbarButton())
			{
				string reason = guard.TryGetDisabledReason(PreviewFeature.LightRig, out string lightReason) ? lightReason : string.Empty;
				bool canToggle = pipelineContext.Capabilities.SupportsLightRig;
				string tooltip = !canToggle && showWarnings && !string.IsNullOrEmpty(reason)
					? reason
					: "Toggle preview lights";

				if (DrawPreviewToolbarButton(
					    new Rect(x, y, buttonWidth, buttonHeight),
					    guard.LightingEnabled,
					    "Lights",
					    tooltip,
					    canToggle,
					    "SceneViewLighting",
					    "d_SceneViewLighting"))
				{
					ToggleLighting();
					RequestPreviewRepaint();
				}

				x += buttonWidth + buttonGap;
			}

			if (ShouldShowGridToolbarButton())
			{
				string reason = guard.TryGetDisabledReason(PreviewFeature.Grid3DOrientation, out string gridReason) ? gridReason : string.Empty;
				bool canToggle = !guard.Use2DCompatibilityMode || ShouldDrawGridIn2DMode();
				string tooltip = !canToggle && showWarnings && !string.IsNullOrEmpty(reason)
					? reason
					: "Toggle preview grid";

				if (DrawPreviewToolbarButton(
					    new Rect(x, y, buttonWidth, buttonHeight),
					    guard.GridEnabled,
					    "Grid",
					    tooltip,
					    canToggle,
					    "Grid.BoxTool",
					    "d_Grid.BoxTool"))
				{
					ToggleGrid();
					RequestPreviewRepaint();
				}

				x += buttonWidth + buttonGap;
			}

			if (ShouldShowSkyboxToolbarButton())
			{
				string reason = guard.TryGetDisabledReason(PreviewFeature.Skybox, out string skyboxReason) ? skyboxReason : string.Empty;
				bool canToggle = pipelineContext.Capabilities.SupportsSkybox;
				string tooltip = !canToggle && showWarnings && !string.IsNullOrEmpty(reason)
					? reason
					: "Toggle preview skybox";

				if (DrawPreviewToolbarButton(
					    new Rect(x, y, buttonWidth, buttonHeight),
					    guard.SkyboxEnabled,
					    "Skybox",
					    tooltip,
					    canToggle,
					    "PreMatSphere",
					    "d_PreMatSphere"))
				{
					ToggleSkybox();
					RequestPreviewRepaint();
				}

				x += buttonWidth + buttonGap;
			}

			if (DrawPreviewToolbarButton(
				    new Rect(x, y, buttonWidth, buttonHeight),
				    _showBounds,
				    "BB",
				    "Toggle prefab bounds overlay",
				    "RectTool",
				    "d_RectTool"))
			{
				_showBounds = !_showBounds;
				RequestPreviewRepaint();
			}

			x += buttonWidth + buttonGap;

			if (showMeshVisualModeControl)
			{
				GetVisualModeButtonContent(out string vmLabel, out string vmTooltip, out string[] vmIcons);
				PreviewVisualMode displayMode = _visualMode != PreviewVisualMode.None ? _visualMode : _lastVisualMode;
				bool tintIcon = displayMode == PreviewVisualMode.Normals || displayMode == PreviewVisualMode.Overdraw;

				int splitResult = DrawPreviewToolbarSplitButton(
					new Rect(x, y, buttonWidth, buttonHeight),
					_visualMode != PreviewVisualMode.None,
					tintIcon,
					vmLabel,
					vmTooltip,
					vmIcons);

				if (splitResult == 1)
				{
					if (_visualMode != PreviewVisualMode.None)
						_visualMode = PreviewVisualMode.None;
					else
						_visualMode = _lastVisualMode != PreviewVisualMode.None
							? _lastVisualMode
							: PreviewVisualMode.Normals;

					RequestPreviewRepaint();
				}
				else if (splitResult == 2)
				{
					ShowVisualModeDropdown(new Rect(x, y + buttonHeight, buttonWidth, 0f));
				}

				x += buttonWidth + buttonGap;
			}

			if (HasAnimationClips())
				DrawAnimationToolbar(ref previewRect);
		}

		private void ShowVisualModeDropdown(Rect buttonRect)
		{
			if (!_hasMeshRendererContent)
				return;

			GenericMenu menu = new GenericMenu();

			menu.AddItem(new GUIContent("None"), _visualMode == PreviewVisualMode.None, () =>
			{
				_visualMode = PreviewVisualMode.None;
				RequestPreviewRepaint();
			});

			menu.AddSeparator("");
			menu.AddItem(new GUIContent("Normals"), _visualMode == PreviewVisualMode.Normals, () => ToggleVisualMode(PreviewVisualMode.Normals));
			menu.AddItem(new GUIContent("UV Checker"), _visualMode == PreviewVisualMode.UvChecker, () => ToggleVisualMode(PreviewVisualMode.UvChecker));
			menu.AddItem(new GUIContent("Vertex Colors"), _visualMode == PreviewVisualMode.VertexColor, () => ToggleVisualMode(PreviewVisualMode.VertexColor));
			menu.AddItem(new GUIContent("Overdraw"), _visualMode == PreviewVisualMode.Overdraw, () => ToggleVisualMode(PreviewVisualMode.Overdraw));

			menu.DropDown(buttonRect);
		}

		private static PreviewVisualMode GetConfiguredDefaultVisualMode()
		{
			return ImprovedPreviewSettings.ModelPreviewDefaultVisualMode switch
			{
				ModelPreviewDefaultVisualMode.Normals => PreviewVisualMode.Normals,
				ModelPreviewDefaultVisualMode.UvChecker => PreviewVisualMode.UvChecker,
				ModelPreviewDefaultVisualMode.VertexColor => PreviewVisualMode.VertexColor,
				ModelPreviewDefaultVisualMode.Overdraw => PreviewVisualMode.Overdraw,
				_ => PreviewVisualMode.None
			};
		}

		protected override void OnBeforeRender()
		{
			if (_needsInitial2DCheck)
			{
				_needsInitial2DCheck = false;
				if (IsTwoDimensionalRendererCompatibilityModeActive())
					SetOrbit(Vector2.zero);
			}

			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;
				if (i >= _renderersInitiallyEnabled.Count || !_renderersInitiallyEnabled[i])
					continue;

				renderer.enabled = true;
			}

			if (_hasMeshRendererContent && _visualMode != PreviewVisualMode.None)
			{
				Material mat = GetVisualModeMaterial();
				if (mat != null)
					SwapMaterials(mat);

				if (_visualMode == PreviewVisualMode.Overdraw)
				{
					_savedClearFlags = PreviewCam.clearFlags;
					_savedBgColor = PreviewCam.backgroundColor;
					PreviewCam.clearFlags = CameraClearFlags.SolidColor;
					PreviewCam.backgroundColor = Color.black;
				}
			}

			if (_showBounds && _hasFramedBounds && _boundsMesh != null && _boundsMat != null)
			{
				BuildBoundsMesh(_boundsMesh, GetRenderBounds());
				Graphics.DrawMesh(_boundsMesh, Matrix4x4.identity, _boundsMat, PreviewLayer, PreviewCam);
			}
		}

		protected override void OnAfterRender()
		{
			if (_hasMeshRendererContent && _visualMode != PreviewVisualMode.None)
			{
				RestoreMaterials();

				if (_visualMode == PreviewVisualMode.Overdraw)
				{
					PreviewCam.clearFlags = _savedClearFlags;
					PreviewCam.backgroundColor = _savedBgColor;
				}
			}

			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (renderer == null)
					continue;
				if (i >= _renderersInitiallyEnabled.Count || !_renderersInitiallyEnabled[i])
					continue;

				renderer.enabled = false;
			}
		}

		protected override void HandleOverlayInput(Rect previewRect)
		{
			if (IsTwoDimensionalRendererCompatibilityModeActive() || !_hasMeshRendererContent)
				return;

			Rect sphereRect = GetLightWidgetSphereRect(previewRect);
			Event evt = Event.current;
			int controlId = GUIUtility.GetControlID("ModelPreviewLightWidget".GetHashCode(), FocusType.Passive, sphereRect);

			switch (evt.GetTypeForControl(controlId))
			{
				case EventType.MouseDown:
					if (evt.button == 0 && sphereRect.Contains(evt.mousePosition))
					{
						GUIUtility.hotControl = controlId;
						_lightWidgetLastMouse = evt.mousePosition;
						evt.Use();
					}

					break;

				case EventType.MouseDrag:
					if (GUIUtility.hotControl == controlId)
					{
						RotateLightRigFromDrag(evt.mousePosition - _lightWidgetLastMouse);
						_lightWidgetLastMouse = evt.mousePosition;
						evt.Use();
					}

					break;

				case EventType.MouseUp:
					if (GUIUtility.hotControl == controlId)
					{
						GUIUtility.hotControl = 0;
						evt.Use();
					}

					break;
			}
		}

		protected override void DrawOverlay(Rect previewRect)
		{
			if (!IsTwoDimensionalRendererCompatibilityModeActive() && _hasMeshRendererContent)
			{
				Rect sphereRect = GetLightWidgetSphereRect(previewRect);
				Texture2D sphereTexture = GetLightWidgetTexture();
				if (sphereTexture != null)
					GUI.DrawTexture(sphereRect, sphereTexture, ScaleMode.StretchToFill, true);
			}

			if (!IsTwoDimensionalRendererCompatibilityModeActive()
			    && _showStats
			    && _statsLines != null
			    && _statsLines.Length > 0)
			{
				DrawStatsOverlay(previewRect);
			}
		}

		private void DrawStatsOverlay(Rect previewRect)
		{
			const float padding = 8f;
			const float lineHeight = 14f;
			const float internalPad = 4f;

			GUIStyle style = _statsStyle;
			if (style == null)
			{
				style = new GUIStyle(EditorStyles.miniLabel)
				{
					fontSize = 10,
					normal = {textColor = new Color(0.85f, 0.85f, 0.85f, 1f)},
					alignment = TextAnchor.MiddleLeft,
					padding = new RectOffset(0, 0, 0, 0)
				};
				_statsStyle = style;
			}

			float yPos = previewRect.yMax - padding - _statsLines.Length * lineHeight;
			for (int i = 0; i < _statsLines.Length; i++)
			{
				Rect lineRect = new Rect(previewRect.x + padding + internalPad, yPos, previewRect.width, lineHeight);
				GUI.Label(lineRect, _statsLines[i], style);
				yPos += lineHeight;
			}
		}

		private void ComputePreviewStats()
		{
			_statsLines = IsTwoDimensionalRendererCompatibilityModeActive()
				? null
				: Compute3DStatsLines();
		}

		private string[] Compute3DStatsLines()
		{
			int totalVerts = 0;
			int totalTris = 0;
			int totalSubMeshes = 0;
			int meshCount = 0;
			HashSet<Material> uniqueMaterials = new HashSet<Material>();

			foreach (Renderer renderer in _renderers)
			{
				if (renderer == null)
					continue;

				Mesh mesh = null;
				if (renderer is MeshRenderer)
				{
					MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
					if (meshFilter != null)
						mesh = meshFilter.sharedMesh;
				}
				else if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
				{
					mesh = skinnedMeshRenderer.sharedMesh;
				}

				if (mesh != null)
				{
					totalVerts += mesh.vertexCount;
					totalTris += (int) mesh.GetIndexCount(0) / 3;
					for (int subMeshIndex = 1; subMeshIndex < mesh.subMeshCount; subMeshIndex++)
						totalTris += (int) mesh.GetIndexCount(subMeshIndex) / 3;

					totalSubMeshes += mesh.subMeshCount;
					meshCount++;
				}

				foreach (Material material in renderer.sharedMaterials)
				{
					if (material != null)
						uniqueMaterials.Add(material);
				}
			}

			List<string> lines = new List<string>
			{
				$"Verts: {FormatCount(totalVerts)}",
				$"Tris: {FormatCount(totalTris)}"
			};

			if (meshCount > 1)
				lines.Add($"Meshes: {meshCount}");
			if (totalSubMeshes > meshCount)
				lines.Add($"SubMeshes: {totalSubMeshes}");

			lines.Add($"Materials: {uniqueMaterials.Count}");

			if (_hasFramedBounds)
			{
				Vector3 size = _framedBounds.size;
				lines.Add($"Bounds: {size.x:F2} x {size.y:F2} x {size.z:F2}");
			}

			return lines.ToArray();
		}

		private static bool HasMeshRendererContent(List<Renderer> renderers)
		{
			if (renderers == null || renderers.Count == 0)
				return false;

			for (int i = 0; i < renderers.Count; i++)
			{
				Renderer renderer = renderers[i];
				if (renderer == null)
					continue;

				if (renderer is MeshRenderer meshRenderer)
				{
					MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
					if (meshFilter != null && meshFilter.sharedMesh != null)
						return true;
					continue;
				}

				if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
					return true;
			}

			return false;
		}

		private static string FormatCount(int count)
		{
			if (count >= 1000000)
				return $"{count / 1000000f:F1}M";
			if (count >= 1000)
				return $"{count / 1000f:F1}K";
			return count.ToString();
		}

		private static Material CreateBoundsMaterial()
		{
			Material material = new Material(Shader.Find("Hidden/Internal-Colored"))
			{
				hideFlags = HideFlags.HideAndDontSave
			};

			material.SetInt("_ZWrite", 0);
			material.SetInt("_Cull", 0);
			material.SetInt("_ZTest", (int) CompareFunction.LessEqual);
			material.SetInt("_SrcBlend", (int) BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int) BlendMode.OneMinusSrcAlpha);
			return material;
		}

		private static void BuildBoundsMesh(Mesh mesh, Bounds bounds)
		{
			Vector3 center = bounds.center;
			Vector3 extents = bounds.extents;
			Vector3[] corners =
			{
				center + new Vector3(-extents.x, -extents.y, -extents.z),
				center + new Vector3(extents.x, -extents.y, -extents.z),
				center + new Vector3(extents.x, -extents.y, extents.z),
				center + new Vector3(-extents.x, -extents.y, extents.z),
				center + new Vector3(-extents.x, extents.y, -extents.z),
				center + new Vector3(extents.x, extents.y, -extents.z),
				center + new Vector3(extents.x, extents.y, extents.z),
				center + new Vector3(-extents.x, extents.y, extents.z),
			};

			int[] indices =
			{
				0, 1, 1, 2, 2, 3, 3, 0,
				4, 5, 5, 6, 6, 7, 7, 4,
				0, 4, 1, 5, 2, 6, 3, 7
			};

			Color[] colors = Enumerable.Repeat(ImprovedEditorTheme.AccentBright, corners.Length).ToArray();

			mesh.Clear();
			mesh.vertices = corners;
			mesh.colors = colors;
			mesh.SetIndices(indices, MeshTopology.Lines, 0);
		}

		private Rect GetLightWidgetSphereRect(Rect previewRect)
		{
			return PreviewLightWidgetSystem.GetSphereRect(previewRect, PreviewLightWidgetSystem.DefaultLayoutConfig);
		}

		private void RotateLightRigFromDrag(Vector2 delta)
		{
			if (PreviewCam == null)
				return;

			Vector3 direction = GetLightRigDirectionWorld();
			Vector3 upAxis = PreviewCam.transform.up;
			Vector3 rightAxis = PreviewCam.transform.right;
			LightWidgetLayoutConfig layout = PreviewLightWidgetSystem.DefaultLayoutConfig;

			Quaternion yaw = Quaternion.AngleAxis(delta.x * layout.DragSensitivity, upAxis);
			Quaternion pitch = Quaternion.AngleAxis(-delta.y * layout.DragSensitivity, rightAxis);
			Vector3 rotated = yaw * pitch * direction;

			SetLightRigDirectionWorld(rotated.normalized);
		}

		private Texture2D GetLightWidgetTexture()
		{
			LightWidgetRenderInput renderInput = new LightWidgetRenderInput(
				GetWidgetSpaceKeyLightDirection(),
				GetWidgetSpaceFillLightDirection(),
				ImprovedPreviewSettings.AmbientColor,
				ImprovedPreviewSettings.KeyIntensity,
				ImprovedPreviewSettings.FillIntensity);
			return PreviewLightWidgetSystem.GetOrUpdateTexture(
				ref _lightWidgetCache,
				renderInput,
				PreviewLightWidgetSystem.DefaultLayoutConfig);
		}

		private Vector3 GetWidgetSpaceKeyLightDirection()
		{
			Vector3 incomingLightWorld = (-GetLightRigDirectionWorld()).normalized;
			return GetWidgetSpaceLightDirection(incomingLightWorld);
		}

		private Vector3 GetWidgetSpaceFillLightDirection()
		{
			Quaternion lightRigRotation = Quaternion.FromToRotation(
				ImprovedPreviewSettings.KeyPosition.normalized,
				GetLightRigDirectionWorld());

			Vector3 fillPositionWorld = lightRigRotation * ImprovedPreviewSettings.FillPosition;
			Vector3 incomingLightWorld = (-fillPositionWorld).normalized;
			return GetWidgetSpaceLightDirection(incomingLightWorld);
		}

		private Vector3 GetWidgetSpaceLightDirection(Vector3 incomingLightWorld)
		{
			if (PreviewCam == null)
				return incomingLightWorld.normalized;

			return PreviewCam.transform.InverseTransformDirection(incomingLightWorld).normalized;
		}

	}
}
#endif
