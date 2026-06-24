using UnityEngine;
using System.Collections;

// ============================================================
//  CargaTonalliVFX — "Carga inversa" de Tonalli sobre Romerito
// ============================================================
//
//  Inspirado en la energía maldita inversa de JJK: mientras se
//  mantiene Y para curar, el tonalli se acumula sobre Romerito como
//  un resplandor que va de blanco-cálido a ámbar saturado, y suelta
//  un destello cada vez que recupera un corazón.
//
//  Anima [HDR] _Color por MaterialPropertyBlock (cero GC, conserva
//  el batching). El SpriteRenderer del cuerpo de Romerito DEBE usar
//  un material con shader Mictlan/SpriteGlowHDR (con _Color blanco se
//  ve idéntico a lo normal).
//
//  Lo conduce RomeritoHealth:
//    • SetProgreso(t01) cada frame mientras carga (0..1).
//    • Destello()        al completar una curación.
//    • Apagar()          al cancelar o terminar el canal.
//
//  Ponlo en el mismo GameObject que el SpriteRenderer del cuerpo.

[RequireComponent(typeof(SpriteRenderer))]
public class CargaTonalliVFX : MonoBehaviour
{
    [Header("Color")]
    [Tooltip("Tono de la carga al máximo (ámbar tonalli). Al inicio mezcla con blanco.")]
    public Color colorCarga = new Color(1f, 0.78f, 0.1f);   // ámbar tonalli

    [Header("Intensidad HDR")]
    [Tooltip("Intensidad al iniciar la carga (apenas roza el umbral del Bloom).")]
    public float intensidadBase = 1.2f;
    [Tooltip("Intensidad justo antes de curar (resplandor ámbar pleno).")]
    public float intensidadPico = 3.0f;
    [Tooltip("Pico del destello en el instante de curar.")]
    public float intensidadDestello = 4.5f;

    [Header("Tiempos")]
    public float duracionDestello = 0.18f;
    public float duracionApagado = 0.30f;
    public AnimationCurve curvaApagado = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Interno ──────────────────────────────────────────────
    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;
    private static readonly int ID_COLOR = Shader.PropertyToID("_Color");

    private Coroutine apagadoRutina;
    private Coroutine destelloRutina;
    private bool destelloActivo = false;
    private bool activo = false;
    private Color colorActual;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        colorActual = Color.white;
        Aplicar(colorActual);
    }

    /// <summary>Llamar cada frame mientras se mantiene la carga (t01 = 0..1).</summary>
    public void SetProgreso(float t01)
    {
        activo = true;
        if (apagadoRutina != null) { StopCoroutine(apagadoRutina); apagadoRutina = null; }
        if (destelloActivo) return;   // el destello manda mientras dura

        colorActual = ColorEnProgreso(Mathf.Clamp01(t01));
        Aplicar(colorActual);
    }

    /// <summary>Destello en el instante de recuperar un corazón.</summary>
    public void Destello()
    {
        if (destelloRutina != null) StopCoroutine(destelloRutina);
        destelloRutina = StartCoroutine(RutinaDestello());
    }

    /// <summary>Apaga la carga (cancelación o fin del canal): fade a neutro.</summary>
    public void Apagar()
    {
        if (!activo) return;
        activo = false;
        if (apagadoRutina != null) StopCoroutine(apagadoRutina);
        apagadoRutina = StartCoroutine(RutinaApagado());
    }

    // ── Cálculo del color ────────────────────────────────────
    // De blanco-cálido (inicio) a ámbar saturado (pico), escalado por intensidad.
    private Color ColorEnProgreso(float t01)
    {
        float inten = Mathf.Lerp(intensidadBase, intensidadPico, t01);
        Color tinte = Color.Lerp(Color.white, colorCarga, t01);
        Color c = tinte * inten;
        c.a = 1f;
        return c;
    }

    private IEnumerator RutinaDestello()
    {
        destelloActivo = true;

        Color pico = colorCarga * intensidadDestello; pico.a = 1f;
        Color fin = colorCarga * intensidadPico;       fin.a = 1f;

        Aplicar(pico);
        float t = 0f;
        while (t < duracionDestello)
        {
            t += Time.deltaTime;
            colorActual = Color.Lerp(pico, fin, t / duracionDestello);
            Aplicar(colorActual);
            yield return null;
        }
        destelloActivo = false;
        destelloRutina = null;
    }

    private IEnumerator RutinaApagado()
    {
        if (destelloRutina != null) { StopCoroutine(destelloRutina); destelloRutina = null; destelloActivo = false; }

        Color ini = colorActual;
        float t = 0f;
        while (t < duracionApagado)
        {
            t += Time.deltaTime;
            float k = curvaApagado.Evaluate(Mathf.Clamp01(t / duracionApagado));
            colorActual = Color.Lerp(ini, Color.white, k);
            Aplicar(colorActual);
            yield return null;
        }
        colorActual = Color.white;
        Aplicar(colorActual);
        apagadoRutina = null;
    }

    private void Aplicar(Color c)
    {
        sr.GetPropertyBlock(mpb);
        mpb.SetColor(ID_COLOR, c);
        sr.SetPropertyBlock(mpb);
    }
}
