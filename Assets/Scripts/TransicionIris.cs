using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

// ============================================================
//  TransicionIris — Cortinilla de muerte (estilo Mario)
// ============================================================
//
//  Una silueta (calavera de Mictlantecuhtli) se cierra sobre
//  Romerito al morir: el mundo queda en negro mientras el
//  tonalli disperso se reagrupa en el Cihuacalli, y el iris se
//  vuelve a abrir centrado en él.
//
//  Borde en rojo-fuego: el único calor del Mictlán es el tonalli
//  de Romerito apagándose.
//
//  SIN SETUP: se crea sola la primera vez que alguien usa
//  TransicionIris.Instancia (singleton perezoso + DontDestroyOnLoad).
//  SesionMictlan la destruye con el resto de objetos persistentes
//  al volver al menú; se recrea sola al siguiente uso.
//
//  Recursos (carpeta Resources):
//    Transiciones/IrisMascara            → shader Mictlan/UI/IrisMascara
//    Transiciones/IrisMictlantecuhtli    → PNG; el ALFA es la abertura
//  Si falta la máscara se usa un círculo (iris clásico).
//
//  USO (dentro de una corrutina):
//    yield return TransicionIris.Instancia.Cerrar(transform);
//    ... teletransporte / reset en negro ...
//    yield return TransicionIris.Instancia.Abrir(transform);
//
//  Todo corre en tiempo NO escalado: sobrevive a hit-stops y a
//  timeScale = 0.
// ============================================================

public class TransicionIris : MonoBehaviour
{
    const string RutaShader  = "Transiciones/IrisMascara";
    const string RutaMascara = "Transiciones/IrisMictlantecuhtli";
    const int OrdenCanvas = 30000;   // por encima de HUD, pausa y tutoriales

    // ── Singleton perezoso ───────────────────────────────────
    static TransicionIris instancia;

    public static TransicionIris Instancia
    {
        get
        {
            if (instancia == null)
            {
                var go = new GameObject("[TransicionIris]");
                DontDestroyOnLoad(go);
                instancia = go.AddComponent<TransicionIris>();
            }
            return instancia;
        }
    }

    // ── Ajustes (editables en Play Mode sobre [TransicionIris]) ──
    [Header("Silueta")]
    [Tooltip("Punto de la máscara (UV, origen abajo-izquierda) que queda fijo sobre " +
             "Romerito. Debe caer en zona SÓLIDA de la silueta (frente de la calavera).")]
    public Vector2 pivote = new Vector2(0.5f, 0.70f);
    [Tooltip("Color del borde. Fuego: único elemento cálido del Mictlán.")]
    public Color colorBorde = new Color(1f, 0.32f, 0.08f, 1f);
    [Tooltip("Grosor del borde en fracción del alto de pantalla.")]
    public float grosorBorde = 0.007f;

    [Header("Escalas (alto de la silueta / alto de pantalla)")]
    [Tooltip("Tamaño con el que arranca: la frente debe cubrir toda la pantalla.")]
    public float escalaAbierta = 12f;
    [Tooltip("Tamaño de la pausa dramática, con Romerito visible dentro de la frente.")]
    public float escalaPausa = 0.6f;

    [Header("Cierre (al morir)")]
    public float duracionEncoger = 0.75f;
    public float duracionPausa   = 0.45f;
    public float duracionColapso = 0.18f;

    [Header("Apertura (al reaparecer)")]
    public float duracionAsomar   = 0.18f;
    public float duracionPausaAp  = 0.2f;
    public float duracionExpandir = 0.55f;
    [Tooltip("Tiempo en negro para que Cinemachine y RoomConfiner se asienten en el checkpoint.")]
    public float esperaCamara = 0.15f;

    /// <summary>True mientras la cortinilla tapa (parcial o totalmente) la pantalla.</summary>
    public bool Cubriendo { get; private set; }

    RawImage imagen;
    Material material;

