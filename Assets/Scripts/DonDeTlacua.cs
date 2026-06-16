using UnityEngine;
using UnityEngine.Events;

// ============================================================
//  DonDeTlacua — El encuentro con Tlacua en Chicunamictlan
// ============================================================
//
//  Coloca este script en el GameObject de la Sala Tlacua.
//  Añade un BoxCollider2D → Is Trigger: true que cubra la entrada.
//
//  FLUJO:
//   1. Romerito entra al trigger → comienza el diálogo de Tlacua
//   2. Al terminar el diálogo → OtorgarDon() se llama automáticamente
//   3. OtorgarDon():
//       • Activa tieneDonDeTlacua en PlayerData
//       • Guarda partida
//       • Muestra el PanelTonalli (antes oculto)
//       • Dispara el efecto visual del don
//       • Dispara el evento OnDonOtorgado (para animar a Tlacua, etc.)
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. Añade este script a "Sala Tlacua" (o a un trigger hijo).
//  2. Asigna en el Inspector:
//       • conversacion → el asset Conversation del diálogo de Tlacua
//       • panelTonalli → el GameObject "PanelTonalli" del HUD Canvas
//       • efectoDon    → prefab de partículas doradas (opcional)
//  3. Conecta el UnityEvent OnDonOtorgado a lo que quieras:
//       • Animación de Tlacua celebrando
//       • Activar la misión de los braseros
//       • Cualquier otra cosa
//  ─────────────────────────────────────────────────────────────

[RequireComponent(typeof(Collider2D))]
public class DonDeTlacua : MonoBehaviour
{
    [Header("Diálogo")]
    [Tooltip("Asset Conversation con el diálogo completo de Tlacua.")]
    public Conversation conversacion;

    [Header("UI del Tonalli")]
    [Tooltip("El GameObject 'PanelTonalli' del HUD Canvas. " +
             "Empieza oculto y se revela al recibir el don.")]
    public GameObject panelTonalli;

    [Header("Feedback Visual (Opcional)")]
    [Tooltip("Efecto de partículas doradas que aparece sobre Romerito al recibir el don.")]
    public GameObject efectoDon;

    [Tooltip("Duración en segundos que el efecto permanece activo.")]
    public float duracionEfecto = 2f;

    [Header("Evento al otorgar el don")]
    [Tooltip("Se dispara cuando Tlacua otorga el don — " +
             "conectá aquí la animación de Tlacua, la misión de braseros, etc.")]
    public UnityEvent OnDonOtorgado;

    // ── Estado interno ───────────────────────────────────────

    private bool yaOtorgado = false;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        // Si ya tiene el don (cargó partida), desactivar este trigger
        if (YaTieneDon())
        {
            yaOtorgado = true;

            // Asegurarse de que el PanelTonalli esté visible
            if (panelTonalli != null)
                panelTonalli.SetActive(true);

            // Desactivar el collider para no disparar el diálogo de nuevo
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
        else
        {
            // Ocultar el PanelTonalli hasta que Tlacua lo otorgue
            if (panelTonalli != null)
                panelTonalli.SetActive(false);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (yaOtorgado) return;
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (DialogueManager.Instance == null) return;
        if (DialogueManager.IsActive) return;

        if (conversacion == null)
        {
            Debug.LogError("[DonDeTlacua] No hay Conversation asignada. " +
                           "Arrastrá el asset de diálogo de Tlacua al campo 'Conversacion'.");
            return;
        }

        yaOtorgado = true; // Marcamos ya aquí para evitar re-disparos

        // Iniciar el diálogo — al terminar, otorgamos el don
        DialogueManager.Instance.StartConversation(conversacion, OtorgarDon);

        Debug.Log("[DonDeTlacua] Diálogo de Tlacua iniciado.");
    }

    // ── API Pública ──────────────────────────────────────────

    /// <summary>
    /// Otorga el Don de Tlacua: activa el almacenamiento de Tonalli
    /// y la capacidad de curación. Llamado al terminar el diálogo.
    /// También puede llamarse manualmente desde el Inspector (para testing).
    /// </summary>
    public void OtorgarDon()
    {
        // Activar en PlayerData
        if (GameManager01.instance != null)
        {
            GameManager01.instance.currentData.tieneDonDeTlacua = true;
            GameManager01.instance.SaveGame();
            Debug.Log("[DonDeTlacua] ✨ Don de Tlacua otorgado y guardado.");
        }
        else
        {
            Debug.LogWarning("[DonDeTlacua] No hay GameManager — el don no se guardó.");
        }

        // Revelar el PanelTonalli
        if (panelTonalli != null)
            panelTonalli.SetActive(true);

        // Efecto visual sobre Romerito
        if (efectoDon != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Vector3 pos = player != null ? player.transform.position : transform.position;
            GameObject fx = Instantiate(efectoDon, pos, Quaternion.identity);
            Destroy(fx, duracionEfecto);
        }

        // Desactivar el collider — ya no necesitamos el trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Disparar evento externo
        OnDonOtorgado?.Invoke();
    }

    // ── Utilidades ───────────────────────────────────────────

    private bool YaTieneDon()
    {
        return GameManager01.instance != null &&
               GameManager01.instance.currentData.tieneDonDeTlacua;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.25f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
            Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
    }
}