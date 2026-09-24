using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ============================================================
//  MainMenu — Pantalla de inicio de Mictlán
// ============================================================
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//  1. Escena nueva: Assets/Scenes/MenuPrincipal.unity
//     Debe contener SÓLO: Camera, Canvas, EventSystem.
//     NADA de Romerito, GameManager01, HUD ni TutorialManager —
//     el menú tiene que poder existir sin sesión de juego.
//
//  2. EventSystem → Input Module = StandaloneInputModule
//     (NO InputSystemUIInputModule: sin actions asset asignado la UI
//      se queda sorda, ya nos pasó con el sistema de tutoriales).
//
//  3. Canvas con: título, botón Continuar, botón Nueva Partida,
//     botón Salir, y un panel de confirmación oculto.
//
//  4. Este script en un GameObject vacío "MainMenu". Cablear campos.
//
//  5. Build Settings: MenuPrincipal debe ser la escena de ÍNDICE 0.
//     (Scenes In Build → arrastrarla arriba del todo.)
//
//  MANDO
//  ─────────────────────────────────────────────────────────────
//  Submit = A (joystick button 0), Cancel = B (joystick button 1) —
//  ya están así en el InputManager del proyecto, no hay que tocar nada.
//  La navegación usa los ejes Horizontal/Vertical, así que responde al
//  STICK IZQUIERDO. La cruceta del mando de Xbox son los ejes 6/7 en el
//  Input Manager antiguo y NO mueve la selección: si la quieres, hay que
//  añadir esos ejes al InputManager y mapearlos a Vertical/Horizontal.
// ============================================================

public class MainMenu : MonoBehaviour
{
    [Header("Escenas")]
    [Tooltip("Escena donde empieza una partida nueva. Debe estar en Build Settings.")]
    public string escenaNuevaPartida = "Chicunamictlan_001";

    [Header("Botones")]
    public Button botonContinuar;
    public Button botonNuevaPartida;
    public Button botonSalir;

    [Tooltip("Botón que queda seleccionado al abrir el menú. Sin esto el mando " +
             "no puede navegar: el EventSystem arranca sin nada seleccionado.")]
    public GameObject primerBotonSeleccionado;

    [Header("Info de partida (opcional)")]
    [Tooltip("Texto bajo 'Continuar' con la zona donde quedó Romerito.")]
    public TextMeshProUGUI textoInfoPartida;

    [Header("Confirmación de sobrescritura")]
    [Tooltip("Panel que avisa antes de borrar una partida existente. " +
             "Empieza desactivado.")]
    public GameObject panelConfirmacion;
    public Button botonConfirmarSi;
    public Button botonConfirmarNo;

    // ── Estado ───────────────────────────────────────────────

    private bool hayPartida;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        // Si venimos de "Volver al Inicio", timeScale podría haber quedado
        // en 0 si el menú de pausa usaba Time.timeScale. Sin esto el menú
        // arranca con las animaciones congeladas.
        Time.timeScale = 1f;

        // El Steam Deck no tiene cursor en Modo Gaming, pero en Modo
        // Escritorio y en el editor sí. Lo liberamos por si el gameplay
        // lo había capturado.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        PlayerData datos = SaveSystem.LeerPartida();
        hayPartida = datos != null && SesionMictlan.EscenaValida(datos.currentScene);

        ConfigurarBotonContinuar(datos);

        if (panelConfirmacion != null)
            panelConfirmacion.SetActive(false);

