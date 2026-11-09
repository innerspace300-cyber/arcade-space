// THE TIME CRYSTAL: a sprite's silhouette in one solid colour (the renderer's: white for a hit flash), premultiplied like Sprites/Default and
// built on its vertex stage (UnitySprites.cginc: the renderer's colour - its fading alpha - and flips
// both reach the shader that way) - laid over the player's ship and portrait as a hit flash (TcDamageFlash), and the warp's
// trail of copies in the pilot's colour (TcWarpTrail).
Shader "TimeCrystal/WhiteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            fixed4 frag (v2f IN) : SV_Target
            {
                fixed a = SampleSpriteTexture(IN.texcoord).a * IN.color.a;
                return fixed4(IN.color.rgb * a, a);
            }
            ENDCG
        }
    }
}
