#if UNITY_EDITOR
using System;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	internal struct PreviewInteractionState
	{
		public Vector2 Orbit;
		public Vector2 TargetOrbit;
		public Vector2 AngularVelocity;
		public float Distance;
		public float TargetDistance;
		public bool IsOrbitDragging;
		public double LastOrbitInputTime;

		public static PreviewInteractionState Create(Vector2 initialOrbit, float initialDistance)
		{
			return new PreviewInteractionState
			{
				Orbit = initialOrbit,
				TargetOrbit = initialOrbit,
				AngularVelocity = Vector2.zero,
				Distance = initialDistance,
				TargetDistance = initialDistance,
				IsOrbitDragging = false,
				LastOrbitInputTime = -1d
			};
		}
	}

	internal readonly struct PreviewInteractionInput
	{
		public readonly Vector2 MouseDelta;
		public readonly float ScrollDeltaY;
		public readonly double Timestamp;

		public PreviewInteractionInput(Vector2 mouseDelta, float scrollDeltaY, double timestamp)
		{
			MouseDelta = mouseDelta;
			ScrollDeltaY = scrollDeltaY;
			Timestamp = timestamp;
		}
	}

	internal readonly struct PreviewInteractionPolicy
	{
		public readonly Func<bool> CanAdjustPitch;
		public readonly Action OnManualInteraction;
		public readonly float PitchMin;
		public readonly float PitchMax;
		public readonly float MinDistance;
		public readonly float MaxDistance;
		public readonly float ZoomScrollFactor;
		public readonly float OrbitEpsilon;
		public readonly float DistanceEpsilon;
		public readonly float AngularVelocityEpsilon;

		public PreviewInteractionPolicy(
			Func<bool> canAdjustPitch,
			Action onManualInteraction,
			float pitchMin,
			float pitchMax,
			float minDistance,
			float maxDistance,
			float zoomScrollFactor,
			float orbitEpsilon = 0.01f,
			float distanceEpsilon = 0.001f,
			float angularVelocityEpsilon = 0.01f)
		{
			CanAdjustPitch = canAdjustPitch;
			OnManualInteraction = onManualInteraction;
			PitchMin = pitchMin;
			PitchMax = pitchMax;
			MinDistance = minDistance;
			MaxDistance = maxDistance;
			ZoomScrollFactor = zoomScrollFactor;
			OrbitEpsilon = orbitEpsilon;
			DistanceEpsilon = distanceEpsilon;
			AngularVelocityEpsilon = angularVelocityEpsilon;
		}
	}

	internal static class PreviewInteractionController
	{
		private const float OrbitVelocitySmoothing = 0.35f;
		private const float FallbackOrbitInputDeltaTime = 1f / 60f;

		public static void BeginOrbitDrag(ref PreviewInteractionState state, double timestamp)
		{
			if (!state.IsOrbitDragging)
			{
				state.IsOrbitDragging = true;
				state.LastOrbitInputTime = timestamp;
			}
		}

		public static void EndOrbitDrag(ref PreviewInteractionState state, double timestamp)
		{
			state.IsOrbitDragging = false;
			state.LastOrbitInputTime = timestamp;
		}

		public static bool ApplyOrbitDrag(
			ref PreviewInteractionState state,
			in PreviewInteractionInput input,
			in PreviewInteractionPolicy policy)
		{
			BeginOrbitDrag(ref state, input.Timestamp);

			bool canAdjustPitch = policy.CanAdjustPitch == null || policy.CanAdjustPitch();
			float dt = FallbackOrbitInputDeltaTime;
			if (state.LastOrbitInputTime >= 0d)
				dt = Mathf.Clamp((float)(input.Timestamp - state.LastOrbitInputTime), 1f / 240f, 0.05f);
			state.LastOrbitInputTime = input.Timestamp;

			state.TargetOrbit.x += input.MouseDelta.x;
			if (canAdjustPitch)
				state.TargetOrbit.y = Mathf.Clamp(state.TargetOrbit.y + input.MouseDelta.y, policy.PitchMin, policy.PitchMax);

			Vector2 rawVelocity = new Vector2(
				input.MouseDelta.x / Mathf.Max(0.0001f, dt),
				canAdjustPitch ? input.MouseDelta.y / Mathf.Max(0.0001f, dt) : 0f);
			state.AngularVelocity = Vector2.Lerp(state.AngularVelocity, rawVelocity, OrbitVelocitySmoothing);

			policy.OnManualInteraction?.Invoke();
			return true;
		}

		public static bool ApplyScrollZoom(
			ref PreviewInteractionState state,
			in PreviewInteractionInput input,
			in PreviewInteractionPolicy policy)
		{
			float nextTargetDistance = state.TargetDistance * (1f + input.ScrollDeltaY * policy.ZoomScrollFactor);
			state.TargetDistance = Mathf.Clamp(nextTargetDistance, policy.MinDistance, policy.MaxDistance);
			policy.OnManualInteraction?.Invoke();
			return true;
		}

		public static bool Tick(
			ref PreviewInteractionState state,
			float dt,
			float orbitSmooth,
			float zoomSmooth,
			in PreviewInteractionPolicy policy)
		{
			if (dt <= 0f)
				return HasPendingMotion(state, policy);

			bool canAdjustPitch = policy.CanAdjustPitch == null || policy.CanAdjustPitch();
			float safeOrbitSmooth = Mathf.Max(0.01f, orbitSmooth);
			float safeZoomSmooth = Mathf.Max(0.01f, zoomSmooth);
			float angularVelocityEpsilonSq = policy.AngularVelocityEpsilon * policy.AngularVelocityEpsilon;

			if (!state.IsOrbitDragging && state.AngularVelocity.sqrMagnitude > angularVelocityEpsilonSq)
			{
				state.TargetOrbit.x += state.AngularVelocity.x * dt;
				if (canAdjustPitch)
					state.TargetOrbit.y = Mathf.Clamp(state.TargetOrbit.y + state.AngularVelocity.y * dt, policy.PitchMin, policy.PitchMax);

				float velocityDecay = Mathf.Exp(-safeOrbitSmooth * dt);
				state.AngularVelocity *= velocityDecay;
				if (state.AngularVelocity.sqrMagnitude <= angularVelocityEpsilonSq)
					state.AngularVelocity = Vector2.zero;
			}

			if (canAdjustPitch)
				state.TargetOrbit.y = Mathf.Clamp(state.TargetOrbit.y, policy.PitchMin, policy.PitchMax);

			if (Vector2.Distance(state.Orbit, state.TargetOrbit) > policy.OrbitEpsilon)
			{
				float orbitBlend = 1f - Mathf.Exp(-safeOrbitSmooth * dt);
				state.Orbit = Vector2.Lerp(state.Orbit, state.TargetOrbit, orbitBlend);
			}
			else
			{
				state.Orbit = state.TargetOrbit;
			}

			if (Mathf.Abs(state.Distance - state.TargetDistance) > policy.DistanceEpsilon)
			{
				float zoomBlend = 1f - Mathf.Exp(-safeZoomSmooth * dt);
				state.Distance = Mathf.Lerp(state.Distance, state.TargetDistance, zoomBlend);
			}
			else
			{
				state.Distance = state.TargetDistance;
			}

			return HasPendingMotion(state, policy);
		}

		public static bool HasPendingMotion(in PreviewInteractionState state, in PreviewInteractionPolicy policy)
		{
			float angularVelocityEpsilonSq = policy.AngularVelocityEpsilon * policy.AngularVelocityEpsilon;
			return Vector2.Distance(state.Orbit, state.TargetOrbit) > policy.OrbitEpsilon
				|| Mathf.Abs(state.Distance - state.TargetDistance) > policy.DistanceEpsilon
				|| (!state.IsOrbitDragging && state.AngularVelocity.sqrMagnitude > angularVelocityEpsilonSq);
		}
	}
}
#endif
