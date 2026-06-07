using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
// Renders particle prefab thumbnails in isolated preview scenes and outputs textures suitable for Project window display.

namespace NoodleHammer.PreviewForge.Editor
{
    internal static class ParticleThumbnailRenderer
    {
        private const float CoarseSampleStep = 1f / 30f;
        private const float RefineSampleStep = 1f / 120f;
        private const float EarlyBurstSampleStep = 1f / 180f;
        private const float EarlyBurstScanSeconds = 0.6f;
        private const float IntensityWindowSampleStep = 1f / 180f;
        private const float HighIntensityThresholdNormalized = 0.9f;
        private const float RefineWindowHalfSpan = 0.08f;
        private const float FallbackStep = 1f / 60f;
        private const float SimulationStep = 1f / 60f;
        private const int MaxParticleBuffer = 10000;
        private const int MaxRefineSeeds = 5;
        private const float MinRefineSeedSeparation = 1f / 30f;
        private const float MinAxisExtent = 0.02f;
        private const int MinParticlesForTightFraming = 8;
        private const float BaseMinFillDistanceScale = 0.68f;
        private const float TinyBurstMinFillDistanceScale = 0.50f;
        private const float TinyBurstCoverageLow = 0.06f;
        private const float TinyBurstCoverageHigh = 0.20f;
        private const float MinInViewAfterFill = 0.82f;
        private const int FillSafetyIterations = 4;
        private const uint DeterministicSeedBase = 101u;
        private static readonly int GameObjectWorldPositionShaderId = Shader.PropertyToID("_GameObjectWorldPosition");

        private static readonly ParticleSystem.Particle[] ParticleBuffer = new ParticleSystem.Particle[MaxParticleBuffer];

        public static Texture2D Render(string assetPath, ParticleThumbnailSurface surface)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                return null;

            GameObject instance = null;
            PreviewRenderUtility preview = null;
            Renderer[] renderers = null;
            bool[] rendererInitialStates = null;

            try
            {
                int thumbnailSize = ParticleThumbnailSettings.GetRenderSize(surface);

                instance = Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                ForceActivateHierarchy(instance);
                Vector3 authoredRootPosition = instance.transform.position;
                Quaternion authoredRootRotation = instance.transform.rotation;

                ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                if (systems == null || systems.Length == 0)
                    return null;
                EnsureDeterministicSeeds(systems, instance.transform);

                preview = new PreviewRenderUtility(true);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = ParticleThumbnailSettings.BackgroundColor;
                preview.cameraFieldOfView = ParticleThumbnailSettings.CameraFov;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = 1000f;
                preview.lights[0].intensity = 1.3f;
                preview.lights[1].intensity = 0f;
                preview.AddSingleGO(instance);

                renderers = instance.GetComponentsInChildren<Renderer>(true);
                rendererInitialStates = CaptureRendererEnabledStates(renderers);
                PreviewRenderCompatibilityUtility.SetRenderersEnabled(renderers, false);

                bool needsMotion = ParticleMotionDetectionUtility.NeedsMotion(systems);

                float coarseScanMax = Mathf.Min(GetScanMaxSeconds(systems, useMaxLifetime: false), ParticleThumbnailSettings.ScanMaxSeconds);
                bool foundBest = TryFindBestCandidate(
                    systems,
                    instance,
                    preview.camera,
                    needsMotion,
                    coarseScanMax,
                    authoredRootPosition,
                    authoredRootRotation,
                    out ParticleFrameCandidate bestCandidate);

                float targetTime = foundBest ? bestCandidate.Time : CoarseSampleStep;
                Bounds targetBounds = foundBest ? bestCandidate.Bounds : new Bounds(Vector3.zero, Vector3.one * 2f);

                ResetSimulation(systems, instance, needsMotion, authoredRootPosition, authoredRootRotation);
                float elapsed = 0f;
                bool firstStep = true;
                AdvanceSimulationTo(
                    systems,
                    instance,
                    needsMotion,
                    targetTime,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref elapsed,
                    ref firstStep);

                if (CountLiveParticles(systems) <= 0)
                {
                    float fallbackScanMax = Mathf.Min(GetScanMaxSeconds(systems, useMaxLifetime: true), ParticleThumbnailSettings.ScanMaxSeconds);
                    float fallbackTime = FindFirstVisibleTime(
                        systems,
                        instance,
                        preview.camera,
                        needsMotion,
                        fallbackScanMax,
                        authoredRootPosition,
                        authoredRootRotation,
                        out Bounds fallbackBounds);
                    if (fallbackTime >= 0f)
                    {
                        targetTime = fallbackTime;
                        targetBounds = fallbackBounds;
                        ResetSimulation(systems, instance, needsMotion, authoredRootPosition, authoredRootRotation);
                        elapsed = 0f;
                        firstStep = true;
                        AdvanceSimulationTo(
                            systems,
                            instance,
                            needsMotion,
                            targetTime,
                            authoredRootPosition,
                            authoredRootRotation,
                            ref elapsed,
                            ref firstStep);
                    }
                }

                if (targetBounds.size.sqrMagnitude <= 0.0001f)
                    targetBounds = ComputeVisualBounds(systems);

                FrameCamera(preview.camera, preview.lights[0], targetBounds, needsMotion, applyTargetFill: true);

                return RenderCurrentFrame(preview, renderers, rendererInitialStates, thumbnailSize, targetTime);
            }
            finally
            {
                if (preview != null)
                    preview.Cleanup();

                if (instance != null)
                    Object.DestroyImmediate(instance);
            }
        }

