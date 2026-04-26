#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	public partial class ModelPrefabPreview
	{
		private void AutoFrame()
		{
			if (PreviewRoot == null || _renderers.Count == 0) return;

			List<FramingCandidate> candidates = BuildFramingCandidates(includeParticleRenderers: false);
			if (candidates.Count == 0)
				candidates = BuildFramingCandidates(includeParticleRenderers: true);
			if (candidates.Count == 0) return;

			Bounds bounds = ComputeLegacyFramingBounds(candidates);
			if (TrySelectRobustFramingBounds(candidates, out Bounds robustBounds))
				bounds = robustBounds;

			_framedBounds = bounds;
			_hasFramedBounds = true;
			SetPivot(bounds.center);

			if (IsTwoDimensionalRendererCompatibilityModeActive())
			{
				float twoDSize = Mathf.Max(bounds.extents.x, bounds.extents.y, 0.1f);
				float twoDDistanceFactor = ImprovedPreviewSettings.ModelPreviewTwoDDistanceFactor;
				float minimumDistance = ImprovedPreviewSettings.ModelPreviewMinimumDistance;
				SetCameraDistanceWithIntroZoom(Mathf.Max(twoDSize * twoDDistanceFactor, minimumDistance));
				SetOrbit(Vector2.zero);
				return;
			}

			float size = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.1f);
			float fovRad = PreviewCam.fieldOfView * Mathf.Deg2Rad * 0.5f;
			float fitMultiplier = ImprovedPreviewSettings.ModelPreviewPerspectiveFitMultiplier;
			float paddingMultiplier = ImprovedPreviewSettings.ModelPreviewPerspectivePaddingMultiplier;
			float minimumPerspectiveDistance = ImprovedPreviewSettings.ModelPreviewMinimumDistance;
			float fittedDist = Mathf.Max(size * fitMultiplier / Mathf.Max(Mathf.Tan(fovRad), 0.001f), minimumPerspectiveDistance);
			float dist = Mathf.Max(fittedDist * paddingMultiplier, ImprovedPreviewSettings.DefaultDist);
			SetCameraDistanceWithIntroZoom(dist);
		}

		private readonly struct FramingCandidate
		{
			public FramingCandidate(Bounds worldBounds)
			{
				WorldBounds = worldBounds;
				Center = worldBounds.center;
				Diagonal = worldBounds.size.magnitude;
			}

			public Bounds WorldBounds { get; }
			public Vector3 Center { get; }
			public float Diagonal { get; }
		}

		private static List<int> BuildComponentIndices(int startIndex, List<List<int>> neighbors, bool[] visited)
		{
			var stack = new Stack<int>();
			var indices = new List<int>();
			stack.Push(startIndex);
			visited[startIndex] = true;

			while (stack.Count > 0)
			{
				int index = stack.Pop();
				indices.Add(index);

				List<int> adjacent = neighbors[index];
				for (int i = 0; i < adjacent.Count; i++)
				{
					int next = adjacent[i];
					if (visited[next])
						continue;

					visited[next] = true;
					stack.Push(next);
				}
			}

			return indices;
		}

		private static float ComputeBoundsGapSqr(Bounds a, Bounds b)
		{
			Vector3 aMin = a.min;
			Vector3 aMax = a.max;
			Vector3 bMin = b.min;
			Vector3 bMax = b.max;

			float dx = Mathf.Max(0f, Mathf.Max(aMin.x - bMax.x, bMin.x - aMax.x));
			float dy = Mathf.Max(0f, Mathf.Max(aMin.y - bMax.y, bMin.y - aMax.y));
			float dz = Mathf.Max(0f, Mathf.Max(aMin.z - bMax.z, bMin.z - aMax.z));
			return dx * dx + dy * dy + dz * dz;
		}

		private static Bounds ComputeLegacyFramingBounds(List<FramingCandidate> candidates)
		{
			Bounds bounds = candidates[0].WorldBounds;
			for (int i = 1; i < candidates.Count; i++)
				bounds.Encapsulate(candidates[i].WorldBounds);

			return bounds;
		}

		private static float ComputeMedianDiagonal(List<FramingCandidate> candidates)
		{
			float[] diagonals = new float[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
				diagonals[i] = Mathf.Max(0f, candidates[i].Diagonal);

			Array.Sort(diagonals);
			int mid = diagonals.Length / 2;
			if ((diagonals.Length & 1) == 1)
				return diagonals[mid];

			return (diagonals[mid - 1] + diagonals[mid]) * 0.5f;
		}

		private List<FramingCandidate> BuildFramingCandidates(bool includeParticleRenderers)
		{
			var candidates = new List<FramingCandidate>(_renderers.Count);
			for (int i = 0; i < _renderers.Count; i++)
			{
				Renderer renderer = _renderers[i];
				if (!ShouldIncludeRendererForFraming(renderer, includeParticleRenderers))
					continue;

				Bounds worldBounds = GetRendererWorldBounds(renderer);
				if (worldBounds.size.sqrMagnitude <= 0.000001f)
					continue;

				candidates.Add(new FramingCandidate(worldBounds));
			}

			return candidates;
		}

		private static bool TrySelectRobustFramingBounds(List<FramingCandidate> candidates, out Bounds selectedBounds)
		{
			selectedBounds = default;
			if (candidates == null || candidates.Count == 0)
				return false;

			float medianDiagonal = ComputeMedianDiagonal(candidates);
			float connectionThreshold = Mathf.Clamp(medianDiagonal * 10f, 0.5f, 25f);
			float connectionThresholdSqr = connectionThreshold * connectionThreshold;

			var neighbors = new List<List<int>>(candidates.Count);
			for (int i = 0; i < candidates.Count; i++)
				neighbors.Add(new List<int>());

			for (int i = 0; i < candidates.Count; i++)
			{
				Bounds a = candidates[i].WorldBounds;
				for (int j = i + 1; j < candidates.Count; j++)
				{
					Bounds b = candidates[j].WorldBounds;
					if (ComputeBoundsGapSqr(a, b) > connectionThresholdSqr)
						continue;

					neighbors[i].Add(j);
					neighbors[j].Add(i);
				}
			}

			bool[] visited = new bool[candidates.Count];
			var components = new List<List<int>>();
			for (int i = 0; i < candidates.Count; i++)
			{
				if (visited[i])
					continue;
				components.Add(BuildComponentIndices(i, neighbors, visited));
			}

			if (components.Count == 0)
				return false;

			float nearestCenterDistance = float.PositiveInfinity;
			for (int i = 0; i < candidates.Count; i++)
			{
				float distance = Vector3.Distance(candidates[i].Center, WorldOffset);
				if (distance < nearestCenterDistance)
					nearestCenterDistance = distance;
			}

			bool pivotReliable = nearestCenterDistance <= Mathf.Max(1.5f, medianDiagonal * 3f);
			List<int> bestComponent = null;
			float bestAverageDistance = float.PositiveInfinity;
			int bestRendererCount = -1;

			for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
			{
				List<int> component = components[componentIndex];
				if (component == null || component.Count == 0)
					continue;

				float averageDistance = 0f;
				for (int i = 0; i < component.Count; i++)
					averageDistance += Vector3.Distance(candidates[component[i]].Center, WorldOffset);

				averageDistance /= component.Count;

				if (pivotReliable)
				{
					bool betterDistance = averageDistance < bestAverageDistance - 0.0001f;
					bool tieDistanceMoreRenderers = Mathf.Abs(averageDistance - bestAverageDistance) <= 0.0001f
					                                && component.Count > bestRendererCount;
					if (!betterDistance && !tieDistanceMoreRenderers)
						continue;
				}
				else
				{
					bool moreRenderers = component.Count > bestRendererCount;
					bool tieRenderersCloser = component.Count == bestRendererCount
					                          && averageDistance < bestAverageDistance - 0.0001f;
					if (!moreRenderers && !tieRenderersCloser)
						continue;
				}

				bestComponent = component;
				bestRendererCount = component.Count;
				bestAverageDistance = averageDistance;
			}

			if (bestComponent == null || bestComponent.Count == 0)
				return false;

			Bounds bounds = candidates[bestComponent[0]].WorldBounds;
			for (int i = 1; i < bestComponent.Count; i++)
				bounds.Encapsulate(candidates[bestComponent[i]].WorldBounds);

			selectedBounds = bounds;
			return true;
		}

		private static bool ShouldIncludeRendererForFraming(Renderer renderer, bool includeParticleRenderers)
		{
			if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
				return false;

			if (!includeParticleRenderers && renderer is ParticleSystemRenderer)
				return false;

			return true;
		}

		private static Bounds GetRendererWorldBounds(Renderer renderer)
		{
			if (renderer is SkinnedMeshRenderer)
				return renderer.bounds;

			Bounds local = renderer.localBounds;
			return TransformBounds(local, renderer.transform.localToWorldMatrix);
		}

		private static Bounds TransformBounds(Bounds local, Matrix4x4 m)
		{
			Vector3 c = local.center;
			Vector3 e = local.extents;
			Bounds wb = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);

			for (int sx = -1; sx <= 1; sx += 2)
			for (int sy = -1; sy <= 1; sy += 2)
			for (int sz = -1; sz <= 1; sz += 2)
				wb.Encapsulate(m.MultiplyPoint3x4(c + new Vector3(e.x * sx, e.y * sy, e.z * sz)));

			return wb;
		}

		private Bounds GetRenderBounds()
		{
			if (!_hasFramedBounds)
				return new Bounds(Vector3.zero, Vector3.one);

			return new Bounds(_framedBounds.center - WorldOffset, _framedBounds.size);
		}
	}
}
#endif
