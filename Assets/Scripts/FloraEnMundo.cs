using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// ============================================================
//  FloraEnMundo — Una planta colocada en el nivel
// ============================================================
//
//  Conecta una planta del escenario con su FloraDefinition:
//    • AVISTAMIENTO: si Romerito pasa a menos de 'radioAvistamiento',
//      la planta queda 'Avistada' en el Herbario (silueta).
//    • COSECHA: según 'modo':
//        SoloDecoracion → solo se avista, no da muestras.
//        AlTocar        → se cosecha al pasar encima (necesita un
//                         Collider2D en trigger).
//        ConMacahuitl   → se cosecha a golpes, igual que la mazorca
//                         de cacao (picietl: 9 golpes).
//
//  REGENERACIÓN
//    No persiste: al recargar la escena la planta vuelve a crecer.
//    Así hay motivo para volver a zonas ya exploradas por muestras
//    para el ticitl.
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//   1. GameObject con SpriteRenderer (o hijo "Visual" con él).
//   2. Asigna 'flora' (una FloraDefinition).
//   3. Modo ConMacahuitl: pon el objeto en la capa 'destructibleLayer'
//      de RomeritoCombat y dale un Collider2D (sólido o trigger).
//      Modo AlTocar: Collider2D con 'Is Trigger' marcado.
//   4. spriteCosechada: la planta cortada (vacío = desaparece).
//  ─────────────────────────────────────────────────────────────
//
// ============================================================

public enum ModoCosecha
{
    SoloDecoracion,
    AlTocar,
    ConMacahuitl
}

public class FloraEnMundo : MonoBehaviour, IGolpeableMacahuitl
{
    [Header("Especie")]
    public FloraDefinition flora;

    [Header("Avistamiento")]
    public bool avistable = true;
    [Tooltip("Distancia a la que Romerito 've' la planta y la registra en el Herbario.")]
    public float radioAvistamiento = 3.5f;

    [Header("Cosecha")]
    public ModoCosecha modo = ModoCosecha.ConMacahuitl;
    [Min(1)] public int golpesParaCosechar = 1;
    [Min(1)] public int muestrasQueDa = 1;
    [Tooltip("Ignora golpes durante este tiempo tras uno (evita doble golpe del mismo swing).")]
    public float invulnerabilidadTrasGolpe = 0.15f;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    [Tooltip("Transform que tiembla al golpe. Vacío = el del SpriteRenderer.")]
    public Transform visual;
    [Tooltip("Sprite de la planta ya cortada. Vacío = desaparece al cosecharla.")]
    public Sprite spriteCosechada;
    public float duracionTemblor = 0.12f;
    public float amplitudTemblor = 0.05f;
    public GameObject efectoGolpe;
    public GameObject efectoCosecha;

    [Header("Eventos (opcionales)")]
    public UnityEvent OnAvistada;
    public UnityEvent OnCosechada;

    // ── Estado ───────────────────────────────────────────────
    private const float INTERVALO_REVISION = 0.25f;

    private Transform jugador;
    private float proximaRevision;
    private bool avistadaYa;
    private bool cosechada;
    private int golpes;
    private float proximoGolpeT;
    private Vector3 visualPosOriginal;
    private Coroutine rutinaTemblor;

    public bool EstaCosechada => cosechada;

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (visual == null && spriteRenderer != null) visual = spriteRenderer.transform;
        if (visual != null) visualPosOriginal = visual.localPosition;

        if (flora == null)
            Debug.LogWarning("[Flora] '" + name + "' no tiene FloraDefinition asignada.", this);
    }

    void Start()
    {
        // Si ya estaba avistada en el save, no hace falta revisar distancia.
        avistadaYa = flora == null || HerbarioManager.GetEtapa(flora) >= EtapaHerbario.Avistada;
    }

    void Update()
    {
        if (avistadaYa || !avistable) return;
        if (Time.time < proximaRevision) return;
        proximaRevision = Time.time + INTERVALO_REVISION;

        if (jugador == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            jugador = p.transform;
        }

        if ((jugador.position - transform.position).sqrMagnitude <= radioAvistamiento * radioAvistamiento)
            Avistar();
    }

    // ── Avistamiento ─────────────────────────────────────────

    private void Avistar()
    {
        avistadaYa = true;
        if (HerbarioManager.Avistar(flora))
            OnAvistada?.Invoke();
    }

    // ── Cosecha al tocar ─────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (modo != ModoCosecha.AlTocar || cosechada) return;
        if (!other.CompareTag("Player") || other.isTrigger) return;
        Cosechar();
    }

    // ── Cosecha a golpes (IGolpeableMacahuitl) ───────────────

    public bool RecibirGolpeMacahuitl(Vector2 origenAtacante, int dano, RomeritoCombat.GodFavor favor)
    {
        if (modo != ModoCosecha.ConMacahuitl || cosechada) return false;

        // Mismo swing tocando dos veces: conecta (recoil/pogo) sin contar.
        if (Time.time < proximoGolpeT) return true;
        proximoGolpeT = Time.time + invulnerabilidadTrasGolpe;

        // Cuenta GOLPES, no daño: el picietl cuesta 9 con cualquier favor.
        golpes++;

        if (efectoGolpe != null)
            Instantiate(efectoGolpe, transform.position, Quaternion.identity);

        if (golpes >= golpesParaCosechar)
        {
            Cosechar();
        }
        else
        {
            float dir = Mathf.Sign(transform.position.x - origenAtacante.x);
            if (dir == 0f) dir = 1f;
            Temblar(dir);
        }
        return true;
    }

    // ── Cosechar ─────────────────────────────────────────────

    private void Cosechar()
    {
        cosechada = true;

        // Cosechar implica haberla visto: Recolectar ya la sube de etapa.
        avistadaYa = true;
        if (flora != null) HerbarioManager.Recolectar(flora, muestrasQueDa);

        if (efectoCosecha != null)
            Instantiate(efectoCosecha, transform.position, Quaternion.identity);

        if (rutinaTemblor != null) { StopCoroutine(rutinaTemblor); rutinaTemblor = null; }
        if (visual != null) visual.localPosition = visualPosOriginal;

        if (spriteRenderer != null)
        {
            if (spriteCosechada != null) spriteRenderer.sprite = spriteCosechada;
            else spriteRenderer.enabled = false;
        }

        OnCosechada?.Invoke();
    }

    // ── Feedback ─────────────────────────────────────────────

    private void Temblar(float dir)
    {
        if (visual == null) return;
        if (rutinaTemblor != null) StopCoroutine(rutinaTemblor);
        rutinaTemblor = StartCoroutine(RutinaTemblor(dir));
    }

    private IEnumerator RutinaTemblor(float dir)
    {
        float t = 0f;
        while (t < duracionTemblor)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / duracionTemblor);
            float x = Mathf.Sin(t * 90f) * amplitudTemblor * k + dir * amplitudTemblor * 0.5f * k;
            visual.localPosition = visualPosOriginal + new Vector3(x, 0f, 0f);
            yield return null;
        }
        visual.localPosition = visualPosOriginal;
        rutinaTemblor = null;
    }

    void OnDrawGizmosSelected()
    {
        if (!avistable) return;
        Gizmos.color = new Color(0.4f, 0.8f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, radioAvistamiento);
    }
}