        CablearBotones();
        SeleccionarPrimerBoton();
    }

    void Update()
    {
        // ── B / Escape cierra la confirmación ────────────────
        //
        // Va lo primero y con return: mientras el panel está abierto, el
        // salvavidas de abajo no debe devolver el foco a los botones del
        // fondo, que están tapados por el bloqueador.
        //
        // "Cancel" ya está mapeado a escape + joystick button 1 (B) en el
        // InputManager del proyecto, así que no hay que tocar ajustes.
        // El StandaloneInputModule también procesa Cancel, pero lo envía
        // como ICancelHandler al objeto seleccionado y Button no implementa
        // esa interfaz — así que no hay doble disparo.
        if (ConfirmacionAbierta)
        {
            if (Input.GetButtonDown("Cancel"))
                CancelarConfirmacion();

            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null &&
                Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.2f &&
                botonConfirmarNo != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(botonConfirmarNo.gameObject);
            }

            return;
        }

        // Salvavidas para mando: si por lo que sea el EventSystem pierde
        // la selección (un clic de ratón en el vacío la borra), cualquier
        // movimiento del stick la recupera. Sin esto el menú se vuelve
        // injugable con mando después de un clic accidental.
        if (EventSystem.current == null) return;

        if (EventSystem.current.currentSelectedGameObject == null &&
            Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.2f)
        {
            SeleccionarPrimerBoton();
        }
    }

    /// <summary>
    /// activeInHierarchy, no activeSelf: si alguna vez el panel cuelga de un
    /// contenedor que se desactiva entero, activeSelf seguiría diciendo true
    /// y B cancelaría un diálogo que el jugador no está viendo.
    /// </summary>
    private bool ConfirmacionAbierta =>
        panelConfirmacion != null && panelConfirmacion.activeInHierarchy;

    // ── Construcción del menú ────────────────────────────────

    void ConfigurarBotonContinuar(PlayerData datos)
    {
        if (botonContinuar != null)
        {
            botonContinuar.interactable = hayPartida;

            // Además de desactivarlo, lo sacamos de la navegación del mando:
            // un botón no-interactable se salta solo, pero si es el primero
            // de la lista el EventSystem se queda sin destino inicial.
            if (!hayPartida && primerBotonSeleccionado == botonContinuar.gameObject)
                primerBotonSeleccionado = botonNuevaPartida != null
                    ? botonNuevaPartida.gameObject
                    : null;
        }

        if (textoInfoPartida == null) return;

        if (!hayPartida)
        {
            textoInfoPartida.text = datos == null
                ? ""
                : "Partida guardada ilegible o de una versión anterior.";
            return;
        }

        textoInfoPartida.text = NombreLegibleDeEscena(datos.currentScene);
    }

    void CablearBotones()
    {
        // Cableamos por código en vez de por Inspector para que renombrar
        // un método no deje un onClick roto y silencioso en la escena.
        if (botonContinuar != null)
        {
            botonContinuar.onClick.RemoveListener(Continuar);
            botonContinuar.onClick.AddListener(Continuar);
        }
        if (botonNuevaPartida != null)
        {
            botonNuevaPartida.onClick.RemoveListener(PulsarNuevaPartida);
            botonNuevaPartida.onClick.AddListener(PulsarNuevaPartida);
        }
        if (botonSalir != null)
        {
            botonSalir.onClick.RemoveListener(Salir);
            botonSalir.onClick.AddListener(Salir);
        }
        if (botonConfirmarSi != null)
        {
            botonConfirmarSi.onClick.RemoveListener(NuevaPartidaConfirmada);
            botonConfirmarSi.onClick.AddListener(NuevaPartidaConfirmada);
        }
        if (botonConfirmarNo != null)
        {
            botonConfirmarNo.onClick.RemoveListener(CancelarConfirmacion);
            botonConfirmarNo.onClick.AddListener(CancelarConfirmacion);
        }
    }

    void SeleccionarPrimerBoton()
    {
        if (EventSystem.current == null || primerBotonSeleccionado == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(primerBotonSeleccionado);
    }

    // ── Acciones ─────────────────────────────────────────────

    /// <summary>
    /// Carga la escena guardada. GameManager01 (que vive en las escenas de
    /// juego, no aquí) hará LoadGame() en su Awake y colocará a Romerito en
    /// su Cihuacalli al recibir la intención Continuar.
    /// </summary>
    public void Continuar()
    {
        PlayerData datos = SaveSystem.LeerPartida();

        if (datos == null || !SesionMictlan.EscenaValida(datos.currentScene))
        {
            Debug.LogWarning("[MainMenu] No hay partida cargable.");
            return;
        }

        SesionMictlan.intencion = IntencionArranque.Continuar;
        SceneManager.LoadScene(datos.currentScene);
    }

    /// <summary>
    /// Si hay partida pide confirmación; si no, arranca directamente.
    /// Un jugador no debería poder borrar veinte horas de Mictlán con un
    /// solo botón mal pulsado en el Deck.
    /// </summary>
    public void PulsarNuevaPartida()
    {
        if (hayPartida && panelConfirmacion != null)
        {
            panelConfirmacion.SetActive(true);

            if (EventSystem.current != null && botonConfirmarNo != null)
            {
                // Arrancamos sobre "No" a propósito: pulsar A por inercia
                // no debe destruir el save.
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(botonConfirmarNo.gameObject);
            }
            return;
        }

        NuevaPartidaConfirmada();
    }

    public void NuevaPartidaConfirmada()
    {
        if (!SesionMictlan.EscenaValida(escenaNuevaPartida))
        {
            Debug.LogError("[MainMenu] '" + escenaNuevaPartida +
                           "' no está en Build Settings. Nueva Partida cancelada.");
            return;
        }

        SaveSystem.Borrar();
        SesionMictlan.intencion = IntencionArranque.NuevaPartida;
        SceneManager.LoadScene(escenaNuevaPartida);
    }

    public void CancelarConfirmacion()
    {
        if (panelConfirmacion != null)
            panelConfirmacion.SetActive(false);
        SeleccionarPrimerBoton();
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Utilidades ───────────────────────────────────────────

    /// <summary>
    /// Traduce el nombre técnico de la escena al nombre nahua del nivel.
    /// Amplía este switch conforme añadas niveles.
    /// </summary>
    string NombreLegibleDeEscena(string escena)
    {
        switch (escena)
        {
            case "Chicunamictlan_001": return "Chiconauhmictlan";
            case "Atlein_01":          return "Itzcuintlan Atlein";
            default:                   return escena;
        }
    }
}
