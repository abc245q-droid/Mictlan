using UnityEngine;

// ============================================================
//  Trampa — Hazard genérico para MICTLÁN
// ============================================================
//
//  Un solo script cubre todos los tipos de trampa:
//    • Pinchos / obsidiana     → collider fino sobre las puntas
//    • Agua / lago             → collider grande sobre el agua
//    • Vacío / precipicio      → collider invisible al fondo del nivel
//
//  Al contacto: Romerito pierde 1 vida y reaparece en una
//  posición segura (soft respawn). Si pierde la última vida,
//  se dispara la secuencia de muerte normal (DieRoutine).
//
//  POSICIÓN SEGURA — tres opciones configurables:
//    • UltimoSueloPiso  → last safe ground de RomeritoMovement
//                         (respawn en el borde del precipicio — más justo)
//    • UltimoCheckpoint → GameManager.lastCheckPointPos
//                         (respawn en el Cihuacalli)
//    • PosicionFija     → Transform asignado manualmente
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. Añade este script al GameObject de la trampa.
//  2. Añade un Collider2D → Is Trigger: true
//     (ajusta la forma al área peligrosa).
//  3. Asigna el tipo de respawn en el Inspector.
//  4. Opcional: asigna efectoContacto (partículas, splash, etc.)
//  ─────────────────────────────────────────────────────────────

public class Trampa : MonoBehaviour
{
    // ── Tipos de posición segura ─────────────────────────────

    public enum TipoRespawn
    {
        [Tooltip("Último suelo firme donde Romerito estuvo parado. " +
                 "Lo más justo para precipicios y plataformas.")]
        UltimoSueloPiso,

        [Tooltip("Posición del último Cihuacalli (checkpoint) activado.")]
        UltimoCheckpoint,

        [Tooltip("Posición manual: arrastrá un Transform al campo PosicionSegura.")]
        PosicionFija
    }

    // ── Inspector ────────────────────────────────────────────

    [Header("Respawn")]
    public TipoRespawn tipoRespawn = TipoRespawn.UltimoSueloPiso;

    [Tooltip("Solo si tipoRespawn = PosicionFija. " +
             "Arrastrá aquí el Transform del punto de reaparición.")]
    public Transform posicionFija;

    [Header("Feedback (Opcional)")]
    [Tooltip("Prefab de partículas o efecto al contacto (splash, chispas, etc.)")]
    public GameObject efectoContacto;

    [Tooltip("Si es true, el efecto spawna en la posición de Romerito. " +
             "Si es false, spawna en el centro de esta trampa.")]
    public bool efectoEnJugador = true;

    // ── Unity ────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        // Solo el collider físico de Romerito (no sus triggers internos)
        if (!other.CompareTag("Player") || other.isTrigger) return;

        RomeritoHealth health = other.GetComponent<RomeritoHealth>();
        if (health == null) return;

        // Calcular posición de reaparición
        Vector2 safePos = ObtenerPosicionSegura(other.gameObject);

        // Aplicar daño y teletransporte
        health.TakeHazardDamage(safePos);

        // Efecto visual
        if (efectoContacto != null)
        {
            Vector3 posEfecto = efectoEnJugador
                ? other.transform.position
                : transform.position;
            Instantiate(efectoContacto, posEfecto, Quaternion.identity);
        }
    }

    // ── Lógica de posición segura ────────────────────────────

    Vector2 ObtenerPosicionSegura(GameObject player)
    {
        switch (tipoRespawn)
        {
            case TipoRespawn.UltimoSueloPiso:
                // Mejor opción para precipicios: usa la posición donde
                // Romerito estuvo parado en suelo firme por última vez.
                RomeritoMovement mov = player.GetComponent<RomeritoMovement>();
                if (mov != null)
                    return mov.lastSafePosition;

                // Fallback si no hay RomeritoMovement
                goto case TipoRespawn.UltimoCheckpoint;

            case TipoRespawn.UltimoCheckpoint:
                if (GameManager01.instance != null)
                    return GameManager01.instance.lastCheckPointPos;

                // Fallback último recurso
                return (Vector2)player.transform.position + Vector2.up * 3f;

            case TipoRespawn.PosicionFija:
                if (posicionFija != null)
                    return posicionFija.position;

                Debug.LogWarning("[Trampa] PosicionFija seleccionada pero no asignada. " +
                                 "Usando último checkpoint.");
                goto case TipoRespawn.UltimoCheckpoint;

            default:
                return (Vector2)player.transform.position + Vector2.up * 3f;
        }
    }

    // ── Gizmos ───────────────────────────────────────────────

    void OnDrawGizmos()
    {
        // Zona de peligro (rojo)
        Gizmos.color = new Color(1f, 0.15f, 0f, 0.3f);
        Collider2D col = GetComponent<Collider2D>();
        if (col is BoxCollider2D box)
            Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
        else
            Gizmos.DrawSphere(transform.position, 0.5f);

        // Posición segura fija (verde)
        if (tipoRespawn == TipoRespawn.PosicionFija && posicionFija != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(posicionFija.position, 0.35f);
            Gizmos.DrawLine(transform.position, posicionFija.position);
        }
    }
}