        private static bool TryFindBestCandidate(
            ParticleSystem[] systems,
            GameObject instance,
            Camera camera,
            bool needsMotion,
            float scanMax,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation,
            out ParticleFrameCandidate bestCandidate)
        {
            bestCandidate = default;
            if (scanMax <= 0f)
                return false;

            int peakLiveCount = 0;
            bool hasCoverageFallback = false;
            ParticleFrameCandidate coverageFallback = default;

            List<ParticleFrameCandidate> coarseCandidates = new List<ParticleFrameCandidate>(96);
            SampleCandidatesInRange(
                systems,
                instance,
                camera,
                needsMotion,
                startTime: 0f,
                endTime: scanMax,
                sampleStep: CoarseSampleStep,
                coarseCandidates,
                authoredRootPosition,
                authoredRootRotation,
                ref peakLiveCount,
                ref hasCoverageFallback,
                ref coverageFallback);

            if (coarseCandidates.Count == 0)
            {
                if (hasCoverageFallback)
                {
                    bestCandidate = coverageFallback;
                    return true;
                }

                return false;
            }

            List<ParticleFrameCandidate> allCandidates = new List<ParticleFrameCandidate>(coarseCandidates.Count + 128);
            allCandidates.AddRange(coarseCandidates);

            float earlyBurstEnd = Mathf.Min(scanMax, EarlyBurstScanSeconds);
            if (earlyBurstEnd > 0.0001f)
            {
                SampleCandidatesInRange(
                    systems,
                    instance,
                    camera,
                    needsMotion,
                    startTime: 0f,
                    endTime: earlyBurstEnd,
                    sampleStep: EarlyBurstSampleStep,
                    allCandidates,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref peakLiveCount,
                    ref hasCoverageFallback,
                    ref coverageFallback);
            }

            List<float> refineSeeds = SelectTopRefineSeedTimes(coarseCandidates, MaxRefineSeeds);
            List<Vector2> refineRanges = BuildMergedRefineRanges(refineSeeds, scanMax, RefineWindowHalfSpan);
            for (int i = 0; i < refineRanges.Count; i++)
            {
                Vector2 range = refineRanges[i];
                SampleCandidatesInRange(
                    systems,
                    instance,
                    camera,
                    needsMotion,
                    startTime: range.x,
                    endTime: range.y,
                    sampleStep: RefineSampleStep,
                    allCandidates,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref peakLiveCount,
                    ref hasCoverageFallback,
                    ref coverageFallback);
            }

            if (TrySelectHighIntensityWindowMidpointCandidate(
                    systems,
                    instance,
                    camera,
                    needsMotion,
                    scanMax,
                    peakLiveCount,
                    authoredRootPosition,
                    authoredRootRotation,
                    out ParticleFrameCandidate intensityWindowCandidate))
            {
                bestCandidate = intensityWindowCandidate;
                return true;
            }

            if (TrySelectPeakAliveCandidate(allCandidates, out ParticleFrameCandidate peakCandidate))
            {
                bestCandidate = peakCandidate;
                return true;
            }

            if (hasCoverageFallback)
            {
                bestCandidate = coverageFallback;
                return true;
            }

            return false;
        }

        private static bool TrySelectHighIntensityWindowMidpointCandidate(
            ParticleSystem[] systems,
            GameObject instance,
            Camera camera,
            bool needsMotion,
            float scanMax,
            int peakLiveCount,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation,
            out ParticleFrameCandidate candidate)
        {
            candidate = default;
            if (scanMax <= 0f || peakLiveCount <= 0)
                return false;

            int liveThreshold = Mathf.Max(1, Mathf.CeilToInt(peakLiveCount * HighIntensityThresholdNormalized));
            float sampleStep = Mathf.Max(1f / 1000f, IntensityWindowSampleStep);

            ResetSimulation(systems, instance, needsMotion, authoredRootPosition, authoredRootRotation);
            float elapsed = 0f;
            bool firstStep = true;

            bool inWindow = false;
            float windowStart = 0f;
            float windowEnd = 0f;
            int windowSampleCount = 0;
            int windowLiveSum = 0;
            int windowPeakLive = 0;
            float windowPeakTime = 0f;

            bool hasBestWindow = false;
            float bestWindowStart = 0f;
            float bestWindowEnd = 0f;
            float bestWindowPeakTime = 0f;
            float bestWindowScore = -1f;
            float bestWindowDuration = 0f;

            while (elapsed < scanMax - 0.0001f)
            {
                float targetTime = Mathf.Min(scanMax, elapsed + sampleStep);
                AdvanceSimulationTo(
                    systems,
                    instance,
                    needsMotion,
                    targetTime,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref elapsed,
                    ref firstStep);

                int liveCount = CountLiveParticles(systems);
                bool qualifies = liveCount >= liveThreshold;

                if (qualifies)
                {
                    if (!inWindow)
                    {
                        inWindow = true;
                        windowStart = elapsed;
                        windowEnd = elapsed;
                        windowSampleCount = 0;
                        windowLiveSum = 0;
                        windowPeakLive = 0;
                        windowPeakTime = elapsed;
                    }

                    windowEnd = elapsed;
                    windowSampleCount++;
                    windowLiveSum += liveCount;
                    if (liveCount > windowPeakLive)
                    {
                        windowPeakLive = liveCount;
                        windowPeakTime = elapsed;
                    }
                }
                else if (inWindow)
                {
                    EvaluateIntensityWindow(
                        peakLiveCount,
                        windowStart,
                        windowEnd,
                        windowSampleCount,
                        windowLiveSum,
                        windowPeakLive,
                        windowPeakTime,
                        ref hasBestWindow,
                        ref bestWindowStart,
                        ref bestWindowEnd,
                        ref bestWindowPeakTime,
                        ref bestWindowScore,
                        ref bestWindowDuration);
                    inWindow = false;
                }
            }

            if (inWindow)
            {
                EvaluateIntensityWindow(
                    peakLiveCount,
                    windowStart,
                    windowEnd,
                    windowSampleCount,
                    windowLiveSum,
                    windowPeakLive,
                    windowPeakTime,
                    ref hasBestWindow,
                    ref bestWindowStart,
                    ref bestWindowEnd,
                    ref bestWindowPeakTime,
                    ref bestWindowScore,
                    ref bestWindowDuration);
            }

            if (!hasBestWindow)
                return false;

            float midpointTime = (bestWindowStart + bestWindowEnd) * 0.5f;
            if (TrySampleCandidateAtTime(
                    systems,
                    instance,
                    camera,
                    needsMotion,
                    midpointTime,
                    authoredRootPosition,
                    authoredRootRotation,
                    out candidate))
            {
                return true;
            }

            return TrySampleCandidateAtTime(
                systems,
                instance,
                camera,
                needsMotion,
                bestWindowPeakTime,
                authoredRootPosition,
                authoredRootRotation,
                out candidate);
        }

