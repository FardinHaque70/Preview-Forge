#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{

/// <summary>
/// Preview implementation for particle system prefabs.
/// Detects prefabs whose root GameObject has a ParticleSystem component.
/// Extends CustomPreviewBase — adds playback toolbar, motion controls,
/// per-frame simulation, and enable/disable renderers around camera.Render().
/// </summary>
/// 
public class ParticlePrefabPreview : CustomPreviewBase
{
	public override PreviewAssetTypeKey PreviewTypeKey => PreviewAssetTypeKey.ParticlePrefab;

	// =====================================================================
	// Detection
	// =====================================================================

	public override bool Supports(GameObject prefab)
	{
		if (prefab == null) return false;
		if (!EditorUtility.IsPersistent(prefab)) return false;
		if (!PrefabUtility.IsPartOfPrefabAsset(prefab)) return false;

		return prefab.GetComponent<ParticleSystem>() != null;
	}

	// =====================================================================
	// Private state
	// =====================================================================

	private readonly List<ParticleSystem> _allSystems = new();
	private readonly List<ParticleSystemRenderer> _renderers = new();
	private readonly List<ParticleSystemSimulationSpace> _origSimSpace = new();
	private readonly List<Transform> _origCustomSpace = new();
	private readonly HashSet<ParticleSystem> _subEmitterSystems = new();

	private enum MotionShape
	{
		Circle,
		Line,
		Figure8
	}

	private MotionShape _motionShape = MotionShape.Circle;
	private float _motionSpeed = 30f;
	private float _motionSize = 3f;
	private bool _needsMotion;
	private const float IntroRestartDeferSeconds = 0.18f;
	private bool _deferRestartUntilCameraSettles;
	private float _postIntroRestartDeferRemaining;
	private Quaternion _prefabRotation = Quaternion.identity;
	private float[] _visibilityProfile = Array.Empty<float>();
	private float _visibilityProfileDuration = -1f;
	private bool _visibilityProfileDirty = true;

	// =====================================================================
	// Setup
	// =====================================================================

