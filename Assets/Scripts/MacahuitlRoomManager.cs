using UnityEngine;
using System.Collections;

public class MacahuitlRoomManager : MonoBehaviour
{
    [Header("Referencias")]
    public WaveSpawner waveSpawner;
    public MacahuitlPickup macahuitlPickup;

    [Tooltip("Transform en el umbral de la sala — aquí reaparecerá Romerito")]
    public Transform spawnTransform;

    [Header("Puertas")]
    [Tooltip("Puerta de ENTRADA — abierta al llegar, se cierra al tomar el Macahuitl")]
    public RoomDoor doorEntrada;
    [Tooltip("Puerta de SALIDA — bloqueada hasta terminar las oleadas, luego destructible")]
    public RoomDoor doorSalida;

    [Header("Configuración")]
    public string enemyTag = "Enemy";

    [Tooltip("Pausa en segundos entre que la sala se limpia y que Romerito reaparece.")]
    public float postResetPause = 0.5f;

    [Header("Límites de la Sala")]
    [Tooltip("Collider2D que define el área de la sala. " +
             "Solo los enemigos DENTRO de este área se destruyen en el reset. " +
             "Usa un PolygonCollider2D o BoxCollider2D en modo Trigger que cubra " +
             "toda la sala y asígnalo aquí.")]
    public Collider2D roomBounds;

    // ── Estado interno ──────────────────────────────────────
    private bool playerIsInRoom = false;
    private bool combatIsActive = false;
    private bool isResetting = false;
    private RomeritoHealth playerHealth;

    public bool CombatIsActive => combatIsActive;

