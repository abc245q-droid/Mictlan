using UnityEngine;

// ============================================================
//  FlyingEnemyAI — Enemigo volador (mosquito) integrado al
//  NUEVO SISTEMA DE ENEMIGOS
// ============================================================
//
//  Conserva intacta la "magia del mosquito" original:
//    • Deambular (wander) alrededor de un ancla con Perlin noise.
//    • Persecución errática de lejos → precisa de cerca (ataque letal).
//    • Evasión de obstáculos por reflexión del rayo.
//    • Picar y retirarse (sting & retreat) con amnesia breve.
//
//  Lo que se AÑADIÓ para hacerlo ciudadano de primera clase del
//  sistema (mismas reglas que MictecahBase, pero para un volador):
//
//    1. Reacción al DAÑO:  se suscribe a EnemyDummy.OnHurt y entra
//       en un estado HERIDO con retroceso que se resuelve por
//       MovePosition (funciona igual si el Rigidbody2D es Kinematic
//       o Dynamic-gravity0; nunca pelea con la física).
//
//    2. Implementa IEnemigoConKnockbackPropio → RomeritoCombat ya
//       NO le aplica un AddForce externo encima (antes ese AddForce
//       era anulado por MovePosition, así que el mosquito NO
//       retrocedía al ser golpeado: ese era el bug principal).
//
//    3. Daño por contacto tuneable por Inspector (contactDamage,
//       contactKnockbackToPlayer) en vez de valores hardcodeados.
//       Cachea RomeritoHealth en Start (no GetComponent por golpe).
//
//    4. Recoil de contacto: al picar a Romerito, un empujón suave
//       + pausa breve, con la MISMA disciplina que el golpe de arma
//       (el golpe de arma tiene prioridad y no se sobreescribe).
//
//  Requisitos en el GameObject:
//    Rigidbody2D (gravityScale = 0) + Collider2D (NO trigger)
//    + EnemyDummy + (este script) + SpriteRenderer.
//
//  Matriz de colisiones (Project Settings ▸ Physics 2D):
//    Enemy × Enemy  → DESMARCADO (que no choquen entre sí)
//    Enemy × Player → MARCADO    (para que OnCollisionEnter2D pique)
//
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyDummy))]
public class FlyingEnemyAI : MonoBehaviour, IEnemigoConKnockbackPropio
{
    [Header("Configuración de Vuelo")]
    public float flySpeed = 3f;
    public float chaseSpeed = 5.5f;
    [Tooltip("Qué tan rápido puede girar (suavizado de la dirección).")]
    public float turnSpeed = 5f;

    [Header("Movimiento Orgánico")]
    [Tooltip("Cuánto se mueve erráticamente de lejos.")]
    public float noiseStrength = 2.5f;
    [Tooltip("Qué tan rápido vibra el ruido de Perlin.")]
    public float noiseFrequency = 4f;
    [Tooltip("Distancia a la que se vuelve 100% preciso (ataque letal).")]
    public float precisionRadius = 1.5f;

    [Header("Combate")]
    public float avoidanceForce = 10f;
    [Tooltip("Amnesia tras picar: tiempo que ignora al jugador para mantener presión.")]
    public float ignorePlayerTime = 1.5f;

    [Header("Daño por contacto a Romerito")]
    [Tooltip("Daño (en corazones) que causa al picar a Romerito.")]
    public int contactDamage = 1;
    [Tooltip("Fuerza del empujón que sufre Romerito al ser picado.")]
    public float contactKnockbackToPlayer = 10f;

    [Header("Retroceso al recibir daño (golpe de arma)")]
    [Tooltip("Fuerza del retroceso al ser golpeado por Romerito.")]
    public float knockbackForce = 7f;
    [Tooltip("Duración del estado Herido (mientras dura, no controla su vuelo).")]
    public float knockbackDuration = 0.18f;
    [Tooltip("Qué tan rápido se apaga el retroceso (mayor = frena antes).")]
    public float knockbackDecay = 6f;

    [Header("Recoil al tocar a Romerito (picadura)")]
    [Tooltip("Empujón suave que sufre el mosquito al picar. Menor que knockbackForce.")]
    public float contactRecoilForce = 3f;
    [Tooltip("Pausa tras la picadura antes de retomar el control del vuelo.")]
    public float contactRecoilDuration = 0.12f;

    [Header("Zonas")]
    public float detectionRange = 7f;
    public float wanderRadius = 3f;