        private static void EvaluateIntensityWindow(
            int peakLiveCount,
            float windowStart,
            float windowEnd,
            int windowSampleCount,
            int windowLiveSum,
            int windowPeakLive,
            float windowPeakTime,
            ref bool hasBestWindow,
            ref float bestWindowStart,
            ref float bestWindowEnd,
            ref float bestWindowPeakTime,
            ref float bestWindowScore,
            ref float bestWindowDuration)
        {
            if (windowSampleCount <= 0 || peakLiveCount <= 0)
                return;

            float duration = Mathf.Max(0f, windowEnd - windowStart);
            float averageNormalized = Mathf.Clamp01((float)windowLiveSum / (windowSampleCount * peakLiveCount));
            float peakNormalized = Mathf.Clamp01((float)windowPeakLive / peakLiveCount);
            float score = averageNormalized * 0.75f + peakNormalized * 0.25f;

            bool isBetter = !hasBestWindow
                || score > bestWindowScore + 0.0001f
                || (Mathf.Abs(score - bestWindowScore) <= 0.0001f && duration > bestWindowDuration + 0.0001f)
                || (Mathf.Abs(score - bestWindowScore) <= 0.0001f
                    && Mathf.Abs(duration - bestWindowDuration) <= 0.0001f
                    && windowStart < bestWindowStart);

            if (!isBetter)
                return;

            hasBestWindow = true;
            bestWindowStart = windowStart;
            bestWindowEnd = windowEnd;
            bestWindowPeakTime = windowPeakTime;
            bestWindowScore = score;
            bestWindowDuration = duration;
        }

        private static void SampleCandidatesInRange(
            ParticleSystem[] systems,
            GameObject instance,
            Camera camera,
            bool needsMotion,
            float startTime,
            float endTime,
            float sampleStep,
            List<ParticleFrameCandidate> output,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation,
            ref int peakLiveCount,
            ref bool hasCoverageFallback,
            ref ParticleFrameCandidate coverageFallback)
        {
            if (output == null)
                return;

            float clampedStart = Mathf.Max(0f, startTime);
            float clampedEnd = Mathf.Max(clampedStart, endTime);
            if (clampedEnd - clampedStart <= 0.0001f)
                return;

            float step = Mathf.Max(1f / 1000f, sampleStep);

            ResetSimulation(systems, instance, needsMotion, authoredRootPosition, authoredRootRotation);
            float elapsed = 0f;
            bool firstStep = true;

            if (clampedStart > 0f)
            {
                AdvanceSimulationTo(
                    systems,
                    instance,
                    needsMotion,
                    clampedStart,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref elapsed,
                    ref firstStep);
            }

            while (elapsed < clampedEnd - 0.0001f)
            {
                float targetTime = Mathf.Min(clampedEnd, elapsed + step);
                AdvanceSimulationTo(
                    systems,
                    instance,
                    needsMotion,
                    targetTime,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref elapsed,
                    ref firstStep);

                int liveCount = CountLiveParticles(systems);
                if (liveCount <= 0)
                    continue;

                if (liveCount > peakLiveCount)
                    peakLiveCount = liveCount;

                Bounds bounds = ComputeVisualBounds(systems);
                FrameCamera(camera, null, bounds, needsMotion, applyTargetFill: false);
                EstimateVisibilityMetrics(camera, bounds, out float coverage, out float inViewRatio, out float centerBias);

                ParticleFrameCandidate candidate = new ParticleFrameCandidate(
                    elapsed,
                    score: 0f,
                    bounds,
                    liveCount,
                    coverage,
                    inViewRatio,
                    centerBias);

                output.Add(candidate);

                if (!hasCoverageFallback
                    || coverage > coverageFallback.Coverage
                    || (Mathf.Approximately(coverage, coverageFallback.Coverage) && inViewRatio > coverageFallback.InViewRatio))
                {
                    coverageFallback = new ParticleFrameCandidate(
                        elapsed,
                        score: 0f,
                        bounds,
                        liveCount,
                        coverage,
                        inViewRatio,
                        centerBias);
                    hasCoverageFallback = true;
                }
            }
        }

