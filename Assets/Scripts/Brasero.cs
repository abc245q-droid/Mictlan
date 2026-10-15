using UnityEngine;
using UnityEngine.Events;

// ============================================================
//  Brasero de Huehuetéotl — fuego sagrado interactuable
// ============================================================
//
//  El jugador lo enciende con B. Cambia de sprite apagado→encendido
//  con el glow, y UNA VEZ ENCENDIDO SE QUEDA ASÍ PARA SIEMPRE
//  (persiste en PlayerData.braserosEncendidos).
//
//  Pertenece opcionalmente a un BraseroGrupo: cuando TODOS los del
//  grupo están encendidos, el grupo dispara su evento de completado
//  (abrir puerta, cutscene, etc.).
//
//  BLOQUEO (SalaSellada):
//  Mientras 'bloqueado' sea true no se puede encender: Tlacua no
//  trabaja con custodios cerca. Se muestra promptBloqueado en lugar
//  del prompt normal y B dispara OnIntentoBloqueado.
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//   1. GameObject con SpriteRenderer + Collider2D (Is Trigger: ON).
//   2. braseroID ÚNICO (ej. "brasero_chicunamictlan_01"). NO repetir.
//   3. spriteApagado / spriteEncendido (assets Sprite).
//   4. (Opcional) glow → CihuacalliGlow sobre el renderer del fuego
//      (material Mictlan/SpriteGlowHDR). apagarConAlpha = FALSE.
//   5. (Opcional) promptInteraccion → hijo con el icono "B".
//   6. Si forma parte de un puzzle, colócalo como HIJO de un
//      BraseroGrupo (se auto-registra), o arrástralo a su lista.
//  ─────────────────────────────────────────────────────────────

[RequireComponent(typeof(Collider2D))]
public class Brasero : MonoBehaviour
{
    [Header("Identidad")]
    [Tooltip("ID único para el guardado. Ej: 'brasero_chicunamictlan_01'. NO repetir.")]
    public string braseroID = "brasero_sin_id";

    [Header("Grupo (opcional)")]
    [Tooltip("Grupo al que pertenece. Si este Brasero es hijo de un BraseroGrupo, " +
             "se asigna solo. Cuando todos los del grupo se encienden, el grupo " +
             "dispara su evento de completado.")]
    public BraseroGrupo grupo;

    [Header("Visuales")]
    public Sprite spriteApagado;
    public Sprite spriteEncendido;
    [Tooltip("Capa de fuego con CihuacalliGlow (componente de brillo genérico). Opcional.")]
    public CihuacalliGlow glow;
    [Tooltip("Objeto del prompt 'Pulsa B' (hijo, opcional).")]
    public GameObject promptInteraccion;

    [Header("Input")]
    public KeyCode botonInteraccion = KeyCode.JoystickButton1;   // B de Xbox
    public KeyCode teclaAlterna = KeyCode.B;

    [Header("Eventos (opcional)")]
    [Tooltip("Se dispara al encender ESTE brasero (FX, sonido).")]
    public UnityEvent OnEncendido;

    [Header("Bloqueo (lo controla SalaSellada)")]
    [Tooltip("Mientras esté bloqueado no se puede encender.")]
    [SerializeField] private bool bloqueado = false;
    [Tooltip("Se muestra en rango mientras está bloqueado (ej. Tlacua escondido). Opcional.")]
    public GameObject promptBloqueado;
    [Tooltip("B pulsado estando bloqueado: chillido de Tlacua, sacudida. Opcional.")]
    public UnityEvent OnIntentoBloqueado;
    [Tooltip("Se desbloquea (zona limpia): brasas, Tlacua asoma. Opcional.")]
    public UnityEvent OnDesbloqueado;
    [Tooltip("Se vuelve a bloquear (custodios reagrupados). Opcional.")]
    public UnityEvent OnBloqueado;

    // ── Estado ───────────────────────────────────────────────
    private SpriteRenderer sr;
    private bool jugadorEnRango = false;
    private bool encendido = false;

    public bool EstaEncendido => encendido;
    public bool EstaBloqueado => bloqueado;

    /// <summary>Lo llama SalaSellada según queden custodios vivos.</summary>
    public void SetBloqueado(bool valor)
    {
        if (bloqueado == valor) return;
        bloqueado = valor;
        if (encendido) return;   // ya encendido: el bloqueo no aplica

        if (bloqueado) OnBloqueado?.Invoke();
        else           OnDesbloqueado?.Invoke();

        // Refrescar el prompt si Romerito está ahí parado.
        if (jugadorEnRango)
        {
            if (bloqueado) MostrarPromptBloqueado();
            else           MostrarPrompt();
        }
    }

    void Start()
    {
        // El swap de sprite va sobre el mismo renderer que el glow (si hay).
        sr = (glow != null) ? glow.Renderer : GetComponent<SpriteRenderer>();

        if (GameManager01.instance != null &&
            GameManager01.instance.BraseroEncendido(braseroID))
        {
            encendido = true;
        }

        AplicarVisual(instantaneo: true);
        OcultarPrompt();
    }

    void Update()
    {
        if (encendido) { OcultarPrompt(); return; }       // ya encendido: nada que hacer
        if (!jugadorEnRango) return;
        if (DialogueManager.IsActive) { OcultarPrompt(); return; }

        // [SALA-SELLADA] Tlacua no trabaja con custodios cerca.
        if (bloqueado)
        {
            MostrarPromptBloqueado();
            if (Input.GetKeyDown(botonInteraccion) || Input.GetKeyDown(teclaAlterna))
                OnIntentoBloqueado?.Invoke();
            return;
        }

        MostrarPrompt();
        if (Input.GetKeyDown(botonInteraccion) || Input.GetKeyDown(teclaAlterna))
            Encender();
    }

    private void Encender()
    {
        if (encendido || bloqueado) return;
        encendido = true;

        if (GameManager01.instance != null)
            GameManager01.instance.EncenderBrasero(braseroID);   // registra + guarda

        AplicarVisual(instantaneo: false);
        OcultarPrompt();

        Debug.Log("[Brasero] '" + braseroID + "' encendido.");
        OnEncendido?.Invoke();

        if (grupo != null) grupo.NotificarEncendido();
    }

    private void AplicarVisual(bool instantaneo)
    {
        if (sr != null)
        {
            Sprite objetivo = encendido ? spriteEncendido : spriteApagado;
            if (objetivo != null) sr.sprite = objetivo;
        }
        if (glow != null) glow.SetEncendido(encendido, instantaneo);
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

    private void MostrarPrompt()
    {
        if (promptInteraccion != null && !promptInteraccion.activeSelf)
            promptInteraccion.SetActive(true);
        if (promptBloqueado != null && promptBloqueado.activeSelf)
            promptBloqueado.SetActive(false);
    }

    private void MostrarPromptBloqueado()
    {
        if (promptInteraccion != null && promptInteraccion.activeSelf)
            promptInteraccion.SetActive(false);
        if (promptBloqueado != null && !promptBloqueado.activeSelf)
            promptBloqueado.SetActive(true);
    }

    private void OcultarPrompt()
    {
        if (promptInteraccion != null && promptInteraccion.activeSelf)
            promptInteraccion.SetActive(false);
        if (promptBloqueado != null && promptBloqueado.activeSelf)
            promptBloqueado.SetActive(false);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.05f, 0.35f);
        Collider2D col = GetComponent<Collider2D>();
        if (col is BoxCollider2D box)
            Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
        else if (col is CircleCollider2D circ)
            Gizmos.DrawWireSphere(transform.position + (Vector3)circ.offset, circ.radius);
    }
}
