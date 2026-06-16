using UnityEngine;

public class RomeritoCombat : MonoBehaviour
{
    // Ahora "Neutro" es un estado válido de ataque, no es "nada"
    public enum GodFavor { Neutro, Huehueteotl, Tlaloc, Tepeyollotl }

    [Header("Estado del Jugador")]
    public bool tieneMacuahuitl = false; // <--- ESTO controla si puedes atacar o no
    public GodFavor currentFavor = GodFavor.Neutro; // Empieza en Neutro por defecto

    [Header("Desbloqueables (Favores)")]
    // --- NUEVO: Banderas para saber qué dioses tenemos ---
    public bool unlockHuehueteotl = false; // Fuego
    public bool unlockTlaloc = false;         // Viento
    public bool unlockTepeyollotl = false;    // Tierra

    [Header("Configuración de Ataque")]
    public Transform attackPoint;
    [Tooltip("Punto de referencia para el hitbox del ataque hacia arriba. " +
             "Colócalo como hijo de Romerito, centrado sobre su cabeza.")]
    public Transform attackPointUp;
    public LayerMask enemyLayers;
    [Tooltip("Layer 'Destructible' — para puertas y paredes rompibles con el Macahuitl")]
    public LayerMask destructibleLayer;

    [Header("Estadísticas Base")]
    public float attackRange = 0.5f;
    public int attackDamage = 1;     // Daño base del arma normal
    public float attackRate = 1f;
    private float nextAttackTime = 0f;

    // Pogo tiene su propio cooldown — más corto que el ataque normal
    // para que los rebotes encadenados sean posibles.
    [Tooltip("Tiempo mínimo entre rebotes de Pogo. Debe ser menor que 1/attackRate.")]
    public float pogoCooldown = 0.15f;
    private float nextPogoTime = 0f;

    // [RECOIL-FIX] Flag: ApplyDamageAreaDesde lo activa cuando impacta algo.
    // Solo se aplica recoil si el golpe conectó — no en ataques al aire.
    private bool _golpeConectado = false;

    // Durante unos frames tras un rebote, forzamos enElAire = true aunque
    // isGrounded aún no se haya actualizado en FixedUpdate.
    private float pogoAireTimer = 0f;
    private const float POGO_AIRE_GRACIA = 0.12f; // segundos de gracia
    // [RECOIL] Retroalimentación física al atacar: Romerito da un paso atrás.
    // La fuerza se inyecta en RomeritoMovement.recoilVelocityX y decae sola.
    [Header("Recoil de Ataque")]
    [Tooltip("Retroceso horizontal al atacar lateralmente (en m/s).")]
    public float recoilFuerzaLateral = 3f;
    [Tooltip("Empuje hacia abajo al atacar arriba — planta los pies en el suelo " +
             "o frena el salto ligeramente.")]
    public float recoilFuerzaArriba = 1.5f;

    [Header("─── POGO ───────────────────────────────")]


    public Transform pogoPoint;
    public float pogoRadius = 0.4f;
    public float pogoFuerza = 4f;
    public GameObject pogoEffect;


    [Header("Referencias Visuales")]
    public Animator anim;
    public GameObject fireEffect;
    public GameObject windEffect;
    // public GameObject earthEffect; // Ejemplo para Tlaltecuhtli

    [Header("Placeholder Visual (Pre-Animación)")]
    [Tooltip("Componente que activa/desactiva GOs de ataque mientras no hay sprites animados.")]
    public AttackVFXController vfxController;

    private RomeritoMovement _movement; // Para leer isGrounded y aplicar el rebote

    [Header("Sprite del Arma")]
    [Tooltip("El SpriteRenderer hijo que muestra el Macahuitl en mano de Romerito. " +
             "Se oculta al inicio y aparece cuando Romerito lo recoge del altar.")]
    public SpriteRenderer macahuitlSprite;

