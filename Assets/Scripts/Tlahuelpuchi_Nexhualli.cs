using UnityEngine;

// ============================================================
//  Tlahuelpuchi — Variante NEXHUALLI (bola de fuego)
// ============================================================
//
//  Contexto cultural:
//    La Tlahuelpuchi es la bruja-vampiro del folclore tlaxcalteca.
//    Su rasgo más citado en la etnografía (Nutini & Roberts,
//    "Bloodsucking Witchcraft") es que se desprende de sus piernas
//    y viaja de noche como una LUMBRE / BOLA DE FUEGO para acechar
//    a sus víctimas. Esa lumbre es el "nexhualli" de esta variante.
//    NO confundir con el Tlahuipuchtli (ritualista de copal/fuego):
//    son entidades distintas y en Mictlán deben mantenerse separadas.
//
//  Comportamiento (diseño de Chema):
//    • Patrulla terrestre LENTA (hereda de MictecahBase).
//    • Al detectar a Romerito: telegraph en tierra (tiembla / se
//      enciende) y se TRANSFORMA en bola de fuego → enemigo volador.
//    • Vuelo: ciclo TEMBLOR (deriva lenta con jitter Perlin) →
//      SACUDIDA (mini-sprint recto hacia Romerito) → temblor →
//      sacudida... acercándose cada vez más (ver diagrama de Chema).
//    • Cuando está lo bastante cerca (o agota sus sacudidas) lanza
//      un ATAQUE FINAL: embestida más rápida y comprometida.
//    • Tras el ataque (acierte o no) REGRESA a su forma terrestre:
//      cae, aterriza y queda VULNERABLE durante un enfriamiento
//      antes de poder transformarse otra vez.
//    • INMUNE en forma de bola de fuego (EnemyDummy.SetInvulnerable).
//      Solo puede ser dañada en su forma terrestre.
//    • Golpearla DURANTE el telegraph de transformación la interrumpe
//      y castiga con un enfriamiento (recompensa a la agresividad).
//
//  Requisitos del prefab (además de los de MictecahBase):
//    Rigidbody2D (Dynamic) + Collider2D + EnemyDummy + este script
//    + hijo "GroundCheck" en el borde frontal-inferior.
//    Opcional: sprites de cada forma y un hijo de VFX para el fuego.
//
//  Notas de diseño:
//    • El Pogo de Romerito SÍ rebota sobre la bola de fuego, pero no
//      la daña (RomeritoCombat rebota aunque TakeDamage se ignore).
//      Es intencional: da al jugador una herramienta de evasión
//      vertical estilo Hollow Knight sin regalarle daño.
//    • La bola de fuego respeta las colisiones con el suelo/paredes
//      (no atraviesa muros): si una sacudida choca, el timer de fase
//      la saca del atasco de forma natural.
//
// ============================================================

public class Tlahuelpuchi_Nexhualli : MictecahBase
{
    // ── Fases internas de la persecución ─────────────────────
    private enum FaseNexhualli
    {
        Transformando,   // Telegraph en tierra (vulnerable, interrumpible)
        Temblor,         // Vuelo: deriva lenta + jitter (inmune)
        Sacudida,        // Vuelo: mini-sprint hacia Romerito (inmune)
        AtaqueFinal,     // Vuelo: embestida definitiva (inmune)
        Regresando       // Cae y aterriza (VULNERABLE de nuevo)
    }
    private FaseNexhualli fase;
    private float faseTimer;

    [Header("── Transformación (telegraph en tierra) ──")]
    [Tooltip("Duración del aviso antes de convertirse en bola de fuego. " +
             "Ventana en la que el jugador puede interrumpirla golpeándola.")]
    public float duracionTransformacion = 0.7f;
    [Tooltip("Tinte del sprite durante el telegraph (se 'enciende').")]
    public Color colorTelegraph = new Color(1f, 0.55f, 0.1f, 1f);
    [Tooltip("Impulso vertical al despegar como bola de fuego.")]
    public float impulsoDespegue = 4f;

