Shader "Hidden/PrefabPreview/UvChecker"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float coarse = fmod(floor(i.uv.x * 8.0) + floor(i.uv.y * 8.0), 2.0);
                float fine = fmod(floor(i.uv.x * 64.0) + floor(i.uv.y * 64.0), 2.0);
                float3 color = lerp(float3(0.15, 0.15, 0.18), float3(0.85, 0.85, 0.82), coarse);
                color *= lerp(0.92, 1.0, fine);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
