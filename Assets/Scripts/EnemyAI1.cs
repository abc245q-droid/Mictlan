using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    public float patrolSpeed = 2f;
    public float chaseSpeed = 4f;
    public float idleTime = 2f;

    [Header("Detección")]
    public float detectionRange = 5f;
    public float stopChaseRange = 7f;
    public Transform playerCheck;
    public LayerMask playerLayer;

    [Header("Detección de Entorno")]
    public Transform groundCheck;
    public float groundCheckDistance = 1f;
    public float wallCheckDistance = 0.6f;
    // IMPORTANTE: Esta capa debe tener SOLO el suelo ("Ground"), no al jugador ni al enemigo.
    public LayerMask groundLayer;

    // Estados
    private enum State { Patrullando, Persiguiendo, Esperando }
    [SerializeField] private State currentState;

    // Variables internas
    private Rigidbody2D rb;
    private Animator anim;
    private Transform playerTransform;
    private float waitTimer;
    private int facingDirection = 1;
    private Vector2 targetVelocity;

    // -------------------------------------------------------
    // FIX 1 — Warmup de sensores
    // Al spawnear, la física de Unity tarda unos frames en asentarse.
    // Durante ese tiempo, el rayo de suelo puede no detectar nada aunque
    // el enemigo esté sobre una plataforma, lo que dispara CheckEdgeOrWall()
    // y mete al enemigo en WaitState antes de que empiece a moverse.
    // Solución: ignorar los sensores de entorno los primeros 0.15 s.
    // -------------------------------------------------------
    private float sensorWarmup = 0.15f;
    private bool sensorsReady => sensorWarmup <= 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        currentState = State.Patrullando;

        // FIX 2 — facingDirection seguro al spawnear
        // Mathf.Sign(0) devuelve 0, lo que rompe toda la lógica de dirección.
        // Comparación directa en su lugar: si localScale.x es negativo → -1, si no → 1.
        facingDirection = (transform.localScale.x < 0) ? -1 : 1;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        // Descontar el warmup de sensores frame a frame
        if (sensorWarmup > 0f)
            sensorWarmup -= Time.deltaTime;

        DebugDrawRays();

        switch (currentState)
        {
            case State.Patrullando:
                PatrolLogic();
                CheckForPlayer();
                break;

            case State.Persiguiendo:
                ChaseLogic();
                break;

            case State.Esperando:
                WaitLogic();
                CheckForPlayer();
                break;
        }

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(targetVelocity.x, rb.linearVelocity.y);
    }

    // --- LÓGICAS ---

    void PatrolLogic()
    {
        // FIX 1 aplicado: durante el warmup movemos sin chequear bordes.
        // En condiciones normales esto solo dura 0.15 s y el enemigo ya
        // estará sobre suelo firme cuando los sensores se activen.
        if (!sensorsReady)
        {
            targetVelocity = new Vector2(facingDirection * patrolSpeed, 0);
            return;
        }

        if (CheckEdgeOrWall())
        {
            EnterWaitState();
        }
        else
        {
            targetVelocity = new Vector2(facingDirection * patrolSpeed, 0);
        }
    }

    void WaitLogic()
    {
        targetVelocity = Vector2.zero;
        waitTimer -= Time.deltaTime;

        if (waitTimer <= 0)
        {
            Flip();
            currentState = State.Patrullando;
        }
    }

    void ChaseLogic()
    {
        if (playerTransform == null) return;

        // [COPAL] Perdió el rastro: el humo lo esconde. Volver a esperar/patrullar.
        if (BarreraCopal.CamuflajeActivo)
        {
            EnterWaitState();
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distToPlayer > stopChaseRange)
        {
            EnterWaitState();
            return;
        }

        int directionToPlayer = (playerTransform.position.x > transform.position.x) ? 1 : -1;
        if (directionToPlayer != facingDirection) Flip();

        if (CheckPrecipice())
        {
            targetVelocity = Vector2.zero;
        }
        else
        {
            targetVelocity = new Vector2(directionToPlayer * chaseSpeed, 0);
        }
    }

    void CheckForPlayer()
    {
        if (playerTransform == null) return;

        // [COPAL] Camuflado → invisible para este enemigo.
        if (BarreraCopal.CamuflajeActivo) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        if (dist < detectionRange)
        {
            // FIX 3 — Detección omnidireccional
            // La versión original solo detectaba si el jugador estaba en la dirección
            // que miraba el enemigo, así que atacar por detrás nunca lo activaba.
            // Ahora detecta en cualquier dirección dentro del rango.
            // Si quieres restaurar el cono frontal, descomenta el bloque inferior.
            currentState = State.Persiguiendo;

            /* -- DETECCIÓN CON CONO FRONTAL (versión original, comentada) --
            Vector2 dirToPlayer = (playerTransform.position - transform.position).normalized;
            if ((facingDirection == 1 && dirToPlayer.x > 0) ||
                (facingDirection == -1 && dirToPlayer.x < 0))
            {
                currentState = State.Persiguiendo;
            }
            */
        }
    }

    void EnterWaitState()
    {
        targetVelocity = Vector2.zero;
        waitTimer = idleTime;
        currentState = State.Esperando;
    }

    // --- SENSORES ---

    bool CheckPrecipice()
    {
        if (groundCheck == null) return false;
        RaycastHit2D groundInfo = Physics2D.Raycast(groundCheck.position, Vector2.down, groundCheckDistance, groundLayer);
        return (groundInfo.collider == null);
    }

    bool CheckEdgeOrWall()
    {
        if (CheckPrecipice()) return true;

        float bodyOffset = 0.2f;
        Vector2 wallOrigin = new Vector2(transform.position.x + (facingDirection * bodyOffset), transform.position.y);
        RaycastHit2D wallInfo = Physics2D.Raycast(wallOrigin, Vector2.right * facingDirection, wallCheckDistance, groundLayer);

        return (wallInfo.collider != null);
    }

    // --- UTILS ---

    void Flip()
    {
        facingDirection *= -1;
        transform.localScale = new Vector3(facingDirection, 1, 1);
    }

    void UpdateAnimation()
    {
        // if (anim != null) anim.SetFloat("Speed", Mathf.Abs(targetVelocity.x));
    }

    // --- DAÑO POR CONTACTO ---
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
            knockbackDir.y = 0.5f;

            Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.AddForce(knockbackDir * 10f, ForceMode2D.Impulse);
            }

            RomeritoHealth playerHealth = collision.gameObject.GetComponent<RomeritoHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(1);
            }
        }
    }

    void DebugDrawRays()
    {
        float bodyOffset = 0.3f;
        Vector2 wallOrigin = new Vector2(transform.position.x + (facingDirection * bodyOffset), transform.position.y);
        Debug.DrawRay(wallOrigin, Vector2.right * facingDirection * wallCheckDistance, Color.blue);

        if (groundCheck != null)
            Debug.DrawRay(groundCheck.position, Vector2.down * groundCheckDistance, Color.cyan);
    }
}