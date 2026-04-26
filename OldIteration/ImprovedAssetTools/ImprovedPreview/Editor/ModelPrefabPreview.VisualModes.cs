#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace FardinHaque.ImprovedAssetTools.Editor
{

public partial class ModelPrefabPreview
{
	private void ToggleVisualMode(PreviewVisualMode mode)
	{
		_visualMode = (_visualMode == mode) ? PreviewVisualMode.None : mode;
		if (_visualMode != PreviewVisualMode.None)
			_lastVisualMode = _visualMode;
		RequestPreviewRepaint();
	}

	private void GetVisualModeButtonContent(out string label, out string tooltip, out string[] icons)
	{
		PreviewVisualMode displayMode = _visualMode != PreviewVisualMode.None ? _visualMode : _lastVisualMode;
		switch (displayMode)
		{
			case PreviewVisualMode.Normals:
				label = "NM"; tooltip = "Normals"; icons = new[] { "d_Mesh Icon", "Mesh Icon", "d_PreMatSphere", "PreMatSphere" }; break;
			case PreviewVisualMode.UvChecker:
				label = "UV"; tooltip = "UV Checker"; icons = new[] { "d_PreTextureRGB", "PreTextureRGB", "d_RawImage Icon", "RawImage Icon" }; break;
			case PreviewVisualMode.VertexColor:
				label = "VC"; tooltip = "Vertex Colors"; icons = new[] { "d_ColorPicker.CycleSlider", "ColorPicker.CycleSlider", "d_PreMatSphere", "PreMatSphere" }; break;
			case PreviewVisualMode.Overdraw:
				label = "OD"; tooltip = "Overdraw"; icons = new[] { "d_Profiler.Rendering", "Profiler.Rendering", "d_SceneViewFx", "SceneViewFx" }; break;
			default:
				label = "NM"; tooltip = "Visual mode"; icons = new[] { "d_SceneViewFx", "SceneViewFx" }; break;
		}
	}

	private Material GetVisualModeMaterial()
	{
		switch (_visualMode)
		{
			case PreviewVisualMode.Normals:
				return _normalsMat ??= CreateNormalsMaterial();
			case PreviewVisualMode.UvChecker:
				return _uvCheckerMat ??= CreateUvCheckerMaterial();
			case PreviewVisualMode.VertexColor:
				return _vertexColorMat ??= CreateVertexColorMaterial();
			case PreviewVisualMode.Overdraw:
				return _overdrawMat ??= CreateOverdrawMaterial();
			default:
				return null;
		}
	}

	private void SwapMaterials(Material replacement)
	{
		_savedMaterials.Clear();
		foreach (var r in _renderers)
		{
			if (r == null) continue;
			_savedMaterials.Add(r.sharedMaterials);
			Material[] mats = new Material[r.sharedMaterials.Length];
			for (int i = 0; i < mats.Length; i++)
				mats[i] = replacement;
			r.sharedMaterials = mats;
		}
	}

	private void RestoreMaterials()
	{
		int idx = 0;
		foreach (var r in _renderers)
		{
			if (r == null) continue;
			if (idx < _savedMaterials.Count)
				r.sharedMaterials = _savedMaterials[idx];
			idx++;
		}
		_savedMaterials.Clear();
	}

	private static void DestroyMaterial(ref Material mat)
	{
		if (mat != null)
			UnityEngine.Object.DestroyImmediate(mat);
		mat = null;
	}

	private static Material CreateNormalsMaterial()
	{
		const string src = @"
Shader ""Hidden/PreviewNormals""
{
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
		Pass
		{
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
			struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
			struct Varyings  { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				return o;
			}
			half4 frag(Varyings i) : SV_Target
			{
				float3 n = normalize(i.normalWS);
				return half4(n * 0.5 + 0.5, 1.0);
			}
			ENDHLSL
		}
	}
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" }
		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
			struct v2f     { float4 pos : SV_POSITION; float3 worldNormal : TEXCOORD0; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				return o;
			}
			half4 frag(v2f i) : SV_Target
			{
				float3 n = normalize(i.worldNormal);
				return half4(n * 0.5 + 0.5, 1.0);
			}
			ENDCG
		}
	}
}";
		return CreateMaterialFromSource(src);
	}

	private static Material CreateUvCheckerMaterial()
	{
		const string src = @"
Shader ""Hidden/PreviewUvChecker""
{
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
		Pass
		{
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
			struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
			struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.uv = v.uv;
				return o;
			}
			half4 frag(Varyings i) : SV_Target
			{
				float2 uv = i.uv;
				float coarse = fmod(floor(uv.x * 8.0) + floor(uv.y * 8.0), 2.0);
				float fine   = fmod(floor(uv.x * 64.0) + floor(uv.y * 64.0), 2.0);
				float3 color = lerp(float3(0.15, 0.15, 0.18), float3(0.85, 0.85, 0.82), coarse);
				color *= lerp(0.92, 1.0, fine);
				return half4(color, 1.0);
			}
			ENDHLSL
		}
	}
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" }
		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
			struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			half4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv;
				float coarse = fmod(floor(uv.x * 8.0) + floor(uv.y * 8.0), 2.0);
				float fine   = fmod(floor(uv.x * 64.0) + floor(uv.y * 64.0), 2.0);
				float3 color = lerp(float3(0.15, 0.15, 0.18), float3(0.85, 0.85, 0.82), coarse);
				color *= lerp(0.92, 1.0, fine);
				return half4(color, 1.0);
			}
			ENDCG
		}
	}
}";
		return CreateMaterialFromSource(src);
	}

	private static Material CreateVertexColorMaterial()
	{
		const string src = @"
Shader ""Hidden/PreviewVertexColor""
{
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" ""RenderPipeline""=""UniversalPipeline"" }
		Pass
		{
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
			struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
			struct Varyings  { float4 positionCS : SV_POSITION; float4 color : COLOR; };
			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.color = v.color;
				return o;
			}
			half4 frag(Varyings i) : SV_Target
			{
				return i.color;
			}
			ENDHLSL
		}
	}
	SubShader
	{
		Tags { ""RenderType""=""Opaque"" }
		Pass
		{
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; float4 color : COLOR; };
			struct v2f     { float4 pos : SV_POSITION; float4 color : COLOR; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				return o;
			}
			half4 frag(v2f i) : SV_Target
			{
				return i.color;
			}
			ENDCG
		}
	}
}";
		return CreateMaterialFromSource(src);
	}

	private static Material CreateOverdrawMaterial()
	{
		const string src = @"
Shader ""Hidden/PreviewOverdraw""
{
	SubShader
	{
		Tags { ""RenderType""=""Transparent"" ""RenderPipeline""=""UniversalPipeline"" ""Queue""=""Transparent"" }
		Pass
		{
			Blend One One
			ZWrite Off
			ZTest Always
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
			struct Attributes { float4 positionOS : POSITION; };
			struct Varyings  { float4 positionCS : SV_POSITION; };
			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				return o;
			}
			half4 frag(Varyings i) : SV_Target
			{
				return half4(0.1, 0.04, 0.02, 0.0);
			}
			ENDHLSL
		}
	}
	SubShader
	{
		Tags { ""RenderType""=""Transparent"" ""Queue""=""Transparent"" }
		Pass
		{
			Blend One One
			ZWrite Off
			ZTest Always
			Cull Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include ""UnityCG.cginc""
			struct appdata { float4 vertex : POSITION; };
			struct v2f     { float4 pos : SV_POSITION; };
			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}
			half4 frag(v2f i) : SV_Target
			{
				return half4(0.1, 0.04, 0.02, 0.0);
			}
			ENDCG
		}
	}
}";
		return CreateMaterialFromSource(src);
	}

	private static Material CreateMaterialFromSource(string shaderSource)
	{
		Shader shader = ShaderUtil.CreateShaderAsset(shaderSource, false);
		if (shader == null)
			return null;

		shader.hideFlags = HideFlags.HideAndDontSave;
		Material mat = new Material(shader)
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		return mat;
	}
}

}
#endif