    // ── Unity ───────────────────────────────────────────────

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<RomeritoHealth>();
            if (playerHealth != null)
            {
                playerHealth.SetResetRoutine(ResetRoomRoutine);
                playerHealth.OnPlayerDied += HandlePlayerDied;
            }
        }

        if (waveSpawner != null)
            waveSpawner.OnAllWavesFinished += HandleCombatFinished;
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.ClearResetRoutine();
            playerHealth.OnPlayerDied -= HandlePlayerDied;
        }

        if (waveSpawner != null)
            waveSpawner.OnAllWavesFinished -= HandleCombatFinished;
    }

    // ── Trigger de entrada ───────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (playerIsInRoom) return;

        playerIsInRoom = true;

        if (spawnTransform != null && GameManager01.instance != null)
        {
            GameManager01.instance.UpdateCheckPoint(spawnTransform.position);
            Debug.Log("[MacahuitlRoom] Checkpoint fijado en el umbral.");
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        // ══════════════════════════════════════════════════════════════
        //  FIX — BUG: Falso positivo de Unity Physics
        // ──────────────────────────────────────────────────────────────
        //  Cuando DieRoutine() llama `col.enabled = false`, Unity trata
        //  la desactivación del collider como una salida física del trigger
        //  y dispara OnTriggerExit2D automáticamente en el siguiente
        //  FixedUpdate. Esto ocurre MIENTRAS ResetRoomRoutine() ya arrancó
        //  (el StartCoroutine ya fue lanzado) pero aún no pasó su check
        //  inicial. Resultado: playerIsInRoom y combatIsActive se ponían
        //  en false, el check fallaba, la corrutina hacía early-exit y
        //  NADA se reseteaba: puertas cerradas, oleadas vivas, macahuitl
        //  perdido.
        //
        //  SOLUCIÓN: si hay combate activo o hay un reset en curso, este
        //  exit NO puede ser legítimo (las puertas están cerradas).
        //  Lo ignoramos. ResetRoomRoutine() gestiona su propia limpieza.
        // ══════════════════════════════════════════════════════════════
        if (combatIsActive || isResetting) return;

        // Salida legítima post-combate (Romerito cruzó la salida destruida).
        // Limpiar para que muertes fuera de la sala sean respawns normales.
        if (playerHealth != null) playerHealth.ClearResetRoutine();
        playerIsInRoom = false;

        // Nota: NO tocamos combatIsActive aquí.
        // Lo gestiona exclusivamente HandleCombatFinished() y ResetRoomRoutine().
    }

    // ── API Pública ──────────────────────────────────────────

    public void NotifyCombatStarted()
    {
        combatIsActive = true;
        Debug.Log("[MacahuitlRoom] Combate iniciado.");

        // Cerrar la entrada — Romerito ya no puede salir por donde entró
        if (doorEntrada != null) doorEntrada.Cerrar();

        // FIX — BUG C: Bloquear la salida explícitamente en cada inicio
        // de combate. No basta con confiar en RoomDoor.Start() porque
        // después de un reset la salida puede haber quedado en estado
        // "destructible". Aquí garantizamos que siempre empieza bloqueada.
        if (doorSalida != null) doorSalida.Bloquear();
    }

    // ── Callbacks ────────────────────────────────────────────

    void HandlePlayerDied()
    {
        if (playerIsInRoom && combatIsActive)
            Debug.Log("[MacahuitlRoom] Romerito cayó. Iniciando secuencia de reset.");
    }

    void HandleCombatFinished()
    {
        combatIsActive = false;
        Debug.Log("[MacahuitlRoom] ¡Oleadas completadas!");

        // Reabrir la entrada — Romerito puede volver atrás si quiere
        if (doorEntrada != null) doorEntrada.Abrir();

        // La salida se vuelve destructible — hay que romperla con el Macahuitl
        if (doorSalida != null) doorSalida.HacerDestructible();
    }

    // ── Corrutina de Reset (inyectada en DieRoutine) ─────────
    //
    //  DieRoutine en RomeritoHealth hace:
    //    yield return StartCoroutine(ResetRoomRoutine())
    //
    //  Orden garantizado:
    //    1. Romerito muerto, invisible e intangible  (DieRoutine)
    //    2. Enemigos destruidos                      (esta corrutina)
    //    3. WaveSpawner reseteado                    (esta corrutina)
    //    4. Macahuitl restaurado en el altar         (esta corrutina)
    //    5. Puertas reconfiguradas                   (esta corrutina)
    //    6. Pausa postResetPause                     (esta corrutina)
    //    7. Romerito respawnea                       (DieRoutine)

    IEnumerator ResetRoomRoutine()
    {
        // Guard: evitar doble reset
        if (isResetting)
        {
            yield return new WaitUntil(() => !isResetting);
            yield break;
        }

        // Solo ejecutar si el jugador estaba en combate dentro de la sala
        if (!playerIsInRoom || !combatIsActive)
        {
            yield return new WaitForSeconds(postResetPause);
            yield break;
        }

        isResetting = true;

        // 1. Destruir solo los enemigos dentro de los límites de la sala
        int destroyed = 0;
        if (roomBounds != null)
        {
            GameObject[] allEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
            foreach (GameObject enemy in allEnemies)
            {
                if (roomBounds.OverlapPoint(enemy.transform.position))
                {
                    Destroy(enemy);
                    destroyed++;
                }
            }
        }
        else
        {
            Debug.LogWarning("[MacahuitlRoom] roomBounds no asignado — " +
                             "se destruirán TODOS los enemigos de la escena.");
            GameObject[] allEnemies = GameObject.FindGameObjectsWithTag(enemyTag);
            foreach (GameObject enemy in allEnemies)
                Destroy(enemy);
            destroyed = allEnemies.Length;
        }
        Debug.Log($"[MacahuitlRoom] {destroyed} enemigo(s) destruido(s) dentro de la sala.");

        // 2. Resetear oleadas
        if (waveSpawner != null)
            waveSpawner.ResetWaves();

        // 3. Restaurar el Macahuitl en el altar
        if (macahuitlPickup != null)
            macahuitlPickup.RestoreToAltar();

        // 4. Confirmar checkpoint en el umbral
        if (spawnTransform != null && GameManager01.instance != null)
            GameManager01.instance.UpdateCheckPoint(spawnTransform.position);

        combatIsActive = false;

        // 5. Reabrir la entrada para que Romerito pueda reentrar tras el respawn
        if (doorEntrada != null) doorEntrada.Abrir();

        // FIX — BUG B: Re-bloquear la salida.
        // Si las oleadas se completaron antes de que Romerito muriera,
        // la salida estaba en modo "destructible". En el siguiente intento
        // debe empezar bloqueada de nuevo, no destructible.
        if (doorSalida != null) doorSalida.Bloquear();

        // 6. Pausa para que el jugador vea la sala vacía antes del respawn
        yield return new WaitForSeconds(postResetPause);

        isResetting = false;
        Debug.Log("[MacahuitlRoom] Reset completo. DieRoutine puede continuar al respawn.");
    }
}