using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
// Applies rendering compatibility safeguards so preview rendering remains consistent across Built-in, URP, and HDRP editor contexts.

namespace NoodleHammer.PreviewForge.Editor
{
    internal enum PreviewRenderPipelineKind
    {
        BuiltIn,
        Urp3D,
        Urp2D,
        Hdrp,
        UnknownSrp,
    }

    /// <summary>
    /// Shared rendering compatibility helpers for thumbnail/preview rendering across
    /// Built-in, URP and HDRP editor preview paths.
    /// </summary>
    internal static class PreviewRenderCompatibilityUtility
    {
        private const bool EnableDiagnostics = false;

        private static readonly HashSet<string> LoggedDiagnosticKeys = new();
        private static readonly MethodInfo PreviewRenderWithSrpMethod = typeof(PreviewRenderUtility).GetMethod(
            "Render",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(bool) },
            null);

        internal static PreviewRenderPipelineKind DetectCurrentPipelineKind()
        {
            RenderPipelineAsset current = GraphicsSettings.currentRenderPipeline;
            if (current == null)
            {
                LogDiagnosticOnce("pipeline:builtin", "Detected Built-in render pipeline.");
                return PreviewRenderPipelineKind.BuiltIn;
            }

            string pipelineTypeName = current.GetType().FullName ?? current.GetType().Name ?? string.Empty;
            string rendererTypeName = TryGetDefaultRendererTypeName(current);
            PreviewRenderPipelineKind kind = ClassifyPipelineKind(pipelineTypeName, rendererTypeName);

            LogDiagnosticOnce(
                $"pipeline:{kind}:{pipelineTypeName}:{rendererTypeName}",
                $"Detected render pipeline: {kind} ({pipelineTypeName}) renderer={rendererTypeName ?? "n/a"}.");

            return kind;
        }

        internal static void RenderPreviewWithCompatibility(PreviewRenderUtility preview)
        {
            if (preview == null)
                return;

            PreviewRenderPipelineKind kind = DetectCurrentPipelineKind();
            bool usedSrpPath = false;
            bool fallbackUsed = false;

            if (ShouldPreferSrpRender(kind))
            {
                if (TryRenderWithSrp(preview))
                {
                    usedSrpPath = true;
                }
                else
                {
                    fallbackUsed = true;
                    preview.Render();
                }
            }
            else
            {
                try
                {
                    preview.Render();
                }
                catch
                {
                    if (!TryRenderWithSrp(preview))
                        throw;

                    usedSrpPath = true;
                    fallbackUsed = true;
                }
            }

            LogDiagnosticOnce(
                $"render-path:{kind}:{usedSrpPath}:{fallbackUsed}",
                $"Render path for {kind}: {(usedSrpPath ? "Render(true)" : "Render()")}, fallback={fallbackUsed}.");
        }

        internal static void RenderPreviewWithCameraPath(PreviewRenderUtility preview)
        {
            if (preview == null || preview.camera == null)
                return;

            bool fallbackUsed = false;
            using (new AmbientRenderSettingsScope(preview.ambientColor))
            {
                try
                {
                    preview.camera.Render();
                }
                catch
                {
                    fallbackUsed = true;
                    RenderPreviewWithCompatibility(preview);
                }
            }

            LogDiagnosticOnce(
                $"camera-path:fallback:{fallbackUsed}",
                $"Preview camera render path used. fallback={fallbackUsed}.");
        }

        internal static ShaderTimeScope PushShaderTime(float time)
        {
            return new ShaderTimeScope(time);
        }

        internal static RendererEnableScope EnableRenderersScoped(IReadOnlyList<Renderer> renderers)
        {
            return new RendererEnableScope(renderers, null);
        }

        internal static RendererEnableScope EnableRenderersScoped(IReadOnlyList<Renderer> renderers, IReadOnlyList<bool> enabledMask)
        {
            return new RendererEnableScope(renderers, enabledMask);
        }