	protected override void OnSetup(GameObject prefab)
	{
		_prefabRotation = prefab.transform.rotation;
		_motionShape = GetConfiguredDefaultMotionShape();
		_motionSpeed = ImprovedPreviewSettings.ParticlePreviewDefaultMotionSpeed;
		_motionSize = ImprovedPreviewSettings.ParticlePreviewDefaultMotionSize;

		_allSystems.Clear();
		_subEmitterSystems.Clear();
		_renderers.Clear();
		_origSimSpace.Clear();
		_origCustomSpace.Clear();

		PreviewRoot.GetComponentsInChildren(true, _allSystems);
		PreviewRoot.GetComponentsInChildren(true, _renderers);
		BuildSubEmitterSystemSet();

		foreach (var r in _renderers)
		{
			if (r != null)
				r.enabled = false;
		}

		float maxDuration = 0.1f;
		float restartDuration = 0.1f;

		foreach (var ps in _allSystems)
		{
			if (ps == null) continue;

			var main = ps.main;
			float systemEnd = main.duration + GetMaxStartLifetime(main.startLifetime);

			maxDuration = Mathf.Max(maxDuration, main.duration);
			restartDuration = Mathf.Max(restartDuration, systemEnd);
		}

		// One consistent time range for playback / scrub / restart
		MaxTime = Mathf.Max(maxDuration, restartDuration);

		_needsMotion = NeedsPreviewMotion();

		for (int i = 0; i < _allSystems.Count; i++)
		{
			var ps = _allSystems[i];
			if (ps == null)
			{
				_origSimSpace.Add(ParticleSystemSimulationSpace.Local);
				_origCustomSpace.Add(null);
				continue;
			}

			var main = ps.main;
			_origSimSpace.Add(main.simulationSpace);
			_origCustomSpace.Add(main.customSimulationSpace);

			if (main.simulationSpace == ParticleSystemSimulationSpace.Custom)
			{
				Transform cs = main.customSimulationSpace;
				bool valid = cs != null &&
				             (cs == PreviewRoot.transform || cs.IsChildOf(PreviewRoot.transform));

				if (!valid)
				{
					main.simulationSpace = ParticleSystemSimulationSpace.Local;
					main.customSimulationSpace = null;
				}
			}
		}

		// Deterministic scrubbing
		uint seed = 42u;
		foreach (var ps in _allSystems)
		{
			if (ps == null) continue;
			ps.useAutoRandomSeed = false;
			ps.randomSeed = seed++;
		}

		float fittedDist = Mathf.Max(
			maxDuration * ImprovedPreviewSettings.ParticlePreviewDurationDistanceMultiplier,
			ImprovedPreviewSettings.ParticlePreviewMinimumFittedDistance);
		if (_needsMotion)
		{
			float halfFov = PreviewCam.fieldOfView * 0.5f * Mathf.Deg2Rad;
			float motionFitDist = Mathf.Max(
				(_motionSize * ImprovedPreviewSettings.ParticlePreviewMotionFitMultiplier) / Mathf.Max(Mathf.Tan(halfFov), 0.001f),
				ImprovedPreviewSettings.ParticlePreviewMinimumFittedDistance);
			fittedDist = Mathf.Max(fittedDist, motionFitDist);
		}

		// Keep the particle preview centered on the prefab origin, like scene/prefab mode,
		// instead of reframing around sampled particle bounds.
		SetPivot(WorldOffset);
		float finalFittedDistance = Mathf.Max(
			fittedDist * ImprovedPreviewSettings.ParticlePreviewFinalDistancePaddingMultiplier,
			ImprovedPreviewSettings.ParticlePreviewMinimumFittedDistance);
		SetCameraDistanceWithIntroZoom(Mathf.Max(finalFittedDistance, ImprovedPreviewSettings.DefaultDist));
		_deferRestartUntilCameraSettles = true;
		_postIntroRestartDeferRemaining = 0f;
		MarkVisibilityProfileDirty();
		RebuildVisibilityProfile();
		SeekToTime(0.05f);
		if (ImprovedPreviewSettings.ParticlePreviewDefaultAutoplay)
			StartPlayback();
		else
			ForcePausedSimulationRefresh();
	}

	protected override void OnCleanup()
	{
		for (int i = 0; i < _allSystems.Count && i < _origSimSpace.Count; i++)
		{
			var ps = _allSystems[i];
			if (ps == null) continue;

			var main = ps.main;
			main.simulationSpace = _origSimSpace[i];
			main.customSimulationSpace = i < _origCustomSpace.Count ? _origCustomSpace[i] : null;
		}

		_allSystems.Clear();
		_subEmitterSystems.Clear();
		_renderers.Clear();
		_origSimSpace.Clear();
		_origCustomSpace.Clear();
		_visibilityProfile = Array.Empty<float>();
		_visibilityProfileDuration = -1f;
		_visibilityProfileDirty = true;
		_deferRestartUntilCameraSettles = false;
		_postIntroRestartDeferRemaining = 0f;
	}

	// =====================================================================
	// Simulate
	// =====================================================================

	protected override void OnSimulate(float dt)
	{
		if (_allSystems.Count == 0)
			return;

		UpdateAutoRestartGate(dt);

		if (ShouldRestartNonLoopingEffect())
		{
			SeekToTime(0.05f);
			StartPlayback();
			return;
		}

		SimulateHierarchy();
	}

	private bool ShouldRestartNonLoopingEffect()
	{
		bool anyLoop = false;

		foreach (var ps in _allSystems)
		{
			if (ps == null) continue;
			if (ps.main.loop)
			{
				anyLoop = true;
				break;
			}
		}

		if (anyLoop)
			return false;

		if (IsAutoRestartDeferred())
			return false;

		return CurrentTime >= MaxTime;
	}

