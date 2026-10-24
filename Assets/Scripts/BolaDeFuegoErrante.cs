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
//    • Recorrido errático con ruido Perlin dentro de un círculo
//      (ancla + radio). Impredecible pero SUAVE: nunca da saltos ni
//      cambios bruscos, el jugador siempre puede leerla.
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
//    • Selecciona el objeto en la escena para ver el círculo de
//      patrulla (Gizmo naranja) y evitar que dos bolas se encimen.
//
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BolaDeFuegoErrante : MonoBehaviour, IGolpeableMacahuitl
{
    [Header("Zona de patrulla")]
    [Tooltip("Radio del área donde vaga, alrededor de su posición inicial.")]
    public float radio = 4f;
    [Tooltip("Velocidad de vuelo (m/s).")]
    public float velocidad = 2.5f;
    [Tooltip("Qué tan rápido cambia de rumbo. Bajo = curvas amplias y legibles.")]
    public float frecuenciaRuido = 0.35f;
    [Tooltip("Fuerza con la que regresa al ancla al acercarse al borde del radio.")]
    public float fuerzaRetorno = 3f;
    [Tooltip("Suavizado del giro. Más alto = responde más rápido al ruido.")]
    public float suavizadoGiro = 4f;

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
    private float semillaX, semillaY;
    private float tiempoRuido;
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

        // Cada bola con su propio patrón aunque compartan prefab.
        semillaX = Random.value * 100f;
        semillaY = Random.value * 100f + 50f;
        tiempoRuido = Random.value * 10f;
        direccionActual = Random.insideUnitCircle.normalized;
    }

    void Start()
    {
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
        tiempoRuido += Time.fixedDeltaTime * frecuenciaRuido;

        Vector2 deseo = new Vector2(
            Mathf.PerlinNoise(semillaX, tiempoRuido) * 2f - 1f,
            Mathf.PerlinNoise(semillaY, tiempoRuido) * 2f - 1f);

        // Retorno suave: casi nulo en el centro, fuerte cerca del borde.
        Vector2 offset = rb.position - ancla;
        float borde = Mathf.Clamp01(offset.magnitude / Mathf.Max(radio, 0.01f));
        if (offset.sqrMagnitude > 0.0001f)
            deseo += -offset.normalized * borde * borde * fuerzaRetorno;

        if (deseo.sqrMagnitude > 0.0001f)
            direccionActual = Vector2.Lerp(direccionActual, deseo.normalized,
                                           suavizadoGiro * Time.fixedDeltaTime).normalized;

        rb.MovePosition(rb.position + direccionActual * velocidad * Time.fixedDeltaTime);
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
        direccionActual = Random.insideUnitCircle.normalized;

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
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
        Gizmos.DrawWireSphere(centro, radio);
    }
#endif
}
