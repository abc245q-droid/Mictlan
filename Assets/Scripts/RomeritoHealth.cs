using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RomeritoHealth : MonoBehaviour
{
    [Header("Configuración de Vida")]
    public int maxHealth = 3;
    public int currentHealth;

    [Header("Invulnerabilidad (I-Frames)")]
    public float iFramesDuration = 1.5f;
    public int numberOfFlashes = 3;

    // ════════════════════════════════════════════════════════════════
    // [FIX-2] INTANGIBILIDAD ROBUSTA — Triple defensa:
    //
    //   A) Rigidbody2D.excludeLayers → cubre TODOS los colliders del
    //      rigidbody de Romerito, incluidos los hijos (compound colliders),
    //      cosa que GetComponents<Collider2D>() (solo raíz) NO hacía.
    //
    //   B) Physics2D.IgnoreCollision por pareja de colliders, buscando a
    //      los enemigos POR COMPONENTE (EnemyDummy / MictecahBase / EnemyAI /
    //      FlyingEnemyAI). Funciona AUNQUE las layers estén mal asignadas.
    //
    //   C) Diagnóstico en Start(): si detecta enemigos cuya layer NO es la
    //      configurada, lo grita en consola con instrucciones de cómo
    //      arreglarlo.
    // ════════════════════════════════════════════════════════════════
    [Header("Intangibilidad en I-Frames")]
    [Tooltip("Nombre de la Layer de los enemigos. IMPORTANTE: escribirlo aquí NO basta — " +
             "cada GameObject enemigo debe tener esta layer asignada en el dropdown 'Layer' " +
             "de la esquina superior de su Inspector (o en su Prefab).")]
    public string enemyLayerName = "Enemy";

    [Tooltip("Referencia a MacahuitlPickup para activar el sprite del altar " +
             "exactamente cuando Romerito reaparece.")]
    public MacahuitlPickup macahuitlPickup;


    [Header("Muerte y Respawn")]
    [Tooltip("Segundos que Romerito permanece muerto/invisible antes de reaparecer. " +
             "El MacahuitlRoomManager usa este tiempo para limpiar la sala.")]
    public float deathPauseDuration = 2f;

    [Header("Referencias")]
    public HeartSystem heartSystem;
    public RomeritoCombat combatScript;

    [Header("Tonalli — Curación")]
    [Tooltip("Referencia al TonalliSystem del HUD Canvas. Arrastrarlo desde la escena.")]
    public TonalliSystem tonalliSystem;

    [Tooltip("Costo en Tonalli para curar un corazón. Con maxTonalliBase=300, " +
             "100 representa un tercio de la barra (3 curaciones desde llena).")]
    public float costoTonalliCurar = 100f;

    [Header("Curación — Hold (canal continuo)")]
    [Tooltip("Segundos que hay que mantener Y para recuperar cada corazón.")]
    public float tiempoCarga = 1.5f;

    [Tooltip("VFX de carga sobre el cuerpo de Romerito. Si se deja vacío, se busca " +
             "un CargaTonalliVFX en este mismo GameObject.")]
    public CargaTonalliVFX cargaVFX;

    [Tooltip("Zona muerta del stick: por debajo de esto se considera 'quieto'.")]
    public float deadzoneMovimiento = 0.2f;

    // ── Estado interno ───────────────────────────────────────
    private bool isInvulnerable = false;
    private bool isDead = false;

    // Estado del hold de curación (canal continuo)
    private bool estaCargando = false;
    private float cargaTimer = 0f;

    // [FIX-2] Máscara de layer enemiga (puede ser 0 si la layer no existe;
    // en ese caso la defensa B sigue funcionando igual).
    private int enemyLayerMask = 0;

    // [FIX-2] Parejas de colliders ignoradas con Physics2D.IgnoreCollision,
    // guardadas para restaurarlas exactamente al terminar los I-frames.
    private readonly List<(Collider2D mine, Collider2D theirs)> ignoredPairs
        = new List<(Collider2D, Collider2D)>();

    private SpriteRenderer spriteRend;     // Renderer raíz (para el parpadeo de I-Frames)
    private SpriteRenderer[] allSprites;   // Raíz + todos los hijos (Macahuitl, etc.)

    private Animator anim;
    private Rigidbody2D rb;
    private RomeritoMovement movement;
    private Collider2D[] allColliders;     // Colliders del RAÍZ (muerte/respawn — sin cambios)
    private Collider2D[] allCollidersDeep; // [FIX-2] Raíz + hijos (para intangibilidad)

    // ── Evento para MacahuitlRoomManager ─────────────────────
    public event System.Action OnPlayerDied;

    private System.Func<IEnumerator> resetRoutineFactory = null;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        currentHealth = maxHealth;
        spriteRend = GetComponent<SpriteRenderer>();
        allSprites = GetComponentsInChildren<SpriteRenderer>();

        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<RomeritoMovement>();
        allColliders = GetComponents<Collider2D>();
        allCollidersDeep = GetComponentsInChildren<Collider2D>(true);

        if (combatScript == null)
            combatScript = GetComponent<RomeritoCombat>();

        // Auto-buscar en escena si no están asignados en el Inspector
        if (heartSystem == null)
            heartSystem = FindObjectOfType<HeartSystem>(true); // true = busca también inactivos
        if (tonalliSystem == null)
            tonalliSystem = FindObjectOfType<TonalliSystem>(true);

        if (cargaVFX == null)
            cargaVFX = GetComponent<CargaTonalliVFX>();

        Debug.Log($"[RomeritoHealth] HeartSystem: {(heartSystem != null ? "ENCONTRADO" : "NULL")}");
        Debug.Log($"[RomeritoHealth] TonalliSystem: {(tonalliSystem != null ? "ENCONTRADO" : "NULL")}");

        if (heartSystem != null)
            heartSystem.InitHearts(maxHealth);

        if (tonalliSystem != null)
        {
            float bonus = GameManager01.instance != null
                ? GameManager01.instance.currentData.bonusCapacidadTonalli
                : 0f;
            tonalliSystem.Inicializar(bonus, 0f);
        }

        // [FIX-2] Resolver máscara + diagnóstico de configuración.
        enemyLayerMask = LayerMask.GetMask(enemyLayerName);
        if (enemyLayerMask == 0)
            Debug.LogWarning($"[RomeritoHealth] La layer '{enemyLayerName}' no existe en " +
                             "Project Settings > Tags and Layers. La intangibilidad usará " +
                             "solo el modo por-componente (defensa B).");

        DiagnosticarLayersEnemigas();
    }

    void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                       UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Re-buscar referencias UI de la nueva escena
        heartSystem = FindObjectOfType<HeartSystem>(true);
        tonalliSystem = FindObjectOfType<TonalliSystem>(true);

        if (heartSystem != null)
            heartSystem.InitHearts(maxHealth);

        if (tonalliSystem != null)
        {
            float bonus = GameManager01.instance != null
                ? GameManager01.instance.currentData.bonusCapacidadTonalli
                : 0f;
            tonalliSystem.Inicializar(bonus, 0f);
        }

        Debug.Log($"[RomeritoHealth] Escena cargada: {scene.name} | " +
                  $"HeartSystem: {(heartSystem != null ? "OK" : "NULL")} | " +
                  $"TonalliSystem: {(tonalliSystem != null ? "OK" : "NULL")}");
    }

    // ── Input de curación ────────────────────────────────────
    // El botón "Curar" debe estar configurado en el Input Manager:
    //   Positive Button: joystick button 3  (Xbox Y / PS Triangle)
    //   Alt Positive Button: f  (teclado — para probar)
    void Update()
    {
        if (isDead) { CancelarCarga(); return; }
        if (DialogueManager.IsActive) { CancelarCarga(); return; }

        ManejarCargaCuracion();
    }

    // ── Curación con hold (canal continuo) ───────────────────
    // Mantener Y, quieto y en el suelo, recupera 1 corazón cada
    // 'tiempoCarga' segundos mientras haya Tonalli y falte vida.
    // Recibir daño (i-frames), moverse, saltar o soltar Y la cancela.
    private void ManejarCargaCuracion()
    {
        bool tieneDon = GameManager01.instance != null &&
                        GameManager01.instance.currentData.tieneDonDeTlacua;

        bool puedeCargar =
            tieneDon &&
            !isInvulnerable &&                                   // recibir daño cancela
            Input.GetButton("Curar") &&                          // Y mantenida
            movement != null && movement.isGrounded &&           // en el suelo
            Mathf.Abs(Input.GetAxisRaw("Horizontal")) < deadzoneMovimiento && // quieto
            currentHealth < maxHealth &&                         // falta vida
            tonalliSystem != null &&
            tonalliSystem.TieneSuficiente(costoTonalliCurar);    // hay Tonalli

        if (!puedeCargar)
        {
            CancelarCarga();
            return;
        }

        if (!estaCargando)
        {
            estaCargando = true;
            cargaTimer = 0f;
        }

        cargaTimer += Time.deltaTime;

        if (cargaVFX != null)
            cargaVFX.SetProgreso(Mathf.Clamp01(cargaTimer / tiempoCarga));

        if (cargaTimer >= tiempoCarga)
        {
            Curar();                       // +1 corazón, gasta Tonalli
            if (cargaVFX != null) cargaVFX.Destello();
            cargaTimer = 0f;               // canal continuo: reinicia el ciclo
            // La siguiente iteración revalida vida<max y Tonalli suficiente.
        }
    }

    private void CancelarCarga()
    {
        if (!estaCargando) return;
        estaCargando = false;
        cargaTimer = 0f;
        if (cargaVFX != null) cargaVFX.Apagar();
    }

    /// <summary>
    /// Gasta una vasija de Tonalli para recuperar un corazón.
    /// Si no hay Tonalli suficiente o la vida ya es máxima, no hace nada.
    /// Llamado desde Update() con el input "Curar", o externamente (ítems, etc.).
    /// </summary>
    public void Curar()
    {
        if (isDead) return;
        if (currentHealth >= maxHealth)
        {
            Debug.Log("[Curar] Vida ya es máxima — no se gasta Tonalli.");
            return;
        }

        if (tonalliSystem == null)
        {
            Debug.LogWarning("[Curar] No hay TonalliSystem asignado en RomeritoHealth.");
            return;
        }

        if (!tonalliSystem.GastarTonalli(costoTonalliCurar))
        {
            Debug.Log("[Curar] Tonalli insuficiente.");
            return;
        }

        currentHealth = Mathf.Min(currentHealth + 1, maxHealth);
        if (heartSystem != null) heartSystem.UpdateHearts(currentHealth);

        Debug.Log($"[Curar] +1 corazón. Vida: {currentHealth}/{maxHealth}");
    }

    // [FIX-2][C] Revisa los enemigos en escena y avisa si su layer real
    // no coincide con enemyLayerName. Esto detecta el error más común:
    // escribir el nombre en el Inspector pero olvidar asignar la layer
    // en los GameObjects/Prefabs de los enemigos.
    private void DiagnosticarLayersEnemigas()
    {
        int malConfigurados = 0;
        foreach (var col in ObtenerCollidersEnemigos())
        {
            if (col == null) continue;
            string layerReal = LayerMask.LayerToName(col.gameObject.layer);
            if (layerReal != enemyLayerName)
            {
                malConfigurados++;
                Debug.LogWarning($"[RomeritoHealth] ⚠ El enemigo '{col.transform.root.name}' " +
                                 $"está en la layer '{layerReal}', no en '{enemyLayerName}'. " +
                                 "Asígnale la layer correcta en el dropdown 'Layer' de su " +
                                 "Inspector (idealmente en el Prefab, marcando 'Yes, change children').");
            }
        }
        if (malConfigurados > 0)
            Debug.LogWarning($"[RomeritoHealth] {malConfigurados} collider(s) enemigos con layer " +
                             "incorrecta. La defensa por-componente los cubrirá de todas formas, " +
                             "pero conviene corregir las layers.");
    }

    // [FIX-2][B] Reúne los colliders (raíz + hijos) de TODOS los enemigos en
    // escena, identificándolos por sus componentes — independiente de layers.
    private List<Collider2D> ObtenerCollidersEnemigos()
    {
        var resultado = new List<Collider2D>();
        var yaVistos = new HashSet<GameObject>();

        void Agregar(Component[] comps)
        {
            foreach (var c in comps)
            {
                GameObject raiz = c.transform.root.gameObject;
                if (!yaVistos.Add(raiz)) continue;            // evitar duplicados
                if (raiz == gameObject) continue;             // nunca el propio Romerito
                resultado.AddRange(raiz.GetComponentsInChildren<Collider2D>(true));
            }
        }

        Agregar(Object.FindObjectsByType<EnemyDummy>(FindObjectsSortMode.None));
        Agregar(Object.FindObjectsByType<MictecahBase>(FindObjectsSortMode.None));
        Agregar(Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None));
        Agregar(Object.FindObjectsByType<FlyingEnemyAI>(FindObjectsSortMode.None));

        return resultado;
    }

    // ── API para que MacahuitlRoomManager inyecte su reset ───

    public void SetResetRoutine(System.Func<IEnumerator> factory)
    {
        resetRoutineFactory = factory;
    }

    public void ClearResetRoutine()
    {
        resetRoutineFactory = null;
    }

    // ── Daño ─────────────────────────────────────────────────

    public void TakeDamage(int damage)
    {
        if (isInvulnerable || isDead) return;

        currentHealth -= damage;
        if (heartSystem != null) heartSystem.UpdateHearts(currentHealth);
        Debug.Log("¡Romerito herido! Vida restante: " + currentHealth);

        if (currentHealth <= 0)
        {
            StartCoroutine(DieRoutine());
            return;
        }

        StartCoroutine(InvunerabilityRoutine());
    }

    public void TakeHazardDamage(Vector2 safePos)
    {
        if (isInvulnerable || isDead) return;

        // Si es el último corazón → muerte normal (DieRoutine)
        if (currentHealth - 1 <= 0)
        {
            TakeDamage(1);
            return;
        }

        // Quitar 1 vida (activa i-frames automáticamente via TakeDamage)
        TakeDamage(1);

        // Teletransportar a posición segura
        transform.position = safePos;

        // Resetear velocidad para que no siga cayendo al reaparecer
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        Debug.Log($"[TakeHazardDamage] Romerito respawneó en {safePos}. " +
                  $"Vida restante: {currentHealth}");
    }

    // ── Corrutina principal de muerte ─────────────────────────

    IEnumerator DieRoutine()
    {
        isDead = true;
        Debug.Log("💀 Romerito ha muerto.");

        // [FIX-2] Restaurar colisiones si muere con I-frames activos.
        SetIntangibleVsEnemies(false);

        if (movement != null) movement.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        foreach (var col in allColliders)
            col.enabled = false;

        if (spriteRend != null) spriteRend.enabled = false;
        foreach (var sr in allSprites) sr.enabled = false;

        OnPlayerDied?.Invoke();

        if (resetRoutineFactory != null)
        {
            Debug.Log("[DieRoutine] Ejecutando reset de sala antes del respawn...");
            yield return StartCoroutine(resetRoutineFactory());
            Debug.Log("[DieRoutine] Reset completado. Procediendo al respawn.");
        }
        else
        {
            yield return new WaitForSeconds(deathPauseDuration);
        }

        Respawn();
    }

    // ── Respawn ──────────────────────────────────────────────

    void Respawn()
    {
        if (GameManager01.instance == null)
        {
            Debug.LogError("¡No hay GameManager! Recargando escena...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            return;
        }

        // 1. Mover al checkpoint
        transform.position = GameManager01.instance.lastCheckPointPos;

        // 2. Restaurar vida y UI
        currentHealth = maxHealth;
        if (heartSystem != null) heartSystem.UpdateHearts(currentHealth);

        // El Tonalli se vacía al morir — se gana de nuevo en combate.
        if (tonalliSystem != null) tonalliSystem.ResetearAlMorir();

        // 3. Restaurar físicas
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
        }

        // 4. Reactivar colliders
        foreach (var col in allColliders)
            col.enabled = true;

        // 5. Hacer visibles todos los sprites (raíz e hijos)
        // Los sprites con tag "WeaponSprite" los controla RomeritoCombat
        foreach (var sr in allSprites)
        {
            if (sr.CompareTag("WeaponSprite")) continue;
            sr.enabled = true;
        }
        if (spriteRend != null) spriteRend.enabled = true;

        // 6. Reactivar movimiento
        if (movement != null) movement.enabled = true;

        // 7. Quitar el Macahuitl y apagar su sprite hijo
        if (combatScript != null)
        {
            combatScript.tieneMacuahuitl = false;
            combatScript.currentFavor = RomeritoCombat.GodFavor.Neutro;
            if (combatScript.macahuitlSprite != null)
                combatScript.macahuitlSprite.enabled = false;
        }

        // 8. Resetear animación
        if (anim != null) anim.Rebind();

        // 9. I-Frames de reaparición
        StartCoroutine(InvunerabilityRoutine());

        isDead = false;
        Debug.Log("[Respawn] Romerito reaparecido en: " + transform.position);
    }

    // ── I-Frames ─────────────────────────────────────────────

    // ★ Llamado por RomeritoCombat.EjecutarPogo() en cada rebote exitoso.
    // I-frames cortos (0.2s) sin parpadeo visual. El pogo NO activa
    // intangibilidad — la colisión física es necesaria para encadenar rebotes.
    public void ActivarIFramesPogo()
    {
        if (isDead) return;
        if (!isInvulnerable)
            StartCoroutine(IFramesPogo());
    }

    IEnumerator IFramesPogo()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(0.2f);
        isInvulnerable = false;
    }

    IEnumerator InvunerabilityRoutine()
    {
        isInvulnerable = true;
        SetIntangibleVsEnemies(true);   // [FIX-2] Activar las dos defensas

        for (int i = 0; i < numberOfFlashes; i++)
        {
            if (spriteRend != null) spriteRend.color = new Color(1, 1, 1, 0.5f);
            yield return new WaitForSeconds(iFramesDuration / (numberOfFlashes * 2));
            if (spriteRend != null) spriteRend.color = Color.white;
            yield return new WaitForSeconds(iFramesDuration / (numberOfFlashes * 2));
        }

        SetIntangibleVsEnemies(false);  // [FIX-2] Restaurar todo
        isInvulnerable = false;
    }

    // ════════════════════════════════════════════════════════
    // [FIX-2] Núcleo de la intangibilidad — doble mecanismo
    // ════════════════════════════════════════════════════════
    private void SetIntangibleVsEnemies(bool intangible)
    {
        // ── Defensa A: Rigidbody2D.excludeLayers ─────────────
        // Cubre TODOS los colliders adjuntos al rigidbody (raíz + hijos).
        // Solo surte efecto si los enemigos tienen bien asignada su layer.
        if (rb != null && enemyLayerMask != 0)
        {
            if (intangible) rb.excludeLayers |= enemyLayerMask;
            else rb.excludeLayers &= ~enemyLayerMask;
        }

        // ── Defensa B: IgnoreCollision por pareja ────────────
        // Independiente de layers: identifica a los enemigos por componente.
        // Garantiza la intangibilidad aunque las layers estén mal configuradas.
        if (intangible)
        {
            ignoredPairs.Clear();
            foreach (var enemyCol in ObtenerCollidersEnemigos())
            {
                if (enemyCol == null || !enemyCol.enabled) continue;
                foreach (var myCol in allCollidersDeep)
                {
                    if (myCol == null) continue;
                    Physics2D.IgnoreCollision(myCol, enemyCol, true);
                    ignoredPairs.Add((myCol, enemyCol));
                }
            }
        }
        else
        {
            foreach (var (mine, theirs) in ignoredPairs)
            {
                // Los enemigos pueden haber muerto/destruido durante los I-frames.
                if (mine == null || theirs == null) continue;
                Physics2D.IgnoreCollision(mine, theirs, false);
            }
            ignoredPairs.Clear();
        }
    }
}