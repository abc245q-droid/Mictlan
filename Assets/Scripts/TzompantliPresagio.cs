using UnityEngine;
using System.Collections;

// ============================================================
//  TzompantliPresagio — telegrafeo de aparición desde el tzompantli
// ============================================================
//
//  Va en cada spawnPoint del WaveSpawner. Antes de que un enemigo
//  aparezca, el cráneo sobrepuesto (hijo con SpriteRenderer) tiembla
//  sobre su travesaño con intensidad creciente, da un último jalón
//  y se "suelta" del tzompantli: se oculta y en su lugar aparece el
//  enemigo. Tras unos segundos el cráneo vuelve a su sitio (fade-in).
//
//  Lectura de diseño (Hollow Knight): el jugador siempre sabe DÓNDE
//  y CUÁNDO va a aparecer el siguiente mictecah. Nunca hay spawn
//  sin aviso.
//
//  SETUP EN UNITY
//  1. En el spawnPoint, agregar este componente.
//  2. Crear un hijo "Craneo" con SpriteRenderer usando el MISMO sprite
//     de cráneo del tzompantli, encima exacto de un cráneo del rack.
//  3. BORRAR del rack el cráneo que queda debajo (si no, al temblar
//     se ve el cráneo duplicado y estático detrás).
//  4. Sorting Order del Craneo = el del rack + 1.
//  5. (Opcional) ParticleSystem de polvo/astillas, AudioSource,
//     material Mictlan/SpriteGlowHDR para el brillo.
//
//  El WaveSpawner lo detecta solo con GetComponent en el spawnPoint;
//  si un spawnPoint no lo tiene, el enemigo aparece como antes.
// ============================================================

public class TzompantliPresagio : MonoBehaviour
{
    [Header("Cráneo sobrepuesto")]
    [Tooltip("Hijo con SpriteRenderer colocado sobre el rack del tzompantli")]
    public SpriteRenderer craneo;

    [Header("Tiempos")]
    [Tooltip("Segundos de temblor antes de que salga el enemigo")]
    public float duracion = 1.1f;
    [Tooltip("Último tramo (s) dentro de 'duracion' donde el cráneo da el jalón final")]
    public float duracionJalon = 0.12f;

    [Header("Temblor")]
    public float amplitudInicial = 0.015f;
    public float amplitudFinal = 0.08f;
    [Tooltip("Velocidad del ruido del temblor")]
    public float frecuencia = 30f;
    [Tooltip("Cómo crece la intensidad de 0 a 1 a lo largo del presagio")]
    public AnimationCurve curvaIntensidad = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Ángulo máximo (grados) de cabeceo del cráneo en el travesaño")]
    public float rotacionMaxima = 8f;

    [Header("Jalón final")]
    [Tooltip("Desplazamiento local del jalón (el cráneo se arranca del travesaño)")]
    public Vector2 desplazamientoJalon = new Vector2(0f, -0.12f);

    [Header("Desprendimiento")]
    [Tooltip("Ocultar el cráneo cuando sale el enemigo (deja el hueco en el rack)")]
    public bool ocultarAlSoltar = true;
    public float segundosParaReaparecer = 2.5f;
    public float duracionReaparicion = 0.6f;

    [Header("Brillo (opcional — material Mictlan/SpriteGlowHDR)")]
    public bool usarBrillo = false;
    [Tooltip("Frío, nunca cálido: el fuego es el único elemento cálido de Mictlán")]
    [ColorUsage(true, true)]
    public Color colorBrillo = new Color(0.75f, 0.95f, 1.1f, 1f);
    public float intensidadBrillo = 1.6f;

    [Header("Extras (opcionales)")]
    public ParticleSystem polvo;
    public AudioSource fuenteAudio;
    public AudioClip sonidoCrujido;
    public AudioClip sonidoSoltar;

    // ── Estado ───────────────────────────────────────────────
    public bool Ocupado { get; private set; }

    private Vector3 posOriginal;
    private Quaternion rotOriginal;
    private Color colorOriginal;
    private MaterialPropertyBlock mpb;
    private Coroutine reaparicionRutina;
    private float semillaX, semillaY;
    private bool inicializado = false;

    private static readonly int ID_COLOR = Shader.PropertyToID("_Color");

    void Awake() => Inicializar();

    void Inicializar()
    {
        if (inicializado) return;
        if (craneo == null) craneo = GetComponentInChildren<SpriteRenderer>();
        if (craneo != null)
        {
            posOriginal = craneo.transform.localPosition;
            rotOriginal = craneo.transform.localRotation;
            colorOriginal = craneo.color;
        }
        mpb = new MaterialPropertyBlock();
        semillaX = Random.value * 100f;
        semillaY = Random.value * 100f + 50f;
        inicializado = true;
    }

    // ── API ──────────────────────────────────────────────────

