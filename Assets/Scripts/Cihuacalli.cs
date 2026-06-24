using UnityEngine;
using UnityEngine.Events;

// ============================================================
//  Cihuacalli — Ancla de Tonalli (checkpoint persistente)
// ============================================================
//
//  Reemplaza al antiguo Checkpoint.cs.
//
//  MODELO:
//    • Solo UN Cihuacalli está encendido a la vez: el checkpoint
//      actual. Al activar otro, el anterior se apaga solo.
//    • Disponible desde el primer contacto (sin permisos).
//    • Interactuable con botón B (Xbox JoystickButton1).
//    • Algunos pueden tener un diálogo de LORE que se reproduce la
//      PRIMERA vez que se interactúa con ellos (en el primero del
//      juego, el de Chantico). Es narrativo, no bloquea nada.
//    • El estado (cuál es el activo) y el lore ya visto persisten en
//      PlayerData, así sobreviven muertes, recargas y cambios de escena.
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//   1. GameObject con SpriteRenderer + Collider2D (Is Trigger: ON)
//      cubriendo la zona de interacción.
//   2. cihuacalliID ÚNICO (ej. "cihuacalli_atlein_01"). NO repetir.
//   3. spriteApagado / spriteEncendido.
//   4. (Opcional) promptInteraccion: hijo con el icono "Pulsa B".
//   5. SOLO en el de Chantico: asigna dialogoLore (créalo con el menú
//      Mictlan/Diálogos/Crear Diálogo de Chantico) y un dialogoLoreID
//      único (ej. "lore_chantico").
//  ─────────────────────────────────────────────────────────────
//
//  Requiere en la escena: GameManager01 y DialogueManager.

[RequireComponent(typeof(Collider2D))]
public class Cihuacalli : MonoBehaviour
{
    // Evento global: cuando CUALQUIER Cihuacalli cambia de estado,
    // todos refrescan su sprite (así el anterior se apaga al instante).
    public static event System.Action OnEstadoCambiado;

    [Header("Identidad")]
    [Tooltip("ID único para el guardado. Ej: 'cihuacalli_atlein_01'. NO repetir entre escenas.")]
    public string cihuacalliID = "cihuacalli_sin_id";

    [Header("Lore (opcional)")]
    [Tooltip("Diálogo que se reproduce la PRIMERA vez que se interactúa con este Cihuacalli. " +
             "En el primero del juego, asigna aquí el diálogo de Chantico. Déjalo vacío para los demás.")]
    public Conversation dialogoLore;

    [Tooltip("ID para recordar que el lore ya se reprodujo (se guarda en collectedItems). " +
             "Ej: 'lore_chantico'. Necesario solo si 'dialogoLore' está asignado.")]
    public string dialogoLoreID = "";

    [Header("Visuales")]
    [Tooltip("Sprite frío / apagado.")]
    public Sprite spriteApagado;
    [Tooltip("Sprite con fuego (solo el checkpoint actual lo muestra).")]
    public Sprite spriteEncendido;
    [Tooltip("Objeto del prompt 'Pulsa B' (hijo, opcional). Se muestra cuando Romerito está en rango.")]
    public GameObject promptInteraccion;

    [Header("Fuego (opcional)")]
    [Tooltip("Capa de llama con CihuacalliGlow. Da el fade-in, latido y fade-out. " +
             "Déjalo vacío si solo quieres el cambio de sprite, sin brillo.")]
    public CihuacalliGlow glow;

    [Header("Input")]
    [Tooltip("Botón de interacción. Por defecto B de Xbox = JoystickButton1.")]
    public KeyCode botonInteraccion = KeyCode.JoystickButton1;
    [Tooltip("Tecla alterna de teclado para probar en el editor.")]
    public KeyCode teclaAlterna = KeyCode.B;

    [Header("Eventos (opcional)")]
    [Tooltip("Se dispara al activar el Cihuacalli (FX de fuego, sonido, partículas).")]
    public UnityEvent OnActivado;

    // ── Estado interno ───────────────────────────────────────
    private SpriteRenderer sr;
    private bool jugadorEnRango = false;
    private bool esActivo = false;          // ¿es el checkpoint actual? (visual)
    private bool dialogoEnCurso = false;

    // ── Unity ────────────────────────────────────────────────
    void Start()
    {
        // El swap de sprite (piedra/llama) y el glow deben operar sobre el
        // MISMO renderer. Si hay glow asignado, usamos el suyo.
        sr = (glow != null) ? glow.Renderer : GetComponent<SpriteRenderer>();
        RefrescarEstado(true);   // estado inicial sin fade (al cargar escena)
        OcultarPrompt();
    }

