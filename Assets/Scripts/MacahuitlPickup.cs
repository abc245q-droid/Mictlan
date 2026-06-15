using UnityEngine;

// ============================================================
//  MacahuitlPickup — v3
// ============================================================
//  CAMBIO: RestoreToAltar() ahora delega en ItemHabilidad.Restaurar()
//  en lugar de manipular sprite/collider directamente. Esto resuelve
//  el problema de que Destroy() eliminaba el objeto antes del reset.
// ============================================================

public class MacahuitlPickup : MonoBehaviour
{
    [Header("Referencias")]
    public WaveSpawner waveSpawner;
    public RomeritoCombat combatScript;
    public MacahuitlRoomManager roomManager;

    [Tooltip("El ItemHabilidad del Macahuitl en el altar")]
    public ItemHabilidad itemHabilidad;

    [Header("Opciones")]
    public AudioClip pickupSound;
    private AudioSource audioSource;

    private bool hasBeenPickedUp = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        // Auto-buscar ItemHabilidad si está en el mismo GameObject
        if (itemHabilidad == null)
            itemHabilidad = GetComponent<ItemHabilidad>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenPickedUp || !other.CompareTag("Player")) return;

        hasBeenPickedUp = true;

        // 1. Equipar el arma (ItemHabilidad ya lo hace internamente,
        //    pero si quieres llamarlo desde aquí también está bien)
        if (combatScript != null)
            combatScript.EquiparMacuahuitl();

        // 2. Sonido
        if (pickupSound != null && audioSource != null)
            audioSource.PlayOneShot(pickupSound);

        // 3. Notificar al RoomManager que empieza el combate
        if (roomManager != null)
            roomManager.NotifyCombatStarted();

        // 4. Iniciar oleadas
        if (waveSpawner != null)
            waveSpawner.StartWaves();
        else
            Debug.LogWarning("[MacahuitlPickup] No hay WaveSpawner asignado.");
    }

    /// <summary>
    /// Llamado por MacahuitlRoomManager durante el reset.
    /// Delega la restauración visual en ItemHabilidad.Restaurar().
    /// </summary>
    public void RestoreToAltar()
    {
        hasBeenPickedUp = false;

        // ★ CORRECCIÓN: delegamos en ItemHabilidad en lugar de tocar
        //   sprite/collider directamente. ItemHabilidad sabe su propio estado.
        if (itemHabilidad != null)
            itemHabilidad.Restaurar();
        else
            Debug.LogWarning("[MacahuitlPickup] No hay ItemHabilidad asignado. " +
                             "El Macahuitl no reaparecerá visualmente.");
    }
}