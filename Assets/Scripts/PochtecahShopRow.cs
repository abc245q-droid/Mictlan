using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  PochtecahShopRow — una fila del catálogo (Nombre + Precio)
// ============================================================
//  La descripción YA NO vive aquí: al recibir foco (mando) o hover
//  (mouse), la fila avisa al UI para que la muestre en el panel lateral.
//  Así la fila siempre es corta y nunca se desborda.
//
//  Cablea en el PREFAB: boton, nombreText, precioText.
// ============================================================

public class PochtecahShopRow : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [Header("Refs del prefab")]
    public Button boton;
    public TMP_Text nombreText;
    public TMP_Text precioText;

    [Header("Estilo")]
    public Color colorComprable = new Color(0.95f, 0.92f, 0.82f, 1f); // amate cálido
    public Color colorSinFondos = new Color(0.55f, 0.5f, 0.45f, 1f);  // apagado
    public Color colorComprado  = new Color(0.45f, 0.7f, 0.5f, 1f);   // verde "asentado"

    private EntradaCatalogo entrada;
    private PochtecahShop tienda;
    private System.Action onCompra;
    private System.Action<EntradaCatalogo> onSeleccion;

    public EntradaCatalogo Entrada => entrada;

    public void Configurar(EntradaCatalogo entrada, PochtecahShop tienda,
                           System.Action onCompra, System.Action<EntradaCatalogo> onSeleccion)
    {
        this.entrada = entrada;
        this.tienda = tienda;
        this.onCompra = onCompra;
        this.onSeleccion = onSeleccion;

        if (nombreText != null) nombreText.text = entrada.nombreMostrado;

        if (boton != null)
        {
            boton.onClick.RemoveAllListeners();
            boton.onClick.AddListener(AlPulsar);
        }

        Refrescar();
    }

    public void Refrescar()
    {
        if (entrada == null || tienda == null) return;

        bool comprado = tienda.YaComprado(entrada.tipo);
        bool puede = tienda.PuedeComprar(entrada.tipo);

        if (precioText != null)
        {
            if (comprado) precioText.text = "Asentado";
            else
            {
                string p = entrada.costoCacao + " cacao";
                if (entrada.costoTajaderas > 0) p += " + " + entrada.costoTajaderas + " taj.";
                precioText.text = p;
            }
        }

        if (boton != null) boton.interactable = !comprado;   // navegable aunque no alcance; el color avisa si no puedes pagar

        Color c = comprado ? colorComprado : (puede ? colorComprable : colorSinFondos);
        if (nombreText != null) nombreText.color = c;
        if (precioText != null) precioText.color = c;
    }

    private void AlPulsar() => onCompra?.Invoke();

    // Foco por mando (navegación del EventSystem).
    public void OnSelect(BaseEventData eventData) => onSeleccion?.Invoke(entrada);

    // Hover por mouse.
    public void OnPointerEnter(PointerEventData eventData) => onSeleccion?.Invoke(entrada);
}
