using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ParticleThumbnailAndPreview.Editor
{
	internal enum PreviewGridStyle
	{
		Stylized = 0,
		Classic = 1,
	}

	internal enum PreviewGridSpace
	{
		Plane3D = 0,
		Plane2D = 1,
	}

	internal readonly struct PreviewGridProfile
	{
		internal readonly bool DefaultEnabled;
		internal readonly float HalfSize;
		internal readonly float Step;
		internal readonly float Alpha;
		internal readonly PreviewGridStyle Style;

		internal PreviewGridProfile(bool defaultEnabled, float halfSize, float step, float alpha, PreviewGridStyle style)
		{
			DefaultEnabled = defaultEnabled;
			HalfSize = halfSize;
			Step = step;
			Alpha = alpha;
			Style = style;
		}
	}

		internal readonly struct PreviewGridDrawRequest
		{
			internal readonly PreviewRenderUtility Preview;
			internal readonly PreviewGridSpace Space;
			internal readonly bool SessionEnabled;
			internal readonly Matrix4x4? GridTransformOverride;
			internal readonly bool? EnabledOverride;
			internal readonly PreviewGridProfile? ProfileOverride;
			internal readonly float OpacityMultiplier;
			internal readonly bool DrawAxisMarkers;
			internal readonly bool DrawGridLines;

			internal PreviewGridDrawRequest(
				PreviewRenderUtility preview,
				PreviewGridSpace space,
				bool sessionEnabled,
				Matrix4x4? gridTransformOverride = null,
				bool? enabledOverride = null,
				PreviewGridProfile? profileOverride = null,
				float opacityMultiplier = 1f,
				bool drawAxisMarkers = true,
				bool drawGridLines = true)
			{
				Preview = preview;
				Space = space;
				SessionEnabled = sessionEnabled;
				GridTransformOverride = gridTransformOverride;
				EnabledOverride = enabledOverride;
				ProfileOverride = profileOverride;
				OpacityMultiplier = opacityMultiplier;
				DrawAxisMarkers = drawAxisMarkers;
				DrawGridLines = drawGridLines;
			}
		}

	internal static class PreviewGridSystem
	{
		private const int MaxCachedMeshes = 16;
		private const int MaxCachedAxisMarkerMeshes = 16;
		private const bool AxisMarkersEnabled = true;
		private const bool AxisArrowsEnabled = false;
		private const float AxisMarkerDistanceSteps = 8f;
		private const float AxisMarkerLiftSteps = 0.02f;
		private const float AxisArrowShaftLengthSteps = 0.85f;
        private const float AxisArrowHeadLengthSteps = 0.45f;
        private const float AxisArrowHalfWidthSteps = 0.14f;
        private const float AxisArrowFadeEndAlpha = 0f;
        private const float AxisTextScale = 1f;
        private const float AxisTextOpacity = 0.8f;
        private const float AxisTextOffsetSteps = 0.42f;
        private const float AxisTextHeightSteps = 0.42f;
        private const float AxisTextStrokeSteps = 0.05f;
        private const float AxisTextCharacterSpacingSteps = 0.16f;

		private static readonly Dictionary<GridMeshCacheKey, Mesh> MeshCache = new();
		private static readonly Queue<GridMeshCacheKey> MeshCacheOrder = new();
		private static readonly Dictionary<AxisMarkerMeshCacheKey, Mesh> AxisMarkerMeshCache = new();
		private static readonly Queue<AxisMarkerMeshCacheKey> AxisMarkerMeshCacheOrder = new();
		private static Material s_gridMaterial;

			internal static bool Draw(in PreviewGridDrawRequest request)
			{
				if (request.Preview == null)
					return false;

			bool enabled = request.EnabledOverride ?? request.SessionEnabled;
			if (!enabled)
				return false;

			PreviewGridProfile profile = request.ProfileOverride ?? PreviewSettings.SharedGridProfile;
			profile = ClampProfile(profile);

			PreviewGridResources.EnsureGridMaterial(ref s_gridMaterial);
			if (s_gridMaterial == null)
				return false;

				Mesh mesh = GetOrCreateMesh(profile, request.Space);
				if (mesh == null)
					return false;

				float opacityMultiplier = Mathf.Clamp01(request.OpacityMultiplier);
				if (opacityMultiplier <= 0f)
					return false;

				Matrix4x4 matrix = request.GridTransformOverride ?? Matrix4x4.identity;
				bool drewAny = false;
				if (request.DrawGridLines)
				{
					ApplyMaterialOpacity(opacityMultiplier);
					request.Preview.DrawMesh(mesh, matrix, s_gridMaterial, 0);
					RestoreMaterialOpacity();
					drewAny = true;
				}

				if (AxisMarkersEnabled && request.DrawAxisMarkers && request.Space == PreviewGridSpace.Plane3D)
				{
					bool drawAxisText = PreviewSettings.SharedGridAxisTextDefaultEnabled;
					Mesh axisMarkerMesh = GetOrCreateAxisMarkerMesh(profile, drawAxisText);
					if (axisMarkerMesh != null)
					{
						ApplyMaterialOpacity(opacityMultiplier);
						request.Preview.DrawMesh(axisMarkerMesh, matrix, s_gridMaterial, 0);
						RestoreMaterialOpacity();
						drewAny = true;
					}
				}

				return drewAny;
			}

			private static Color s_previousMaterialColor;
			private static bool s_hasPreviousMaterialColor;

			private static void ApplyMaterialOpacity(float opacityMultiplier)
			{
				if (s_gridMaterial == null || !s_gridMaterial.HasProperty("_Color"))
					return;

				s_previousMaterialColor = s_gridMaterial.GetColor("_Color");
				s_hasPreviousMaterialColor = true;
				Color tinted = s_previousMaterialColor;
				tinted.a *= opacityMultiplier;
				s_gridMaterial.SetColor("_Color", tinted);
			}

			private static void RestoreMaterialOpacity()
			{
				if (!s_hasPreviousMaterialColor || s_gridMaterial == null || !s_gridMaterial.HasProperty("_Color"))
					return;

				s_gridMaterial.SetColor("_Color", s_previousMaterialColor);
				s_hasPreviousMaterialColor = false;
			}

		internal static void ClearMeshCache()
		{
			foreach (Mesh mesh in MeshCache.Values)
			{
				if (mesh != null)
					Object.DestroyImmediate(mesh);
			}

			MeshCache.Clear();
			MeshCacheOrder.Clear();

			foreach (Mesh mesh in AxisMarkerMeshCache.Values)
			{
				if (mesh != null)
					Object.DestroyImmediate(mesh);
			}

			AxisMarkerMeshCache.Clear();
			AxisMarkerMeshCacheOrder.Clear();
		}

		private static PreviewGridProfile ClampProfile(PreviewGridProfile profile)
		{
			float halfSize = Mathf.Clamp(profile.HalfSize, PreviewSettings.MinSharedGridHalfSize, PreviewSettings.MaxSharedGridHalfSize);
			float step = Mathf.Clamp(profile.Step, PreviewSettings.MinSharedGridStep, PreviewSettings.MaxSharedGridStep);
			float alpha = Mathf.Clamp01(profile.Alpha);
			return new PreviewGridProfile(profile.DefaultEnabled, halfSize, step, alpha, profile.Style);
		}

		private static Mesh GetOrCreateMesh(PreviewGridProfile profile, PreviewGridSpace space)
		{
			var key = new GridMeshCacheKey(profile, space);
			if (MeshCache.TryGetValue(key, out Mesh cachedMesh))
			{
				if (cachedMesh != null)
					return cachedMesh;

				MeshCache.Remove(key);
			}

			EvictOldestMeshIfNeeded();
			bool is2D = space == PreviewGridSpace.Plane2D;
			Mesh created = PreviewGridResources.CreateGridMesh(profile.HalfSize, profile.Step, profile.Alpha, is2D, profile.Style);
			if (created == null)
				return null;

			MeshCache[key] = created;
			MeshCacheOrder.Enqueue(key);
			return created;
		}

		private static void EvictOldestMeshIfNeeded()
		{
			while (MeshCache.Count >= MaxCachedMeshes && MeshCacheOrder.Count > 0)
			{
				GridMeshCacheKey oldest = MeshCacheOrder.Dequeue();
				if (!MeshCache.TryGetValue(oldest, out Mesh mesh))
					continue;

				MeshCache.Remove(oldest);
				if (mesh != null)
					Object.DestroyImmediate(mesh);
			}
		}

		private static Mesh GetOrCreateAxisMarkerMesh(PreviewGridProfile profile, bool drawText)
		{
			var key = new AxisMarkerMeshCacheKey(profile.Step, profile.Alpha, drawText);
			if (AxisMarkerMeshCache.TryGetValue(key, out Mesh cachedMesh))
			{
				if (cachedMesh != null)
					return cachedMesh;

				AxisMarkerMeshCache.Remove(key);
			}

			EvictOldestAxisMarkerMeshIfNeeded();
			Mesh created = BuildAxisMarkerMesh(profile.Step, profile.Alpha, drawText);
			if (created == null)
				return null;

			AxisMarkerMeshCache[key] = created;
			AxisMarkerMeshCacheOrder.Enqueue(key);
			return created;
		}

		private static void EvictOldestAxisMarkerMeshIfNeeded()
		{
			while (AxisMarkerMeshCache.Count >= MaxCachedAxisMarkerMeshes && AxisMarkerMeshCacheOrder.Count > 0)
			{
				AxisMarkerMeshCacheKey oldest = AxisMarkerMeshCacheOrder.Dequeue();
				if (!AxisMarkerMeshCache.TryGetValue(oldest, out Mesh mesh))
					continue;

				AxisMarkerMeshCache.Remove(oldest);
				if (mesh != null)
					Object.DestroyImmediate(mesh);
			}
		}

		private static Mesh BuildAxisMarkerMesh(float step, float alpha, bool drawText)
		{
			float safeStep = Mathf.Max(step, PreviewSettings.MinSharedGridStep);
			float markerAlpha = Mathf.Clamp01(Mathf.Max(alpha * 2.6f, 0.72f));
			float distance = safeStep * AxisMarkerDistanceSteps;
			float lift = safeStep * AxisMarkerLiftSteps;
			float shaftLength = safeStep * AxisArrowShaftLengthSteps;
			float headLength = safeStep * AxisArrowHeadLengthSteps;
			float halfWidth = safeStep * AxisArrowHalfWidthSteps;
            float textHeight = safeStep * AxisTextHeightSteps * AxisTextScale;
            float stroke = safeStep * AxisTextStrokeSteps * AxisTextScale;
            float textOffset = safeStep * AxisTextOffsetSteps;
            float characterSpacing = safeStep * AxisTextCharacterSpacingSteps * AxisTextScale;

			var vertices = new List<Vector3>(256);
			var colors = new List<Color>(256);
			var triangles = new List<int>(384);

			AddAxisMarker(vertices, colors, triangles, Vector3.right, distance, lift, shaftLength, headLength, halfWidth, textHeight, stroke, textOffset, characterSpacing, "+X",
				new Color(1f, 0.35f, 0.35f, markerAlpha), AxisArrowsEnabled, drawText);
			AddAxisMarker(vertices, colors, triangles, Vector3.left, distance, lift, shaftLength, headLength, halfWidth, textHeight, stroke, textOffset, characterSpacing, "-X",
				new Color(1f, 0.56f, 0.56f, markerAlpha), AxisArrowsEnabled, drawText);
			AddAxisMarker(vertices, colors, triangles, Vector3.forward, distance, lift, shaftLength, headLength, halfWidth, textHeight, stroke, textOffset, characterSpacing, "+Z",
				new Color(0.34f, 0.74f, 1f, markerAlpha), AxisArrowsEnabled, drawText);
			AddAxisMarker(vertices, colors, triangles, Vector3.back, distance, lift, shaftLength, headLength, halfWidth, textHeight, stroke, textOffset, characterSpacing, "-Z",
				new Color(0.54f, 0.82f, 1f, markerAlpha), AxisArrowsEnabled, drawText);

			if (vertices.Count == 0 || triangles.Count == 0)
				return null;

			var mesh = new Mesh {hideFlags = HideFlags.HideAndDontSave};
			mesh.SetVertices(vertices);
			mesh.SetColors(colors);
			mesh.SetTriangles(triangles, 0, true);
			mesh.RecalculateBounds();
			return mesh;
		}

		private static void AddAxisMarker(
			List<Vector3> vertices,
			List<Color> colors,
			List<int> triangles,
			Vector3 direction,
			float distance,
			float lift,
			float shaftLength,
			float headLength,
			float halfWidth,
			float textHeight,
			float textStroke,
			float textOffset,
			float characterSpacing,
			string label,
			Color color,
			bool drawArrow,
			bool drawText)
		{
			Vector3 center = direction * distance + Vector3.up * lift;
			Vector3 shaftStart = center - direction * (shaftLength * 0.5f);
			Vector3 shaftEnd = center + direction * (shaftLength * 0.5f);
			Vector3 tip = shaftEnd + direction * headLength;
			Vector3 perp = Vector3.Cross(Vector3.up, direction).normalized;

			if (drawArrow)
			{
                float endAlpha = color.a * Mathf.Clamp01(AxisArrowFadeEndAlpha);
                Color startColor = color;
                Color endColor = new Color(color.r, color.g, color.b, endAlpha);
                AddFilledSegmentGradient(vertices, colors, triangles, shaftStart, shaftEnd, halfWidth, startColor, endColor, Vector3.up);
                AddFilledTriangleGradient(vertices, colors, triangles, shaftEnd + perp * halfWidth, shaftEnd - perp * halfWidth, tip, endColor, endColor, endColor);
			}

			if (drawText)
			{
				float textAlpha = Mathf.Clamp01(color.a * Mathf.Clamp01(AxisTextOpacity));
				Color textColor = new Color(color.r, color.g, color.b, textAlpha);
				Vector3 textCenter = tip + direction * textOffset;
				AddAxisLabel(vertices, colors, triangles, textCenter, direction, perp, label, textHeight, textStroke, characterSpacing, textColor);
			}
        }

		private static void AddAxisLabel(
			List<Vector3> vertices,
			List<Color> colors,
			List<int> triangles,
			Vector3 center,
			Vector3 forward,
			Vector3 right,
			string label,
			float height,
			float stroke,
			float charSpacing,
			Color color)
		{
			float charWidth = height * 0.62f;
			float totalWidth = label.Length * charWidth + (label.Length - 1) * charSpacing;
			Vector3 cursor = center - right * (totalWidth * 0.5f) + right * (charWidth * 0.5f);

			for (int i = 0; i < label.Length; i++)
			{
				AddGlyph(vertices, colors, triangles, cursor, forward, right, label[i], charWidth, height, stroke, color);
				cursor += right * (charWidth + charSpacing);
			}
		}

		private static void AddGlyph(
			List<Vector3> vertices,
			List<Color> colors,
			List<int> triangles,
			Vector3 center,
			Vector3 forward,
			Vector3 right,
			char glyph,
			float width,
			float height,
			float stroke,
			Color color)
		{
			Vector3 up = forward.sqrMagnitude > 0.000001f ? forward.normalized : Vector3.forward;
			float halfWidth = width * 0.5f;
			float halfHeight = height * 0.5f;
			switch (glyph)
			{
				case '+':
					AddFilledSegment(vertices, colors, triangles, center - right * halfWidth, center + right * halfWidth, stroke * 0.5f, color, Vector3.up);
					AddFilledSegment(vertices, colors, triangles, center - up * halfHeight, center + up * halfHeight, stroke * 0.5f, color, Vector3.up);
					break;
				case '-':
					AddFilledSegment(vertices, colors, triangles, center - right * halfWidth, center + right * halfWidth, stroke * 0.5f, color, Vector3.up);
					break;
				case 'X':
					AddFilledSegment(vertices, colors, triangles, center - right * halfWidth - up * halfHeight, center + right * halfWidth + up * halfHeight, stroke * 0.5f, color, Vector3.up);
					AddFilledSegment(vertices, colors, triangles, center - right * halfWidth + up * halfHeight, center + right * halfWidth - up * halfHeight, stroke * 0.5f, color, Vector3.up);
					break;
				case 'Z':
					AddFilledSegment(vertices, colors, triangles, center + up * halfHeight - right * halfWidth, center + up * halfHeight + right * halfWidth, stroke * 0.5f, color, Vector3.up);
					AddFilledSegment(vertices, colors, triangles, center - up * halfHeight - right * halfWidth, center - up * halfHeight + right * halfWidth, stroke * 0.5f, color, Vector3.up);
					AddFilledSegment(vertices, colors, triangles, center + up * halfHeight + right * halfWidth, center - up * halfHeight - right * halfWidth, stroke * 0.5f, color, Vector3.up);
					break;
			}
		}

		private static void AddFilledSegment(
			List<Vector3> vertices,
			List<Color> colors,
			List<int> triangles,
			Vector3 from,
			Vector3 to,
			float halfWidth,
			Color color,
			Vector3 planeNormal)
		{
			Vector3 direction = to - from;
			if (direction.sqrMagnitude <= 0.000001f)
				return;

			Vector3 normal = planeNormal.sqrMagnitude > 0.000001f ? planeNormal.normalized : Vector3.up;
			Vector3 side = Vector3.Cross(normal, direction.normalized);
			if (side.sqrMagnitude <= 0.000001f)
				side = Vector3.Cross(Vector3.forward, direction.normalized);
			if (side.sqrMagnitude <= 0.000001f)
				side = Vector3.Cross(Vector3.right, direction.normalized);
			side = side.normalized * halfWidth;
			int baseIndex = vertices.Count;
			vertices.Add(from + side);
			vertices.Add(from - side);
			vertices.Add(to - side);
			vertices.Add(to + side);
			colors.Add(color);
			colors.Add(color);
			colors.Add(color);
			colors.Add(color);
			triangles.Add(baseIndex + 0);
			triangles.Add(baseIndex + 1);
			triangles.Add(baseIndex + 2);
			triangles.Add(baseIndex + 0);
			triangles.Add(baseIndex + 2);
			triangles.Add(baseIndex + 3);
		}

		private static void AddFilledTriangle(
			List<Vector3> vertices,
			List<Color> colors,
			List<int> triangles,
			Vector3 a,
			Vector3 b,
			Vector3 c,
			Color color)
		{
			int baseIndex = vertices.Count;
			vertices.Add(a);
			vertices.Add(b);
			vertices.Add(c);
			colors.Add(color);
			colors.Add(color);
			colors.Add(color);
			triangles.Add(baseIndex + 0);
			triangles.Add(baseIndex + 1);
			triangles.Add(baseIndex + 2);
		}

        private static void AddFilledSegmentGradient(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 from,
            Vector3 to,
            float halfWidth,
            Color fromColor,
            Color toColor,
            Vector3 planeNormal)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude <= 0.000001f)
                return;

            Vector3 normal = planeNormal.sqrMagnitude > 0.000001f ? planeNormal.normalized : Vector3.up;
            Vector3 side = Vector3.Cross(normal, direction.normalized);
            if (side.sqrMagnitude <= 0.000001f)
                side = Vector3.Cross(Vector3.forward, direction.normalized);
            if (side.sqrMagnitude <= 0.000001f)
                side = Vector3.Cross(Vector3.right, direction.normalized);
            side = side.normalized * halfWidth;
            int baseIndex = vertices.Count;
            vertices.Add(from + side);
            vertices.Add(from - side);
            vertices.Add(to - side);
            vertices.Add(to + side);
            colors.Add(fromColor);
            colors.Add(fromColor);
            colors.Add(toColor);
            colors.Add(toColor);
            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        private static void AddFilledTriangleGradient(
            List<Vector3> vertices,
            List<Color> colors,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Color colorA,
            Color colorB,
            Color colorC)
        {
            int baseIndex = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(colorA);
            colors.Add(colorB);
            colors.Add(colorC);
            triangles.Add(baseIndex + 0);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
        }

		private readonly struct GridMeshCacheKey
		{
			private readonly int _halfSizeMilli;
			private readonly int _stepMilli;
			private readonly int _alphaMilli;
			private readonly PreviewGridStyle _style;
			private readonly PreviewGridSpace _space;

			internal GridMeshCacheKey(PreviewGridProfile profile, PreviewGridSpace space)
			{
				_halfSizeMilli = Mathf.RoundToInt(profile.HalfSize * 1000f);
				_stepMilli = Mathf.RoundToInt(profile.Step * 1000f);
				_alphaMilli = Mathf.RoundToInt(profile.Alpha * 1000f);
				_style = profile.Style;
				_space = space;
			}

			public override bool Equals(object obj)
			{
				return obj is GridMeshCacheKey other && Equals(other);
			}

			private bool Equals(GridMeshCacheKey other)
			{
				return _halfSizeMilli == other._halfSizeMilli
				       && _stepMilli == other._stepMilli
				       && _alphaMilli == other._alphaMilli
				       && _style == other._style
				       && _space == other._space;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					int hash = _halfSizeMilli;
					hash = (hash * 397) ^ _stepMilli;
					hash = (hash * 397) ^ _alphaMilli;
					hash = (hash * 397) ^ (int) _style;
					hash = (hash * 397) ^ (int) _space;
					return hash;
				}
			}
		}

		private readonly struct AxisMarkerMeshCacheKey
		{
			private readonly int _stepMilli;
			private readonly int _alphaMilli;
			private readonly bool _drawText;

			internal AxisMarkerMeshCacheKey(float step, float alpha, bool drawText)
			{
				_stepMilli = Mathf.RoundToInt(step * 1000f);
				_alphaMilli = Mathf.RoundToInt(alpha * 1000f);
				_drawText = drawText;
			}

			public override bool Equals(object obj)
			{
				return obj is AxisMarkerMeshCacheKey other && Equals(other);
			}

			private bool Equals(AxisMarkerMeshCacheKey other)
			{
				return _stepMilli == other._stepMilli
				       && _alphaMilli == other._alphaMilli
				       && _drawText == other._drawText;
			}

			public override int GetHashCode()
			{
				unchecked
				{
					return ((_stepMilli * 397) ^ _alphaMilli) * 397 ^ (_drawText ? 1 : 0);
				}
			}
		}
	}
}
