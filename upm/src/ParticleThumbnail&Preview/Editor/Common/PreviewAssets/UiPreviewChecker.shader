Shader "Hidden/PrefabPreview/UiChecker"
{
    Properties
    {
        _MainTex ("Checker Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.30, 0.30, 0.30, 0.20)
        _BackgroundColor ("Background Color", Color) = (0.10, 0.10, 0.10, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _BackgroundColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float opacity = saturate(_Color.a);
                fixed3 tinted = tex.rgb * _Color.rgb;
                fixed3 blended = lerp(_BackgroundColor.rgb, tinted, opacity);
                return fixed4(blended, 1);
            }
            ENDCG
        }
    }
}