	private void UpdateAutoRestartGate(float dt)
	{
		if (_deferRestartUntilCameraSettles)
		{
			if (IsCameraMotionSettling())
				return;

			_deferRestartUntilCameraSettles = false;
			if (CurrentTime >= MaxTime)
				_postIntroRestartDeferRemaining = IntroRestartDeferSeconds;
		}

		if (_postIntroRestartDeferRemaining > 0f)
			_postIntroRestartDeferRemaining = Mathf.Max(0f, _postIntroRestartDeferRemaining - Mathf.Max(0f, dt));
	}

	private bool IsAutoRestartDeferred()
	{
		if (_deferRestartUntilCameraSettles)
			return true;

		return _postIntroRestartDeferRemaining > 0f;
	}

	// =====================================================================
	// Simulation helpers
	// =====================================================================

	private void SimulateHierarchy()
	{
		if (_allSystems.Count == 0)
			return;

		if (NeedsSeek)
		{
			RebuildSimulationToTime(CurrentTime);
			SimulatedTime = CurrentTime;
			NeedsSeek = false;
		}
		else if (IsPlaying)
		{
			float dt = Mathf.Max(0f, CurrentTime - SimulatedTime);
			if (dt > 0f)
			{
				if (_needsMotion)
					ApplyMotionTransform(CurrentTime);

				foreach (var ps in _allSystems)
				{
					if (ps != null)
						ps.Simulate(dt, withChildren: false, restart: false, fixedTimeStep: true);
				}

				SimulatedTime = CurrentTime;
			}
		}
	}

	private void RebuildSimulationToTime(float targetTime)
	{
		StopAndClearAll();
		ResetRootTransform();

		if (targetTime <= 0f)
		{
			if (_needsMotion)
				ApplyMotionTransform(0f);

			// Prime systems at frame zero so restart/play has a valid emission state,
			// especially for burst-driven effects.
			foreach (var ps in _allSystems)
			{
				if (ps != null)
					ps.Simulate(0f, withChildren: false, restart: true, fixedTimeStep: true);
			}
			return;
		}

		if (!_needsMotion)
		{
			foreach (var ps in _allSystems)
			{
				if (ps != null)
					ps.Simulate(targetTime, withChildren: false, restart: true, fixedTimeStep: false);
			}
			return;
		}

		// For motion-driven effects, step through time while moving the root so
		// rate-over-distance emission has a valid velocity path.
		const float seekStep = 1f / 30f;

		float stepped = 0f;
		bool first = true;

		while (stepped < targetTime - 0.0001f)
		{
			float dt = Mathf.Min(seekStep, targetTime - stepped);
			stepped += dt;

			ApplyMotionTransform(stepped);

			foreach (var ps in _allSystems)
			{
				if (ps != null)
					ps.Simulate(dt, withChildren: false, restart: first, fixedTimeStep: false);
			}

			first = false;
		}
	}

	private void StopAndClearAll()
	{
		foreach (var ps in _allSystems)
		{
			if (ps == null) continue;
			ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ps.Clear(true);
		}
	}

	private float GetMaxStartLifetime(ParticleSystem.MinMaxCurve curve)
	{
		switch (curve.mode)
		{
			case ParticleSystemCurveMode.Constant:
				return curve.constant;

			case ParticleSystemCurveMode.TwoConstants:
				return Mathf.Max(curve.constantMin, curve.constantMax);

			case ParticleSystemCurveMode.Curve:
			case ParticleSystemCurveMode.TwoCurves:
				return curve.curveMultiplier;

			default:
				return 0f;
		}
	}

	private bool NeedsPreviewMotion()
	{
		foreach (var ps in _allSystems)
		{
			if (ps == null)
				continue;

			if (_subEmitterSystems.Contains(ps))
				continue;

			var main = ps.main;
			if (main.simulationSpace != ParticleSystemSimulationSpace.World)
				continue;

			if (ps.emission.rateOverDistanceMultiplier <= 0.0001f)
				continue;

			return true;
		}

		return false;
	}

