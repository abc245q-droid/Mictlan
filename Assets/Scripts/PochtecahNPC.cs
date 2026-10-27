using UnityEngine;

// ============================================================
//  PochtecahNPC — Interacción con el mercader
// ============================================================
//
//  Espejo del patrón de Cihuacalli: proximidad + botón B, prompt,
//  gate por DialogueManager.IsActive. Al interactuar:
//    1ª vez con lore asignado → reproduce el diálogo, luego abre tienda.
//    Resto de veces           → abre la tienda directo.
//
//  Requiere:
//    • Collider2D en trigger (área de interacción).
//    • Un PochtecahShop (lógica) en el mismo objeto o asignado.
//    • Un PochtecahShopUI en la escena (panel).
//
// ============================================================

[RequireComponent(typeof(PochtecahShop))]
public class PochtecahNPC : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Lógica de comercio. Si se deja vacío, se toma del mismo objeto.")]
    public PochtecahShop tienda;
    [Tooltip("Panel de tienda. Si se deja vacío, se busca en la escena.")]
    public PochtecahShopUI ui;

    [Header("Lore (opcional)")]
    [Tooltip("Diálogo que se reproduce la PRIMERA vez que se habla con este Pochtecah.")]
    public Conversation dialogoLore;
    [Tooltip("ID para recordar que el lore ya se vio (se guarda en collectedItems). Ej: 'lore_pochtecah_n1'.")]
    public string dialogoLoreID = "";

    [Header("Regalo: Códice Maestro")]
    [Tooltip("La PRIMERA vez que Romerito habla con este Pochtecah recibe el " +
             "Códice Maestro (mapa, inventario, equipamiento, bestiario, herbario). " +
             "Actívalo solo en el Pochtecah que Romerito libera del cuauhcalli " +
             "en el Nivel 1 (EncuentroPochtecah).")]
    public bool regalaCodiceMaestro = false;
    [Tooltip("Diálogo de la entrega. Si se deja vacío se usa dialogoLore.")]
    public Conversation dialogoRegalo;
    [Tooltip("true → el regalo incluye el Estuche de Tlacuilo: el mapa se dibuja " +
             "desde ya. false → el Estuche se sigue comprando aparte.")]
    public bool regaloIncluyeEstuche = true;
    [Tooltip("Mensaje que enseña a abrir el Códice (View / Tab). Opcional.")]
    public MensajeTutorial mensajeCodice;

    [Header("UI de prompt")]
    [Tooltip("Objeto hijo 'Pulsa B' que aparece en rango (opcional).")]
    public GameObject promptInteraccion;

    [Header("Input")]
    public KeyCode botonInteraccion = KeyCode.JoystickButton1;
    public KeyCode teclaAlterna = KeyCode.B;

    private bool jugadorEnRango;
    private bool ocupado;   // durante el lore, antes de abrir tienda

    void Awake()
    {
        if (tienda == null) tienda = GetComponent<PochtecahShop>();
        if (ui == null) ui = FindObjectOfType<PochtecahShopUI>();
    }

    void Update()
    {
        if (!jugadorEnRango) { return; }

        // Nada de prompts/input durante diálogo o con la tienda ya abierta.
        if (DialogueManager.IsActive || PochtecahShopUI.IsOpen || ocupado ||
            CodiceUI.BloqueaInput)
        {
            OcultarPrompt();
            return;
        }

        MostrarPrompt();

        if (Input.GetKeyDown(botonInteraccion) || Input.GetKeyDown(teclaAlterna))
            Interactuar();
    }

    /// <summary>
    /// Arranca la interacción sin esperar al botón (p. ej. EncuentroPochtecah
    /// al liberarlo: entrega del Códice Maestro). Respeta los mismos bloqueos.
    /// </summary>
    public void ForzarInteraccion()
    {
        if (DialogueManager.IsActive || PochtecahShopUI.IsOpen || ocupado) return;
        Interactuar();
    }

    private void Interactuar()
    {
        // ── Regalo del Códice Maestro (una sola vez en toda la partida) ──
        // Esa primera visita es solo para el regalo; la tienda abre en la
        // siguiente interacción.
        if (regalaCodiceMaestro && !CodiceMaestro.TieneCodice)
        {
            EntregarCodice();
            return;
        }

        if (dialogoLore != null && !LoreYaVisto() && DialogueManager.Instance != null)
        {
            OcultarPrompt();
            ocupado = true;
            DialogueManager.Instance.StartConversation(dialogoLore, () =>
            {
                MarcarLoreVisto();
                ocupado = false;
                AbrirTienda();
            });
            return;
        }

        AbrirTienda();
    }

    private void EntregarCodice()
    {
        bool usaLore = dialogoRegalo == null;
        Conversation conv = usaLore ? dialogoLore : dialogoRegalo;

        if (conv != null && DialogueManager.Instance != null)
        {
            OcultarPrompt();
            ocupado = true;
            DialogueManager.Instance.StartConversation(conv, () =>
            {
                ocupado = false;
                FinalizarRegalo(usaLore);
            });
            return;
        }

        FinalizarRegalo(false);
    }

    private void FinalizarRegalo(bool loreReproducido)
    {
        if (!CodiceMaestro.DarCodice(regaloIncluyeEstuche)) return;

        // Si el lore sirvió de diálogo de entrega, no se repite al abrir la tienda.
        if (loreReproducido) MarcarLoreVisto();

        if (GameManager01.instance != null) GameManager01.instance.SaveGame();

        if (mensajeCodice != null && TutorialManager.Instance != null)
            TutorialManager.Instance.Mostrar(mensajeCodice);
    }

    private void AbrirTienda()
    {
        if (ui == null) { Debug.LogError("[Pochtecah] No hay PochtecahShopUI en la escena."); return; }
        OcultarPrompt();
        ui.Abrir(tienda);
    }

    // ── Lore una-vez (espejo de Cihuacalli, vía collectedItems) ──
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
        {
            GameManager01.instance.currentData.collectedItems.Add(dialogoLoreID);
            GameManager01.instance.SaveGame();
        }
    }

    // ── Proximidad ────────────────────────────────────────
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
    }

    private void OcultarPrompt()
    {
        if (promptInteraccion != null && promptInteraccion.activeSelf)
            promptInteraccion.SetActive(false);
    }
}