    void Start()
    {
        _movement = GetComponent<RomeritoMovement>();

        // El sprite del arma empieza invisible — aparece al recoger el Macahuitl
        if (macahuitlSprite != null)
            macahuitlSprite.enabled = false;

        if (GameManager01.instance != null)
        {
            PlayerData data = GameManager01.instance.currentData;

            tieneMacuahuitl = data.tieneMacuahuitl;
            unlockHuehueteotl = data.unlockHuehueteotl;
            unlockTlaloc = data.unlockTlaloc;
            unlockTepeyollotl = data.unlockTepeyollotl;
        }
    }

    void Update()
    {

        if (DialogueManager.IsActive) return;

        // 1. Si NO tenemos el arma física, no hacemos nada al presionar botón
        if (!tieneMacuahuitl) return;

        // Decrementar timer de gracia del pogo
        if (pogoAireTimer > 0f) pogoAireTimer -= Time.deltaTime;

        // --- NUEVO: CAMBIO DE ARMA (Input) ---
        CheckWeaponSwitch();

        if (Input.GetButtonDown("Fire1"))
        {
            // FIX Bug 1: el pogo usa su propio cooldown (más corto),
            // el ataque normal usa nextAttackTime.
            // Así los rebotes encadenados no quedan bloqueados por attackRate.

            // FIX Bug 2: pogoAireTimer garantiza enElAire = true durante
            // los frames de gracia tras un rebote, aunque isGrounded aún
            // no se haya actualizado en FixedUpdate.
            bool enElAire = (_movement != null && !_movement.isGrounded)
                            || (pogoAireTimer > 0f);
            float verticalInput = Input.GetAxisRaw("Vertical");
            bool presionaAbajo = (verticalInput < 0f);
            bool presionaArriba = (verticalInput > 0f);

            if (enElAire && presionaAbajo && Time.time >= nextPogoTime)
            {
                EjecutarPogo();
                // El pogo NO consume nextAttackTime — permite atacar
                // inmediatamente después de aterrizar sin penalización.
                nextPogoTime = Time.time + pogoCooldown;
            }
            else if (presionaArriba && Time.time >= nextAttackTime)
            {
                PerformAttackUp();
                nextAttackTime = Time.time + 1f / attackRate;
            }
            else if (!presionaAbajo && !presionaArriba && Time.time >= nextAttackTime)
            {
                PerformAttack();
                nextAttackTime = Time.time + 1f / attackRate;
            }
        }
    }


    // ────────────────────────────────────────────────────────────
    //  POGO — Lógica Principal
    // ────────────────────────────────────────────────────────────

