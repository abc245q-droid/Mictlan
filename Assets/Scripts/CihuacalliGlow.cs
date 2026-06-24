using UnityEngine;
using System.Collections;

// ============================================================
//  CihuacalliGlow — Brillo HDR + latido del Cihuacalli
// ============================================================
//
//  Anima la propiedad [HDR] _Color del shader Mictlan/SpriteGlowHDR
//  por MaterialPropertyBlock (cero GC, conserva el batching).
//
//  Dos formas de uso (toggle 'apagarConAlpha'):
//
//   • apagarConAlpha = FALSE  (por defecto — UN SOLO sprite, ej. el comal):
//       - Apagado  → _Color neutro (1,1,1,1): el sprite se ve normal (gris).
//       - Encendido→ se tiñe de ámbar y RESPIRA, sin volver nunca al gris.
//     Pon este componente directamente sobre el SpriteRenderer del comal,
//     con un Material que use Mictlan/SpriteGlowHDR.
//
//   • apagarConAlpha = TRUE  (capa de fuego SEPARADA encima de la piedra):
//       - Apagado  → alpha 0 (la llama desaparece).
//       - Encendido→ aparece (fade-in) y respira.
//     Pon esto sobre la capa "Llama"; la piedra va en otra capa estática.
//
//  IMPORTANTE: si usas el cambio de Sprite Apagado/Encendido del script
//  Cihuacalli sobre ESTE MISMO renderer, pelea con el glow. Elige uno:
//  o el glow tiñe (deja esos campos vacíos), o el swap (sin glow aquí).

[RequireComponent(typeof(SpriteRenderer))]
public class CihuacalliGlow : MonoBehaviour
{
    [Header("Modo")]
    [Tooltip("FALSE: un solo sprite (comal) — apagado se ve normal, encendido se tiñe. " +
             "TRUE: capa de fuego separada — apagado = invisible (alpha 0).")]
    public bool apagarConAlpha = false;

    [Header("Colores")]
    [Tooltip("Color del sprite cuando está APAGADO. Blanco = se ve tal cual (sin teñir).")]
    public Color colorApagado = Color.white;
    [Tooltip("Tono base del fuego cuando está ENCENDIDO (se multiplica por la intensidad).")]
    public Color colorFuego = new Color(1f, 0.55f, 0.18f);   // ámbar Mictlán

    [Header("Intensidad HDR (latido)")]
    [Tooltip("Multiplicador MÍNIMO del latido. Mantenlo > 1.4 (umbral del Bloom) para que " +
             "NUNCA se vea apagado mientras es el activo: el fuego siempre brilla, solo respira.")]
    public float intensidadMin = 1.6f;
    [Tooltip("Multiplicador máximo del latido.")]
    public float intensidadMax = 2.4f;
    [Tooltip("Velocidad del latido en ciclos por segundo (0.5 ≈ una respiración cada 2 s).")]
    public float velocidadLatido = 0.5f;

    [Header("Transición encender / apagar")]
    [Tooltip("Segundos que dura el fade al encender o apagar.")]
    public float duracionFade = 0.6f;
    [Tooltip("Curva del fade (suave por defecto).")]
    public AnimationCurve curvaFade = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Interno ──────────────────────────────────────────────
    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;
    private static readonly int ID_COLOR = Shader.PropertyToID("_Color");

    // Renderer sobre el que actúa el glow. Cihuacalli lo usa para que el
    // cambio de sprite (piedra/llama) ocurra en ESTE mismo renderer.
    public SpriteRenderer Renderer
    {
        get { if (sr == null) sr = GetComponent<SpriteRenderer>(); return sr; }
    }

    private Coroutine rutina;
    private bool encendido = false;
    private Color colorActual;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        colorActual = ColorApagadoFinal();
        AplicarColor(colorActual);
    }

    /// <summary>Enciende o apaga. 'instantaneo' = sin fade (al cargar escena).</summary>
    public void SetEncendido(bool on, bool instantaneo = false)
    {
        // Evita re-disparar el fade si ya está en el estado pedido
        // (cada activación dispara el evento global; sin esto, re-flamea).
        if (on == encendido && !instantaneo) return;

        encendido = on;
        if (rutina != null) StopCoroutine(rutina);

        if (instantaneo)
        {
            if (on) rutina = StartCoroutine(Latir());
            else { colorActual = ColorApagadoFinal(); AplicarColor(colorActual); }
            return;
        }

        rutina = StartCoroutine(on ? FadeInYLatir() : FadeOut());
    }

    private IEnumerator FadeInYLatir()
    {
        Color ini = colorActual;
        Color destino = colorFuego * ((intensidadMin + intensidadMax) * 0.5f);
        destino.a = 1f;

        float t = 0f;
        while (t < duracionFade)
        {
            t += Time.deltaTime;
            float k = curvaFade.Evaluate(Mathf.Clamp01(t / duracionFade));
            colorActual = Color.Lerp(ini, destino, k);
            AplicarColor(colorActual);
            yield return null;
        }
        rutina = StartCoroutine(Latir());
    }

    private IEnumerator Latir()
    {
        // Respiración: la intensidad oscila entre min y max, SIEMPRE encendido.
        while (true)
        {
            float s = (Mathf.Sin(Time.time * velocidadLatido * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
            float intensidad = Mathf.Lerp(intensidadMin, intensidadMax, s);
            colorActual = colorFuego * intensidad;
            colorActual.a = 1f;
            AplicarColor(colorActual);
            yield return null;
        }
    }

    private IEnumerator FadeOut()
    {
        Color ini = colorActual;
        Color destino = ColorApagadoFinal();

        float t = 0f;
        while (t < duracionFade)
        {
            t += Time.deltaTime;
            float k = curvaFade.Evaluate(Mathf.Clamp01(t / duracionFade));
            colorActual = Color.Lerp(ini, destino, k);
            AplicarColor(colorActual);
            yield return null;
        }
        colorActual = destino;
        AplicarColor(colorActual);
    }

    // Color final del estado apagado según el modo.
    private Color ColorApagadoFinal()
    {
        if (apagarConAlpha) return new Color(0f, 0f, 0f, 0f);   // capa de fuego: invisible
        return colorApagado;                                    // un solo sprite: visible neutro
    }

    private void AplicarColor(Color c)
    {
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ID_COLOR, c);
        sr.SetPropertyBlock(mpb);
    }
}