using UnityEngine;
using System.Collections;

// ============================================================
//  EntradaVertical — Spawn con impulso diagonal al cambiar escena
// ============================================================
//
//  Coloca este script en la escena DESTINO. Cuando Romerito llega
//  aquí proveniente de una SalidaVertical, lo posiciona y le aplica
//  un impulso diagonal para que aterrice en el piso de la izquierda
//  (o en la dirección que configures).
//
//  SETUP EN UNITY (Escena DESTINO — Atlein_01):
//  ─────────────────────────────────────────────────────────────
//  1. Crea un GameObject vacío: "Entrada_Desde_Abajo"
//  2. Posicionalo donde Romerito debe aparecer
//     (encima del hueco por donde sube, cerca del techo).
//  3. Añade este script (EntradaVertical).
//  4. En el Inspector asigna:
//       • doorID        → debe coincidir con 'targetDoorID' de SalidaVertical
//                         (ej: "entrada_desde_abajo")
//       • impulso       → Vector2 del empuje inicial
//                         X negativo = hacia la izquierda
//                         Y positivo = hacia arriba
//                         Ejemplo: (-4, 3) → cae en diagonal a la izquierda
//       • esperarFrames → frames a esperar antes de aplicar el impulso
//                         (1-2 es suficiente para que el Rigidbody esté listo)
//  ─────────────────────────────────────────────────────────────
//
//  LÓGICA:
//  • En Start(), compara GameManager01.nextDoorID con su propio doorID.
//  • Si coinciden: teletransporta a Romerito y aplica el impulso.
//  • Si no coincide: no hace nada (otro EntradaVertical de la escena
//    se encargará, o Romerito spawneará en el checkpoint guardado).
//  ─────────────────────────────────────────────────────────────

public class EntradaVertical : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("ID único de esta entrada. Debe coincidir con el 'targetDoorID' " +
             "de la SalidaVertical que lleva aquí.")]
    public string doorID = "entrada_desde_abajo";

    [Header("Impulso de Entrada")]
    [Tooltip("Velocidad inicial que se aplica a Romerito al aparecer. " +
             "X negativo = hacia la izquierda (para caer en el piso izquierdo). " +
             "Ajusta Y para controlar cuánto sube antes de caer.")]
    public Vector2 impulso = new Vector2(-4f, 2f);

    [Tooltip("Frames a esperar antes de aplicar el impulso. " +
             "Mínimo 1 para que el Rigidbody esté inicializado.")]
    [Range(1, 5)]
    public int esperarFrames = 2;

    [Header("Cámara (Opcional)")]
    [Tooltip("Si está activo, desactiva el suavizado de Cinemachine durante 1 segundo " +
             "para evitar un pan brusco de cámara al entrar.")]
    public bool snapCamara = true;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        // ¿Venimos de una SalidaVertical que apunta a esta entrada?
        if (GameManager01.instance == null) return;
        if (GameManager01.instance.nextDoorID != doorID) return;

        // Limpiar el doorID para que no se reactive en respawns
        GameManager01.instance.SetNextDoor("");

        // Buscar a Romerito
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[EntradaVertical] No se encontró al jugador con tag 'Player'.");
            return;
        }

        // Teleportar a la posición de esta entrada
        player.transform.position = transform.position;

        // Actualizar checkpoint en esta posición
        GameManager01.instance.UpdateCheckPoint(transform.position);

        // Aplicar impulso con un frame de retraso
        StartCoroutine(AplicarImpulso(player));

        Debug.Log($"[EntradaVertical] Romerito entra por '{doorID}' con impulso {impulso}.");
    }

    // ── Corrutina ─────────────────────────────────────────────

    IEnumerator AplicarImpulso(GameObject player)
    {
        // Esperar los frames configurados
        for (int i = 0; i < esperarFrames; i++)
            yield return null;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null) yield break;

        // Aplicar impulso diagonal
        // Usamos velocity directa en lugar de AddForce para control preciso
        rb.linearVelocity = impulso;

        // Congelar movimiento de Romerito brevemente (opcional):
        // Si quieres que Romerito no pueda actuar hasta aterrizar,
        // descomenta las siguientes líneas:
        //
        // RomeritoMovement mov = player.GetComponent<RomeritoMovement>();
        // if (mov != null) mov.enabled = false;
        // yield return new WaitForSeconds(0.4f);
        // if (mov != null) mov.enabled = true;
    }

    // ── Gizmo para ver el punto de spawn en el editor ────────

    void OnDrawGizmos()
    {
        // Punto de spawn
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        // Dirección del impulso
        Gizmos.color = Color.cyan;
        Vector3 dir = new Vector3(impulso.x, impulso.y, 0f).normalized;
        Gizmos.DrawLine(transform.position,
                        transform.position + dir * 1.5f);

        // Etiqueta visual
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f,
                                  $"ENTRADA\n{doorID}");
#endif
    }
}