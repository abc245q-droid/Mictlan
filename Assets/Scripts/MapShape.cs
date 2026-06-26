using UnityEngine;

// ============================================================
//  MapShape — Silueta de una sala para el Códice
// ============================================================
//
//  Componente que defines a mano con un PolygonCollider2D, siguiendo
//  el contorno real de paredes y pisos. El mapa dibuja ESTA forma en
//  lugar del rectángulo del RoomConfiner de cámara.
//
//  No cambia nada de la lógica de estados: la sala se sigue rastreando
//  por su mapRoomId (visitada → Borrador → Asentada). Esto solo decide
//  el DIBUJO.
//
//  Setup:
//    • Pon este componente en un objeto nuevo (no en el RoomConfiner
//      de cámara). Trae un PolygonCollider2D; edítalo a la forma de la sala.
//    • mapRoomId debe COINCIDIR con el de la sala (el del RoomConfiner).
//    • Coloca el objeto en una capa que no colisione con nada (para que
//      el collider sea solo "dato de dibujo", no física real). Marca
//      Is Trigger por seguridad.
//
// ============================================================

[RequireComponent(typeof(PolygonCollider2D))]
public class MapShape : MonoBehaviour
{
    [Tooltip("Debe coincidir con el mapRoomId de la sala (el del RoomConfiner). Ej: 'L1_tianguis'.")]
    public string mapRoomId = "";

    public PolygonCollider2D Poly => GetComponent<PolygonCollider2D>();

    public Bounds Bounds => Poly.bounds;

    /// <summary>Malla triangulada en espacio de mundo. CreateMesh maneja
    /// formas cóncavas y múltiples contornos sin que tengamos que triangular.</summary>
    public Mesh CrearMallaMundo() => Poly.CreateMesh(true, true);
}
