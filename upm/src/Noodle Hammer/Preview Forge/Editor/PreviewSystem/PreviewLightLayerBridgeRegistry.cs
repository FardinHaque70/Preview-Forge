using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoodleHammer.PreviewForge.Editor
{
    internal interface IPreviewLightLayerBridge
    {
        PreviewRenderPipelineKind PipelineKind { get; }
        string BridgeName { get; }
        void ApplyLightLayers(Light light, uint renderingLayerMask);
    }

    internal readonly struct PreviewLightLayerSupportStatus
    {
        internal readonly PreviewRenderPipelineKind PipelineKind;
        internal readonly bool BridgeRequired;
        internal readonly bool BridgeRegistered;
        internal readonly bool FullBridgeSupport;
        internal readonly bool UsesGenericFallback;
        internal readonly bool LightingDisabledByPipeline;
        internal readonly string BridgeName;

        internal PreviewLightLayerSupportStatus(
            PreviewRenderPipelineKind pipelineKind,
            bool bridgeRequired,
            bool bridgeRegistered,
            bool fullBridgeSupport,
            bool usesGenericFallback,
            bool lightingDisabledByPipeline,
            string bridgeName)
        {
            PipelineKind = pipelineKind;
            BridgeRequired = bridgeRequired;
            BridgeRegistered = bridgeRegistered;
            FullBridgeSupport = fullBridgeSupport;
            UsesGenericFallback = usesGenericFallback;
            LightingDisabledByPipeline = lightingDisabledByPipeline;
            BridgeName = bridgeName;
        }
    }

    internal static class PreviewLightLayerBridgeRegistry
    {
        private static readonly Dictionary<PreviewRenderPipelineKind, IPreviewLightLayerBridge> BridgesByPipeline = new();
        private static readonly HashSet<string> LoggedDiagnosticKeys = new();

        internal static void Register(IPreviewLightLayerBridge bridge)
        {
            if (bridge == null)
                return;

            if (BridgesByPipeline.TryGetValue(bridge.PipelineKind, out IPreviewLightLayerBridge existing))
            {
                if (!ReferenceEquals(existing, bridge))
                {
                    LogOnce(
                        $"duplicate:{bridge.PipelineKind}",
                        $"Ignoring duplicate light-layer bridge '{bridge.BridgeName}' for {bridge.PipelineKind}; already registered '{existing.BridgeName}'.");
                }

                return;
            }

            BridgesByPipeline.Add(bridge.PipelineKind, bridge);
            LogOnce($"registered:{bridge.PipelineKind}", $"Registered light-layer bridge '{bridge.BridgeName}' for {bridge.PipelineKind}.");
        }

        internal static PreviewLightLayerSupportStatus GetSupportStatus(PreviewRenderPipelineKind pipelineKind)
        {
            bool lightingDisabledByPipeline = pipelineKind == PreviewRenderPipelineKind.Urp2D;
            bool bridgeRequired = pipelineKind == PreviewRenderPipelineKind.Urp3D || pipelineKind == PreviewRenderPipelineKind.Hdrp;
            bool bridgeRegistered = BridgesByPipeline.TryGetValue(pipelineKind, out IPreviewLightLayerBridge bridge);
            bool fullBridgeSupport = bridgeRequired && bridgeRegistered;
            bool usesGenericFallback = pipelineKind != PreviewRenderPipelineKind.BuiltIn
                && !lightingDisabledByPipeline
                && !fullBridgeSupport;

            return new PreviewLightLayerSupportStatus(
                pipelineKind,
                bridgeRequired,
                bridgeRegistered,
                fullBridgeSupport,
                usesGenericFallback,
                lightingDisabledByPipeline,
                bridgeRegistered ? bridge.BridgeName : null);
        }

        internal static void ApplyBridge(Light light, PreviewRenderPipelineKind pipelineKind, uint renderingLayerMask)
        {
            if (light == null || !BridgesByPipeline.TryGetValue(pipelineKind, out IPreviewLightLayerBridge bridge))
            {
                LogMissingBridgeFallback(pipelineKind);
                return;
            }

            try
            {
                bridge.ApplyLightLayers(light, renderingLayerMask);
            }
            catch (Exception exception)
            {
                LogOnce(
                    $"bridge-failed:{pipelineKind}",
                    $"Light-layer bridge '{bridge.BridgeName}' failed for {pipelineKind}. Falling back to Light.renderingLayerMask only. {exception.GetType().Name}: {exception.Message}");
            }
        }

        internal static void ClearForTests()
        {
            BridgesByPipeline.Clear();
            LoggedDiagnosticKeys.Clear();
        }

        internal static bool IsBridgeRegisteredForTests(PreviewRenderPipelineKind pipelineKind)
        {
            return BridgesByPipeline.ContainsKey(pipelineKind);
        }

        private static void LogMissingBridgeFallback(PreviewRenderPipelineKind pipelineKind)
        {
            if (pipelineKind == PreviewRenderPipelineKind.BuiltIn || pipelineKind == PreviewRenderPipelineKind.Urp2D)
                return;

            string message = pipelineKind == PreviewRenderPipelineKind.UnknownSrp
                ? "Active SRP has no package-specific preview light-layer bridge. Falling back to Light.renderingLayerMask only."
                : $"Active {pipelineKind} preview has no matching light-layer bridge. Falling back to Light.renderingLayerMask only.";

            LogOnce($"bridge-missing:{pipelineKind}", message);
        }

        private static void LogOnce(string key, string message)
        {
            if (!PreviewSettings.EnableDiagnostics || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(message))
                return;

            if (!LoggedDiagnosticKeys.Add(key))
                return;

            PreviewDiagnostics.Log("PreviewLightLayers", message);
        }
    }
}