	private void BuildSubEmitterSystemSet()
	{
		_subEmitterSystems.Clear();

		foreach (var ps in _allSystems)
		{
			if (ps == null)
				continue;

			var subEmitters = ps.subEmitters;
			int count = subEmitters.subEmittersCount;
			for (int i = 0; i < count; i++)
			{
				ParticleSystem sub = subEmitters.GetSubEmitterSystem(i);
				if (sub != null)
					_subEmitterSystems.Add(sub);
			}
		}
	}

	private static MotionShape GetConfiguredDefaultMotionShape()
	{
		return ImprovedPreviewSettings.ParticlePreviewDefaultMotionShape switch
		{
			ParticlePreviewDefaultMotionShape.Line => MotionShape.Line,
			ParticlePreviewDefaultMotionShape.Figure8 => MotionShape.Figure8,
			_ => MotionShape.Circle
		};
	}


	// =====================================================================
	// Motion
	// =====================================================================

	private void ApplyMotionTransform(float t)
	{
		const float lookAhead = 0.001f;

		Vector3 pos = WorldOffset + MotionPosition(t);
		Vector3 posNext = WorldOffset + MotionPosition(t + lookAhead);
		Vector3 dir = posNext - pos;

		PreviewRoot.transform.position = pos;
		PreviewRoot.transform.rotation = dir.sqrMagnitude > 0.000001f
			? Quaternion.LookRotation(dir.normalized, Vector3.up) * _prefabRotation
			: _prefabRotation;
	}

	private void ResetRootTransform()
	{
		PreviewRoot.transform.position = WorldOffset;
		PreviewRoot.transform.rotation = _prefabRotation;
	}

	private Vector3 MotionPosition(float t)
	{
		switch (_motionShape)
		{
			case MotionShape.Circle:
			{
				float angle = t * (_motionSize > 0f ? _motionSpeed / _motionSize : 1f);
				return new Vector3(
					Mathf.Cos(angle) * _motionSize,
					0f,
					Mathf.Sin(angle) * _motionSize);
			}

			case MotionShape.Line:
			{
				float period = (_motionSize * 2f) / Mathf.Max(0.001f, _motionSpeed);
				return new Vector3(
					Mathf.Lerp(-_motionSize, _motionSize, Mathf.PingPong(t / period, 1f)),
					0f,
					0f);
			}

			case MotionShape.Figure8:
			{
				float angle = t * (_motionSize > 0f ? _motionSpeed / _motionSize : 1f);
				float denom = 1f + Mathf.Sin(angle) * Mathf.Sin(angle);

				return new Vector3(
					_motionSize * Mathf.Cos(angle) / Mathf.Max(0.001f, denom),
					0f,
					_motionSize * Mathf.Sin(angle) * Mathf.Cos(angle) / Mathf.Max(0.001f, denom));
			}
		}

		return Vector3.zero;
	}

	// =====================================================================
	// Render hooks
	// =====================================================================

	protected override void OnBeforeRender()
	{
		if (PreviewCam != null)
		{
			PreviewCam.nearClipPlane = 0.01f;
			PreviewCam.farClipPlane = 500f;
		}

		foreach (var r in _renderers)
		{
			if (r != null)
				r.enabled = true;
		}
	}

	protected override void OnAfterRender()
	{
		foreach (var r in _renderers)
		{
			if (r != null)
				r.enabled = false;
		}
	}

	// =====================================================================
	// Extra toolbar
	// =====================================================================

	protected override void DrawExtraToolbar(ref Rect previewRect)
	{
		DrawPlaybackBar(ref previewRect);

		if (_needsMotion)
			DrawMotionBar(ref previewRect);
	}

