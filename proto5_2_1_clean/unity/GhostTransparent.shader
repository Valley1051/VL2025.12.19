Shader "Custom/GhostTransparent"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {} 
        _TintColor ("Tint Color", Color) = (0,0,0,0) // Default to invisible if no color set
        _Cutoff ("Luma Cutoff", Range(0, 1)) = 0.1
        _Intensity ("Intensity", Range(0, 5)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha One // Additive Blending
        Cull Off 
        Lighting Off 
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float _Cutoff;
            float _Intensity;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _TintColor;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                
                // Luma calculation (perceived brightness)
                float luma = dot(col.rgb, float3(0.299, 0.587, 0.114));
                
                // Luma Key: If too dark, make transparent
                if (luma < _Cutoff)
                {
                    col.a = 0;
                }
                
                // [New] Gradient Alpha Fade (Feet fade)
                // UV.y 0.0 ~ 0.2 fade out
                float alphaFade = smoothstep(0.0, 0.2, i.texcoord.y);
                col.a *= alphaFade;
                
                // Apply Tint and Intensity
                col.rgb *= i.color.rgb * _Intensity;
                col.a *= i.color.a;
                
                return col;
            }
            ENDCG
        }
    }
}
