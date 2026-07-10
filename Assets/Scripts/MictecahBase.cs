using UnityEngine;

// ============================================================
//  MictecahBase — Esqueleto común de TODOS los Mictecah
// ============================================================
//
//  Esta clase ABSTRACTA contiene lo que comparten los 4 (y los
//  futuros) Mictecah:
//
//    • Máquina de estados: Patrullando · Esperando · Persiguiendo · Herido
//    • Patrullaje horizontal con detección de PARED y PRECIPICIO
//    • Sensor de suelo (para los que saltan)
//    • Retroceso breve al recibir daño (se suscribe a EnemyDummy.OnHurt)
//    • Daño por contacto a Romerito
//    • Warmup de sensores al spawnear (evita falsos positivos al nacer)
//    • Flip que conserva la escala original del sprite
//
//  Lo ÚNICO que cada Mictecah define es su PERSECUCIÓN, mediante:
//    - LogicaPersecucion()   (obligatorio)
//    - ReaccionaAlJugador    (Mictecah 1 lo pone en false: nunca persigue)
//    - IniciarPersecucion()  (opcional, p. ej. para arrancar un sprint)
//
//  Componentes requeridos en el GameObject:
//    Rigidbody2D + Collider2D + EnemyDummy + (este script)
//    + un hijo "GroundCheck" colocado en el BORDE FRONTAL-INFERIOR.
//
//  --- IMPORTANTE: que los enemigos NO choquen entre sí ---
//  La forma profesional y robusta es la Matriz de Colisiones:
//    Project Settings ▸ Physics 2D ▸ Layer Collision Matrix
//    Pon todos los enemigos en una layer "Enemy" y DESMARCA la
//    casilla Enemy × Enemy. Así se ignoran "como a otra profundidad"
//    aunque hagan spawn desde el WaveSpawner. (Mictecah 1 lo pide
//    explícitamente, pero conviene para los 4.)
//
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyDummy))]
public abstract class MictecahBase : MonoBehaviour, IEnemigoConKnockbackPropio
{
    // ── Estados ──────────────────────────────────────────────
    protected enum Estado { Patrullando, Esperando, Persiguiendo, Herido }
    [SerializeField] protected Estado estado = Estado.Patrullando;

    [Header("── Patrullaje ──")]
    public float patrolSpeed = 2f;
    [Tooltip("Pausa al toparse con pared/precipicio antes de girar.")]
    public float idleTime = 0.4f;

    [Header("── Detección del Jugador ──")]
    public float detectionRange = 5f;
    public float stopChaseRange = 8f;

    [Header("── Sensores de Entorno ──")]
    [Tooltip("Hijo colocado en el BORDE FRONTAL-INFERIOR. Detecta precipicios.")]
    public Transform groundCheck;
    public float groundCheckDistance = 1f;
    public float wallCheckDistance = 0.6f;
    [Tooltip("Largo del rayo bajo el cuerpo para saber si está en el suelo (saltadores).")]
    public float groundedRayLength = 0.6f;
    [Tooltip("SOLO suelo y paredes. NO incluyas al jugador ni a los enemigos.")]
    public LayerMask groundLayer;

    [Header("── Retroceso al recibir daño ──")]
    public float knockbackForce = 6f;
    public float knockbackUp = 3f;
    public float knockbackDuration = 0.2f;

    [Header("── Daño por contacto a Romerito ──")]
    public int contactDamage = 1;
    public float contactKnockbackToPlayer = 10f;

    // [FIX-CONTACT] Retroalimentacion del ENEMIGO al tocar a Romerito.
    // Valores menores que knockbackForce/knockbackDuration: tropiezo, no golpe de arma.
    [Header("Reaccion del enemigo al tocar a Romerito")]
    [Tooltip("Retroceso suave al hacer contacto. Menor que knockbackForce.")]
    public float contactRecoilForce = 2.5f;
    [Tooltip("Componente vertical del retroceso de contacto.")]
    public float contactRecoilUp = 1f;
    [Tooltip("Pausa tras el contacto antes de que la IA retome el control. Sprint queda cancelado, salto se interrumpe.")]
    public float contactPauseDuration = 0.45f;

    // ── Referencias ──────────────────────────────────────────
    protected Rigidbody2D rb;
    protected Animator anim;
    protected EnemyDummy dummy;
    protected Transform player;
    protected RomeritoHealth playerHealth;

    // ── Estado interno ───────────────────────────────────────
    protected int facing = 1;                 // 1 = derecha, -1 = izquierda
    protected Vector2 targetVelocity;         // x que aplicará FixedUpdate cuando controlVelocidad = true
    protected bool controlVelocidad = true;   // si false, FixedUpdate NO toca la velocidad (para saltos/dashes balísticos)

    private float waitTimer;
    private float hurtTimer;
    private bool hurtApplyImpulse;
    private Vector2 hurtVelocity;

    private float sensorWarmup = 0.15f;
    protected bool SensoresListos => sensorWarmup <= 0f;
    protected bool EstaEnSuelo { get; private set; }

