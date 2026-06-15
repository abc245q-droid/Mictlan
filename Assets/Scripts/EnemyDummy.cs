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

    [Header("Tonalli (Recompensa para Romerito)")]
    [Tooltip("Tonalli que gana Romerito al golpear a este enemigo sin matarlo.")]
    public float tonalliPorGolpe = 15f;
    [Tooltip("Tonalli que gana Romerito al matar a este enemigo.")]
    public float tonalliPorMatar = 50f;

    [Header("Feedback")]
    public GameObject deathEffect;
    private SpriteRenderer sr;

    // ★ Evento para el WaveSpawner (descontar enemigos vivos)
    public event System.Action OnDeath;

    // ★ NUEVO: Evento para la IA (retroceso al recibir daño)
    //   MictecahBase hace:  dummy.OnHurt += RecibirGolpe;
    public event System.Action OnHurt;

    void Start()
    {
        currentHealth = maxHealth;
        sr = GetComponent<SpriteRenderer>();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        StartCoroutine(FlashWhite());

        // ★ Avisar a la IA ANTES de comprobar la muerte
        //   (si murió, ya no importa el retroceso porque se destruye)
        if (currentHealth > 0)
        {
            OnHurt?.Invoke();
            // Romerito gana Tonalli al golpear sin matar
            TonalliSystem.Instance?.GanarTonalli(tonalliPorGolpe);
        }

        if (currentHealth <= 0)
            Die();
    }

    void Die()
    {
        // Romerito gana Tonalli mayor al matar al enemigo
        TonalliSystem.Instance?.GanarTonalli(tonalliPorMatar);

        // Notificar al WaveSpawner ANTES de destruir el objeto
        OnDeath?.Invoke();

        // --- Loot (cacao) ---
        if (lootDrop != null)
        {
            float randomValue = Random.Range(0f, 100f);
            if (randomValue <= dropChance)
            {
                int amountToDrop = Random.Range(minDrops, maxDrops + 1);
                for (int i = 0; i < amountToDrop; i++)
                {
                    Vector2 randomOffset = Random.insideUnitCircle * lootScatter;
                    Vector3 spawnPos = transform.position + (Vector3)randomOffset;
                    Instantiate(lootDrop, spawnPos, Quaternion.identity);
                }
            }
        }

        // Efecto de muerte
        if (deathEffect != null)
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
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