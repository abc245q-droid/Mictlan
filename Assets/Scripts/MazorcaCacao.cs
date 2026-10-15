using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections;

// ============================================================
//  MazorcaCacao — mazorca petrificada que suelta semillas
// ============================================================
//
//  Recompensa pequeña colocada a mano en el nivel. Cada golpe del
//  Macahuitl la hace temblar, la agrieta un poco más y suelta
//  semillas; el último golpe la parte y libera el resto.
//
//  Ritmo (estilo depósito de geo de Hollow Knight): el jugador ve
//  que cada golpe paga, así que "terminar de romperla" se siente
//  bien y no tedioso.
//
//  Las semillas usan CollectibleItem v2 (caída física): salen
//  disparadas en dirección OPUESTA a Romerito, rebotan y se asientan.
//
//  PERSISTENCIA
//   Rota = rota para siempre (PlayerData.collectedItems). Si dejas
//   'mazorcaID' vacío se genera uno con escena + posición, así que
//   no hace falta inventar IDs a mano. Pon 'persistente' en false
//   si la quieres renovable (se regenera al recargar la escena).
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//   1. GameObject en la capa 'destructibleLayer' de RomeritoCombat.
//   2. Collider2D (sólido o trigger: el ataque lo detecta igual).
//   3. Hijo "Visual" con SpriteRenderer → asígnalo a 'visual'.
//      (Así el temblor no mueve el collider.)
//   4. semillaPrefab → el prefab de la semilla de cacao.
//   5. spritesPorGolpe: intacta → agrietada 1 → agrietada 2 …
//      spriteRota: la cáscara partida que queda (o vacío = desaparece).
//  ─────────────────────────────────────────────────────────────

public class MazorcaCacao : MonoBehaviour, IGolpeableMacahuitl
{
    [Header("Identidad / Persistencia")]
    [Tooltip("Vacío = se genera con escena + posición.")]
    public string mazorcaID = "";
    [Tooltip("Rota se queda rota entre sesiones. Falso = se regenera al recargar la escena.")]
    public bool persistente = true;

    [Header("Resistencia")]
    public int golpesParaRomper = 4;
    [Tooltip("Ignora golpes durante este tiempo tras uno (evita doble golpe del mismo swing).")]
    public float invulnerabilidadTrasGolpe = 0.15f;

    [Header("Semillas")]
    public GameObject semillaPrefab;
    [Tooltip("Semillas que suelta en cada golpe que NO la rompe.")]
    public int semillasPorGolpe = 1;
    [Tooltip("Semillas que suelta al romperse.")]
    public int semillasAlRomper = 3;
    [Tooltip("De dónde salen las semillas (relativo a la mazorca).")]
    public Vector2 puntoSalida = new Vector2(0f, 0.4f);
    [Tooltip("Velocidad lateral (alejándose de Romerito): mín / máx.")]
    public Vector2 rangoLateral = new Vector2(0.6f, 2.6f);
    [Tooltip("Velocidad vertical: mín / máx.")]
    public Vector2 rangoVertical = new Vector2(4f, 6.5f);
    [Tooltip("Probabilidad de que una semilla salga hacia el lado de Romerito.")]
    [Range(0f, 1f)] public float probHaciaRomerito = 0.2f;
    [Tooltip("Paredes/suelo: las semillas nunca nacen dentro de ellos.")]
    public LayerMask capaObstaculos;

    [Header("Visual")]
    [Tooltip("Transform que tiembla (hijo con el sprite). Vacío = el del SpriteRenderer.")]
    public Transform visual;
    public SpriteRenderer spriteRenderer;
    [Tooltip("Sprite por golpes recibidos: [0] intacta, [1] tras 1 golpe, …")]
    public Sprite[] spritesPorGolpe;
    [Tooltip("Cáscara partida que queda al romperse. Vacío = desaparece.")]
    public Sprite spriteRota;

    [Header("Feedback del golpe")]
    public float duracionTemblor = 0.14f;
    public float amplitudTemblor = 0.06f;
    [Tooltip("Aplastamiento al recibir el golpe (squash).")]
    public float aplastamiento = 0.12f;
    public GameObject efectoGolpe;
    public GameObject efectoRomper;

    [Header("Eventos (opcionales)")]
    public UnityEvent OnGolpe;
    public UnityEvent OnRota;

    // ── Estado ───────────────────────────────────────────────
    private int golpesRecibidos = 0;
    private bool rota = false;
    private float proximoGolpeT = 0f;
    private Collider2D[] colliders;
    private Vector3 visualPosOriginal;
    private Vector3 visualEscalaOriginal;
    private Coroutine feedbackRutina;

