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
//
// ============================================================

public class HUDCanvas : MonoBehaviour
{
    [Header("Economía (Monedero)")]
    [Tooltip("TMP que muestra el saldo de semillas de cacao.")]
    public TextMeshProUGUI cacaoText;

    [Tooltip("TMP que muestra el saldo de tajaderas.")]
    public TextMeshProUGUI tajaderaText;
}
