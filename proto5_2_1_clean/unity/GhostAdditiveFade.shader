Shader "Custom/GhostAdditiveFade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _FadeHeight ("Fade Height (from Bottom)", Range(0, 1)) = 0.2
        _MinLuminance ("Min Luminance (Black Clip)", Range(0, 1)) = 0.15
    }
    SubShader
    {
        // Render Queue: Transparent+1 to ensure it draws after background
        Tags { "Queue"="Transparent+1" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        // Additive Blend: Prevents graying out, adds light
        Blend SrcAlpha One 
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _FadeHeight;
            float _MinLuminance;
            
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _TintColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Sample texture
                fixed4 texCol = tex2D(_MainTex, i.uv);
                
                // 1. Calculate Luminance (Force B&W mask behavior)
                float lum = dot(texCol.rgb, float3(0.299, 0.587, 0.114));
                
                // 2. Black Clipping (Hard Cutoff to remove background noise/gray rect)
                // If luminance is below threshold, alpha becomes 0.
                float alpha = (lum < _MinLuminance) ? 0.0 : lum;

                // 3. Vertical Fade (Bottom to Top)
                float fade = smoothstep(0.0, _FadeHeight, i.uv.y);
                alpha *= fade;
                
                // 4. Pure White Logic
                // Ignore texture color. Use _TintColor * Alpha.
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _TintColor);
                
                // Output RGB = Tint * Alpha
                // Output Alpha = Alpha
                return fixed4(tint.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
