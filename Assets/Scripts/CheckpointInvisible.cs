using UnityEngine;
using UnityEngine.Events;

// ============================================================
//  CheckpointInvisible — Punto de reaparición sin Cihuacalli
// ============================================================
//
//  Para tramos sin Cihuacalli (p. ej. el Nivel 0, Atlein): al cruzarlo,
//  Romerito reaparecerá aquí si muere. No se ve ni se anuncia.
//
//  REGLAS
//   • Solo AVANZA: un checkpoint con 'orden' menor o igual al último
//     alcanzado no hace nada (volver atrás no mueve la reaparición).
//   • Un Cihuacalli encendido siempre manda: si ya hay uno activo, los
//     checkpoints invisibles se ignoran.
//   • Guarda la partida en silencio (como un Cihuacalli, sin efectos),
//     para que "Continuar" también arranque desde aquí.
//
//  SETUP EN UNITY
//   1. GameObject vacío con BoxCollider2D (Is Trigger ON) que cruce el
//      camino de lado a lado (alto suficiente para no saltarlo).
//   2. Este componente. 'orden' = 1, 2, 3... en el sentido de la ruta.
//   3. (Opcional) puntoReaparicion → hijo colocado en el piso, donde
//      debe aparecer Romerito. Si está vacío, se usa este transform.
// ============================================================

[RequireComponent(typeof(Collider2D))]
public class CheckpointInvisible : MonoBehaviour
{
    [Tooltip("Posición en la ruta: 1, 2, 3... Solo se acepta si es mayor que el último alcanzado.")]
    public int orden = 1;
    [Tooltip("Dónde reaparece Romerito (pies en el piso). Vacío = este transform.")]
    public Transform puntoReaparicion;
    [Tooltip("Guardar la partida en silencio al cruzarlo.")]
    public bool guardarPartida = true;
    [Tooltip("Se dispara al alcanzar este checkpoint: aquí se conecta el efecto " +
             "visual donde se concentra el teyolía de Romerito.")]
    public UnityEvent alActivarse;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        var gm = GameManager01.instance;
        if (gm == null || gm.currentData == null) return;

        PlayerData datos = gm.currentData;
        if (!string.IsNullOrEmpty(datos.cihuacalliActivoID)) return;   // manda el Cihuacalli
        if (orden <= datos.checkpointInvisibleOrden) return;            // solo avanza

        datos.checkpointInvisibleOrden = orden;
        Vector2 pos = puntoReaparicion != null ? (Vector2)puntoReaparicion.position
                                               : (Vector2)transform.position;

        if (guardarPartida)
        {
            gm.UpdateCheckPoint(pos);   // fija la reaparición y guarda
        }
        else
        {
            gm.lastCheckPointPos = pos;
            datos.tieneCheckpointGuardado = true;
        }

        Debug.Log($"[CheckpointInvisible] '{name}' (orden {orden}) → reaparición en {pos}.");
        alActivarse?.Invoke();
    }

    void OnDrawGizmos()
    {
        var c = GetComponent<Collider2D>();
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.25f);
        if (c != null) Gizmos.DrawCube(c.bounds.center, c.bounds.size);

        Vector3 p = puntoReaparicion != null ? puntoReaparicion.position : transform.position;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(p, 0.3f);
        Gizmos.DrawLine(p, p + Vector3.up * 1.2f);
    }
}