	protected override bool ShouldDrawSharedToolbarInPreview() => false;
	protected override bool RenderPreviewAtOrigin() => false;
	protected override bool ShouldShowLightsToolbarButton() => false;
	// Particles need perspective orbit camera regardless of the active render pipeline.
	protected override bool ShouldUse2DCompatibilityMode() => false;
	// No external-focus auto-pause: the repaint is now scoped to the Inspector only
	// (ObjectPreview.Repaint), so ticking the simulation never floods other windows
	// with repaints or disrupts their input. Particles play continuously.
	protected override bool ShouldAutoPausePlaybackOnExternalInteraction() => false;
	protected override bool Prefer3DGridOrientationWhenNotIn2DCompatibilityMode() => true;

	private void DrawPlaybackBar(ref Rect previewRect)
	{
		const float rowHeight = 40f;
		const float buttonSize = 29f;
		const float sidePadding = 6f;
		const float buttonGap = 4f;
		const float transportGap = 16f;
		const float dividerGap = 10f;
		const float secondRowGap = 4f;
		const float minSliderWidth = 72f;

		float sharedWidth = GetSharedPreviewToolbarWidth(buttonSize, buttonGap);
		float firstRowMinWidth = sidePadding
			+ buttonSize + buttonGap
			+ buttonSize + buttonGap
			+ transportGap
			+ minSliderWidth
			+ sidePadding;
		bool wrapSharedButtons = sharedWidth > 0f &&
		                         previewRect.width < firstRowMinWidth + dividerGap + dividerGap + sharedWidth;

		float barH = wrapSharedButtons ? rowHeight * 2f + secondRowGap : rowHeight;
		var bar = new Rect(previewRect.x, previewRect.y, previewRect.width, barH);
		previewRect = new Rect(previewRect.x, previewRect.y + barH, previewRect.width, previewRect.height - barH);

		ImprovedEditorTheme.DrawToolbarBackground(bar);

		float firstRowY = bar.y;
		float centerY = firstRowY + rowHeight * 0.5f;
		float buttonY = Mathf.Round(centerY - buttonSize * 0.5f);
		float x = bar.x + sidePadding;

		Rect playRect = new Rect(x, buttonY, buttonSize, buttonSize);
		bool newPlaying = DrawPlaybackIconToggle(playRect, IsPlaying, GetPlaybackIconContent(IsPlaying));
		x += buttonSize + buttonGap;

		if (newPlaying != IsPlaying)
		{
			if (newPlaying) StartPlayback();
			else StopPlayback();
		}

		Rect restartRect = new Rect(x, buttonY, buttonSize, buttonSize);
		if (DrawPlaybackIconButton(restartRect, GetRestartIconContent()))
		{
			SeekToTime(0.05f);
			ForcePausedSimulationRefresh();
			StartPlayback();
		}
		x += buttonSize + buttonGap;

		float safeMax = Mathf.Max(0.0001f, MaxTime);
		float clampedTime = Mathf.Clamp(CurrentTime, 0f, safeMax);
		float normalizedTime = Mathf.Clamp01(clampedTime / safeMax);

		float sharedX = bar.xMax - sidePadding - sharedWidth;
		float dividerX = sharedX - dividerGap;
		float scrubberLeft = x + transportGap;
		float scrubberRight = wrapSharedButtons
			? bar.xMax - sidePadding
			: dividerX - dividerGap;
		float sliderWidth = Mathf.Max(56f, scrubberRight - scrubberLeft);
		Rect sliderRect = new Rect(scrubberLeft, Mathf.Round(centerY - 9f), sliderWidth, 18f);
		float newTime = DrawPlaybackSlider(sliderRect, clampedTime, safeMax, normalizedTime);

		if (sharedWidth > 0f && !wrapSharedButtons)
		{
			ImprovedEditorTheme.DrawDivider(new Rect(dividerX, firstRowY + 8f, 1f, rowHeight - 16f));
			float sharedButtonX = sharedX;
			DrawSharedPreviewToolbarButtons(buttonY, buttonSize, buttonSize, buttonGap, ref sharedButtonX);
		}
		else if (sharedWidth > 0f)
		{
			float secondRowY = firstRowY + rowHeight + secondRowGap;
			ImprovedEditorTheme.DrawDivider(new Rect(bar.x, secondRowY - 1f, bar.width, 1f));
			float sharedButtonY = Mathf.Round(secondRowY + rowHeight * 0.5f - buttonSize * 0.5f);
			float sharedButtonX = Mathf.Max(bar.x + sidePadding, bar.xMax - sidePadding - sharedWidth);
			DrawSharedPreviewToolbarButtons(sharedButtonY, buttonSize, buttonSize, buttonGap, ref sharedButtonX);
		}

		if (!Mathf.Approximately(newTime, clampedTime))
		{
			SeekToTime(newTime);
			ForcePausedSimulationRefresh();
		}
	}

