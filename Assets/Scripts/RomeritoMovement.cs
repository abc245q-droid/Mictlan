using UnityEngine;
using System.Collections;

public class RomeritoMovement : MonoBehaviour
{


    [HideInInspector] public Vector2 platformVelocity; // Velocidad externa inyectada

    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float runSpeed = 10f;
    public float jumpForce = 12f;
    public float smoothTime = 0.05f;

    [Header("Abilities (Desbloqueables)")]
    public bool unlockDoubleJump = false;
    public bool unlockRun = false;
    public bool unlockWallClimb = false; // --- NUEVO: Actívalo para probar ---


    // --- NUEVO: Agregamos estas dos ---
    public bool unlockWallJump = false;
    public bool unlockDash = false;


    [Header("Physics Settings")]
    public float maxFallSpeed = -15f;
    public float lowJumpMultiplier = 2f;
    public float fallMultiplier = 2.5f;
    private float defaultGravity; // --- NUEVO: Para recordar la gravedad original ---

    public float airDrag = 2f; // Resistencia del aire (cuánto tarda en frenar solo)
    public float airControlSmooth = 0.3f; // Suavizado para cambiar de dirección en el aire

    [Header("Wall Interaction")]
    public float wallSlideSpeed = 2f;
    public Vector2 wallJumpPower = new Vector2(8f, 12f);
    public float wallJumpDuration = 0.2f;

    // --- NUEVO: Configuración de Escalada ---
    [Header("Wall Climb Settings")]
    public float climbSpeed = 5f;
    public float maxClimbTime = 2f; // Cuántos segundos puede trepar
    private float climbTimer;
    private bool isClimbing;

    [Header("Checks")]
    public Transform groundCheck;
    public Transform wallCheck;
    public float checkRadius = 0.2f;
    public LayerMask whatIsGround;

    [Header("Input")]
    [Tooltip("Dead zone exclusiva para el flip visual. Filtra el rebote mecánico del " +
             "stick analógico al soltarlo. No afecta la física del movimiento.\n" +
             "Rango recomendado: 0.2 – 0.35f")]
    public float flipDeadZone = 0.25f;

    [Header("Jump Buffer & Coyote")]
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.15f;

    [Header("DASH Settings")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.4f;
    public float dashDrag = 0.1f;

    // Estados Internos
    private Rigidbody2D rb;
    private Animator animator001;
    private float inputX;
    private float inputY; // --- NUEVO: Necesitamos input vertical ---
    private int facingDirection = 1;
    private float smoothInputVelocity;
    private bool isFacingRight = true;

    // Flags de Estado
    public bool isGrounded;
    public bool isTouchingWall;
    public bool isWallSliding;
    public bool canDash = true;
    private bool isDashing = false;
    private bool isDashDecelerating = false;
    private bool isPogoBouncing = false;

    // [RECOIL] Velocidad horizontal adicional al atacar; decae en MoveCharacterSmooth.
    private float recoilVelocityX = 0f;
    private const float RECOIL_DECAY = 14f; // qué tan rápido se amortigua
    private bool isRunning = false;

    // Flag para Doble Salto
    private bool canDoubleJump;

    // Flags de Control Físico
    private bool jumpRequest = false;
    private bool wallJumpRequest = false;
    private bool isWallJumpLockingControl = false;

    // Timers
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float airControlTimer;

    // Corrutinas
    private Coroutine dashRoutine;

    [Header("Effects")]
    private TrailRenderer tr;

    [Header("Sistema de Respawn Suave")]
    [HideInInspector] public Vector2 lastSafePosition;

    [Header("Seguridad de Respawn")]
    public float safeSpotWidth = 0.3f;
    public float minTimeOnGround = 0.2f;
    private float safeGroundTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator001 = GetComponent<Animator>();

        tr = GetComponent<TrailRenderer>();
        if (tr != null) tr.emitting = false;

        lastSafePosition = transform.position;

        defaultGravity = rb.gravityScale; // Guardamos la gravedad base (normalmente 3 o 4)
        climbTimer = maxClimbTime; // Tanque lleno al inicio

        if (GameManager01.instance != null)
        {
            PlayerData data = GameManager01.instance.currentData;

            unlockDoubleJump = data.unlockDoubleJump;
            unlockRun = data.unlockRun;
            unlockWallClimb = data.unlockWallClimb;
            unlockWallJump = data.unlockWallJump;
            unlockDash = data.unlockDash;

            // Opcional: Si el jugador viene de cargar partida (no de una puerta)
            // podríamos teletransportarlo aquí.
        }


    }

