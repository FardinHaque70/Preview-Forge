#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	public static class ImprovedPreviewSettings
	{
		private const string ProjectSettingsFilePath = "ProjectSettings/ImprovedAssetTools/ImprovedPreviewSettings.asset";
		private const string ScriptFileName = "ImprovedPreviewSettings.cs";
		private const string DefaultSkyboxFileName = "SkyboxPreview.png";
		private const string LegacyDefaultSkyboxFileName = "HDRI_Preview.png";
		private const string DefaultReflectionFileName = "ReflectionMap.hdr";
		private const string DefaultSkyboxGuid = "83fb20c8d36eb4afe9b376da509dc0d6";
		private const string DefaultReflectionGuid = "e628e60fedd134eae92c45e351f5f566";

		public const bool D_IsActive = true;
		public const float D_OrbitSmooth = 18f;
		public const float D_ZoomSmooth = 18f;
		public const float D_Fov = 30f;
		public const float D_DefaultDist = 8f;
		public const float D_MaxPreviewResolutionScale = 0.7f;
		public const float MinPreviewResolutionScale = 0.25f;
		public const float MaxPreviewResolutionScale = 1f;
		[Obsolete("Use D_MaxPreviewResolutionScale.")]
		public const float FixedResolutionScale = D_MaxPreviewResolutionScale;
		public const int FixedAntiAliasing = 2;
		public const bool D_ShowLightGizmos = false;
		public const float D_GridHalfSize = 10f;
		public const float D_GridStep = 1f;
		public const float D_GridAlpha = 0.22f;
		public static readonly Color D_BgColor = Color.black;
		public static readonly Vector3 D_KeyPos = new Vector3(-3f, 4f, -3f);
		public const float D_KeyIntensity = 1.2f;
		public static readonly Vector3 D_FillPos = new Vector3(3f, 2f, 3f);
		public const float D_FillIntensity = 0.6f;
		public static readonly Color D_AmbientColor = new Color(0.58f, 0.58f, 0.58f, 1f);
		public const bool D_RimLightEnabled = true;
		public const float D_RimLightIntensity = 0.5f;
		public static readonly Vector2 D_RimLightRotation = new Vector2(160f, 0f);
		public static readonly Color D_RimLightColor = Color.white;
		public const bool D_Light2Enabled = D_RimLightEnabled;
		public const float D_Light2Intensity = D_RimLightIntensity;
		public static readonly Vector2 D_Light2Rotation = D_RimLightRotation;
		public static readonly Color D_Light2Color = D_RimLightColor;
		public const bool D_ShowToolbarSkyboxToggle = true;
		public const bool D_ShowToolbarLightsToggle = true;
		public const bool D_ShowToolbarGridToggle = true;
		public const bool D_EnableModelPrefabPreview = true;
		public const bool D_EnableSpritePrefabPreview = true;
		public const bool D_EnableUiPrefabPreview = true;
		public const bool D_EnableParticlePrefabPreview = true;
		public const bool D_EnableMaterialPreview = true;
		public const bool D_EnableNonVisualPrefabPreview = true;
		public const bool D_ModelPreviewDefaultTurntable = true;
		public const bool D_ModelPreviewDefaultLights = true;
		public const bool D_ModelPreviewDefaultGrid = true;
		public const bool D_ModelPreviewDefaultSkybox = true;
		public const bool D_ModelPreviewDefaultStats = true;
		public const bool D_ModelPreviewDefaultBounds = false;
		public const ModelPreviewDefaultVisualMode D_ModelPreviewDefaultVisualMode = ModelPreviewDefaultVisualMode.None;
		public const float D_ModelPreviewTwoDDistanceFactor = 3.2f;
		public const float D_ModelPreviewPerspectiveFitMultiplier = 1.5f;
		public const float D_ModelPreviewPerspectivePaddingMultiplier = 1f;
		public const float D_ModelPreviewMinimumDistance = 0.3f;
		public const bool D_SpritePreviewDefaultGrid = true;
		public const float D_SpritePreviewTwoDDistanceFactor = 3.2f;
		// Legacy serialized compatibility only. Sprite previews are always orthographic and do not use skybox.
		public const bool D_SpritePreviewDefaultSkybox = false;
		// Legacy serialized compatibility only. Sprite previews are always orthographic.
		public const float D_SpritePreviewPerspectivePaddingMultiplier = 1.3f;
		// Legacy serialized compatibility only. Sprite previews are always orthographic.
		public const float D_SpritePreviewMinimumPerspectiveDistance = 3f;
		public const bool D_UiPreviewDefaultGrid = true;
		// Legacy serialized compatibility only. UI previews always keep skybox disabled.
		public const bool D_UiPreviewDefaultSkybox = false;
		public const float D_UiPreviewDistanceFactor = 2.4f;
		public const float D_UiPreviewMinimumDistance = 0.3f;
		public const bool D_ParticlePreviewDefaultAutoplay = true;
		public const bool D_ParticlePreviewDefaultGrid = true;
		public const bool D_ParticlePreviewDefaultSkybox = false;
		public const ParticlePreviewDefaultMotionShape D_ParticlePreviewDefaultMotionShape = ParticlePreviewDefaultMotionShape.Circle;
		public const float D_ParticlePreviewDefaultMotionSpeed = 30f;
		public const float D_ParticlePreviewDefaultMotionSize = 3f;
		public const float D_ParticlePreviewDurationDistanceMultiplier = 2f;
		public const float D_ParticlePreviewMinimumFittedDistance = 4f;
		public const float D_ParticlePreviewMotionFitMultiplier = 1.75f;
		public const float D_ParticlePreviewFinalDistancePaddingMultiplier = 1.15f;
		public const bool D_MaterialPreviewDefaultTurntable = true;
		public const bool D_MaterialPreviewDefaultLights = true;
		public const bool D_MaterialPreviewDefaultGrid = true;
		public const bool D_MaterialPreviewDefaultSkybox = true;
		public const bool D_MaterialPreviewDefaultReflection = true;
		public const MaterialPreviewDefaultMeshMode D_MaterialPreviewDefaultMeshMode = MaterialPreviewDefaultMeshMode.PipelineDefault;
		public const float D_MaterialPreviewFitMultiplier = 1.15f;
		public const float D_MaterialPreviewIntroOvershootMultiplier = 1.35f;
		public const float D_MaterialPreviewIntroOvershootOffset = 0.35f;
		public const float D_MaterialPreviewMinimumDistance = 0.6f;
		public const int D_PreviewRefreshFps = 45;
		public const int MinPreviewRefreshFps = 15;
		public const int MaxPreviewRefreshFps = 60;
		[Obsolete("Use D_PreviewRefreshFps.")]
		public const int D_MaxPlaybackFps = D_PreviewRefreshFps;
		[Obsolete("Use MinPreviewRefreshFps.")]
		public const int MinPlaybackFps = MinPreviewRefreshFps;
		[Obsolete("Use MaxPreviewRefreshFps.")]
		public const int MaxPlaybackFps = MaxPreviewRefreshFps;
		public const bool D_EnableDiagnostics = false;

		private static ImprovedPreviewSettingsAsset _cachedAsset;
		private static ImprovedPreviewSettingsAsset _cachedAppliedAsset;
		private static string _cachedAppliedJson;
		private static bool _legacyMigrationChecked;
		private static UnityEditor.Editor _settingsEditor;

		// Resolved once at class load; used to scope repaints to Inspector windows only.
		private static readonly System.Type s_InspectorWindowType =
			System.Type.GetType("UnityEditor.InspectorWindow, UnityEditor");

		public static bool Active => AppliedAsset.isActive;
		public static float OrbitSmooth => AppliedAsset.orbitSmooth;
		public static float ZoomSmooth => AppliedAsset.zoomSmooth;
		public static float Fov => AppliedAsset.fov;
		public static float DefaultDist => AppliedAsset.defaultDist;
		public static float ResolutionScale => Mathf.Clamp(AppliedAsset.maxPreviewResolutionScale, MinPreviewResolutionScale, MaxPreviewResolutionScale);
		public static int AntiAliasing => FixedAntiAliasing;
		public static bool ShowLightGizmos => AppliedAsset.showGizmos;
		public static Color BgColor => AppliedAsset.backgroundColor;
		public static Cubemap SkyboxCubemap => AppliedAsset.skyboxCubemap;
		public static Texture SkyboxTexture => AppliedAsset.skyboxCubemap != null ? AppliedAsset.skyboxCubemap : LoadDefaultSkyboxTexture();
		public static Cubemap ReflectionCubemap => AppliedAsset.skyboxCubemap != null ? AppliedAsset.skyboxCubemap : LoadDefaultReflectionCubemap();
		public static Color AmbientColor => D_AmbientColor;
		public static float KeyIntensity => AppliedAsset.keyIntensity;
		public static Vector3 KeyPosition => AppliedAsset.keyPos;
		public static float FillIntensity => AppliedAsset.fillIntensity;
		public static Vector3 FillPosition => AppliedAsset.fillPos;
		public static bool RimLightEnabled => AppliedAsset.light2Enabled;
		public static float RimLightIntensity => AppliedAsset.light2Intensity;
		public static Vector2 RimLightRotation => AppliedAsset.light2Rotation;
		public static Color RimLightColor => AppliedAsset.light2Color;
		public static bool AdditionalLight2Enabled => RimLightEnabled;
		public static float AdditionalLight2Intensity => RimLightIntensity;
		public static Vector2 AdditionalLight2Rotation => RimLightRotation;
		public static Color AdditionalLight2Color => RimLightColor;
		public static float GridHalfSize => AppliedAsset.gridHalfSize;
		public static float GridStep => AppliedAsset.gridStep;
		public static float GridAlpha => AppliedAsset.gridAlpha;
		public static bool ShowToolbarSkyboxToggle => AppliedAsset.showToolbarSkyboxToggle;
		public static bool ShowToolbarLightsToggle => AppliedAsset.showToolbarLightsToggle;
		public static bool ShowToolbarGridToggle => AppliedAsset.showToolbarGridToggle;
		public static bool HasUguiSupport
		{
			get
			{
				return System.Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI") != null
					&& System.Type.GetType("UnityEngine.UI.CanvasScaler, UnityEngine.UI") != null
					&& System.Type.GetType("UnityEngine.UI.LayoutRebuilder, UnityEngine.UI") != null;
			}
		}
		public static bool EnableModelPrefabPreview => AppliedAsset.enableModelPrefabPreview;
		public static bool EnableSpritePrefabPreview => AppliedAsset.enableSpritePrefabPreview;
		public static bool EnableUiPrefabPreview => HasUguiSupport && AppliedAsset.enableUiPrefabPreview;
		public static bool EnableParticlePrefabPreview => AppliedAsset.enableParticlePrefabPreview;
		public static bool EnableMaterialPreview => AppliedAsset.enableMaterialPreview;
		public static bool EnableNonVisualPrefabPreview => AppliedAsset.enableNonVisualPrefabPreview;
		public static bool ModelPreviewDefaultTurntable => AppliedAsset.modelPreviewDefaultTurntable;
		public static bool ModelPreviewDefaultLights => AppliedAsset.modelPreviewDefaultLights;
		public static bool ModelPreviewDefaultGrid => AppliedAsset.modelPreviewDefaultGrid;
		public static bool ModelPreviewDefaultSkybox => AppliedAsset.modelPreviewDefaultSkybox;
		public static bool ModelPreviewDefaultStats => AppliedAsset.modelPreviewDefaultStats;
		public static bool ModelPreviewDefaultBounds => AppliedAsset.modelPreviewDefaultBounds;
		public static ModelPreviewDefaultVisualMode ModelPreviewDefaultVisualMode => AppliedAsset.modelPreviewDefaultVisualMode;
		public static float ModelPreviewTwoDDistanceFactor => Mathf.Clamp(AppliedAsset.modelPreviewTwoDDistanceFactor, 0.5f, 10f);
		public static float ModelPreviewPerspectiveFitMultiplier => Mathf.Clamp(AppliedAsset.modelPreviewPerspectiveFitMultiplier, 0.5f, 6f);
		public static float ModelPreviewPerspectivePaddingMultiplier => Mathf.Clamp(AppliedAsset.modelPreviewPerspectivePaddingMultiplier, 0.5f, 4f);
		public static float ModelPreviewMinimumDistance => Mathf.Clamp(AppliedAsset.modelPreviewMinimumDistance, 0.1f, 25f);
		public static bool SpritePreviewDefaultGrid => AppliedAsset.spritePreviewDefaultGrid;
		public static float SpritePreviewTwoDDistanceFactor => Mathf.Clamp(AppliedAsset.spritePreviewTwoDDistanceFactor, 0.5f, 10f);
		public static bool UiPreviewDefaultGrid => AppliedAsset.uiPreviewDefaultGrid;
		public static float UiPreviewDistanceFactor => Mathf.Clamp(AppliedAsset.uiPreviewDistanceFactor, 0.5f, 10f);
		public static float UiPreviewMinimumDistance => Mathf.Clamp(AppliedAsset.uiPreviewMinimumDistance, 0.1f, 25f);
		public static bool ParticlePreviewDefaultAutoplay => AppliedAsset.particlePreviewDefaultAutoplay;
		public static bool ParticlePreviewDefaultGrid => AppliedAsset.particlePreviewDefaultGrid;
		public static bool ParticlePreviewDefaultSkybox => AppliedAsset.particlePreviewDefaultSkybox;
		public static ParticlePreviewDefaultMotionShape ParticlePreviewDefaultMotionShape => AppliedAsset.particlePreviewDefaultMotionShape;
		public static float ParticlePreviewDefaultMotionSpeed => Mathf.Clamp(AppliedAsset.particlePreviewDefaultMotionSpeed, 0f, 360f);
		public static float ParticlePreviewDefaultMotionSize => Mathf.Clamp(AppliedAsset.particlePreviewDefaultMotionSize, 0f, 20f);
		public static float ParticlePreviewDurationDistanceMultiplier => Mathf.Clamp(AppliedAsset.particlePreviewDurationDistanceMultiplier, 0.1f, 10f);
		public static float ParticlePreviewMinimumFittedDistance => Mathf.Clamp(AppliedAsset.particlePreviewMinimumFittedDistance, 0.1f, 100f);
		public static float ParticlePreviewMotionFitMultiplier => Mathf.Clamp(AppliedAsset.particlePreviewMotionFitMultiplier, 0.1f, 10f);
		public static float ParticlePreviewFinalDistancePaddingMultiplier => Mathf.Clamp(AppliedAsset.particlePreviewFinalDistancePaddingMultiplier, 0.1f, 10f);
		public static bool MaterialPreviewDefaultTurntable => AppliedAsset.materialPreviewDefaultTurntable;
		public static bool MaterialPreviewDefaultLights => AppliedAsset.materialPreviewDefaultLights;
		public static bool MaterialPreviewDefaultGrid => AppliedAsset.materialPreviewDefaultGrid;
		public static bool MaterialPreviewDefaultSkybox => AppliedAsset.materialPreviewDefaultSkybox;
		public static bool MaterialPreviewDefaultReflection => AppliedAsset.materialPreviewDefaultReflection;
		public static MaterialPreviewDefaultMeshMode MaterialPreviewDefaultMeshMode => AppliedAsset.materialPreviewDefaultMeshMode;
		public static float MaterialPreviewFitMultiplier => Mathf.Clamp(AppliedAsset.materialPreviewFitMultiplier, 0.1f, 4f);
		public static float MaterialPreviewIntroOvershootMultiplier => Mathf.Clamp(AppliedAsset.materialPreviewIntroOvershootMultiplier, 1f, 4f);
		public static float MaterialPreviewIntroOvershootOffset => Mathf.Clamp(AppliedAsset.materialPreviewIntroOvershootOffset, 0f, 10f);
		public static float MaterialPreviewMinimumDistance => Mathf.Clamp(AppliedAsset.materialPreviewMinimumDistance, 0.1f, 25f);
		public static int PreviewRefreshUpdatesPerSecond => Mathf.Clamp(AppliedAsset.maxPlaybackFps, MinPreviewRefreshFps, MaxPreviewRefreshFps);
		[Obsolete("Use PreviewRefreshUpdatesPerSecond.")]
		public static int MaxPlaybackUpdatesPerSecond => PreviewRefreshUpdatesPerSecond;
		public static bool ShowCapabilityWarnings => true;
		public static bool EnableDiagnostics => AppliedAsset.enableDiagnostics;
		public static int AppliedRevision => Mathf.Max(1, Asset.appliedRevision);

		public static bool IsPreviewTypeEnabled(PreviewAssetTypeKey previewTypeKey)
		{
			return previewTypeKey switch
			{
				PreviewAssetTypeKey.ModelPrefab => EnableModelPrefabPreview,
				PreviewAssetTypeKey.SpritePrefab => EnableSpritePrefabPreview,
				PreviewAssetTypeKey.UiPrefab => EnableUiPrefabPreview,
				PreviewAssetTypeKey.ParticlePrefab => EnableParticlePrefabPreview,
				PreviewAssetTypeKey.Material => EnableMaterialPreview,
				PreviewAssetTypeKey.NonVisualPrefab => EnableNonVisualPrefabPreview,
				_ => true
			};
		}

		public static bool GetDefaultLightingEnabled(PreviewAssetTypeKey previewTypeKey)
		{
			return previewTypeKey switch
			{
				PreviewAssetTypeKey.ModelPrefab => ModelPreviewDefaultLights,
				PreviewAssetTypeKey.Material => MaterialPreviewDefaultLights,
				_ => false
			};
		}

		public static bool GetDefaultGridEnabled(PreviewAssetTypeKey previewTypeKey)
		{
			return previewTypeKey switch
			{
				PreviewAssetTypeKey.ModelPrefab => ModelPreviewDefaultGrid,
				PreviewAssetTypeKey.SpritePrefab => SpritePreviewDefaultGrid,
				PreviewAssetTypeKey.UiPrefab => UiPreviewDefaultGrid,
				PreviewAssetTypeKey.ParticlePrefab => ParticlePreviewDefaultGrid,
				PreviewAssetTypeKey.Material => MaterialPreviewDefaultGrid,
				_ => false
			};
		}

		public static bool GetDefaultSkyboxEnabled(PreviewAssetTypeKey previewTypeKey)
		{
			return previewTypeKey switch
			{
				PreviewAssetTypeKey.ModelPrefab => ModelPreviewDefaultSkybox,
				PreviewAssetTypeKey.ParticlePrefab => ParticlePreviewDefaultSkybox,
				PreviewAssetTypeKey.Material => MaterialPreviewDefaultSkybox,
				_ => false
			};
		}

		[MenuItem("Window/Improved Asset Tools/Settings/Improved Preview")]
		public static void SelectSettingsAsset()
		{
			SettingsService.OpenProjectSettings("Project/Improved Asset Tools/Improved Preview");
		}

		[SettingsProvider]
		public static SettingsProvider CreateSettingsProvider()
		{
			return new SettingsProvider("Project/Improved Asset Tools/Improved Preview", SettingsScope.Project)
			{
				label = "Improved Preview",
				guiHandler = _ => DrawSettingsProviderGui(),
				keywords = new HashSet<string>
				{
					"preview",
					"prefab",
					"lighting",
					"skybox",
					"camera",
					"asset tools",
				},
			};
		}

		private static void DrawSettingsProviderGui()
		{
			ImprovedPreviewSettingsAsset asset = Asset;
			if (_settingsEditor == null || _settingsEditor.target != asset)
			{
				if (_settingsEditor != null)
					Object.DestroyImmediate(_settingsEditor);

				_settingsEditor = UnityEditor.Editor.CreateEditor(asset);
			}

			_settingsEditor?.OnInspectorGUI();
		}

		public static ImprovedPreviewSettingsAsset Asset
		{
			get
			{
				if (_cachedAsset == null)
					_cachedAsset = LoadDraftAsset();

				TryMigrateLegacySettingsIfNeeded(_cachedAsset);
				_cachedAsset.EnsureAppliedSnapshotInitialized();
				return _cachedAsset;
			}
		}

		internal static ImprovedPreviewSettingsAsset AppliedAsset
		{
			get
			{
				ImprovedPreviewSettingsAsset asset = Asset;
				string appliedJson = asset.appliedSettingsJson ?? string.Empty;
				if (_cachedAppliedAsset == null || _cachedAppliedJson != appliedJson)
				{
					if (_cachedAppliedAsset != null)
						Object.DestroyImmediate(_cachedAppliedAsset);

					_cachedAppliedAsset = AppliedSettingsUtility.CreateAppliedClone(asset, appliedJson);
					_cachedAppliedJson = appliedJson;
				}

				return _cachedAppliedAsset ?? asset;
			}
		}

		internal static void InvalidateAppliedSnapshotCache()
		{
			_cachedAppliedJson = string.Empty;
			if (_cachedAppliedAsset != null)
			{
				Object.DestroyImmediate(_cachedAppliedAsset);
				_cachedAppliedAsset = null;
			}
		}

		internal static void SaveDraftState(ImprovedPreviewSettingsAsset asset)
		{
			if (asset == null)
				return;

			ImprovedPreviewSettingsStorage storage = ImprovedPreviewSettingsStorage.instance;
			string json = EditorJsonUtility.ToJson(asset, false);
			if (storage.SettingsJson == json)
				return;

			storage.SettingsJson = json;
			storage.SaveStorage();
			_cachedAsset = asset;
		}

		internal static void NotifySettingsApplied()
		{
			InvalidateAppliedSnapshotCache();
			EditorApplication.QueuePlayerLoopUpdate();
			// Repaint only Inspector windows — NOT all views.
			// RepaintAllViews() would also repaint the Project Settings window itself,
			// which disrupts any active slider drag in our own settings UI and causes
			// input drops. Inspector windows are the only windows that host our preview;
			// they pick up changed AppliedRevision on their next natural repaint.
			RepaintInspectorWindows();
		}

		private static void RepaintInspectorWindows()
		{
			if (s_InspectorWindowType != null)
			{
				Object[] inspectors = Resources.FindObjectsOfTypeAll(s_InspectorWindowType);
				if (inspectors.Length > 0)
				{
					foreach (Object w in inspectors)
						(w as EditorWindow)?.Repaint();
					return;
				}
			}

			// Fallback: InspectorWindow type not found (should not happen in normal use).
			InternalEditorUtility.RepaintAllViews();
		}

		public static void ApplyDefaults(ImprovedPreviewSettingsAsset asset)
		{
			if (asset == null)
				return;

			asset.isActive = D_IsActive;
			asset.defaultDist = D_DefaultDist;
			asset.fov = D_Fov;
			asset.orbitSmooth = D_OrbitSmooth;
			asset.zoomSmooth = D_ZoomSmooth;
			asset.maxPreviewResolutionScale = D_MaxPreviewResolutionScale;
			asset.showGizmos = D_ShowLightGizmos;
			asset.backgroundColor = D_BgColor;
			asset.skyboxCubemap = null;
			asset.keyIntensity = D_KeyIntensity;
			asset.keyPos = D_KeyPos;
			asset.fillIntensity = D_FillIntensity;
			asset.fillPos = D_FillPos;
			asset.light2Enabled = D_RimLightEnabled;
			asset.light2Intensity = D_RimLightIntensity;
			asset.light2Rotation = D_RimLightRotation;
			asset.light2Color = D_RimLightColor;
			asset.gridHalfSize = D_GridHalfSize;
			asset.gridStep = D_GridStep;
			asset.gridAlpha = D_GridAlpha;
			asset.enableModelPrefabPreview = D_EnableModelPrefabPreview;
			asset.enableSpritePrefabPreview = D_EnableSpritePrefabPreview;
			asset.enableUiPrefabPreview = D_EnableUiPrefabPreview;
			asset.enableParticlePrefabPreview = D_EnableParticlePrefabPreview;
			asset.enableMaterialPreview = D_EnableMaterialPreview;
			asset.enableNonVisualPrefabPreview = D_EnableNonVisualPrefabPreview;
			asset.modelPreviewDefaultTurntable = D_ModelPreviewDefaultTurntable;
			asset.modelPreviewDefaultLights = D_ModelPreviewDefaultLights;
			asset.modelPreviewDefaultGrid = D_ModelPreviewDefaultGrid;
			asset.modelPreviewDefaultSkybox = D_ModelPreviewDefaultSkybox;
			asset.modelPreviewDefaultStats = D_ModelPreviewDefaultStats;
			asset.modelPreviewDefaultBounds = D_ModelPreviewDefaultBounds;
			asset.modelPreviewDefaultVisualMode = D_ModelPreviewDefaultVisualMode;
			asset.modelPreviewTwoDDistanceFactor = D_ModelPreviewTwoDDistanceFactor;
			asset.modelPreviewPerspectiveFitMultiplier = D_ModelPreviewPerspectiveFitMultiplier;
			asset.modelPreviewPerspectivePaddingMultiplier = D_ModelPreviewPerspectivePaddingMultiplier;
			asset.modelPreviewMinimumDistance = D_ModelPreviewMinimumDistance;
			asset.spritePreviewDefaultGrid = D_SpritePreviewDefaultGrid;
			asset.spritePreviewTwoDDistanceFactor = D_SpritePreviewTwoDDistanceFactor;
			asset.uiPreviewDefaultGrid = D_UiPreviewDefaultGrid;
			asset.uiPreviewDefaultSkybox = D_UiPreviewDefaultSkybox;
			asset.uiPreviewDistanceFactor = D_UiPreviewDistanceFactor;
			asset.uiPreviewMinimumDistance = D_UiPreviewMinimumDistance;
			asset.particlePreviewDefaultAutoplay = D_ParticlePreviewDefaultAutoplay;
			asset.particlePreviewDefaultGrid = D_ParticlePreviewDefaultGrid;
			asset.particlePreviewDefaultSkybox = D_ParticlePreviewDefaultSkybox;
			asset.particlePreviewDefaultMotionShape = D_ParticlePreviewDefaultMotionShape;
			asset.particlePreviewDefaultMotionSpeed = D_ParticlePreviewDefaultMotionSpeed;
			asset.particlePreviewDefaultMotionSize = D_ParticlePreviewDefaultMotionSize;
			asset.particlePreviewDurationDistanceMultiplier = D_ParticlePreviewDurationDistanceMultiplier;
			asset.particlePreviewMinimumFittedDistance = D_ParticlePreviewMinimumFittedDistance;
			asset.particlePreviewMotionFitMultiplier = D_ParticlePreviewMotionFitMultiplier;
			asset.particlePreviewFinalDistancePaddingMultiplier = D_ParticlePreviewFinalDistancePaddingMultiplier;
			asset.materialPreviewDefaultTurntable = D_MaterialPreviewDefaultTurntable;
			asset.materialPreviewDefaultLights = D_MaterialPreviewDefaultLights;
			asset.materialPreviewDefaultGrid = D_MaterialPreviewDefaultGrid;
			asset.materialPreviewDefaultSkybox = D_MaterialPreviewDefaultSkybox;
			asset.materialPreviewDefaultReflection = D_MaterialPreviewDefaultReflection;
			asset.materialPreviewDefaultMeshMode = D_MaterialPreviewDefaultMeshMode;
			asset.materialPreviewFitMultiplier = D_MaterialPreviewFitMultiplier;
			asset.materialPreviewIntroOvershootMultiplier = D_MaterialPreviewIntroOvershootMultiplier;
			asset.materialPreviewIntroOvershootOffset = D_MaterialPreviewIntroOvershootOffset;
			asset.materialPreviewMinimumDistance = D_MaterialPreviewMinimumDistance;
			asset.showToolbarSkyboxToggle = D_ShowToolbarSkyboxToggle;
			asset.showToolbarLightsToggle = D_ShowToolbarLightsToggle;
			asset.showToolbarGridToggle = D_ShowToolbarGridToggle;
			asset.maxPlaybackFps = D_PreviewRefreshFps;
			asset.enableDiagnostics = D_EnableDiagnostics;
		}

		public static void LogDiagnostic(string message)
		{
			if (!EnableDiagnostics || string.IsNullOrEmpty(message))
				return;

			Debug.Log($"[Improved Preview] {message}");
		}

		private static ImprovedPreviewSettingsAsset LoadDraftAsset()
		{
			ImprovedPreviewSettingsAsset asset = ScriptableObject.CreateInstance<ImprovedPreviewSettingsAsset>();
			ApplyDefaults(asset);

			ImprovedPreviewSettingsStorage storage = ImprovedPreviewSettingsStorage.instance;
			if (!string.IsNullOrEmpty(storage.SettingsJson))
			{
				try
				{
					EditorJsonUtility.FromJsonOverwrite(storage.SettingsJson, asset);
				}
				catch (Exception exception)
				{
					Debug.LogWarning($"[Improved Preview] Failed to load saved settings. Using defaults. {exception.Message}");
					ApplyDefaults(asset);
				}
			}

			return asset;
		}

		private static void TryMigrateLegacySettingsIfNeeded(ImprovedPreviewSettingsAsset asset)
		{
			if (_legacyMigrationChecked || asset == null)
				return;

			_legacyMigrationChecked = true;
		}

		private static Texture LoadDefaultSkyboxTexture()
		{
			string commonSkyboxPath = CombineAssetPath(GetToolRootDirectory(), $"Common/Skybox/{DefaultSkyboxFileName}");
			string legacyPath = CombineAssetPath(GetSystemRootDirectory(), $"HDRI/{LegacyDefaultSkyboxFileName}");
			string[] guidCandidates = {DefaultSkyboxGuid};
			string[] pathCandidates = {commonSkyboxPath, legacyPath};

			if (ScriptRelativeAssetUtility.TryLoadFirstByGuids(guidCandidates, out Cubemap guidCubemap))
				return guidCubemap;
			if (ScriptRelativeAssetUtility.TryLoadFirstByGuids(guidCandidates, out Texture guidTexture))
				return guidTexture;
			if (ScriptRelativeAssetUtility.TryLoadFirstAtPaths(pathCandidates, out Cubemap pathCubemap))
				return pathCubemap;
			if (ScriptRelativeAssetUtility.TryLoadFirstAtPaths(pathCandidates, out Texture pathTexture))
				return pathTexture;
			if (ScriptRelativeAssetUtility.TryFindAssetByFileName(DefaultSkyboxFileName, "/Common/Skybox/", out Cubemap commonCubemap))
				return commonCubemap;
			if (ScriptRelativeAssetUtility.TryFindAssetByFileName(DefaultSkyboxFileName, "/Common/Skybox/", out Texture commonTexture))
				return commonTexture;
			if (ScriptRelativeAssetUtility.TryFindAssetByFileName(LegacyDefaultSkyboxFileName, "/ImprovedPreview/", out Cubemap legacyCubemap))
				return legacyCubemap;
			if (ScriptRelativeAssetUtility.TryFindAssetByFileName(LegacyDefaultSkyboxFileName, "/ImprovedPreview/", out Texture legacyTexture))
				return legacyTexture;

			return null;
		}

		private static Cubemap LoadDefaultReflectionCubemap()
		{
			string commonReflectionPath = CombineAssetPath(GetToolRootDirectory(), $"Common/Skybox/{DefaultReflectionFileName}");
			string[] guidCandidates = {DefaultReflectionGuid};
			string[] pathCandidates = {commonReflectionPath};

			if (ScriptRelativeAssetUtility.TryLoadFirstByGuids(guidCandidates, out Cubemap guidCubemap))
				return guidCubemap;
			if (ScriptRelativeAssetUtility.TryLoadFirstAtPaths(pathCandidates, out Cubemap pathCubemap))
				return pathCubemap;
			if (ScriptRelativeAssetUtility.TryFindAssetByFileName(DefaultReflectionFileName, "/Common/Skybox/", out Cubemap foundCubemap))
				return foundCubemap;

			return null;
		}

		private static string GetToolRootDirectory()
		{
			return ScriptRelativeAssetUtility.GetScriptDirectory(ScriptFileName, "Assets/ImprovedAssetTools");
		}

		private static string GetSystemRootDirectory()
		{
			return GetToolRootDirectory();
		}

		private static string CombineAssetPath(string left, string right)
		{
			return ScriptRelativeAssetUtility.CombineAssetPath(left, right);
		}
	}
}
#endif