        private static bool TrySelectPeakAliveCandidate(List<ParticleFrameCandidate> candidates, out ParticleFrameCandidate bestCandidate)
        {
            bestCandidate = default;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                ParticleFrameCandidate candidate = candidates[i];
                if (!found
                    || candidate.LiveCount > bestCandidate.LiveCount
                    || (candidate.LiveCount == bestCandidate.LiveCount && candidate.Time < bestCandidate.Time))
                {
                    bestCandidate = candidate;
                    found = true;
                }
            }

            return found;
        }

        private static bool TrySampleCandidateAtTime(
            ParticleSystem[] systems,
            GameObject instance,
            Camera camera,
            bool needsMotion,
            float sampleTime,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation,
            out ParticleFrameCandidate candidate)
        {
            candidate = default;
            float clampedTime = Mathf.Max(0f, sampleTime);

            ResetSimulation(systems, instance, needsMotion, authoredRootPosition, authoredRootRotation);
            float elapsed = 0f;
            bool firstStep = true;
            AdvanceSimulationTo(
                systems,
                instance,
                needsMotion,
                clampedTime,
                authoredRootPosition,
                authoredRootRotation,
                ref elapsed,
                ref firstStep);

            int liveCount = CountLiveParticles(systems);
            if (liveCount <= 0)
                return false;

            Bounds bounds = ComputeVisualBounds(systems);
            FrameCamera(camera, null, bounds, needsMotion, applyTargetFill: false);
            EstimateVisibilityMetrics(camera, bounds, out float coverage, out float inViewRatio, out float centerBias);
            candidate = new ParticleFrameCandidate(
                clampedTime,
                score: 0f,
                bounds,
                liveCount,
                coverage,
                inViewRatio,
                centerBias);
            return true;
        }

        private static List<float> SelectTopRefineSeedTimes(List<ParticleFrameCandidate> coarseCandidates, int maxSeeds)
        {
            List<ParticleFrameCandidate> scored = new List<ParticleFrameCandidate>(coarseCandidates.Count);
            for (int i = 0; i < coarseCandidates.Count; i++)
            {
                scored.Add(coarseCandidates[i]);
            }

            scored.Sort((a, b) =>
            {
                int byLive = b.LiveCount.CompareTo(a.LiveCount);
                if (byLive != 0)
                    return byLive;

                int byCoverage = b.Coverage.CompareTo(a.Coverage);
                if (byCoverage != 0)
                    return byCoverage;

                return b.InViewRatio.CompareTo(a.InViewRatio);
            });

            List<float> seeds = new List<float>(Mathf.Max(0, maxSeeds));
            for (int i = 0; i < scored.Count && seeds.Count < maxSeeds; i++)
            {
                float time = scored[i].Time;
                bool tooClose = false;
                for (int j = 0; j < seeds.Count; j++)
                {
                    if (Mathf.Abs(seeds[j] - time) < MinRefineSeedSeparation)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    seeds.Add(time);
            }

            return seeds;
        }

        private static List<Vector2> BuildMergedRefineRanges(List<float> seedTimes, float scanMax, float halfSpan)
        {
            List<Vector2> ranges = new List<Vector2>();
            if (seedTimes == null || seedTimes.Count == 0)
                return ranges;

            List<float> sorted = new List<float>(seedTimes);
            sorted.Sort();

            float clampedHalfSpan = Mathf.Max(0f, halfSpan);
            float clampedScanMax = Mathf.Max(0f, scanMax);

            for (int i = 0; i < sorted.Count; i++)
            {
                float seed = Mathf.Clamp(sorted[i], 0f, clampedScanMax);
                float start = Mathf.Max(0f, seed - clampedHalfSpan);
                float end = Mathf.Min(clampedScanMax, seed + clampedHalfSpan);
                if (end <= start + 0.0001f)
                    continue;

                if (ranges.Count == 0)
                {
                    ranges.Add(new Vector2(start, end));
                    continue;
                }

                int last = ranges.Count - 1;
                Vector2 previous = ranges[last];
                if (start <= previous.y + 0.0001f)
                {
                    ranges[last] = new Vector2(previous.x, Mathf.Max(previous.y, end));
                }
                else
                {
                    ranges.Add(new Vector2(start, end));
                }
            }

            return ranges;
        }

        internal static List<float> BuildRefineSampleTimelineForTests(float[] seedTimes, float scanMax, float halfSpan, float step)
        {
            List<float> seeds = new List<float>();
            if (seedTimes != null)
            {
                for (int i = 0; i < seedTimes.Length; i++)
                    seeds.Add(seedTimes[i]);
            }

            List<Vector2> ranges = BuildMergedRefineRanges(seeds, scanMax, halfSpan);
            List<float> timeline = new List<float>();
            float sampleStep = Mathf.Max(1f / 1000f, step);

            for (int i = 0; i < ranges.Count; i++)
            {
                float elapsed = ranges[i].x;
                while (elapsed < ranges[i].y - 0.0001f)
                {
                    elapsed = Mathf.Min(ranges[i].y, elapsed + sampleStep);
                    timeline.Add(elapsed);
                }
            }

            return timeline;
        }

        private static float FindFirstVisibleTime(
            ParticleSystem[] systems,
            GameObject instance,
            Camera camera,
            bool needsMotion,
            float scanMax,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation,
            out Bounds visibleBounds)
        {
            visibleBounds = default;
            ResetSimulation(systems, instance, needsMotion, authoredRootPosition, authoredRootRotation);
            float elapsed = 0f;
            bool firstStep = true;

            while (elapsed < scanMax - 0.0001f)
            {
                float targetTime = Mathf.Min(scanMax, elapsed + FallbackStep);
                AdvanceSimulationTo(
                    systems,
                    instance,
                    needsMotion,
                    targetTime,
                    authoredRootPosition,
                    authoredRootRotation,
                    ref elapsed,
                    ref firstStep);

                int liveCount = CountLiveParticles(systems);
                if (liveCount <= 0)
                    continue;

                Bounds bounds = ComputeVisualBounds(systems);
                FrameCamera(camera, null, bounds, needsMotion, applyTargetFill: false);
                EstimateVisibilityMetrics(camera, bounds, out float coverage, out _, out _);
                if (coverage > 0.0001f)
                {
                    visibleBounds = bounds;
                    return elapsed;
                }
            }

            return -1f;
        }

        private static Texture2D RenderCurrentFrame(
            PreviewRenderUtility preview,
            Renderer[] renderers,
            IReadOnlyList<bool> rendererEnabledStates,
            int thumbnailSize,
            float frameTime)
        {
            using (PreviewRenderCompatibilityUtility.PushShaderTime(frameTime))
            {
                SyncWorldPositionShaderOffsets(renderers);
                preview.camera.backgroundColor = ParticleThumbnailSettings.BackgroundColor;
                preview.BeginPreview(new Rect(0f, 0f, thumbnailSize, thumbnailSize), GUIStyle.none);
                using (PreviewRenderCompatibilityUtility.EnableRenderersScoped(renderers, rendererEnabledStates))
                {
                    PreviewRenderCompatibilityUtility.RenderPreviewWithCameraPath(preview);
                }

                Texture result = preview.EndPreview();
                if (result == null)
                    return null;

                RenderTexture captureRt = RenderTexture.GetTemporary(thumbnailSize, thumbnailSize, 0, RenderTextureFormat.ARGB32);
                captureRt.filterMode = FilterMode.Bilinear;

                RenderTexture previousActive = RenderTexture.active;
                try
                {
                    Graphics.Blit(result, captureRt);
                    RenderTexture.active = captureRt;

                    Texture2D output = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.RGBA32, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "ParticleThumbnail"
                    };
                    output.ReadPixels(new Rect(0f, 0f, thumbnailSize, thumbnailSize), 0, 0);
                    output.Apply(false, false);
                    return output;
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(captureRt);
                }
            }
        }

        private static bool[] CaptureRendererEnabledStates(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return new bool[0];

            var states = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                states[i] = renderer != null && renderer.enabled;
            }

            return states;
        }

        private static void SyncWorldPositionShaderOffsets(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return;

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                bool requiresOffsetSync = false;
                for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    Material material = sharedMaterials[materialIndex];
                    if (material == null
                        || !material.HasProperty(GameObjectWorldPositionShaderId)
                        || !material.IsKeywordEnabled("_CFXR_LIGHTING_WPOS_OFFSET"))
                    {
                        continue;
                    }

                    requiresOffsetSync = true;
                    break;
                }

                if (!requiresOffsetSync)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(GameObjectWorldPositionShaderId, renderer.transform.position);
                renderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private static float GetScanMaxSeconds(ParticleSystem[] systems, bool useMaxLifetime)
        {
            float max = 0.1f;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                ParticleSystem.MainModule main = ps.main;
                float lifetime = useMaxLifetime
                    ? GetLifetime(main.startLifetime, wantMax: true)
                    : GetLifetime(main.startLifetime, wantMax: false);
                max = Mathf.Max(max, main.duration + lifetime);
            }

            return max;
        }

        private static float GetLifetime(ParticleSystem.MinMaxCurve curve, bool wantMax)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return wantMax ? Mathf.Max(curve.constantMin, curve.constantMax) : Mathf.Min(curve.constantMin, curve.constantMax);
                default:
                    return curve.curveMultiplier;
            }
        }

        private static void ResetSimulation(
            ParticleSystem[] systems,
            GameObject instance,
            bool needsMotion,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                ps.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(withChildren: false);
            }

            if (instance == null)
                return;

            if (needsMotion)
            {
                ApplyMotionPose(instance, 0f, authoredRootPosition, authoredRootRotation);
            }
            else
            {
                instance.transform.position = authoredRootPosition;
                instance.transform.rotation = authoredRootRotation;
            }
        }

        private static void AdvanceSimulationTo(
            ParticleSystem[] systems,
            GameObject instance,
            bool needsMotion,
            float targetTime,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation,
            ref float elapsed,
            ref bool firstStep)
        {
            while (elapsed < targetTime - 0.0001f)
            {
                float dt = Mathf.Min(SimulationStep, targetTime - elapsed);
                float nextElapsed = elapsed + dt;

                if (needsMotion && instance != null)
                    ApplyMotionPose(instance, nextElapsed, authoredRootPosition, authoredRootRotation);

                for (int i = 0; i < systems.Length; i++)
                {
                    ParticleSystem ps = systems[i];
                    if (ps == null)
                        continue;

                    ps.Simulate(dt, withChildren: false, restart: firstStep, fixedTimeStep: false);
                }

                elapsed = nextElapsed;
                firstStep = false;
            }
        }

        private static int CountLiveParticles(ParticleSystem[] systems)
        {
            int total = 0;
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps != null)
                    total += ps.particleCount;
            }

            return total;
        }

        private static void EnsureDeterministicSeeds(ParticleSystem[] systems, Transform root)
        {
            if (systems == null || systems.Length == 0 || root == null)
                return;

            List<(string key, ParticleSystem system)> orderedSystems = new List<(string, ParticleSystem)>(systems.Length);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                string seedKey = BuildSeedKey(ps, root);
                orderedSystems.Add((seedKey, ps));
            }

            orderedSystems.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

            uint seed = DeterministicSeedBase;
            for (int i = 0; i < orderedSystems.Count; i++)
            {
                ParticleSystem ps = orderedSystems[i].system;
                if (ps == null)
                    continue;

                ps.Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(withChildren: false);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++;
            }
        }

        private static string BuildSeedKey(ParticleSystem ps, Transform root)
        {
            if (ps == null || root == null)
                return "<null>";

            StringBuilder builder = new StringBuilder(96);
            Transform current = ps.transform;
            if (current == root)
            {
                builder.Append("<root>");
            }
            else
            {
                List<string> segments = new List<string>(8);
                while (current != null && current != root)
                {
                    segments.Add(current.name);
                    current = current.parent;
                }

                for (int i = segments.Count - 1; i >= 0; i--)
                {
                    if (builder.Length > 0)
                        builder.Append('/');

                    builder.Append(segments[i]);
                }
            }

            ParticleSystem[] systemsOnTransform = ps.transform.GetComponents<ParticleSystem>();
            int localIndex = 0;
            for (int i = 0; i < systemsOnTransform.Length; i++)
            {
                if (systemsOnTransform[i] == ps)
                {
                    localIndex = i;
                    break;
                }
            }

            builder.Append('#');
            builder.Append(localIndex);
            return builder.ToString();
        }

        private static Bounds ComputeVisualBounds(ParticleSystem[] systems)
        {
            bool hasParticles = false;
            Bounds fullBounds = new Bounds(Vector3.zero, Vector3.zero);
            List<float> minXs = ParticleThumbnailSettings.EnableTightFraming ? new List<float>(128) : null;
            List<float> minYs = ParticleThumbnailSettings.EnableTightFraming ? new List<float>(128) : null;
            List<float> minZs = ParticleThumbnailSettings.EnableTightFraming ? new List<float>(128) : null;
            List<float> maxXs = ParticleThumbnailSettings.EnableTightFraming ? new List<float>(128) : null;
            List<float> maxYs = ParticleThumbnailSettings.EnableTightFraming ? new List<float>(128) : null;
            List<float> maxZs = ParticleThumbnailSettings.EnableTightFraming ? new List<float>(128) : null;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem ps = systems[i];
                if (ps == null)
                    continue;

                int count = ps.GetParticles(ParticleBuffer);
                for (int j = 0; j < count; j++)
                {
                    Vector3 position = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                        ? ParticleBuffer[j].position
                        : ps.transform.TransformPoint(ParticleBuffer[j].position);

                    Vector3 size3 = ParticleBuffer[j].GetCurrentSize3D(ps);
                    Vector3 particleSize = new Vector3(
                        Mathf.Max(Mathf.Abs(size3.x), MinAxisExtent),
                        Mathf.Max(Mathf.Abs(size3.y), MinAxisExtent),
                        Mathf.Max(Mathf.Abs(size3.z), MinAxisExtent));
                    Bounds particleBounds = new Bounds(position, particleSize);

                    if (!hasParticles)
                    {
                        fullBounds = particleBounds;
                        hasParticles = true;
                    }
                    else
                    {
                        fullBounds.Encapsulate(particleBounds);
                    }

                    if (minXs != null)
                    {
                        Vector3 extents = particleBounds.extents;
                        minXs.Add(position.x - extents.x);
                        minYs.Add(position.y - extents.y);
                        minZs.Add(position.z - extents.z);
                        maxXs.Add(position.x + extents.x);
                        maxYs.Add(position.y + extents.y);
                        maxZs.Add(position.z + extents.z);
                    }
                }
            }

            if (!hasParticles || fullBounds.size.sqrMagnitude <= 0.0001f)
                return new Bounds(Vector3.zero, Vector3.one * 2f);

            Bounds result = fullBounds;
            if (ParticleThumbnailSettings.EnableTightFraming
                && minXs != null
                && minXs.Count >= MinParticlesForTightFraming
                && TryBuildPercentileBounds(
                    minXs,
                    minYs,
                    minZs,
                    maxXs,
                    maxYs,
                    maxZs,
                    ParticleThumbnailSettings.ParticleFramingPercentile,
                    out Bounds tightBounds))
            {
                result = tightBounds;
            }

            Vector3 minSize = new Vector3(MinAxisExtent, MinAxisExtent, MinAxisExtent);
            result.size = Vector3.Max(result.size, minSize);
            return result;
        }

        private static bool TryBuildPercentileBounds(
            List<float> minXs,
            List<float> minYs,
            List<float> minZs,
            List<float> maxXs,
            List<float> maxYs,
            List<float> maxZs,
            float percentile,
            out Bounds bounds)
        {
            bounds = default;
            if (minXs == null
                || minYs == null
                || minZs == null
                || maxXs == null
                || maxYs == null
                || maxZs == null)
            {
                return false;
            }

            int count = minXs.Count;
            if (count == 0
                || minYs.Count != count
                || minZs.Count != count
                || maxXs.Count != count
                || maxYs.Count != count
                || maxZs.Count != count)
            {
                return false;
            }

            float trimFraction = Mathf.Clamp01((1f - Mathf.Clamp(percentile, 0.80f, 0.99f)) * 0.5f);
            int trimCount = Mathf.Clamp(Mathf.CeilToInt(count * trimFraction), 0, Mathf.Max(0, (count - 1) / 2));
            if (trimCount <= 0)
                return false;

            minXs.Sort();
            minYs.Sort();
            minZs.Sort();
            maxXs.Sort();
            maxYs.Sort();
            maxZs.Sort();

            int lowIndex = trimCount;
            int highIndex = count - trimCount - 1;
            if (lowIndex >= highIndex)
                return false;

            Vector3 min = new Vector3(minXs[lowIndex], minYs[lowIndex], minZs[lowIndex]);
            Vector3 max = new Vector3(maxXs[highIndex], maxYs[highIndex], maxZs[highIndex]);
            if (max.x < min.x || max.y < min.y || max.z < min.z)
                return false;

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static void EstimateVisibilityMetrics(Camera camera, Bounds bounds, out float coverage, out float inViewRatio, out float centerBias)
        {
            TryGetViewportMetrics(camera, bounds, out coverage, out inViewRatio);
            centerBias = EstimateCenterBias(camera, bounds);
        }

        private static bool TryGetViewportMetrics(Camera camera, Bounds bounds, out float coverage, out float inViewRatio)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            int inFrontCorners = 0;

            Vector3 extents = bounds.extents;
            Vector3 center = bounds.center;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = center + Vector3.Scale(extents, new Vector3(sx, sy, sz));
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                if (viewport.z <= 0f)
                    continue;

                inFrontCorners++;
                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            if (inFrontCorners <= 0)
            {
                coverage = 0f;
                inViewRatio = 0f;
                return false;
            }

            float unclampedWidth = Mathf.Max(0f, maxX - minX);
            float unclampedHeight = Mathf.Max(0f, maxY - minY);
            float unclampedArea = unclampedWidth * unclampedHeight;

            float clampedMinX = Mathf.Clamp01(minX);
            float clampedMinY = Mathf.Clamp01(minY);
            float clampedMaxX = Mathf.Clamp01(maxX);
            float clampedMaxY = Mathf.Clamp01(maxY);
            float clampedWidth = Mathf.Max(0f, clampedMaxX - clampedMinX);
            float clampedHeight = Mathf.Max(0f, clampedMaxY - clampedMinY);
            float clampedArea = Mathf.Clamp01(clampedWidth * clampedHeight);

            coverage = clampedArea;

            float inFrontRatio = inFrontCorners / 8f;
            if (unclampedArea <= 0.000001f)
            {
                inViewRatio = inFrontRatio;
            }
            else
            {
                inViewRatio = Mathf.Clamp01((clampedArea / unclampedArea) * inFrontRatio);
            }

            return true;
        }

        private static float EstimateScreenCoverage(Camera camera, Bounds bounds)
        {
            return TryGetViewportMetrics(camera, bounds, out float coverage, out _) ? coverage : 0f;
        }

        private static float EstimateInViewRatio(Camera camera, Bounds bounds)
        {
            return TryGetViewportMetrics(camera, bounds, out _, out float inViewRatio) ? inViewRatio : 0f;
        }

        private static float EstimateCenterBias(Camera camera, Bounds bounds)
        {
            Vector3 viewport = camera.WorldToViewportPoint(bounds.center);
            if (viewport.z <= 0f)
                return 0f;

            float distance = Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
            return 1f - Mathf.Clamp01(distance / 0.70710677f);
        }

        private static void FrameCamera(Camera camera, Light mainLight, Bounds bounds, bool needsMotion, bool applyTargetFill)
        {
            Quaternion cameraRotation = Quaternion.Euler(ParticleThumbnailSettings.CameraPitch, ParticleThumbnailSettings.CameraYaw, 0f);
            Vector3 forward = cameraRotation * Vector3.forward;
            Vector3 right = cameraRotation * Vector3.right;
            Vector3 up = cameraRotation * Vector3.up;

            float distance;
            float maxDepth;
            Vector3 center;

            if (needsMotion)
            {
                center = Vector3.zero;
                float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float radius = ParticleThumbnailSettings.MotionRadius;
                float padding = ParticleThumbnailSettings.MotionPadding;
                distance = Mathf.Max((radius * (1.5f + padding)) / Mathf.Tan(halfFov), 0.3f);
                maxDepth = radius * 3f;
                camera.nearClipPlane = Mathf.Max(0.01f, distance - maxDepth);
                camera.farClipPlane = distance + maxDepth + 1f;
            }
            else
            {
                center = bounds.center;
                float fitDistance = ComputeContainmentDistance(camera, bounds, right, up, forward, out maxDepth);
                distance = fitDistance;

                if (applyTargetFill)
                {
                    SetCameraPose(camera, center, forward, cameraRotation, fitDistance);
                    float coverageAtFit = EstimateScreenCoverage(camera, bounds);
                    float distanceScale = ComputeTargetFillDistanceScale(coverageAtFit, ParticleThumbnailSettings.ThumbnailFillTarget);
                    distance = fitDistance * distanceScale;

                    for (int i = 0; i < FillSafetyIterations && distance < fitDistance; i++)
                    {
                        SetCameraPose(camera, center, forward, cameraRotation, distance);
                        float inViewRatio = EstimateInViewRatio(camera, bounds);
                        if (inViewRatio >= MinInViewAfterFill)
                            break;

                        distance = Mathf.Lerp(distance, fitDistance, 0.5f);
                    }
                }

                camera.nearClipPlane = Mathf.Max(0.01f, distance - maxDepth - 0.5f);
                camera.farClipPlane = distance + maxDepth + 1f;
            }

            SetCameraPose(camera, center, forward, cameraRotation, distance);

            if (mainLight != null)
                mainLight.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
        }

        private static float ComputeContainmentDistance(Camera camera, Bounds bounds, Vector3 right, Vector3 up, Vector3 forward, out float maxDepth)
        {
            float padding = ParticleThumbnailSettings.BoundsPadding;
            Vector3 extents = bounds.extents * (1f + padding);
            float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(halfFov);
            float maxRight = 0f;
            float maxUp = 0f;
            maxDepth = 0f;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = new Vector3(extents.x * sx, extents.y * sy, extents.z * sz);
                maxRight = Mathf.Max(maxRight, Mathf.Abs(Vector3.Dot(corner, right)));
                maxUp = Mathf.Max(maxUp, Mathf.Abs(Vector3.Dot(corner, up)));
                maxDepth = Mathf.Max(maxDepth, Mathf.Abs(Vector3.Dot(corner, forward)));
            }

            return Mathf.Max(maxRight / tanHalf, maxUp / tanHalf, 0.3f);
        }

        private static float ComputeTargetFillDistanceScale(float coverageAtFit, float targetFill)
        {
            if (coverageAtFit <= 0.0001f || targetFill <= 0.0001f)
                return 1f;

            float ratio = Mathf.Clamp(coverageAtFit / targetFill, 0.0001f, 1f);
            float distanceScale = Mathf.Sqrt(ratio);
            float dynamicMinScale = ComputeDynamicMinFillDistanceScale(coverageAtFit);
            return Mathf.Clamp(distanceScale, dynamicMinScale, 1f);
        }

        private static float ComputeDynamicMinFillDistanceScale(float coverageAtFit)
        {
            float t = Mathf.InverseLerp(TinyBurstCoverageLow, TinyBurstCoverageHigh, Mathf.Clamp01(coverageAtFit));
            return Mathf.Lerp(TinyBurstMinFillDistanceScale, BaseMinFillDistanceScale, t);
        }

        internal static float ComputeTargetFillDistanceScaleForTests(float coverageAtFit, float targetFill)
        {
            return ComputeTargetFillDistanceScale(coverageAtFit, targetFill);
        }

        internal static float GetMinFillDistanceScaleForTests(float coverageAtFit)
        {
            return ComputeDynamicMinFillDistanceScale(coverageAtFit);
        }

        internal static List<float> BuildEarlyBurstSampleTimelineForTests(float scanMax)
        {
            List<float> timeline = new List<float>();
            float endTime = Mathf.Min(Mathf.Max(scanMax, 0f), EarlyBurstScanSeconds);
            float elapsed = 0f;

            while (elapsed < endTime - 0.0001f)
            {
                elapsed = Mathf.Min(endTime, elapsed + EarlyBurstSampleStep);
                timeline.Add(elapsed);
            }

            return timeline;
        }

        private static void SetCameraPose(Camera camera, Vector3 center, Vector3 forward, Quaternion cameraRotation, float distance)
        {
            camera.transform.position = center - forward * distance;
            camera.transform.rotation = cameraRotation;
        }

        private static void ApplyMotionPose(
            GameObject instance,
            float time,
            Vector3 authoredRootPosition,
            Quaternion authoredRootRotation)
        {
            const float lookAheadTime = 0.001f;

            Vector3 offset = CirclePosition(time);
            Vector3 nextOffset = CirclePosition(time + lookAheadTime);
            Vector3 direction = nextOffset - offset;

            instance.transform.position = authoredRootPosition + offset;
            if (direction.sqrMagnitude > 0.000001f)
            {
                instance.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * authoredRootRotation;
            }
            else
            {
                instance.transform.rotation = authoredRootRotation;
            }
        }

        private static Vector3 CirclePosition(float time)
        {
            float radius = ParticleThumbnailSettings.MotionRadius;
            float speed = ParticleThumbnailSettings.MotionSpeed;
            float angle = time * (speed / Mathf.Max(radius, 0.0001f));
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private static void ForceActivateHierarchy(GameObject root)
        {
            if (root == null)
                return;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                t.gameObject.SetActive(true);
                t.gameObject.layer = 0;
            }
        }

    }
}
