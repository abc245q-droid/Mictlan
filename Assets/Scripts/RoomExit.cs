using UnityEngine;

// ============================================================
//  RoomExit — Puerta de salida bloqueada durante el combate
// ============================================================
//
//  Coloca este script en el GameObject de la puerta de salida
//  de la sala del Macahuitl EN LUGAR de SceneExit.
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. En el GameObject de la puerta de salida:
//       • Añade este script
//       • Añade un Collider2D en modo Trigger (la zona de salida)
//       • Asigna roomManager → el MacahuitlRoomManager de la sala
//
//  2. Visual de bloqueo (opcional pero recomendado):
//       • Crea un GameObject hijo "Barrera" con un sprite semi-
//         transparente (ej: cadenas, huesos cruzados, un glifo
//         de Mictlantecuhtli) y asígnalo a blockerVisual.
//       • La barrera aparece cuando el combate está activo
//         y desaparece cuando las oleadas se limpian.
//
//  3. Puerta que avanza de nivel: asigna sceneToLoad y targetDoorID
//     igual que harías con el SceneExit original.
//  ─────────────────────────────────────────────────────────────

public class RoomExit : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Nombre de la escena a cargar al salir (igual que SceneExit)")]
    public string sceneToLoad;

    [Tooltip("ID de la puerta de destino en la escena siguiente")]
    public string targetDoorID;

    [Header("Referencias")]
    [Tooltip("El MacahuitlRoomManager de esta sala")]
    public MacahuitlRoomManager roomManager;

    [Header("Visual de Bloqueo (Opcional)")]
    [Tooltip("GameObject con el sprite que indica que la salida está bloqueada. " +
             "Se activa durante el combate y se desactiva al terminar.")]
    public GameObject blockerVisual;

    [Tooltip("Sonido al intentar salir con el combate activo")]
    public AudioClip blockedSound;
    private AudioSource audioSource;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        // Estado inicial: si hay combate activo al entrar la escena,
        // la barrera ya debe estar visible.
        RefreshBlockerVisual();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        // ── Combate activo: bloquear la salida ────────────────
        if (roomManager != null && roomManager.CombatIsActive)
        {
            // Feedback sonoro
            if (blockedSound != null && audioSource != null)
                audioSource.PlayOneShot(blockedSound);

            Debug.Log("[RoomExit] Salida bloqueada — elimina a todos los enemigos primero.");
            return;
        }

        // ── Combate terminado (o no hay manager): salir ───────
        if (GameManager01.instance != null)
        {
            GameManager01.instance.SetNextDoor(targetDoorID);
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("[RoomExit] No hay GameManager. Asegúrate de que existe en la escena.");
        }
    }

    // ── Actualizar visual de barrera ─────────────────────────

    /// <summary>
    /// Llamado por MacahuitlRoomManager cuando el estado del combate cambia.
    /// </summary>
    public void RefreshBlockerVisual()
    {
        if (blockerVisual == null) return;

        bool blocked = (roomManager != null && roomManager.CombatIsActive);
        blockerVisual.SetActive(blocked);
    }
}