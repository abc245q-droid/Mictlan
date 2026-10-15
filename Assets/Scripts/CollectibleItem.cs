using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  CollectibleItem — semillas de cacao / tajaderas
//  [CACAO v2] Caída física natural
// ============================================================
//
//  ANTES: al tocar el suelo la semilla se volvía Kinematic y
//  flotaba a 'hoverHeight' sobre el piso con una onda senoidal.
//
//  AHORA: es un cuerpo físico de verdad.
//    1. Sale disparada con un arco aleatorio y GIRA en el aire.
//    2. Rebota en el suelo y paredes, cada rebote más bajo
//       (material físico propio: rebote + fricción).
//    3. Rueda/derrapa un poco y se ASIENTA: se duerme y se queda
//       quieta en el piso.
//    4. Sigue siendo Dynamic: si el suelo desaparece (plataforma
//       inestable, puerta rota) vuelve a caer sola.
//
//  SETUP (el mismo de antes, sin cambios obligatorios):
//    • Rigidbody2D (Dynamic). Gravedad, interpolación y detección
//      continua se configuran solas desde aquí.
//    • groundCollider: SÓLIDO, con Exclude Layers = capa del Jugador.
//      Recomendado CircleCollider2D o CapsuleCollider2D (rueda mejor
//      que una caja).
//    • pickupTrigger: TRIGGER para recolectar (en el aire o en el piso).
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
public class CollectibleItem : MonoBehaviour
{
    public enum CurrencyType { Cacao, Tajadera }

    [Header("Configuración del Item")]
    public CurrencyType type;
    public int value = 1;

    [Header("Salida (al soltarla el enemigo)")]
    [Tooltip("Impulso vertical base al salir.")]
    public float initialJumpForce = 5f;
    [Tooltip("Variación aleatoria del impulso vertical (±).")]
    public float variacionSalto = 1.2f;
    [Tooltip("Velocidad lateral máxima al salir (aleatoria entre -x y x).")]
    public float velocidadLateral = 2.2f;
    [Tooltip("Giro máximo en el aire (grados/s, aleatorio).")]
    public float giroMaximo = 540f;

    [Header("Física de Caída")]
    [Tooltip("Escala de gravedad. Más alta = cae más 'pesada'.")]
    public float gravedad = 2.5f;
    [Tooltip("Rebote (0 = se queda pegada, 1 = rebota igual de alto).")]
    [Range(0f, 1f)] public float rebote = 0.35f;
    [Tooltip("Fricción con el suelo: frena el derrape y la rodada.")]
    [Range(0f, 1f)] public float friccion = 0.6f;
    [Tooltip("Freno del giro (amortigua el rodar).")]
    public float frenoGiro = 1.5f;
    [Tooltip("Tope de velocidad de caída (evita atravesar plataformas finas).")]
    public float velocidadMaximaCaida = 18f;

    [Header("Asentarse")]
    [Tooltip("Por debajo de esta velocidad, apoyada en el suelo, empieza a asentarse.")]
    public float velocidadReposo = 0.25f;
    [Tooltip("Segundos casi quieta en el suelo antes de dormirse.")]
    public float tiempoParaAsentarse = 0.2f;
    [Tooltip("Al asentarse, gira suavemente hasta quedar derecha. " +
             "Apágalo si prefieres que quede 'tirada' en el ángulo en que cayó.")]
    public bool enderezarAlAsentarse = true;
    public float velocidadEnderezar = 10f;

    [Header("Efectos Visuales")]
    public GameObject pickupEffect;

    [Header("Colliders")]
    [Tooltip("Collider SÓLIDO: solo debe colisionar con el suelo. " +
             "En 'Layer Overrides' de este collider configura " +
             "'Exclude Layers' = capa del Jugador, para que nunca empuje a Romerito.")]
    public Collider2D groundCollider;
    [Tooltip("Collider TRIGGER: detecta al jugador para recolectar el ítem, " +
             "tanto en el aire como ya en el suelo.")]
    public Collider2D pickupTrigger;

    // ── Estado ───────────────────────────────────────────────
    private Rigidbody2D rb;
    private bool apoyada = false;      // tocando suelo este paso de física
    private bool asentada = false;
    private float timerReposo = 0f;
    private bool recolectada = false;
    private bool enderezada = false;   // ya se durmió una vez tras enderezarse

    // [MAZORCA] Velocidad de salida impuesta por quien la suelta.
    private bool salidaFijada = false;
    private Vector2 velocidadSalida;

    /// <summary>
    /// Llamar justo después de Instantiate para elegir la velocidad de
    /// salida (ej. la mazorca lanza las semillas lejos de Romerito).
    /// Si no se llama, sale con el arco aleatorio de siempre.
    /// </summary>
    public void FijarSalida(Vector2 velocidad)
    {
        salidaFijada = true;
        velocidadSalida = velocidad;
        if (rb != null) rb.linearVelocity = velocidad;
    }