    [Header("Referencias")]
    [Tooltip("SOLO paredes/suelo. NO incluyas al jugador ni a los enemigos.")]
    public LayerMask obstacleLayer;

    // ── Referencias internas ─────────────────────────────────
    private Rigidbody2D rb;
    private SpriteRenderer spriteRend;
    private EnemyDummy dummy;
    private Transform playerTransform;
    private RomeritoHealth playerHealth;

    // ── Estado de vuelo ──────────────────────────────────────
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private float changeDirTimer;
    private bool isChasing = false;
    private float noiseSeedX;
    private float noiseSeedY;
    private float ignoreTimer = 0f;
    private Vector2 currentMoveDirection;

    // ── Estado HERIDO (retroceso propio) ─────────────────────
    private bool herido = false;
    private float hurtTimer = 0f;
    private Vector2 knockbackVel;

    // ── Ciclo de vida ────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRend = GetComponent<SpriteRenderer>();
        dummy = GetComponent<EnemyDummy>();
    }

    void OnEnable()
    {
        // Se suscribe al MISMO evento que MictecahBase para reaccionar al daño.
        if (dummy != null) dummy.OnHurt += RecibirGolpe;
    }

    void OnDisable()
    {
        if (dummy != null) dummy.OnHurt -= RecibirGolpe;
    }

    void Start()
    {
        startPosition = transform.position;
        currentMoveDirection = Vector2.zero;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
            playerHealth = player.GetComponent<RomeritoHealth>();
        }

        noiseSeedX = Random.Range(0f, 1000f);
        noiseSeedY = Random.Range(0f, 1000f);

        PickNewPosition();
    }

    void Update()
    {
        // El estado Herido tiene prioridad absoluta: la IA suelta el control.
        if (herido)
        {
            hurtTimer -= Time.deltaTime;
            if (hurtTimer <= 0f) herido = false;
            return;
        }

        // 1. Amnesia (cooldown tras picar)
        if (ignoreTimer > 0f)
        {
            ignoreTimer -= Time.deltaTime;
            isChasing = false;
        }
        else if (playerTransform != null)
        {
            // [COPAL] El humo ritual también engaña a los ojos que vuelan.
            // Camuflado → pierde el objetivo de inmediato y no re-detecta
            // hasta que el camuflaje caiga. WanderLogic toma el control y
            // el volador se aleja con su deambular normal.
            if (BarreraCopal.CamuflajeActivo)
            {
                isChasing = false;
            }
            else
            {
                // 2. Detección con histéresis (evita parpadeo en el borde del rango).
                float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
                float activeRange = isChasing ? detectionRange + 3f : detectionRange;
                isChasing = distToPlayer < activeRange;
            }
        }

        // 3. Selección de objetivo
        if (isChasing && playerTransform != null)
            ChaseLogic();
        else
            WanderLogic();

        FlipSprite();
    }

    void FixedUpdate()
    {
        // ── HERIDO: el retroceso manda; se resuelve por MovePosition y decae.
        // Funciona sea Kinematic o Dynamic y NUNCA pelea con AddForce externos.
        if (herido)
        {
            rb.MovePosition(rb.position + knockbackVel * Time.fixedDeltaTime);
            knockbackVel = Vector2.Lerp(knockbackVel, Vector2.zero, knockbackDecay * Time.fixedDeltaTime);
            return;
        }

        // ── Vuelo normal ────────────────────────────────────
        float speed = isChasing ? chaseSpeed : flySpeed;

        // A. Dirección deseada hacia el objetivo.
        Vector2 desiredDirection = (targetPosition - (Vector2)transform.position).normalized;

        // B. Evasión de obstáculos (override).
        Vector2 avoidance = AvoidObstacles(desiredDirection);
        if (avoidance != Vector2.zero)
        {
            desiredDirection += avoidance;
            desiredDirection.Normalize();
        }

        // C. Suavizado: convierte el movimiento robótico en curvas orgánicas.
        currentMoveDirection = Vector2.Lerp(currentMoveDirection, desiredDirection, turnSpeed * Time.fixedDeltaTime);

        // D. Mover.
        rb.MovePosition(rb.position + currentMoveDirection * speed * Time.fixedDeltaTime);
    }

    // ── PERCEPCIÓN / MOVIMIENTO (intacto respecto al original) ──
    void WanderLogic()
    {
        changeDirTimer -= Time.deltaTime;
        if (changeDirTimer <= 0f || Vector2.Distance(transform.position, targetPosition) < 0.2f)
        {
            PickNewPosition();
            changeDirTimer = 2f;
        }
    }

    void ChaseLogic()
    {
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        // Factor 0..1: lejos → mucho ruido; dentro de precisionRadius → ataque recto.
        float noiseFactor = Mathf.Clamp01((dist - precisionRadius) / 5f);

        float time = Time.time * noiseFrequency;
        float nX = (Mathf.PerlinNoise(time + noiseSeedX, 0f) - 0.5f) * 2f; // -1..1
        float nY = (Mathf.PerlinNoise(0f, time + noiseSeedY) - 0.5f) * 2f; // -1..1

        Vector2 organicOffset = new Vector2(nX, nY) * noiseStrength * noiseFactor;
        targetPosition = (Vector2)playerTransform.position + organicOffset;
    }

    Vector2 AvoidObstacles(Vector2 dir)
    {
        float checkDist = 1.5f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, checkDist, obstacleLayer);
        if (hit.collider != null)
        {
            // Reflejar la dirección para "rebotar" suavemente del obstáculo.
            Vector2 reflectDir = Vector2.Reflect(dir, hit.normal);
            return reflectDir.normalized * avoidanceForce;
        }
        return Vector2.zero;
    }

    void PickNewPosition()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * wanderRadius;
            Vector2 potentialPos = startPosition + randomPoint;
            if (!Physics2D.OverlapCircle(potentialPos, 0.2f, obstacleLayer))
            {
                targetPosition = potentialPos;
                return;
            }
        }
    }

    // Elige un punto en la dirección OPUESTA al jugador (retirada tras picar).
    void RetreatFromPlayer()
    {
        if (playerTransform == null) { PickNewPosition(); return; }

        Vector2 dirToPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        Vector2 retreatPos = (Vector2)transform.position - dirToPlayer * 3f;

        if (!Physics2D.OverlapCircle(retreatPos, 0.2f, obstacleLayer))
            targetPosition = retreatPos;
        else
            PickNewPosition();
    }

    void FlipSprite()
    {
        if (spriteRend == null) return;
        // Usa currentMoveDirection para evitar flipeos rápidos por el jitter.
        if (Mathf.Abs(currentMoveDirection.x) > 0.1f)
            spriteRend.flipX = currentMoveDirection.x > 0f;
    }

    // ── REACCIÓN AL DAÑO (golpe de arma) ─────────────────────
    // Se dispara desde EnemyDummy.OnHurt (mismo canal que MictecahBase).
    private void RecibirGolpe()
    {
        Vector2 dir;
        if (playerTransform != null)
            dir = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
        else
            dir = (currentMoveDirection.sqrMagnitude > 0.001f) ? -currentMoveDirection.normalized : Vector2.up;

        dir += Vector2.up * 0.35f; // pizca de elevación para que el picotazo "salte"
        EntrarHerido(dir.normalized * knockbackForce, knockbackDuration);

        // Al recuperarse volará hacia atrás y luego re-evaluará la persecución.
        RetreatFromPlayer();
    }

    // ── RECOIL AL PICAR A ROMERITO ───────────────────────────
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // Efecto en Romerito.
        Vector2 knockDir = ((Vector2)collision.transform.position - (Vector2)transform.position).normalized;
        Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerRb.AddForce(knockDir * contactKnockbackToPlayer, ForceMode2D.Impulse);
        }
        if (playerHealth != null)
            playerHealth.TakeDamage(contactDamage);

        // Amnesia + retirada táctica (sabor original del mosquito).
        ignoreTimer = ignorePlayerTime;
        RetreatFromPlayer();

        // Recoil físico suave del propio mosquito, con la MISMA disciplina que
        // el golpe de arma: si YA está herido por un golpe, ese tiene prioridad
        // y no lo sobreescribimos.
        if (!herido)
            EntrarHerido(-knockDir * contactRecoilForce, contactRecoilDuration);
    }

    // Entrada única al estado Herido: centraliza el retroceso propio.
    private void EntrarHerido(Vector2 velocidadRetroceso, float duracion)
    {
        herido = true;
        hurtTimer = duracion;
        knockbackVel = velocidadRetroceso;
    }

    // ── DEBUG ────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition != Vector2.zero ? startPosition : (Vector2)transform.position, wanderRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Radio de precisión (zona de muerte).
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, precisionRadius);
    }
}