    // ── Hooks que las subclases pueden/deben sobreescribir ──
    /// <summary>Lógica de persecución específica de cada Mictecah.</summary>
    protected abstract void LogicaPersecucion();
    /// <summary>Mictecah 1 lo pone en false para NO reaccionar al jugador.</summary>
    protected virtual bool ReaccionaAlJugador => true;
    /// <summary>Se llama al pasar de Patrulla/Espera a Persecución (p. ej. arrancar sprint).</summary>
    protected virtual void IniciarPersecucion() { estado = Estado.Persiguiendo; }
    /// <summary>Se llama cuando recibe daño (para abortar sprints/saltos, etc.).</summary>
    protected virtual void OnHerido() { }

    // ── Ciclo de vida ────────────────────────────────────────
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        dummy = GetComponent<EnemyDummy>();
    }

    protected virtual void OnEnable()
    {
        if (dummy != null) dummy.OnHurt += RecibirGolpe;
    }

    protected virtual void OnDisable()
    {
        if (dummy != null) dummy.OnHurt -= RecibirGolpe;
    }

    protected virtual void Start()
    {
        facing = (transform.localScale.x < 0) ? -1 : 1;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<RomeritoHealth>();
        }
    }

    protected virtual void Update()
    {
        if (sensorWarmup > 0f) sensorWarmup -= Time.deltaTime;

        EstaEnSuelo = CheckGroundBelow();

        switch (estado)
        {
            case Estado.Patrullando:
                LogicaPatrulla();
                ChequearJugador();
                break;

            case Estado.Esperando:
                LogicaEspera();
                ChequearJugador();
                break;

            case Estado.Persiguiendo:
                // [COPAL] Si Romerito se camufla a media persecución, el
                // enemigo PIERDE el rastro: pausa breve, media vuelta y
                // regresa a patrullar (EntrarEspera hace el retorno orgánico).
                // Sin esto, se quedaba encima del jugador esperando a que la
                // barrera caducara — daño de contacto garantizado al expirar.
                if (BarreraCopal.CamuflajeActivo)
                {
                    EntrarEspera();
                    break;
                }
                LogicaPersecucion();   // ← cada subclase
                break;

            case Estado.Herido:
                LogicaHerido();
                break;
        }

        ActualizarAnimacion();
    }

    protected virtual void FixedUpdate()
    {
        // El aturdimiento tiene prioridad absoluta sobre cualquier control de IA.
        if (estado == Estado.Herido)
        {
            if (hurtApplyImpulse)
            {
                // Se aplica UNA sola vez y se deja decaer con la física (drag).
                // Como FixedUpdate corre después del Update donde se golpeó,
                // este set SOBREESCRIBE cualquier AddForce que aplique RomeritoCombat:
                // así el retroceso es siempre el mismo y nunca se duplica.
                rb.linearVelocity = hurtVelocity;
                hurtApplyImpulse = false;
            }
            return;
        }

        if (controlVelocidad)
            rb.linearVelocity = new Vector2(targetVelocity.x, rb.linearVelocity.y);
    }

    // ── PATRULLAJE (común) ───────────────────────────────────
    protected virtual void LogicaPatrulla()
    {
        // Durante el warmup avanzamos sin chequear bordes (la física aún se asienta).
        if (!SensoresListos)
        {
            targetVelocity = new Vector2(facing * patrolSpeed, 0f);
            return;
        }

        if (HayParedAdelante() || HayPrecipicioAdelante())
            EntrarEspera();
        else
            targetVelocity = new Vector2(facing * patrolSpeed, 0f);
    }

    protected virtual void LogicaEspera()
    {
        targetVelocity = Vector2.zero;
        waitTimer -= Time.deltaTime;
        if (waitTimer <= 0f)
        {
            Voltear();
            estado = Estado.Patrullando;
        }
    }

    protected void EntrarEspera()
    {
        targetVelocity = Vector2.zero;
        waitTimer = idleTime;
        estado = Estado.Esperando;
    }

    // ── PERCEPCIÓN (común) ───────────────────────────────────
    protected virtual void ChequearJugador()
    {
        if (!ReaccionaAlJugador || player == null) return;

        // [COPAL] El humo ritual esconde a Romerito de los ojos del Mictlán.
        // Mientras el camuflaje está activo, este enemigo no puede detectarlo.
        if (BarreraCopal.CamuflajeActivo) return;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist < detectionRange)
            IniciarPersecucion();
    }

    // ── HERIDO / RETROCESO (común) ───────────────────────────
    private void RecibirGolpe()
    {
        // Retrocede ALEJÁNDOSE del jugador (el que golpea es Romerito).
        int dir = facing;
        if (player != null)
            dir = (player.position.x > transform.position.x) ? -1 : 1;

        controlVelocidad = false;            // la IA suelta el control durante el aturdimiento
        hurtVelocity = new Vector2(dir * knockbackForce, knockbackUp);
        hurtApplyImpulse = true;
        hurtTimer = knockbackDuration;
        estado = Estado.Herido;

        OnHerido();                          // hook para abortar sprints/saltos en subclases
    }

    protected virtual void LogicaHerido()
    {
        hurtTimer -= Time.deltaTime;
        if (hurtTimer <= 0f)
        {
            // ★ FIX (Saltarín se traba a medio salto): si el knockback lo dejó
            // todavía en el aire, NO devolvemos el control a la IA todavía.
            // Antes, esto frenaba en seco la velocidad horizontal a mitad de
            // una caída y lo dejaba "pasmado" hasta tocar el suelo. Ahora
            // simplemente dejamos que la física termine la caída con la
            // velocidad del knockback intacta, y reevaluamos cada frame.
            if (!EstaEnSuelo) return;

            controlVelocidad = true;
            targetVelocity = Vector2.zero;
            estado = Estado.Patrullando;     // re-evalúa percepción en el siguiente frame
        }
    }

    // ── SENSORES (común) ─────────────────────────────────────
    protected bool HayPrecipicioAdelante()
    {
        if (groundCheck == null) return false;
        return !Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundLayer);
    }

    protected bool HayParedAdelante()
    {
        Vector2 origen = (Vector2)transform.position + new Vector2(facing * 0.2f, 0f);
        return Physics2D.Raycast(origen, Vector2.right * facing, wallCheckDistance, groundLayer);
    }

    protected bool CheckGroundBelow()
    {
        return Physics2D.Raycast(transform.position, Vector2.down, groundedRayLength, groundLayer);
    }

    // ── UTILIDADES (común) ───────────────────────────────────
    protected void Voltear()
    {
        facing *= -1;
        AplicarEscala();
    }

    protected void MirarHacia(int dir)
    {
        if (dir != 0 && dir != facing)
        {
            facing = dir;
            AplicarEscala();
        }
    }

    private void AplicarEscala()
    {
        // Conserva el tamaño original del sprite (no lo fuerza a 1).
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * facing;
        transform.localScale = s;
    }

    protected int DireccionAlJugador()
    {
        if (player == null) return facing;
        return (player.position.x > transform.position.x) ? 1 : -1;
    }

    protected float DistanciaAlJugador()
    {
        if (player == null) return Mathf.Infinity;
        return Vector2.Distance(transform.position, player.position);
    }

    protected virtual void ActualizarAnimacion()
    {
        if (anim == null) return;
        // Descomenta según tus parámetros del Animator:
        // anim.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        // anim.SetBool("EnSuelo", EstaEnSuelo);
        // anim.SetBool("Herido", estado == Estado.Herido);
    }

    // ── DAÑO POR CONTACTO (común) ────────────────────────────
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // [FIX-CONTACT] El enemigo reacciona al contacto: pequeno retroceso
        // + pausa breve que interrumpe sprint, salto o cualquier accion en curso.
        ReaccionarAlContacto();

        // Efecto en Romerito (sin cambios)
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
    }


    // ── REACCIÓN AL CONTACTO CON ROMERITO (común) ─────────────────

    // [FIX-CONTACT] Reutiliza el mecanismo de Estado.Herido con fuerzas menores.
    // Garantias de diseño:
    //   • Si el enemigo ya está en Estado.Herido (golpe de arma), no sobreescribe.
    //   • Al entrar en Herido, FixedUpdate deja de aplicar la velocidad del sprint.
    //   • Update ya no llama a LogicaPersecucion(), cancelando el sprint/salto.
    //   • Al recuperarse, regresa a Patrullando y ChequearJugador() decide si
    //     re-inicia la persecución o sigue patrullando.
    //   • Se llama OnHerido() para que subclases reseteen su estado interno
    //     (p. ej. Saltarin: enElAire = false, jumpTimer = jumpCooldown).
    private void ReaccionarAlContacto()
    {
        // El golpe de arma tiene prioridad absoluta.
        if (estado == Estado.Herido) return;

        // Retroceder alejandose de Romerito.
        int dir = facing;
        if (player != null)
            dir = (player.position.x > transform.position.x) ? -1 : 1;

        // Inyectamos en el mismo canal que RecibirGolpe(); FixedUpdate lo aplica.
        controlVelocidad = false;
        hurtVelocity = new Vector2(dir * contactRecoilForce, contactRecoilUp);
        hurtApplyImpulse = true;
        hurtTimer = contactPauseDuration;
        estado = Estado.Herido;

        // Hook para subclases: abortar sprints, resetear saltos, etc.
        OnHerido();
    }

    // ── DEBUG (común) ────────────────────────────────────────
    protected virtual void OnDrawGizmosSelected()
    {
        // Rango de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Pared
        Vector3 origen = transform.position + new Vector3(facing * 0.2f, 0, 0);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origen, origen + Vector3.right * facing * wallCheckDistance);

        // Precipicio
        if (groundCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * groundCheckDistance);
        }

        // Suelo bajo el cuerpo
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundedRayLength);
    }
}