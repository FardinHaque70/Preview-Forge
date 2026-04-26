#if UNITY_EDITOR
using UnityEngine;

namespace FardinHaque.ImprovedAssetTools.Editor
{
	internal readonly struct LightWidgetLayoutConfig
	{
		public readonly float BaseSphereSize;
		public readonly float SphereScale;
		public readonly float TopPadding;
		public readonly float RightPadding;
		public readonly float DragSensitivity;
		public readonly int TextureSize;

		public LightWidgetLayoutConfig(
			float baseSphereSize,
			float sphereScale,
			float topPadding,
			float rightPadding,
			float dragSensitivity,
			int textureSize)
		{
			BaseSphereSize = baseSphereSize;
			SphereScale = sphereScale;
			TopPadding = topPadding;
			RightPadding = rightPadding;
			DragSensitivity = dragSensitivity;
			TextureSize = textureSize;
		}
	}

	internal readonly struct LightWidgetRenderInput
	{
		public readonly Vector3 KeyLightDirection;
		public readonly Vector3 FillLightDirection;
		public readonly Color AmbientColor;
		public readonly float KeyIntensity;
		public readonly float FillIntensity;

		public LightWidgetRenderInput(
			Vector3 keyLightDirection,
			Vector3 fillLightDirection,
			Color ambientColor,
			float keyIntensity,
			float fillIntensity)
		{
			KeyLightDirection = keyLightDirection;
			FillLightDirection = fillLightDirection;
			AmbientColor = ambientColor;
			KeyIntensity = keyIntensity;
			FillIntensity = fillIntensity;
		}
	}

	internal struct LightWidgetCacheState
	{
		public Texture2D Texture;
		public Vector3 LastKeyLightDirection;
		public Vector3 LastFillLightDirection;
		public float LastKeyIntensity;
		public float LastFillIntensity;
		public Color LastAmbientColor;
	}

	internal static class PreviewLightWidgetSystem
	{
		public static readonly LightWidgetLayoutConfig DefaultLayoutConfig =
			new LightWidgetLayoutConfig(74f, 0.9f, 12f, 12f, 1.5f, 96);

		public static Rect GetSphereRect(Rect previewRect, in LightWidgetLayoutConfig layout)
		{
			float sphereSize = layout.BaseSphereSize * layout.SphereScale;
			return new Rect(
				previewRect.xMax - sphereSize - layout.RightPadding,
				previewRect.y + layout.TopPadding,
				sphereSize,
				sphereSize);
		}

		public static Texture2D GetOrUpdateTexture(
			ref LightWidgetCacheState cache,
			in LightWidgetRenderInput input,
			in LightWidgetLayoutConfig layout)
		{
			if (cache.Texture == null)
			{
				cache.Texture = new Texture2D(layout.TextureSize, layout.TextureSize, TextureFormat.RGBA32, false)
				{
					hideFlags = HideFlags.HideAndDontSave,
					wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Bilinear
				};

				cache.LastKeyLightDirection = Vector3.negativeInfinity;
				cache.LastFillLightDirection = Vector3.negativeInfinity;
				cache.LastKeyIntensity = -1f;
				cache.LastFillIntensity = -1f;
				cache.LastAmbientColor = Color.clear;
			}

			bool needsRebuild =
				(cache.LastKeyLightDirection - input.KeyLightDirection).sqrMagnitude > 0.0005f
				|| (cache.LastFillLightDirection - input.FillLightDirection).sqrMagnitude > 0.0005f
				|| !Mathf.Approximately(cache.LastKeyIntensity, input.KeyIntensity)
				|| !Mathf.Approximately(cache.LastFillIntensity, input.FillIntensity)
				|| cache.LastAmbientColor != input.AmbientColor;

			if (needsRebuild)
			{
				RebuildTexture(cache.Texture, input);
				cache.LastKeyLightDirection = input.KeyLightDirection;
				cache.LastFillLightDirection = input.FillLightDirection;
				cache.LastKeyIntensity = input.KeyIntensity;
				cache.LastFillIntensity = input.FillIntensity;
				cache.LastAmbientColor = input.AmbientColor;
			}

			return cache.Texture;
		}

		public static void DisposeTexture(ref LightWidgetCacheState cache)
		{
			if (cache.Texture != null)
				Object.DestroyImmediate(cache.Texture);

			cache.Texture = null;
			cache.LastKeyLightDirection = Vector3.negativeInfinity;
			cache.LastFillLightDirection = Vector3.negativeInfinity;
			cache.LastKeyIntensity = -1f;
			cache.LastFillIntensity = -1f;
			cache.LastAmbientColor = Color.clear;
		}

		private static void RebuildTexture(Texture2D texture, in LightWidgetRenderInput input)
		{
			int width = texture.width;
			int height = texture.height;
			Vector3 viewDirection = new Vector3(0f, 0f, 1f);
			Color transparentBackground = new Color(0f, 0f, 0f, 0f);

			float ambient = Mathf.Clamp01((input.AmbientColor.r + input.AmbientColor.g + input.AmbientColor.b) / 3f);
			float keyStrength = Mathf.Clamp01(input.KeyIntensity / 2f);
			float fillStrength = Mathf.Clamp01(input.FillIntensity / 2f);

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					float nx = (x + 0.5f) / width * 2f - 1f;
					float ny = (y + 0.5f) / height * 2f - 1f;
					float radiusSquared = nx * nx + ny * ny;

					if (radiusSquared > 1f)
					{
						texture.SetPixel(x, y, transparentBackground);
						continue;
					}

					float nz = Mathf.Sqrt(1f - radiusSquared);
					Vector3 normal = new Vector3(nx, ny, nz);

					float keyDiffuse = Mathf.Clamp01(Vector3.Dot(normal, input.KeyLightDirection)) * (0.58f * keyStrength);
					float fillDiffuse = Mathf.Clamp01(Vector3.Dot(normal, input.FillLightDirection)) * (0.28f * fillStrength);
					Vector3 reflected = Vector3.Reflect(-input.KeyLightDirection, normal);
					float specular = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(reflected, viewDirection)), 28f) * 0.28f * keyStrength;
					float rim = Mathf.Pow(1f - nz, 1.5f) * 0.12f;

					float shade = 0.18f + ambient * 0.35f + keyDiffuse + fillDiffuse + specular + rim;
					Color color = new Color(shade, shade, shade, 1f);
					texture.SetPixel(x, y, color);
				}
			}

			texture.Apply(false, false);
		}
	}
}
#endif