    void EjecutarPogo()
    {
        if (pogoPoint == null)
        {
            Debug.LogWarning("[Pogo] No hay 'pogoPoint' asignado en el Inspector. " +
                             "Crea un hijo vacío debajo de Romerito y asígnalo.");
            return;
        }

        // [FIX-1] El VFX se muestra SIEMPRE que el jugador ejecuta el input
        // correcto (en el aire + abajo + Fire1), sin importar si hay enemigos
        // debajo. El feedback visual es inmediato e incondicional.
        // Antes estaban al final, bloqueados por el guard de "rebotar".
        vfxController?.MostrarPogo();
        SpawnPogoEffect();

        // Detectar enemigos y objetos destruibles DEBAJO de Romerito
        Collider2D[] impactados = Physics2D.OverlapCircleAll(
            pogoPoint.position, pogoRadius, enemyLayers);

        // [FIX-2] Sin impacto → ataque visual sin rebote. Se elimina el
        // ApplyDamageArea() que usaba attackPoint (punto lateral) en lugar
        // de pogoPoint — era código muerto que no detectaba nada correcto.
        if (impactados.Length == 0)
        {
            Debug.Log("[Pogo] Sin impacto — ataque visual ejecutado, sin rebote.");
            return;
        }

        // ── Golpeó algo: aplicar daño + rebote según el favor ──
        bool rebotar = false;

        foreach (Collider2D col in impactados)
        {
            // Caso A — Enemigo
            EnemyDummy enemigo = col.GetComponent<EnemyDummy>();
            if (enemigo != null)
            {
                enemigo.TakeDamage(attackDamage);
                rebotar = true;
            }

            // Caso B — Objeto destruible
            ObjetoDestruible destruible = col.GetComponent<ObjetoDestruible>();
            if (destruible != null)
            {
                destruible.RecibirGolpe(attackDamage, currentFavor);
                rebotar = true;
            }
        }

        if (!rebotar) return;

        // FIX Bug 3: activar I-frames brevemente para que el contacto
        // con el enemigo justo después del rebote no infliga daño.
        // RomeritoHealth expone un método público para esto.
        RomeritoHealth health = GetComponent<RomeritoHealth>();
        if (health != null) health.ActivarIFramesPogo();

        // Reiniciar el timer de gracia: los próximos frames cuentan
        // como "en el aire" aunque FixedUpdate tarde en actualizarse.
        pogoAireTimer = POGO_AIRE_GRACIA;

        // ── Animación ─────────────────────────────────────────
        //anim?.SetTrigger("Attack");

        // ── Rebote según el Favor activo ──────────────────────
        switch (currentFavor)
        {
            case GodFavor.Neutro:
                _movement.ApplyPogoBounce(pogoFuerza);
                break;

            case GodFavor.Huehueteotl:                          // ← AÑADIR
                if (fireEffect != null)
                {
                    GameObject fx = Instantiate(fireEffect, pogoPoint.position, Quaternion.identity);
                    Destroy(fx, 1f);
                }
                _movement.ApplyPogoBounce(pogoFuerza * 1.3f);  // Rebote un 30% más alto
                break;

            case GodFavor.Tlaloc:                               // ← AÑADIR
                if (windEffect != null)
                {
                    GameObject fx = Instantiate(windEffect, pogoPoint.position, Quaternion.identity);
                    Destroy(fx, 1f);
                }
                _movement.ApplyPogoBounce(pogoFuerza * 1.6f);  // Rebote máximo (viento)
                break;

            case GodFavor.Tepeyollotl:                          // ← AÑADIR
                _movement.ApplyPogoBounce(pogoFuerza);          // Rebote base + onda sísmica

                break;

        }



        Debug.Log($"[Pogo] ¡Rebote! Favor: {currentFavor} | Impactados: {impactados.Length}");
    }


    // --- NUEVO: Lógica de Cambio de Favor ---
    void CheckWeaponSwitch()
    {
        // Tecla 1: Neutro (Siempre disponible si tienes el arma)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentFavor = GodFavor.Neutro;
            Debug.Log("Favor: Neutro");
        }

        // Tecla 2: Huehueteotl (Requiere desbloqueo)
        if (Input.GetKeyDown(KeyCode.Alpha2) && unlockHuehueteotl)
        {
            currentFavor = GodFavor.Huehueteotl;
            Debug.Log("Favor: Huehueteotl  (Fuego)");
        }

        // Tecla 3: Tlaloc (Requiere desbloqueo)
        if (Input.GetKeyDown(KeyCode.Alpha3) && unlockTlaloc)
        {
            currentFavor = GodFavor.Tlaloc;
            Debug.Log("Favor: Tlaloc (Viento)");
        }

