Shader "Mictlan/UI/IrisMascara"
{
    // ════════════════════════════════════════════════════════════════
    //  Cortinilla de iris con silueta (estilo Mario).
    //
    //  La máscara (alfa de _MainTex) marca la ABERTURA: donde alfa = 1
    //  se ve el juego; donde alfa = 0, negro. La silueta se escala
    //  alrededor de _Centro (viewport) usando _Pivote (UV de la máscara)
    //  como punto fijo. _Escala = alto de la silueta / alto de pantalla.
    //
    //  _Escala muy grande → pantalla libre. _Escala = 0 → negro total.
    //  Borde de fuego: dilatación de la máscara por muestreo en anillo.
    //
    //  Se usa i.uv del RawImage a pantalla completa (0..1, origen abajo
    //  izquierda) en vez de coordenadas de pantalla: evita el volteo de
    //  Y entre D3D / Vulkan / OpenGL (Steam Deck).
    // ════════════════════════════════════════════════════════════════
    Properties
    {
        [PerRendererData] _MainTex ("Mascara (alfa = abertura)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Centro ("Centro (viewport)", Vector) = (0.5, 0.5, 0, 0)
        _Pivote ("Pivote en la mascara (UV)", Vector) = (0.5, 0.70, 0, 0)
        _Escala ("Escala", Float) = 12
        _Aspecto ("Aspecto de pantalla", Float) = 1.7777
        _ColorBorde ("Color del borde", Color) = (1, 0.3, 0.08, 1)
        _GrosorBorde ("Grosor del borde", Float) = 0.008
        _UsarCirculo ("Usar circulo (sin mascara)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _Centro;
            float4 _Pivote;
            float4 _ColorBorde;
            float  _Escala;
            float  _Aspecto;
            float  _GrosorBorde;
            float  _UsarCirculo;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // d: desplazamiento desde el centro, en unidades de "alto de pantalla".
            float Mascara (float2 d)
            {
                if (_UsarCirculo > 0.5)
                {
                    float r = _Escala * 0.5;
                    return 1.0 - smoothstep(r - 0.002, r + 0.002, length(d));
                }

                float2 uv = _Pivote.xy + d / max(_Escala, 1e-5);
                // Fuera de la textura = cerrado, aunque la textura venga en Repeat.
                float dentro = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).a * dentro;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                if (_Escala <= 0.0001)
                    return fixed4(0, 0, 0, 1);

                float2 d = i.uv - _Centro.xy;
                d.x *= _Aspecto;

                float m = Mascara(d);

                // Anillo interior (borde sólido)
                float g = _GrosorBorde;
                float q = g * 0.7071;
                float n = 0;
                n = max(n, Mascara(d + float2( g, 0)));
                n = max(n, Mascara(d + float2(-g, 0)));
                n = max(n, Mascara(d + float2(0,  g)));
                n = max(n, Mascara(d + float2(0, -g)));
                n = max(n, Mascara(d + float2( q,  q)));
                n = max(n, Mascara(d + float2(-q,  q)));
                n = max(n, Mascara(d + float2( q, -q)));
                n = max(n, Mascara(d + float2(-q, -q)));

                // Halo exterior tenue a doble distancia
                float g2 = g * 2.2;
                float h = 0;
                h = max(h, Mascara(d + float2( g2, 0)));
                h = max(h, Mascara(d + float2(-g2, 0)));
                h = max(h, Mascara(d + float2(0,  g2)));
                h = max(h, Mascara(d + float2(0, -g2)));

                float borde = saturate(n - m);
                float halo  = saturate(h - m) * 0.4;
                float b = max(borde, halo);

                float alfa = saturate(max(1.0 - m, b));
                return fixed4(_ColorBorde.rgb * b, alfa);
            }
            ENDCG
        }
    }
}
