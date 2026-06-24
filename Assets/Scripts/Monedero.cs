using UnityEngine;
using TMPro; // <--- IMPORTANTE: Necesario para usar textos modernos

public class Monedero : MonoBehaviour
{
    [Header("Economía")]
    public int cacaoSeeds = 0;   // Moneda común
    public int tajaderas = 0;    // Moneda rara (Valen por 20 cacaos o son para items especiales)

    [Header("Interfaz (UI)")]
    // Aquí arrastraremos los textos que creaste en el Canvas
    public TextMeshProUGUI cacaoText;
    public TextMeshProUGUI tajaderaText;

    void Start()
    {
        if (GameManager01.instance != null)
        {
            cacaoSeeds = GameManager01.instance.currentData.cacao;
            tajaderas = GameManager01.instance.currentData.tajaderas;
        }
        UpdateUI();
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