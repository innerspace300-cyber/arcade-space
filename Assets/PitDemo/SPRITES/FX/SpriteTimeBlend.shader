// ARcade: level 3's frog arena's ways in and out - each piece of bamboo, and the road under them, its arena's
// time of day by the courtyard, fading out along the way into the walk's own look (the same art in the walk's
// time: _AltTex the walk she came by, left of it; _AltTexR the one she leaves by, right), by how far out from
// the arena it stands (BossArena: _CenterX its middle, _HalfWidth the courtyard's, _Reach the ways'; the fade
// from _Start of the way out, over _Span of it; below 0: on out past the ways, into the walk's far layers - _Sway 1 the
// swaying bamboo's, bent in the wind as SpriteSway).
Shader "SpatialEmulator/SpriteTimeBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (the arena's time)", 2D) = "white" {}
        [PerRendererData] _AltTex ("Same art, the walk's time (left)", 2D) = "white" {}
        [PerRendererData] _AltTexR ("Same art, the walk's time (right)", 2D) = "white" {}
        _CenterX ("Arena middle (world x)", Float) = 0
        _HalfWidth ("Courtyard half width", Float) = 1
        _Reach ("Ways' reach", Float) = 1
        _Start ("Fade from (of the way, from the courtyard)", Float) = 0.3
        _Span ("Fade over", Float) = 0.3
        _Sway ("Bends in the wind (SpriteSway's)", Float) = 0
        _StartR ("Way out: fade from", Float) = 0.3
        _SpanR ("Way out: fade over", Float) = 0.3
        _PeakR ("Way out: the arena's look at most past its edge", Float) = 1
        _NearR ("Way out: down to that from the edge over", Float) = 0.0001
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
            TEXTURE2D(_AltTex);
            SAMPLER(sampler_AltTex);
            TEXTURE2D(_AltTexR);
            SAMPLER(sampler_AltTexR);
            CBUFFER_START(UnityPerMaterial)
                float _CenterX, _HalfWidth, _Reach, _Start, _Span, _Sway, _StartR, _SpanR, _PeakR, _NearR;
            CBUFFER_END
            float _BambooSway, _SwayBase, _SwayHeight;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float worldX : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.worldX = world.x;
                float bend = saturate((world.y - _SwayBase) / max(_SwayHeight, 1e-4));
                world.x += _BambooSway * bend * bend * _Sway;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 arena = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 walk = i.worldX < _CenterX ? SAMPLE_TEXTURE2D(_AltTex, sampler_AltTex, i.uv) : SAMPLE_TEXTURE2D(_AltTexR, sampler_AltTexR, i.uv);
                // u: 1 at the courtyard's edge, 0 at the ways' far end (and on out, the walk's look).
                float u = 1.0 - (abs(i.worldX - _CenterX) - _HalfWidth) / max(_Reach, 1e-4);
                bool left = i.worldX < _CenterX;   // (the way out, right, its own fade: BossArena.BlendWays)
                float k = saturate((u - (left ? _Start : _StartR)) / max(left ? _Span : _SpanR, 1e-4));
                k = k * k * (3.0 - 2.0 * k);
                if (!left)
                {
                    // The way out: only _PeakR of the arena's look along its long fade, from all of it at the edge over _NearR.
                    float n = saturate((u - 1.0 + _NearR) / max(_NearR, 1e-4));
                    k = max(k * _PeakR, n * n * (3.0 - 2.0 * n));
                }
                half4 c = lerp(walk, arena, k) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
