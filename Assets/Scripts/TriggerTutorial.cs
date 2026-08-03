using UnityEngine;

// ============================================================
//  TriggerTutorial
// ============================================================
//  BoxCollider2D isTrigger que dispara un MensajeTutorial una vez
//  cuando el Player entra. Ideal para hints de tutorial (mover,
//  saltar, cámara). Áreas amplias sin problema — el mensaje solo
//  se dispara una vez por trigger.
// ============================================================

[RequireComponent(typeof(Collider2D))]
public class TriggerTutorial : MonoBehaviour
{
    [Header("Mensaje")]
    public MensajeTutorial mensaje;

    [Header("Comportamiento")]
    [Tooltip("Si es true, se destruye tras disparar. Útil para no acumular triggers muertos.")]
    public bool destruirTrasDisparar = true;

    private bool disparado;

    void Reset()
    {
        // Configuración automática al añadir el componente
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (disparado) return;
        if (!other.CompareTag("Player")) return;
        if (mensaje == null || TutorialManager.Instance == null) return;

        disparado = true;
        TutorialManager.Instance.Mostrar(mensaje);

        if (destruirTrasDisparar)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }
}