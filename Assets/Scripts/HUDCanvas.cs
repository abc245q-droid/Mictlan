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

    [Header("Cacao (aparece con la primera semilla)")]
    [Tooltip("Contenedor del ícono + contador de cacao. Vacío = solo el TMP de cacao.")]
    public GameObject panelCacao;

    void Awake()
    {
        InicializarEstadoHUD();
    }

    // [HUD-N0-FIX] Segunda pasada cuando ya corrieron TODOS los Awake
    // (GameManager01 incluido): en Awake el orden entre objetos no está
    // garantizado y GameManager01.instance puede seguir en null.
    void Start()
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

        // [HUD-N0] El cacao no aparece hasta recoger la primera semilla.
        GameObject cacao = PanelCacao;
        if (cacao != null)
        {
            bool visible = CacaoDesbloqueado;
            cacao.SetActive(visible);
            var gmLog = GameManager01.instance;
            if (gmLog != null && gmLog.currentData != null)
                Debug.Log($"[HUDCanvas] Cacao visible={visible} (bandera={gmLog.currentData.hudCacaoDesbloqueado}, " +
                          $"cacao={gmLog.currentData.cacao}, tajaderas={gmLog.currentData.tajaderas})");
        }
    }

    GameObject PanelCacao =>
        panelCacao != null ? panelCacao : (cacaoText != null ? cacaoText.gameObject : null);

    /// <summary>¿Ya recogió cacao alguna vez? (sin GameManager: sí).</summary>
    public static bool CacaoDesbloqueado
    {
        get
        {
            var gm = GameManager01.instance;
            // Sin datos todavía: bloqueado. La primera semilla lo revela igual.
            if (gm == null || gm.currentData == null) return false;
            PlayerData d = gm.currentData;
            return d.hudCacaoDesbloqueado || d.cacao > 0 || d.tajaderas > 0;
        }
    }

    /// <summary>Muestra el contador de cacao con un 'pop' (primera semilla).</summary>
    public void RevelarCacao()
    {
        GameObject p = PanelCacao;
        if (p == null || p.activeSelf) return;
        p.SetActive(true);
        if (isActiveAndEnabled) StartCoroutine(Pop((RectTransform)p.transform));
    }

    System.Collections.IEnumerator Pop(RectTransform rt)
    {
        const float duracion = 0.25f;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracion);
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.2f, k / 0.6f)
                               : Mathf.Lerp(1.2f, 1f, (k - 0.6f) / 0.4f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }
}