    void OnEnable() { OnEstadoCambiado += RefrescarEstadoAnimado; }
    void OnDisable() { OnEstadoCambiado -= RefrescarEstadoAnimado; }

    void Update()
    {
        if (!jugadorEnRango) return;

        // Durante un diálogo, nada de prompts ni inputs.
        if (DialogueManager.IsActive) { OcultarPrompt(); return; }

        MostrarPrompt();

        if (Input.GetKeyDown(botonInteraccion) || Input.GetKeyDown(teclaAlterna))
            Interactuar();
    }

    // ── Interacción (botón B) ────────────────────────────────
    private void Interactuar()
    {
        if (dialogoEnCurso) return;

        // ¿Toca reproducir el lore por primera vez?
        if (dialogoLore != null && !LoreYaVisto())
        {
            ReproducirLore();
            return;     // al terminar el diálogo se llama a Activar()
        }

        Activar();
    }

    private void ReproducirLore()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[Cihuacalli] No hay DialogueManager en la escena. Activo sin lore.");
            Activar();
            return;
        }

        OcultarPrompt();
        dialogoEnCurso = true;

        DialogueManager.Instance.StartConversation(dialogoLore, () =>
        {
            dialogoEnCurso = false;
            MarcarLoreVisto();
            Activar();
        });

        Debug.Log("[Cihuacalli] Diálogo de lore iniciado en '" + cihuacalliID + "'.");
    }

    // ── Activación como checkpoint ───────────────────────────
    private void Activar()
    {
        // Pasa a ser el ÚNICO encendido + fija el respawn + guarda.
        if (GameManager01.instance != null)
            GameManager01.instance.ActivarCihuacalli(cihuacalliID, transform.position);

        // Notifica a todos para que el anterior se apague y este se encienda.
        OnEstadoCambiado?.Invoke();

        Debug.Log("[Cihuacalli] '" + cihuacalliID + "' es ahora el checkpoint activo.");
        OnActivado?.Invoke();
    }

    // ── Refresco visual según el ID activo ───────────────────
    private void RefrescarEstadoAnimado() => RefrescarEstado(false);

    private void RefrescarEstado(bool instantaneo)
    {
        esActivo = GameManager01.instance != null &&
                   GameManager01.instance.EsCihuacalliActivo(cihuacalliID);
        AplicarSprite();
        if (glow != null) glow.SetEncendido(esActivo, instantaneo);
    }

    // ── Persistencia del lore (one-shot vía collectedItems) ──
    private bool LoreYaVisto()
    {
        return !string.IsNullOrEmpty(dialogoLoreID) &&
               GameManager01.instance != null &&
               GameManager01.instance.currentData != null &&
               GameManager01.instance.currentData.collectedItems.Contains(dialogoLoreID);
    }

    private void MarcarLoreVisto()
    {
        if (string.IsNullOrEmpty(dialogoLoreID)) return;
        if (GameManager01.instance == null || GameManager01.instance.currentData == null) return;
        if (!GameManager01.instance.currentData.collectedItems.Contains(dialogoLoreID))
            GameManager01.instance.currentData.collectedItems.Add(dialogoLoreID);
        // El guardado en disco ocurre en Activar() -> ActivarCihuacalli -> UpdateCheckPoint.
    }

    // ── Rango del jugador ────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        jugadorEnRango = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        jugadorEnRango = false;
        OcultarPrompt();
    }

    // ── Visuales ─────────────────────────────────────────────
    private void AplicarSprite()
    {
        if (sr == null) return;
        Sprite objetivo = esActivo ? spriteEncendido : spriteApagado;
        if (objetivo != null) sr.sprite = objetivo;
    }

    private void MostrarPrompt()
    {
        if (promptInteraccion != null && !promptInteraccion.activeSelf)
            promptInteraccion.SetActive(true);
    }

    private void OcultarPrompt()
    {
        if (promptInteraccion != null && promptInteraccion.activeSelf)
            promptInteraccion.SetActive(false);
    }

    // Visualiza el área de interacción en el editor.
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.35f);
        Collider2D col = GetComponent<Collider2D>();
        if (col is BoxCollider2D box)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        else if (col is CircleCollider2D circ)
            Gizmos.DrawWireSphere(transform.position + (Vector3)circ.offset, circ.radius);
    }
}