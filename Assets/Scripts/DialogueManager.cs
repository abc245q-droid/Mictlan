using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

// ============================================================
//  DialogueManager — Controlador del sistema de Lore
// ============================================================
//
//  Singleton. Reproduce conversaciones (Conversation) con:
//    • Efecto máquina de escribir (configurable / desactivable)
//    • Tecla / clic para continuar
//    • Árboles de decisión (opciones de respuesta)
//    • Congelado automático de Romerito (sin tocar RomeritoMovement)
//    • Callback onComplete para encadenar eventos al terminar
//
//  Uso desde cualquier script:
//      DialogueManager.Instance.StartConversation(miConversacion);
//      DialogueManager.Instance.StartConversation(convo, () => AbrirPaso());
//
//  Flag global para que otros sistemas se silencien si hace falta:
//      if (DialogueManager.IsActive) return;
//
//  --- SETUP DE UI (en un Canvas) ---
//    dialoguePanel   → Panel raíz (se activa/desactiva)
//    speakerText     → TMP para el nombre del hablante
//    bodyText        → TMP para el cuerpo del diálogo
//    continueHint    → (opcional) icono/flecha "pulsa para continuar"
//    choicesPanel    → (opcional) contenedor de las opciones
//    choiceButtons   → (opcional) botones pre-colocados para las respuestas
//
// ============================================================

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    /// <summary>True mientras hay una conversación en curso.</summary>
    public static bool IsActive { get; private set; }

    [Header("── Referencias de UI ──")]
    public GameObject dialoguePanel;
    public TMP_Text speakerText;
    public TMP_Text bodyText;
    [Tooltip("Opcional: flecha/indicador de 'pulsa para continuar'.")]
    public GameObject continueHint;

    [Header("── Opciones (árbol de decisión) ──")]
    [Tooltip("Opcional: contenedor que agrupa los botones de respuesta.")]
    public GameObject choicesPanel;
    [Tooltip("Opcional: botones pre-colocados. Se activan según el número de opciones.")]
    public Button[] choiceButtons;

    [Header("── Retratos (plantilla de personajes) ──")]
    [Tooltip("Opcional: Image donde aparece el retrato del personaje del lado izquierdo.")]
    public Image retratoIzquierda;
    [Tooltip("Opcional: Image donde aparece el retrato del personaje del lado derecho.")]
    public Image retratoDerecha;
    [Tooltip("Tinte del retrato del personaje que NO está hablando (resalta al activo).")]
    public Color colorRetratoInactivo = new Color(0.45f, 0.45f, 0.5f, 1f);

    [Header("── Máquina de escribir ──")]
    [Tooltip("Caracteres por segundo. 0 = mostrar la línea completa al instante.")]
    public float velocidadTexto = 45f;

    [Header("── Input ──")]
    public KeyCode teclaAvanzar = KeyCode.E;
    [Tooltip("Además de la tecla, también se puede avanzar con Espacio, Enter y clic izquierdo.")]
    public bool permitirEspacioEnterClic = true;

    [Header("── Acotaciones escénicas ──")]
    public Color colorAcotacion = new Color(0.6f, 0.65f, 0.7f, 1f);

    // ── Eventos públicos ─────────────────────────────────────
    public event System.Action OnDialogueStarted;
    public event System.Action<Conversation> OnDialogueEnded;

    // ── Estado interno ───────────────────────────────────────
    private Conversation convoActual;
    private DialogueNode nodoActual;
    private int indiceLinea;

    private bool escribiendo;
    private string lineaCompleta = "";
    private int charsVisibles;
    private float charTimer;

    private bool mostrandoOpciones;
    private bool bloquearInputUnFrame;   // evita que el mismo clic que inicia el diálogo lo avance

    private System.Action onComplete;

    // ── Congelado del jugador (sin tocar su script) ──────────
    private RomeritoMovement playerMovement;
    private RomeritoCombat playerCombat;
    private Rigidbody2D playerRb;
    private bool refsJugadorCacheadas;

    // ── Unity ────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (choicesPanel != null) choicesPanel.SetActive(false);
        OcultarRetratos();   // ← AÑADE ESTA LÍNEA
        IsActive = false;
    }

    void Update()
    {
        if (!IsActive) return;

        if (bloquearInputUnFrame) { bloquearInputUnFrame = false; return; }

        // --- Máquina de escribir ---
        if (escribiendo)
        {
            AvanzarTypewriter();

            // Permitir completar la línea de golpe al pulsar.
            if (AvancePresionado())
                CompletarLinea();

            return;
        }

        // --- Mostrando opciones: la selección se maneja por clic o teclas numéricas ---
        if (mostrandoOpciones)
        {
            ChequearTeclasNumericas();
            return;
        }

        // --- Línea terminada, esperando avance ---
        if (AvancePresionado())
            SiguienteLinea();
    }

    // ── API pública ──────────────────────────────────────────
    public void StartConversation(Conversation conversation, System.Action alTerminar = null)
    {
        if (IsActive)
        {
            Debug.LogWarning("[DialogueManager] Ya hay una conversación en curso.");
            return;
        }
        if (conversation == null || conversation.nodes == null || conversation.nodes.Length == 0)
        {
            Debug.LogError("[DialogueManager] Conversación vacía o nula.");
            return;
        }

        convoActual = conversation;
        onComplete = alTerminar;
        IsActive = true;
        bloquearInputUnFrame = true;

        CongelarJugador(true);

        if (dialoguePanel != null) dialoguePanel.SetActive(true);
        OcultarOpciones();
        OcultarRetratos();

        OnDialogueStarted?.Invoke();

        IrANodo(0);
    }

    // ── Flujo de nodos ───────────────────────────────────────
    private void IrANodo(int indice)
    {
        if (indice < 0 || indice >= convoActual.nodes.Length)
        {
            TerminarConversacion();
            return;
        }

        nodoActual = convoActual.nodes[indice];
        indiceLinea = -1;
        SiguienteLinea();
    }

    private void SiguienteLinea()
    {
        indiceLinea++;

        // ¿Se acabaron las líneas del nodo?
        if (nodoActual.lines == null || indiceLinea >= nodoActual.lines.Length)
        {
            // ¿Hay opciones? → mostrar árbol de decisión.
            if (nodoActual.choices != null && nodoActual.choices.Length > 0)
            {
                MostrarOpciones();
            }
            else
            {
                // Nodo lineal: saltar al siguiente o terminar.
                if (nodoActual.nextNode >= 0)
                    IrANodo(nodoActual.nextNode);
                else
                    TerminarConversacion();
            }
            return;
        }

        MostrarLinea(nodoActual.lines[indiceLinea]);
    }

    private void MostrarLinea(DialogueLine linea)
    {
        // Resolver nombre/color: el Personaje (plantilla) tiene prioridad
        // sobre los campos sueltos speaker/speakerColor.
        string nombre = linea.speaker;
        Color colorNombre = linea.speakerColor;
        if (linea.personaje != null)
        {
            nombre = linea.personaje.nombreEnPantalla;
            colorNombre = linea.personaje.colorNombre;
        }

        // Nombre del hablante (oculto en acotaciones)
        if (speakerText != null)
        {
            if (linea.esAcotacion || string.IsNullOrEmpty(nombre))
            {
                speakerText.text = "";
                speakerText.gameObject.SetActive(false);
            }
            else
            {
                speakerText.gameObject.SetActive(true);
                speakerText.text = nombre;
                speakerText.color = colorNombre;
            }
        }

        // Retrato (plantilla de personajes)
        AplicarRetrato(linea);

        // Cuerpo
        if (bodyText != null)
        {
            bodyText.fontStyle = linea.esAcotacion ? FontStyles.Italic : FontStyles.Normal;
            bodyText.color = linea.esAcotacion ? colorAcotacion : Color.white;
        }

        // Arrancar máquina de escribir
        lineaCompleta = linea.text;
        if (velocidadTexto <= 0f)
        {
            // Instantáneo
            if (bodyText != null) bodyText.text = lineaCompleta;
            escribiendo = false;
            MostrarHint(true);
        }
        else
        {
            charsVisibles = 0;
            charTimer = 0f;
            escribiendo = true;
            MostrarHint(false);
            if (bodyText != null) bodyText.text = "";
        }
    }

    private void AvanzarTypewriter()
    {
        charTimer -= Time.unscaledDeltaTime;
        while (charTimer <= 0f && charsVisibles < lineaCompleta.Length)
        {
            charsVisibles++;
            charTimer += 1f / velocidadTexto;
        }

        if (bodyText != null)
            bodyText.text = lineaCompleta.Substring(0, charsVisibles);

        if (charsVisibles >= lineaCompleta.Length)
        {
            escribiendo = false;
            MostrarHint(true);
        }
    }

    private void CompletarLinea()
    {
        charsVisibles = lineaCompleta.Length;
        if (bodyText != null) bodyText.text = lineaCompleta;
        escribiendo = false;
        MostrarHint(true);
    }

    // ── Opciones / árbol de decisión ─────────────────────────
    private void MostrarOpciones()
    {
        mostrandoOpciones = true;
        MostrarHint(false);

        if (choicesPanel != null) choicesPanel.SetActive(true);
        if (choiceButtons == null) return;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null) continue;

            if (i < nodoActual.choices.Length)
            {
                int indice = i; // captura para el listener
                choiceButtons[i].gameObject.SetActive(true);

                TMP_Text label = choiceButtons[i].GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = nodoActual.choices[i].text;

                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => SeleccionarOpcion(indice));
            }
            else
            {
                choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private void ChequearTeclasNumericas()
    {
        // Permite elegir con 1..9 además del clic.
        for (int i = 0; i < nodoActual.choices.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
            {
                SeleccionarOpcion(i);
                return;
            }
        }
    }

    public void SeleccionarOpcion(int indice)
    {
        if (!mostrandoOpciones) return;
        if (indice < 0 || indice >= nodoActual.choices.Length) return;

        int destino = nodoActual.choices[indice].targetNode;
        OcultarOpciones();

        if (destino >= 0) IrANodo(destino);
        else TerminarConversacion();
    }

    private void OcultarOpciones()
    {
        mostrandoOpciones = false;
        if (choicesPanel != null) choicesPanel.SetActive(false);
        if (choiceButtons == null) return;
        foreach (var b in choiceButtons)
            if (b != null) b.gameObject.SetActive(false);
    }

    // ── Fin ──────────────────────────────────────────────────
    private void TerminarConversacion()
    {
        IsActive = false;
        escribiendo = false;
        mostrandoOpciones = false;

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        OcultarOpciones();
        OcultarRetratos();

        CongelarJugador(false);

        Conversation terminada = convoActual;
        convoActual = null;
        nodoActual = null;

        // Primero el callback puntual, luego el evento global.
        System.Action cb = onComplete;
        onComplete = null;
        cb?.Invoke();

        OnDialogueEnded?.Invoke(terminada);
    }

    // ── Utilidades ───────────────────────────────────────────
    private bool AvancePresionado()
    {
        if (Input.GetKeyDown(teclaAvanzar)) return true;
        if (permitirEspacioEnterClic)
        {
            if (Input.GetKeyDown(KeyCode.Space)) return true;
            if (Input.GetKeyDown(KeyCode.Return)) return true;
            if (Input.GetKeyDown(KeyCode.KeypadEnter)) return true;
            if (Input.GetMouseButtonDown(0)) return true;
        }
        return false;
    }

    private void MostrarHint(bool visible)
    {
        if (continueHint != null) continueHint.SetActive(visible);
    }

    // ── Retratos ─────────────────────────────────────────────
    private void AplicarRetrato(DialogueLine linea)
    {
        if (linea.limpiarRetratos)
        {
            OcultarRetratos();
            return;
        }

        // Sin personaje (fallback o acotación): no tocamos los retratos,
        // así el dios permanece en pantalla mientras corre la narración.
        if (linea.personaje == null) return;

        Sprite sp = linea.personaje.GetRetrato(linea.expresion);
        Color activo = linea.personaje.tinte;

        if (linea.personaje.lado == LadoRetrato.Izquierda)
        {
            SetRetrato(retratoIzquierda, sp, activo);
            AtenuarSiTiene(retratoDerecha);
        }
        else
        {
            SetRetrato(retratoDerecha, sp, activo);
            AtenuarSiTiene(retratoIzquierda);
        }
    }

    private void SetRetrato(Image img, Sprite sp, Color tinte)
    {
        if (img == null) return;
        if (sp == null) { img.gameObject.SetActive(false); return; }
        img.gameObject.SetActive(true);
        img.sprite = sp;
        img.color = tinte;
        img.preserveAspect = true;
    }

    private void AtenuarSiTiene(Image img)
    {
        if (img == null || !img.gameObject.activeSelf || img.sprite == null) return;
        img.color = colorRetratoInactivo;
    }

    private void OcultarRetratos()
    {
        SetRetrato(retratoIzquierda, null, Color.white);
        SetRetrato(retratoDerecha, null, Color.white);
    }

    private void CongelarJugador(bool congelar)
    {
        if (!refsJugadorCacheadas)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerMovement = p.GetComponent<RomeritoMovement>();
                playerCombat = p.GetComponent<RomeritoCombat>();
                playerRb = p.GetComponent<Rigidbody2D>();
            }
            refsJugadorCacheadas = true;
        }

        if (congelar && playerRb != null)
            playerRb.linearVelocity = Vector2.zero;

        if (playerMovement != null) playerMovement.enabled = !congelar;
        if (playerCombat != null) playerCombat.enabled = !congelar;
    }
}
