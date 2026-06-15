using UnityEngine;

public class SceneEntrance : MonoBehaviour
{
    public string doorID; // Ej: "Entrada_Norte", "Entrada_Desde_Mictlan1"

    void Start()
    {
        // 1. Preguntamos al GameManager si esta es la puerta correcta
        if (GameManager01.instance != null && GameManager01.instance.nextDoorID == doorID)
        {
            // 2. Buscamos a Romerito (ahora es persistente)
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                // 3. Movemos a Romerito a ESTA posición
                player.transform.position = transform.position;

                // 4. Importante: Si Romerito miraba a la derecha y esta entrada
                // requiere mirar a la izquierda, aquí podrías forzar el Flip.
            }
        }
    }

    // Para verlo en el editor
    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        // Dibuja una flecha indicando hacia dónde mira
    }
}