    void Update()
    {
        if (isDashing) return;
        if (DialogueManager.IsActive) return;

        // 1. CHEQUEOS
        CheckSurroundings();

        // Recargar habilidades al tocar suelo
        if (isGrounded)
        {
            canDoubleJump = true;
            climbTimer = maxClimbTime; // --- NUEVO: Recargar estamina de escalada ---
            isClimbing = false;
        }

        // 2. INPUTS
        inputX = Input.GetAxisRaw("Horizontal");
        inputY = Input.GetAxisRaw("Vertical"); // --- NUEVO ---

        // 3. LÓGICA DE RESPAWN
        if (isGrounded && !isDashing)
        {
            safeGroundTimer += Time.deltaTime;
            if (safeGroundTimer > minTimeOnGround)
            {
                if (CheckIfFullyGrounded())
                {
                    lastSafePosition = transform.position;
                }
            }
        }
        else
        {
            safeGroundTimer = 0f;
        }

        // 4. TIMERS DE SALTO
        if (Input.GetButtonDown("Jump")) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (isGrounded) coyoteTimeCounter = coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;

        // 5. FLIP VISUAL
        // Bloqueamos el flip si estamos trepando para evitar bugs visuales
        // [FIX-1] Dead zone exclusiva para el flip: Mathf.Abs(inputX) > flipDeadZone
        // en lugar de inputX != 0. Filtra el rebote mecánico del stick analógico
        // al soltarlo (GetAxisRaw no aplica la dead zone de Unity internamente).
        // El inputX completo sigue usándose para la física del movimiento.
        if (!isWallJumpLockingControl && Mathf.Abs(inputX) > flipDeadZone && !isDashing && !isClimbing)
        {
            facingDirection = (int)Mathf.Sign(inputX);
            transform.localScale = new Vector3(facingDirection, 1, 1);
            isFacingRight = facingDirection == 1;
        }

        // 6. LÓGICA DE WALL JUMP
        // Permitimos saltar tanto si deslizamos COMO si escalamos
        if (Input.GetButtonDown("Jump") && (isWallSliding || isClimbing) && !isDashing && unlockWallJump)
        {
            wallJumpRequest = true;
            jumpBufferCounter = 0;
        }

        // LÓGICA DE DOBLE SALTO
        // Añadimos !isClimbing a las restricciones
        if (Input.GetButtonDown("Jump") && !isGrounded && unlockDoubleJump && canDoubleJump && !isTouchingWall && !isClimbing)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            canDoubleJump = false;
            jumpBufferCounter = 0;
        }

        // 7. DASH INPUT (Tap)
        if (Input.GetButtonDown("Dash") && canDash && !isDashing && unlockDash)
        {
            if (dashRoutine != null) StopCoroutine(dashRoutine);
            dashRoutine = StartCoroutine(PerformDash());
        }

        // 8. SALTO NORMAL
        if (jumpBufferCounter > 0 && coyoteTimeCounter > 0 && !isDashing && !isWallSliding && !isClimbing)
        {
            jumpRequest = true;
            jumpBufferCounter = 0;
            coyoteTimeCounter = 0;
        }
        // 8. POGO
        if (isPogoBouncing && rb.linearVelocity.y <= 0f)
            isPogoBouncing = false;

