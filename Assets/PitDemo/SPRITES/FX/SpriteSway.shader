// ARcade: a sprite bending in the wind - each of its points pushed sideways
// by _BambooSway (world units, set each frame by ParallaxLevelBackground),
// none at _SwayBase (its roots, on the ground) and more the higher it is, up
// to the full sway _SwayHeight above that: level 3's black bamboo swaying in
// time with its shadows on the ground.
Shader "SpatialEmulator/SpriteSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _BambooSway, _SwayBase, _SwayHeight;

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float k = saturate((world.y - _SwayBase) / max(_SwayHeight, 1e-4));
                world.x += _BambooSway * k * k;   // (bending: the tops most)
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
            }
            ENDHLSL
        }
    }
}
