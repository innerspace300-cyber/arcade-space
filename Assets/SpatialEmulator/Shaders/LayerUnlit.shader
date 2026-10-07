// Unlit, alpha-blended, double-sided layer shader.
// MAME's bitmaps are top-down while Unity textures are bottom-up, so V is
// flipped here instead of per-material tiling tricks. _FlipV = 0 shows a
// texture as-is (the built-in demo's render textures).
// ARcade's CRT filter (_Crt = 1; CrtEffect), as on an arcade monitor: the
// picture bent by the tube's glass with rounded corners, dark lines between
// the game's pixel rows, a red-green-blue stripe across each pixel, red and
// blue a touch apart, a glow, dark corners and a faint flicker.
Shader "SpatialEmulator/LayerUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
        _FlipV ("Flip V", Float) = 1
        _Crt ("CRT filter", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _FlipV;
            float _Crt;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, lerp(v.uv.y, 1.0 - v.uv.y, _FlipV));
                return o;
            }

            // 0-1, bright through each line's middle: `lines` counts them
            // across, `px` is screen pixels per line; under `least`, two,
            // four... lines go together into one (eased between, no pop).
            float Lines(float lines, float px, float least)
            {
                float level = max(0.0, log2(least / px));
                float k = exp2(floor(level));
                float a = pow(sin(frac(lines / k) * 3.14159), 3.0);
                float b = pow(sin(frac(lines / (k * 2.0)) * 3.14159), 3.0);
                return lerp(a, b, frac(level));
            }

            float3 Stripe(float col)
            {
                col = frac(col / 3.0) * 3.0;
                return col < 1.0 ? float3(1.5, 0.6, 0.6) : col < 2.0 ? float3(0.6, 1.5, 0.6) : float3(0.6, 0.6, 1.5);
            }

            float3 Grille(float stripes, float px)
            {
                float level = max(0.0, log2(2.0 / px));
                float k = exp2(floor(level));
                return lerp(Stripe(stripes / k), Stripe(stripes / (k * 2.0)), frac(level));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Crt < 0.5) return tex2D(_MainTex, i.uv);

                // The tube's glass bends the picture: pushed out toward the
                // corners (every layer the same, so they stay lined up), and
                // nothing past its rounded edge.
                float2 d = i.uv * 2.0 - 1.0;
                d *= 1.0 + 0.11 * (d.yx * d.yx);
                float2 uv = d * 0.5 + 0.5;
                // How many screen pixels a game pixel row (and a grille
                // stripe) covers here - taken before anything returns.
                float rows = uv.y * _MainTex_TexelSize.w, stripes = uv.x * _MainTex_TexelSize.z * 3.0;
                float rowPx = 1.0 / max(fwidth(rows), 1e-4), stripePx = 1.0 / max(fwidth(stripes), 1e-4);
                if (any(uv < 0.0) || any(uv > 1.0)) return fixed4(0, 0, 0, 0);
                float2 q = max(abs(d) - 0.86, 0.0) / 0.14;
                float rounded = 1.0 - smoothstep(0.75, 1.0, length(q));

                // Nothing here (most of an upper layer): done, without the
                // filter's eight more reads.
                fixed4 c = tex2D(_MainTex, uv);
                if (c.a < 0.004) return fixed4(0, 0, 0, 0);

                // Red and blue a little apart (the guns' convergence), and a glow round bright pixels.
                float2 texel = _MainTex_TexelSize.xy;
                c.r = tex2D(_MainTex, uv + float2(texel.x * 0.7, 0)).r;
                c.b = tex2D(_MainTex, uv - float2(texel.x * 0.7, 0)).b;
                fixed3 glow = (tex2D(_MainTex, uv + float2(texel.x * 1.5, 0)).rgb + tex2D(_MainTex, uv - float2(texel.x * 1.5, 0)).rgb
                    + tex2D(_MainTex, uv + float2(0, texel.y * 1.5)).rgb + tex2D(_MainTex, uv - float2(0, texel.y * 1.5)).rgb) * 0.25;

                // Each pixel row bright through its middle, a wide dark gap
                // between rows. Too far off for a row to be drawn (under 3
                // screen pixels), rows go together in twos, fours... so the
                // lines show at any distance - fewer and coarser, never gone.
                float scan = lerp(0.08, 1.45, Lines(rows, rowPx, 3.0));
                // An aperture grille: each pixel a red, a green and a blue
                // stripe (grouped the same way, too small to draw).
                float3 mask = Grille(stripes, stripePx);
                // The corners dark, and a faint flicker.
                float vignette = saturate(1.0 - 0.45 * (d.x * d.x * d.x * d.x + d.y * d.y * d.y * d.y));
                float flicker = 1.0 + 0.025 * sin(_Time.y * 90.0);

                c.rgb = (c.rgb * scan * mask + glow * 0.3) * vignette * flicker * 1.45;
                c.a *= rounded;
                return c;
            }
            ENDCG
        }
    }
}
