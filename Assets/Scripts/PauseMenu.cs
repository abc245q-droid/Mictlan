using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ============================================================
//  PauseMenu — Pausa del juego con soporte Xbox y teclado
// ============================================================
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. Crea un GameObject vacío "PauseMenu" en la escena.
//  2. Añade este script.
//  3. Crea un Panel de UI (Canvas → Panel) llamado "PausePanel":
//       • Un Text/TMP con "PAUSA"
//       • Un botón "Reanudar"   → llama a Reanudar()
//       • Un botón "Salir"      → llama a SalirAlMenu() (opcional por ahora)
//  4. Asigna en el Inspector:
//       • pausePanel  → el Panel de UI
//       • (Opcional) timeScaleOnPause → 0 para pausa total
//
//  CONTROLES:
//  • Teclado:  Escape
//  • Xbox:     botón Start (joystick button 7)
//  ─────────────────────────────────────────────────────────────
//
//  INTEGRACIÓN AUTOMÁTICA:
//  • No pausa si hay un diálogo activo (DialogueManager.IsActive).
//  • Congela a Romerito desactivando sus componentes de movimiento
//    y combate (sin usar Time.timeScale = 0 para que las animaciones
//    de UI y el diálogo no se rompan).
//  • Compatible con el sistema de Favores (FavorManager).
//  ─────────────────────────────────────────────────────────────

