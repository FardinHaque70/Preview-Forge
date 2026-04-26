#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public partial class ModelPrefabPreview
{
	// Animation preview state (model assets only)
	private bool _isModelAsset;
	private AnimationClip[] _animClips;
	private string[] _animClipNames;
	private int _currentClipIndex;
	private Animator _previewAnimator;
	private PlayableGraph _playableGraph;
	private AnimationClipPlayable _clipPlayable;
	private GUIStyle _animTimeStyle;

	private void DrawAnimationToolbar(ref Rect previewRect)
	{
		AnimationClip clip = _animClips[_currentClipIndex];
		EnsureAnimStyles();

		const float barHeight = 28f;
		const float sidePadding = 6f;
		const float buttonSize = 22f;
		const float gap = 4f;

		var bar = new Rect(previewRect.x, previewRect.y, previewRect.width, barHeight);
		previewRect = new Rect(previewRect.x, previewRect.y + barHeight, previewRect.width, previewRect.height - barHeight);
		ImprovedEditorTheme.DrawToolbarBackground(bar);

		float x = bar.x + sidePadding;
		float centerY = Mathf.Round(bar.center.y - buttonSize * 0.5f);

		bool playing = IsPlaying;
		if (DrawPreviewToolbarButton(new Rect(x, centerY, buttonSize, buttonSize), playing,
			playing ? "||" : ">", playing ? "Pause" : "Play",
			playing ? "d_PauseButton" : "d_PlayButton",
			playing ? "PauseButton" : "PlayButton"))
		{
			if (playing) StopPlayback();
			else StartPlayback();
		}
		x += buttonSize + gap;

		if (DrawPreviewToolbarButton(new Rect(x, centerY, buttonSize, buttonSize), false,
			"T", "Reset to default pose", "d_Refresh", "Refresh"))
		{
			ResetToBindPose();
		}
		x += buttonSize + gap;

		if (_animClips.Length > 1)
		{
			float dropdownWidth = Mathf.Min(120f, (bar.width - sidePadding * 2f - buttonSize * 2 - gap * 2) * 0.35f);
			var dropdownRect = new Rect(x, centerY, dropdownWidth, buttonSize);
			int newIndex = EditorGUI.Popup(dropdownRect, _currentClipIndex, _animClipNames, EditorStyles.toolbarPopup);
			if (newIndex != _currentClipIndex)
			{
				_currentClipIndex = newIndex;
				SetupPlayableGraph(_animClips[_currentClipIndex]);
				MaxTime = _animClips[_currentClipIndex].length;
				SeekToTime(0f);
				SampleCurrentClip(0f);
				StartPlayback();
			}
			x += dropdownWidth + gap;
		}

		float progressX = x;
		float progressWidth = bar.xMax - sidePadding - progressX;
		if (progressWidth > 40f)
		{
			const float progressHeight = 16f;
			var progressRect = new Rect(progressX, Mathf.Round(bar.center.y - progressHeight * 0.5f), progressWidth, progressHeight);

			EditorGUI.DrawRect(progressRect, new Color(0.15f, 0.15f, 0.15f, 1f));

			float progress = clip.length > 0f ? Mathf.Clamp01(CurrentTime / clip.length) : 0f;
			var fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);
			EditorGUI.DrawRect(fillRect, new Color(0.35f, 0.35f, 0.35f, 1f));

			float playheadX = progressRect.x + progressRect.width * progress;
			EditorGUI.DrawRect(new Rect(playheadX - 1f, progressRect.y, 2f, progressRect.height), new Color(0.85f, 0.85f, 0.85f, 0.9f));

			string timeText = $"{CurrentTime:F2} / {clip.length:F2}s";
			GUI.Label(progressRect, timeText, _animTimeStyle);

			int scrubId = GUIUtility.GetControlID("AnimScrub".GetHashCode(), FocusType.Passive, progressRect);
			Event evt = Event.current;
			switch (evt.GetTypeForControl(scrubId))
			{
				case EventType.MouseDown:
					if (evt.button == 0 && progressRect.Contains(evt.mousePosition))
					{
						GUIUtility.hotControl = scrubId;
						float t = Mathf.Clamp01((evt.mousePosition.x - progressRect.x) / progressRect.width) * clip.length;
						SeekToTime(t);
						SampleCurrentClip(t);
						evt.Use();
					}
					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl == scrubId)
					{
						float t = Mathf.Clamp01((evt.mousePosition.x - progressRect.x) / progressRect.width) * clip.length;
						SeekToTime(t);
						SampleCurrentClip(t);
						evt.Use();
					}
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == scrubId)
					{
						GUIUtility.hotControl = 0;
						evt.Use();
					}
					break;
			}
		}
	}

	private bool HasAnimationClips()
	{
		return _isModelAsset && _animClips != null && _animClips.Length > 0;
	}

	private void DiscoverAnimationClips(GameObject prefab)
	{
		_isModelAsset = false;
		_animClips = null;
		_animClipNames = null;
		_currentClipIndex = 0;

		if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Model)
			return;

		_isModelAsset = true;

		string assetPath = AssetDatabase.GetAssetPath(prefab);
		if (string.IsNullOrEmpty(assetPath))
			return;

		var clipSet = new HashSet<AnimationClip>();
		var clipList = new List<AnimationClip>();

		CollectClipsFromAsset(assetPath, clipSet, clipList);
		CollectClipsFromSiblingModels(assetPath, clipSet, clipList);

		if (clipList.Count == 0)
			return;

		_animClips = clipList.ToArray();
		_animClipNames = new string[_animClips.Length];
		for (int i = 0; i < _animClips.Length; i++)
			_animClipNames[i] = _animClips[i].name;

		_previewAnimator = PreviewRoot.GetComponent<Animator>();
		if (_previewAnimator == null)
			_previewAnimator = PreviewRoot.AddComponent<Animator>();

		_previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		_previewAnimator.enabled = true;

		foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
		{
			if (subAsset is Avatar avatar)
			{
				_previewAnimator.avatar = avatar;
				break;
			}
		}

		MaxTime = _animClips[0].length;
	}

	private static void CollectClipsFromAsset(string assetPath, HashSet<AnimationClip> clipSet, List<AnimationClip> clipList)
	{
		foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
		{
			if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && clipSet.Add(clip))
				clipList.Add(clip);
		}
	}

	private static void CollectClipsFromSiblingModels(string modelAssetPath, HashSet<AnimationClip> clipSet, List<AnimationClip> clipList)
	{
		string directory = System.IO.Path.GetDirectoryName(modelAssetPath);
		if (string.IsNullOrEmpty(directory))
			return;

		string modelFileName = System.IO.Path.GetFileNameWithoutExtension(modelAssetPath);

		string[] guids = AssetDatabase.FindAssets("t:Model", new[] { directory });
		foreach (string guid in guids)
		{
			string siblingPath = AssetDatabase.GUIDToAssetPath(guid);

			if (siblingPath == modelAssetPath)
				continue;
			if (System.IO.Path.GetDirectoryName(siblingPath) != directory)
				continue;

			string siblingFileName = System.IO.Path.GetFileNameWithoutExtension(siblingPath);
			if (!siblingFileName.StartsWith(modelFileName + "@", StringComparison.OrdinalIgnoreCase))
				continue;

			CollectClipsFromAsset(siblingPath, clipSet, clipList);
		}
	}

	private static bool TryGetModelRootRotationCompensation(GameObject prefab, out Quaternion rotation)
	{
		rotation = Quaternion.identity;

		if (prefab == null)
			return false;

		if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Model)
			return false;

		Quaternion modelRootRotation = prefab.transform.localRotation;
		if (Quaternion.Angle(modelRootRotation, Quaternion.identity) <= 0.1f)
			return false;

		rotation = modelRootRotation;
		return true;
	}

	protected override void OnSimulate(float dt)
	{
		if (!HasAnimationClips()) return;

		AnimationClip clip = _animClips[_currentClipIndex];

		if (CurrentTime >= clip.length)
		{
			if (clip.isLooping)
			{
				SeekToTime(CurrentTime % clip.length);
				StartPlayback();
			}
			else
			{
				SeekToTime(clip.length);
				StopPlayback();
				return;
			}
		}

		SampleCurrentClip(CurrentTime);
	}

	private void ResetToBindPose()
	{
		StopPlayback();
		DestroyPlayableGraph();

		if (PreviewRoot != null && _previewAnimator != null)
		{
			_previewAnimator.Rebind();
			_previewAnimator.Update(0f);
		}

		SeekToTime(0f);
		RequestPreviewRepaint();
	}

	private void SetupPlayableGraph(AnimationClip clip)
	{
		DestroyPlayableGraph();

		_playableGraph = PlayableGraph.Create("PreviewAnimGraph");
		_playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

		var output = AnimationPlayableOutput.Create(_playableGraph, "Output", _previewAnimator);
		_clipPlayable = AnimationClipPlayable.Create(_playableGraph, clip);
		output.SetSourcePlayable(_clipPlayable);
		_playableGraph.Play();
	}

	private void DestroyPlayableGraph()
	{
		if (_playableGraph.IsValid())
			_playableGraph.Destroy();
	}

	private void SampleCurrentClip(float time)
	{
		if (_animClips == null || _currentClipIndex >= _animClips.Length) return;
		if (PreviewRoot == null) return;

		if (!_playableGraph.IsValid())
			SetupPlayableGraph(_animClips[_currentClipIndex]);

		_clipPlayable.SetTime(time);
		_playableGraph.Evaluate();
	}

	private void EnsureAnimStyles()
	{
		if (_animTimeStyle == null)
		{
			_animTimeStyle = new GUIStyle(EditorStyles.miniLabel)
			{
				fontSize = 10,
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = new Color(0.75f, 0.75f, 0.75f, 1f) }
			};
		}
	}
}

}
#endif
