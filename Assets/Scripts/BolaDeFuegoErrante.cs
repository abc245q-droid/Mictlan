using UnityEngine;
using System.Collections;

// ============================================================
//  BolaDeFuegoErrante — Obstáculo aéreo del Nivel 1 (Chicunamictlán)
// ============================================================
//
//  Brasa del fuego viejo que escapó de los braseros apagados. Sin
//  ritual que la guíe, vaga sin rumbo por el aire. NO persigue a
//  Romerito ni reacciona a él: es un peligro de zona (familia de la
//  Toloatzin), no un cazador.
//
//  ⚠ No confundir con BolaDeFuego.cs, que es el PROYECTIL del Favor
//    de Huehueteotl que lanza Romerito.
//
//  COMPORTAMIENTO:
//    • Vuela en línea (casi) recta por TODA la sala y REBOTA al
//      chocar con paredes, suelo, techo y plataformas (capasRebote).
//      En cada rebote se desvía un ángulo aleatorio: nunca repite el
//      mismo recorrido. Entre rebotes ondula levemente (Perlin).
//    • Nunca sale de su sala: si la sala tiene huecos/salidas, el
//      límite del RoomConfiner (o 'limitesSala') también la rebota.
//    • Evita trayectorias casi horizontales o verticales puras para
//      no quedarse "ping-ponguenado" entre dos superficies.
//    • Daño por contacto + empuje (mismo patrón que NubeHumoChile).
//      La Barrera de Copal en modo Cubierto protege (invulnerabilidad
//      externa vía PuedeRecibirDano).
//    • Macahuitl (golpe lateral o pogo) la DISPERSA: desaparece y
//      reaparece en su ancla tras unos segundos. NO suelta Tonalli
//      (es obstáculo, no granja de recursos). El pogo sobre ella sí
//      rebota a Romerito.
//    • (Opcional) braseroVinculado: si ese brasero está encendido,
//      la brasa se extingue para siempre. Recompensa visual de
//      encender braseros (arco de la Toxiuhmolpilia).
//    • Se congela con PausaMundo (diálogos, tienda, hit-stop).
//
//  SETUP DEL PREFAB:
//    • Raíz:
//        - Rigidbody2D → lo configura el script (Kinematic, sin gravedad).
//        - CircleCollider2D → Is Trigger: TRUE (radio ≈ tamaño del núcleo
//          de la llama, no del glow: el hitbox debe ser honesto).
//        - Layer: la del 'destructibleLayer' de RomeritoCombat
//          (para que el Macahuitl y el pogo la detecten vía
//          IGolpeableMacahuitl). NO usar la layer Enemy: no es enemigo,
//          no tiene EnemyDummy y no debe dar Tonalli.
//    • Hijo "Visual": SpriteRenderer de la bola (+ glow con
//      Mictlan/SpriteGlowHDR como capa separada). Arrástralo al
//      campo 'visual'. El script lo escala para el pop/aparición.
//    • Hijo "Estela" (Particle System, hermano de Visual, NO dentro):
//      Simulation Space = World, Rate over Distance. Campo 'estela'.
//      Al dispersarse deja de emitir pero sus chispas se apagan solas.
//    • Hijo "ChispasDispersion" (Particle System, hermano de Visual):
//      Play On Awake OFF, Looping OFF, un Burst. Campo
//      'chispasDispersion'. El script lo dispara al golpearla.
//    • (Alternativa) prefab de chispas → 'efectoDispersion'.
//    • capasRebote: Suelo (y cualquier otra layer de geometría).
//      NO incluir Destructible: rebotaría con mazorcas, puertas y
//      con otras bolas de fuego.
//    • Gizmo al seleccionarla: hitbox, dirección inicial y
//      límites de la sala detectada.
//
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BolaDeFuegoErrante : MonoBehaviour, IGolpeableMacahuitl
{
    [Header("Movimiento")]
    [Tooltip("Velocidad de vuelo (m/s).")]
    public float velocidad = 3f;
    [Tooltip("Layers contra las que rebota: Suelo (geometría). NO incluir Destructible.")]
    public LayerMask capasRebote;
    [Tooltip("Desviación aleatoria máxima en cada rebote (grados). 0 = rebote perfecto tipo billar.")]
    [Range(0f, 60f)] public float desvioRebote = 25f;
    [Tooltip("Ángulo mínimo respecto a horizontal y vertical. Evita el ping-pong eterno.")]
    [Range(0f, 40f)] public float anguloMinimoEje = 15f;
    [Tooltip("Ondulación entre rebotes (grados/s). 0 = líneas rectas.")]
    public float ondulacion = 30f;
    [Tooltip("Qué tan rápido cambia la ondulación.")]
    public float frecuenciaRuido = 0.5f;
    [Tooltip("Dirección inicial en grados (0 = derecha, 90 = arriba). -1 = aleatoria.")]
    public float direccionInicialGrados = -1f;

    [Header("Límites de la sala")]
    [Tooltip("Busca sola el RoomConfiner que contiene su posición inicial.")]
    public bool autoDetectarSala = true;
    [Tooltip("Collider de la sala (si se asigna, ignora la auto-detección).")]
    public Collider2D limitesSala;

    [Header("Daño a Romerito")]
    public int danio = 1;
    [Tooltip("Impulso horizontal que recibe Romerito al tocarla.")]
    public float fuerzaEmpuje = 10f;
    [Tooltip("Componente vertical del empuje (0 = solo horizontal).")]
    public float empujeVertical = 0.6f;

    [Header("Dispersión con el Macahuitl")]
    [Tooltip("Si es false, el Macahuitl la atraviesa (pincho volador).")]
    public bool destructible = true;
    [Tooltip("Segundos hasta reaparecer en el ancla.")]
    public float tiempoReaparicion = 5f;
    [Tooltip("Hijo con el sprite y el glow.")]
    public GameObject visual;
    [Tooltip("Particle System hijo (hermano de Visual) con un Burst de chispas.")]
    public ParticleSystem chispasDispersion;
    [Tooltip("Particle System de la estela (hermano de Visual). Opcional.")]
    public ParticleSystem estela;
    [Tooltip("Alternativa: prefab de chispas que se instancia al dispersarse.")]
    public GameObject efectoDispersion;
    public float duracionEfecto = 0.6f;

    [Header("Feedback visual")]
    [Tooltip("Escala máxima del 'pop' al recibir el golpe.")]
    public float escalaPop = 1.35f;
    [Tooltip("Duración del pop antes de dispersarse (s).")]
    public float duracionPop = 0.08f;
    [Tooltip("Duración del crecimiento al reaparecer (s). Sin hitbox mientras crece.")]
    public float duracionAparicion = 0.4f;

    [Header("Vínculo con brasero (opcional)")]
    [Tooltip("Si este brasero está encendido, la brasa se extingue para siempre.")]
    public Brasero braseroVinculado;

    // ── Internos ─────────────────────────────────────────────
    private Rigidbody2D rb;
    private Collider2D hitbox;
    private Vector2 ancla;
    private Vector2 direccionActual;
    private float semilla;
    private float tiempoRuido;
    private float radioCast = 0.3f;
    private Vector2 direccionInicial;
    private const float PIEL = 0.02f;           // margen para no quedar pegada
    private readonly RaycastHit2D[] hits = new RaycastHit2D[4];
    private ContactFilter2D filtroRebote;
    private bool dispersa;
    private bool extinta;
    private Vector3 escalaVisualOriginal = Vector3.one;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;

        ancla = transform.position;
        if (visual != null) escalaVisualOriginal = visual.transform.localScale;

        // Radio de detección = tamaño real del hitbox (con escala).
        CircleCollider2D circulo = hitbox as CircleCollider2D;
        if (circulo != null)
        {
            Vector3 esc = transform.lossyScale;
            radioCast = circulo.radius * Mathf.Max(Mathf.Abs(esc.x), Mathf.Abs(esc.y));
        }
        else
        {
            radioCast = Mathf.Min(hitbox.bounds.extents.x, hitbox.bounds.extents.y);
        }

        filtroRebote = new ContactFilter2D();
        filtroRebote.SetLayerMask(capasRebote);
        filtroRebote.useTriggers = false;

        // Cada bola con su propio patrón aunque compartan prefab.
        semilla = Random.value * 100f;
        tiempoRuido = Random.value * 10f;

        direccionInicial = direccionInicialGrados >= 0f
            ? (Vector2)(Quaternion.Euler(0f, 0f, direccionInicialGrados) * Vector2.right)
            : Random.insideUnitCircle.normalized;
        if (direccionInicial.sqrMagnitude < 0.01f) direccionInicial = new Vector2(1f, 1f).normalized;
        direccionActual = EvitarEjes(direccionInicial.normalized);
    }

    void Start()
    {
        if (limitesSala == null && autoDetectarSala) limitesSala = BuscarSala(ancla);
        if (BraseroEncendido()) Extinguir(false);
    }

    void FixedUpdate()
    {
        if (extinta) return;

        if (BraseroEncendido())
        {
            Extinguir(true);
            return;
        }

        if (dispersa || PausaMundo.Activa || PausaMundo.Congelado) return;

        // Tiempo propio: si el mundo se pausa, el patrón se congela y
        // continúa exactamente donde iba, sin "saltos" al reanudar.
        float dt = Time.fixedDeltaTime;
        tiempoRuido += dt * frecuenciaRuido;

        // Ondulación leve entre rebotes (le da vida sin volverla ilegible).
        if (ondulacion > 0f)
        {
            float giro = (Mathf.PerlinNoise(semilla, tiempoRuido) * 2f - 1f) * ondulacion * dt;
            direccionActual = Rotar(direccionActual, giro);
        }

        Vector2 pos = rb.position;
        float restante = velocidad * dt;

        // Hasta 3 rebotes por paso (esquinas).
        for (int i = 0; i < 3 && restante > 0.0001f; i++)
        {
            int n = Physics2D.CircleCast(pos, radioCast, direccionActual, filtroRebote, hits, restante + PIEL);
            RaycastHit2D hit = PrimerImpactoValido(n);

            if (!hit)
            {
                pos += direccionActual * restante;
                restante = 0f;
                break;
            }

            float avance = Mathf.Max(0f, hit.distance - PIEL);
            pos += direccionActual * avance;
            restante -= avance;
            direccionActual = Rebotar(direccionActual, hit.normal);

            // Atascada (empezó tocando): sale empujada por la normal.
            if (hit.distance <= PIEL) pos += hit.normal * PIEL;
        }

        // Límite de sala: si la siguiente posición queda fuera, rebota
        // hacia dentro (salidas abiertas, pozos, huecos en el techo).
        if (limitesSala != null && !limitesSala.OverlapPoint(pos))
        {
            Vector2 borde = limitesSala.ClosestPoint(pos);
            Vector2 normal = borde - pos;
            if (normal.sqrMagnitude < 0.0001f) normal = (Vector2)limitesSala.bounds.center - pos;
            direccionActual = Rebotar(direccionActual, normal.normalized);
            pos = rb.position;   // no cruza el límite este paso
        }

        rb.MovePosition(pos);
    }

    // ── Rebote ───────────────────────────────────────────────
    private RaycastHit2D PrimerImpactoValido(int n)
    {
        RaycastHit2D mejor = default;
        float menor = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit2D h = hits[i];
            if (h.collider == null || h.collider == hitbox) continue;
            if (h.collider.attachedRigidbody == rb) continue;
            if (h.distance < menor) { menor = h.distance; mejor = h; }
        }
        return mejor;
    }

    private Vector2 Rebotar(Vector2 dir, Vector2 normal)
    {
        Vector2 r = Vector2.Reflect(dir, normal);
        r = Rotar(r, Random.Range(-desvioRebote, desvioRebote));

        // Garantiza que salga alejándose de la superficie.
        if (Vector2.Dot(r, normal) < 0.2f)
            r = (r + normal * 0.6f).normalized;

        return EvitarEjes(r.normalized);
    }

    /// Empuja la dirección fuera de las franjas casi horizontales/verticales.
    private Vector2 EvitarEjes(Vector2 dir)
    {
        if (anguloMinimoEje <= 0f) return dir;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;           // -180..180
        float cuadrante = Mathf.Floor((ang + 360f) % 360f / 90f) * 90f;   // 0,90,180,270
        float dentro = ((ang + 360f) % 360f) - cuadrante;                  // 0..90
        dentro = Mathf.Clamp(dentro, anguloMinimoEje, 90f - anguloMinimoEje);
        float rad = (cuadrante + dentro) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static Vector2 Rotar(Vector2 v, float grados)
    {
        float r = grados * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static Collider2D BuscarSala(Vector2 punto)
    {
        RoomConfiner[] salas = Object.FindObjectsByType<RoomConfiner>(FindObjectsSortMode.None);
        foreach (RoomConfiner sala in salas)
        {
            Collider2D c = sala.GetComponent<Collider2D>();
            if (c != null && c.OverlapPoint(punto)) return c;
        }
        return null;
    }

    // ── Daño por contacto ────────────────────────────────────
    // Stay (no Enter): si Romerito sigue dentro cuando acaban sus
    // i-frames, vuelve a recibir daño. PuedeRecibirDano filtra
    // i-frames, Barrera de Copal y pausa.
    void OnTriggerStay2D(Collider2D other)
    {
        if (dispersa || extinta) return;
        if (!other.CompareTag("Player")) return;

        RomeritoHealth health = other.GetComponent<RomeritoHealth>();
        if (health == null) health = other.GetComponentInParent<RomeritoHealth>();
        if (health == null || !health.PuedeRecibirDano) return;

        Rigidbody2D prb = other.attachedRigidbody;
        if (prb != null && fuerzaEmpuje > 0f)
        {
            float lado = Mathf.Sign(prb.position.x - rb.position.x);
            if (lado == 0f) lado = 1f;
            prb.linearVelocity = Vector2.zero;
            prb.AddForce(new Vector2(lado, empujeVertical) * fuerzaEmpuje, ForceMode2D.Impulse);
        }

        health.TakeDamage(danio);
    }

    // ── Macahuitl (lateral y pogo) ───────────────────────────
    public bool RecibirGolpeMacahuitl(Vector2 origenAtacante, int dano, RomeritoCombat.GodFavor favor)
    {
        if (!destructible || dispersa || extinta) return false;
        StartCoroutine(RutinaReaparecer());
        return true;   // conecta: recoil lateral / rebote de pogo
    }

    private IEnumerator RutinaReaparecer()
    {
        dispersa = true;
        if (hitbox != null) hitbox.enabled = false;   // ya no daña durante el pop

        // ── Pop: se hincha un instante (lee como "le di") ──
        if (visual != null && duracionPop > 0f)
        {
            float p = 0f;
            while (p < duracionPop)
            {
                if (!PausaMundo.Activa) p += Time.deltaTime;
                float k = Mathf.Clamp01(p / duracionPop);
                visual.transform.localScale = escalaVisualOriginal * Mathf.Lerp(1f, escalaPop, k);
                yield return null;
            }
        }

        SpawnEfecto();
        SetVisible(false);
        if (visual != null) visual.transform.localScale = escalaVisualOriginal;

        // Tiempo de juego (no realtime): durante una pausa no reaparece.
        float t = 0f;
        while (t < tiempoReaparicion)
        {
            if (!PausaMundo.Activa) t += Time.deltaTime;
            yield return null;
        }

        if (extinta) yield break;

        rb.position = ancla;
        transform.position = ancla;
        direccionActual = EvitarEjes(Random.insideUnitCircle.normalized);

        // ── Aparición gradual: crece desde 0, todavía SIN hitbox ──
        // Así nunca "aparece encima" de Romerito sin aviso.
        if (visual != null)
        {
            visual.SetActive(true);
            float a = 0f;
            while (a < duracionAparicion)
            {
                if (extinta) yield break;
                if (!PausaMundo.Activa) a += Time.deltaTime;
                float k = Mathf.Clamp01(a / Mathf.Max(duracionAparicion, 0.0001f));
                visual.transform.localScale = escalaVisualOriginal * Mathf.SmoothStep(0f, 1f, k);
                yield return null;
            }
            visual.transform.localScale = escalaVisualOriginal;
        }

        SetVisible(true);
        dispersa = false;
    }

    // ── Brasero ──────────────────────────────────────────────
    private bool BraseroEncendido()
    {
        return braseroVinculado != null && braseroVinculado.EstaEncendido;
    }

    private void Extinguir(bool conEfecto)
    {
        extinta = true;
        StopAllCoroutines();
        if (conEfecto && !dispersa) SpawnEfecto();
        SetVisible(false);
    }

    // ── Utilidades ───────────────────────────────────────────
    private void SetVisible(bool valor)
    {
        if (visual != null) visual.SetActive(valor);
        if (hitbox != null) hitbox.enabled = valor;

        // La estela deja de emitir pero conserva sus chispas vivas,
        // que se apagan solas (World space) en vez de desaparecer de golpe.
        if (estela != null)
        {
            if (valor) { if (!estela.isPlaying) estela.Play(true); }
            else estela.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SpawnEfecto()
    {
        if (chispasDispersion != null)
        {
            chispasDispersion.Clear(true);
            chispasDispersion.Play(true);
        }

        if (efectoDispersion != null)
        {
            GameObject fx = Instantiate(efectoDispersion, transform.position, Quaternion.identity);
            Destroy(fx, duracionEfecto);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 centro = Application.isPlaying ? (Vector3)ancla : transform.position;

        // Hitbox / radio de rebote
        float r = radioCast;
        if (!Application.isPlaying)
        {
            CircleCollider2D c = GetComponent<CircleCollider2D>();
            if (c != null)
                r = c.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        }
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        Gizmos.DrawWireSphere(centro, r);

        // Dirección inicial (si es fija)
        if (direccionInicialGrados >= 0f)
        {
            Vector3 d = Quaternion.Euler(0f, 0f, direccionInicialGrados) * Vector3.right;
            Gizmos.DrawLine(centro, centro + d * 1.5f);
        }

        // Límites de sala
        Collider2D sala = limitesSala;
        if (sala == null && autoDetectarSala) sala = BuscarSala(centro);
        if (sala != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Bounds b = sala.bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
#endif
}
