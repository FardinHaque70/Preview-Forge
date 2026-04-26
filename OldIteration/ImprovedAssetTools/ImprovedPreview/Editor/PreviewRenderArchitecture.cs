#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FardinHaque.ImprovedAssetTools.Editor
{

internal enum PreviewRenderPipelineKind
{
	BuiltIn,
	Urp3D,
	Urp2D,
	UnknownSrp
}

internal struct PreviewPipelineCapabilities
{
	public bool SupportsSkybox;
	public bool SupportsReflectionMap;
	public bool SupportsPerspectiveOrbit;
	public bool Supports3DGridOrientation;
	public bool SupportsLightRig;

	public static PreviewPipelineCapabilities BuiltIn()
	{
		return new PreviewPipelineCapabilities
		{
			SupportsSkybox = true,
			SupportsReflectionMap = true,
			SupportsPerspectiveOrbit = true,
			Supports3DGridOrientation = true,
			SupportsLightRig = true
		};
	}

	public static PreviewPipelineCapabilities Urp3D() => BuiltIn();

	public static PreviewPipelineCapabilities Urp2D()
	{
		return new PreviewPipelineCapabilities
		{
			SupportsSkybox = false,
			SupportsReflectionMap = false,
			SupportsPerspectiveOrbit = false,
			Supports3DGridOrientation = false,
			SupportsLightRig = false
		};
	}

	public static PreviewPipelineCapabilities UnknownSrp()
	{
		return new PreviewPipelineCapabilities
		{
			SupportsSkybox = false,
			SupportsReflectionMap = false,
			SupportsPerspectiveOrbit = true,
			Supports3DGridOrientation = true,
			SupportsLightRig = true
		};
	}
}

internal readonly struct PreviewPipelineContext
{
	public readonly PreviewRenderPipelineKind Kind;
	public readonly PreviewPipelineCapabilities Capabilities;
	public readonly string Label;
	public readonly bool UseEditor2DFallback;

	public PreviewPipelineContext(
		PreviewRenderPipelineKind kind,
		PreviewPipelineCapabilities capabilities,
		string label,
		bool useEditor2DFallback = false)
	{
		Kind = kind;
		Capabilities = capabilities;
		Label = string.IsNullOrEmpty(label) ? "Unknown" : label;
		UseEditor2DFallback = useEditor2DFallback;
	}

	public bool IsUrp2D => Kind == PreviewRenderPipelineKind.Urp2D;
	public bool IsEffectively2D => IsUrp2D || UseEditor2DFallback;
}

internal static class PreviewPipelineContextResolver
{
	private static readonly Dictionary<int, PreviewPipelineContext> ContextCache = new();
	private static readonly HashSet<string> LoggedDiagnosticKeys = new();

	public static PreviewPipelineContext GetCurrentContext()
	{
		int behaviorModeHash = GetEditorBehaviorModeHash();
		RenderPipelineAsset renderPipeline = GraphicsSettings.currentRenderPipeline;
		if (renderPipeline == null)
		{
			const PreviewRenderPipelineKind builtInKind = PreviewRenderPipelineKind.BuiltIn;
			bool useEditor2DFallback = ShouldUseEditor2DFallback(builtInKind);
			PreviewPipelineCapabilities builtInCapabilities = GetEffectiveCapabilities(
				builtInKind,
				PreviewPipelineCapabilities.BuiltIn(),
				useEditor2DFallback);
			return new PreviewPipelineContext(
				builtInKind,
				builtInCapabilities,
				"Built-in",
				useEditor2DFallback);
		}

		int pipelineId = renderPipeline.GetInstanceID();
		int contextCacheKey = ComputeContextCacheKey(pipelineId, behaviorModeHash);
		if (ContextCache.TryGetValue(contextCacheKey, out PreviewPipelineContext cachedContext))
			return cachedContext;

		string label = BuildRenderPipelineLabel(renderPipeline);
		PreviewRenderPipelineKind kind = PreviewRenderPipelineKind.UnknownSrp;
		PreviewPipelineCapabilities detectedCapabilities = PreviewPipelineCapabilities.UnknownSrp();

		if (IsUniversalRenderPipeline(renderPipeline))
		{
			if (TryGetDefaultRendererData(renderPipeline, out UnityEngine.Object rendererData) && Is2DRendererData(rendererData))
			{
				kind = PreviewRenderPipelineKind.Urp2D;
				detectedCapabilities = PreviewPipelineCapabilities.Urp2D();
			}
			else
			{
				kind = PreviewRenderPipelineKind.Urp3D;
				detectedCapabilities = PreviewPipelineCapabilities.Urp3D();
			}
		}
		else
		{
			kind = PreviewRenderPipelineKind.UnknownSrp;
			detectedCapabilities = PreviewPipelineCapabilities.UnknownSrp();
		}

		bool useFallback = ShouldUseEditor2DFallback(kind);
		PreviewPipelineCapabilities capabilities = GetEffectiveCapabilities(kind, detectedCapabilities, useFallback);
		var context = new PreviewPipelineContext(kind, capabilities, label, useFallback);
		ContextCache[contextCacheKey] = context;

		LogDiagnosticOnce(
			$"preview-pipeline:{label}:{kind}:{(useFallback ? "editor2d" : "native")}",
			useFallback
				? $"Preview pipeline detected: {label} ({kind}) with Editor Default Behaviour Mode 2D fallback."
				: $"Preview pipeline detected: {label} ({kind}).");

		return context;
	}

	private static int ComputeContextCacheKey(int pipelineId, int behaviorModeHash)
	{
		unchecked
		{
			return (pipelineId * 397) ^ behaviorModeHash;
		}
	}

	private static int GetEditorBehaviorModeHash()
	{
#if UNITY_2018_1_OR_NEWER
		return (int)EditorSettings.defaultBehaviorMode;
#else
		return 0;
#endif
	}

	private static bool IsEditorDefaultBehavior2D()
	{
#if UNITY_2018_1_OR_NEWER
		return EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D;
#else
		return false;
#endif
	}

	private static bool ShouldUseEditor2DFallback(PreviewRenderPipelineKind kind)
	{
		if (kind != PreviewRenderPipelineKind.BuiltIn && kind != PreviewRenderPipelineKind.UnknownSrp)
			return false;

		return IsEditorDefaultBehavior2D();
	}

	private static PreviewPipelineCapabilities GetEffectiveCapabilities(
		PreviewRenderPipelineKind kind,
		PreviewPipelineCapabilities detectedCapabilities,
		bool useEditor2DFallback)
	{
		if (!useEditor2DFallback)
			return detectedCapabilities;

		if (kind == PreviewRenderPipelineKind.BuiltIn || kind == PreviewRenderPipelineKind.UnknownSrp)
			return PreviewPipelineCapabilities.Urp2D();

		return detectedCapabilities;
	}

	private static bool IsUniversalRenderPipeline(RenderPipelineAsset renderPipeline)
	{
		if (renderPipeline == null)
			return false;

		string fullName = renderPipeline.GetType().FullName ?? string.Empty;
		return fullName.IndexOf("UniversalRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0
			|| fullName.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool TryGetDefaultRendererData(RenderPipelineAsset renderPipeline, out UnityEngine.Object rendererData)
	{
		rendererData = null;
		if (renderPipeline == null)
			return false;

		var serializedPipeline = new SerializedObject(renderPipeline);
		SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
		SerializedProperty defaultRendererIndexProperty = serializedPipeline.FindProperty("m_DefaultRendererIndex");
		if (rendererList == null || !rendererList.isArray || rendererList.arraySize <= 0)
			return false;

		int defaultRendererIndex = defaultRendererIndexProperty != null
			? Mathf.Clamp(defaultRendererIndexProperty.intValue, 0, rendererList.arraySize - 1)
			: 0;
		SerializedProperty rendererEntry = rendererList.GetArrayElementAtIndex(defaultRendererIndex);
		rendererData = rendererEntry != null ? rendererEntry.objectReferenceValue : null;
		return rendererData != null;
	}

	private static bool Is2DRendererData(UnityEngine.Object rendererData)
	{
		if (rendererData == null)
			return false;

		Type rendererType = rendererData.GetType();
		string typeName = rendererType.FullName ?? rendererType.Name;
		return typeName.IndexOf("Renderer2DData", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string BuildRenderPipelineLabel(RenderPipelineAsset renderPipeline)
	{
		if (renderPipeline == null)
			return "Built-in";

		string pipelineLabel = renderPipeline.name;
		if (TryGetDefaultRendererData(renderPipeline, out UnityEngine.Object rendererData))
			pipelineLabel = $"{renderPipeline.name}/{rendererData.GetType().Name}";
		return pipelineLabel;
	}

	private static void LogDiagnosticOnce(string key, string message)
	{
		if (!ImprovedPreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
			return;
		if (!LoggedDiagnosticKeys.Add(key))
			return;

		ImprovedPreviewSettings.LogDiagnostic(message);
	}
}

internal enum PreviewFeature
{
	Skybox,
	ReflectionMap,
	PerspectiveOrbit,
	Grid3DOrientation,
	LightRig
}

internal readonly struct PreviewFeatureState
{
	public readonly bool SkyboxRequested;
	public readonly bool ReflectionRequested;
	public readonly bool GridRequested;
	public readonly bool LightingRequested;

	public PreviewFeatureState(
		bool skyboxRequested,
		bool reflectionRequested,
		bool gridRequested,
		bool lightingRequested)
	{
		SkyboxRequested = skyboxRequested;
		ReflectionRequested = reflectionRequested;
		GridRequested = gridRequested;
		LightingRequested = lightingRequested;
	}
}

internal readonly struct PreviewFeatureGuardResult
{
	private readonly Dictionary<PreviewFeature, string> _disabledReasons;

	public readonly bool Use2DCompatibilityMode;
	public readonly bool SkyboxEnabled;
	public readonly bool ReflectionEnabled;
	public readonly bool GridEnabled;
	public readonly bool LightingEnabled;
	public readonly bool ShouldDrawGridAs3D;

	public PreviewFeatureGuardResult(
		bool use2DCompatibilityMode,
		bool skyboxEnabled,
		bool reflectionEnabled,
		bool gridEnabled,
		bool lightingEnabled,
		bool shouldDrawGridAs3D,
		Dictionary<PreviewFeature, string> disabledReasons)
	{
		Use2DCompatibilityMode = use2DCompatibilityMode;
		SkyboxEnabled = skyboxEnabled;
		ReflectionEnabled = reflectionEnabled;
		GridEnabled = gridEnabled;
		LightingEnabled = lightingEnabled;
		ShouldDrawGridAs3D = shouldDrawGridAs3D;
		_disabledReasons = disabledReasons ?? new Dictionary<PreviewFeature, string>();
	}

	public bool TryGetDisabledReason(PreviewFeature feature, out string reason)
	{
		if (_disabledReasons != null && _disabledReasons.TryGetValue(feature, out reason))
			return !string.IsNullOrEmpty(reason);
		reason = null;
		return false;
	}
}

internal static class PreviewFeatureGuard
{
	public static PreviewFeatureGuardResult Evaluate(
		PreviewPipelineContext context,
		bool wants2DCompatibilityMode,
		bool force2DCompatibilityMode,
		bool shouldDrawGridIn2DMode,
		PreviewFeatureState requested,
		bool prefer3DGridOrientationWhenNotIn2DCompatibilityMode = false)
	{
		bool use2DCompatibilityMode = force2DCompatibilityMode || (wants2DCompatibilityMode && context.IsEffectively2D);
		PreviewPipelineCapabilities capabilities = context.Capabilities;
		var disabledReasons = new Dictionary<PreviewFeature, string>();

		bool skyboxEnabled = requested.SkyboxRequested && capabilities.SupportsSkybox;
		if (requested.SkyboxRequested && !capabilities.SupportsSkybox)
			disabledReasons[PreviewFeature.Skybox] = $"Skybox is not supported in {ToDisplayLabel(context.Kind)} mode.";

		bool reflectionEnabled = requested.ReflectionRequested && capabilities.SupportsReflectionMap;
		if (requested.ReflectionRequested && !capabilities.SupportsReflectionMap)
			disabledReasons[PreviewFeature.ReflectionMap] = $"Reflection map is not supported in {ToDisplayLabel(context.Kind)} mode.";

		bool lightingEnabled = requested.LightingRequested && capabilities.SupportsLightRig;
		if (requested.LightingRequested && !capabilities.SupportsLightRig)
			disabledReasons[PreviewFeature.LightRig] = $"Custom light rig is not supported in {ToDisplayLabel(context.Kind)} mode.";

		bool supports3DGridOrientation = capabilities.Supports3DGridOrientation;
		bool shouldDrawGridAs3D = !use2DCompatibilityMode
			&& (supports3DGridOrientation || prefer3DGridOrientationWhenNotIn2DCompatibilityMode);
		bool gridEnabled = requested.GridRequested && (use2DCompatibilityMode ? shouldDrawGridIn2DMode : true);
		if (requested.GridRequested && use2DCompatibilityMode && !shouldDrawGridIn2DMode)
			disabledReasons[PreviewFeature.Grid3DOrientation] = $"Grid is disabled by this preview in {ToDisplayLabel(context.Kind)} mode.";

		return new PreviewFeatureGuardResult(
			use2DCompatibilityMode,
			skyboxEnabled,
			reflectionEnabled,
			gridEnabled,
			lightingEnabled,
			shouldDrawGridAs3D,
			disabledReasons);
	}

	public static string ToDisplayLabel(PreviewRenderPipelineKind kind)
	{
		return kind switch
		{
			PreviewRenderPipelineKind.BuiltIn => "Built-in",
			PreviewRenderPipelineKind.Urp3D => "URP 3D",
			PreviewRenderPipelineKind.Urp2D => "URP 2D",
			_ => "SRP Fallback"
		};
	}
}

internal interface IPreviewRenderHost
{
	string HostName { get; }
	bool PreserveCameraColorBuffer { get; }
}

internal readonly struct PreviewCameraRenderInput
{
	public readonly float Aspect;
	public readonly float Fov;
	public readonly float Distance;
	public readonly Vector2 Orbit;
	public readonly Vector3 Pivot;
	public readonly Color BackgroundColor;
	public readonly bool Use2DCompatibilityMode;

	public PreviewCameraRenderInput(
		float aspect,
		float fov,
		float distance,
		Vector2 orbit,
		Vector3 pivot,
		Color backgroundColor,
		bool use2DCompatibilityMode)
	{
		Aspect = aspect;
		Fov = fov;
		Distance = distance;
		Orbit = orbit;
		Pivot = pivot;
		BackgroundColor = backgroundColor;
		Use2DCompatibilityMode = use2DCompatibilityMode;
	}
}

internal interface IPreviewRenderStrategy
{
	PreviewRenderPipelineKind Kind { get; }
	void ConfigureCamera(Camera camera, PreviewCameraRenderInput input);
	void Render(IPreviewRenderHost host, Camera camera, RenderTexture target, Color clearColor, bool useSkybox);
}

internal sealed class BuiltInPreviewRenderStrategy : IPreviewRenderStrategy
{
	public PreviewRenderPipelineKind Kind => PreviewRenderPipelineKind.BuiltIn;

	public void ConfigureCamera(Camera camera, PreviewCameraRenderInput input)
	{
		PreviewRenderStrategyUtility.ConfigureCameraCommon(camera, input, forceOrthographic: false);
	}

	public void Render(IPreviewRenderHost host, Camera camera, RenderTexture target, Color clearColor, bool useSkybox)
	{
		PreviewRenderStrategyUtility.RenderCamera(host, camera, target, clearColor, useSkybox, "Preview_BuiltIn_Clear");
	}
}

internal sealed class Urp3DPreviewRenderStrategy : IPreviewRenderStrategy
{
	public PreviewRenderPipelineKind Kind => PreviewRenderPipelineKind.Urp3D;

	public void ConfigureCamera(Camera camera, PreviewCameraRenderInput input)
	{
		PreviewRenderStrategyUtility.ConfigureCameraCommon(camera, input, forceOrthographic: false);
	}

	public void Render(IPreviewRenderHost host, Camera camera, RenderTexture target, Color clearColor, bool useSkybox)
	{
		PreviewRenderStrategyUtility.RenderCamera(host, camera, target, clearColor, useSkybox, "Preview_URP3D_Clear");
	}
}

internal sealed class Urp2DPreviewRenderStrategy : IPreviewRenderStrategy
{
	public PreviewRenderPipelineKind Kind => PreviewRenderPipelineKind.Urp2D;

	public void ConfigureCamera(Camera camera, PreviewCameraRenderInput input)
	{
		PreviewRenderStrategyUtility.ConfigureCameraCommon(camera, input, forceOrthographic: false);
	}

	public void Render(IPreviewRenderHost host, Camera camera, RenderTexture target, Color clearColor, bool useSkybox)
	{
		PreviewRenderStrategyUtility.RenderCamera(host, camera, target, clearColor, useSkybox: false, "Preview_URP2D_Clear");
	}
}

internal sealed class UnknownSrpPreviewRenderStrategy : IPreviewRenderStrategy
{
	public PreviewRenderPipelineKind Kind => PreviewRenderPipelineKind.UnknownSrp;

	public void ConfigureCamera(Camera camera, PreviewCameraRenderInput input)
	{
		PreviewRenderStrategyUtility.ConfigureCameraCommon(camera, input, forceOrthographic: false);
	}

	public void Render(IPreviewRenderHost host, Camera camera, RenderTexture target, Color clearColor, bool useSkybox)
	{
		PreviewRenderStrategyUtility.RenderCamera(host, camera, target, clearColor, useSkybox: false, "Preview_UnknownSRP_Clear");
	}
}

internal static class PreviewRenderStrategyFactory
{
	private static readonly IPreviewRenderStrategy BuiltInStrategy = new BuiltInPreviewRenderStrategy();
	private static readonly IPreviewRenderStrategy Urp3DStrategy = new Urp3DPreviewRenderStrategy();
	private static readonly IPreviewRenderStrategy Urp2DStrategy = new Urp2DPreviewRenderStrategy();
	private static readonly IPreviewRenderStrategy UnknownSrpStrategy = new UnknownSrpPreviewRenderStrategy();

	public static IPreviewRenderStrategy GetStrategy(PreviewRenderPipelineKind kind)
	{
		return kind switch
		{
			PreviewRenderPipelineKind.BuiltIn => BuiltInStrategy,
			PreviewRenderPipelineKind.Urp3D => Urp3DStrategy,
			PreviewRenderPipelineKind.Urp2D => Urp2DStrategy,
			_ => UnknownSrpStrategy
		};
	}
}

internal static class PreviewRenderStrategyUtility
{
	public static void ConfigureCameraCommon(Camera camera, PreviewCameraRenderInput input, bool forceOrthographic)
	{
		if (camera == null)
			return;

		camera.aspect = input.Aspect;
		camera.backgroundColor = input.BackgroundColor;

		bool useOrthographic = forceOrthographic || input.Use2DCompatibilityMode;
		if (useOrthographic)
		{
			camera.orthographic = true;
			camera.orthographicSize = input.Distance * 0.5f;
			camera.transform.position = input.Pivot + Vector3.back * input.Distance;
			camera.transform.rotation = Quaternion.identity;
		}
		else
		{
			camera.orthographic = false;
			camera.fieldOfView = input.Fov;
			Quaternion cameraRotation = Quaternion.Euler(input.Orbit.y, input.Orbit.x, 0f);
			Vector3 forward = cameraRotation * Vector3.forward;
			camera.transform.position = input.Pivot - forward * input.Distance;
			camera.transform.rotation = cameraRotation;
		}
	}

	public static void RenderCamera(IPreviewRenderHost host, Camera camera, RenderTexture target, Color clearColor, bool useSkybox, string commandName)
	{
		if (camera == null || target == null)
			return;

		camera.targetTexture = target;
		if (!host.PreserveCameraColorBuffer)
		{
			PreviewEnvironmentRenderer.ClearRenderTexture(target, clearColor, commandName);
		}
		else
		{
			camera.clearFlags = CameraClearFlags.Depth;
		}

		if (!useSkybox && camera.clearFlags == CameraClearFlags.Skybox)
			camera.clearFlags = CameraClearFlags.SolidColor;

		camera.Render();
		camera.targetTexture = null;
	}
}

internal static class PreviewEnvironmentRenderer
{
	internal readonly struct RenderSettingsScope : IDisposable
	{
		private readonly AmbientMode _previousAmbientMode;
		private readonly Color _previousAmbientColor;
		private readonly DefaultReflectionMode _previousReflectionMode;
		private readonly Texture _previousCustomReflection;
		private readonly Material _previousSkybox;
		private readonly bool _restoreSkybox;

		public RenderSettingsScope(
			AmbientMode previousAmbientMode,
			Color previousAmbientColor,
			DefaultReflectionMode previousReflectionMode,
			Texture previousCustomReflection,
			Material previousSkybox,
			bool restoreSkybox)
		{
			_previousAmbientMode = previousAmbientMode;
			_previousAmbientColor = previousAmbientColor;
			_previousReflectionMode = previousReflectionMode;
			_previousCustomReflection = previousCustomReflection;
			_previousSkybox = previousSkybox;
			_restoreSkybox = restoreSkybox;
		}

		public void Dispose()
		{
			RenderSettings.ambientMode = _previousAmbientMode;
			RenderSettings.ambientLight = _previousAmbientColor;
			RenderSettings.defaultReflectionMode = _previousReflectionMode;
			SetCustomReflectionSafe(_previousCustomReflection);
			if (_restoreSkybox)
				RenderSettings.skybox = _previousSkybox;
		}
	}

	private static readonly System.Reflection.PropertyInfo CustomReflectionTextureProperty =
		typeof(RenderSettings).GetProperty(
			"customReflectionTexture",
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

	public static RenderSettingsScope BeginRenderSettingsScope(
		bool use2DCompatibilityMode,
		bool lightingEnabled,
		Color ambientColor,
		bool reflectionEnabled,
		Cubemap reflectionCubemap,
		Material overrideSkybox)
	{
		AmbientMode previousAmbientMode = RenderSettings.ambientMode;
		Color previousAmbientColor = RenderSettings.ambientLight;
		DefaultReflectionMode previousReflectionMode = RenderSettings.defaultReflectionMode;
		Texture previousCustomReflection = GetCustomReflectionSafe();
		Material previousSkybox = RenderSettings.skybox;

		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = use2DCompatibilityMode
			? Color.white
			: (lightingEnabled ? ambientColor : Color.black);

		if (reflectionEnabled && reflectionCubemap != null)
		{
			RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
			SetCustomReflectionSafe(reflectionCubemap);
		}

		if (overrideSkybox != null)
			RenderSettings.skybox = overrideSkybox;

		return new RenderSettingsScope(
			previousAmbientMode,
			previousAmbientColor,
			previousReflectionMode,
			previousCustomReflection,
			previousSkybox,
			restoreSkybox: true);
	}

	public static void ClearRenderTexture(RenderTexture renderTexture, Color clearColor, string commandName)
	{
		if (renderTexture == null)
			return;

		RenderTexture previousActive = RenderTexture.active;
		try
		{
			using var clearBuffer = new CommandBuffer
			{
				name = string.IsNullOrEmpty(commandName) ? "Preview_ClearRT" : commandName
			};
			clearBuffer.SetRenderTarget(renderTexture);
			clearBuffer.ClearRenderTarget(true, true, clearColor);
			Graphics.ExecuteCommandBuffer(clearBuffer);

			RenderTexture.active = renderTexture;
			GL.Clear(true, true, clearColor);
		}
		finally
		{
			RenderTexture.active = previousActive;
		}
	}

	private static Texture GetCustomReflectionSafe()
	{
		try
		{
			if (CustomReflectionTextureProperty != null)
				return CustomReflectionTextureProperty.GetValue(null) as Texture;

#pragma warning disable CS0618
			return RenderSettings.customReflection;
#pragma warning restore CS0618
		}
		catch (ArgumentException)
		{
			return null;
		}
	}

	private static void SetCustomReflectionSafe(Texture reflectionTexture)
	{
		try
		{
			if (CustomReflectionTextureProperty != null)
			{
				CustomReflectionTextureProperty.SetValue(null, reflectionTexture);
				return;
			}

#pragma warning disable CS0618
			RenderSettings.customReflection = reflectionTexture as Cubemap;
#pragma warning restore CS0618
		}
		catch (ArgumentException)
		{
			// Ignore: platform-specific reflection behavior can reject assignments.
		}
	}
}

internal static class PreviewGridMeshBuilder
{
	private const float AxisAlphaBoost = 1.85f;
	private const float AxisTailAlphaRatio = 0.32f;

	public static void BuildGridMesh(Mesh mesh, float halfSize, float step, float alpha)
	{
		if (mesh == null)
			return;

		float safeHalfSize = Mathf.Max(0.05f, halfSize);
		float safeStep = Mathf.Max(0.05f, step);
		int count = Mathf.Max(1, Mathf.RoundToInt(safeHalfSize / safeStep));
		var vertices = new List<Vector3>();
		var colors = new List<Color>();

		Color baseColor = EditorGUIUtility.isProSkin
			? new Color(1f, 1f, 1f, alpha)
			: new Color(0f, 0f, 0f, alpha);
		Color xAxisColor = EditorGUIUtility.isProSkin
			? new Color(1f, 0.28f, 0.28f, Mathf.Clamp01(alpha * AxisAlphaBoost))
			: new Color(0.75f, 0.12f, 0.12f, Mathf.Clamp01(alpha * AxisAlphaBoost));
		Color yAxisColor = EditorGUIUtility.isProSkin
			? new Color(0.28f, 1f, 0.28f, Mathf.Clamp01(alpha * AxisAlphaBoost))
			: new Color(0.12f, 0.58f, 0.12f, Mathf.Clamp01(alpha * AxisAlphaBoost));

		for (int i = -count; i <= count; i++)
		{
			float position = i * safeStep;
			float fade = Mathf.Pow(1f - Mathf.Abs(position) / safeHalfSize, 2f);
			bool isCenterAxis = i == 0;

			Color xLineBase = isCenterAxis ? xAxisColor : baseColor;
			Color xPeak = new Color(xLineBase.r, xLineBase.g, xLineBase.b, xLineBase.a * fade);
			float xTailAlpha = isCenterAxis ? xLineBase.a * AxisTailAlphaRatio : 0f;
			Color xZero = new Color(xLineBase.r, xLineBase.g, xLineBase.b, xTailAlpha);

			Color yLineBase = isCenterAxis ? yAxisColor : baseColor;
			Color yPeak = new Color(yLineBase.r, yLineBase.g, yLineBase.b, yLineBase.a * fade);
			float yTailAlpha = isCenterAxis ? yLineBase.a * AxisTailAlphaRatio : 0f;
			Color yZero = new Color(yLineBase.r, yLineBase.g, yLineBase.b, yTailAlpha);

			vertices.Add(new Vector3(-safeHalfSize, 0f, position)); colors.Add(xZero);
			vertices.Add(new Vector3(0f, 0f, position)); colors.Add(xPeak);
			vertices.Add(new Vector3(0f, 0f, position)); colors.Add(xPeak);
			vertices.Add(new Vector3(safeHalfSize, 0f, position)); colors.Add(xZero);

			vertices.Add(new Vector3(position, 0f, -safeHalfSize)); colors.Add(yZero);
			vertices.Add(new Vector3(position, 0f, 0f)); colors.Add(yPeak);
			vertices.Add(new Vector3(position, 0f, 0f)); colors.Add(yPeak);
			vertices.Add(new Vector3(position, 0f, safeHalfSize)); colors.Add(yZero);
		}

		int[] indices = new int[vertices.Count];
		for (int i = 0; i < indices.Length; i++)
			indices[i] = i;

		mesh.Clear();
		mesh.SetVertices(vertices);
		mesh.SetColors(colors);
		mesh.SetIndices(indices, MeshTopology.Lines, 0);
	}
}

internal static class PreviewPipelineBadgeDrawer
{
	private static readonly GUIStyle BadgeStyle = new(EditorStyles.miniLabel)
	{
		alignment = TextAnchor.MiddleCenter,
		fontStyle = FontStyle.Bold,
		padding = new RectOffset(8, 8, 4, 4)
	};

	public static void Draw(Rect previewRect, PreviewPipelineContext context)
	{
		string label = PreviewFeatureGuard.ToDisplayLabel(context.Kind);
		if (string.IsNullOrEmpty(label))
			return;

		Vector2 size = BadgeStyle.CalcSize(new GUIContent(label));
		float width = Mathf.Max(78f, size.x + 4f);
		float height = 20f;
		var badgeRect = new Rect(previewRect.x + 12f, previewRect.y + 12f, width, height);

		Color background = new Color(0f, 0f, 0f, 0.65f);
		Color border = new Color(1f, 1f, 1f, 0.15f);
		EditorGUI.DrawRect(badgeRect, background);
		EditorGUI.DrawRect(new Rect(badgeRect.x, badgeRect.yMax - 1f, badgeRect.width, 1f), border);
		EditorGUI.DrawRect(new Rect(badgeRect.x, badgeRect.y, 1f, badgeRect.height), border);
		EditorGUI.DrawRect(new Rect(badgeRect.xMax - 1f, badgeRect.y, 1f, badgeRect.height), border);
		GUI.Label(badgeRect, label, BadgeStyle);
	}
}

}
#endif
