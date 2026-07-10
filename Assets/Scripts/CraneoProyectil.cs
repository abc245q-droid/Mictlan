using UnityEngine;

// ============================================================
//  CraneoProyectil — El cráneo lanzado por el Mictecah Lanza Cráneos
// ============================================================
//
//  Idea de diseño (Chema):
//    El Mictecah Lanza Cráneos escupe cráneos en ARCO. Mientras vuelan
//    son un proyectil balístico. Al ATERRIZAR, el cráneo "despierta" y
//    se convierte en un Cráneo Patrullero normal — el mismo enemigo que
//    el jugador ya conoce. La munición ES un enemigo que sabe leer.
//
//  Control de población (POR LANZADOR, no global):
//    Cada cráneo pertenece a UN lanzador (su "dueño"). El cupo de
//    patrulleros vivos se cuenta por-lanzador, NO globalmente. Así:
//      • Varios lanzadores en escena no comparten cupo entre sí.
//      • Los patrulleros NATIVOS del nivel (colocados a mano) no cuentan.
//      • Patrulleros de otras salas jamás interfieren.
//    Si el cupo del dueño está lleno, el cráneo que aterriza se disuelve
//    en vez de acumularse.
//
//  Home Run (golpear en vuelo):
//    Romerito puede batear un cráneo EN VUELO. RomeritoCombat detecta el
//    CraneoProyectil volando y llama a Redirigir(dir) en lugar de dañarlo.
//    El cráneo sale disparado en la dirección del golpe (como pelota de
//    baseball) y sigue siendo un proyectil: al aterrizar patrulla o se
//    disuelve según el cupo, igual que si lo hubiera lanzado el Mictecah.
//
//  CÓMO SE USA:
//    Vive en el MISMO prefab del Mictecah1_Patrullero, DESACTIVADO por
//    defecto. El Lanza Cráneos lo instancia, lo activa y llama Lanzar(...).
//
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
public class CraneoProyectil : MonoBehaviour
{
    [Header("── Vuelo ──")]
    [Tooltip("Capa de suelo/paredes. Debe coincidir con la de la base.")]
    public LayerMask groundLayer;
    [Tooltip("Largo del rayo hacia abajo para detectar el aterrizaje.")]
    public float rayoAterrizaje = 0.35f;
    [Tooltip("Daño por contacto MIENTRAS vuela (antes de aterrizar).")]
    public int danoEnVuelo = 1;
    [Tooltip("Empuje que aplica a Romerito si lo golpea en vuelo.")]
    public float knockbackEnVuelo = 8f;
    [Tooltip("Velocidad de giro visual del cráneo mientras vuela (grados/seg).")]
    public float velocidadGiro = 360f;
    [Tooltip("Seguro anti-atasco: si vuela más de este tiempo sin aterrizar, " +
             "resuelve igual (se convierte o se destruye).")]
    public float tiempoMaxVuelo = 6f;

    [Header("── Home Run (bateo en vuelo) ──")]
    [Tooltip("Rapidez con la que sale el cráneo al ser bateado por Romerito.")]
    public float velocidadRedireccion = 14f;
    [Tooltip("Componente hacia arriba que se añade al bateo (para que describa " +
             "un nuevo arco en vez de salir totalmente plano).")]
    public float alzaRedireccion = 4f;
    [Tooltip("Un cráneo bateado, ¿puede dañar a OTROS enemigos al impactarlos? " +
             "Si está activo, se convierte en un mini-proyectil ofensivo.")]
    public bool bateadoDanaEnemigos = true;
    [Tooltip("Daño a otros enemigos si bateadoDanaEnemigos está activo.")]
    public int danoBateadoAEnemigos = 1;

    [Header("── Efectos ──")]
    [Tooltip("VFX opcional al aterrizar y despertar como patrullero.")]
    public GameObject vfxAterrizaje;
    [Tooltip("VFX opcional al destruirse por cupo lleno.")]
    public GameObject vfxDisolver;
    [Tooltip("VFX opcional al ser bateado (chispas del impacto).")]
    public GameObject vfxBateo;

