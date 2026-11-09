// ARcade: a sprite's shape multiplying what's already drawn by one colour
// (_Color, set per renderer with a property block) - laid just over the
// knight (whose own Shader Graph material ignores the renderer's colour),
// it puts her in the level's light exactly as the others' colours are
// multiplied (LevelLit): darker and cooler at night, not tinted.
Shader "SpatialEmulator/SpriteMultiply"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _Color ("Light", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend DstColor Zero

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                // Where she's drawn, the light; elsewhere (and her soft edges) leave it be.
                return half4(lerp(half3(1, 1, 1), _Color.rgb, a), 1);
            }
            ENDHLSL
        }
    }
}
