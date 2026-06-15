using UnityEngine;
using UnityEngine;

// ============================================================
//  RoomDoor — Barrera física de la sala del Macahuitl
// ============================================================
//
//  Un mismo script maneja AMBAS puertas (entrada y salida),
//  con comportamiento diferente según el campo 'doorType'.
//
//  ENTRADA (DoorType.Entrada):
//    • Abierta al llegar a la sala (Romerito puede entrar y salir)
//    • Se cierra cuando Romerito toca el Macahuitl (combate activo)
//    • Se abre cuando Romerito muere o cuando termina el combate
//
//  SALIDA (DoorType.Salida):
//    • Bloqueada físicamente todo el tiempo durante el combate
//    • Al terminar las oleadas, se vuelve destructible:
//      el Macahuitl puede romperla con un golpe normal
//    • Una vez destruida, no regresa (no forma parte del reset)
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. Crea dos GameObjects en la sala: "Puerta_Entrada" y "Puerta_Salida"
//  2. A cada uno añade:
//       • Este script (RoomDoor)
//       • Un Collider2D (no Trigger) — la barrera física
//       • Un SpriteRenderer — el visual de la puerta
//  3. En el Inspector de cada puerta asigna:
//       • doorType     → Entrada o Salida según corresponda
//       • roomManager  → el MacahuitlRoomManager de la sala
//  4. En el MacahuitlRoomManager asigna:
//       • doorEntrada  → la puerta de entrada
//       • doorSalida   → la puerta de salida
//  ─────────────────────────────────────────────────────────────

public class RoomDoor : MonoBehaviour
{
    public enum DoorType { Entrada, Salida }

    [Header("Configuración")]
    public DoorType doorType = DoorType.Entrada;

    [Header("Visual")]
    [Tooltip("Sprite cuando la puerta está activa/cerrada")]
    public Sprite spriteCerrada;
    [Tooltip("Sprite cuando la puerta está abierta (solo Entrada)")]
    public Sprite spriteAbierta;
    [Tooltip("Sprite cuando la Salida es destructible (antes de romperla)")]
    public Sprite spriteDestructible;

    [Header("Puerta Destructible (solo Salida)")]
    [Tooltip("Golpes del Macahuitl necesarios para romper la Salida")]
    public int hitsToBreak = 1;
    [Tooltip("Efecto de partículas al romperse (opcional)")]
    public GameObject breakEffect;

    // ── Estado interno ──────────────────────────────────────
    private Collider2D col;
    private SpriteRenderer sr;
    private bool esDestructible = false;
    private int hitsReceived = 0;
    private bool destroyed = false;

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();

        // Estado inicial según tipo
        if (doorType == DoorType.Entrada)
            Abrir();         // La entrada empieza abierta
        else
            Bloquear();      // La salida empieza bloqueada
    }

    // ── API pública (llamada por MacahuitlRoomManager) ───────

    /// <summary>Cierra físicamente la puerta. Coloca el sprite de cerrada.</summary>
    public void Cerrar()
    {
        if (destroyed) return;
        esDestructible = false;
        if (col != null) col.enabled = true;
        SetSprite(spriteCerrada);
    }

    /// <summary>Bloquea la puerta (alias semántico de Cerrar para la Salida).</summary>
    public void Bloquear() => Cerrar();

    /// <summary>Abre la puerta: desactiva el collider y muestra sprite de abierta.</summary>
    public void Abrir()
    {
        if (destroyed) return;
        esDestructible = false;
        if (col != null) col.enabled = false;
        SetSprite(spriteAbierta);
    }

    /// <summary>
    /// Solo para la Salida: activa el modo destructible.
    /// La puerta sigue siendo físicamente sólida pero acepta golpes del Macahuitl.
    /// </summary>
    public void HacerDestructible()
    {
        if (doorType != DoorType.Salida || destroyed) return;
        esDestructible = true;
        hitsReceived = 0;
        if (col != null) col.enabled = true;
        SetSprite(spriteDestructible);
        Debug.Log("[RoomDoor] Salida ahora es destructible — golpéala con el Macahuitl.");
    }

    // ── Recibir golpe del Macahuitl ───────────────────────────

    /// <summary>
    /// RomeritoCombat llama a este método cuando el ataque del Macahuitl
    /// impacta el collider de la Salida en modo destructible.
    /// </summary>
    public void RecibirGolpe()
    {
        if (!esDestructible || destroyed) return;

        hitsReceived++;
        Debug.Log($"[RoomDoor] Golpe {hitsReceived}/{hitsToBreak} en la Salida.");

        if (hitsReceived >= hitsToBreak)
            Romper();
    }

    // ── Romper la puerta ──────────────────────────────────────

    void Romper()
    {
        destroyed = true;
        esDestructible = false;

        if (breakEffect != null)
            Instantiate(breakEffect, transform.position, Quaternion.identity);

        // Desactivar collider y sprite — la puerta ya no existe
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;

        Debug.Log("[RoomDoor] ¡Salida destruida! Romerito puede avanzar.");
    }

    // ── Utilidad ─────────────────────────────────────────────

    void SetSprite(Sprite sprite)
    {
        if (sr != null && sprite != null)
            sr.sprite = sprite;
    }

    // Gizmo para ver la puerta en el editor aunque sea invisible
    void OnDrawGizmos()
    {
        Gizmos.color = doorType == DoorType.Entrada
            ? new Color(0f, 1f, 0f, 0.4f)
            : new Color(1f, 0.3f, 0f, 0.4f);

        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}