    [Header("── Temblor (vuelo errático lento) ──")]
    [Tooltip("Duración de cada fase de temblor antes de la siguiente sacudida.")]
    public float duracionTemblor = 0.9f;
    [Tooltip("Velocidad de deriva LENTA hacia Romerito durante el temblor.")]
    public float velocidadDeriva = 1.4f;
    [Tooltip("Amplitud del jitter (Perlin) que hace 'temblar' a la lumbre.")]
    public float amplitudTemblor = 2.2f;
    [Tooltip("Frecuencia del jitter. Más alto = vibración más nerviosa.")]
    public float frecuenciaTemblor = 6f;

    [Header("── Sacudida (mini-sprint aéreo) ──")]
    [Tooltip("Velocidad de la sacudida hacia la posición de Romerito.")]
    public float velocidadSacudida = 9f;
    [Tooltip("Duración de cada sacudida. Corta = desplazamiento seco.")]
    public float duracionSacudida = 0.22f;
    [Tooltip("Sacudidas máximas antes de forzar el ataque final.")]
    public int maxSacudidas = 4;

    [Header("── Ataque final ──")]
    [Tooltip("Si tras una sacudida queda a esta distancia o menos, " +
             "lanza el ataque final de inmediato.")]
    public float rangoAtaqueFinal = 2.5f;
    [Tooltip("Velocidad de la embestida final (mayor que la sacudida).")]
    public float velocidadAtaqueFinal = 13f;
    [Tooltip("Duración de la embestida final.")]
    public float duracionAtaqueFinal = 0.35f;
    [Tooltip("Seguro: tiempo máximo total en el aire antes de forzar el regreso.")]
    public float maxTiempoVuelo = 7f;

    [Header("── Enfriamiento (ventana de castigo) ──")]
    [Tooltip("Segundos que tarda en poder transformarse de nuevo tras " +
             "aterrizar. Durante este tiempo patrulla y es VULNERABLE.")]
    public float enfriamientoTransformacion = 3.5f;
    [Tooltip("Enfriamiento aplicado si el jugador la interrumpe " +
             "golpeándola durante el telegraph de transformación.")]
    public float enfriamientoTrasInterrupcion = 2.5f;

    [Header("── Presentación (opcional) ──")]
    [Tooltip("Sprite de la forma terrestre (bruja). Si es null, no se toca.")]
    public Sprite spriteTerrestre;
    [Tooltip("Sprite de la forma de bola de fuego. Si es null, no se toca.")]
    public Sprite spriteBolaDeFuego;
    [Tooltip("Hijo con partículas/glow HDR del fuego. Se activa solo en vuelo.")]
    public GameObject vfxBolaDeFuego;

    // ★ NUEVO [SPRITES]: Las dos formas tienen siluetas muy distintas
    //   (bruja alta y encorvada vs. lumbre compacta y redondeada). Un solo
    //   collider fijo quedaría injusto en una de las dos formas (demasiado
    //   grande o demasiado chico). Redimensionamos el MISMO CapsuleCollider2D
    //   en cada transición — más barato que activar/desactivar dos colliders
    //   y evita duplicar el trabajo de OnCollisionEnter2D.
    [Header("── Collider por Forma ──")]
    [Tooltip("Collider2D principal del enemigo (Capsule). Requerido para " +
             "que el tamaño de golpeo coincida con cada forma.")]
    public CapsuleCollider2D colliderPrincipal;
    [Tooltip("Tamaño del collider en forma terrestre (bruja de pie, alta y angosta).")]
    public Vector2 colliderTamanoTerrestre = new Vector2(0.5f, 1.3f);
    [Tooltip("Offset del collider en forma terrestre (centro del cuerpo, no de los pies).")]
    public Vector2 colliderOffsetTerrestre = new Vector2(0f, 0.65f);
    [Tooltip("Tamaño del collider en forma ígnea (lumbre compacta y redondeada).")]
    public Vector2 colliderTamanoIgneo = new Vector2(0.75f, 0.85f);
    [Tooltip("Offset del collider en forma ígnea (centro de la llama).")]
    public Vector2 colliderOffsetIgneo = new Vector2(0f, 0f);