    /// <summary>
    /// Corre el presagio completo. El WaveSpawner hace
    /// yield return presagio.Presagiar() y luego instancia al enemigo.
    /// </summary>
    public IEnumerator Presagiar()
    {
        Inicializar();
        if (craneo == null) yield break;

        // Si el cráneo seguía oculto o reapareciendo, vuelve a su sitio ya.
        DetenerReaparicion();
        Restaurar();

        Ocupado = true;

        if (polvo != null) polvo.Play();
        if (fuenteAudio != null && sonidoCrujido != null)
            fuenteAudio.PlayOneShot(sonidoCrujido);

        Transform t = craneo.transform;
        float tiempoTemblor = Mathf.Max(0f, duracion - duracionJalon);
        float tt = 0f;

        // 1. Temblor creciente
        while (tt < tiempoTemblor)
        {
            tt += Time.deltaTime;
            float k = curvaIntensidad.Evaluate(Mathf.Clamp01(tt / Mathf.Max(0.0001f, tiempoTemblor)));
            float amp = Mathf.Lerp(amplitudInicial, amplitudFinal, k);

            float nx = Mathf.PerlinNoise(semillaX, tt * frecuencia) * 2f - 1f;
            float ny = Mathf.PerlinNoise(semillaY, tt * frecuencia) * 2f - 1f;

            t.localPosition = posOriginal + new Vector3(nx * amp, ny * amp * 0.5f, 0f);
            t.localRotation = rotOriginal * Quaternion.Euler(0f, 0f, nx * rotacionMaxima * k);

            if (usarBrillo) AplicarBrillo(k);
            yield return null;
        }

        // 2. Jalón: el cráneo se arranca del travesaño
        Vector3 desde = t.localPosition;
        Vector3 hasta = posOriginal + (Vector3)desplazamientoJalon;
        float tj = 0f;
        while (tj < duracionJalon)
        {
            tj += Time.deltaTime;
            float k = Mathf.Clamp01(tj / Mathf.Max(0.0001f, duracionJalon));
            t.localPosition = Vector3.Lerp(desde, hasta, k * k);
            if (usarBrillo) AplicarBrillo(1f);
            yield return null;
        }

        // 3. Se suelta
        if (fuenteAudio != null && sonidoSoltar != null)
            fuenteAudio.PlayOneShot(sonidoSoltar);

        if (ocultarAlSoltar)
        {
            craneo.enabled = false;
            reaparicionRutina = StartCoroutine(RutinaReaparicion());
        }
        else
        {
            Restaurar();
        }

        Ocupado = false;
    }

    /// <summary>
    /// Cancela todo y deja el cráneo en su sitio. Llamado por
    /// WaveSpawner.ResetWaves()/StopWaves() (muerte de Romerito).
    /// </summary>
    public void Cancelar()
    {
        Inicializar();
        DetenerReaparicion();
        if (polvo != null) polvo.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Restaurar();
        Ocupado = false;
    }

    // ── Interno ──────────────────────────────────────────────

    IEnumerator RutinaReaparicion()
    {
        yield return new WaitForSeconds(segundosParaReaparecer);

        craneo.transform.localPosition = posOriginal;
        craneo.transform.localRotation = rotOriginal;
        LimpiarBrillo();

        Color c = colorOriginal; c.a = 0f;
        craneo.color = c;
        craneo.enabled = true;

        float t = 0f;
        while (t < duracionReaparicion)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0f, colorOriginal.a, t / Mathf.Max(0.0001f, duracionReaparicion));
            craneo.color = c;
            yield return null;
        }
        craneo.color = colorOriginal;
        reaparicionRutina = null;
    }

    void DetenerReaparicion()
    {
        if (reaparicionRutina != null)
        {
            StopCoroutine(reaparicionRutina);
            reaparicionRutina = null;
        }
    }

    void Restaurar()
    {
        if (craneo == null) return;
        craneo.transform.localPosition = posOriginal;
        craneo.transform.localRotation = rotOriginal;
        craneo.color = colorOriginal;
        craneo.enabled = true;
        LimpiarBrillo();
    }

    void AplicarBrillo(float k)
    {
        craneo.GetPropertyBlock(mpb);
        Color c = Color.Lerp(Color.white, colorBrillo * intensidadBrillo, k);
        c.a = 1f;
        mpb.SetColor(ID_COLOR, c);
        craneo.SetPropertyBlock(mpb);
    }

    void LimpiarBrillo()
    {
        if (!usarBrillo || craneo == null) return;
        craneo.GetPropertyBlock(mpb);
        mpb.SetColor(ID_COLOR, Color.white);
        craneo.SetPropertyBlock(mpb);
    }

    void OnDisable()
    {
        // Si el objeto se apaga a medio presagio, que no quede torcido.
        if (inicializado) { DetenerReaparicion(); Restaurar(); Ocupado = false; }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.4f, 0.4f, 0f));
    }
}