        internal static void SetRenderersEnabled(IReadOnlyList<Renderer> renderers, bool enabled)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        internal readonly struct ShaderTimeScope : IDisposable
        {
            private readonly Vector4 _previousTime;
            private readonly Vector4 _previousSinTime;
            private readonly Vector4 _previousCosTime;
            private readonly Vector4 _previousDeltaTime;

            internal ShaderTimeScope(float time)
            {
                _previousTime = Shader.GetGlobalVector("_Time");
                _previousSinTime = Shader.GetGlobalVector("_SinTime");
                _previousCosTime = Shader.GetGlobalVector("_CosTime");
                _previousDeltaTime = Shader.GetGlobalVector("unity_DeltaTime");

                float shaderTime = Mathf.Max(0f, time);
                Shader.SetGlobalVector("_Time", new Vector4(shaderTime / 20f, shaderTime, shaderTime * 2f, shaderTime * 3f));
                Shader.SetGlobalVector("_SinTime", new Vector4(Mathf.Sin(shaderTime / 8f), Mathf.Sin(shaderTime / 4f), Mathf.Sin(shaderTime / 2f), Mathf.Sin(shaderTime)));
                Shader.SetGlobalVector("_CosTime", new Vector4(Mathf.Cos(shaderTime / 8f), Mathf.Cos(shaderTime / 4f), Mathf.Cos(shaderTime / 2f), Mathf.Cos(shaderTime)));
                Shader.SetGlobalVector("unity_DeltaTime", new Vector4(0.016f, 1f / 0.016f, 0f, 0f));
            }

            public void Dispose()
            {
                Shader.SetGlobalVector("_Time", _previousTime);
                Shader.SetGlobalVector("_SinTime", _previousSinTime);
                Shader.SetGlobalVector("_CosTime", _previousCosTime);
                Shader.SetGlobalVector("unity_DeltaTime", _previousDeltaTime);
            }
        }

        internal readonly struct RendererEnableScope : IDisposable
        {
            private readonly Renderer[] _renderers;
            private readonly bool[] _previousStates;

            internal RendererEnableScope(IReadOnlyList<Renderer> renderers, IReadOnlyList<bool> enabledMask)
            {
                if (renderers == null || renderers.Count == 0)
                {
                    _renderers = Array.Empty<Renderer>();
                    _previousStates = Array.Empty<bool>();
                    return;
                }

                _renderers = new Renderer[renderers.Count];
                _previousStates = new bool[renderers.Count];
                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];
                    _renderers[i] = renderer;
                    if (renderer == null)
                        continue;

                    _previousStates[i] = renderer.enabled;
                    bool targetEnabled = enabledMask == null || i >= enabledMask.Count || enabledMask[i];
                    if (renderer.enabled != targetEnabled)
                        renderer.enabled = targetEnabled;
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null)
                        continue;

