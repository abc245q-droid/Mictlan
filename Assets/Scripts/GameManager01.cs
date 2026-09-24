using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO; // Necesario para guardar archivos
using Unity.Cinemachine; // Cinemachine 3.x — corte de cámara al reposicionar

public class GameManager01 : MonoBehaviour
{
    public static GameManager01 instance;

    [Header("Control de Escenas")]
    public string nextDoorID;
    public Vector2 lastCheckPointPos;

    // --- REFERENCIAS A DATOS ---
    public PlayerData currentData = new PlayerData(); // Los datos vivos del juego

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadGame(); // Al iniciar el juego, intentamos cargar datos previos

            // sceneLoaded se dispara DESPUÉS de todos los Awake de la escena
            // nueva y ANTES de todos los Start. Es exactamente la ventana que
            // necesitamos para colocar a Romerito: ya existe (su Awake corrió)
            // pero CameraFinder.Start todavía no ha enganchado la Cinemachine,
            // así que la cámara se inicializa ya sobre la posición correcta y
            // no hay que forzar ningún corte de cámara.
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        if (instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene escena, LoadSceneMode modo)
    {
        if (SesionMictlan.intencion != IntencionArranque.Continuar) return;

        // La intención se consume aunque falle la colocación: no queremos que
        // se quede pegada y teletransporte a Romerito al cambiar de sala.
        SesionMictlan.intencion = IntencionArranque.Ninguna;

        if (!currentData.tieneCheckpointGuardado)
        {
            // Partida guardada antes de tocar ningún Cihuacalli: Romerito se
            // queda donde el diseñador lo colocó en la escena.
            Debug.Log("[GameManager] Continuar sin Cihuacalli previo: " +
                      "Romerito arranca en el spawn de la escena.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[GameManager] No hay Player en '" + escena.name +
                           "'. No se pudo restaurar la posición.");
            return;
        }

        player.transform.position = lastCheckPointPos;

        Rigidbody2D rbJugador = player.GetComponent<Rigidbody2D>();
        if (rbJugador != null)
        {
            rbJugador.linearVelocity = Vector2.zero;
            rbJugador.angularVelocity = 0f;
        }

        // La colocación resuelve DÓNDE tiene que mirar la cámara, pero no
        // impide que llegue ahí interpolando. Esto es lo que corta.
        CortarCamara();
        StartCoroutine(CortarCamaraTrasUnFrame());

        Debug.Log("[GameManager] Romerito restaurado en " + lastCheckPointPos);
    }

    // ── Corte de cámara ──────────────────────────────────────

    /// <summary>
    /// Invalida el estado previo de todas las CinemachineCamera de la escena.
    ///
    /// PreviousStateIsValid = false hace que Cinemachine trate su próximo
    /// update como el primer frame de esa cámara: sin estado anterior no hay
    /// nada que interpolar y el damping no entra en juego. Es el mecanismo
    /// que la propia Cinemachine usa internamente al activar una cámara.
    ///
    /// No confundir con poner el Damping a 0: eso mataría el peso de la
    /// cámara durante todo el juego. Aquí sólo se salta UN frame.
    /// </summary>
    void CortarCamara()
    {
        var camaras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);

