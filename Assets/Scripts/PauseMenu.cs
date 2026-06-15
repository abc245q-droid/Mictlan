using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool GameIsPaused = false;
    public GameObject pauseMenuUI; // Aquí arrastraremos el Panel_Pausa

    void Update()
    {
        // Detectar la tecla ESCAPE (o Start en control)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameIsPaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false); // Ocultar menú
        Time.timeScale = 1f;          // 1 = Tiempo normal
        GameIsPaused = false;
    }

    void Pause()
    {
        pauseMenuUI.SetActive(true);  // Mostrar menú
        Time.timeScale = 0f;          // 0 = Tiempo congelado
        GameIsPaused = true;
    }

    // --- FUNCIONES PARA LOS BOTONES ---

    public void LoadMenu()
    {
        Time.timeScale = 1f; // IMPORTANTE: Descongelar antes de cambiar escena
        SceneManager.LoadScene("MenuPrincipal"); // Si tienes una escena de menú
    }

    public void QuitGame()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }

    // Esta función conecta tu botón de "Nueva Partida" con el GameManager
    public void ResetGameFromMenu()
    {
        Time.timeScale = 1f; // Siempre descongelar antes de recargar

        if (GameManager01.instance != null)
        {
            GameManager01.instance.NewGame();
        }
    }
}