using UnityEngine;

// ============================================================
//  EnemyDummy — Vida + Loot (cacao) + eventos OnHurt / OnDeath
// ============================================================
//
//  CAMBIO respecto a tu versión anterior:
//  Se añade el evento público "OnHurt" (★), hermano de "OnDeath".
//  La IA de los Mictecah (MictecahBase) se suscribe a OnHurt para
//  entrar en su estado de "Herido" y retroceder brevemente.
//
//  TODO lo demás es idéntico:
//    • Vida y daño (TakeDamage)
//    • Drop de semillas de cacao (lootDrop)  ← SISTEMA INTACTO
//    • Flash de daño
//    • OnDeath para el WaveSpawner
//
// ============================================================

public class EnemyDummy : MonoBehaviour
{
    [Header("Stats")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Loot (Recompensas)")]
    public GameObject lootDrop;                       // ← Prefab de la semilla de cacao
    [Range(0, 100)] public float dropChance = 100f;

    [Header("Cantidad de Loot")]
    public int minDrops = 1;
    public int maxDrops = 3;
    public float lootScatter = 0.5f;

    [Header("Validación de Spawn (evita que el loot atraviese paredes)")]
    [Tooltip("SOLO paredes/suelo (capa 'Suelo'). El cacao nunca se dispersará más allá de estos colliders.")]
    public LayerMask obstacleLayer;
    [Tooltip("Margen de seguridad para no spawnear pegado a la pared (en unidades).")]
    public float wallSafetyMargin = 0.08f;

    [Header("Tonalli (Recompensa para Romerito)")]
    [Tooltip("Tonalli que gana Romerito al golpear a este enemigo sin matarlo.")]
    public float tonalliPorGolpe = 15f;
    [Tooltip("Tonalli que gana Romerito al matar a este enemigo.")]
    public float tonalliPorMatar = 50f;

    [Header("Efecto Visual de Tonalli (orbes que vuelan hacia Romerito)")]
    [Tooltip("Prefab con el script TonalliOrb. Se instancia SOLO si Romerito ya tiene el Don de Tlacua.")]
    public GameObject tonalliOrbPrefab;
    [Tooltip("Cantidad de orbes al golpear sin matar.")]
    public int orbesPorGolpe = 2;
    [Tooltip("Cantidad de orbes al matar.")]
    public int orbesPorMatar = 5;

    [Header("Feedback")]
    public GameObject deathEffect;
    private SpriteRenderer sr;

    // ★ NUEVO [NEXHUALLI]: Invulnerabilidad temporal.
    //   La Tlahuelpuchi (y futuros enemigos con fases) la activan durante
    //   su forma inmune. TakeDamage se ignora por completo mientras esté
    //   activa: sin daño, sin OnHurt, sin Tonalli. El Pogo de Romerito
    //   sigue rebotando (eso lo decide RomeritoCombat, no este script).
    private bool esInvulnerable = false;
    public bool EsInvulnerable => esInvulnerable;
    public void SetInvulnerable(bool valor) { esInvulnerable = valor; }

    [Header("Categoría del Enemigo")]
    [Tooltip("Marca true si es jefe o mini-jefe. Los jefes se destruyen " +
             "permanentemente y se registran en PlayerData.jefesDerrotados " +
             "para persistir entre sesiones. Los enemigos con esJefe=false " +
             "que además tengan un componente EnemigoRespawnable se reagrupan " +
             "cuando Romerito reaparece en un Cihuacalli.")]
    public bool esJefe = false;

    [Tooltip("ID único del jefe (ej: 'jefe_mascara_turquesa_n4'). " +
             "Solo relevante si esJefe = true.")]
    public string jefeID = "";

    // Flag de runtime — lo marca WaveSpawner al instanciar. NO va en el Inspector.
    [System.NonSerialized] public bool esDeOleada = false;

    // ★ Evento para el WaveSpawner (descontar enemigos vivos)
    public event System.Action OnDeath;

    // ★ NUEVO: Evento para la IA (retroceso al recibir daño)
    //   MictecahBase hace:  dummy.OnHurt += RecibirGolpe;
    public event System.Action OnHurt;

    void Start()
    {
        // Si es jefe y ya fue derrotado en una sesión anterior, no aparecer.
        if (esJefe && !string.IsNullOrEmpty(jefeID) &&
            GameManager01.instance != null &&
            GameManager01.instance.currentData != null &&
            GameManager01.instance.currentData.jefesDerrotados != null &&
            GameManager01.instance.currentData.jefesDerrotados.Contains(jefeID))
        {
            Destroy(gameObject);
            return;
        }

        currentHealth = maxHealth;
        sr = GetComponent<SpriteRenderer>();
    }

    public void TakeDamage(int damage)
    {
        // ★ [NEXHUALLI] Forma inmune: el golpe no hace NADA (ni flash, ni
        //   knockback, ni Tonalli). El feedback de "golpe inútil" queda a
        //   cargo del enemigo (VFX propios) si se desea.
        if (esInvulnerable) return;

        currentHealth -= damage;
        StartCoroutine(FlashWhite());

        if (currentHealth > 0)
        {
            OnHurt?.Invoke();

            // Solo transferir Tonalli si Romerito tiene el don de Tlacua
            if (TieneDonDeTlacua())
            {
                TonalliSystem.Instance?.GanarTonalli(tonalliPorGolpe);
                SpawnTonalliOrbs(orbesPorGolpe);
            }
        }

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        // Solo transferir Tonalli si Romerito tiene el don de Tlacua
        if (TieneDonDeTlacua())
        {
            TonalliSystem.Instance?.GanarTonalli(tonalliPorMatar);
            SpawnTonalliOrbs(orbesPorMatar);
        }

        OnDeath?.Invoke();

        if (lootDrop != null)
        {
            float randomValue = Random.Range(0f, 100f);
            if (randomValue <= dropChance)
            {
                int amountToDrop = Random.Range(minDrops, maxDrops + 1);
                for (int i = 0; i < amountToDrop; i++)
                {
                    Vector3 spawnPos = ResolveLootSpawnPoint();
                    Instantiate(lootDrop, spawnPos, Quaternion.identity);
                }
            }
        }

        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        // ── Ruta de destrucción según categoría ──────────────

        // A) JEFE — destrucción permanente + persistencia en el save
        if (esJefe)
        {
            if (!string.IsNullOrEmpty(jefeID) && GameManager01.instance != null)
            {
                var data = GameManager01.instance.currentData;
                if (data.jefesDerrotados == null)
                    data.jefesDerrotados = new System.Collections.Generic.List<string>();
                if (!data.jefesDerrotados.Contains(jefeID))
                    data.jefesDerrotados.Add(jefeID);
                GameManager01.instance.SaveGame();
            }
            Destroy(gameObject);
            return;
        }

        // B) ENEMIGO DE OLEADA — destrucción normal (WaveSpawner reinstancia)
        if (esDeOleada)
        {
            Destroy(gameObject);
            return;
        }

        // C) ENEMIGO DE MUNDO — si tiene EnemigoRespawnable, se dispersa
        var respawnable = GetComponent<EnemigoRespawnable>();
        if (respawnable != null)
        {
            respawnable.Dispersar();
            return;
        }

        // D) FALLBACK — enemigo sin categoría explícita, destruir
        Destroy(gameObject);
    }

    // ── API pública para EnemigoRespawnable ──────────────────

    /// <summary>
    /// Restaura la vida al máximo. Usado por EnemigoRespawnable
    /// al reagrupar el enemigo — Start() no corre en reactivación
    /// de GameObject, así que restauramos manualmente.
    /// </summary>
    public void ResetearVida()
    {
        currentHealth = maxHealth;
    }

    // ★ NUEVO: Calcula un punto de dispersión seguro para el loot.
    //   Lanza un rayo desde el enemigo hacia el offset aleatorio; si hay
    //   una pared en el camino (capa "obstacleLayer"), detiene el punto
    //   justo antes del impacto en vez de dejar que el loot caiga del
    //   otro lado. Así el cacao nunca queda inaccesible tras un muro.
    private Vector3 ResolveLootSpawnPoint()
    {
        Vector2 origen = transform.position;
        Vector2 offset = Random.insideUnitCircle * lootScatter;

        float distancia = offset.magnitude;
        if (distancia < 0.001f)
            return transform.position; // Sin desplazamiento: la posición del enemigo ya es segura

        Vector2 direccion = offset / distancia; // normalizado

        RaycastHit2D hit = Physics2D.Raycast(origen, direccion, distancia, obstacleLayer);

        if (hit.collider != null)
        {
            // Hay pared en el camino: nos quedamos justo antes del impacto
            Vector2 puntoSeguro = hit.point - direccion * wallSafetyMargin;
            return new Vector3(puntoSeguro.x, puntoSeguro.y, transform.position.z);
        }

        Vector2 puntoFinal = origen + offset;
        return new Vector3(puntoFinal.x, puntoFinal.y, transform.position.z);
    }

    // ★ NUEVO: Instancia los orbes visuales de Tonalli en la posición del
    //   enemigo. Cada orbe (TonalliOrb.cs) se encarga de su propio impulso
    //   y persecución hacia Romerito — aquí solo los disparamos.
    private void SpawnTonalliOrbs(int cantidad)
    {
        if (tonalliOrbPrefab == null) return;

        for (int i = 0; i < cantidad; i++)
        {
            Instantiate(tonalliOrbPrefab, transform.position, Quaternion.identity);
        }
    }

    private bool TieneDonDeTlacua()
    {
        return GameManager01.instance != null &&
               GameManager01.instance.currentData.tieneDonDeTlacua;
    }

    System.Collections.IEnumerator FlashWhite()
    {
        if (sr != null)
        {
            sr.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sr.color = Color.white;
        }
    }
}