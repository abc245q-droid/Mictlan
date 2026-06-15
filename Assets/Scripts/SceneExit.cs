using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneExit : MonoBehaviour
{
    public string sceneToLoad;
    public string targetDoorID;

    void OnTriggerEnter2D(Collider2D other)
    {
        // DEBUG 1: ¿Detectamos colisión?
        Debug.Log("Algo tocó la puerta: " + other.gameObject.name);

        if (other.CompareTag("Player") && !other.isTrigger)
        {
            // DEBUG 2: ¿Es el Player?
            Debug.Log("¡Es el Player! Intentando contactar al GameManager...");

            if (GameManager01.instance != null)
            {
                // DEBUG 3: ¿Existe el GameManager?
                Debug.Log("GameManager encontrado. Seteando puerta: " + targetDoorID);

                GameManager01.instance.SetNextDoor(targetDoorID);
                SceneManager.LoadScene(sceneToLoad);
            }
            else
            {
                // ERROR: No hay GameManager
                Debug.LogError("FATAL: ¡GameManager01.instance es NULL! Asegúrate de iniciar el juego desde la escena donde está el Manager o que este sea persistente.");
            }
        }
    }
}