    // Un material por combinación (rebote, fricción): cacao y tajadera
    // pueden tener valores distintos sin crear uno nuevo por cada drop.
    private static readonly Dictionary<Vector2, PhysicsMaterial2D> materiales =
        new Dictionary<Vector2, PhysicsMaterial2D>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (groundCollider == null)
        {
            foreach (Collider2D c in GetComponents<Collider2D>())
                if (!c.isTrigger) { groundCollider = c; break; }
        }
    }

    void Start()
    {
        // Configuración física (sobrescribe lo que tenga el prefab).
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravedad;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.angularDamping = frenoGiro;
        rb.constraints = RigidbodyConstraints2D.None;

        if (groundCollider != null)
            groundCollider.sharedMaterial = ObtenerMaterial();

        // Salida en arco aleatorio con giro.
        if (salidaFijada)
        {
            rb.linearVelocity = velocidadSalida;
        }
        else
        {
            float vy = initialJumpForce + Random.Range(-variacionSalto, variacionSalto);
            float vx = Random.Range(-velocidadLateral, velocidadLateral);
            rb.linearVelocity = new Vector2(vx, Mathf.Max(0.5f, vy));
        }
        rb.angularVelocity = Random.Range(-giroMaximo, giroMaximo);
    }

    PhysicsMaterial2D ObtenerMaterial()
    {
        Vector2 clave = new Vector2(rebote, friccion);
        if (!materiales.TryGetValue(clave, out PhysicsMaterial2D m) || m == null)
        {
            m = new PhysicsMaterial2D("CacaoFisico") { bounciness = rebote, friction = friccion };
            materiales[clave] = m;
        }
        return m;
    }

    void FixedUpdate()
    {
        if (recolectada) return;

        // Si ya estaba asentada y algo la movió (suelo que desaparece,
        // un golpe), vuelve a ser una semilla "viva".
        if (asentada)
        {
            if (!rb.IsSleeping() && rb.linearVelocity.sqrMagnitude > velocidadReposo * velocidadReposo * 4f)
                Despertar();
            else
            {
                Enderezar();
                apoyada = false;
                return;
            }
        }

        // Tope de caída.
        if (rb.linearVelocity.y < -velocidadMaximaCaida)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -velocidadMaximaCaida);

        // ¿Casi quieta y apoyada? → contar para asentarse.
        bool lenta = rb.linearVelocity.magnitude < velocidadReposo &&
                     Mathf.Abs(rb.angularVelocity) < 30f;
        if (apoyada && lenta)
        {
            timerReposo += Time.fixedDeltaTime;
            if (timerReposo >= tiempoParaAsentarse) Asentar();
        }
        else timerReposo = 0f;

        apoyada = false;   // OnCollisionStay2D lo vuelve a marcar
    }

    void OnCollisionStay2D(Collision2D col)    { RevisarApoyo(col); }
    void OnCollisionEnter2D(Collision2D col)   { RevisarApoyo(col); }

    // Solo cuenta como "suelo" un contacto cuya normal apunte hacia arriba
    // (no paredes ni techos). groundCollider tiene excluido al Jugador,
    // así que esto solo puede venir del escenario.
    void RevisarApoyo(Collision2D col)
    {
        for (int i = 0; i < col.contactCount; i++)
        {
            if (col.GetContact(i).normal.y > 0.5f)
            {
                apoyada = true;
                return;
            }
        }
    }

    void Asentar()
    {
        asentada = true;
        timerReposo = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        // Se queda Dynamic (si el suelo se va, cae), pero sin rodar:
        // freno de giro alto mientras está asentada.
        rb.angularDamping = 25f;
        if (!enderezarAlAsentarse) rb.Sleep();
    }

    void Despertar()
    {
        asentada = false;
        enderezada = false;
        rb.angularDamping = frenoGiro;
    }

    void Enderezar()
    {
        if (!enderezarAlAsentarse) return;
        float z = Mathf.DeltaAngle(0f, rb.rotation);
        if (Mathf.Abs(z) < 0.5f)
        {
            // Dormir UNA sola vez: si luego algo la despierta (el suelo se
            // va), no la volvemos a dormir y la gravedad hace su trabajo.
            if (!enderezada) { enderezada = true; rb.MoveRotation(0f); rb.Sleep(); }
            return;
        }
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, 0f, velocidadEnderezar * Time.fixedDeltaTime));
    }

    // ── Recolección ──────────────────────────────────────────
    // pickupTrigger es SIEMPRE trigger (en el aire y en el suelo),
    // así que la recolección nunca genera impulso físico sobre Romerito.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (recolectada) return;
        if (other.CompareTag("Player"))
            Collect(other.gameObject);
    }

    void Collect(GameObject player)
    {
        Monedero inventory = player.GetComponent<Monedero>();
        if (inventory == null) inventory = player.GetComponentInParent<Monedero>();
        if (inventory == null) return;

        recolectada = true;

        if (type == CurrencyType.Cacao) inventory.AddCacao(value);
        else if (type == CurrencyType.Tajadera) inventory.AddTajadera(value);

        if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
