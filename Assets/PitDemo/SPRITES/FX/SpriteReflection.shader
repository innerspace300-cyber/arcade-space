// ARcade: a character's reflection in still water (SpriteShadow, on a level
// with reflections - level 4): its sprite, flipped below its feet by the
// renderer, tinted and see-through, rippling side to side (more the further
// from its feet), fading out with depth. _Rect: the frame's place in its sheet
// (x: its bottom's v, y: its height in v), so the ripple and the fade run from
// its feet whatever sheet it's on. _Ripple: x its sway (texels), y how many
// waves down it, z how fast.
Shader "SpatialEmulator/SpriteReflection"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _Color ("Tint", Color) = (1, 1, 1, 0.5)
        [PerRendererData] _Rect ("Frame (v bottom, v height)", Vector) = (0, 1, 0, 0)
        _Ripple ("Ripple (texels, waves, speed)", Vector) = (1.2, 9, 3, 0)
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
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Rect;
                float4 _Ripple;
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

            half4 frag(Varyings i) : SV_Target
            {
                // How far up the frame from its feet (0) to its top (1): in the reflection, how deep.
                float k = saturate((i.uv.y - _Rect.x) / max(1e-5, _Rect.y));
                float2 uv = i.uv;
                uv.x += sin(k * _Ripple.y * 6.2832 + _Time.y * _Ripple.z) * _Ripple.x * _MainTex_TexelSize.x * (0.35 + k);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                c.rgb *= _Color.rgb;
                // (a little lighter in the water's sheen, fading out with depth)
                c.rgb = lerp(c.rgb, _Color.rgb, 0.25);
                c.a *= _Color.a * pow(1.0 - k, 1.4);
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
