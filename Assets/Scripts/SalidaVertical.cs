using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  SalidaVertical — Transición vertical entre escenas
// ============================================================
//
//  Coloca este script en un GameObject con BoxCollider2D (Trigger)
//  posicionado en el TECHO de la escena actual.
//
//  Solo se activa cuando Romerito cruza el trigger moviéndose
//  HACIA ARRIBA — así no se dispara si cae de regreso.
//
//  SETUP EN UNITY (Escena ORIGEN — Chichnauhmictlan_01):
//  ─────────────────────────────────────────────────────────────
//  1. Crea un GameObject vacío: "Salida_Arriba"
//  2. Añade:
//       • BoxCollider2D → Is Trigger: true
//         (cúbrelo con todo el ancho del hueco por donde sube Romerito)
//       • Este script (SalidaVertical)
//  3. En el Inspector asigna:
//       • sceneToLoad    → nombre exacto de la escena destino
//                          (ej: "Atlein_01")
//       • targetDoorID   → ID del EntradaVertical en la escena destino
//                          (ej: "entrada_desde_abajo")
//       • velocidadMinima → velocidad vertical mínima para activarse
//                           (default 0.1 — cualquier movimiento hacia arriba)
//  ─────────────────────────────────────────────────────────────

public class SalidaVertical : MonoBehaviour
{
    [Header("Destino")]
    [Tooltip("Nombre exacto de la escena a cargar (debe estar en Build Settings).")]
    public string sceneToLoad;

    [Tooltip("ID del EntradaVertical en la escena destino. " +
             "Debe coincidir exactamente con el campo 'doorID' del EntradaVertical.")]
    public string targetDoorID = "entrada_desde_abajo";

    [Header("Configuración")]
    [Tooltip("Velocidad vertical mínima hacia arriba para que se active la transición. " +
             "Evita activarse si Romerito está quieto o cayendo.")]
    public float velocidadMinimaArriba = 0.1f;

    // ── Unity ────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {

        Debug.Log($"[SalidaVertical] Contacto con: {other.name} | tag: {other.tag} | isTrigger: {other.isTrigger}");
        // Solo el jugador, no sus triggers internos
        if (!other.CompareTag("Player") || other.isTrigger) return;

        // Verificar que va hacia ARRIBA
        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb == null || rb.linearVelocity.y < velocidadMinimaArriba) return;

        // Verificar que tenemos destino
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError("[SalidaVertical] 'sceneToLoad' está vacío. Asigna la escena destino.");
            return;
        }

        Debug.Log($"[SalidaVertical] Romerito sube al nivel siguiente → {sceneToLoad} (puerta: {targetDoorID})");

        // Guardar en GameManager qué puerta usar al llegar
        if (GameManager01.instance != null)
            GameManager01.instance.SetNextDoor(targetDoorID);

        // Cargar la siguiente escena
        SceneManager.LoadScene(sceneToLoad);
    }

    // ── Gizmo para ver el trigger en el editor ───────────────

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.35f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
            Gizmos.DrawCube(transform.position + (Vector3)col.offset,
                            col.size);
        else
            Gizmos.DrawCube(transform.position, new Vector3(2f, 0.3f, 0f));

        // Flecha hacia arriba
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position,
                        transform.position + Vector3.up * 1.5f);
    }
}