    static readonly int IdCentro      = Shader.PropertyToID("_Centro");
    static readonly int IdPivote      = Shader.PropertyToID("_Pivote");
    static readonly int IdEscala      = Shader.PropertyToID("_Escala");
    static readonly int IdAspecto     = Shader.PropertyToID("_Aspecto");
    static readonly int IdColorBorde  = Shader.PropertyToID("_ColorBorde");
    static readonly int IdGrosorBorde = Shader.PropertyToID("_GrosorBorde");
    static readonly int IdUsarCirculo = Shader.PropertyToID("_UsarCirculo");

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        instancia = this;
        Construir();
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
        if (material != null) Destroy(material);
    }

    void Construir()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OrdenCanvas;
        canvas.pixelPerfect = false;

        var hijo = new GameObject("Iris", typeof(RectTransform));
        hijo.transform.SetParent(transform, false);
        var rt = (RectTransform)hijo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        imagen = hijo.AddComponent<RawImage>();
        imagen.raycastTarget = false;
        imagen.uvRect = new Rect(0f, 0f, 1f, 1f);

        Shader shader = Resources.Load<Shader>(RutaShader);
        if (shader == null)
        {
            Debug.LogError($"[TransicionIris] No se encontró el shader en Resources/{RutaShader}. " +
                           "La cortinilla quedará desactivada.");
            imagen.enabled = false;
            return;
        }

        material = new Material(shader) { name = "Mat_Iris (runtime)" };
        imagen.material = material;

        Texture2D mascara = Resources.Load<Texture2D>(RutaMascara);
        if (mascara != null)
        {
            imagen.texture = mascara;
            material.SetFloat(IdUsarCirculo, 0f);
        }
        else
        {
            Debug.LogWarning($"[TransicionIris] Sin máscara en Resources/{RutaMascara}: " +
                             "se usará un iris circular.");
            imagen.texture = Texture2D.whiteTexture;
            material.SetFloat(IdUsarCirculo, 1f);
        }

        imagen.enabled = false;
    }

    // ── API ──────────────────────────────────────────────────

    /// <summary>Cierra la silueta sobre 'objetivo' hasta negro total.</summary>
    public IEnumerator Cerrar(Transform objetivo)
    {
        if (material == null) yield break;

        Mostrar(true);
        yield return Animar(objetivo, escalaAbierta, escalaPausa, duracionEncoger, SuavizarSalida);
        yield return Sostener(objetivo, escalaPausa, duracionPausa);
        yield return Animar(objetivo, escalaPausa, 0f, duracionColapso, SuavizarEntrada);
        Aplicar(objetivo, 0f);   // negro total
    }

    /// <summary>
    /// Desde negro, asienta la cámara en la nueva posición y abre la silueta
    /// centrada en 'objetivo'.
    /// </summary>
    public IEnumerator Abrir(Transform objetivo)
    {
        if (material == null) { Mostrar(false); yield break; }

        Mostrar(true);
        Aplicar(objetivo, 0f);
        yield return AsentarCamara();

        yield return Animar(objetivo, 0f, escalaPausa, duracionAsomar, SuavizarSalida);
        yield return Sostener(objetivo, escalaPausa, duracionPausaAp);
        yield return Animar(objetivo, escalaPausa, escalaAbierta, duracionExpandir, SuavizarEntrada);
        Mostrar(false);
    }

    /// <summary>Oculta la cortinilla de golpe (p. ej. al salir al menú).</summary>
    public void Cancelar()
    {
        StopAllCoroutines();
        Mostrar(false);
    }

    // ── Internos ─────────────────────────────────────────────

    void Mostrar(bool visible)
    {
        Cubriendo = visible;
        if (imagen != null) imagen.enabled = visible;
    }

    IEnumerator Animar(Transform objetivo, float desde, float hasta, float duracion,
                       System.Func<float, float> curva)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = curva(Mathf.Clamp01(t / Mathf.Max(duracion, 0.0001f)));
            Aplicar(objetivo, Mathf.LerpUnclamped(desde, hasta, k));
            yield return null;
        }
        Aplicar(objetivo, hasta);
    }

    IEnumerator Sostener(Transform objetivo, float escala, float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            Aplicar(objetivo, escala);   // sigue al objetivo si la cámara se mueve
            yield return null;
        }
    }

    // Tras un teletransporte: esperar a que RoomConfiner reciba su
    // OnTriggerEnter2D (colliders reactivados → siguiente paso de física)
    // y anular el damping de Cinemachine para que la cámara salte en seco
    // en vez de viajar por el mapa.
    IEnumerator AsentarCamara()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        InvalidarCamaras();
        yield return null;

        float t = 0f;
        while (t < esperaCamara)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        InvalidarCamaras();   // por si RoomConfiner cambió el confiner con retraso
        yield return null;
    }

    static void InvalidarCamaras()
    {
        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            cam.PreviousStateIsValid = false;
    }

    void Aplicar(Transform objetivo, float escala)
    {
        material.SetVector(IdCentro, CentroViewport(objetivo));
        material.SetVector(IdPivote, pivote);
        material.SetFloat(IdEscala, Mathf.Max(0f, escala));
        material.SetFloat(IdAspecto, Screen.height > 0 ? (float)Screen.width / Screen.height : 1.7777f);
        material.SetColor(IdColorBorde, colorBorde);
        material.SetFloat(IdGrosorBorde, grosorBorde);
    }

    static Vector2 CentroViewport(Transform objetivo)
    {
        Camera cam = Camera.main;
        if (cam == null || objetivo == null) return new Vector2(0.5f, 0.5f);

        // Centro visual del sprite (el pivote del transform suele estar en los pies).
        Vector3 punto = objetivo.position;
        var sr = objetivo.GetComponent<SpriteRenderer>();
        if (sr != null && sr.enabled && sr.sprite != null)
            punto = sr.bounds.center;

        Vector3 vp = cam.WorldToViewportPoint(punto);
        return new Vector2(vp.x, vp.y);
    }

    static float SuavizarSalida(float x)  { float u = 1f - x; return 1f - u * u * u; }  // rápido → lento
    static float SuavizarEntrada(float x) { return x * x * x; }                          // lento → rápido
}
