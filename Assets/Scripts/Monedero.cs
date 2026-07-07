using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; // <--- IMPORTANTE: Necesario para usar textos modernos

public class Monedero : MonoBehaviour
{
    [Header("Economía")]
    public int cacaoSeeds = 0;   // Moneda común
    public int tajaderas = 0;    // Moneda rara (Valen por 20 cacaos o son para items especiales)

    [Header("Interfaz (UI)")]
    // ══════════════════════════════════════════════════════════════
    //  FIX — BUG: al cambiar de escena el HUD anterior se destruye y
    //  estas referencias quedaban "missing", así el contador se veía
    //  en 0 aunque cacaoSeeds internamente fuera correcto.
    //  Ahora se re-buscan automáticamente vía HUDCanvas en cada
    //  sceneLoaded. Asignarlas en el inspector es opcional: sirve
    //  como fallback para la escena inicial antes del primer load.
    // ══════════════════════════════════════════════════════════════
    public TextMeshProUGUI cacaoText;
    public TextMeshProUGUI tajaderaText;

    void Start()
    {
        if (GameManager01.instance != null)
        {
            cacaoSeeds = GameManager01.instance.currentData.cacao;
            tajaderas = GameManager01.instance.currentData.tajaderas;
        }
        ReencontrarUI();
        UpdateUI();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Al cargar una nueva escena, el HUD anterior murió con ella.
    // Re-enganchamos el Monedero al HUD nuevo y refrescamos la pantalla.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReencontrarUI();
        UpdateUI();
    }

    /// <summary>
    /// Busca el HUDCanvas de la escena activa y toma sus refs de TMP.
    /// Si no hay HUDCanvas, conserva lo que ya estuviera asignado
    /// (útil para escenas de test sin HUD).
    /// </summary>
    void ReencontrarUI()
    {
        HUDCanvas hud = FindObjectOfType<HUDCanvas>(true); // true: incluye inactivos
        if (hud == null)
        {
            Debug.LogWarning("[Monedero] No hay HUDCanvas en la escena. " +
                             "El contador de cacao/tajaderas no se actualizará visualmente.");
            return;
        }

        if (hud.cacaoText != null) cacaoText = hud.cacaoText;
        if (hud.tajaderaText != null) tajaderaText = hud.tajaderaText;
    }

    // Función para añadir Cacao
    public void AddCacao(int amount)
    {
        cacaoSeeds += amount;

        Debug.Log($"Cacao total: {cacaoSeeds}");
        UpdateUI(); // <--- Refrescamos la pantalla
    }

    // Función para añadir Tajaderas
    public void AddTajadera(int amount)
    {
        tajaderas += amount;
        Debug.Log($"Tajaderas total: {tajaderas}");
        UpdateUI(); // <--- Refrescamos la pantalla
    }

    // ── Pago ──────────────────────────────────────────────
    public bool PuedePagar(int costoCacao, int costoTajaderas = 0)
    {
        return cacaoSeeds >= costoCacao && tajaderas >= costoTajaderas;
    }

    /// <summary>Intenta cobrar. Devuelve true solo si alcanzaba y se descontó.</summary>
    public bool TryGastar(int costoCacao, int costoTajaderas = 0)
    {
        if (!PuedePagar(costoCacao, costoTajaderas)) return false;
        cacaoSeeds -= costoCacao;
        tajaderas -= costoTajaderas;
        UpdateUI();
        return true;
    }

    // Función auxiliar para mantener el código limpio
    void UpdateUI()
    {
        // Convertimos el número a texto
        if (cacaoText != null)
            cacaoText.text = cacaoSeeds.ToString(); // Opcional: cacaoSeeds.ToString("000") para ceros a la izq

        if (tajaderaText != null)
            tajaderaText.text = tajaderas.ToString();
    }
}
