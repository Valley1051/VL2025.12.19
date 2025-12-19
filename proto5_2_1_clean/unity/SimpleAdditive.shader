Shader "Custom/SimpleAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _FadeHeight ("Fade Height", Range(0, 1)) = 0.2
        _FadePower ("Fade Power", Range(0.1, 5)) = 1.0
        
        // SpriteMask support properties
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha One
        Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }

        // Stencil block controlled by SpriteRenderer
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

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
            fixed4 _Color;
            float _FadeHeight;
            float _FadePower;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.texcoord);
                
                // Vertical Gradient Alpha (Fade out bottom)
                // UV.y: 0 (Bottom) -> 1 (Top)
                // We want to fade out near 0.
                float fade = smoothstep(0.0, _FadeHeight, i.texcoord.y);
                fade = pow(fade, _FadePower);
                
                col.rgb *= fade;
                
                // 黒に近いほど透明になり、白に近いほど加算される
                // アルファチャンネルは無視してRGB値で加算合成を行う
                return col * i.color;
            }
            ENDCG
        }
    }
}