    public bool EstaRota => rota;

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        colliders = GetComponents<Collider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (visual == null && spriteRenderer != null) visual = spriteRenderer.transform;
        if (visual != null)
        {
            visualPosOriginal = visual.localPosition;
            visualEscalaOriginal = visual.localScale;
        }
        if (string.IsNullOrEmpty(mazorcaID))
        {
            Vector3 p = transform.position;
            mazorcaID = $"mazorca_{SceneManager.GetActiveScene().name}_{p.x:F1}_{p.y:F1}";
        }
    }

    void Start()
    {
        if (persistente && YaRota())
        {
            golpesRecibidos = golpesParaRomper;
            AplicarRota(instantaneo: true);
            return;
        }
        ActualizarSprite();
    }

    // ── IGolpeableMacahuitl ──────────────────────────────────

    public bool RecibirGolpeMacahuitl(Vector2 origenAtacante, int dano, RomeritoCombat.GodFavor favor)
    {
        if (rota) return false;

        // Mismo swing tocando dos veces: cuenta como conectado (recoil/pogo)
        // pero no hace daño extra.
        if (Time.time < proximoGolpeT) return true;
        proximoGolpeT = Time.time + invulnerabilidadTrasGolpe;

        // Cuenta GOLPES, no daño: romperla cuesta lo mismo con cualquier
        // favor o pasiva, y el jugador aprende el número de golpes.
        golpesRecibidos++;
        float dirAlejarse = Mathf.Sign(transform.position.x - origenAtacante.x);
        if (dirAlejarse == 0f) dirAlejarse = 1f;

        if (efectoGolpe != null)
            Instantiate(efectoGolpe, transform.position + (Vector3)puntoSalida, Quaternion.identity);

        if (golpesRecibidos >= golpesParaRomper)
        {
            SoltarSemillas(semillasAlRomper, dirAlejarse);
            Romper();
        }
        else
        {
            SoltarSemillas(semillasPorGolpe, dirAlejarse);
            ActualizarSprite();
            Feedback(dirAlejarse);
            OnGolpe?.Invoke();
        }
        return true;
    }

    // ── Semillas ─────────────────────────────────────────────

    void SoltarSemillas(int cantidad, float dirAlejarse)
    {
        if (semillaPrefab == null || cantidad <= 0) return;

        Vector2 origen = (Vector2)transform.position + puntoSalida;
        // Si el punto de salida quedó dentro de un muro, usar el centro.
        if (capaObstaculos != 0 && Physics2D.OverlapPoint(origen, capaObstaculos))
            origen = transform.position;

        for (int i = 0; i < cantidad; i++)
        {
            float lado = (Random.value < probHaciaRomerito) ? -dirAlejarse : dirAlejarse;
            Vector2 vel = new Vector2(lado * Random.Range(rangoLateral.x, rangoLateral.y),
                                      Random.Range(rangoVertical.x, rangoVertical.y));

            GameObject s = Instantiate(semillaPrefab, origen, Quaternion.identity);
            CollectibleItem item = s.GetComponent<CollectibleItem>();
            if (item != null) item.FijarSalida(vel);
        }
    }

    // ── Romper ───────────────────────────────────────────────

    void Romper()
    {
        rota = true;
        if (efectoRomper != null)
            Instantiate(efectoRomper, transform.position, Quaternion.identity);

        if (persistente && GameManager01.instance != null && GameManager01.instance.currentData != null)
            GameManager01.instance.currentData.RegistrarID(
                GameManager01.instance.currentData.collectedItems, mazorcaID);
        // Sin SaveGame aquí: se guarda con el siguiente guardado normal
        // (Cihuacalli), igual que el resto del progreso menor.

        AplicarRota(instantaneo: false);
        OnRota?.Invoke();
    }

    void AplicarRota(bool instantaneo)
    {
        rota = true;
        foreach (Collider2D c in colliders) if (c != null) c.enabled = false;

        if (feedbackRutina != null) { StopCoroutine(feedbackRutina); feedbackRutina = null; }
        if (visual != null)
        {
            visual.localPosition = visualPosOriginal;
            visual.localScale = visualEscalaOriginal;
        }

        if (spriteRenderer != null)
        {
            if (spriteRota != null) spriteRenderer.sprite = spriteRota;
            else spriteRenderer.enabled = false;
        }
    }

    bool YaRota()
    {
        var gm = GameManager01.instance;
        return gm != null && gm.currentData != null &&
               gm.currentData.collectedItems != null &&
               gm.currentData.collectedItems.Contains(mazorcaID);
    }

    // ── Visual ───────────────────────────────────────────────

    void ActualizarSprite()
    {
        if (spriteRenderer == null || spritesPorGolpe == null || spritesPorGolpe.Length == 0) return;
        int i = Mathf.Clamp(golpesRecibidos, 0, spritesPorGolpe.Length - 1);
        if (spritesPorGolpe[i] != null) spriteRenderer.sprite = spritesPorGolpe[i];
    }

    void Feedback(float dirAlejarse)
    {
        if (visual == null) return;
        if (feedbackRutina != null) StopCoroutine(feedbackRutina);
        feedbackRutina = StartCoroutine(RutinaFeedback(dirAlejarse));
    }

    IEnumerator RutinaFeedback(float dir)
    {
        float t = 0f;
        while (t < duracionTemblor)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / duracionTemblor);   // 1 → 0

            // Temblor que arranca empujado hacia el lado del golpe y se apaga.
            float x = Mathf.Sin(t * 90f) * amplitudTemblor * k + dir * amplitudTemblor * 0.5f * k;
            visual.localPosition = visualPosOriginal + new Vector3(x, 0f, 0f);

            // Squash & stretch: más ancha y baja al impacto.
            float s = aplastamiento * k;
            visual.localScale = Vector3.Scale(visualEscalaOriginal, new Vector3(1f + s, 1f - s, 1f));
            yield return null;
        }
        visual.localPosition = visualPosOriginal;
        visual.localScale = visualEscalaOriginal;
        feedbackRutina = null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.3f, 0.1f, 1f);
        Vector3 p = transform.position + (Vector3)puntoSalida;
        Gizmos.DrawWireSphere(p, 0.08f);
        Gizmos.DrawLine(p, p + new Vector3(rangoLateral.y, rangoVertical.y, 0f) * 0.15f);
        Gizmos.DrawLine(p, p + new Vector3(-rangoLateral.y, rangoVertical.y, 0f) * 0.15f);
    }
}
