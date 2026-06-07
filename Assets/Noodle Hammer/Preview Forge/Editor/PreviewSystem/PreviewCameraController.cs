using UnityEngine;
// Provides reusable camera orbit, pan, and zoom math used by preview sessions for smooth and predictable interaction.

namespace NoodleHammer.PreviewForge.Editor
{
    internal struct PreviewCameraInteractionState
    {
        internal Vector2 Orbit;
        internal Vector2 TargetOrbit;
        internal Vector2 OrbitAngularVelocity;
        internal float Distance;
        internal float TargetDistance;
        internal Vector3 Pivot;
        internal Vector3 TargetPivot;
        internal bool IsOrbitDragging;
        internal double LastOrbitInputTime;
    }

    internal readonly struct PreviewCameraInteractionConfig
    {
        internal readonly float PitchMin;
        internal readonly float PitchMax;
        internal readonly float MaxDeltaTime;
        internal readonly float OrbitEpsilon;
        internal readonly float DistanceEpsilon;
        internal readonly float PivotEpsilon;
        internal readonly float AngularVelocityEpsilon;
        internal readonly float OrbitHoldStillResetSeconds;
        internal readonly float ZoomSmooth;

        internal PreviewCameraInteractionConfig(
            float pitchMin,
            float pitchMax,
            float maxDeltaTime,
            float orbitEpsilon,
            float distanceEpsilon,
            float pivotEpsilon,
            float angularVelocityEpsilon,
            float orbitHoldStillResetSeconds,
            float zoomSmooth)
        {
            PitchMin = pitchMin;
            PitchMax = pitchMax;
            MaxDeltaTime = maxDeltaTime;
            OrbitEpsilon = orbitEpsilon;
            DistanceEpsilon = distanceEpsilon;
            PivotEpsilon = pivotEpsilon;
            AngularVelocityEpsilon = angularVelocityEpsilon;
            OrbitHoldStillResetSeconds = orbitHoldStillResetSeconds;
            ZoomSmooth = zoomSmooth;
        }
    }

    internal static class PreviewCameraController
    {
        internal static bool Tick(
            ref PreviewCameraInteractionState state,
            ref double lastInteractionUpdateTime,
            double now,
            float orbitSmoothing,
            float panSmoothing,
            bool effective2D,
            in PreviewCameraInteractionConfig config)
        {
            if (lastInteractionUpdateTime < 0d)
            {
                lastInteractionUpdateTime = now;
                return HasPendingMotion(state, config);
            }

            float dt = Mathf.Clamp((float)(now - lastInteractionUpdateTime), 0f, config.MaxDeltaTime);
            lastInteractionUpdateTime = now;
            if (dt <= 0f)
                return HasPendingMotion(state, config);

            if (state.IsOrbitDragging && state.LastOrbitInputTime >= 0d && now - state.LastOrbitInputTime > config.OrbitHoldStillResetSeconds)
                state.OrbitAngularVelocity = Vector2.zero;

            float angularVelocityEpsilonSq = config.AngularVelocityEpsilon * config.AngularVelocityEpsilon;
            if (!state.IsOrbitDragging && state.OrbitAngularVelocity.sqrMagnitude > angularVelocityEpsilonSq)
            {
                state.TargetOrbit.x += state.OrbitAngularVelocity.x * dt;
                if (!effective2D)
                    state.TargetOrbit.y = Mathf.Clamp(state.TargetOrbit.y + state.OrbitAngularVelocity.y * dt, config.PitchMin, config.PitchMax);

                float velocityDecay = Mathf.Exp(-orbitSmoothing * dt);
                state.OrbitAngularVelocity *= velocityDecay;
                if (state.OrbitAngularVelocity.sqrMagnitude <= angularVelocityEpsilonSq)
                    state.OrbitAngularVelocity = Vector2.zero;
            }

            state.TargetOrbit.y = effective2D ? 0f : Mathf.Clamp(state.TargetOrbit.y, config.PitchMin, config.PitchMax);

            if (Vector2.Distance(state.Orbit, state.TargetOrbit) > config.OrbitEpsilon)
            {
                float orbitBlend = 1f - Mathf.Exp(-orbitSmoothing * dt);
                state.Orbit = Vector2.Lerp(state.Orbit, state.TargetOrbit, orbitBlend);
            }
            else
            {
                state.Orbit = state.TargetOrbit;
            }

            if (Mathf.Abs(state.Distance - state.TargetDistance) > config.DistanceEpsilon)
            {
                float zoomBlend = 1f - Mathf.Exp(-config.ZoomSmooth * dt);
                state.Distance = Mathf.Lerp(state.Distance, state.TargetDistance, zoomBlend);
            }
            else
            {
                state.Distance = state.TargetDistance;
            }

            float pivotDeltaSqr = (state.Pivot - state.TargetPivot).sqrMagnitude;
            if (pivotDeltaSqr > config.PivotEpsilon * config.PivotEpsilon)
            {
                float panBlend = 1f - Mathf.Exp(-panSmoothing * dt);
                state.Pivot = Vector3.Lerp(state.Pivot, state.TargetPivot, panBlend);
            }
            else
            {
                state.Pivot = state.TargetPivot;
            }

            return HasPendingMotion(state, config);
        }

        internal static bool HasPendingMotion(in PreviewCameraInteractionState state, in PreviewCameraInteractionConfig config)
        {
            float angularVelocityEpsilonSq = config.AngularVelocityEpsilon * config.AngularVelocityEpsilon;
            return Vector2.Distance(state.Orbit, state.TargetOrbit) > config.OrbitEpsilon
                   || Mathf.Abs(state.Distance - state.TargetDistance) > config.DistanceEpsilon
                   || (state.Pivot - state.TargetPivot).sqrMagnitude > config.PivotEpsilon * config.PivotEpsilon
                   || (!state.IsOrbitDragging && state.OrbitAngularVelocity.sqrMagnitude > angularVelocityEpsilonSq);
        }
    }
}
