using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  HerbarioSlot — Una casilla de la cuadrícula del Herbario
// ============================================================
//
//  Al recibir el foco (mando) o el puntero (ratón), le pide a
//  HerbarioUI que muestre el detalle de su planta.
//
//  Si HerbarioUI no tiene 'slotPrefab', crea las casillas solo
//  con este componente. Si quieres un diseño propio, haz un prefab
//  con Button + HerbarioSlot y asigna aquí fondo, icono y etiqueta.
//
// ============================================================

public class HerbarioSlot : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    public Image fondo;
    public Image icono;
    public TMP_Text etiqueta;

    [System.NonSerialized] public int indice;
    [System.NonSerialized] public HerbarioUI ui;

    public void Pintar(Sprite sprite, Color colorIcono, string texto, Color colorFondo)
    {
        if (fondo != null) fondo.color = colorFondo;

        if (icono != null)
        {
            icono.sprite = sprite;
            icono.color = colorIcono;
            icono.enabled = sprite != null;
        }

        if (etiqueta != null) etiqueta.text = texto;
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (ui != null) ui.AlSeleccionarSlot(indice);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // El ratón también mueve el foco, así mando y ratón no se pelean.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }
}