public class PauseMenu : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────

    [Header("UI")]
    [Tooltip("Panel de UI que se muestra al pausar. Asígnalo desde el Inspector.")]
    public GameObject pausePanel;

    [Header("Configuración")]
    [Tooltip("Si es true, Time.timeScale se pone a 0 al pausar. " +
             "Si es false, solo se congelan los componentes de Romerito " +
             "(mejor opción si usas animaciones de UI o diálogos).")]
    public bool usarTimeScale = false;

    [Tooltip("Escena del menú principal para el botón 'Volver al Inicio'.")]
    public string escenaMenuPrincipal = "MenuPrincipal";

    [Header("Guardado")]
    [Tooltip("true  → 'Guardar' registra la posición EXACTA de Romerito. " +
             "Cómodo para sesiones cortas en Steam Deck, pero permite guardar " +
             "en caída libre o sobre pinchos.\n" +
             "false → registra su último Cihuacalli (clásico de Metroidvania; " +
             "es lo que ya hace la muerte). Si aún no ha tocado ninguno, se " +
             "usa la posición actual automáticamente.")]
    public bool guardarEnPosicionActual = false;

    [Header("Navegación con mando")]
    [Tooltip("Botón que queda seleccionado al abrir la pausa. Sin esto el " +
             "mando no puede navegar el panel.")]
    public GameObject primerBotonSeleccionado;

    [Header("Feedback (opcional)")]
    [Tooltip("Texto que confirma el guardado. Se muestra 2 s.")]
    public GameObject avisoGuardado;

    [Header("Guardado seguro (sólo si guardarEnPosicionActual = true)")]
    [Tooltip("Botón 'Guardar'. Se deshabilita mientras Romerito esté en el aire, " +
             "para que no pueda dejar la partida guardada en caída libre o sobre " +
             "pinchos. Si lo dejas vacío, no se bloquea nada.")]
    public Button botonGuardar;

    [Tooltip("Texto tipo 'Busca suelo firme para guardar'. Se muestra sólo " +
             "mientras el guardado está bloqueado. Opcional.")]
    public GameObject avisoSueloRequerido;

    [Tooltip("Botón al que salta la selección si el botón designado como primero " +
             "está deshabilitado (normalmente 'Reanudar'). Sin esto, pausar en el " +
             "aire con 'Guardar' como primer botón deja el mando sin foco.")]
    public GameObject botonRespaldoSeleccion;

    // ── Estado interno ───────────────────────────────────────

    private bool pausado = false;

    // Referencias a componentes de Romerito (cacheadas en Start)
    private RomeritoMovement movimiento;
    private RomeritoCombat combate;
    private Rigidbody2D rb;

    // ── Constantes de input ──────────────────────────────────

    // Xbox Start = joystick button 7
    // (en algunos drivers puede ser joystick button 6 — ajustar si es necesario)
    private const KeyCode XBOX_START = KeyCode.JoystickButton7;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        CachearRomerito();

        // El panel empieza oculto
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (avisoSueloRequerido != null)
            avisoSueloRequerido.SetActive(false);
    }

    /// <summary>
    /// Busca a Romerito y guarda sus componentes. Se llama en Start y otra vez
    /// al pausar si las referencias vinieron nulas.
    ///
    /// Romerito es DontDestroyOnLoad y este PauseMenu vive en la escena, así que
    /// el orden normal (Awake de Romerito → Start del PauseMenu) siempre funciona.
    /// Pero si alguna vez falla — una escena cargada sin Romerito, un cambio en
    /// el orden de ejecución — el cacheo nulo era silencioso y dejaba el bloqueo
    /// de guardado sin funcionar. Ahora se reintenta.
    /// </summary>
    private void CachearRomerito()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        movimiento = player.GetComponent<RomeritoMovement>();
        combate = player.GetComponent<RomeritoCombat>();
        rb = player.GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Detectar input de pausa (Escape O botón Start de Xbox)
        bool presionadoPausa = Input.GetKeyDown(KeyCode.Escape) ||
                               Input.GetKeyDown(XBOX_START);

        if (!presionadoPausa) return;

        // No pausar si hay un diálogo activo
        if (DialogueManager.IsActive) return;

        // Alternar pausa
        if (pausado)
            Reanudar();
        else
            Pausar();
    }

    // ── API Pública ──────────────────────────────────────────

    /// <summary>
    /// Pausa el juego. Llamado automáticamente por Update
    /// o puede llamarse desde un evento de UI.
    /// </summary>
    public void Pausar()
    {
        pausado = true;

        // Mostrar panel
        if (pausePanel != null)
            pausePanel.SetActive(true);

        if (avisoGuardado != null)
            avisoGuardado.SetActive(false);

        if (movimiento == null) CachearRomerito();

        // Se evalúa AQUÍ, no en Update: durante la pausa Romerito no se mueve,
        // así que su estado de suelo es el del instante en que se pausó y no
        // hace falta recalcularlo. Además, con usarTimeScale = true el
        // FixedUpdate está parado y isGrounded ni siquiera se actualizaría.
        //
        // Importante que vaya antes de dar el foco: si 'Guardar' quedó
        // deshabilitado y era el primer botón, la selección tiene que saberlo.
        ActualizarDisponibilidadDeGuardado();

        // Dar el foco al mando. SetSelectedGameObject(null) primero: si no,
        // el EventSystem puede ignorar la nueva selección cuando el objeto
        // anterior sigue marcado como seleccionado.
        DarFocoAlMando();

        // Congelar juego
        if (usarTimeScale)
        {
            Time.timeScale = 0f;
        }
        else
        {
            // Congelamos componentes de Romerito sin tocar Time.timeScale
            CongelarRomerito(true);
        }

        Debug.Log("[PauseMenu] Juego pausado.");
    }

    /// <summary>
    /// Reanuda el juego. Llamado por botón UI o automáticamente.
    /// </summary>
    public void Reanudar()
    {
        pausado = false;

        // Ocultar panel
        if (pausePanel != null)
            pausePanel.SetActive(false);

        // Descongelar juego
        if (usarTimeScale)
        {
            Time.timeScale = 1f;
        }
        else
        {
            CongelarRomerito(false);
        }

        Debug.Log("[PauseMenu] Juego reanudado.");
    }

    /// <summary>
    /// Escribe la partida en disco sin salir del juego.
    /// Conectar al botón "Guardar" del panel de pausa.
    /// </summary>
    public void Guardar()
    {
        if (GameManager01.instance == null)
        {
            Debug.LogError("[PauseMenu] No hay GameManager01: no se puede guardar.");
            return;
        }

        // Segunda barrera. El botón ya está deshabilitado, pero este método es
        // público y puede llamarse desde otro onClick o desde código.
        if (!PuedeGuardarAqui())
        {
            Debug.Log("[PauseMenu] Guardado bloqueado: Romerito no está en suelo.");
            return;
        }

        GameManager01.instance.GuardarDesdeMenuPausa(guardarEnPosicionActual);

        if (avisoGuardado != null)
            StartCoroutine(MostrarAvisoGuardado());
    }

    /// <summary>
    /// Guarda y devuelve al jugador a la pantalla de inicio.
    /// Conectar al botón "Guardar y Volver al Inicio".
    /// </summary>
    public void GuardarYVolverAlInicio()
    {
        if (GameManager01.instance != null)
        {
            // Este botón NO se bloquea nunca. Negarle la salida a alguien que
            // pausó en mitad de un salto sería absurdo: tendría que reanudar,
            // caer y volver a pausar sólo para poder cerrar el juego.
            //
            // En su lugar degradamos: si no se puede guardar donde está, se
            // guarda en el último Cihuacalli. Pierde el trayecto desde el
            // checkpoint, pero conserva habilidades, cacao y mapa — y sale.
            bool enPosicion = guardarEnPosicionActual && PuedeGuardarAqui();

            if (guardarEnPosicionActual && !enPosicion)
                Debug.Log("[PauseMenu] En el aire: se guarda en el último " +
                          "Cihuacalli en vez de la posición actual.");

            GameManager01.instance.GuardarDesdeMenuPausa(enPosicion);
        }

        VolverAlInicio();
    }

    /// <summary>
    /// Vuelve al menú SIN guardar. Conectar a un botón "Salir sin guardar"
    /// si lo quieres, o dejarlo sólo como API interna.
    /// </summary>
    public void VolverAlInicio()
    {
        pausado = false;

        // Descongelamos ANTES de desmontar: si volviésemos al menú con
        // Romerito congelado y timeScale a 0, la corrutina de transición
        // seguiría funcionando (yield return null es por frames) pero el
        // menú arrancaría con las animaciones muertas.
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        StartCoroutine(SesionMictlan.RutinaVolverAlMenu(escenaMenuPrincipal));
    }

    /// <summary>Alias antiguo. Se mantiene por si alguna escena lo tiene
    /// cableado en un onClick del Inspector.</summary>
    public void SalirAlMenu()
    {
        VolverAlInicio();
    }

    // ── Guardado seguro ──────────────────────────────────────

    /// <summary>
    /// ¿Es seguro escribir la posición actual de Romerito en el save?
    ///
    /// En modo Cihuacalli (guardarEnPosicionActual = false) siempre lo es:
    /// la posición que se escribe es la del último checkpoint, que por
    /// definición es suelo firme. El bloqueo sólo tiene sentido en el modo
    /// "guardar donde estoy".
    /// </summary>
    public bool PuedeGuardarAqui()
    {
        if (!guardarEnPosicionActual) return true;
        if (movimiento == null) return true;   // sin referencia, no bloqueamos
        return movimiento.isGrounded;
    }

    private void ActualizarDisponibilidadDeGuardado()
    {
        bool puede = PuedeGuardarAqui();

        if (botonGuardar != null)
            botonGuardar.interactable = puede;

        if (avisoSueloRequerido != null)
            avisoSueloRequerido.SetActive(!puede);
    }

    /// <summary>
    /// Selecciona el primer botón para que el mando pueda navegar, con
    /// respaldo si ese botón quedó deshabilitado.
    ///
    /// Un Selectable no interactuable NO puede recibir el foco: el EventSystem
    /// acepta la llamada pero el botón no responde ni a Submit ni a navegación,
    /// y el panel se queda muerto para el mando.
    /// </summary>
    private void DarFocoAlMando()
    {
        if (EventSystem.current == null) return;

        GameObject destino = primerBotonSeleccionado;

        Selectable sel = destino != null ? destino.GetComponent<Selectable>() : null;
        if (sel != null && !sel.IsInteractable())
            destino = botonRespaldoSeleccion;

        if (destino == null) return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(destino);
    }

    private IEnumerator MostrarAvisoGuardado()
    {
        avisoGuardado.SetActive(true);
        // Realtime: el panel de pausa puede estar corriendo con timeScale = 0.
        yield return new WaitForSecondsRealtime(2f);
        if (avisoGuardado != null)
            avisoGuardado.SetActive(false);
    }

    // ── Utilidades ───────────────────────────────────────────

    /// <summary>
    /// Activa/desactiva los componentes de Romerito para congelarlo
    /// sin necesidad de Time.timeScale = 0.
    /// </summary>
    private void CongelarRomerito(bool congelar)
    {
        if (movimiento != null)
            movimiento.enabled = !congelar;

        if (combate != null)
            combate.enabled = !congelar;

        // Si hay Rigidbody, frenarlo en seco al pausar
        if (rb != null && congelar)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // ── Propiedad pública ─────────────────────────────────────

    /// <summary>
    /// Estado actual de la pausa. Útil para que otros scripts
    /// (FavorManager, WaveSpawner, etc.) comprueben si deben detenerse.
    /// </summary>
    public bool EstaPausado => pausado;
}