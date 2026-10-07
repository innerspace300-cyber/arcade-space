// Flat-colored overlay for the cabinet's move gizmo: drawn after everything
// else and never depth-tested, so the arrows stay visible through the
// cabinet's layers and the real room. Faces turned away from the camera are
// shaded darker so the arrows still read as 3D.
Shader "SpatialEmulator/GizmoOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Back
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; fixed shade : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float3 toCamera = normalize(WorldSpaceViewDir(v.vertex));
                o.shade = 0.55 + 0.45 * saturate(dot(n, toCamera));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_Color.rgb * i.shade, _Color.a);
            }
            ENDCG
        }
    }
}
