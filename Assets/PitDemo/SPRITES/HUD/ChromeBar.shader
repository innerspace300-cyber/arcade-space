// ARcade: ENDLESS KNIGHT's health bar as rainbow liquid chrome - a UI image's white
// bar drawn as polished metal: a rainbow flowing along it (rippling like a liquid), a
// chrome profile top to bottom (sky-bright top, a dark horizon line, a lit lower
// reflection), the art's own darker pixels kept, a glint sweeping across now and then,
// all snapped to the art's pixels and a few colour steps. ChromeBar drives it.
Shader "SpatialEmulator/ChromeBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Rect ("Sprite's rect in its texture (uv: x, y, w, h)", Vector) = (0,0,1,1)
        _Phase ("Flow (ChromeBar)", Float) = 0
        _Glint ("Glint across (0-1, outside: none)", Float) = -1
        _Steps ("Colour steps", Float) = 7
        _Bright ("Brightness (a dimmer bar behind)", Float) = 1
        _HueShift ("Hue turn", Float) = 0
        _HueRange ("How much of the rainbow (1: all)", Float) = 1
        _OnlyPale ("Only its pale pixels (1)", Float) = 0
        _Bar ("The whole bar, in its canvas (x, width, one art pixel; 0: by its sprite)", Vector) = (0,0,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _Rect, _Bar;
            float _Phase, _Glint, _Steps, _Bright, _HueShift, _HueRange, _OnlyPale;
            float4 _ClipRect;

            v2f vert(appdata v)
            {
                v2f o;
                o.world = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float3 Rainbow(float h)
            {
                float3 c = saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
                return lerp(1, c, 0.8);   // (pastel, like light on chrome)
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 art = tex2D(_MainTex, i.uv);
                // Where in the bar, in its own pixels (snapped: everything moves pixel by pixel).
                float2 size = _Rect.zw * _MainTex_TexelSize.zw;
                float2 p = floor((i.uv - _Rect.xy) * _MainTex_TexelSize.zw) + 0.5;
                float v = p.y / max(size.y, 1);
                // Along the whole bar (a tiled bar repeats its sprite: one liquid all the same).
                float bx = p.x, bw = size.x;
                if (_Bar.z > 0)
                {
                    bx = floor((i.world.x - _Bar.x) / _Bar.z) + 0.5;
                    bw = _Bar.y / _Bar.z;
                    // (its shading from one column of the art: the sprite's segment lines left out, its ends kept)
                    if (p.x > 2 && p.x < size.x - 2)
                        art.rgb = tex2D(_MainTex, float2(_Rect.x + 8.5 * _MainTex_TexelSize.x, i.uv.y)).rgb;
                }

                // The rainbow flowing along, rippling: one sweep of it across the bar.
                float h = bx / max(bw, 1) * 0.9 - p.y * 0.02 + _Phase * 0.12 + sin(bx * 0.25 + _Phase * 2.3) * 0.03;
                // (part of the rainbow: back and forth through it, not jumping round)
                if (_HueRange < 1) h = abs(frac(h * 0.5) * 2 - 1) * _HueRange;
                float3 hue = Rainbow(h + _HueShift);

                // Chrome, top to bottom: bright sky, the colour, a dark horizon, a lit reflection below.
                float3 c;
                if (v > 0.72) c = lerp(hue, 1, 0.55 + (v - 0.72) * 1.2);
                else if (v > 0.48) c = hue;
                else if (v > 0.40) c = hue * 0.6;
                else c = hue * lerp(1.0, 0.85, v / 0.40);

                // The art's own shading (its seams and edge) kept.
                float lum = dot(art.rgb, float3(0.3, 0.59, 0.11));
                c *= lerp(0.6, 1.05, saturate((lum - 0.5) / 0.4));

                // The glint: a slanted streak of white sweeping across.
                float g = bx - (_Glint * (bw + 16) - 8) + (p.y - size.y * 0.5) * 0.7;
                c += step(abs(g), 1.2) * 0.75 + step(abs(g - 2.5), 0.6) * 0.35;

                c *= _Bright;
                c = floor(saturate(c) * _Steps + 0.5) / _Steps;
                // (just the white of an animation's art: its gold glass and frame left as they are)
                float pale = 1;
                if (_OnlyPale > 0.5)
                {
                    float sat = max(art.r, max(art.g, art.b)) - min(art.r, min(art.g, art.b));
                    pale = step(0.6, lum) * step(sat, 0.2);
                }
                fixed4 o = fixed4(lerp(art.rgb, c, pale), art.a) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                o.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return o;
            }
            ENDCG
        }
    }
}
