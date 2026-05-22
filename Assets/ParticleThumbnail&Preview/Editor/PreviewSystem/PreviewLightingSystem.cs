using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleThumbnailAndPreview.Editor
{
    internal readonly struct SharedPreviewLightingProfile
    {
        internal readonly Color AmbientColor;
        internal readonly bool SunEnabled;
        internal readonly Color SunColor;
        internal readonly float SunIntensity;
        internal readonly float SunShadowStrength;
        internal readonly bool KeyEnabled;
        internal readonly float KeyIntensity;
        internal readonly bool FillEnabled;
        internal readonly float FillIntensity;
        internal readonly bool RimEnabled;
        internal readonly float RimIntensity;
        internal readonly Color RimColor;

        internal SharedPreviewLightingProfile(
            Color ambientColor,
            bool sunEnabled,
            Color sunColor,
            float sunIntensity,
            float sunShadowStrength,
            bool keyEnabled,
            float keyIntensity,
            bool fillEnabled,
            float fillIntensity,
            bool rimEnabled,
            float rimIntensity,
            Color rimColor)
        {
            AmbientColor = ambientColor;
            SunEnabled = sunEnabled;
            SunColor = sunColor;
            SunIntensity = sunIntensity;
            SunShadowStrength = sunShadowStrength;
            KeyEnabled = keyEnabled;
            KeyIntensity = keyIntensity;
            FillEnabled = fillEnabled;
            FillIntensity = fillIntensity;
            RimEnabled = rimEnabled;
            RimIntensity = rimIntensity;
            RimColor = rimColor;
        }

        internal static SharedPreviewLightingProfile FromSettings()
        {
            return new SharedPreviewLightingProfile(
                PreviewSettings.ModelAmbientLightColor,
                PreviewSettings.ModelSunLightEnabled,
                PreviewSettings.ModelSunLightColor,
                PreviewSettings.ModelSunLightIntensity,
                PreviewSettings.ModelSunLightShadowStrength,
                PreviewSettings.ModelKeyLightEnabled,
                PreviewSettings.ModelKeyLightIntensity,
                PreviewSettings.ModelFillLightEnabled,
                PreviewSettings.ModelFillLightIntensity,
                PreviewSettings.ModelRimLightEnabled,
                PreviewSettings.ModelRimLightIntensity,
                PreviewSettings.ModelRimLightColor);
        }
    }

    internal static class PreviewLightingSystem
    {
        #region Constants

        private const float SunShadowBias = 0.05f;
        private const float SunShadowNormalBias = 0.4f;

        #endregion

        #region Profile

        internal static SharedPreviewLightingProfile CreateProfileFromSettings()
        {
            return SharedPreviewLightingProfile.FromSettings();
        }

        internal static Quaternion RotationFromYawPitch(Vector2 yawPitch)
        {
            return Quaternion.Euler(yawPitch.y, yawPitch.x, 0f);
        }

        internal static uint ResolvePreviewLightRenderingLayerMask(
            IReadOnlyList<Renderer> renderers,
            PreviewRenderPipelineKind pipelineKind)
        {
            uint fallbackMask = GraphicsSettings.defaultRenderingLayerMask;
            if (pipelineKind == PreviewRenderPipelineKind.BuiltIn || renderers == null || renderers.Count == 0)
                return fallbackMask;

            uint combinedMask = 0u;
            bool foundRenderer = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                combinedMask |= renderer.renderingLayerMask;
                foundRenderer = true;
            }

            return foundRenderer ? combinedMask : fallbackMask;
        }

        #endregion

        #region Light Setup

        internal static void EnsureSunLight(PreviewRenderUtility preview, ref Light sunLight)
        {
            if (sunLight != null || preview == null)
                return;

            var sunRoot = new GameObject("PreviewSunLight") { hideFlags = HideFlags.HideAndDontSave };
            sunLight = sunRoot.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowBias = SunShadowBias;
            sunLight.shadowNormalBias = SunShadowNormalBias;
            preview.AddSingleGO(sunRoot);
        }

        internal static void EnsureRimLight(PreviewRenderUtility preview, ref Light rimLight)
        {
            if (rimLight != null || preview == null)
                return;

            var rimRoot = new GameObject("PreviewRimLight") { hideFlags = HideFlags.HideAndDontSave };
            rimLight = rimRoot.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.shadows = LightShadows.None;
            preview.AddSingleGO(rimRoot);
        }

        #endregion

        #region Apply

        internal static void ApplyLighting(
            PreviewRenderUtility preview,
            Light sunLight,
            Light rimLight,
            in SharedPreviewLightingProfile profile,
            bool lightingEnabled,
            uint renderingLayerMask,
            Quaternion sunRotation,
            Quaternion keyRotation,
            Quaternion fillRotation,
            Quaternion rimRotation)
        {
            if (preview == null)
                return;

            bool activeLighting = lightingEnabled;
            preview.ambientColor = activeLighting ? profile.AmbientColor : Color.black;

            Light[] previewLights = preview.lights;
            if (previewLights != null && previewLights.Length > 0)
            {
                ApplyDirectionalLight(
                    previewLights[0],
                    activeLighting && profile.KeyEnabled,
                    profile.KeyIntensity,
                    keyRotation,
                    Color.white,
                    renderingLayerMask,
                    LightShadows.None,
                    0f);
            }

            if (previewLights != null && previewLights.Length > 1)
            {
                ApplyDirectionalLight(
                    previewLights[1],
                    activeLighting && profile.FillEnabled,
                    profile.FillIntensity,
                    fillRotation,
                    Color.white,
                    renderingLayerMask,
                    LightShadows.None,
                    0f);
            }

            ApplyDirectionalLight(
                sunLight,
                activeLighting && profile.SunEnabled,
                profile.SunIntensity,
                sunRotation,
                profile.SunColor,
                renderingLayerMask,
                LightShadows.Soft,
                profile.SunShadowStrength);

            ApplyDirectionalLight(
                rimLight,
                activeLighting && profile.RimEnabled,
                profile.RimIntensity,
                rimRotation,
                profile.RimColor,
                renderingLayerMask,
                LightShadows.None,
                0f);
        }

        private static void ApplyDirectionalLight(
            Light light,
            bool enabled,
            float intensity,
            Quaternion worldRotation,
            Color color,
            uint renderingLayerMask,
            LightShadows shadows,
            float shadowStrength)
        {
            if (light == null)
                return;

            light.type = LightType.Directional;
            light.enabled = enabled;
            light.shadows = shadows;
            light.intensity = enabled ? intensity : 0f;
            light.color = color;
            light.renderingLayerMask = unchecked((int) renderingLayerMask);
            light.transform.rotation = worldRotation;
            if (shadows != LightShadows.None)
                light.shadowStrength = enabled ? shadowStrength : 0f;
        }

        #endregion
    }
}
