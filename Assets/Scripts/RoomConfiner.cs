using UnityEngine;
using System.Collections;
using Unity.Cinemachine; // Asegúrate de usar el namespace correcto según tu versión

public class RoomConfiner : MonoBehaviour
{
    [Header("Configuración")]
    public CinemachineCamera virtualCamera;
    public float transitionDelay = 0.0f; // Tiempo de espera antes de cambiar (ej: 0.5 seg)

    [Tooltip("ID de esta sala para el mapa. Convención: 'L{nivel}_{nombre}'. Ej: 'L0_entrada'.")]
    public string mapRoomId = "";

    [Header("Opcional: Estilo Retro")]
    public bool stopPlayerDuringTransition = false; // ¿Congelar a Romerito?

    private Collider2D myRoomCollider;
    private CinemachineConfiner2D confinerExtension;
    private Coroutine transitionCoroutine;

    void Start()
    {
        myRoomCollider = GetComponent<Collider2D>();

        // Buscamos la extensión automáticamente si tenemos la cámara
        if (virtualCamera != null)
        {
            confinerExtension = virtualCamera.GetComponent<CinemachineConfiner2D>();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !other.isTrigger)
        {
            MapManager.OnRoomEntered(mapRoomId);   // ← AÑADIR: registra el Borrador al entrar

            // Detenemos cualquier transición previa para evitar conflictos
            if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);

            // Iniciamos la nueva rutina de cambio
            transitionCoroutine = StartCoroutine(SwitchRoomRoutine(other.gameObject));
        }
    }

    IEnumerator SwitchRoomRoutine(GameObject player)
    {
        // 1. (Opcional) Congelar al jugador "Estilo Zelda/Metroid NES"
        RomeritoMovement movement = player.GetComponent<RomeritoMovement>();
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        Vector2 savedVelocity = Vector2.zero;

        if (stopPlayerDuringTransition && movement != null && rb != null)
        {
            movement.enabled = false;     // Desactiva inputs
            savedVelocity = rb.linearVelocity;  // Guarda velocidad actual
            rb.linearVelocity = Vector2.zero;   // Frena en seco

            // Opcional: Pausar animaciones
            Animator anim = player.GetComponent<Animator>();
            if (anim) anim.speed = 0;
        }

        // 2. LA PAUSA: Esperamos el tiempo definido
        yield return new WaitForSeconds(transitionDelay);

        // 3. CAMBIO DE JAULA: Aplicamos los nuevos límites
        if (confinerExtension != null && myRoomCollider != null)
        {
            confinerExtension.BoundingShape2D = myRoomCollider;
            confinerExtension.InvalidateBoundingShapeCache();
        }

        // 4. (Opcional) Descongelar al jugador
        if (stopPlayerDuringTransition && movement != null && rb != null)
        {
            movement.enabled = true;
            rb.linearVelocity = savedVelocity; // Restaurar inercia si quieres

            Animator anim = player.GetComponent<Animator>();
            if (anim) anim.speed = 1;
        }
    }
}