using UnityEngine;

// ============================================================
//  DialogueTrigger — Dispara una conversación al cruzar una zona
// ============================================================
//
//  Coloca este script en un GameObject con un Collider2D en modo
//  Trigger, justo donde quieras que arranque el diálogo (p. ej.
//  la salida del altar, donde aparece Mictlantecuhtli).
//
//  Asigna en el Inspector el asset Conversation que debe reproducir.
//
//  • playOnce: si está activo, no se repite. Si hay GameManager,
//    se recuerda entre escenas/guardados usando un dialogueID.
//  • Evento OnTerminado: se dispara al cerrar el diálogo (úsalo para
//    abrir el paso, despawnear al dios, iniciar el combate, etc.).
//
//  Para conversaciones definidas por código (como el encuentro de
//  Mictlantecuhtli ya escrito), usa EncuentroMictlantecuhtli en su
//  lugar; este trigger es para conversaciones basadas en asset.
//
// ============================================================

[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [Header("Conversación")]
    public Conversation conversation;

    [Header("Repetición")]
    [Tooltip("Si está activo, la conversación solo ocurre una vez.")]
    public bool playOnce = true;

    [Tooltip("ID único para recordar (vía GameManager) que ya ocurrió. " +
             "Ej: 'lore_mictlantecuhtli_atlein'. Déjalo vacío para no persistir.")]
    public string dialogueID = "";

    [Header("Evento al terminar (opcional)")]
    public UnityEngine.Events.UnityEvent OnTerminado;

    private bool yaUsado = false;

    void Start()
    {
        // Si ya ocurrió en una sesión anterior (guardado), no repetir.
        if (playOnce && !string.IsNullOrEmpty(dialogueID) &&
            GameManager01.instance != null &&
            GameManager01.instance.currentData != null &&
            GameManager01.instance.currentData.collectedItems != null &&
            GameManager01.instance.currentData.collectedItems.Contains(dialogueID))
        {
            yaUsado = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (yaUsado) return;
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (DialogueManager.IsActive) return;
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[DialogueTrigger] No hay DialogueManager en la escena.");
            return;
        }

        yaUsado = true;

        // Persistir que ya ocurrió.
        if (playOnce && !string.IsNullOrEmpty(dialogueID) &&
            GameManager01.instance != null &&
            GameManager01.instance.currentData != null)
        {
            if (!GameManager01.instance.currentData.collectedItems.Contains(dialogueID))
                GameManager01.instance.currentData.collectedItems.Add(dialogueID);
        }

        DialogueManager.Instance.StartConversation(conversation, () =>
        {
            OnTerminado?.Invoke();
        });
    }
}
