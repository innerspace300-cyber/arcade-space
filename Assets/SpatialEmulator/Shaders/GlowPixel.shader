// GlowPixel — the cabinet slam's splash pixels: a white square core with a
// glow round it in the particle's colour (the room grid's), added over the
// camera's picture (the AR camera has no bloom). The texture's red channel
// marks the core; its alpha is the core plus the glow's falloff.
Shader "SpatialEmulator/GlowPixel"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _Glow ("Glow brightness", Float) = 2.5
        _Core ("Core brightness", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Glow;
                half _Core;
            CBUFFER_END

            // NEON RGB (PlaneGridFrame): each pixel's glow turns through the rainbow from its own hue.
            float _GridRainbow;

            float3 HueTurn(float3 c, float turn)
            {
                // (the colour's hue, turned: a full-strength neon in, a full-strength neon out)
                float mx = max(c.r, max(c.g, c.b)), mn = min(c.r, min(c.g, c.b)), d = max(mx - mn, 1e-4);
                float h = mx == c.r ? (c.g - c.b) / d : mx == c.g ? 2 + (c.b - c.r) / d : 4 + (c.r - c.g) / d;
                h = frac(h / 6 + turn);
                return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1) * mx;
            }

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 glow = _GridRainbow > 0.5 ? HueTurn(i.color.rgb, _Time.y * 0.6) : i.color.rgb;
                half3 c = lerp(glow * _Glow, half3(1, 1, 1) * _Core, t.r);
                half a = t.a * i.color.a;
                return half4(c * a, a);
            }
            ENDHLSL
        }
    }
}
