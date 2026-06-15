using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO; // Necesario para guardar archivos

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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- SISTEMA DE GUARDADO ---

    public void SaveGame()
    {
        // 1. Recopilar datos actuales de Romerito
        GatherDataFromPlayer();

        // 2. Convertir a JSON (Texto)
        string json = JsonUtility.ToJson(currentData, true);

        // 3. Escribir en disco
        string path = Application.persistentDataPath + "/romerito_save.json";
        File.WriteAllText(path, json);

        Debug.Log("Juego Guardado en: " + path);
    }

    public void LoadGame()
    {
        string path = Application.persistentDataPath + "/romerito_save.json";

        if (File.Exists(path))
        {
            // 1. Leer texto
            string json = File.ReadAllText(path);

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

    // Llamamos a esto justo antes de guardar para asegurar que tenemos lo �ltimo
    void GatherDataFromPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Guardar Posici�n
            // (Ojo: Generalmente guardamos la del �ltimo Checkpoint, no la actual exacta, 
            // pero si quieres guardar exacto usa player.transform.position)
            currentData.positionX = lastCheckPointPos.x;
            currentData.positionY = lastCheckPointPos.y;
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

            // Guardar Econom�a
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
        SaveGame(); // �Guardado Autom�tico al tocar Checkpoint!
    }


    // --- NUEVA PARTIDA ---
    public void NewGame()
    {
        string path = Application.persistentDataPath + "/romerito_save.json";

        // 1. Borrar el archivo f�sico si existe
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Archivo de guardado eliminado.");
        }

        // 2. Resetear los datos en la memoria RAM
        currentData = new PlayerData(); // Crea una hoja en blanco

        // 3. Resetear valores temporales del Manager
        lastCheckPointPos = Vector2.zero; // O la posici�n inicial que prefieras
        nextDoorID = "";

        // 4. Recargar la escena para que Romerito se reinicie
        // (Aseg�rate de tener "using UnityEngine.SceneManagement;" arriba)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // O si tienes una escena espec�fica de inicio:
        // SceneManager.LoadScene("Nivel_1_Mictlan");
    }

    [ContextMenu("Borrar Save (Desarrollo)")]
    public void BorrarSaveDesarrollo()
    {
        string path = Application.persistentDataPath + "/romerito_save.json";
        if (File.Exists(path)) File.Delete(path);
        currentData = new PlayerData();
        Debug.Log("[Dev] Save borrado. Pr�ximo Play empieza desde cero.");
    }

    public void SetNextDoor(string doorID)
    {
        nextDoorID = doorID;
    }
}