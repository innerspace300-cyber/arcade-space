// PlaneGrid.shader — the detected planes while the room's being scanned: thin neon grid lines (Settings' colour: PlaneGridFrame) with a soft glow
// round them, drawn additively over the camera, and a slow pulse rolling out from each plane's middle. One grid for
// the whole room (PlaneGridFrame squares it up with the walls), so floor lines carry on up the walls. The second
// material on the plane prefab (ShadowReceiver, the first, still occludes and takes the cabinet's shadow).
// _PlaneAlpha is faded by ARPlaneMeshVisualizerFader; uv2 feathers the plane's edge (ARFeatheredPlaneMeshVisualizer).

Shader "SpatialEmulator/PlaneGrid"
{
    Properties
    {
        _Cell ("Cell Size (metres)", Float) = 0.15
        _LineWidth ("Line Half Width (metres)", Float) = 0.0035
        _PlaneAlpha ("Plane Alpha", Float) = 1
        _ShortestUVMapping ("Shortest UV Mapping", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 plane : TEXCOORD0;
                float3 uv2 : TEXCOORD1;
                float3 world : TEXCOORD2;
                float3 normal : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float _Cell;
                float _LineWidth;
                float _PlaneAlpha;
                float _ShortestUVMapping;
            CBUFFER_END

            // From PlaneGridFrame: xy the grid's first axis (flat on the floor), zw where its lines start; the floor's height.
            float4 _GridFrame;
            float _GridFloorY;
            // From PlaneGridFrame too: the chosen neon's bloom and line.
            float4 _GridGlowColor;
            float4 _GridLineColor;
            // NEON RGB (1): a rainbow flowing across the room instead, like ENDLESS KNIGHT's health bar.
            float _GridRainbow;
            // The single neons (PlaneGridFrame.Shades): five shades and neighbouring hues of the colour that a
            // faster wave loops through, so the colour lives like NEON RGB's but stays itself.
            float4 _GridShades[5];

            float3 Rainbow(float h)
            {
                return saturate(abs(frac(h + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3) - 1);
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.plane = v.vertex.xz;   // (metres across the plane: its mesh lies flat in its own space)
                o.uv2 = v.uv2;
                o.world = TransformObjectToWorld(v.vertex.xyz);
                o.normal = TransformObjectToWorldDir(float3(0, 1, 0));   // (a plane's normal: its mesh's up)
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // How far to the nearest line, in metres.
                // The room's grid, not the plane's: on a floor or ceiling its two flat axes, on a wall height and the
                // flat axis running along that wall.
                float2 axisU = _GridFrame.xy, axisV = float2(-axisU.y, axisU.x);
                float2 flat = i.world.xz;
                float u = dot(flat, axisU) - _GridFrame.z, v = dot(flat, axisV) - _GridFrame.w;
                float height = i.world.y - _GridFloorY;
                float2 room;
                if (abs(i.normal.y) > 0.7) room = float2(u, v);
                else room = float2(abs(dot(i.normal.xz, axisU)) > abs(dot(i.normal.xz, axisV)) ? v : u, height);
                float2 cell = abs(frac(room / _Cell - 0.5) - 0.5) * _Cell;
                float near = min(cell.x, cell.y);
                float pixel = fwidth(near);
                // A hard core, never under a pixel wide, thinning out into the distance rather than shimmering.
                float width = max(_LineWidth, pixel * 0.7);
                float core = (1 - smoothstep(width, width + pixel, near)) / (1 + pixel * 45);
                // Two falloffs make the bloom: tight and bright, then wide and faint.
                float glow = exp(-near / 0.01) * 0.6 + exp(-near / 0.03) * 0.16;
                // A slow pulse rolling outward.
                float pulse = 0.8 + 0.5 * pow(saturate(sin(length(i.plane) * 5 - _Time.y * 2.5) * 0.5 + 0.5), 6);
                float3 glowColour, lineColour;
                // (one sweep every couple of metres, drifting slowly through the room)
                float wave = frac((room.x + room.y) * 0.4 + height * 0.25 - _Time.y * 0.4);
                if (_GridRainbow > 0.5)
                {
                    glowColour = Rainbow(wave);
                    lineColour = lerp(glowColour, 1, 0.55);
                }
                else
                {
                    // Tighter and about twice as fast as the rainbow, round the five shades and back to the first.
                    float t = frac((room.x + room.y) * 0.6 + height * 0.4 - _Time.y * 0.85) * 5;
                    uint k = min((uint)t, 4u);
                    float f = smoothstep(0, 1, frac(t));
                    glowColour = lerp(_GridShades[k].rgb, _GridShades[k == 4u ? 0u : k + 1u].rgb, f);
                    lineColour = lerp(glowColour, 1, 0.45);
                }
                float3 colour = glowColour * glow * pulse + lineColour * core * pulse;
                // Fading to nothing at the plane's edge (the template's feather), and with the fader.
                float feather = 1 - smoothstep(1, max(_ShortestUVMapping, 1.01), i.uv2.x);
                return half4(colour * feather * _PlaneAlpha, 0);
            }
            ENDHLSL
        }
    }
}
