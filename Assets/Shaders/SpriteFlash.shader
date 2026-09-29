Shader "Mictlan/SpriteFlash"
{
    // ============================================================
    //  Mictlan/SpriteFlash
    // ============================================================
    //  Sprite estándar + destello de color sólido + vibración de
    //  vértices. Lo usa ImpactoDano: durante un impacto intercambia
    //  temporalmente el material de Romerito (y del Macahuitl) por
    //  uno con este shader y lo anima con MaterialPropertyBlock.
    //
    //  • _FlashAmount 0..1  → mezcla hacia _FlashColor. Respeta el
    //    alfa del SpriteRenderer.color: el parpadeo semitransparente
    //    de los i-frames sigue funcionando encima del destello.
    //  • _ShakeOffset.xy    → desplazamiento en espacio de objeto.
    //    Solo mueve lo que se dibuja: colliders y hurtbox no se mueven
    //    (como hace Sakurai en Smash).
    //
    //  Basado en Sprites-Default (UnitySprites.cginc): conserva flip,
    //  atlas, instancing, PixelSnap y alfa externo de ETC1.
    // ============================================================

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Color del destello", Color) = (1,1,1,1)
        _FlashAmount ("Cantidad de destello", Range(0,1)) = 0
        _ShakeOffset ("Vibración (espacio de objeto)", Vector) = (0,0,0,0)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _FlashColor;
            float  _FlashAmount;
            float4 _ShakeOffset;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex.xy += _ShakeOffset.xy;
                OUT.vertex = UnityObjectToClipPos(OUT.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                fixed4 c;
                // Color normal (textura × tinte) mezclado hacia el destello.
                c.rgb = lerp(tex.rgb * IN.color.rgb, _FlashColor.rgb, _FlashAmount);
                // El alfa siempre sale del sprite y del SpriteRenderer.color,
                // para respetar la silueta y el parpadeo de i-frames.
                c.a = tex.a * IN.color.a;
                c.rgb *= c.a;   // premultiplicado (Blend One OneMinusSrcAlpha)
                return c;
            }
        ENDCG
        }
    }
}