    // ── Estado interno ───────────────────────────────────────
    private Rigidbody2D rb;
    private MictecahBase cerebroPatrullero;
    private EnemyDummy dummy;
    private Collider2D col;
    private bool volando = false;
    private bool yaResuelto = false;
    private bool fueBateado = false;
    private float vueloTimer;
    private float gravedadOriginal;
    private bool muerteEnganchada = false;

    // Dueño de este cráneo (el lanzador que lo escupió). Gestiona el cupo.
    private MictecahLanzaCraneos dueno;

    // ¿Está este cráneo volando ahora mismo? Lo consulta RomeritoCombat
    // para decidir entre BATEAR (vuelo) o hacer daño normal (ya patrulla).
    public bool EstaVolando => volando && !yaResuelto;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        cerebroPatrullero = GetComponent<MictecahBase>();
        dummy = GetComponent<EnemyDummy>();
        col = GetComponent<Collider2D>();
        gravedadOriginal = rb.gravityScale;

        // Si este componente está activo (instanciado como proyectil),
        // apagamos el cerebro patrullero YA, antes de que su Start pueda
        // hacerlo patrullar o dañar durante el primer frame de vuelo.
        if (enabled && cerebroPatrullero != null)
            cerebroPatrullero.enabled = false;
    }

    // Signo de la trayectoria intencionada del vuelo: +1 derecha, -1 izquierda.
    // Se captura al Lanzar/Redirigir y se usa en Aterrizar para orientar al
    // patrullero. NO usar rb.linearVelocity.x al aterrizar: si el cráneo
    // rebota contra una pared antes de tocar suelo, la física de Unity ya
    // invirtió vx cuando llegamos a Aterrizar y el patrullero despierta al
    // revés (bug del "sortear al lanzador por arriba").
    private int signoTrayectoria = 1;

    // ── API pública: el Lanza Cráneos llama esto al instanciar ──
    public void Lanzar(Vector2 velocidadInicial, MictecahLanzaCraneos lanzador)
    {
        dueno = lanzador;

        if (cerebroPatrullero != null) cerebroPatrullero.enabled = false;

        rb.gravityScale = gravedadOriginal > 0f ? gravedadOriginal : 1f;
        rb.linearVelocity = velocidadInicial;

        // Congelar el signo de la trayectoria. Se prefiere el vx inicial;
        // si es despreciable (lanzamiento vertical puro), respetamos el
        // facing actual del lanzador.
        if (Mathf.Abs(velocidadInicial.x) > 0.05f)
            signoTrayectoria = velocidadInicial.x > 0f ? 1 : -1;
        else if (transform.localScale.x != 0f)
            signoTrayectoria = transform.localScale.x > 0f ? 1 : -1;

        volando = true;
        yaResuelto = false;
        fueBateado = false;
        vueloTimer = tiempoMaxVuelo;
    }

    // ── API pública: Romerito batea el cráneo en vuelo ───────
    //   direccionGolpe: dirección del ataque de Romerito (ya normalizada
    //   o no; la normalizamos aquí). El cráneo sale como pelota de béisbol.
    public void Redirigir(Vector2 direccionGolpe)
    {
        if (!EstaVolando) return;

        fueBateado = true;
        vueloTimer = tiempoMaxVuelo;   // reinicia el seguro anti-atasco

        Vector2 dir = direccionGolpe.sqrMagnitude > 0.001f
            ? direccionGolpe.normalized
            : new Vector2(Mathf.Sign(transform.localScale.x), 0f);

        // Añadimos alza para que trace un nuevo arco… pero SOLO si el bateo
        // no va predominantemente hacia abajo (pogo). Si va hacia abajo, el
        // alza lo contrarrestaría y anularía el "clavado" al suelo.
        Vector2 nuevaVel = dir * velocidadRedireccion;
        if (dir.y > -0.5f)
            nuevaVel += Vector2.up * alzaRedireccion;
        rb.linearVelocity = nuevaVel;

        // El bateo redefine la trayectoria intencionada.
        if (Mathf.Abs(nuevaVel.x) > 0.05f)
            signoTrayectoria = nuevaVel.x > 0f ? 1 : -1;

        if (vfxBateo != null)
            Instantiate(vfxBateo, transform.position, Quaternion.identity);
    }

    void Update()
    {
        if (!volando) return;

        transform.Rotate(0f, 0f, velocidadGiro * Time.deltaTime);

        vueloTimer -= Time.deltaTime;
        if (vueloTimer <= 0f) { Aterrizar(); return; }

        if (rb.linearVelocity.y <= 0.1f)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                transform.position, Vector2.down, rayoAterrizaje, groundLayer);
            if (hit.collider != null)
                Aterrizar();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!volando || yaResuelto) return;

        // Romerito: daño por contacto en vuelo (solo si NO fue bateado por él;
        // un cráneo bateado no debería castigar al que lo bateó).
        if (collision.gameObject.CompareTag("Player"))
        {
            if (fueBateado) return;

            RomeritoHealth vida = collision.gameObject.GetComponent<RomeritoHealth>();
            if (vida != null) vida.TakeDamage(danoEnVuelo);

            Rigidbody2D prb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (prb != null)
            {
                Vector2 dir = (collision.transform.position - transform.position).normalized;
                dir.y = 0.4f;
                prb.AddForce(dir * knockbackEnVuelo, ForceMode2D.Impulse);
            }

            Disolver();
            return;
        }

        // Cráneo bateado que golpea a OTRO enemigo → le hace daño y se disuelve.
        if (fueBateado && bateadoDanaEnemigos)
        {
            EnemyDummy otro = collision.gameObject.GetComponent<EnemyDummy>();
            if (otro != null && otro != dummy)
            {
                otro.TakeDamage(danoBateadoAEnemigos);
                Disolver();
                return;
            }
        }

        // Suelo/pared → aterrizar.
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            Aterrizar();
    }

    // ── Aterrizaje: convertirse en patrullero… o disolverse ──
    private void Aterrizar()
    {
        if (yaResuelto) return;
        yaResuelto = true;
        volando = false;

        // ¿Hay cupo EN EL DUEÑO? Sin dueño (caso raro) usamos un fallback
        // permisivo: se convierte. El cupo lo administra el lanzador.
        bool hayCupo = (dueno == null) || dueno.HayCupoLibre();

        if (!hayCupo)
        {
            Disolver();
            return;
        }

        // Ocupar una plaza en el dueño y enganchar su liberación a la muerte.
        if (dueno != null) dueno.RegistrarHijo();
        EngancharMuerte();

        // ══════════════════════════════════════════════════════════════
        //  FIX v2 — Dirección al aterrizar.
        //  MictecahBase.Start() (que corre al reactivar el cerebro más
        //  abajo) lee el facing desde `transform.localScale.x`. Aplicamos
        //  aquí el signo verdadero de la trayectoria, capturado al
        //  Lanzar/Redirigir en `signoTrayectoria`.
        //
        //  Por qué no usar sign(rb.linearVelocity.x) aquí: si el cráneo
        //  rebota contra una pared antes de tocar suelo (típico al saltar
        //  por arriba del lanzador — la parábola choca con el techo o
        //  con una pared aledaña), la física de Unity ya invirtió vx
        //  cuando este método se ejecuta desde OnCollisionEnter2D → el
        //  patrullero despertaba al revés.
        // ══════════════════════════════════════════════════════════════
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * signoTrayectoria;
            transform.localScale = s;
        }

        transform.rotation = Quaternion.identity;
        rb.angularVelocity = 0f;
        rb.linearVelocity = Vector2.zero;

        if (vfxAterrizaje != null)
            Instantiate(vfxAterrizaje, transform.position, Quaternion.identity);

        if (cerebroPatrullero != null) cerebroPatrullero.enabled = true;
        this.enabled = false;
    }

    // ── Cupo: liberar la plaza del dueño cuando este patrullero muere ──
    private void EngancharMuerte()
    {
        if (muerteEnganchada || dummy == null) return;
        muerteEnganchada = true;
        dummy.OnDeath += LiberarPlaza;
    }

    private void LiberarPlaza()
    {
        if (dueno != null) dueno.LiberarHijo();
        if (dummy != null) dummy.OnDeath -= LiberarPlaza;
    }

    private void Disolver()
    {
        if (vfxDisolver != null)
            Instantiate(vfxDisolver, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (muerteEnganchada && dummy != null)
            dummy.OnDeath -= LiberarPlaza;
    }
}