	private float DrawPlaybackSlider(Rect rect, float currentTime, float maxTime, float normalizedTime)
	{
		Event e = Event.current;
		int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);

		Rect trackRect = new Rect(rect.x, rect.y + rect.height * 0.5f - 3f, rect.width, 6f);
		Rect fillRect = new Rect(trackRect.x, trackRect.y, trackRect.width * normalizedTime, trackRect.height);
		float thumbCenterX = Mathf.Lerp(trackRect.x, trackRect.xMax, normalizedTime);
		Rect thumbRect = new Rect(thumbCenterX - 5f, rect.y + rect.height * 0.5f - 8f, 10f, 16f);

		Color trackColor = ImprovedEditorTheme.GetSliderTrackColor();
		Color thumbColor = ImprovedEditorTheme.GetSliderThumbColor(GUIUtility.hotControl == controlId);

		EditorGUI.DrawRect(trackRect, trackColor);
		DrawVisibilityProfile(trackRect);
		DrawCurrentTimeMarker(trackRect, thumbCenterX);
		EditorGUI.DrawRect(thumbRect, thumbColor);

		switch (e.GetTypeForControl(controlId))
		{
			case EventType.MouseDown:
				if (rect.Contains(e.mousePosition))
				{
					GUIUtility.hotControl = controlId;
					e.Use();
					return TimeFromMouse(rect, e.mousePosition.x, maxTime);
				}
				break;
			case EventType.MouseDrag:
				if (GUIUtility.hotControl == controlId)
				{
					e.Use();
					return TimeFromMouse(rect, e.mousePosition.x, maxTime);
				}
				break;
			case EventType.MouseUp:
				if (GUIUtility.hotControl == controlId)
				{
					GUIUtility.hotControl = 0;
					e.Use();
					return TimeFromMouse(rect, e.mousePosition.x, maxTime);
				}
				break;
		}