        // Tecla 4: Tepeyollotl (Requiere desbloqueo)
        if (Input.GetKeyDown(KeyCode.Alpha4) && unlockTepeyollotl)
        {
            currentFavor = GodFavor.Tepeyollotl;
            Debug.Log("Favor: Tlaltecuhtli (Tierra)");
        }
    }

    void PerformAttack()
    {
        // Activar animación (puede ser la misma para todos o variar con un BlendTree)
        if (anim != null) anim.SetTrigger("Attack");

        // ── Placeholder visual ────────────────────────────────────────────────
        // Muestra el GO de ataque correspondiente al favor activo.
        // Cuando existan animaciones reales, basta con eliminar esta línea.
        vfxController?.MostrarAtaqueNormal((int)currentFavor);

        _golpeConectado = false; // [RECOIL-FIX] Resetear antes de intentar el golpe

        // Lógica según el favor seleccionado
        switch (currentFavor)
        {
            case GodFavor.Neutro:
                // --- ATAQUE NORMAL (Físico) ---
                // Sin efectos especiales, daño base puro.
                ApplyDamageArea(attackDamage, 0f);
                Debug.Log("Ataque Físico Normal");
                break;

            case GodFavor.Huehueteotl:
                // --- ATAQUE DE FUEGO ---
                // Más daño + Partículas
                if (fireEffect) Instantiate(fireEffect, attackPoint.position, Quaternion.identity);
                ApplyDamageArea(attackDamage + 1, 0f);
                break;

            case GodFavor.Tlaloc:
                // --- ATAQUE DE VIENTO ---
                // Daño normal + Empuje (Knockback) fuerte
                if (windEffect) Instantiate(windEffect, attackPoint.position, Quaternion.identity);
                ApplyDamageArea(attackDamage, 8f); // 8f de fuerza de empuje
                break;

            case GodFavor.Tepeyollotl:
                // --- ATAQUE DE TIERRA ---
                // Daño alto + Rango mayor + Lento (esto se ajustaría en attackRate)
                ApplyDamageArea(attackDamage + 2, 2f, attackRange * 1.5f);
                break;
        }

        // [RECOIL-FIX] Solo retrocede si el golpe conectó con un objetivo
        if (_golpeConectado && _movement != null)
            _movement.AplicarRecoilAtaque(-transform.localScale.x * recoilFuerzaLateral);
    }

    // ────────────────────────────────────────────────────────────
    //  ATAQUE SUPERIOR — Arriba + Fire1
    // ────────────────────────────────────────────────────────────

    void PerformAttackUp()
    {
        if (anim != null) anim.SetTrigger("Attack");

        // ── Placeholder visual ────────────────────────────────
        vfxController?.MostrarAtaqueSuperior((int)currentFavor);

        _golpeConectado = false; // [RECOIL-FIX] Resetear antes de intentar el golpe

        // Usamos attackPointUp como origen del hitbox.
        // Si no está asignado, avisamos y salimos.
        if (attackPointUp == null)
        {
            Debug.LogWarning("[Combat] attackPointUp no asignado. " +
                             "Crea un hijo vacío sobre la cabeza de Romerito y asígnalo.");
            return;
        }

        // El ataque superior comparte la lógica de daño con el normal,
        // pero parte de attackPointUp en lugar de attackPoint.
        // ApplyDamageAreaDesde permite especificar el origen sin duplicar código.
        switch (currentFavor)
        {
            case GodFavor.Neutro:
                ApplyDamageAreaDesde(attackPointUp.position, attackDamage, 0f);
                break;

            case GodFavor.Huehueteotl:
                if (fireEffect) Instantiate(fireEffect, attackPointUp.position, Quaternion.identity);
                ApplyDamageAreaDesde(attackPointUp.position, attackDamage + 1, 0f);
                break;

            case GodFavor.Tlaloc:
                if (windEffect) Instantiate(windEffect, attackPointUp.position, Quaternion.identity);
                ApplyDamageAreaDesde(attackPointUp.position, attackDamage, 8f);
                break;

            case GodFavor.Tepeyollotl:
                ApplyDamageAreaDesde(attackPointUp.position, attackDamage + 2, 2f, attackRange * 1.5f);
                break;
        }

        // [RECOIL-FIX] Solo retrocede si el golpe conectó con un objetivo
        if (_golpeConectado && _movement != null)
            _movement.AplicarRecoilAtaque(0f, -recoilFuerzaArriba);
    }

    // Wrapper para el ataque lateral — mantiene compatibilidad con llamadas existentes
    void ApplyDamageArea(int dmg, float knockbackForce, float customRange = -1)
    {
        ApplyDamageAreaDesde(attackPoint.position, dmg, knockbackForce, customRange);
    }

    // Función base: recibe el origen del hitbox como parámetro.
    // Usada por el ataque normal (attackPoint) y el superior (attackPointUp).
    void ApplyDamageAreaDesde(Vector2 origen, int dmg, float knockbackForce, float customRange = -1)
    {
        float range = (customRange > 0) ? customRange : attackRange;

        // Detectamos todo lo que esté en la capa de "Enemigos" o "Destruibles"
        Collider2D[] hitObjects = Physics2D.OverlapCircleAll(origen, range, enemyLayers);

        foreach (Collider2D obj in hitObjects)
        {
            // --- CASO A: ENEMIGOS ---
            EnemyDummy enemyScript = obj.GetComponent<EnemyDummy>();
            if (enemyScript != null)
            {
                enemyScript.TakeDamage(dmg);
                _golpeConectado = true; // [RECOIL-FIX]

                // Lógica de Empuje (Knockback)
                if (knockbackForce > 0)
                {
                    Rigidbody2D enemyRb = obj.GetComponent<Rigidbody2D>();

                    // ★ FIX (Saltarín gira/se traba a medio salto): si el enemigo
                    // tiene su propio MictecahBase, su knockback YA se aplica
                    // automáticamente vía EnemyDummy.OnHurt → RecibirGolpe()
                    // (tunable por enemigo: knockbackForce/knockbackUp en el
                    // Inspector). Aplicar este AddForce ENCIMA hacía que dos
                    // sistemas de física pelearan por el mismo Rigidbody2D en
                    // el mismo golpe. Solo lo usamos para enemigos "dummy"
                    // sin IA propia (que no tienen knockback por su cuenta).
                    bool tieneKnockbackPropio = obj.GetComponent<MictecahBase>() != null;

                    if (enemyRb != null && !tieneKnockbackPropio)
                    {
                        Vector2 direction = (obj.transform.position - transform.position).normalized;
                        direction += Vector2.up * 0.5f;
                        enemyRb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
                    }
                }
                continue;
            }

            // --- CASO B: OBJETOS DESTRUIBLES ---
            ObjetoDestruible paredScript = obj.GetComponent<ObjetoDestruible>();
            if (paredScript != null)
            {
                paredScript.RecibirGolpe(dmg, currentFavor);
                _golpeConectado = true; // [RECOIL-FIX]
            }
        }

        // --- CASO C: PUERTAS DESTRUCTIBLES (RoomDoor) ---
        // Usa una LayerMask separada ("Destructible") para no mezclar con enemigos.
        if (destructibleLayer != 0)
        {
            Collider2D[] hitDoors = Physics2D.OverlapCircleAll(origen, range, destructibleLayer);

            foreach (Collider2D hit in hitDoors)
            {
                RoomDoor door = hit.GetComponent<RoomDoor>();
                if (door != null) { door.RecibirGolpe(); _golpeConectado = true; } // [RECOIL-FIX]
            }
        }
    }

    void SpawnPogoEffect()
    {
        if (pogoEffect == null) return;
        GameObject fx = Instantiate(pogoEffect, pogoPoint.position, Quaternion.identity);
        Destroy(fx, 0.5f);
    }

    // ────────────────────────────────────────────────────────────
    //  GIZMOS — Visualización del radio del Pogo en el Editor
    // ────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        // Rango de ataque normal (rojo)
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        // Rango del ataque superior (amarillo)
        if (attackPointUp != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPointUp.position, attackRange);
        }

        // Rango del Pogo (magenta)
        if (pogoPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(pogoPoint.position, pogoRadius);
        }
    }

    // --- FUNCIONES PÚBLICAS PARA ITEMS ---

    // Llamar a esto cuando recojas el arma por primera vez
    public void EquiparMacuahuitl()
    {
        tieneMacuahuitl = true;
        currentFavor = GodFavor.Neutro;

        // Mostrar el sprite del arma en mano de Romerito
        if (macahuitlSprite != null)
            macahuitlSprite.enabled = true;

        Debug.Log("¡Macuahuitl obtenido!");
    }

    // --- NUEVO: Función para desbloquear dioses específicos ---
    public void UnlockFavor(string favorName)
    {
        if (favorName == "Huehueteotl") unlockHuehueteotl = true;
        else if (favorName == "Tlaloc") unlockTlaloc = true;
        else if (favorName == "Tepeyollotl") unlockTepeyollotl = true;

        Debug.Log("¡Nuevo favor desbloqueado: " + favorName + "!");
    }

    // Llamar a esto cuando desbloquees o cambies de dios
    public void CambiarFavor(GodFavor nuevoFavor)
    {
        if (!tieneMacuahuitl) return; // No puedes tener favor sin arma
        currentFavor = nuevoFavor;
        Debug.Log("Favor cambiado a: " + nuevoFavor);
    }
}