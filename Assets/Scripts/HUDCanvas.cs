using UnityEngine;
using TMPro;

// ============================================================
//  HUDCanvas — Anclaje del HUD por escena
// ============================================================
//
//  El HUD de gameplay vive en el Canvas de cada escena y se destruye
//  al cargar la siguiente. Los sistemas persistentes (los que viven
//  en Romerito con DontDestroyOnLoad — Monedero, etc.) mantienen
//  referencias serializadas a esos TMP, que quedan "missing" tras el
//  cambio de escena. Este componente resuelve eso:
//
//    1) Cada escena tiene un HUDCanvas en el root de su Canvas HUD.
//    2) Los TMP del HUD se asignan aquí desde el Inspector.
//    3) Los sistemas persistentes hacen FindObjectOfType<HUDCanvas>
//       en sceneLoaded y toman las referencias frescas.
//
//  Es el mismo patrón que RomeritoHealth ya usa con HeartSystem y
//  TonalliSystem, generalizado para el HUD entero.
//
//  SETUP EN UNITY:
//   1. Añadir este componente al GameObject raíz del Canvas HUD de
//      la escena (donde ya viven los corazones, la barra de Tonalli,
//      los TMP de cacao y tajaderas, etc.).
//   2. Arrastrar los TMP correspondientes a sus campos.
//   3. Repetir en cada escena de juego.
//  Extensión: además de exponer referencias TMP, este componente
//  inicializa el estado visual del HUD según PlayerData al cargar
//  cada escena. Actualmente: PanelTonalli visible solo si el
//  jugador ya recibió el Don de Tlacua.
// ============================================================

public class HUDCanvas : MonoBehaviour
{
    [Header("Economía (Monedero)")]
    [Tooltip("TMP que muestra el saldo de semillas de cacao.")]
    public TextMeshProUGUI cacaoText;

    [Tooltip("TMP que muestra el saldo de tajaderas.")]
    public TextMeshProUGUI tajaderaText;

    [Header("Tonalli")]
    [Tooltip("Panel del HUD que muestra la barra de Tonalli. " +
             "Se activa automáticamente si PlayerData.tieneDonDeTlacua es true.")]
    public GameObject panelTonalli;

    void Awake()
    {
        InicializarEstadoHUD();
    }

    /// <summary>
    /// Sincroniza la visibilidad de los elementos del HUD con PlayerData.
    /// Llamado en Awake para asegurar el estado correcto desde el
    /// primer frame de cada escena, y también por DonDeTlacua tras
    /// otorgar el don (para forzar el refresh en la misma escena).
    /// </summary>
    public void InicializarEstadoHUD()
    {
        if (panelTonalli != null)
        {
            bool tieneDon = GameManager01.instance != null &&
                            GameManager01.instance.currentData != null &&
                            GameManager01.instance.currentData.tieneDonDeTlacua;

            panelTonalli.SetActive(tieneDon);
        }
    }
}