Shader "Mictlan/SpriteGlowHDR"
{
    // ============================================================
    //  Mictlan/SpriteGlowHDR
    // ============================================================
    //  Shader simple para sprites que deben "brillar" de verdad con
    //  Post Processing Stack v2 + Bloom (Built-in Render Pipeline).
    //
    //  El _Color expuesto como [HDR] permite valores > 1.0 en el
    //  Inspector del Material (el picker cambia a modo HDR con
    //  control de Intensity). Esos valores superan el Threshold del
    //  Bloom y generan el brillo real, en vez de solo "verse claro".
    //
    //  LIMITACIÓN A PROPÓSITO: no soporta flip de sprite ni atlas
    //  (no lo necesita un VFX suelto como el orbe de Tonalli). Si
    //  más adelante lo quieres usar en algo con flip direccional,
    //  se extiende sin problema.
    //
    //  USO:
    //    1. Crea un Material nuevo, asígnale este shader.
    //    2. En el campo "Color y Brillo (HDR)", sube la Intensity
    //       hasta que se vea brillante (ej. ámbar ~3x el color base
    //       de TonalliSystem.colorTonalli).
    //    3. Asigna el Material al SpriteRenderer del orbe.
    // ============================================================

    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Color y Brillo (HDR)", Color) = (1,1,1,1)
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                // _Color puede exceder 1.0 (HDR) — eso es lo que activa el Bloom
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, IN.texcoord);
                return texColor * IN.color;
            }
            ENDCG
        }
    }
}