        UpdateAnimations();
    }

    void FixedUpdate()
    {
        if (isDashing) return;
        if (DialogueManager.IsActive) return;

        if (isGrounded) isWallJumpLockingControl = false;

        // --- NUEVO: LÓGICA DE ESCALADA (Prioridad Alta) ---
        bool tryingToClimb = (inputY != 0); // ¿El jugador está presionando arriba o abajo?

        if (unlockWallClimb && isTouchingWall && !isGrounded && tryingToClimb && climbTimer > 0 && !isDashDecelerating && !isPogoBouncing)
        {
            isClimbing = true;
            isWallSliding = false; // Escalar anula deslizar

            // Quitamos gravedad para control total
            rb.gravityScale = 0;

            // Movemos en Y según el input
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, inputY * climbSpeed);

            // Consumimos energía
            climbTimer -= Time.fixedDeltaTime;
        }
        else
        {
            // Si estábamos escalando y dejamos de hacerlo (o se acabó el tiempo)
            if (isClimbing)
            {
                isClimbing = false;
                rb.gravityScale = defaultGravity; // Restauramos gravedad
            }

            // Wall Slide (Solo si NO estamos escalando)
            bool isPushingIntoWall = (inputX != 0 && Mathf.Sign(inputX) == Mathf.Sign(facingDirection));
            if (unlockWallJump && isTouchingWall && !isGrounded && rb.linearVelocity.y < 0 && isPushingIntoWall)
                isWallSliding = true;
            else
                isWallSliding = false;
        }

        if (isDashDecelerating) return;

        // WALL JUMP
        if (wallJumpRequest)
        {
            isClimbing = false; // Romper escalada al saltar
            rb.gravityScale = defaultGravity; // Restaurar gravedad por si acaso

            isWallJumpLockingControl = true;
            airControlTimer = wallJumpDuration;

            float jumpDirection = -facingDirection;
            transform.localScale = new Vector3(Mathf.Sign(jumpDirection), 1, 1);
            facingDirection = (int)jumpDirection;

            rb.linearVelocity = new Vector2(wallJumpPower.x * jumpDirection, wallJumpPower.y);
            wallJumpRequest = false;
        }

        // SALTO NORMAL
        if (jumpRequest)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpRequest = false;
        }

        // GRAVEDAD VARIABLE
        BetterJumpPhysics();

        // LÓGICA DE CORRER
        float targetMoveSpeed = moveSpeed;
        if (unlockRun && Input.GetButton("Dash") && !isWallJumpLockingControl)
        {
            targetMoveSpeed = runSpeed;
            isRunning = true;
        }
        else
        {
            isRunning = false;
        }

        // MOVIMIENTO HORIZONTAL
        // Desactivamos movimiento horizontal si estamos trepando para evitar separarnos de la pared
        if (isClimbing)
        {
            // Opcional: Empujar levemente hacia la pared para asegurar contacto
            rb.linearVelocity = new Vector2(facingDirection * 0.1f, rb.linearVelocity.y);
        }
        else if (isWallJumpLockingControl)
        {
            airControlTimer -= Time.fixedDeltaTime;
            if (airControlTimer <= 0) isWallJumpLockingControl = false;
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, new Vector2(0, rb.linearVelocity.y), 1f * Time.fixedDeltaTime);
        }
        else
        {
            MoveCharacterSmooth(targetMoveSpeed);
        }

        // WALL SLIDE FÍSICO
        if (isWallSliding && !isWallJumpLockingControl && !isClimbing)
        {
            WallSlideLerp();
        }
    }

    // --- FUNCIONES AUXILIARES ---

    void CheckSurroundings()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, whatIsGround);
        isTouchingWall = Physics2D.Raycast(wallCheck.position, new Vector2(transform.localScale.x, 0), 0.1f, whatIsGround); // Aumenté un poco el raycast para asegurar mejor agarre
    }

    bool CheckIfFullyGrounded()
    {
        Vector2 pos = transform.position;
        Vector2 offsetLeft = new Vector2(-safeSpotWidth, 0);
        Vector2 offsetRight = new Vector2(safeSpotWidth, 0);
        float checkDist = 1.0f;

        bool hitLeft = Physics2D.Raycast(pos + offsetLeft, Vector2.down, checkDist, whatIsGround);
        bool hitRight = Physics2D.Raycast(pos + offsetRight, Vector2.down, checkDist, whatIsGround);

        return hitLeft && hitRight;
    }

    void MoveCharacterSmooth(float currentSpeed)
    {
        // 1. MOVIMIENTO EN EL SUELO
        if (isGrounded)
        {
            float targetSpeed = inputX * currentSpeed;
            float currentSpeedX = Mathf.SmoothDamp(rb.linearVelocity.x - platformVelocity.x, targetSpeed, ref smoothInputVelocity, smoothTime);

            // AQUÍ ESTÁ LA MAGIA: Sumamos la velocidad de la plataforma al final
            rb.linearVelocity = new Vector2(currentSpeedX + platformVelocity.x + recoilVelocityX, rb.linearVelocity.y); // [RECOIL]
        }
        // 2. MOVIMIENTO AÉREO (Momentum)
        else
        {
            // En el aire, la plataforma ya no nos empuja, pero conservamos la inercia
            // gracias a que platformVelocity se vuelve (0,0) al salir, 
            // pero el Rigidbody ya trae el impulso acumulado.

            if (inputX != 0)
            {
                float targetSpeed = inputX * currentSpeed;
                float currentSpeedX = Mathf.SmoothDamp(rb.linearVelocity.x, targetSpeed, ref smoothInputVelocity, airControlSmooth);
                rb.linearVelocity = new Vector2(currentSpeedX + recoilVelocityX, rb.linearVelocity.y); // [RECOIL]
            }
            else
            {
                float currentSpeedX = Mathf.Lerp(rb.linearVelocity.x, 0, airDrag * Time.fixedDeltaTime);
                rb.linearVelocity = new Vector2(currentSpeedX + recoilVelocityX, rb.linearVelocity.y); // [RECOIL]
            }
        }

        // [RECOIL] Amortiguación del retroceso de ataque: decae hacia 0 cada FixedUpdate.
        recoilVelocityX = Mathf.Lerp(recoilVelocityX, 0f, RECOIL_DECAY * Time.fixedDeltaTime);
    }

    void WallSlideLerp()
    {
        float targetY = -wallSlideSpeed;
        float newY = Mathf.Lerp(rb.linearVelocity.y, targetY, 0.2f);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
    }

    void BetterJumpPhysics()
    {
        // No aplicar física de salto si estamos trepando
        if (isDashing || isWallSliding || isClimbing) return;

        if (rb.linearVelocity.y < 0)
        {
            float smoothFall = Mathf.Lerp(1f, fallMultiplier, Mathf.Clamp01(-rb.linearVelocity.y / 15f));
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (smoothFall - 1f) * Time.fixedDeltaTime;

            if (rb.linearVelocity.y < maxFallSpeed)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);
        }
        else if (rb.linearVelocity.y > 0 && !Input.GetButton("Jump") && !isWallJumpLockingControl && !isPogoBouncing)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    void UpdateAnimations()
    {
        if (animator001 == null) return;
        animator001.SetBool("isGrounded", isGrounded);

        float velocidadRelativa = Mathf.Abs(rb.linearVelocity.x - platformVelocity.x);
        //animator001.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));

        if (Mathf.Abs(inputX) < 0.01f)
        {
            velocidadRelativa = 0f;
        }

        animator001.SetFloat("Speed", velocidadRelativa);
        //  animator001.SetBool("isRunning", isRunning && Mathf.Abs(inputX) > 0.1f);

        // --- NUEVO: Parámetros para escalar ---
        //  animator001.SetBool("isClimbing", isClimbing); // Verdadero si trepa arriba/abajo
        //  animator001.SetBool("isWallSliding", isWallSliding); // Verdadero si solo resbala
        //  animator001.SetFloat("verticalVelocity", rb.linearVelocity.y); // Para animar si sube o baja
    }

    // --- CORRUTINA DE DASH ---
    IEnumerator PerformDash()
    {
        canDash = false;
        isDashing = true;
        animator001.SetBool("isDashing", isDashing);

        if (tr != null) tr.emitting = true;

        float dashDirX = (inputX != 0) ? inputX : facingDirection;
        Vector2 dashDir = new Vector2(dashDirX, 0).normalized;

        float originalGravity = isClimbing ? defaultGravity : rb.gravityScale;
        rb.gravityScale = 0;

        float t = 0f;
        while (t < dashDuration)
        {
            rb.linearVelocity = dashDir * dashSpeed;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.gravityScale = originalGravity;
        isDashing = false;
        animator001.SetBool("isDashing", isDashing);

        if (tr != null) tr.emitting = false;

        yield return StartCoroutine(DashDeceleration());
        dashRoutine = null;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    IEnumerator DashDeceleration()
    {
        isDashDecelerating = true;
        float decelTime = 0.1f;
        float t = 0;

        Vector2 startVel = rb.linearVelocity;
        Vector2 endVel = new Vector2(startVel.x * dashDrag, startVel.y);

        while (t < decelTime)
        {
            t += Time.fixedDeltaTime;
            float newX = Mathf.Lerp(startVel.x, endVel.x, t / decelTime);
            rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
            yield return new WaitForFixedUpdate();
        }
        isDashDecelerating = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (groundCheck != null) Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        if (wallCheck != null) Gizmos.DrawWireSphere(wallCheck.position, checkRadius);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 pos = transform.position;
        Vector3 offL = new Vector3(-safeSpotWidth, 0, 0);
        Vector3 offR = new Vector3(safeSpotWidth, 0, 0);

        Gizmos.DrawLine(pos + offL, pos + offL + Vector3.down);
        Gizmos.DrawLine(pos + offR, pos + offR + Vector3.down);
    }

    // Función pública para que el item de "Habilidad" la llame
    public void UnlockAbility(string abilityName)
    {
        if (abilityName == "DoubleJump") unlockDoubleJump = true;
        else if (abilityName == "Run") unlockRun = true;
        else if (abilityName == "WallClimb") unlockWallClimb = true; // --- NUEVO ---


        // --- NUEVO ---
        else if (abilityName == "WallJump") unlockWallJump = true;
        else if (abilityName == "Dash") unlockDash = true;



    }

    // [RECOIL] Llamado por RomeritoCombat al ejecutar un ataque.
    // fuerzaX: retroceso horizontal (negativo = hacia izquierda).
    // fuerzaY: empuje vertical instantáneo (negativo = hacia abajo, para ataque arriba).
    public void AplicarRecoilAtaque(float fuerzaX, float fuerzaY = 0f)
    {
        recoilVelocityX = fuerzaX;
        // El componente vertical es un nudge instantáneo (no decae; la gravedad lo corrige).
        if (Mathf.Abs(fuerzaY) > 0.01f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y + fuerzaY);
    }

    /// <summary>
    /// Cancela la caída de Romerito y aplica un rebote hacia arriba.
    /// Llamado por RomeritoCombat cuando el Pogo impacta un objetivo.
    /// </summary>
    /// <param name="force">Fuerza del rebote. Varía según el Favor activo.</param>
    public void ApplyPogoBounce(float force)
    {

        Debug.Log($"[Pogo] ANTES: vy={rb.linearVelocity.y}");
        // Cancelar la velocidad vertical actual (la caída)
        // para que el rebote siempre tenga la misma altura,
        // independientemente de cuánto estemos cayendo.
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);

        // Aplicar el impulso hacia arriba
        rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        Debug.Log($"[Pogo] DESPUÉS: vy={rb.linearVelocity.y}");
        isPogoBouncing = true; // ← AÑADIR
        // Restablecer el doble salto para que el jugador
        // pueda encadenar rebotes con plataformas o saltar tras el Pogo
        if (unlockDoubleJump)
            canDoubleJump = true;

        Debug.Log($"[Pogo] Rebote aplicado con fuerza {force}");
    }

    // ── Congelado durante diálogos ───────────────────────────

    void OnEnable()
    {
        if (DialogueManager.Instance == null) return;
        DialogueManager.Instance.OnDialogueStarted += CongelarParaDialogo;
        DialogueManager.Instance.OnDialogueEnded += DescongelarDespuesDialogo;
    }

    void OnDisable()
    {
        if (DialogueManager.Instance == null) return;
        DialogueManager.Instance.OnDialogueStarted -= CongelarParaDialogo;
        DialogueManager.Instance.OnDialogueEnded -= DescongelarDespuesDialogo;
    }

    void CongelarParaDialogo()
    {
        rb.linearVelocity = Vector2.zero;
        // RomeritoMovement y RomeritoCombat se desactivan solos
        // por el guard "if (DialogueManager.IsActive) return;" en sus Updates.
        // No hace falta .enabled = false.
    }

    void DescongelarDespuesDialogo(Conversation _)
    {
        // El movimiento se reactiva solo cuando IsActive vuelve a false.
        // No hace falta hacer nada extra aquí, pero puedes añadir
        // efectos visuales de "reaparición" si quieres.
    }


}