                    renderer.enabled = _previousStates[i];
                }
            }
        }

        private readonly struct AmbientRenderSettingsScope : IDisposable
        {
            private readonly AmbientMode _previousAmbientMode;
            private readonly Color _previousAmbientLight;
            private readonly Color _previousAmbientSkyColor;
            private readonly Color _previousAmbientEquatorColor;
            private readonly Color _previousAmbientGroundColor;

            internal AmbientRenderSettingsScope(Color ambientColor)
            {
                _previousAmbientMode = RenderSettings.ambientMode;
                _previousAmbientLight = RenderSettings.ambientLight;
                _previousAmbientSkyColor = RenderSettings.ambientSkyColor;
                _previousAmbientEquatorColor = RenderSettings.ambientEquatorColor;
                _previousAmbientGroundColor = RenderSettings.ambientGroundColor;

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
            }

            public void Dispose()
            {
                RenderSettings.ambientMode = _previousAmbientMode;
                RenderSettings.ambientLight = _previousAmbientLight;
                RenderSettings.ambientSkyColor = _previousAmbientSkyColor;
                RenderSettings.ambientEquatorColor = _previousAmbientEquatorColor;
                RenderSettings.ambientGroundColor = _previousAmbientGroundColor;
            }
        }

        internal static PreviewRenderPipelineKind ClassifyPipelineKindForTests(string pipelineTypeName, string rendererTypeName)
        {
            return ClassifyPipelineKind(pipelineTypeName, rendererTypeName);
        }

        internal static bool HasSrpRenderOverloadForTests => PreviewRenderWithSrpMethod != null;

        internal static bool ShouldPreferSrpRenderForTests(PreviewRenderPipelineKind kind)
        {
            return ShouldPreferSrpRender(kind);
        }

        private static bool TryRenderWithSrp(PreviewRenderUtility preview)
        {
            if (preview == null || PreviewRenderWithSrpMethod == null)
                return false;

            try
            {
                PreviewRenderWithSrpMethod.Invoke(preview, new object[] { true });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldPreferSrpRender(PreviewRenderPipelineKind kind)
        {
            return kind != PreviewRenderPipelineKind.BuiltIn;
        }

        private static PreviewRenderPipelineKind ClassifyPipelineKind(string pipelineTypeName, string rendererTypeName)
        {
            if (string.IsNullOrEmpty(pipelineTypeName))
                return PreviewRenderPipelineKind.BuiltIn;

            if (ContainsIgnoreCase(pipelineTypeName, "UniversalRenderPipeline"))
            {
                return ContainsIgnoreCase(rendererTypeName, "Renderer2DData")
                    ? PreviewRenderPipelineKind.Urp2D
                    : PreviewRenderPipelineKind.Urp3D;
            }

            if (ContainsIgnoreCase(pipelineTypeName, "HDRenderPipeline")
                || ContainsIgnoreCase(pipelineTypeName, "HighDefinitionRenderPipeline")
                || ContainsIgnoreCase(pipelineTypeName, "HDRP"))
            {
                return PreviewRenderPipelineKind.Hdrp;
            }

            return PreviewRenderPipelineKind.UnknownSrp;
        }

        private static string TryGetDefaultRendererTypeName(RenderPipelineAsset pipelineAsset)
        {
            if (pipelineAsset == null)
                return null;

            string pipelineTypeName = pipelineAsset.GetType().FullName ?? pipelineAsset.GetType().Name ?? string.Empty;
            if (!ContainsIgnoreCase(pipelineTypeName, "UniversalRenderPipeline"))
                return null;

            try
            {
                SerializedObject serializedPipeline = new SerializedObject(pipelineAsset);
                SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
                SerializedProperty defaultRendererIndexProperty = serializedPipeline.FindProperty("m_DefaultRendererIndex");
                if (rendererList != null && rendererList.isArray && rendererList.arraySize > 0)
                {
                    int defaultIndex = defaultRendererIndexProperty != null
                        ? Mathf.Clamp(defaultRendererIndexProperty.intValue, 0, rendererList.arraySize - 1)
                        : 0;
                    SerializedProperty rendererEntry = rendererList.GetArrayElementAtIndex(defaultIndex);
                    UnityEngine.Object rendererData = rendererEntry != null ? rendererEntry.objectReferenceValue : null;
                    if (rendererData != null)
                        return rendererData.GetType().FullName ?? rendererData.GetType().Name;
                }

                SerializedProperty singleRendererData = serializedPipeline.FindProperty("m_RendererData");
                if (singleRendererData != null && singleRendererData.objectReferenceValue != null)
                {
                    UnityEngine.Object rendererData = singleRendererData.objectReferenceValue;
                    return rendererData.GetType().FullName ?? rendererData.GetType().Name;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static bool ContainsIgnoreCase(string value, string token)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
                return false;

            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void LogDiagnosticOnce(string key, string message)
        {
            if (!EnableDiagnostics || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
                return;

            if (!LoggedDiagnosticKeys.Add(key))
                return;

            Debug.Log("[ParticleRenderCompat] " + message);
        }
    }
}
