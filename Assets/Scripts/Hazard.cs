using UnityEngine;

public class Hazard : MonoBehaviour
{
    [Header("Configuración")]
    public int damageAmount = 1;
    public bool instantKill = false; // Por si quieres pozos sin fondo que maten de una

    void OnTriggerEnter2D(Collider2D collision)
    {

       // if (collision.CompareTag("Xolo"))
        {
         //   return; // El Xolo ignora el agua
        }

        if (collision.CompareTag("Player"))
        {

          
            RomeritoHealth health = collision.GetComponent<RomeritoHealth>();
            RomeritoMovement movement = collision.GetComponent<RomeritoMovement>();

            if (health != null && movement != null)
            {
                if (instantKill)
                {
                    health.TakeDamage(1000); // Matar instantáneamente
                }
                else
                {
                    // Llamamos a la nueva función pasando la posición segura que recuerda el movimiento
                    health.TakeHazardDamage(movement.lastSafePosition);
                }
            }
        }
    }
}