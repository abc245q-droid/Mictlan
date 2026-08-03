using UnityEngine;

// ============================================================
//  MacahuitlPickup — v5 (delega Recoger en ItemHabilidad)
// ============================================================
//  CAMBIOS v5 respecto a v4:
//  - Eliminado el campo 'combatScript': ya no se llama
//    EquiparMacuahuitl() directamente. Ahora se delega todo el
//    pickup a itemHabilidad.Recoger(jugador), que centraliza la
//    lógica y evita el doble equipar.
//  - Guarda referencia al Player en OnTriggerEnter2D para poder
//    pasársela a Recoger() cuando el jugador presione B.
//
//  ⚠️ SETUP EN INSPECTOR — CRÍTICO:
//  En el ItemHabilidad del altar del Macahuitl, marca:
//    • pickupAutomatico = FALSE
//    • esReseteable = TRUE
//  Sin pickupAutomatico=FALSE, ItemHabilidad se recogerá por
//  contacto y anulará el prompt "Presiona B".
// ============================================================

public class MacahuitlPickup : MonoBehaviour, IInteractuable
{
    [Header("Referencias")]
    public WaveSpawner waveSpawner;
    public MacahuitlRoomManager roomManager;

    [Tooltip("El ItemHabilidad del Macahuitl en el altar. " +
             "Debe tener pickupAutomatico=FALSE en su Inspector.")]
    public ItemHabilidad itemHabilidad;

    [Header("Tutorial")]
    [Tooltip("Mensaje modal que se muestra al obtener el Macahuitl.")]
    public MensajeTutorial mensajeMacahuitl;

    [Header("Prompt")]
    public string textoPrompt = "Presiona B";

    [Header("Opciones")]
    public AudioClip pickupSound;
    private AudioSource audioSource;

    private bool hasBeenPickedUp = false;
    private bool jugadorEnRango = false;
    private GameObject jugadorRef;

    // ---------------- IInteractuable ----------------
    public string TextoPrompt => textoPrompt;
    public Vector3 PosicionMundo => transform.position;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (itemHabilidad == null)
            itemHabilidad = GetComponent<ItemHabilidad>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (hasBeenPickedUp || !other.CompareTag("Player")) return;
        jugadorEnRango = true;
        jugadorRef = other.gameObject;
        if (InteractionManager.Instance != null)
            InteractionManager.Instance.Registrar(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        jugadorEnRango = false;
        // No limpiamos jugadorRef: si vuelve a entrar, OnTriggerEnter2D
        // lo sobrescribe. Si no vuelve, no importa.
        if (InteractionManager.Instance != null)
            InteractionManager.Instance.Desregistrar(this);
    }

    // ---------------- Ejecución del pickup ----------------
    public void Interactuar()
    {
        if (hasBeenPickedUp || jugadorRef == null) return;
        hasBeenPickedUp = true;

        // 1. Delegar pickup en ItemHabilidad (equipa arma, efectos, oculta sprite)
        if (itemHabilidad != null)
            itemHabilidad.Recoger(jugadorRef);
        else
            Debug.LogWarning("[MacahuitlPickup] No hay ItemHabilidad asignado — no se equipó arma.");

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

        // 5. Mensaje tutorial modal (pausa el juego, botón Aceptar)
        if (mensajeMacahuitl != null && TutorialManager.Instance != null)
            TutorialManager.Instance.Mostrar(mensajeMacahuitl);
    }

    /// <summary>
    /// Llamado por MacahuitlRoomManager durante el reset.
    /// Delega la restauración visual en ItemHabilidad.Restaurar().
    /// </summary>
    public void RestoreToAltar()
    {
        hasBeenPickedUp = false;

        if (itemHabilidad != null)
            itemHabilidad.Restaurar();
        else
            Debug.LogWarning("[MacahuitlPickup] No hay ItemHabilidad asignado.");

        // Si el jugador aún está en rango durante el reset, re-registrar
        if (jugadorEnRango && InteractionManager.Instance != null)
            InteractionManager.Instance.Registrar(this);
    }
}