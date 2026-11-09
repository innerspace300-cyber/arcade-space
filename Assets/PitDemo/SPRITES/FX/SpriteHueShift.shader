// ARcade: an unlit sprite with its hue turned and saturation scaled - each
// Slime Boss after the first comes in a new colour (EndlessKnightDirector).
Shader "SpatialEmulator/SpriteHueShift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Hue ("Hue turn (0-1)", Range(0, 1)) = 0
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 1
        _MaskHue ("Only colours near this hue (0-1)", Range(0, 1)) = 0
        _MaskWidth ("How near (0: every colour)", Range(0, 0.5)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Hue, _Saturation, _Brightness, _MaskHue, _MaskWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            // In HSV: the hue turned, saturation and brightness scaled.
            float3 Shift(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float3 hsv = float3(abs(q.z + (q.w - q.y) / (6.0 * d + 1e-10)), d / (q.x + 1e-10), q.x);
                // (masked: only the colours near _MaskHue - a viking's clothes, not his skin or beard)
                float w = 1.0;
                if (_MaskWidth > 0.0)
                {
                    float d = abs(frac(hsv.x - _MaskHue + 0.5) - 0.5);
                    w = saturate((_MaskWidth - d) / (_MaskWidth * 0.35)) * saturate(hsv.y * 4.0);
                }
                hsv.x = frac(hsv.x + _Hue * w);
                hsv.y = saturate(hsv.y * lerp(1.0, _Saturation, w));
                hsv.z = saturate(hsv.z * lerp(1.0, _Brightness, w));
                float3 rgb = saturate(abs(frac(hsv.x + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return hsv.z * lerp(float3(1, 1, 1), rgb, hsv.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb = Shift(c.rgb);
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