    // ── Estado interno ───────────────────────────────────────
    private SpriteRenderer sr;
    private float gravedadOriginal;
    private float cooldownRestante;      // > 0 → no puede transformarse
    private float tiempoEnVuelo;
    private int sacudidasHechas;
    private float semillaX, semillaY;    // semillas Perlin (cada instancia tiembla distinto)
    private Color colorOriginal = Color.white;

    private bool EnFormaIgnea =>
        estado == Estado.Persiguiendo &&
        (fase == FaseNexhualli.Temblor ||
         fase == FaseNexhualli.Sacudida ||
         fase == FaseNexhualli.AtaqueFinal);

    // ── Ciclo de vida ────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
        gravedadOriginal = rb.gravityScale;
        semillaX = Random.Range(0f, 1000f);
        semillaY = Random.Range(0f, 1000f);
    }

    protected override void Update()
    {
        if (cooldownRestante > 0f) cooldownRestante -= Time.deltaTime;
        base.Update();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // Seguridad: nunca dejarla inmune/sin gravedad si se desactiva a media transformación.
        if (dummy != null) dummy.SetInvulnerable(false);
        if (rb != null) rb.gravityScale = gravedadOriginal;
    }

    // ── DETECCIÓN → TRANSFORMACIÓN ───────────────────────────
    // La base llama a esto cuando Romerito entra en detectionRange.
    // Solo despega si el enfriamiento terminó; si no, sigue patrullando
    // (esa es exactamente la ventana en la que hay que castigarla).
    protected override void IniciarPersecucion()
    {
        if (cooldownRestante > 0f) return;
        if (!EstaEnSuelo) return;          // solo despega desde el suelo

        estado = Estado.Persiguiendo;
        fase = FaseNexhualli.Transformando;
        faseTimer = duracionTransformacion;
        sacudidasHechas = 0;
        tiempoEnVuelo = 0f;

        // Telegraph: quieta, encarando a Romerito, "encendiéndose".
        controlVelocidad = true;
        targetVelocity = Vector2.zero;
        MirarHacia(DireccionAlJugador());

        if (anim != null) anim.SetTrigger("Transformar");
    }

    // ── LÓGICA DE PERSECUCIÓN (requerida por la base) ────────
    protected override void LogicaPersecucion()
    {
        faseTimer -= Time.deltaTime;
        if (EnFormaIgnea) tiempoEnVuelo += Time.deltaTime;

        switch (fase)
        {
            case FaseNexhualli.Transformando: TickTransformando(); break;
            case FaseNexhualli.Temblor:       TickTemblor();       break;
            case FaseNexhualli.Sacudida:      TickSacudida();      break;
            case FaseNexhualli.AtaqueFinal:   TickAtaqueFinal();   break;
            case FaseNexhualli.Regresando:    TickRegresando();    break;
        }
    }

    // ── FASE: Transformando (telegraph, vulnerable) ──────────
    private void TickTransformando()
    {
        targetVelocity = Vector2.zero;
        MirarHacia(DireccionAlJugador());

        // Se "enciende" gradualmente: blanco → naranja fuego.
        if (sr != null)
        {
            float t = 1f - Mathf.Clamp01(faseTimer / duracionTransformacion);
            sr.color = Color.Lerp(colorOriginal, colorTelegraph, t);
        }

        if (faseTimer <= 0f)
            Despegar();
    }

    private void Despegar()
    {
        // Cambio de forma: inmune, sin gravedad, control total del vuelo.
        dummy.SetInvulnerable(true);
        rb.gravityScale = 0f;
        controlVelocidad = false;                 // la base suelta la física
        rb.linearVelocity = Vector2.up * impulsoDespegue;

        AplicarPresentacionIgnea(true);

        fase = FaseNexhualli.Temblor;
        faseTimer = duracionTemblor;
    }

    // ── FASE: Temblor (deriva lenta + jitter Perlin) ─────────
    private void TickTemblor()
    {
        MirarHacia(DireccionAlJugador());

        // Deriva lenta hacia Romerito…
        Vector2 haciaJugador = Vector2.zero;
        if (player != null)
            haciaJugador = ((Vector2)player.position - (Vector2)transform.position).normalized;

        // …más un jitter Perlin en ambos ejes (el "temblor" del diagrama).
        float t = Time.time * frecuenciaTemblor;
        float jx = (Mathf.PerlinNoise(t + semillaX, 0f) - 0.5f) * 2f;
        float jy = (Mathf.PerlinNoise(0f, t + semillaY) - 0.5f) * 2f;

        rb.linearVelocity = haciaJugador * velocidadDeriva
                          + new Vector2(jx, jy) * amplitudTemblor;

        if (faseTimer <= 0f)
        {
            if (tiempoEnVuelo >= maxTiempoVuelo) { IniciarRegreso(); return; }
            IniciarSacudida();
        }
    }

    // ── FASE: Sacudida (mini-sprint comprometido) ────────────
    private void IniciarSacudida()
    {
        // La dirección se fija AL INICIO de la sacudida (compromiso, como
        // el sprint del Mictecah 2): recalcula dónde está Romerito AHORA
        // y se lanza en línea recta. Entre sacudidas siempre hay temblor,
        // así que el jugador puede leer y esquivar cada embestida.
        Vector2 dir = Vector2.right * facing;
        if (player != null)
            dir = ((Vector2)player.position - (Vector2)transform.position).normalized;

        MirarHacia(dir.x >= 0f ? 1 : -1);
        rb.linearVelocity = dir * velocidadSacudida;

        fase = FaseNexhualli.Sacudida;
        faseTimer = duracionSacudida;
        sacudidasHechas++;
    }

    private void TickSacudida()
    {
        // Mantiene la velocidad fijada (si choca con pared, la física la
        // frena y el timer la saca del atasco).
        if (faseTimer <= 0f)
        {
            if (tiempoEnVuelo >= maxTiempoVuelo) { IniciarRegreso(); return; }

            bool cerca = DistanciaAlJugador() <= rangoAtaqueFinal;
            bool agotada = sacudidasHechas >= maxSacudidas;

            if (cerca || agotada) IniciarAtaqueFinal();
            else
            {
                fase = FaseNexhualli.Temblor;
                faseTimer = duracionTemblor;
            }
        }
    }

    // ── FASE: Ataque final (embestida definitiva) ────────────
    private void IniciarAtaqueFinal()
    {
        Vector2 dir = Vector2.right * facing;
        if (player != null)
            dir = ((Vector2)player.position - (Vector2)transform.position).normalized;

        MirarHacia(dir.x >= 0f ? 1 : -1);
        rb.linearVelocity = dir * velocidadAtaqueFinal;

        fase = FaseNexhualli.AtaqueFinal;
        faseTimer = duracionAtaqueFinal;

        if (anim != null) anim.SetTrigger("AtaqueFinal");
    }

    private void TickAtaqueFinal()
    {
        if (faseTimer <= 0f)
            IniciarRegreso();
    }

    // ── FASE: Regresando (vuelve a ser bruja, VULNERABLE) ────
    private void IniciarRegreso()
    {
        // Vuelve a la forma terrestre EN EL AIRE: la gravedad la baja.
        // Desde este instante ya es vulnerable → castigable en la caída.
        dummy.SetInvulnerable(false);
        rb.gravityScale = gravedadOriginal;
        AplicarPresentacionIgnea(false);

        // Corta el impulso horizontal para que caiga cerca del jugador
        // (la ventana de castigo debe ocurrir donde está la acción).
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        fase = FaseNexhualli.Regresando;
    }

    private void TickRegresando()
    {
        if (!EstaEnSuelo) return;   // esperamos el aterrizaje

        // Aterrizó: arranca el enfriamiento y vuelve a la vida terrestre.
        cooldownRestante = enfriamientoTransformacion;
        controlVelocidad = true;
        targetVelocity = Vector2.zero;
        estado = Estado.Patrullando;
    }

    // ── HERIDO: interrumpir la transformación ────────────────
    // La base entra en Estado.Herido al recibir daño (solo posible en
    // forma terrestre). Si estaba cargando la transformación, se cancela
    // y se castiga con enfriamiento: golpear el telegraph es rentable.
    protected override void OnHerido()
    {
        if (fase == FaseNexhualli.Transformando)
        {
            cooldownRestante = Mathf.Max(cooldownRestante, enfriamientoTrasInterrupcion);
            if (sr != null) sr.color = colorOriginal;
            fase = FaseNexhualli.Regresando;   // sella la fase: el intento quedó cancelado
        }

        // Seguridad extra: nunca quedar sin gravedad tras un golpe.
        rb.gravityScale = gravedadOriginal;
        dummy.SetInvulnerable(false);
        AplicarPresentacionIgnea(false);
    }

    // ── CONTACTO CON ROMERITO ────────────────────────────────
    // En forma ígnea NO usamos la reacción de la base (recoil + Herido
    // romperían el vuelo): dañamos, empujamos y pasamos directo al
    // regreso — la lumbre "se consume" al impactar.
    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (!EnFormaIgnea)
        {
            base.OnCollisionEnter2D(collision);   // comportamiento terrestre normal
            return;
        }

        if (!collision.gameObject.CompareTag("Player")) return;

        Vector2 knockDir = (collision.transform.position - transform.position).normalized;
        knockDir.y = 0.5f;

        Rigidbody2D prb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (prb != null)
        {
            prb.linearVelocity = Vector2.zero;
            prb.AddForce(knockDir * contactKnockbackToPlayer, ForceMode2D.Impulse);
        }

        if (playerHealth != null)
            playerHealth.TakeDamage(contactDamage);

        IniciarRegreso();
    }

    // ── Presentación ─────────────────────────────────────────
    private void AplicarPresentacionIgnea(bool ignea)
    {
        if (sr != null)
        {
            sr.color = colorOriginal;
            if (ignea && spriteBolaDeFuego != null) sr.sprite = spriteBolaDeFuego;
            if (!ignea && spriteTerrestre != null)  sr.sprite = spriteTerrestre;
        }

        if (vfxBolaDeFuego != null)
            vfxBolaDeFuego.SetActive(ignea);

        if (anim != null) anim.SetBool("BolaDeFuego", ignea);

        AplicarColliderPorForma(ignea);
    }

    // ★ NUEVO [SPRITES]: Ajusta tamaño/offset del collider al cambiar de forma.
    //   Si no se asignó colliderPrincipal en el Inspector, no hace nada
    //   (compatibilidad hacia atrás con prefabs viejos / testing rápido).
    private void AplicarColliderPorForma(bool ignea)
    {
        if (colliderPrincipal == null) return;

        colliderPrincipal.size   = ignea ? colliderTamanoIgneo   : colliderTamanoTerrestre;
        colliderPrincipal.offset = ignea ? colliderOffsetIgneo   : colliderOffsetTerrestre;
    }

    // ── DEBUG ────────────────────────────────────────────────
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Rango del ataque final (rojo-anaranjado)
        Gizmos.color = new Color(1f, 0.4f, 0f);
        Gizmos.DrawWireSphere(transform.position, rangoAtaqueFinal);
    }
}
