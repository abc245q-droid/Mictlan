using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("Escena del menú principal para el botón 'Salir' (opcional).")]
    public string escenaMenuPrincipal = "MenuPrincipal";

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
        // Cachear referencias a Romerito
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            movimiento = player.GetComponent<RomeritoMovement>();
            combate = player.GetComponent<RomeritoCombat>();
            rb = player.GetComponent<Rigidbody2D>();
        }

        // El panel empieza oculto
        if (pausePanel != null)
            pausePanel.SetActive(false);
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
    /// Carga la escena del menú principal.
    /// Conectar al botón "Salir" del panel de pausa.
    /// </summary>
    public void SalirAlMenu()
    {
        // Restaurar timeScale por seguridad antes de cambiar de escena
        Time.timeScale = 1f;
        pausado = false;

        UnityEngine.SceneManagement.SceneManager.LoadScene(escenaMenuPrincipal);
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