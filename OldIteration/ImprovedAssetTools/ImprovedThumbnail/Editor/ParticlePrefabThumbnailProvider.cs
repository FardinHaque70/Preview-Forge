using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public sealed class ParticlePrefabThumbnailProvider : ThumbnailProviderBase
{
    private const float SampleStep = 0.05f;
    private const float FallbackSampleStep = 1f / 60f;
    private const float MotionSpeed = 60f;
    private const float MotionRadius = 3f;
    private const int MaxParticleBuffer = 10000;

    private readonly ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[MaxParticleBuffer];

    public override string Id => "particle-prefab";
    public override ThumbnailAssetKind AssetKind => ThumbnailAssetKind.ParticlePrefab;
    public override int Priority => ImprovedThumbnailSettings.ParticleThumbnailProviderPriority;

    protected override ThumbnailSupportInfo EvaluateSupport(Object asset, string guid, string assetPath)
    {
        if (!ImprovedThumbnailSettings.EnableParticleThumbnailProvider)
            return ThumbnailSupportInfo.Unsupported;

        GameObject prefab = asset as GameObject;
        if (prefab == null)
            return ThumbnailSupportInfo.Unsupported;

        bool supported = ThumbnailPrefabUtility.HasRootParticleSystem(prefab);
        return supported
            ? new ThumbnailSupportInfo(true, ThumbnailAssetKind.ParticlePrefab, Priority)
            : ThumbnailSupportInfo.Unsupported;
    }

    protected override ThumbnailFrameSet RenderFrames(ThumbnailProviderContext context, ThumbnailRenderContext renderContext)
    {
        int thumbSize = ImprovedThumbnailSettings.GetThumbnailSize(context.Surface);
        renderContext.EnsureInfrastructure(
            thumbSize,
            ImprovedThumbnailSettings.RenderBackgroundColor,
            ImprovedThumbnailSettings.ThumbnailCameraFov,
            ImprovedThumbnailSettings.GeneralThumbnailLightIntensity,
            0f);

        return RenderStaticFrame(context, renderContext, thumbSize);
    }

    private ThumbnailFrameSet RenderStaticFrame(ThumbnailProviderContext context, ThumbnailRenderContext renderContext, int thumbSize)
    {
        GameObject instance = null;
        try
        {
            instance = Object.Instantiate(context.Prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            renderContext.Preview.AddSingleGO(instance);
            renderContext.SetInstance(instance);

            ThumbnailPrefabUtility.ForceActivateHierarchy(instance);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            ThumbnailPrefabUtility.SetRendererEnabled(renderers, false);

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            if (systems.Length == 0)
                return null;

            bool needsMotion = NeedsMotion(systems);
            float scanMax = Mathf.Min(GetScanMax(systems), ImprovedThumbnailSettings.ParticleScanMaxSeconds);
            float bestTime = FindPeakTime(systems, instance, needsMotion, scanMax);
            ResetSimulation(systems, instance, needsMotion);
            float elapsed = 0f;
            bool firstStep = true;
            AdvanceSimulationTo(systems, instance, needsMotion, bestTime, ref elapsed, ref firstStep);

            // Some effects (for example burst-at-boundary shields) can miss the coarse scan window.
            // Keep original behavior first, then fallback only when the selected frame is empty.
            if (CountLiveParticles(systems) <= 0)
            {
                float fallbackScanMax = Mathf.Min(GetScanMaxFallback(systems), ImprovedThumbnailSettings.ParticleScanMaxSeconds);
                float fallbackTime = FindFirstVisibleTime(systems, instance, needsMotion, fallbackScanMax);
                if (fallbackTime >= 0f)
                {
                    bestTime = fallbackTime;
                    ResetSimulation(systems, instance, needsMotion);
                    elapsed = 0f;
                    firstStep = true;
                    AdvanceSimulationTo(systems, instance, needsMotion, bestTime, ref elapsed, ref firstStep);
                }
            }

            Bounds bounds = ComputeParticleBounds(systems);
            FrameCamera(renderContext.Preview.camera, renderContext.Preview.lights[0], bounds, ImprovedThumbnailSettings.ThumbnailBoundsPadding, needsMotion);

            Texture2D staticFrame = RenderCurrentFrame(renderContext, renderers, thumbSize, bestTime);
            return new ThumbnailFrameSet { StaticFrame = staticFrame };
        }
        finally
        {
            if (instance != null)
                Object.DestroyImmediate(instance);
        }
    }

    private float FindPeakTime(ParticleSystem[] systems, GameObject instance, bool needsMotion, float scanMax)
    {
        ResetSimulation(systems, instance, needsMotion);
        float bestTime = SampleStep;
        int bestCount = -1;
        float elapsed = 0f;
        bool firstStep = true;

        while (elapsed < scanMax - 0.0001f)
        {
            float targetTime = Mathf.Min(scanMax, elapsed + SampleStep);
            AdvanceSimulationTo(systems, instance, needsMotion, targetTime, ref elapsed, ref firstStep);

            int count = CountLiveParticles(systems);
            if (count > bestCount)
            {
                bestCount = count;
                bestTime = elapsed;
            }
        }

        return bestTime;
    }

    private Texture2D RenderCurrentFrame(ThumbnailRenderContext renderContext, Renderer[] renderers, int thumbSize, float frameTime)
    {
        Vector4 prevTime = Shader.GetGlobalVector("_Time");
        Vector4 prevSinTime = Shader.GetGlobalVector("_SinTime");
        Vector4 prevCosTime = Shader.GetGlobalVector("_CosTime");
        Vector4 prevDeltaTime = Shader.GetGlobalVector("unity_DeltaTime");

        try
        {
            Shader.SetGlobalVector("_Time", new Vector4(frameTime / 20f, frameTime, frameTime * 2f, frameTime * 3f));
            Shader.SetGlobalVector("_SinTime", new Vector4(Mathf.Sin(frameTime / 8f), Mathf.Sin(frameTime / 4f), Mathf.Sin(frameTime / 2f), Mathf.Sin(frameTime)));
            Shader.SetGlobalVector("_CosTime", new Vector4(Mathf.Cos(frameTime / 8f), Mathf.Cos(frameTime / 4f), Mathf.Cos(frameTime / 2f), Mathf.Cos(frameTime)));
            Shader.SetGlobalVector("unity_DeltaTime", new Vector4(0.016f, 1f / 0.016f, 0f, 0f));

            renderContext.Preview.camera.backgroundColor = ImprovedThumbnailSettings.RenderBackgroundColor;
            renderContext.Preview.BeginPreview(new Rect(0f, 0f, thumbSize, thumbSize), GUIStyle.none);
            ThumbnailPrefabUtility.SetRendererEnabled(renderers, true);
            try
            {
                renderContext.Preview.Render(true);
            }
            finally
            {
                ThumbnailPrefabUtility.SetRendererEnabled(renderers, false);
            }

            return renderContext.CapturePreview(thumbSize);
        }
        finally
        {
            Shader.SetGlobalVector("_Time", prevTime);
            Shader.SetGlobalVector("_SinTime", prevSinTime);
            Shader.SetGlobalVector("_CosTime", prevCosTime);
            Shader.SetGlobalVector("unity_DeltaTime", prevDeltaTime);
        }
    }

    private float GetScanMax(ParticleSystem[] systems)
    {
        float max = 0.1f;
        for (int i = 0; i < systems.Length; i++)
            max = Mathf.Max(max, systems[i].main.duration + GetMinLifetime(systems[i].main.startLifetime));
        return max;
    }

    private float GetScanMaxFallback(ParticleSystem[] systems)
    {
        float max = 0.1f;
        for (int i = 0; i < systems.Length; i++)
            max = Mathf.Max(max, systems[i].main.duration + GetMaxLifetime(systems[i].main.startLifetime));
        return max;
    }

    private static float GetMinLifetime(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Min(curve.constantMin, curve.constantMax);
            default:
                return curve.curveMultiplier;
        }
    }

    private static float GetMaxLifetime(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return Mathf.Max(curve.constantMin, curve.constantMax);
            default:
                return curve.curveMultiplier;
        }
    }

    private static void ClearAll(ParticleSystem[] systems)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(withChildren: false, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            systems[i].Clear(withChildren: false);
        }
    }

    private static int CountLiveParticles(ParticleSystem[] systems)
    {
        int total = 0;
        for (int i = 0; i < systems.Length; i++)
            total += systems[i].particleCount;
        return total;
    }

    private static void ResetSimulation(ParticleSystem[] systems, GameObject instance, bool needsMotion)
    {
        ClearAll(systems);
        if (!needsMotion || instance == null)
            return;

        ApplyMotionPose(instance, 0f);
    }

    private float FindFirstVisibleTime(ParticleSystem[] systems, GameObject instance, bool needsMotion, float scanMax)
    {
        ResetSimulation(systems, instance, needsMotion);
        float elapsed = 0f;
        bool firstStep = true;

        while (elapsed < scanMax - 0.0001f)
        {
            float targetTime = Mathf.Min(scanMax, elapsed + FallbackSampleStep);
            AdvanceSimulationTo(systems, instance, needsMotion, targetTime, ref elapsed, ref firstStep);
            if (CountLiveParticles(systems) > 0)
                return elapsed;
        }

        return -1f;
    }

    private static bool NeedsMotion(ParticleSystem[] systems)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            if (ps == null || ps.emission.rateOverDistanceMultiplier <= 0f)
                continue;

            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
                return true;
        }

        return false;
    }

    private static void AdvanceSimulationTo(ParticleSystem[] systems, GameObject instance, bool needsMotion, float targetTime, ref float elapsed, ref bool firstStep)
    {
        const float step = 1f / 60f;

        while (elapsed < targetTime - 0.0001f)
        {
            float dt = Mathf.Min(step, targetTime - elapsed);
            float nextElapsed = elapsed + dt;

            if (needsMotion && instance != null)
                ApplyMotionPose(instance, nextElapsed);

            for (int i = 0; i < systems.Length; i++)
                systems[i].Simulate(dt, withChildren: false, restart: firstStep, fixedTimeStep: false);

            elapsed = nextElapsed;
            firstStep = false;
        }
    }

    private static void ApplyMotionPose(GameObject instance, float time)
    {
        instance.transform.position = CirclePosition(time);
        Vector3 dir = CirclePosition(time + 0.001f) - instance.transform.position;
        if (dir.sqrMagnitude > 0.000001f)
            instance.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private static Vector3 CirclePosition(float t)
    {
        float angle = t * (MotionSpeed / MotionRadius);
        return new Vector3(Mathf.Cos(angle) * MotionRadius, 0f, Mathf.Sin(angle) * MotionRadius);
    }

    private Bounds ComputeParticleBounds(ParticleSystem[] systems)
    {
        bool found = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem ps = systems[i];
            int count = ps.GetParticles(_particleBuffer);
            for (int j = 0; j < count; j++)
            {
                Vector3 position = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                    ? _particleBuffer[j].position
                    : ps.transform.TransformPoint(_particleBuffer[j].position);

                Vector3 size3 = _particleBuffer[j].GetCurrentSize3D(ps);
                float size = Mathf.Max(size3.x, size3.y, size3.z, 0.01f);
                Bounds particleBounds = new Bounds(position, Vector3.one * size);

                if (!found)
                {
                    bounds = particleBounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(particleBounds);
                }
            }
        }

        return found && bounds.size.sqrMagnitude > 0.001f
            ? bounds
            : new Bounds(Vector3.zero, Vector3.one * 2f);
    }

    private static void FrameCamera(Camera camera, Light mainLight, Bounds bounds, float boundsPadding, bool needsMotion)
    {
        Quaternion cameraRotation = Quaternion.Euler(ImprovedThumbnailSettings.ThumbnailCameraPitch, ImprovedThumbnailSettings.ThumbnailCameraYaw, 0f);
        Vector3 forward = cameraRotation * Vector3.forward;
        Vector3 right = cameraRotation * Vector3.right;
        Vector3 up = cameraRotation * Vector3.up;

        float distance;
        Vector3 center;

        if (needsMotion)
        {
            center = Vector3.zero;
            float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float padding = ImprovedThumbnailSettings.ParticleThumbnailMotionPadding;
            distance = Mathf.Max((MotionRadius * (1.5f + padding)) / Mathf.Tan(halfFov), 0.3f);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - MotionRadius * 3f);
            camera.farClipPlane = distance + MotionRadius * 3f + 1f;
        }
        else
        {
            center = bounds.center;
            float particleBoundsPadding = Mathf.Clamp(boundsPadding, 0f, 0.08f);
            Vector3 extents = bounds.extents * (1f + particleBoundsPadding);
            float halfFov = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(halfFov);
            float maxRight = 0f;
            float maxUp = 0f;
            float maxDepth = 0f;

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = new Vector3(extents.x * sx, extents.y * sy, extents.z * sz);
                maxRight = Mathf.Max(maxRight, Mathf.Abs(Vector3.Dot(corner, right)));
                maxUp = Mathf.Max(maxUp, Mathf.Abs(Vector3.Dot(corner, up)));
                maxDepth = Mathf.Max(maxDepth, Mathf.Abs(Vector3.Dot(corner, forward)));
            }

            distance = Mathf.Max(maxRight / tanHalf, maxUp / tanHalf, 0.3f);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - maxDepth - 0.5f);
            camera.farClipPlane = distance + maxDepth + 1f;
        }

        camera.transform.position = center - forward * distance;
        camera.transform.rotation = cameraRotation;
        mainLight.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
    }

}

}
