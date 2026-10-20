using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  CodiceUI — El Códice de Romerito (menú de progreso)
// ============================================================
//
//  Menú con pestañas donde se consulta el progreso: mapa,
//  inventario, equipamiento, bestiario y herbario. Se desbloquea
//  completo, de una vez, con el Códice Maestro que regala el
//  Pochtecah del Nivel 0 (ver CodiceMaestro / PochtecahNPC).
//
//  CONTROLES (mando Xbox / Steam Deck · teclado):
//    View (JoystickButton6) · Tab   → abrir / cerrar
//    LB / RB                · Q / E → pestaña anterior / siguiente
//    B                      · Escape → cerrar
//
//  Antes de recibir el Códice, View/Tab no hace nada. El menú de
//  pausa (Start/Escape) existe siempre: no depende del Códice.
//
//  PESTAÑA MAPA
//    Reutiliza MapScreenUI tal cual. Al asignarlo en 'mapa', este
//    script lo pone en modo "gestionado": MapScreenUI deja de leer
//    su propio input y solo dibuja cuando la pestaña Mapa está
//    visible. Su 'panel' debe ser la página del Mapa (o un hijo).
//
//  PESTAÑAS SIN SISTEMA TODAVÍA (inventario, equipamiento,
//  bestiario, herbario): son GameObjects de página que se activan
//  y desactivan. Por ahora pueden contener solo el amate en blanco
//  y un texto; cada sistema llenará su página después.
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. Este componente va en un GameObject SIEMPRE ACTIVO (ej. el
//     mismo objeto que hoy tiene MapScreenUI). No dentro de panelRaiz.
//  2. panelRaiz → panel completo del Códice (fondo de amate, barra
//     de pestañas, área de página). Empieza inactivo.
//  3. En 'paginas', una entrada por pestaña, en el orden de la barra:
//       tipo, titulo, pagina (GameObject), encabezado (Image/TMP de
//       la pestaña en la barra, se tiñe activa/inactiva) y
//       primerSeleccionable (opcional, para navegar con mando).
//  4. mapa → el MapScreenUI de la escena.
//  5. hudJuego → el HUD a ocultar mientras el Códice está abierto.
//  ─────────────────────────────────────────────────────────────
//
// ============================================================

public enum PestanaCodice
{
    Mapa,
    Inventario,
    Equipamiento,
    Bestiario,
    Herbario
}

[System.Serializable]
public class PaginaCodice
{
    public PestanaCodice tipo;

    [Tooltip("Título que se muestra arriba al estar en esta pestaña. " +
             "Vacío = nombre del tipo.")]
    public string titulo = "";

    [Tooltip("Contenido de la página. Se activa solo cuando esta pestaña está visible.")]
    public GameObject pagina;

    [Tooltip("Gráfico de la pestaña en la barra superior (Image o TMP). " +
             "Se tiñe con el color activo/inactivo. Opcional.")]
    public Graphic encabezado;

    [Tooltip("Selectable que recibe el foco del mando al entrar a la página. Opcional.")]
    public GameObject primerSeleccionable;
}

public class CodiceUI : MonoBehaviour
{
    // ── Estado global (lo consultan Favores, interactuables y pausa) ──

    public static bool IsOpen { get; private set; }

    /// <summary>Frame en que se cerró el Códice por última vez.</summary>
    public static int FrameCierre { get; private set; } = -1;

    /// <summary>
    /// True con el Códice abierto Y en el frame en que se cierra.
    /// B cierra el Códice pero también es interactuar y ejecutar Favor:
    /// sin el frame de cierre, cerrar junto a un Pochtecah abriría su
    /// tienda, o lanzaría la habilidad del Favor activo.
    /// </summary>
    public static bool BloqueaInput => IsOpen || Time.frameCount <= FrameCierre;

    // ── Inspector ────────────────────────────────────────────

    [Header("Refs de UI")]
    [Tooltip("Panel completo del Códice. Empieza inactivo.")]
    public GameObject panelRaiz;
    [Tooltip("HUD a ocultar mientras el Códice está abierto (opcional).")]
    public GameObject hudJuego;
    [Tooltip("Texto con el título de la pestaña actual (opcional).")]
    public TMP_Text tituloLabel;
    [Tooltip("MapScreenUI de la escena. Se dibuja solo en la pestaña Mapa.")]
    public MapScreenUI mapa;

    [Header("Páginas (en el orden de la barra)")]
    public List<PaginaCodice> paginas = new List<PaginaCodice>();

    [Header("Estilo de pestañas")]
    public Color colorPestanaActiva   = new Color(0.90f, 0.55f, 0.20f, 1.00f);
    public Color colorPestanaInactiva = new Color(0.35f, 0.30f, 0.24f, 0.60f);

    [Header("Comportamiento")]
    [Tooltip("true → el mundo se congela (PausaMundo) mientras el Códice está abierto.\n" +
             "false → estilo Hollow Knight: Romerito queda quieto pero los " +
             "enemigos siguen moviéndose y pueden golpearlo.")]
    public bool pausarMundo = true;

    [Tooltip("Al reabrir, vuelve a la última pestaña consultada. " +
             "Si es false, siempre abre en la primera (Mapa).")]
    public bool recordarUltimaPestana = true;

    [Header("Input")]
    public KeyCode botonAbrir     = KeyCode.JoystickButton6;   // View / Back
    public KeyCode teclaAbrir     = KeyCode.Tab;
    public KeyCode botonAnterior  = KeyCode.JoystickButton4;   // LB
    public KeyCode teclaAnterior  = KeyCode.Q;
    public KeyCode botonSiguiente = KeyCode.JoystickButton5;   // RB
    public KeyCode teclaSiguiente = KeyCode.E;
    public KeyCode botonCerrar    = KeyCode.JoystickButton1;   // B
    public KeyCode teclaCerrar    = KeyCode.Escape;

    // ── Estado interno ───────────────────────────────────────

    private int indiceActual = 0;
    private bool bloquearInputUnFrame;

    // Congelado del jugador. Se recuerda el estado previo de cada
    // componente: si algo ya los tenía apagados (secuencia de muerte,
    // cinemática), cerrar el Códice no debe reactivarlos.
    private RomeritoMovement playerMovement;
    private RomeritoCombat playerCombat;
    private Rigidbody2D playerRb;
    private bool movimientoEstabaActivo;
    private bool combateEstabaActivo;

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        IsOpen = false;

        if (panelRaiz != null) panelRaiz.SetActive(false);
        foreach (var p in paginas)
            if (p != null && p.pagina != null) p.pagina.SetActive(false);

        // A partir de aquí el mapa solo se abre desde el Códice.
        if (mapa != null) mapa.gestionadoPorCodice = true;
    }

    void OnDestroy()
    {
        // Desmontaje de sesión con el Códice abierto: no dejar el mundo pausado.
        if (IsOpen)
        {
            IsOpen = false;
            if (pausarMundo) PausaMundo.Liberar(PausaMundo.Codice);
        }
    }

    // Con "Enter Play Mode Options" (sin recarga de dominio) los estáticos
    // sobreviven entre Plays: limpiamos al arrancar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarAlArrancar()
    {
        IsOpen = false;
        FrameCierre = -1;
    }

    void Update()
    {
        if (!IsOpen)
        {
            if (!PuedeAbrir()) return;
            if (Input.GetKeyDown(botonAbrir) || Input.GetKeyDown(teclaAbrir))
                Abrir();
            return;
        }

        // El mismo View/Tab que abrió no debe cerrar en el mismo frame.
        if (bloquearInputUnFrame) { bloquearInputUnFrame = false; return; }

        if (Input.GetKeyDown(botonAbrir) || Input.GetKeyDown(teclaAbrir) ||
            Input.GetKeyDown(botonCerrar) || Input.GetKeyDown(teclaCerrar))
        {
            Cerrar();
            return;
        }

        if (Input.GetKeyDown(botonAnterior) || Input.GetKeyDown(teclaAnterior))
            CambiarPestana(-1);
        else if (Input.GetKeyDown(botonSiguiente) || Input.GetKeyDown(teclaSiguiente))
            CambiarPestana(+1);
    }

    /// <summary>
    /// ¿Se puede abrir ahora? Requiere el Códice Maestro y que ninguna
    /// otra UI tenga el control (diálogo, tienda, pausa, modal de tutorial).
    /// </summary>
    private bool PuedeAbrir()
    {
        if (!CodiceMaestro.TieneCodice) return false;
        if (DialogueManager.IsActive) return false;
        if (PochtecahShopUI.IsOpen) return false;
        if (PauseMenu.EstaAbierto) return false;
        if (PausaMundo.Activa) return false;    // modal de tutorial, etc.
        if (paginas.Count == 0) return false;
        return true;
    }

    // ── API pública ──────────────────────────────────────────

    /// <summary>Abre el Códice en la pestaña que toque (última o primera).</summary>
    public void Abrir()
    {
        int indice = recordarUltimaPestana ? indiceActual : 0;
        AbrirEnIndice(indice);
    }

    /// <summary>
    /// Abre el Códice directamente en una pestaña. Pensado para NPCs:
    /// el ticitl puede abrir el Herbario al entregarle una planta.
    /// Respeta el desbloqueo: sin Códice Maestro no hace nada.
    /// </summary>
    public void AbrirEn(PestanaCodice tipo)
    {
        int indice = BuscarIndice(tipo);
        if (indice < 0)
        {
            Debug.LogWarning("[Códice] No hay página configurada para " + tipo);
            return;
        }
        AbrirEnIndice(indice);
    }

    public void Cerrar()
    {
        if (!IsOpen) return;
        IsOpen = false;
        FrameCierre = Time.frameCount;

        // El mapa limpia sus siluetas y se duerme.
        if (mapa != null) mapa.CerrarEmbebido();

        foreach (var p in paginas)
            if (p != null && p.pagina != null) p.pagina.SetActive(false);

        if (panelRaiz != null) panelRaiz.SetActive(false);
        if (hudJuego != null) hudJuego.SetActive(true);

        CongelarJugador(false);
        if (pausarMundo) PausaMundo.Liberar(PausaMundo.Codice);

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ── Internos ─────────────────────────────────────────────

    private void AbrirEnIndice(int indice)
    {
        if (IsOpen) { MostrarPagina(indice); return; }
        if (!CodiceMaestro.TieneCodice || paginas.Count == 0) return;

        IsOpen = true;
        bloquearInputUnFrame = true;

        CongelarJugador(true);
        if (pausarMundo) PausaMundo.Solicitar(PausaMundo.Codice);

        if (panelRaiz != null) panelRaiz.SetActive(true);
        if (hudJuego != null) hudJuego.SetActive(false);

        MostrarPagina(indice);
    }

    private void CambiarPestana(int direccion)
    {
        int n = paginas.Count;
        if (n <= 1) return;
        int nuevo = ((indiceActual + direccion) % n + n) % n;   // vuelta circular
        MostrarPagina(nuevo);
    }

    private void MostrarPagina(int indice)
    {
        if (paginas.Count == 0) return;
        indice = Mathf.Clamp(indice, 0, paginas.Count - 1);
        indiceActual = indice;

        PaginaCodice actual = paginas[indice];

        // Primero apagar el mapa si dejamos de verlo: así su panel no
        // queda encendido si comparte objeto con otra página.
        bool mapaVisible = actual != null && actual.tipo == PestanaCodice.Mapa;
        if (mapa != null && !mapaVisible) mapa.CerrarEmbebido();

        for (int i = 0; i < paginas.Count; i++)
        {
            var p = paginas[i];
            if (p == null) continue;
            bool activa = i == indice;

            if (p.pagina != null) p.pagina.SetActive(activa);
            if (p.encabezado != null)
                p.encabezado.color = activa ? colorPestanaActiva : colorPestanaInactiva;
        }

        if (mapa != null && mapaVisible) mapa.AbrirEmbebido();

        if (tituloLabel != null && actual != null)
            tituloLabel.text = string.IsNullOrEmpty(actual.titulo)
                ? actual.tipo.ToString()
                : actual.titulo;

        // Foco del mando. SetSelectedGameObject(null) primero: si no, el
        // EventSystem puede ignorar la nueva selección.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);

            GameObject foco = actual != null ? actual.primerSeleccionable : null;

            // Páginas con botones creados en tiempo de ejecución (Herbario,
            // Bestiario...): la propia página dice qué recibe el foco.
            if (foco == null && actual != null && actual.pagina != null)
            {
                IPaginaCodice dinamica = actual.pagina.GetComponentInChildren<IPaginaCodice>(true);
                if (dinamica != null) foco = dinamica.AlMostrarPagina();
            }

            if (foco != null)
                EventSystem.current.SetSelectedGameObject(foco);
        }
    }

    private int BuscarIndice(PestanaCodice tipo)
    {
        for (int i = 0; i < paginas.Count; i++)
            if (paginas[i] != null && paginas[i].tipo == tipo) return i;
        return -1;
    }

    // ── Congelado del jugador (espejo de PochtecahShopUI) ────

    private void CongelarJugador(bool congelar)
    {
        if (playerMovement == null || playerCombat == null || playerRb == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerMovement = p.GetComponent<RomeritoMovement>();
                playerCombat = p.GetComponent<RomeritoCombat>();
                playerRb = p.GetComponent<Rigidbody2D>();
            }
        }

        if (congelar)
        {
            movimientoEstabaActivo = playerMovement != null && playerMovement.enabled;
            combateEstabaActivo = playerCombat != null && playerCombat.enabled;

            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
            if (playerMovement != null) playerMovement.enabled = false;
            if (playerCombat != null) playerCombat.enabled = false;
        }
        else
        {
            if (playerMovement != null && movimientoEstabaActivo) playerMovement.enabled = true;
            if (playerCombat != null && combateEstabaActivo) playerCombat.enabled = true;
        }
    }
}
