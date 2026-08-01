using UnityEngine;

// ============================================================
//  MapRoom — Sala del Códice de Romerito
// ============================================================
//
//  Componente INDEPENDIENTE del sistema de cámara. Su única
//  responsabilidad es representar una sala en el mapa:
//    (a) registrar la entrada de Romerito como Borrador de Tonalli,
//    (b) proveer bounds como fallback cuando no hay MapShape.
//
//  Convive con RoomConfiner sin acoplarse a él. Casos típicos:
//    • Mismo GameObject que un RoomConfiner (comparten Collider2D
//      trigger): la sala del mapa coincide con la zona de cámara.
//    • GameObject propio: la zona del mapa no coincide con la
//      cámara (varias vistas dentro de una misma sala, pasillos
//      que no reconfinan, arenas aditivas, etc.).
//
//  Convención de mapRoomId: 'L{nivel}_{nombre}'. Ej: 'L1_pozo'.
//
// ============================================================

[RequireComponent(typeof(Collider2D))]
public class MapRoom : MonoBehaviour
{
    [Tooltip("ID de esta sala para el mapa. Convención: 'L{nivel}_{nombre}'. Ej: 'L1_pozo'.")]
    public string mapRoomId = "";

    private Collider2D miCollider;

    /// <summary>Bounds del collider, para el dibujo rectangular fallback del mapa.</summary>
    public Bounds Bounds
    {
        get
        {
            if (miCollider == null) miCollider = GetComponent<Collider2D>();
            return miCollider != null ? miCollider.bounds : new Bounds(transform.position, Vector3.zero);
        }
    }

    void Awake()
    {
        miCollider = GetComponent<Collider2D>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        MapManager.OnRoomEntered(mapRoomId);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(mapRoomId))
            Debug.LogWarning($"[MapRoom] '{name}' no tiene mapRoomId. " +
                             "Convención: 'L{n}_nombre' (p.ej. 'L1_pozo').", this);

        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[MapRoom] '{name}': el Collider2D debería estar en modo Trigger " +
                             "para detectar al player sin bloquear la física.", this);
    }
#endif
}
