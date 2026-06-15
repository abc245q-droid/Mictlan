using UnityEngine;

public class FlyingEnemyAI : MonoBehaviour
{
    [Header("Configuración de Vuelo")]
    public float flySpeed = 3f;
    public float chaseSpeed = 5.5f;
    public float turnSpeed = 5f; // NUEVO: Qué tan rápido puede girar (suavizado)

    [Header("Movimiento Orgánico")]
    public float noiseStrength = 2.5f;  // Cuánto se mueve erráticamente de lejos
    public float noiseFrequency = 4f;   // Qué tan rápido vibra
    public float precisionRadius = 1.5f; // Distancia a la que se vuelve 100% preciso (ataque letal)

    [Header("Combate")]
    public float avoidanceForce = 10f;
    public float ignorePlayerTime = 1.5f; // Reduje un poco para mantener presión

    [Header("Zonas")]
    public float detectionRange = 7f;
    public float wanderRadius = 3f;

    [Header("Referencias")]
    public LayerMask obstacleLayer;

    // Variables internas
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private float changeDirTimer;
    private Transform playerTransform;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRend;

    private bool isChasing = false;
    private float noiseSeedX;
    private float noiseSeedY;
    private float ignoreTimer = 0f;

    // Variable para el suavizado de movimiento
    private Vector2 currentMoveDirection;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRend = GetComponent<SpriteRenderer>();
        startPosition = transform.position;
        currentMoveDirection = Vector2.zero; // Inicializar

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerTransform = player.transform;

        noiseSeedX = Random.Range(0f, 1000f);
        noiseSeedY = Random.Range(0f, 1000f);

        PickNewPosition();
    }

    void Update()
    {
        // 1. Timer de Amnesia (Cooldown tras ataque)
        if (ignoreTimer > 0)
        {
            ignoreTimer -= Time.deltaTime;
            isChasing = false;
        }
        else if (playerTransform != null)
        {
            // 2. Detección
            float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            // Histeria: Si ya te estaba persiguiendo, te persigue un poco más lejos (detection + 3)
            // Esto evita que entre y salga del estado "Chase" en el borde del rango.
            float activeRange = isChasing ? detectionRange + 3f : detectionRange;

            if (distToPlayer < activeRange)
            {
                isChasing = true;
            }
            else
            {
                isChasing = false;
            }
        }

        // 3. Selección de Objetivo
        if (isChasing && playerTransform != null)
        {
            ChaseLogic();
        }
        else
        {
            WanderLogic();
        }

        FlipSprite();
    }

    void FixedUpdate()
    {
        float speed = isChasing ? chaseSpeed : flySpeed;

        // A. Calcular dirección deseada hacia el target
        Vector2 desiredDirection = (targetPosition - (Vector2)transform.position).normalized;

        // B. Evitar obstáculos (Override)
        Vector2 avoidance = AvoidObstacles(desiredDirection);
        if (avoidance != Vector2.zero)
        {
            desiredDirection += avoidance;
            desiredDirection.Normalize();
        }

        // C. SUAVIZADO (Lerp): Convertimos el movimiento robótico en curvas orgánicas
        // Interpolamos entre la dirección actual y la deseada
        currentMoveDirection = Vector2.Lerp(currentMoveDirection, desiredDirection, turnSpeed * Time.fixedDeltaTime);

        // D. Mover
        Vector2 newPos = rb.position + (currentMoveDirection * speed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }

    void WanderLogic()
    {
        changeDirTimer -= Time.deltaTime;
        if (changeDirTimer <= 0 || Vector2.Distance(transform.position, targetPosition) < 0.2f)
        {
            PickNewPosition();
            changeDirTimer = 2f; // Hardcoded o variable pública
        }
    }

    void ChaseLogic()
    {
        float dist = Vector2.Distance(transform.position, playerTransform.position);

        // --- LA MAGIA DEL MOSQUITO ---
        // Calculamos un factor de 0 a 1. 
        // Si está lejos (> 6m), factor es 1 (mucho ruido).
        // Si está cerca (< precisionRadius), factor es 0 (cero ruido, ataque recto).
        float noiseFactor = Mathf.Clamp01((dist - precisionRadius) / 5f);

        // Perlin Noise dependiente del tiempo
        float time = Time.time * noiseFrequency;
        float nX = (Mathf.PerlinNoise(time + noiseSeedX, 0) - 0.5f) * 2f; // -1 a 1
        float nY = (Mathf.PerlinNoise(0, time + noiseSeedY) - 0.5f) * 2f; // -1 a 1

        // Aplicamos el offset escalado por la distancia
        Vector2 organicOffset = new Vector2(nX, nY) * noiseStrength * noiseFactor;

        targetPosition = (Vector2)playerTransform.position + organicOffset;
    }

    Vector2 AvoidObstacles(Vector2 dir)
    {
        // Pequeña mejora: Raycast un poco más largo según velocidad
        float checkDist = 1.5f;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, checkDist, obstacleLayer);

        if (hit.collider != null)
        {
            // Truco: Reflejar la dirección para "rebotar" suavemente del obstáculo
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

    // Función para huir tras atacar
    void RetreatFromPlayer()
    {
        // Elegimos un punto en la dirección OPUESTA al jugador
        Vector2 dirToPlayer = (playerTransform.position - transform.position).normalized;
        Vector2 retreatPos = (Vector2)transform.position - (dirToPlayer * 3f); // 3 metros hacia atrás

        // Verificamos que no sea dentro de una pared (básico)
        if (!Physics2D.OverlapCircle(retreatPos, 0.2f, obstacleLayer))
        {
            targetPosition = retreatPos;
        }
        else
        {
            // Si atrás hay pared, elegimos cualquier otro lado
            PickNewPosition();
        }
    }

    void FlipSprite()
    {
        // Usamos currentMoveDirection para evitar flipeos rápidos por el jitter
        if (Mathf.Abs(currentMoveDirection.x) > 0.1f)
        {
            spriteRend.flipX = currentMoveDirection.x > 0;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            RomeritoHealth health = collision.gameObject.GetComponent<RomeritoHealth>(); // Asumo que tienes este script o similar
            Rigidbody2D playerRb = collision.gameObject.GetComponent<Rigidbody2D>();

            // Dirección del golpe
            Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;

            if (playerRb != null)
            {
                // Unity 6 usa linearVelocity. Si usas versión vieja cambia a .velocity
                playerRb.linearVelocity = Vector2.zero;
                playerRb.AddForce(knockbackDir * 10f, ForceMode2D.Impulse); // Aumenté un poco la fuerza
            }

            // Aplicar Daño
             if (health != null) health.TakeDamage(1); 
            // ^ Descomenta cuando tengas el script de vida

            // COOLDOWN
            ignoreTimer = ignorePlayerTime;

            // Retirada Táctica
            RetreatFromPlayer();
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startPosition != Vector2.zero ? startPosition : transform.position, wanderRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Visualizar radio de precisión (zona de muerte)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, precisionRadius);
    }
}