using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  CodiceSlot — Casilla seleccionable de una página del Códice
// ============================================================
//
//  La comparten el Herbario y el Equipamiento (y servirá para el
//  Bestiario). Al recibir el foco del mando (o el puntero):
//    • avisa a su página con 'alSeleccionar(indice)' para que
//      muestre el detalle;
//    • crece un poco ('escalaFoco'), animado con tiempo SIN escalar:
//      el Códice corre con Time.timeScale = 0.
//
//  Las páginas la crean solas con CodiceSlot.Crear(...). Si quieres
//  un diseño propio, haz un prefab con Button + CodiceSlot y asigna
//  fondo, icono y etiqueta.
//
//  No cambia el orden de hermanos al tomar el foco: dentro de un
//  GridLayoutGroup eso reordenaría las casillas.
// ============================================================

public class CodiceSlot : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    public Image fondo;
    public Image icono;
    public TMP_Text etiqueta;

    [System.NonSerialized] public int indice;
    [System.NonSerialized] public System.Action<int> alSeleccionar;
    [System.NonSerialized] public float escalaFoco = 1.1f;

    // Unidades de escala por segundo (0.1 de crecimiento ≈ 0.07 s).
    private const float VELOCIDAD_ESCALA = 1.5f;

    private bool conFoco;

    // ── Pintado ──────────────────────────────────────────────

    public void Pintar(Sprite sprite, Color colorIcono, string texto, Color colorTexto, Color colorFondo)
    {
        if (fondo != null) fondo.color = colorFondo;

        if (icono != null)
        {
            icono.sprite = sprite;
            icono.color = colorIcono;
            icono.enabled = sprite != null;
        }

        if (etiqueta != null)
        {
            etiqueta.text = texto ?? "";
            etiqueta.color = colorTexto;
        }
    }

    // ── Foco ─────────────────────────────────────────────────

    public void OnSelect(BaseEventData eventData)
    {
        conFoco = true;
        alSeleccionar?.Invoke(indice);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        conFoco = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // El ratón también mueve el foco, así mando y ratón no se pelean.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    void OnDisable()
    {
        conFoco = false;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        float objetivo = conFoco ? escalaFoco : 1f;
        float actual = transform.localScale.x;
        if (Mathf.Approximately(actual, objetivo)) return;

        float s = Mathf.MoveTowards(actual, objetivo, Time.unscaledDeltaTime * VELOCIDAD_ESCALA);
        transform.localScale = new Vector3(s, s, 1f);
    }

    // ── Fábrica ──────────────────────────────────────────────

    /// <summary>
    /// Crea una casilla con fondo, icono (arriba) y etiqueta (franja
    /// inferior) dentro de 'padre'. 'colorFoco' tiñe el fondo con foco.
    /// </summary>
    public static CodiceSlot Crear(Transform padre, Color colorFoco)
    {
        var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CodiceSlot));
        go.transform.SetParent(padre, false);

        var slot = go.GetComponent<CodiceSlot>();
        slot.fondo = go.GetComponent<Image>();

        var boton = go.GetComponent<Button>();
        boton.targetGraphic = slot.fondo;
        AplicarColorFoco(boton, colorFoco);

        // Icono: ocupa la casilla con margen, respetando proporción.
        var goIcono = new GameObject("Icono", typeof(RectTransform), typeof(Image));
        var rtIcono = goIcono.GetComponent<RectTransform>();
        rtIcono.SetParent(go.transform, false);
        rtIcono.anchorMin = new Vector2(0f, 0.22f);
        rtIcono.anchorMax = Vector2.one;
        rtIcono.offsetMin = new Vector2(6f, 0f);
        rtIcono.offsetMax = new Vector2(-6f, -6f);
        slot.icono = goIcono.GetComponent<Image>();
        slot.icono.preserveAspect = true;
        slot.icono.raycastTarget = false;

        // Etiqueta: franja inferior con el nombre (o "?").
        var goTxt = new GameObject("Etiqueta", typeof(RectTransform), typeof(TextMeshProUGUI));
        var rtTxt = goTxt.GetComponent<RectTransform>();
        rtTxt.SetParent(go.transform, false);
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = new Vector2(1f, 0.22f);
        rtTxt.offsetMin = new Vector2(2f, 2f);
        rtTxt.offsetMax = new Vector2(-2f, 0f);
        var tmp = goTxt.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 8f;
        tmp.fontSizeMax = 20f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        slot.etiqueta = tmp;

        return slot;
    }

    public static void AplicarColorFoco(Button b, Color colorFoco)
    {
        if (b == null) return;
        ColorBlock c = b.colors;
        c.highlightedColor = colorFoco;
        c.selectedColor = colorFoco;
        b.colors = c;
    }
}
