using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  PochtecahShopUI — Panel de la tienda del Pochtecah
// ============================================================
//
//  Lista de filas cortas (Nombre + Precio) + un PANEL LATERAL de
//  detalle que muestra la descripción de la fila seleccionada.
//
//  Reusa los patrones del juego:
//    • Congela a Romerito SIN timeScale (igual que DialogueManager).
//    • Selección por EventSystem para navegar con el mando.
//    • Bandera estática IsOpen para que NPCs/Cihuacalli ignoren input.
//
//  Cableado en el inspector:
//    panel             — raíz del panel (se activa/desactiva).
//    contenedorFilas   — Transform con VerticalLayoutGroup donde van las filas.
//    filaPrefab        — prefab con PochtecahShopRow.
//    cacaoLabel        — TMP del saldo de cacao (opcional).
//    mensajeLabel      — TMP para "Sin fondos", etc. (opcional).
//    detalleNombre     — TMP del nombre, en el panel lateral (opcional).
//    detalleDescripcion— TMP de la descripción, en el panel lateral (opcional).
//    detallePrecio     — TMP del precio, en el panel lateral (opcional).
//
// ============================================================

public class PochtecahShopUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Refs de UI")]
    public GameObject panel;
    public Transform contenedorFilas;
    public GameObject filaPrefab;
    public TMP_Text cacaoLabel;
    public TMP_Text tajaderaLabel;
    public TMP_Text mensajeLabel;

    [Header("HUD a ocultar mientras compras")]
    [Tooltip("Raíz del HUD de juego (corazones, Tonalli, cacao). Se oculta al abrir la tienda.")]
    public GameObject hudJuego;

    [Header("Panel de detalle (lateral)")]
    public TMP_Text detalleNombre;
    public TMP_Text detalleDescripcion;
    public TMP_Text detallePrecio;

    [Header("Input")]
    [Tooltip("Cerrar la tienda. Por defecto B de Xbox + Escape.")]
    public KeyCode botonCerrar = KeyCode.JoystickButton1;
    public KeyCode teclaCerrarAlterna = KeyCode.Escape;

    // ── Estado interno ──
    private PochtecahShop tiendaActual;
    private readonly List<PochtecahShopRow> filas = new List<PochtecahShopRow>();
    private bool bloquearInputUnFrame;
    private System.Action onCerrar;

    // Congelado del jugador (espejo de DialogueManager).
    private RomeritoMovement playerMovement;
    private RomeritoCombat playerCombat;
    private Rigidbody2D playerRb;
    private bool refsCacheadas;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        if (IsOpen) IsOpen = false;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (bloquearInputUnFrame) { bloquearInputUnFrame = false; return; }

        if (Input.GetKeyDown(botonCerrar) || Input.GetKeyDown(teclaCerrarAlterna))
            Cerrar();
    }

    // ── Apertura / cierre ─────────────────────────────────
    public void Abrir(PochtecahShop tienda, System.Action alCerrar = null)
    {
        if (IsOpen || tienda == null) return;

        tiendaActual = tienda;
        onCerrar = alCerrar;
        IsOpen = true;
        bloquearInputUnFrame = true;   // el mismo B que abrió no cierra

        CongelarJugador(true);
        if (panel != null) panel.SetActive(true);
        if (hudJuego != null) hudJuego.SetActive(false);   // ← AÑADIR
        if (mensajeLabel != null) mensajeLabel.text = "";

        ConstruirFilas();
        Refrescar();
        SeleccionarPrimeraComprable();
    }

    public void Cerrar()
    {
        if (!IsOpen) return;
        IsOpen = false;

        if (panel != null) panel.SetActive(false);
        if (hudJuego != null) hudJuego.SetActive(true);    // ← AÑADIR
        CongelarJugador(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        onCerrar?.Invoke();
        tiendaActual = null;
    }

    // ── Construcción de filas ─────────────────────────────
    private void ConstruirFilas()
    {
        foreach (Transform hijo in contenedorFilas) Destroy(hijo.gameObject);
        filas.Clear();

        if (filaPrefab == null || contenedorFilas == null)
        {
            Debug.LogError("[PochtecahUI] Falta filaPrefab o contenedorFilas.");
            return;
        }

        foreach (var entrada in tiendaActual.catalogo)
        {
            GameObject go = Instantiate(filaPrefab, contenedorFilas);
            go.SetActive(true);
            var fila = go.GetComponent<PochtecahShopRow>();
            if (fila == null)
            {
                Debug.LogError("[PochtecahUI] El filaPrefab no tiene PochtecahShopRow.");
                continue;
            }
            EntradaCatalogo e = entrada; // captura local para los closures
            fila.Configurar(e, tiendaActual, () => IntentarComprar(e), MostrarDetalle);
            filas.Add(fila);
        }
    }

    private void IntentarComprar(EntradaCatalogo entrada)
    {
        var r = tiendaActual.Comprar(entrada.tipo);
        if (mensajeLabel != null)
        {
            switch (r)
            {
                case PochtecahShop.ResultadoCompra.Exito: mensajeLabel.text = "El Pochtecah asiente."; break;
                case PochtecahShop.ResultadoCompra.SinFondos: mensajeLabel.text = "No tienes suficiente cacao."; break;
                case PochtecahShop.ResultadoCompra.YaLoTienes: mensajeLabel.text = "Ya lo llevas contigo."; break;
                default: mensajeLabel.text = ""; break;
            }
        }

        Refrescar();
        ReenfocarTrasCompra(entrada);
    }

    // Tras comprar, la fila adquirida queda no-interactable y el EventSystem
    // pierde el foco. Sin foco no se puede seguir navegando ni comprando.
    // Reanclamos el foco a una fila aún comprable para no tener que reabrir.
    private void ReenfocarTrasCompra(EntradaCatalogo entradaComprada)
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);

        PochtecahShopRow siguiente = null;
        foreach (var f in filas)
            if (f.boton != null && f.boton.interactable) { siguiente = f; break; }

        if (siguiente != null)
        {
            EventSystem.current.SetSelectedGameObject(siguiente.boton.gameObject);
            MostrarDetalle(siguiente.Entrada);
        }
        else
        {
            // Ya no queda nada por comprar: deja el detalle en lo recién adquirido.
            MostrarDetalle(entradaComprada);
        }
    }

    // ── Panel de detalle ──────────────────────────────────
    private void MostrarDetalle(EntradaCatalogo entrada)
    {
        if (entrada == null) return;

        if (detalleNombre != null) detalleNombre.text = entrada.nombreMostrado;
        if (detalleDescripcion != null) detalleDescripcion.text = entrada.descripcion;

        if (detallePrecio != null)
        {
            if (tiendaActual != null && tiendaActual.YaComprado(entrada.tipo))
                detallePrecio.text = "Asentado";
            else
            {
                string p = entrada.costoCacao + " cacao";
                if (entrada.costoTajaderas > 0) p += " + " + entrada.costoTajaderas + " tajaderas";
                detallePrecio.text = p;
            }
        }
    }

    // ── Refresco de estado ────────────────────────────────
    private void Refrescar()
    {
        foreach (var f in filas) f.Refrescar();

        if (tiendaActual != null && tiendaActual.monedero != null)
        {
            if (cacaoLabel != null) cacaoLabel.text = tiendaActual.monedero.cacaoSeeds.ToString();
            if (tajaderaLabel != null) tajaderaLabel.text = tiendaActual.monedero.tajaderas.ToString();
        }
    }

    private void SeleccionarPrimeraComprable()
    {
        if (EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);

        PochtecahShopRow objetivo = null;
        foreach (var f in filas)
            if (f.boton != null && f.boton.interactable) { objetivo = f; break; }

        // Si nada es comprable, enfoca la primera para no dejar el foco vacío.
        if (objetivo == null && filas.Count > 0) objetivo = filas[0];

        if (objetivo != null && objetivo.boton != null)
        {
            EventSystem.current.SetSelectedGameObject(objetivo.boton.gameObject);
            MostrarDetalle(objetivo.Entrada); // por si OnSelect no dispara a tiempo
        }
    }

    // ── Congelado del jugador (espejo exacto de DialogueManager) ──
    private void CongelarJugador(bool congelar)
    {
        if (!refsCacheadas)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerMovement = p.GetComponent<RomeritoMovement>();
                playerCombat = p.GetComponent<RomeritoCombat>();
                playerRb = p.GetComponent<Rigidbody2D>();
            }
            refsCacheadas = true;
        }

        if (congelar && playerRb != null) playerRb.linearVelocity = Vector2.zero;
        if (playerMovement != null) playerMovement.enabled = !congelar;
        if (playerCombat != null) playerCombat.enabled = !congelar;
    }
}