        foreach (var cam in camaras)
            cam.PreviousStateIsValid = false;
    }

    /// <summary>
    /// Segunda pasada, un frame después.
    ///
    /// CameraFinder.Start() asigna el Follow y CameraLookControl.Start()
    /// engancha el CinemachinePositionComposer. Ambos corren DESPUÉS de
    /// sceneLoaded, y cualquiera de los dos puede dejar la cámara con
    /// estado válido antes de que llegue a encuadrar a Romerito.
    ///
    /// Repetir el corte aquí cuesta un FindObjectsByType y nos hace
    /// inmunes al orden de ejecución, que no queremos tener que recordar
    /// cada vez que añadamos un componente a la cámara.
    /// </summary>
    IEnumerator CortarCamaraTrasUnFrame()
    {
        yield return null;
        CortarCamara();
    }

    // --- SISTEMA DE GUARDADO ---

    public void SaveGame()
    {
        // 1. Recopilar datos actuales de Romerito
        GatherDataFromPlayer();

        // 2. Convertir a JSON (Texto)
        string json = JsonUtility.ToJson(currentData, true);

        // 3. Escribir en disco
        File.WriteAllText(SaveSystem.Ruta, json);

        Debug.Log("Juego Guardado en: " + SaveSystem.Ruta);
    }

    public void LoadGame()
    {
        if (File.Exists(SaveSystem.Ruta))
        {
            // 1. Leer texto
            string json = File.ReadAllText(SaveSystem.Ruta);

            // 2. Convertir Texto a Datos
            currentData = JsonUtility.FromJson<PlayerData>(json);

            lastCheckPointPos = new Vector2(currentData.positionX, currentData.positionY);
            Debug.Log("Datos cargados correctamente.");
        }
        else
        {
            Debug.Log("No hay archivo de guardado. Iniciando juego nuevo.");
        }
    }

    // --- PUENTES ENTRE JUGADOR Y MANAGER ---

    // Llamamos a esto justo antes de guardar para asegurar que tenemos lo último
    void GatherDataFromPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Guardar Posición
            // (Ojo: Generalmente guardamos la del último Checkpoint, no la actual exacta, 
            // pero si quieres guardar exacto usa player.transform.position)
            if (guardarEnPosicionActual)
            {
                // Guardado manual desde el menú de pausa con la opción de
                // "guardar donde estoy". No marcamos tieneCheckpointGuardado
                // como algo distinto: para Continuar es una posición válida
                // igual que un Cihuacalli.
                currentData.positionX = player.transform.position.x;
                currentData.positionY = player.transform.position.y;
                currentData.tieneCheckpointGuardado = true;
                lastCheckPointPos = player.transform.position;
                guardarEnPosicionActual = false; // se consume, no es un modo
            }
            else
            {
                currentData.positionX = lastCheckPointPos.x;
                currentData.positionY = lastCheckPointPos.y;
            }

            currentData.currentScene = SceneManager.GetActiveScene().name;

            // Guardar Movimiento
            RomeritoMovement mov = player.GetComponent<RomeritoMovement>();
            if (mov != null)
            {
                currentData.unlockDoubleJump = mov.unlockDoubleJump;
                currentData.unlockRun = mov.unlockRun;
                currentData.unlockWallClimb = mov.unlockWallClimb;
                currentData.unlockWallJump = mov.unlockWallJump;
                currentData.unlockDash = mov.unlockDash;
            }

            // Guardar Combate
            RomeritoCombat combat = player.GetComponent<RomeritoCombat>();
            if (combat != null)
            {
                currentData.tieneMacuahuitl = combat.tieneMacuahuitl;
                currentData.unlockHuehueteotl = combat.unlockHuehueteotl;
                currentData.unlockTlaloc = combat.unlockTlaloc;
                currentData.unlockTepeyollotl = combat.unlockTepeyollotl;
            }

            // Guardar Economía
            Monedero monedero = player.GetComponent<Monedero>();
            if (monedero != null)
            {
                currentData.cacao = monedero.cacaoSeeds;
                currentData.tajaderas = monedero.tajaderas;
            }

            // Guardar Tonalli (solo la ampliación de capacidad — el Tonalli actual no se guarda)
            if (TonalliSystem.Instance != null)
                currentData.bonusCapacidadTonalli = TonalliSystem.Instance.BonusCapacidadPct;
        }
    }

    public void UpdateCheckPoint(Vector2 pos)
    {
        lastCheckPointPos = pos;

        if (currentData != null)
            currentData.tieneCheckpointGuardado = true;

        SaveGame(); // ¡Guardado Automático al tocar Checkpoint!
    }

    // ── GUARDADO MANUAL (menú de pausa) ──────────────────────

    /// <summary>
    /// Bandera de un solo uso leída por GatherDataFromPlayer. Se pone a true
    /// justo antes de un SaveGame para que esa escritura concreta use la
    /// posición actual de Romerito en vez del último Cihuacalli.
    /// </summary>
    [System.NonSerialized] public bool guardarEnPosicionActual = false;

    /// <summary>
    /// Guardado invocado desde el menú de pausa.
    /// </summary>
    /// <param name="usarPosicionActual">
    /// true  → Romerito reaparecerá exactamente donde está ahora.
    ///         Cómodo, pero permite guardar sobre pinchos o en caída libre.
    /// false → reaparecerá en su último Cihuacalli (comportamiento clásico
    ///         de Metroidvania; es lo que ya hace la muerte).
    /// </param>
    public void GuardarDesdeMenuPausa(bool usarPosicionActual)
    {
        // Si nunca ha tocado un Cihuacalli no hay checkpoint al que volver,
        // así que forzamos posición actual: es eso o perder toda la sesión.
        if (!usarPosicionActual && currentData != null &&
            !currentData.tieneCheckpointGuardado)
        {
            usarPosicionActual = true;
        }

        guardarEnPosicionActual = usarPosicionActual;
        SaveGame();
    }


    // ── CIHUACALLIS (Checkpoints) ────────────────────────────
    // Solo uno encendido a la vez: el que coincide con cihuacalliActivoID.

    public string CihuacalliActivoID =>
        currentData != null ? currentData.cihuacalliActivoID : "";

    public bool EsCihuacalliActivo(string id) =>
        currentData != null &&
        !string.IsNullOrEmpty(id) &&
        currentData.cihuacalliActivoID == id;

    /// <summary>
    /// Hace de este Cihuacalli el checkpoint actual: pasa a ser el ÚNICO
    /// encendido, fija el punto de reaparición y guarda (UpdateCheckPoint
    /// ya hace el SaveGame). El Cihuacalli anterior queda apagado.
    /// </summary>
    public void ActivarCihuacalli(string id, Vector2 pos)
    {
        if (currentData != null)
            currentData.cihuacalliActivoID = id;
        UpdateCheckPoint(pos);
    }

    // ── BRASEROS DE HUEHUETÉOTL ───────────────────────────────
    // A diferencia de los Cihuacallis, varios pueden estar encendidos
    // a la vez y, una vez encendidos, se quedan así de forma permanente.

    public bool BraseroEncendido(string id) =>
        currentData != null && currentData.braserosEncendidos.Contains(id);

    /// <summary>Marca un brasero como encendido (permanente) y guarda.</summary>
    public void EncenderBrasero(string id)
    {
        if (currentData != null)
            currentData.RegistrarID(currentData.braserosEncendidos, id);
        SaveGame();
    }


    // --- NUEVA PARTIDA ---
    public void NewGame()
    {
        // 1. Borrar el archivo físico si existe
        SaveSystem.Borrar();

        // 2. Resetear los datos en la memoria RAM
        currentData = new PlayerData(); // Crea una hoja en blanco

        // 3. Resetear valores temporales del Manager
        lastCheckPointPos = Vector2.zero; // O la posición inicial que prefieras
        nextDoorID = "";

        // 4. Recargar la escena para que Romerito se reinicie
        // (Asegúrate de tener "using UnityEngine.SceneManagement;" arriba)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // O si tienes una escena específica de inicio:
        // SceneManager.LoadScene("Nivel_1_Mictlan");
    }

    [ContextMenu("Borrar Save (Desarrollo)")]
    public void BorrarSaveDesarrollo()
    {
        SaveSystem.Borrar();
        currentData = new PlayerData();
        Debug.Log("[Dev] Save borrado. Próximo Play empieza desde cero.");
    }

    [ContextMenu("DEV: Dar Estuche de Tlacuilo")]
    void DevDarEstuche()
    {
        MapManager.DarEstucheDeTlacuilo();
        SaveGame();
        Debug.Log("[DEV] Estuche otorgado. Camina entre salas para generar Borrador.");
    }

    [ContextMenu("DEV: Comprar papel Nivel 0")]
    void DevComprarPapelN0()
    {
        MapManager.ComprarPapel(0);
        SaveGame();
        Debug.Log("[DEV] Amate del Nivel 0 comprado.");
    }

    [ContextMenu("DEV: Comprar papel Nivel 1")]
    void DevComprarPapelN1()
    {
        MapManager.ComprarPapel(1);
        SaveGame();
        Debug.Log("[DEV] Amate del Nivel 1 comprado.");
    }

    public void SetNextDoor(string doorID)
    {
        nextDoorID = doorID;
    }
}