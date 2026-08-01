using UnityEngine;

// ============================================================
//  EnemigoRespawnable — Marca un enemigo como reagrupable
//  REGISTRO_V2 — fix del bug de OnDisable→Desregistrar
// ============================================================
//
//  Añadir este script a TODOS los enemigos "normales" del mundo:
//    • Patrullas de Mictecah (Sprinter, LanzaCráneos, Nexhualli)
//    • Silbadores (Chichtlacatl)
//    • Brujas (Tlahuelpuchi)
//    • Cuāuhtli comunes, Ahuaque Labriego, etc.
//
//  NO añadirlo a jefes/mini-jefes ni a enemigos de oleada (esos se
//  manejan vía EnemyDummy.esJefe / esDeOleada respectivamente).
//
//  DIÉGESIS:
//  Es el yohual-ehécatl — el viento nocturno que reagrupa el ihíyotl
//  disperso de los muertos comunes cuando el tonalli de Romerito se
//  reanuda en el fogón.
//
//  CAMBIOS v2:
//  • Registro en OnEnable (ahora seguro gracias al lazy singleton
//    de MictlanEnemyRegistry).
//  • Desregistro SOLO en OnDestroy, no en OnDisable. Un enemigo
//    disperso está desactivado pero sigue siendo parte del nivel:
//    tiene que permanecer en el registro para que RespawnearTodos()
//    lo pueda encontrar y reactivar.
// ============================================================

[RequireComponent(typeof(EnemyDummy))]
public class EnemigoRespawnable : MonoBehaviour
{
    // ── Estado inicial (capturado en Awake) ──────────────────
    private Vector3 posicionInicial;
    private Quaternion rotacionInicial;
    private Vector3 escalaInicial;
    private int maxHealthInicial;

    private EnemyDummy dummy;
    private Rigidbody2D rb;
    private MictecahBase mictecahAI;
    private FlyingEnemyAI flyingAI;
    private EnemyAI enemyAI;

    private bool estaDisperso = false;

    public bool EstaDisperso => estaDisperso;

    void Awake()
    {
        // Capturar el estado inicial ANTES de que nada lo modifique.
        posicionInicial = transform.position;
        rotacionInicial = transform.rotation;
        escalaInicial   = transform.localScale;

        dummy      = GetComponent<EnemyDummy>();
        rb         = GetComponent<Rigidbody2D>();
        mictecahAI = GetComponent<MictecahBase>();
        flyingAI   = GetComponent<FlyingEnemyAI>();
        enemyAI    = GetComponent<EnemyAI>();

        if (dummy != null)
            maxHealthInicial = dummy.maxHealth;
    }

    void OnEnable()
    {
        // Auto-registro. Ahora seguro: MictlanEnemyRegistry.Instance
        // es un lazy singleton que se auto-crea si no existe.
        // Registrar es idempotente (HashSet).
        MictlanEnemyRegistry.Instance?.Registrar(this);
    }

    // NOTA: NO hay OnDisable. Un enemigo disperso está desactivado pero
    // sigue perteneciendo al nivel — debe permanecer en el registro.

    void OnDestroy()
    {
        // Solo cuando el GameObject se destruye realmente (cambio de
        // escena, o destruido explícitamente por otro sistema).
        // Los enemigos con EnemigoRespawnable NO se destruyen al morir:
        // Dispersar() solo los desactiva.
        if (MictlanEnemyRegistry.Instance != null)
            MictlanEnemyRegistry.Instance.Desregistrar(this);
    }

    /// <summary>
    /// Llamado por EnemyDummy.Die() en vez de Destroy(). Desactiva
    /// el GameObject y marca al enemigo como "disperso".
    /// </summary>
    public void Dispersar()
    {
        estaDisperso = true;
        Debug.Log($"[EnemigoRespawnable] {name} se dispersa (yohual-ehécatl).");
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Llamado por MictlanEnemyRegistry cuando Romerito reaparece
    /// en un Cihuacalli. Reagrupa al enemigo en su posición inicial
    /// con salud completa y máquina de estados limpia.
    /// </summary>
    public void Respawnear()
    {
        if (!estaDisperso) return;   // Estaba vivo, no tocar

        // 1. Restaurar transform
        transform.position   = posicionInicial;
        transform.rotation   = rotacionInicial;
        transform.localScale = escalaInicial;

        // 2. Restaurar físicas
        if (rb != null)
        {
            rb.linearVelocity  = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // 3. Reactivar (OnEnable dispara → Registrar idempotente).
        gameObject.SetActive(true);

        // 4. Restaurar vida vía API pública (Start no corre en
        //    reactivación de GameObject).
        if (dummy != null)
        {
            dummy.maxHealth = maxHealthInicial;
            dummy.ResetearVida();
        }

        // 5. Reiniciar IA (evita estados residuales como "Persiguiendo"
        //    a un Romerito que ya no está donde estaba).
        if (mictecahAI != null) mictecahAI.enabled = true;
        if (flyingAI   != null) flyingAI.enabled   = true;
        if (enemyAI    != null) enemyAI.enabled    = true;

        estaDisperso = false;
    }
}