		return currentTime;
	}

	private static void DrawCurrentTimeMarker(Rect trackRect, float thumbCenterX)
	{
		Rect glowRect = new Rect(
			Mathf.Round(thumbCenterX - 2f),
			trackRect.y - 2f,
			4f,
			trackRect.height + 4f);
		Rect markerRect = new Rect(
			Mathf.Round(thumbCenterX - 1f),
			trackRect.y - 3f,
			2f,
			trackRect.height + 6f);

		EditorGUI.DrawRect(glowRect, new Color(0f, 0f, 0f, 0.35f));
		EditorGUI.DrawRect(markerRect, new Color(1f, 1f, 1f, 0.95f));
	}

	private void DrawVisibilityProfile(Rect trackRect)
	{
		EnsureVisibilityProfile();
		if (_visibilityProfile == null || _visibilityProfile.Length < 2)
			return;

		float segmentWidth = trackRect.width / (_visibilityProfile.Length - 1);
		for (int i = 0; i < _visibilityProfile.Length - 1; i++)
		{
			float left = _visibilityProfile[i];
			float right = _visibilityProfile[i + 1];
			float strength = Mathf.Clamp01(Mathf.Max(left, right));
			Color segmentColor = Color.Lerp(
				ImprovedEditorTheme.GetSliderFillStart(),
				ImprovedEditorTheme.GetSliderFillEnd(),
				strength);

			Rect segmentRect = new Rect(
				trackRect.x + segmentWidth * i,
				trackRect.y,
				Mathf.Ceil(segmentWidth + 1f),
				trackRect.height);
			EditorGUI.DrawRect(segmentRect, segmentColor);
		}
	}

	private void EnsureVisibilityProfile()
	{
		if (!_visibilityProfileDirty && Mathf.Approximately(_visibilityProfileDuration, MaxTime))
			return;

		RebuildVisibilityProfile();
	}

	private void MarkVisibilityProfileDirty()
	{
		_visibilityProfileDirty = true;
	}

	private void RebuildVisibilityProfile()
	{
		if (_allSystems.Count == 0 || PreviewRoot == null)
		{
			_visibilityProfile = Array.Empty<float>();
			_visibilityProfileDuration = MaxTime;
			_visibilityProfileDirty = false;
			return;
		}

		bool wasPlaying = IsPlaying;
		float restoreTime = CurrentTime;
		StopPlayback();

		const int sampleCount = 64;
		float[] aliveCounts = new float[sampleCount];
		float duration = Mathf.Max(0.0001f, MaxTime);
		float maxAliveCount = 0f;

		for (int i = 0; i < sampleCount; i++)
		{
			float sampleTime = duration * i / (sampleCount - 1);
			RebuildSimulationToTime(sampleTime);
			float alive = GetAliveParticleCount();
			aliveCounts[i] = alive;
			maxAliveCount = Mathf.Max(maxAliveCount, alive);
		}

		if (maxAliveCount > 0.0001f)
		{
			for (int i = 0; i < aliveCounts.Length; i++)
				aliveCounts[i] /= maxAliveCount;
		}
		else
		{
			for (int i = 0; i < aliveCounts.Length; i++)
				aliveCounts[i] = 0f;
		}

		_visibilityProfile = aliveCounts;
		_visibilityProfileDuration = MaxTime;
		_visibilityProfileDirty = false;

		SeekToTime(restoreTime);
		ForcePausedSimulationRefresh();
		if (wasPlaying)
			StartPlayback();
	}

	private float GetAliveParticleCount()
	{
		float total = 0f;
		for (int i = 0; i < _allSystems.Count; i++)
		{
			ParticleSystem ps = _allSystems[i];
			if (ps != null)
				total += ps.particleCount;
		}

		return total;
	}

	private static float TimeFromMouse(Rect rect, float mouseX, float maxTime)
	{
		float t = Mathf.InverseLerp(rect.x, rect.xMax, mouseX);
		return Mathf.Lerp(0f, maxTime, Mathf.Clamp01(t));
	}

	private static bool DrawPlaybackIconToggle(Rect rect, bool value, GUIContent content)
	{
		Event e = Event.current;
		if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition) && e.button == 0)
		{
			value = !value;
			GUI.changed = true;
			e.Use();
		}

		DrawPlaybackIconButtonVisual(rect, content, value);
		return value;
	}

	private static bool DrawPlaybackIconButton(Rect rect, GUIContent content)
	{
		bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
		if (clicked)
			GUI.changed = true;
		DrawPlaybackIconButtonVisual(rect, content, false);
		return clicked;
	}

	private static void DrawPlaybackIconButtonVisual(Rect rect, GUIContent content, bool active)
	{
		bool hovered = rect.Contains(Event.current.mousePosition);
		Color bg = ImprovedEditorTheme.GetToolbarButtonBackground(active, hovered);
		Color border = ImprovedEditorTheme.GetToolbarButtonBorder(active);

		EditorGUI.DrawRect(rect, bg);
		EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), border);
		EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), border);

		Texture image = content.image;
		if (image != null)
		{
			float iconSize = Mathf.Min(rect.width, rect.height) - 8f;
			Rect iconRect = new Rect(
				Mathf.Round(rect.center.x - iconSize * 0.5f),
				Mathf.Round(rect.center.y - iconSize * 0.5f),
				iconSize,
				iconSize);
			Color previousColor = GUI.color;
			GUI.color = ImprovedEditorTheme.GetToolbarIconTint(active);
			GUI.DrawTexture(iconRect, image, ScaleMode.ScaleToFit, true);
			GUI.color = previousColor;
		}
		else
		{
			GUI.Label(rect, content.text, EditorStyles.miniLabel);
		}

		if (!string.IsNullOrEmpty(content.tooltip) && rect.Contains(Event.current.mousePosition))
			GUI.Label(rect, new GUIContent(string.Empty, content.tooltip));
	}

	private static GUIContent GetPlaybackIconContent(bool isPlaying)
	{
		if (isPlaying)
			return GetIconContent("Pause", "Pause particle preview playback", "PauseButton", "d_PauseButton");

		return GetIconContent("Play", "Play particle preview playback", "PlayButton", "d_PlayButton");
	}

	private static GUIContent GetRestartIconContent()
	{
		return GetIconContent("Restart", "Restart particle preview playback", "Refresh", "d_Refresh");
	}

	private void DrawMotionBar(ref Rect previewRect)
	{
		const float barH = 28f;
		var bar = new Rect(previewRect.x, previewRect.y, previewRect.width, barH);
		previewRect = new Rect(previewRect.x, previewRect.y + barH, previewRect.width, previewRect.height - barH);

		Color bg = EditorGUIUtility.isProSkin
			? new Color(0.12f, 0.12f, 0.12f, 0.95f)
			: new Color(0.78f, 0.78f, 0.78f, 0.95f);
		EditorGUI.DrawRect(bar, bg);
		EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width, 1f), new Color(0f, 0f, 0f, 0.3f));

		float x = bar.x + 8f;
		float y = bar.y + 5f;
		float h = barH - 10f;

		GUI.Label(new Rect(x, y, 100f, h), "Movement Speed", EditorStyles.miniLabel);
		x += 104f;

		EditorGUI.BeginChangeCheck();
		float newSpeed = Mathf.Max(0.01f, EditorGUI.FloatField(new Rect(x, y, 46f, h), _motionSpeed, EditorStyles.numberField));
		if (EditorGUI.EndChangeCheck())
		{
			_motionSpeed = newSpeed;
			MarkVisibilityProfileDirty();
			NeedsSeek = true;
			ForcePausedSimulationRefresh();
		}
		x += 62f;

		GUI.Label(new Rect(x, y, 44f, h), "Shape", EditorStyles.miniLabel);
		x += 48f;

		EditorGUI.BeginChangeCheck();
		MotionShape newShape = (MotionShape)EditorGUI.EnumPopup(new Rect(x, y, 80f, h), _motionShape, EditorStyles.miniPullDown);
		if (EditorGUI.EndChangeCheck())
		{
			_motionShape = newShape;
			MarkVisibilityProfileDirty();
			NeedsSeek = true;
			ForcePausedSimulationRefresh();
		}
		x += 96f;

		GUI.Label(new Rect(x, y, 68f, h), "Shape Size", EditorStyles.miniLabel);
		x += 72f;

		EditorGUI.BeginChangeCheck();
		float newSize = Mathf.Max(0.1f, EditorGUI.FloatField(new Rect(x, y, 46f, h), _motionSize, EditorStyles.numberField));
		if (EditorGUI.EndChangeCheck())
		{
			_motionSize = newSize;
			MarkVisibilityProfileDirty();
			NeedsSeek = true;
			ForcePausedSimulationRefresh();
		}

		Event e = Event.current;
		if (bar.Contains(e.mousePosition) &&
		    GUIUtility.hotControl == 0 &&
		    (e.type == EventType.MouseDown || e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel))
		{
			e.Use();
		}
	}
}
}
#endif
