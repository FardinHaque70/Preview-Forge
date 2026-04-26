#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FardinHaque.ImprovedAssetTools.Editor
{

[CanEditMultipleObjects]
[CustomEditor(typeof(Material))]
public sealed class MaterialPreviewEditor : MaterialEditor, IPreviewRenderHost
{
	private const PreviewAssetTypeKey PreviewTypeKey = PreviewAssetTypeKey.Material;
	private const float MinimumCustomPreviewSize = 120f;
	private const int PreviewLayer = 31;
	private static readonly Vector3 WorldOffset = new Vector3(5000f, 5000f, 5000f);

	private enum PreviewMeshType
	{
		Cube,
		Sphere,
		Torus,
		Quad
	}

	private Camera _previewCamera;
	private Light _light0;
	private Light _light1;
	private Light _light2;
	private GameObject _previewMeshObject;
	private MeshFilter _previewMeshFilter;
	private MeshRenderer _previewMeshRenderer;
	private RenderTexture _previewRT;
	private Texture _lastPreviewTexture;
	private int _rtWidth;
	private int _rtHeight;
	private int _lastRenderedViewWidth;
	private int _lastRenderedViewHeight;
	private int _lastAppliedRevision = -1;
	private int _lastDefaultMeshMaterialInstanceId = int.MinValue;

	private Mesh _cubeMesh;
	private Mesh _sphereMesh;
	private Mesh _torusMesh;
	private Mesh _quadMesh;
	private PreviewMeshType _previewMeshType = PreviewMeshType.Sphere;
	private Mesh _gridMesh;
	private Material _gridMat;
	private float _cachedGridHalfSize = -1f;
	private float _cachedGridStep = -1f;
	private float _cachedGridAlpha = -1f;
	private Material _skyboxPreviewMat;
	private Texture _lastSkyboxTexture;
	private bool _showUnsupportedMaterialOverlay;
	private string _unsupportedMaterialOverlayText = string.Empty;
	private GUIStyle _unsupportedMaterialOverlayStyle;

	private Vector2 _orbit = new Vector2(35f, 15f);
	private Vector2 _targetOrbit = new Vector2(35f, 15f);
	private float _distance = 2.3f;
	private float _targetDistance = 2.3f;
	private Vector2 _orbitAngularVelocity;
	private bool _isOrbitDragging;
	private double _lastOrbitInputTime = -1d;
	private Vector3 _pivot = WorldOffset;
	private bool _useLighting = true;
	private bool _showGrid = true;
	private bool _showSkybox = true;
	private bool _showReflection = true;
	private bool _previewDirty = true;
	private double _lastInteractionUpdateTime = -1d;
	private double _nextContinuousRenderTime = -1d;
	private double _nextContinuousRepaintTime = -1d;
	private bool _continuousRepaintLoopSubscribed;
	private bool _turntableActive;
	private double _lastTurntableRepaintTime = -1d;

	private Vector3 _lightRigDirectionWorld;
	private bool _hasCustomLightRigDirection;

	private LightWidgetCacheState _lightWidgetCache;
	private Vector2 _lightWidgetLastMouse;

	private readonly List<Light> _enabledSceneLights = new();
	private static readonly Dictionary<string, Texture> IconCache = new();
	private static readonly HashSet<string> LoggedDiagnosticKeys = new();

	private static readonly GUIContent SettingsContent = new GUIContent("Settings", "Open Improved Preview settings");
	string IPreviewRenderHost.HostName => nameof(MaterialPreviewEditor);
	bool IPreviewRenderHost.PreserveCameraColorBuffer => false;

	private new void OnDisable()
	{
		StopContinuousRepaintLoop();
		CleanupPreviewResources();
		InvokeMaterialEditorDisable();
	}

	private void InvokeMaterialEditorDisable()
	{
		try
		{
			MethodInfo onDisableMethod = typeof(MaterialEditor).GetMethod(
				"OnDisable",
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			onDisableMethod?.Invoke(this, null);
		}
		catch (TargetInvocationException)
		{
			// Ignore: we never want base cleanup exceptions to block editor teardown.
		}
		catch (MethodAccessException)
		{
			// Ignore: best-effort invocation across Unity versions.
		}
	}

	public override void OnPreviewSettings()
	{
		if (!ImprovedPreviewSettings.Active || !ImprovedPreviewSettings.IsPreviewTypeEnabled(PreviewTypeKey))
		{
			base.OnPreviewSettings();
			return;
		}

		if (GUILayout.Button(SettingsContent, EditorStyles.toolbarButton))
			ImprovedPreviewSettings.SelectSettingsAsset();
	}

	public override void OnPreviewGUI(Rect r, GUIStyle background)
	{
		if (!ImprovedPreviewSettings.Active
			|| !ImprovedPreviewSettings.IsPreviewTypeEnabled(PreviewTypeKey)
			|| !ShouldDrawCustomPreviewForRect(r))
		{
			StopContinuousRepaintLoop();
			base.OnPreviewGUI(r, background);
			return;
		}

		DrawCustomPreview(r);
	}

	public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
	{
		if (!ImprovedPreviewSettings.Active
			|| !ImprovedPreviewSettings.IsPreviewTypeEnabled(PreviewTypeKey)
			|| !ShouldDrawCustomPreviewForRect(r))
		{
			StopContinuousRepaintLoop();
			base.OnInteractivePreviewGUI(r, background);
			return;
		}

		DrawCustomPreview(r);
	}

	private static bool ShouldDrawCustomPreviewForRect(Rect rect)
	{
		return rect.width >= MinimumCustomPreviewSize && rect.height >= MinimumCustomPreviewSize;
	}

	private void DrawCustomPreview(Rect previewRect)
	{
		if (!ImprovedPreviewSettings.IsPreviewTypeEnabled(PreviewTypeKey))
		{
			StopContinuousRepaintLoop();
			base.OnPreviewGUI(previewRect, GUIStyle.none);
			return;
		}

		if (!(target is Material material) || targets == null || targets.Length != 1 || previewRect.width <= 1f || previewRect.height <= 1f)
		{
			StopContinuousRepaintLoop();
			base.OnPreviewGUI(previewRect, GUIStyle.none);
			return;
		}

		EnsurePreviewSetup();
		EnsureDefaultPreviewMeshForMaterial(material);
		EnsureContinuousRepaintLoop();
		int appliedRevision = ImprovedPreviewSettings.AppliedRevision;
		if (_lastAppliedRevision != appliedRevision)
		{
			_lastAppliedRevision = appliedRevision;
			_previewDirty = true;
		}

		DrawInPreviewToolbar(ref previewRect);
		ApplySelectedMesh();
		HandleLightWidgetInput(previewRect);
		HandleCameraInput(previewRect);
		UpdateInteractionMotion();

		int currentViewWidth = Mathf.Max(1, Mathf.RoundToInt(previewRect.width));
		int currentViewHeight = Mathf.Max(1, Mathf.RoundToInt(previewRect.height));
		bool viewportSizeChanged = _lastRenderedViewWidth != currentViewWidth || _lastRenderedViewHeight != currentViewHeight;

		if (Event.current.type == EventType.Repaint)
		{
			double now = EditorApplication.timeSinceStartup;
			double continuousIntervalSeconds = GetContinuousPreviewFrameIntervalSeconds();
			bool continuousFrameDue = _nextContinuousRenderTime < 0d || now >= _nextContinuousRenderTime;

			// Advance turntable orbit before rendering so this frame captures the new angle.
			// Moving _orbit directly (no target-chase) gives constant-speed rotation.
			// The material editor drives its own repaint loop via EditorApplication.update.
			if (_turntableActive)
			{
				double nowTt = EditorApplication.timeSinceStartup;
				float ttDt = _lastTurntableRepaintTime >= 0d
					? Mathf.Clamp((float)(nowTt - _lastTurntableRepaintTime), 0f, 0.1f)
					: 0f;
				_lastTurntableRepaintTime = nowTt;
				if (ttDt > 0f)
				{
					float turntableDelta = 30f * ttDt;
					_orbit.x += turntableDelta;
					_targetOrbit.x += turntableDelta;
					_orbitAngularVelocity = Vector2.zero;
					_isOrbitDragging = false;
					_previewDirty = true;
				}
			}

			if (_previewDirty || _lastPreviewTexture == null || viewportSizeChanged || continuousFrameDue)
			{
				try
				{
					RenderPreview(previewRect, material, currentViewWidth, currentViewHeight);
					_nextContinuousRenderTime = now + continuousIntervalSeconds;
				}
				catch (ExitGUIException)
				{
					throw;
				}
				catch (Exception exception)
				{
					LogDiagnosticOnce(
						$"material-preview-render-error:{exception.GetType().FullName}",
						$"Material preview rendering failed with {exception.GetType().Name}. Falling back to Unity preview for this frame.");
					CleanupPreviewResources();
					base.OnPreviewGUI(previewRect, GUIStyle.none);
					return;
				}
			}
		}

		if (_lastPreviewTexture != null)
			EditorGUI.DrawPreviewTexture(previewRect, _lastPreviewTexture, null, ScaleMode.StretchToFill);

		DrawUnsupportedMaterialOverlay(previewRect);
		DrawLightWidgetOverlay(previewRect);
	}

	private void DrawInPreviewToolbar(ref Rect previewRect)
	{
		if (previewRect.height < 44f)
			return;

		PreviewPipelineContext pipelineContext = GetMaterialPreviewPipelineContext();
		PreviewFeatureState featureState = BuildFeatureState(pipelineContext);
		PreviewFeatureGuardResult guard = EvaluateFeatureGuard(pipelineContext, featureState);
		bool showWarnings = ImprovedPreviewSettings.ShowCapabilityWarnings;
		const float barHeight = 40f;
		const float buttonHeight = 29f;
		const float sidePadding = 6f;
		const float buttonGap = 4f;

		var bar = new Rect(previewRect.x, previewRect.y, previewRect.width, barHeight);
		previewRect = new Rect(previewRect.x, previewRect.y + barHeight, previewRect.width, previewRect.height - barHeight);
		ImprovedEditorTheme.DrawToolbarBackground(bar);

		bool showLightsButton = pipelineContext.Capabilities.SupportsLightRig;
		bool showSkyboxButton = pipelineContext.Capabilities.SupportsSkybox;
		bool showReflectionButton = pipelineContext.Capabilities.SupportsReflectionMap;
		int buttonCount = 6; // turntable + 4 mesh toggles + grid
		if (showLightsButton) buttonCount++;
		if (showSkyboxButton) buttonCount++;
		if (showReflectionButton) buttonCount++;
		float availableWidth = bar.width - sidePadding * 2f - (buttonCount - 1) * buttonGap;
		float buttonWidth = Mathf.Max(1f, availableWidth / buttonCount);
		float x = bar.x + sidePadding;
		float y = Mathf.Round(bar.center.y - buttonHeight * 0.5f);

		if (DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), _turntableActive, "Auto", "Toggle turntable auto-rotation", "RotateTool", "d_RotateTool"))
		{
			_turntableActive = !_turntableActive;
			if (_turntableActive) _previewDirty = true;
		}
		x += buttonWidth + buttonGap;

		DrawMeshToolbarButton(
			ref x, y, buttonWidth, buttonHeight, buttonGap,
			PreviewMeshType.Cube, "Cube", "Preview material on a cube mesh",
			"PreMatCube", "d_PreMatCube");
		DrawMeshToolbarButton(
			ref x, y, buttonWidth, buttonHeight, buttonGap,
			PreviewMeshType.Sphere, "Sphere", "Preview material on a sphere mesh",
			"PreMatSphere", "d_PreMatSphere");
		DrawMeshToolbarButton(
			ref x, y, buttonWidth, buttonHeight, buttonGap,
			PreviewMeshType.Torus, "Torus", "Preview material on a torus mesh",
			"PreMatTorus", "d_PreMatTorus");
		DrawMeshToolbarButton(
			ref x, y, buttonWidth, buttonHeight, buttonGap,
			PreviewMeshType.Quad, "Quad", "Preview material on a quad mesh",
			"PreMatQuad", "d_PreMatQuad");

		if (showLightsButton && DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.LightingEnabled, "Lights", "Toggle preview lights", true, "SceneViewLighting", "d_SceneViewLighting"))
		{
			_useLighting = !_useLighting;
			RequestPreviewRepaint();
		}
		if (showLightsButton)
			x += buttonWidth + buttonGap;

		string gridReason = guard.TryGetDisabledReason(PreviewFeature.Grid3DOrientation, out string reasonGrid) ? reasonGrid : string.Empty;
		bool gridSupported = true;
		string gridTooltip = !gridSupported && showWarnings && !string.IsNullOrEmpty(gridReason)
			? gridReason
			: "Toggle preview grid";
		if (DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.GridEnabled, "Grid", gridTooltip, gridSupported, "Grid.BoxTool", "d_Grid.BoxTool"))
		{
			_showGrid = !_showGrid;
			RequestPreviewRepaint();
		}
		x += buttonWidth + buttonGap;

		if (showSkyboxButton && DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.SkyboxEnabled, "Skybox", "Toggle preview skybox", true, "PreMatSphere", "d_PreMatSphere"))
		{
			_showSkybox = !_showSkybox;
			RequestPreviewRepaint();
		}
		if (showSkyboxButton)
			x += buttonWidth + buttonGap;

		if (showReflectionButton && DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), guard.ReflectionEnabled, "Refl", "Toggle reflection map", true, "SceneViewFx", "d_SceneViewFx", "SceneviewFx", "d_SceneviewFx", "ReflectionProbe Icon", "d_ReflectionProbe Icon"))
		{
			_showReflection = !_showReflection;
			RequestPreviewRepaint();
		}
	}

	private PreviewFeatureGuardResult EvaluateFeatureGuard(PreviewPipelineContext pipelineContext, PreviewFeatureState featureState)
	{
		return PreviewFeatureGuard.Evaluate(
			pipelineContext,
			// Keep material preview camera behavior consistent (perspective/orbit) across pipelines.
			wants2DCompatibilityMode: false,
			force2DCompatibilityMode: false,
			shouldDrawGridIn2DMode: true,
			featureState,
			prefer3DGridOrientationWhenNotIn2DCompatibilityMode: true);
	}

	private PreviewFeatureState BuildFeatureState(PreviewPipelineContext pipelineContext)
	{
		return new PreviewFeatureState(
			skyboxRequested: pipelineContext.Capabilities.SupportsSkybox && _showSkybox,
			reflectionRequested: pipelineContext.Capabilities.SupportsReflectionMap && _showReflection,
			gridRequested: _showGrid,
			lightingRequested: _useLighting);
	}

	private static PreviewPipelineContext GetMaterialPreviewPipelineContext()
	{
		return PreviewPipelineContextResolver.GetCurrentContext();
	}

	private void DrawMeshToolbarButton(
		ref float x,
		float y,
		float buttonWidth,
		float buttonHeight,
		float buttonGap,
		PreviewMeshType meshType,
		string label,
		string tooltip,
		params string[] iconNames)
	{
		bool active = _previewMeshType == meshType;
		if (DrawPreviewToolbarButton(new Rect(x, y, buttonWidth, buttonHeight), active, label, tooltip, iconNames))
		{
			if (!active)
			{
				_previewMeshType = meshType;
				ApplySelectedMesh();
				RequestPreviewRepaint();
			}
		}

		x += buttonWidth + buttonGap;
	}

	private void EnsurePreviewSetup()
	{
		if (_previewCamera != null && _previewMeshRenderer != null && _previewMeshFilter != null)
			return;

		CleanupPreviewResources();

		GameObject camObject = CreatePreviewObject("___MaterialPreviewCam___");
		_previewCamera = camObject.AddComponent<Camera>();
		_previewCamera.enabled = false;
		_previewCamera.clearFlags = CameraClearFlags.SolidColor;
		_previewCamera.backgroundColor = ImprovedPreviewSettings.BgColor;
		_previewCamera.cameraType = CameraType.Preview;
		_previewCamera.nearClipPlane = 0.01f;
		_previewCamera.farClipPlane = 1000f;
		_previewCamera.fieldOfView = ImprovedPreviewSettings.Fov;
		_previewCamera.depthTextureMode = DepthTextureMode.Depth;
		_previewCamera.cullingMask = 1 << PreviewLayer;

		_light0 = CreatePreviewLight("___MaterialPreviewLight0___", 1.2f, LightShadows.Soft, 0.8f);
		_light1 = CreatePreviewLight("___MaterialPreviewLight1___", 0.6f, LightShadows.None, 0f);
		_light2 = CreatePreviewLight("___MaterialPreviewLight2___", 0f, LightShadows.None, 0f);

		_previewMeshObject = CreatePreviewObject("___MaterialPreviewMesh___");
		_previewMeshFilter = _previewMeshObject.AddComponent<MeshFilter>();
		_previewMeshRenderer = _previewMeshObject.AddComponent<MeshRenderer>();
		_previewMeshRenderer.shadowCastingMode = ShadowCastingMode.On;
		_previewMeshRenderer.receiveShadows = true;
		_gridMat = CreateGridMaterial();
		_gridMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
		_cachedGridHalfSize = -1f;
		_cachedGridStep = -1f;
		_cachedGridAlpha = -1f;

		_cubeMesh = CreatePrimitiveMesh(PrimitiveType.Cube);
		_sphereMesh = CreatePrimitiveMesh(PrimitiveType.Sphere);
		_torusMesh = CreateTorusMesh(0.52f, 0.18f, 36, 24);
		_quadMesh = CreateDoubleSidedQuadMesh(1f);

		_orbit = new Vector2(35f, 15f);
		_targetOrbit = _orbit;
		_pivot = WorldOffset;
		_distance = 2.3f;
		_targetDistance = 2.3f;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;
		Material currentMaterial = target as Material;
		_previewMeshType = GetConfiguredDefaultPreviewMeshType(currentMaterial);
		_lastDefaultMeshMaterialInstanceId = currentMaterial != null ? currentMaterial.GetInstanceID() : int.MinValue;
		ApplySelectedMesh();
		_useLighting = ImprovedPreviewSettings.MaterialPreviewDefaultLights;
		_showGrid = ImprovedPreviewSettings.MaterialPreviewDefaultGrid;
		_showSkybox = ImprovedPreviewSettings.MaterialPreviewDefaultSkybox;
		_showReflection = ImprovedPreviewSettings.MaterialPreviewDefaultReflection;
		_lastInteractionUpdateTime = -1d;
		_nextContinuousRenderTime = -1d;
		_nextContinuousRepaintTime = -1d;
		_turntableActive = ImprovedPreviewSettings.MaterialPreviewDefaultTurntable;
		_lastTurntableRepaintTime = -1d;
		_previewDirty = true;
		_hasCustomLightRigDirection = false;
		ResetLightRigDirection();
	}

	private static GameObject CreatePreviewObject(string name)
	{
		var previewObject = new GameObject(name)
		{
			hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable
		};
		previewObject.layer = PreviewLayer;
		previewObject.transform.position = WorldOffset;
		previewObject.transform.rotation = Quaternion.identity;
		previewObject.transform.localScale = Vector3.one;
		return previewObject;
	}

	private static Light CreatePreviewLight(string name, float intensity, LightShadows shadows, float shadowStrength)
	{
		GameObject lightObject = CreatePreviewObject(name);
		Light light = lightObject.AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = intensity;
		light.shadows = shadows;
		light.shadowStrength = shadowStrength;
		light.cullingMask = 1 << PreviewLayer;
		light.enabled = false;
		return light;
	}

	private void ApplySelectedMesh()
	{
		if (_previewMeshFilter == null)
			return;

		Mesh mesh = GetSelectedMesh();
		if (_previewMeshFilter.sharedMesh == mesh)
			return;

		_previewMeshFilter.sharedMesh = mesh;
		FitCameraToMesh(mesh, animateIntro: true);
		_previewDirty = true;
	}

	private Mesh GetSelectedMesh()
	{
		return _previewMeshType switch
		{
			PreviewMeshType.Cube => _cubeMesh,
			PreviewMeshType.Torus => _torusMesh,
			PreviewMeshType.Quad => _quadMesh,
			_ => _sphereMesh,
		};
	}

	private static PreviewMeshType GetDefaultPreviewMeshType()
	{
		return GetMaterialPreviewPipelineContext().IsEffectively2D
			? PreviewMeshType.Quad
			: PreviewMeshType.Sphere;
	}

	private static PreviewMeshType GetConfiguredDefaultPreviewMeshType(Material material)
	{
		if (MaterialRenderCompatibilityUtility.HasPlanePreviewTypeTag(material))
			return PreviewMeshType.Quad;

		return ImprovedPreviewSettings.MaterialPreviewDefaultMeshMode switch
		{
			MaterialPreviewDefaultMeshMode.Cube => PreviewMeshType.Cube,
			MaterialPreviewDefaultMeshMode.Sphere => PreviewMeshType.Sphere,
			MaterialPreviewDefaultMeshMode.Torus => PreviewMeshType.Torus,
			MaterialPreviewDefaultMeshMode.Quad => PreviewMeshType.Quad,
			_ => GetDefaultPreviewMeshType()
		};
	}

	private void EnsureDefaultPreviewMeshForMaterial(Material material)
	{
		if (material == null)
			return;

		int materialInstanceId = material.GetInstanceID();
		if (_lastDefaultMeshMaterialInstanceId == materialInstanceId)
			return;

		_lastDefaultMeshMaterialInstanceId = materialInstanceId;
		_previewMeshType = GetConfiguredDefaultPreviewMeshType(material);
		ApplySelectedMesh();
	}

	private void FitCameraToMesh(Mesh mesh, bool animateIntro)
	{
		float minimumDistance = ImprovedPreviewSettings.MaterialPreviewMinimumDistance;
		if (mesh == null)
		{
			_distance = 2.3f;
			_targetDistance = 2.3f;
			_lastInteractionUpdateTime = -1d;
			_pivot = WorldOffset;
			return;
		}

		Bounds bounds = mesh.bounds;
		float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
		float halfFov = Mathf.Max(1f, ImprovedPreviewSettings.Fov) * 0.5f * Mathf.Deg2Rad;
		float safeSin = Mathf.Max(0.08f, Mathf.Sin(halfFov));
		float fittedDistance = Mathf.Clamp(
			(radius / safeSin) * ImprovedPreviewSettings.MaterialPreviewFitMultiplier,
			minimumDistance,
			50f);
		if (animateIntro)
		{
			_targetDistance = fittedDistance;
			_distance = Mathf.Clamp(
				Mathf.Max(
					fittedDistance * ImprovedPreviewSettings.MaterialPreviewIntroOvershootMultiplier,
					fittedDistance + ImprovedPreviewSettings.MaterialPreviewIntroOvershootOffset),
				minimumDistance,
				50f);
			_lastInteractionUpdateTime = -1d;
		}
		else
		{
			_distance = fittedDistance;
			_targetDistance = fittedDistance;
			_lastInteractionUpdateTime = -1d;
		}

		_pivot = WorldOffset + bounds.center;
	}

	private void HandleCameraInput(Rect previewRect)
	{
		if (GUIUtility.hotControl != 0)
			return;

		Event evt = Event.current;
		bool pointerInPreview = previewRect.Contains(evt.mousePosition);
		double now = EditorApplication.timeSinceStartup;

		if (evt.type == EventType.MouseDown && evt.button == 0 && pointerInPreview)
		{
			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionController.BeginOrbitDrag(ref interactionState, now);
			ApplyInteractionState(interactionState);
		}

		if (evt.type == EventType.MouseDrag && evt.button == 0)
		{
			if (!pointerInPreview)
				return;

			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionInput input = new PreviewInteractionInput(evt.delta, 0f, now);
			PreviewInteractionPolicy orbitPolicy = BuildOrbitInteractionPolicy();
			PreviewInteractionController.ApplyOrbitDrag(ref interactionState, input, orbitPolicy);
			ApplyInteractionState(interactionState);
			evt.Use();
			RequestPreviewRepaint();
		}

		if (evt.type == EventType.MouseUp && evt.button == 0)
		{
			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionController.EndOrbitDrag(ref interactionState, now);
			ApplyInteractionState(interactionState);
		}

		if (evt.type == EventType.ScrollWheel && pointerInPreview)
		{
			PreviewInteractionState interactionState = BuildInteractionState();
			PreviewInteractionInput input = new PreviewInteractionInput(Vector2.zero, evt.delta.y, now);
			PreviewInteractionPolicy zoomPolicy = BuildZoomInteractionPolicy();
			PreviewInteractionController.ApplyScrollZoom(ref interactionState, input, zoomPolicy);
			ApplyInteractionState(interactionState);
			evt.Use();
			RequestPreviewRepaint();
		}
	}

	private bool UpdateInteractionMotion()
	{
		double now = EditorApplication.timeSinceStartup;
		if (!PreviewUpdateLoopController.TryGetDeterministicDeltaTime(ref _lastInteractionUpdateTime, now, 0.05f, out float dt))
			return false;

		PreviewInteractionState interactionState = BuildInteractionState();
		PreviewInteractionPolicy policy = BuildTickInteractionPolicy();
		bool hasPendingMotion = PreviewInteractionController.Tick(
			ref interactionState,
			dt,
			ImprovedPreviewSettings.OrbitSmooth,
			ImprovedPreviewSettings.ZoomSmooth,
			policy);
		bool changed = HasStateChanged(in interactionState);
		ApplyInteractionState(interactionState);
		if (changed)
			_previewDirty = true;

		return hasPendingMotion;
	}

	private PreviewInteractionState BuildInteractionState()
	{
		return new PreviewInteractionState
		{
			Orbit = _orbit,
			TargetOrbit = _targetOrbit,
			AngularVelocity = _orbitAngularVelocity,
			Distance = _distance,
			TargetDistance = _targetDistance,
			IsOrbitDragging = _isOrbitDragging,
			LastOrbitInputTime = _lastOrbitInputTime
		};
	}

	private void ApplyInteractionState(in PreviewInteractionState state)
	{
		_orbit = state.Orbit;
		_targetOrbit = state.TargetOrbit;
		_orbitAngularVelocity = state.AngularVelocity;
		_distance = state.Distance;
		_targetDistance = state.TargetDistance;
		_isOrbitDragging = state.IsOrbitDragging;
		_lastOrbitInputTime = state.LastOrbitInputTime;
	}

	private bool HasStateChanged(in PreviewInteractionState nextState)
	{
		return (_orbit - nextState.Orbit).sqrMagnitude > 0.000001f
			|| (_targetOrbit - nextState.TargetOrbit).sqrMagnitude > 0.000001f
			|| Mathf.Abs(_distance - nextState.Distance) > 0.0001f
			|| Mathf.Abs(_targetDistance - nextState.TargetDistance) > 0.0001f
			|| (_orbitAngularVelocity - nextState.AngularVelocity).sqrMagnitude > 0.000001f
			|| _isOrbitDragging != nextState.IsOrbitDragging;
	}

	private PreviewInteractionPolicy BuildOrbitInteractionPolicy()
	{
		return new PreviewInteractionPolicy(
			canAdjustPitch: null,
			onManualInteraction: () => { _turntableActive = false; },
			pitchMin: -89f,
			pitchMax: 89f,
			minDistance: 0.35f,
			maxDistance: 500f,
			zoomScrollFactor: 0.03f);
	}

	private PreviewInteractionPolicy BuildZoomInteractionPolicy()
	{
		return new PreviewInteractionPolicy(
			canAdjustPitch: null,
			onManualInteraction: null,
			pitchMin: -89f,
			pitchMax: 89f,
			minDistance: 0.35f,
			maxDistance: 500f,
			zoomScrollFactor: 0.03f);
	}

	private PreviewInteractionPolicy BuildTickInteractionPolicy()
	{
		return new PreviewInteractionPolicy(
			canAdjustPitch: null,
			onManualInteraction: null,
			pitchMin: -89f,
			pitchMax: 89f,
			minDistance: 0.35f,
			maxDistance: 500f,
			zoomScrollFactor: 0.03f);
	}

	private void RenderPreview(Rect previewRect, Material material, int viewWidth, int viewHeight)
	{
		if (_previewCamera == null || _previewMeshRenderer == null || _previewMeshFilter == null || material == null)
			return;

		float pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
		int rtWidth = Mathf.Max(1, Mathf.RoundToInt(viewWidth * pixelsPerPoint));
		int rtHeight = Mathf.Max(1, Mathf.RoundToInt(viewHeight * pixelsPerPoint));
		EnsureRenderTexture(rtWidth, rtHeight);

		PreviewPipelineContext pipelineContext = GetMaterialPreviewPipelineContext();
		string pipelineLabel = GetPipelineDisplayLabel(pipelineContext);
		PreviewFeatureState featureState = BuildFeatureState(pipelineContext);
		PreviewFeatureGuardResult guard = EvaluateFeatureGuard(pipelineContext, featureState);
		IPreviewRenderStrategy strategy = PreviewRenderStrategyFactory.GetStrategy(pipelineContext.Kind);
		MaterialRenderResolution renderResolution = MaterialRenderCompatibilityUtility.ResolveMaterial(material);
		Material renderMaterial = renderResolution.RenderMaterial != null
			? renderResolution.RenderMaterial
			: material;
		bool fallbackRendered = renderResolution.UsesFallback;
		_showUnsupportedMaterialOverlay = false;
		_unsupportedMaterialOverlayText = string.Empty;
		if (renderResolution.PreflightUnsupported)
		{
			SetUnsupportedMaterialOverlayText(pipelineLabel);
			if (!string.IsNullOrEmpty(renderResolution.Reason))
			{
				LogDiagnosticOnce(
					$"material-preview-preflight-fallback:{material.GetInstanceID()}:{pipelineContext.Kind}",
					$"Material '{material.name}' uses fallback preview material: {renderResolution.Reason}");
			}
		}

		float aspect = (float)_rtWidth / _rtHeight;
		PreviewCameraRenderInput liveCameraInput = BuildCameraRenderInput(aspect, _orbit, guard.Use2DCompatibilityMode);
		strategy.ConfigureCamera(_previewCamera, liveCameraInput);
		RebuildGridIfNeeded();

		Material skyboxMaterial = null;
		bool skyboxReady = guard.SkyboxEnabled && TryGetRenderableSkyboxMaterial(RenderSettings.skybox, out skyboxMaterial);

		RenderMaterialPass(renderMaterial, featureState, guard, strategy, skyboxReady, skyboxMaterial);

		if (!fallbackRendered)
		{
			_showUnsupportedMaterialOverlay = false;
			_unsupportedMaterialOverlayText = string.Empty;
		}

		_lastPreviewTexture = _previewRT;
		_previewDirty = false;
		_lastRenderedViewWidth = viewWidth;
		_lastRenderedViewHeight = viewHeight;
	}

	private PreviewCameraRenderInput BuildCameraRenderInput(float aspect, Vector2 orbit, bool use2DCompatibilityMode)
	{
		return new PreviewCameraRenderInput(
			aspect,
			ImprovedPreviewSettings.Fov,
			_distance,
			orbit,
			_pivot,
			ImprovedPreviewSettings.BgColor,
			use2DCompatibilityMode);
	}

	private void RenderMaterialPass(
		Material materialToRender,
		PreviewFeatureState featureState,
		PreviewFeatureGuardResult guard,
		IPreviewRenderStrategy strategy,
		bool skyboxReady,
		Material skyboxMaterial)
	{
		if (materialToRender == null || _previewMeshRenderer == null || _previewCamera == null)
			return;

		ConfigureLights(guard.LightingEnabled);
		_previewMeshRenderer.sharedMaterial = materialToRender;
		bool forceGridFallback = featureState.SkyboxRequested && !skyboxReady && !guard.Use2DCompatibilityMode;
		_previewCamera.clearFlags = skyboxReady ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;

		GetEnabledSceneLights(_enabledSceneLights);
		foreach (Light sceneLight in _enabledSceneLights)
			sceneLight.enabled = false;

		using var renderSettingsScope = PreviewEnvironmentRenderer.BeginRenderSettingsScope(
			guard.Use2DCompatibilityMode,
			guard.LightingEnabled,
			ImprovedPreviewSettings.AmbientColor,
			guard.ReflectionEnabled,
			ImprovedPreviewSettings.ReflectionCubemap,
			skyboxReady ? skyboxMaterial : null);

		try
		{
			if ((guard.GridEnabled || forceGridFallback) && _gridMesh != null && _gridMat != null)
			{
				Graphics.DrawMesh(
					_gridMesh,
					guard.ShouldDrawGridAs3D
						? Matrix4x4.TRS(WorldOffset + new Vector3(0f, -0.001f, 0f), Quaternion.identity, Vector3.one)
						: Matrix4x4.TRS(WorldOffset, Quaternion.Euler(90f, 0f, 0f), Vector3.one),
					_gridMat,
					PreviewLayer,
					_previewCamera);
			}

			strategy.Render(this, _previewCamera, _previewRT, ImprovedPreviewSettings.BgColor, skyboxReady);
		}
		finally
		{
			if (_light0 != null) _light0.enabled = false;
			if (_light1 != null) _light1.enabled = false;
			if (_light2 != null) _light2.enabled = false;

			foreach (Light sceneLight in _enabledSceneLights)
				sceneLight.enabled = true;
		}
	}

	private void EnsureRenderTexture(int width, int height)
	{
		int antiAliasing = Mathf.Max(1, ImprovedPreviewSettings.AntiAliasing);
		bool needsResize = _previewRT == null
			|| _previewRT.antiAliasing != antiAliasing
			|| _rtWidth != width
			|| _rtHeight != height;
		if (!needsResize)
			return;

		if (_previewRT != null)
		{
			_previewRT.Release();
			DestroyImmediate(_previewRT);
		}

		_previewRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
		{
			antiAliasing = antiAliasing,
			filterMode = FilterMode.Bilinear
		};
		_previewRT.Create();
		_rtWidth = width;
		_rtHeight = height;
	}

	private void ConfigureLights(bool lightingEnabled)
	{
		if (_light0 == null || _light1 == null || _light2 == null)
			return;

		EnsureLightRigInitialized();

		Quaternion lightRigRotation = Quaternion.FromToRotation(GetDefaultLightRigDirection(), _lightRigDirectionWorld);
		Vector3 keyPos = _pivot + lightRigRotation * ImprovedPreviewSettings.KeyPosition;
		Vector3 fillPos = _pivot + lightRigRotation * ImprovedPreviewSettings.FillPosition;

		_light0.transform.position = keyPos;
		_light1.transform.position = fillPos;
		_light0.intensity = lightingEnabled ? ImprovedPreviewSettings.KeyIntensity : 0f;
		_light1.intensity = lightingEnabled ? ImprovedPreviewSettings.FillIntensity : 0f;

		if ((_pivot - keyPos).sqrMagnitude > 0.0001f)
			_light0.transform.LookAt(_pivot);
		if ((_pivot - fillPos).sqrMagnitude > 0.0001f)
			_light1.transform.LookAt(_pivot);

		_light2.intensity = lightingEnabled && ImprovedPreviewSettings.RimLightEnabled ? ImprovedPreviewSettings.RimLightIntensity : 0f;
		_light2.color = ImprovedPreviewSettings.RimLightColor;
		_light2.transform.rotation = Quaternion.Euler(ImprovedPreviewSettings.RimLightRotation.x, ImprovedPreviewSettings.RimLightRotation.y, 0f);

		_light0.enabled = _light0.intensity > 0f;
		_light1.enabled = _light1.intensity > 0f;
		_light2.enabled = _light2.intensity > 0f;
	}

	private void GetEnabledSceneLights(List<Light> result)
	{
		result.Clear();
		Light[] sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
		for (int i = 0; i < sceneLights.Length; i++)
		{
			Light light = sceneLights[i];
			if (light == null || !light.enabled)
				continue;
			if (light == _light0 || light == _light1 || light == _light2)
				continue;
			result.Add(light);
		}
	}

	private void HandleLightWidgetInput(Rect previewRect)
	{
		if (!GetMaterialPreviewPipelineContext().Capabilities.SupportsLightRig)
			return;

		Rect sphereRect = GetLightWidgetSphereRect(previewRect);
		Event evt = Event.current;
		int controlId = GUIUtility.GetControlID("MaterialPreviewLightWidget".GetHashCode(), FocusType.Passive, sphereRect);

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

	private void DrawLightWidgetOverlay(Rect previewRect)
	{
		if (!GetMaterialPreviewPipelineContext().Capabilities.SupportsLightRig)
			return;

		Rect sphereRect = GetLightWidgetSphereRect(previewRect);
		Texture2D sphereTexture = GetLightWidgetTexture();
		if (sphereTexture != null)
			GUI.DrawTexture(sphereRect, sphereTexture, ScaleMode.StretchToFill, true);
	}

	private void DrawUnsupportedMaterialOverlay(Rect previewRect)
	{
		if (!_showUnsupportedMaterialOverlay || string.IsNullOrEmpty(_unsupportedMaterialOverlayText))
			return;
		if (Event.current.type != EventType.Repaint)
			return;

		GUIStyle style = _unsupportedMaterialOverlayStyle;
		if (style == null)
		{
			style = new GUIStyle(EditorStyles.miniBoldLabel)
			{
				fontSize = 10,
				alignment = TextAnchor.MiddleLeft,
				normal = { textColor = new Color(1f, 0.72f, 0.72f, 1f) },
				padding = new RectOffset(0, 0, 0, 0)
			};
			_unsupportedMaterialOverlayStyle = style;
		}

		GUIContent content = new GUIContent(_unsupportedMaterialOverlayText);
		float maxWidth = Mathf.Max(80f, previewRect.width - 16f);
		float width = Mathf.Min(maxWidth, style.CalcSize(content).x + 14f);
		var backgroundRect = new Rect(previewRect.x + 8f, previewRect.y + 8f, width, 18f);
		EditorGUI.DrawRect(backgroundRect, new Color(0f, 0f, 0f, 0.65f));
		var labelRect = new Rect(backgroundRect.x + 6f, backgroundRect.y + 1f, backgroundRect.width - 10f, backgroundRect.height - 2f);
		GUI.Label(labelRect, content, style);
	}

	private static Rect GetLightWidgetSphereRect(Rect previewRect)
	{
		return PreviewLightWidgetSystem.GetSphereRect(previewRect, PreviewLightWidgetSystem.DefaultLayoutConfig);
	}

	private void RotateLightRigFromDrag(Vector2 delta)
	{
		if (_previewCamera == null)
			return;

		Vector3 direction = GetLightRigDirectionWorld();
		Vector3 upAxis = _previewCamera.transform.up;
		Vector3 rightAxis = _previewCamera.transform.right;
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
		Vector3 keyReference = ImprovedPreviewSettings.KeyPosition.sqrMagnitude > 0.0001f
			? ImprovedPreviewSettings.KeyPosition.normalized
			: GetDefaultLightRigDirection();
		Quaternion lightRigRotation = Quaternion.FromToRotation(keyReference, GetLightRigDirectionWorld());
		Vector3 fillPositionWorld = lightRigRotation * ImprovedPreviewSettings.FillPosition;
		Vector3 incomingLightWorld = (-fillPositionWorld).normalized;
		return GetWidgetSpaceLightDirection(incomingLightWorld);
	}

	private Vector3 GetWidgetSpaceLightDirection(Vector3 incomingLightWorld)
	{
		if (_previewCamera == null)
			return incomingLightWorld.normalized;

		return _previewCamera.transform.InverseTransformDirection(incomingLightWorld).normalized;
	}

	private void RebuildGridIfNeeded()
	{
		if (_gridMesh == null)
			return;

		float halfSize = ImprovedPreviewSettings.GridHalfSize;
		float step = ImprovedPreviewSettings.GridStep;
		float alpha = ImprovedPreviewSettings.GridAlpha;

		if (Mathf.Approximately(_cachedGridHalfSize, halfSize)
			&& Mathf.Approximately(_cachedGridStep, step)
			&& Mathf.Approximately(_cachedGridAlpha, alpha))
			return;

		_cachedGridHalfSize = halfSize;
		_cachedGridStep = step;
		_cachedGridAlpha = alpha;
		PreviewGridMeshBuilder.BuildGridMesh(_gridMesh, halfSize, step, alpha);
	}

	private static Material CreateGridMaterial()
	{
		var material = new Material(Shader.Find("Hidden/Internal-Colored"))
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		material.SetInt("_ZWrite", 0);
		material.SetInt("_Cull", 0);
		material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
		material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
		material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
		material.renderQueue = 2999;
		return material;
	}

	private bool TryGetRenderableSkyboxMaterial(Material fallbackSkybox, out Material skyboxMaterial)
	{
		skyboxMaterial = null;
		if (!_showSkybox)
			return false;

		Texture skyboxTexture = ImprovedPreviewSettings.SkyboxTexture;
		if (skyboxTexture != null)
		{
			if (skyboxTexture != _lastSkyboxTexture)
			{
				DestroyMaterialResource(ref _skyboxPreviewMat);
				_skyboxPreviewMat = CreateSkyboxMaterialFromTexture(skyboxTexture);
				_lastSkyboxTexture = skyboxTexture;
			}

			if (IsSkyboxMaterialRenderable(_skyboxPreviewMat))
			{
				skyboxMaterial = _skyboxPreviewMat;
				return true;
			}
		}

		if (IsSkyboxMaterialRenderable(fallbackSkybox))
		{
			skyboxMaterial = fallbackSkybox;
			return true;
		}

		return false;
	}

	private static bool IsSkyboxMaterialRenderable(Material material)
	{
		return material != null
			&& material.shader != null
			&& material.shader.isSupported
			&& material.passCount > 0;
	}

	private static Material CreateSkyboxMaterialFromTexture(Texture texture)
	{
		if (texture == null)
			return null;

		if (texture is Cubemap cubemap)
		{
			Shader cubemapShader = Shader.Find("Skybox/Cubemap");
			if (cubemapShader == null || !cubemapShader.isSupported)
				return null;

			var cubemapMaterial = new Material(cubemapShader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			cubemapMaterial.SetTexture("_Tex", cubemap);
			return cubemapMaterial;
		}

		Shader panoramicShader = Shader.Find("Skybox/Panoramic");
		if (panoramicShader == null || !panoramicShader.isSupported)
			return null;

		var panoramicMaterial = new Material(panoramicShader)
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		panoramicMaterial.SetTexture("_MainTex", texture);
		return panoramicMaterial;
	}

	private static void LogDiagnosticOnce(string key, string message)
	{
		if (!ImprovedPreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
			return;
		if (!LoggedDiagnosticKeys.Add(key))
			return;

		ImprovedPreviewSettings.LogDiagnostic(message);
	}

	private static string GetPipelineDisplayLabel(PreviewPipelineContext pipelineContext)
	{
		string displayLabel = PreviewFeatureGuard.ToDisplayLabel(pipelineContext.Kind);
		return !string.IsNullOrEmpty(displayLabel) ? displayLabel : pipelineContext.Label;
	}

	private void SetUnsupportedMaterialOverlayText(string pipelineLabel)
	{
		_showUnsupportedMaterialOverlay = true;
		_unsupportedMaterialOverlayText = $"Unsupported shader for {pipelineLabel}";
	}

	private static bool DrawPreviewToolbarButton(Rect rect, bool active, string fallbackText, string tooltip, params string[] iconNames)
	{
		return DrawPreviewToolbarButton(rect, active, fallbackText, tooltip, true, iconNames);
	}

	private static bool DrawPreviewToolbarButton(Rect rect, bool active, string fallbackText, string tooltip, bool isEnabled, params string[] iconNames)
	{
		bool clicked = isEnabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
		if (clicked)
			GUI.changed = true;

		GUIContent content = GetIconContent(fallbackText, tooltip, iconNames);
		bool hovered = rect.Contains(Event.current.mousePosition);

		Color background = ImprovedEditorTheme.GetToolbarButtonBackground(active, hovered);
		Color border = ImprovedEditorTheme.GetToolbarButtonBorder(active);
		if (!isEnabled)
		{
			background *= new Color(1f, 1f, 1f, 0.5f);
			border *= new Color(1f, 1f, 1f, 0.65f);
		}

		EditorGUI.DrawRect(rect, background);
		EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
		EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

		if (content.image != null)
		{
			float iconSize = Mathf.Min(rect.width, rect.height) - 8f;
			Rect iconRect = new Rect(
				Mathf.Round(rect.center.x - iconSize * 0.5f),
				Mathf.Round(rect.center.y - iconSize * 0.5f),
				iconSize,
				iconSize);
			Color previousColor = GUI.color;
			Color tint = ImprovedEditorTheme.GetToolbarIconTint(active);
			if (!isEnabled)
				tint *= new Color(1f, 1f, 1f, 0.45f);
			GUI.color = tint;
			GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
			GUI.color = previousColor;
		}
		else
		{
			Color previousColor = GUI.color;
			if (!isEnabled)
				GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, previousColor.a * 0.55f);
			GUI.Label(rect, content.text, EditorStyles.miniLabel);
			GUI.color = previousColor;
		}

		if (!string.IsNullOrEmpty(content.tooltip) && rect.Contains(Event.current.mousePosition))
			GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));

		return clicked;
	}

	private static GUIContent GetIconContent(string fallbackText, string tooltip, params string[] iconNames)
	{
		for (int i = 0; i < iconNames.Length; i++)
		{
			string iconName = iconNames[i];
			if (string.IsNullOrEmpty(iconName))
				continue;

			if (!IconCache.TryGetValue(iconName, out Texture icon))
			{
				GUIContent iconContent = EditorGUIUtility.IconContent(iconName);
				icon = iconContent != null ? iconContent.image : EditorGUIUtility.FindTexture(iconName);
				IconCache[iconName] = icon;
			}

			if (icon != null)
				return new GUIContent(icon, tooltip);
		}

		return new GUIContent(fallbackText, tooltip);
	}

	private void EnsureLightRigInitialized()
	{
		if (_hasCustomLightRigDirection)
			return;

		_lightRigDirectionWorld = GetDefaultLightRigDirection();
		_hasCustomLightRigDirection = true;
	}

	private Vector3 GetLightRigDirectionWorld()
	{
		EnsureLightRigInitialized();
		return _lightRigDirectionWorld;
	}

	private void SetLightRigDirectionWorld(Vector3 direction)
	{
		if (direction.sqrMagnitude < 0.0001f)
			return;

		_lightRigDirectionWorld = direction.normalized;
		_hasCustomLightRigDirection = true;
		RequestPreviewRepaint();
	}

	private void ResetLightRigDirection()
	{
		_lightRigDirectionWorld = GetDefaultLightRigDirection();
		_hasCustomLightRigDirection = true;
	}

	private static Vector3 GetDefaultLightRigDirection()
	{
		Vector3 key = ImprovedPreviewSettings.KeyPosition;
		return key.sqrMagnitude > 0.0001f
			? key.normalized
			: new Vector3(-0.35f, 0.55f, -0.75f).normalized;
	}

	private void RequestPreviewRepaint()
	{
		_previewDirty = true;
		Repaint();
	}

	private void EnsureContinuousRepaintLoop()
	{
		if (_continuousRepaintLoopSubscribed)
			return;

		_continuousRepaintLoopSubscribed = true;
		_nextContinuousRepaintTime = -1d;
		EditorApplication.update += ContinuousRepaintLoopUpdate;
	}

	private void StopContinuousRepaintLoop()
	{
		if (!_continuousRepaintLoopSubscribed)
			return;

		_continuousRepaintLoopSubscribed = false;
		EditorApplication.update -= ContinuousRepaintLoopUpdate;
		_nextContinuousRepaintTime = -1d;
	}

	private void ContinuousRepaintLoopUpdate()
	{
		if (!_continuousRepaintLoopSubscribed)
			return;

		if (this == null
			|| target == null
			|| !ImprovedPreviewSettings.Active
			|| !ImprovedPreviewSettings.IsPreviewTypeEnabled(PreviewTypeKey))
		{
			StopContinuousRepaintLoop();
			return;
		}

		double now = EditorApplication.timeSinceStartup;
		if (_nextContinuousRepaintTime >= 0d && now < _nextContinuousRepaintTime)
			return;

		_nextContinuousRepaintTime = now + GetContinuousPreviewFrameIntervalSeconds();
		Repaint();
	}

	private static double GetContinuousPreviewFrameIntervalSeconds()
	{
		return 1d / Mathf.Max(1, ImprovedPreviewSettings.PreviewRefreshUpdatesPerSecond);
	}

	private void CleanupPreviewResources()
	{
		DestroyMeshResource(ref _cubeMesh);
		DestroyMeshResource(ref _sphereMesh);
		DestroyMeshResource(ref _torusMesh);
		DestroyMeshResource(ref _quadMesh);
		DestroyMeshResource(ref _gridMesh);
		DestroyMaterialResource(ref _gridMat);
		DestroyMaterialResource(ref _skyboxPreviewMat);
		_lastSkyboxTexture = null;
		_showUnsupportedMaterialOverlay = false;
		_unsupportedMaterialOverlayText = string.Empty;
		_unsupportedMaterialOverlayStyle = null;
		_cachedGridHalfSize = -1f;
		_cachedGridStep = -1f;
		_cachedGridAlpha = -1f;
		PreviewLightWidgetSystem.DisposeTexture(ref _lightWidgetCache);

		if (_previewRT != null)
		{
			_previewRT.Release();
			DestroyImmediate(_previewRT);
		}
		_previewRT = null;
		_lastPreviewTexture = null;
		_rtWidth = 0;
		_rtHeight = 0;
		_lastRenderedViewWidth = 0;
		_lastRenderedViewHeight = 0;
		_lastAppliedRevision = -1;
		_lastDefaultMeshMaterialInstanceId = int.MinValue;

		DestroyPreviewObject(_previewMeshObject);
		DestroyPreviewObject(_previewCamera != null ? _previewCamera.gameObject : null);
		DestroyPreviewObject(_light0 != null ? _light0.gameObject : null);
		DestroyPreviewObject(_light1 != null ? _light1.gameObject : null);
		DestroyPreviewObject(_light2 != null ? _light2.gameObject : null);

		_previewMeshObject = null;
		_previewMeshFilter = null;
		_previewMeshRenderer = null;
		_previewCamera = null;
		_light0 = null;
		_light1 = null;
		_light2 = null;
		_enabledSceneLights.Clear();
		_turntableActive = false;
		_lastTurntableRepaintTime = -1d;
		_orbitAngularVelocity = Vector2.zero;
		_isOrbitDragging = false;
		_lastOrbitInputTime = -1d;
		_lastInteractionUpdateTime = -1d;
		_nextContinuousRenderTime = -1d;
		_nextContinuousRepaintTime = -1d;
		_previewDirty = true;
		_hasCustomLightRigDirection = false;
	}

	private static void DestroyPreviewObject(GameObject previewObject)
	{
		if (previewObject != null)
			DestroyImmediate(previewObject);
	}

	private static void DestroyMeshResource(ref Mesh mesh)
	{
		if (mesh == null)
			return;

		DestroyImmediate(mesh);
		mesh = null;
	}

	private static void DestroyMaterialResource(ref Material material)
	{
		if (material == null)
			return;

		DestroyImmediate(material);
		material = null;
	}

	private static Mesh CreatePrimitiveMesh(PrimitiveType primitiveType)
	{
		GameObject primitive = GameObject.CreatePrimitive(primitiveType);
		Mesh primitiveMesh = null;
		MeshFilter filter = primitive.GetComponent<MeshFilter>();
		if (filter != null && filter.sharedMesh != null)
			primitiveMesh = Object.Instantiate(filter.sharedMesh);
		Object.DestroyImmediate(primitive);

		if (primitiveMesh != null)
			primitiveMesh.hideFlags = HideFlags.HideAndDontSave;
		return primitiveMesh;
	}

	private static Mesh CreateTorusMesh(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
	{
		var mesh = new Mesh
		{
			name = "___MaterialPreviewTorus___",
			hideFlags = HideFlags.HideAndDontSave
		};

		int rows = minorSegments + 1;
		int vertexCount = (majorSegments + 1) * rows;
		var vertices = new Vector3[vertexCount];
		var normals = new Vector3[vertexCount];
		var uvs = new Vector2[vertexCount];
		var triangles = new int[majorSegments * minorSegments * 6];

		int vertexIndex = 0;
		for (int major = 0; major <= majorSegments; major++)
		{
			float u = major / (float)majorSegments * Mathf.PI * 2f;
			Vector3 majorDir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
			Vector3 center = majorDir * majorRadius;

			for (int minor = 0; minor <= minorSegments; minor++)
			{
				float v = minor / (float)minorSegments * Mathf.PI * 2f;
				float cosV = Mathf.Cos(v);
				float sinV = Mathf.Sin(v);

				Vector3 normal = new Vector3(majorDir.x * cosV, sinV, majorDir.z * cosV).normalized;
				vertices[vertexIndex] = center + normal * minorRadius;
				normals[vertexIndex] = normal;
				uvs[vertexIndex] = new Vector2(major / (float)majorSegments, minor / (float)minorSegments);
				vertexIndex++;
			}
		}

		int triangleIndex = 0;
		for (int major = 0; major < majorSegments; major++)
		{
			for (int minor = 0; minor < minorSegments; minor++)
			{
				int a = major * rows + minor;
				int b = (major + 1) * rows + minor;
				int c = b + 1;
				int d = a + 1;

				// Wind triangles so front faces point outward.
				triangles[triangleIndex++] = a;
				triangles[triangleIndex++] = d;
				triangles[triangleIndex++] = b;
				triangles[triangleIndex++] = d;
				triangles[triangleIndex++] = c;
				triangles[triangleIndex++] = b;
			}
		}

		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uvs;
		mesh.triangles = triangles;
		mesh.RecalculateBounds();
		return mesh;
	}

	private static Mesh CreateDoubleSidedQuadMesh(float size)
	{
		float halfSize = Mathf.Max(0.01f, size) * 0.5f;
		var mesh = new Mesh
		{
			name = "___MaterialPreviewQuad___",
			hideFlags = HideFlags.HideAndDontSave
		};

		var vertices = new[]
		{
			new Vector3(-halfSize, -halfSize, 0f),
			new Vector3(halfSize, -halfSize, 0f),
			new Vector3(halfSize, halfSize, 0f),
			new Vector3(-halfSize, halfSize, 0f),
			new Vector3(-halfSize, -halfSize, 0f),
			new Vector3(halfSize, -halfSize, 0f),
			new Vector3(halfSize, halfSize, 0f),
			new Vector3(-halfSize, halfSize, 0f),
		};

		var normals = new[]
		{
			Vector3.forward,
			Vector3.forward,
			Vector3.forward,
			Vector3.forward,
			Vector3.back,
			Vector3.back,
			Vector3.back,
			Vector3.back,
		};

		var uvs = new[]
		{
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(0f, 1f),
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(0f, 1f),
		};

		var triangles = new[]
		{
			0, 1, 2, 0, 2, 3,
			6, 5, 4, 7, 6, 4
		};

		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uvs;
		mesh.triangles = triangles;
		mesh.RecalculateBounds();
		return mesh;
	}
}

}
#endif
