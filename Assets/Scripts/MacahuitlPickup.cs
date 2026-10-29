using UnityEngine;
using System.Collections;

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
    [Tooltip("Pausa (s) entre cerrar el tutorial y el sello de la sala, " +
             "para que el golpe de la puerta se lea como consecuencia.")]
    public float pausaTrasTutorial = 0.25f;

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

        // 3. Mensaje tutorial modal PRIMERO (pausa el juego, botón Aceptar).
        //    Si ya se vio (reintento tras morir), Mostrar no abre nada.
        if (mensajeMacahuitl != null && TutorialManager.Instance != null)
            TutorialManager.Instance.Mostrar(mensajeMacahuitl);

        // 4. [SECUENCIA] Al cerrarse el tutorial: sello de la sala (temblor),
        //    corazones uno a uno (teyolía) y oleadas. Antes todo esto ocurría
        //    detrás del modal y pasaba desapercibido.
        //    Corre en el RoomManager: este objeto puede destruirse tras recogerse.
        MonoBehaviour anfitrion = roomManager != null ? (MonoBehaviour)roomManager : this;
        anfitrion.StartCoroutine(SecuenciaInicioCombate(roomManager, waveSpawner, pausaTrasTutorial));
    }

    static IEnumerator SecuenciaInicioCombate(MacahuitlRoomManager rm, WaveSpawner ws, float pausa)
    {
        // Esperar a que el jugador cierre el tutorial (y a que el mundo se reanude).
        while ((TutorialManager.Instance != null && TutorialManager.Instance.ModalAbierto) ||
               PausaMundo.Activa)
            yield return null;

        if (pausa > 0f) yield return new WaitForSeconds(pausa);

        // Notificar al RoomManager que empieza el combate (sello + teyolía)
        if (rm != null)
            rm.NotifyCombatStarted();

        // Iniciar oleadas
        if (ws != null)
            ws.StartWaves();
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

        if (itemHabilidad != null)
            itemHabilidad.Restaurar();
        else
            Debug.LogWarning("[MacahuitlPickup] No hay ItemHabilidad asignado.");

        // Si el jugador aún está en rango durante el reset, re-registrar
        if (jugadorEnRango && InteractionManager.Instance != null)
            InteractionManager.Instance.Registrar(this);
    }
}