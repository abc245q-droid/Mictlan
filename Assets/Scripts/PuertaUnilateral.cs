using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PuertaUnilateral — Barrera rompible de un solo lado
//  PUERTA_UNILATERAL_V2 — soporte para trampilla pisable
// ============================================================
//
//  Sistema de shortcuts estilo Dark Souls / Hollow Knight.
//  Bloquea el paso desde ambos lados, pero solo puede romperse
//  atacándola desde uno específico (Izquierdo/Derecho/Arriba/Abajo).
//
//  DIÉGESIS:
//  Sellos rituales (tlaquimilolli) fijados desde un solo lado.
//  Cuerdas de maguey visibles → Macahuitl las corta. Del otro
//  lado solo se ve la piedra lisa: el sello es inalcanzable.
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. Crea un GameObject "Puerta_Shortcut_XXX" en la escena.
//  2. Añádele:
//       • Este script (PuertaUnilateral)
//       • Un Collider2D (NO trigger) — la barrera física
//       • Un SpriteRenderer con el visual de la puerta sellada
//       • Un AudioSource (opcional)
//  3. Layer del GameObject = "Destructible" (o la que uses en
//     RomeritoCombat.destructibleLayer).
//  4. En el Inspector:
//       • puertaID          → ID único global, obligatorio
//       • ladoRompible      → desde qué lado se rompe
//       • hitsToBreak       → cuántos golpes aguanta
//       • esPisable         → ★ NUEVO: marcar en true si la puerta
//                             está al nivel del piso y Romerito
//                             debe poder caminar sobre ella
//       • capaSueloNombre   → nombre de la capa de suelo (default "Suelo")
//  ─────────────────────────────────────────────────────────────
//
//  ESPISABLE (v2):
//  Cuando la puerta actúa como trampilla horizontal (típicamente
//  ladoRompible=Arriba), su collider vive en layer "Destructible"
//  para que el combate lo detecte, pero el ground-check del
//  movimiento solo mira layer "Suelo". Con esPisable=true, la
//  puerta genera automáticamente un GameObject hijo con un collider
//  clonado en layer "Suelo" — así Romerito puede pararse encima y
//  hacer el pogo hacia abajo. Al romperse, el hijo también se
//  desactiva y Romerito cae natural por la trampilla abierta.
//
//  PERSISTENCIA:
//  Al romperse, agrega su puertaID a PlayerData.collectedItems y
//  guarda partida. Al cargar la escena, si el ID ya está, la puerta
//  se autodestruye.
//
// ============================================================

[RequireComponent(typeof(Collider2D))]
public class PuertaUnilateral : MonoBehaviour
{
    public enum Lado { Izquierdo, Derecho, Arriba, Abajo }

    // ── Inspector ────────────────────────────────────────────

    [Header("Identidad (¡obligatorio para persistencia!)")]
    [Tooltip("ID único global de esta puerta. Ej: 'puerta_atlein_shortcut_01'.")]
    public string puertaID = "";

    [Header("Lado Rompible")]
    [Tooltip("Desde qué lado se puede romper. Los golpes del otro lado rebotan.")]
    public Lado ladoRompible = Lado.Izquierdo;

    [Header("Resistencia")]
    [Tooltip("Golpes del Macahuitl necesarios para romperla desde el lado correcto.")]
    [Min(1)] public int hitsToBreak = 1;

    [Header("Trampilla Pisable (para puertas horizontales al piso)")]
    [Tooltip("Si esta puerta está al nivel del piso y Romerito debe poder " +
             "caminar sobre ella, marca esto en true. Se genera un collider " +
             "hijo en la capa de suelo para que el ground-check lo detecte. " +
             "Típicamente usado con ladoRompible=Arriba (pogo desde arriba).")]
    public bool esPisable = false;

    [Tooltip("Nombre de la capa de suelo del juego (case-sensitive). " +
             "Solo relevante si esPisable=true. Default: 'Suelo'.")]
    public string capaSueloNombre = "Suelo";

    [Header("Visual")]
    [Tooltip("Sprite de la puerta sellada (opcional).")]
    public Sprite spriteBloqueada;

    [Header("Feedback Visual")]
    [Tooltip("Efecto al romperse.")]
    public GameObject breakEffect;

    [Tooltip("Efecto al golpear desde el lado equivocado.")]
    public GameObject reboteEffect;

    [Header("Feedback Sonoro")]
    [Tooltip("Sonido al romperse.")]
    public AudioClip sonidoRoto;

    [Tooltip("Sonido de rebote al golpear desde el lado equivocado.")]
    public AudioClip sonidoRebote;

    // ── Estado interno ───────────────────────────────────────
    private int hitsRecibidos = 0;
    private bool destruida = false;
    private Collider2D col;
    private SpriteRenderer sr;
    private AudioSource audioSource;
    private GameObject helperPisable;   // ★ v2: hijo con collider en capa Suelo

    // ── Unity ────────────────────────────────────────────────

    void Start()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        if (string.IsNullOrEmpty(puertaID))
        {
            Debug.LogWarning(
                $"[PuertaUnilateral] '{name}' no tiene puertaID. " +
                "No persistirá el estado de rotura entre sesiones.",
                this);
        }

        // Persistencia: si ya fue rota antes, autodestruirse.
        if (!string.IsNullOrEmpty(puertaID) &&
            GameManager01.instance != null &&
            GameManager01.instance.currentData != null &&
            GameManager01.instance.currentData.collectedItems != null &&
            GameManager01.instance.currentData.collectedItems.Contains(puertaID))
        {
            Destroy(gameObject);
            return;
        }

        if (spriteBloqueada != null && sr != null)
            sr.sprite = spriteBloqueada;

        ConfigurarPisableSiCorresponde();
    }

    // ── API — llamado por RomeritoCombat ─────────────────────

    /// <summary>
    /// Recibe un golpe desde la posición del atacante. La puerta decide
    /// si acepta el golpe según el lado desde el que viene.
    /// </summary>
    public void RecibirGolpe(Vector2 posicionAtacante)
    {
        if (destruida) return;

        if (!ViendoLadoCorrecto(posicionAtacante))
        {
            Rebotar();
            return;
        }

        hitsRecibidos++;
        Debug.Log($"[PuertaUnilateral] {puertaID} — golpe {hitsRecibidos}/{hitsToBreak}");

        if (hitsRecibidos >= hitsToBreak)
            Romper();
    }

    // ── Lógica interna ───────────────────────────────────────

    private bool ViendoLadoCorrecto(Vector2 posicionAtacante)
    {
        Vector2 delta = posicionAtacante - (Vector2)transform.position;
        switch (ladoRompible)
        {
            case Lado.Izquierdo: return delta.x < 0;
            case Lado.Derecho:   return delta.x > 0;
            case Lado.Abajo:     return delta.y < 0;
            case Lado.Arriba:    return delta.y > 0;
            default: return false;
        }
    }

    private void Rebotar()
    {
        if (reboteEffect != null)
            Instantiate(reboteEffect, transform.position, Quaternion.identity);

        if (sonidoRebote != null && audioSource != null)
            audioSource.PlayOneShot(sonidoRebote);

        Debug.Log($"[PuertaUnilateral] {puertaID} — el sello está del otro lado.");
    }

    private void Romper()
    {
        destruida = true;

        // Persistir en el save.
        if (!string.IsNullOrEmpty(puertaID) &&
            GameManager01.instance != null &&
            GameManager01.instance.currentData != null)
        {
            if (GameManager01.instance.currentData.collectedItems == null)
                GameManager01.instance.currentData.collectedItems = new List<string>();

            if (!GameManager01.instance.currentData.collectedItems.Contains(puertaID))
                GameManager01.instance.currentData.collectedItems.Add(puertaID);

            GameManager01.instance.SaveGame();
        }

        if (breakEffect != null)
            Instantiate(breakEffect, transform.position, Quaternion.identity);

        if (sonidoRoto != null && audioSource != null)
            audioSource.PlayOneShot(sonidoRoto);

        // Desactivar collider del padre y sprite.
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;

        // ★ v2: desactivar también el helper pisable — así Romerito
        // cae natural por la trampilla abierta.
        if (helperPisable != null) helperPisable.SetActive(false);

        Debug.Log($"[PuertaUnilateral] {puertaID} ¡rota! Atajo abierto.");
    }

    // ── Trampilla pisable (v2) ───────────────────────────────

    private void ConfigurarPisableSiCorresponde()
    {
        if (!esPisable) return;

        int idxCapaSuelo = LayerMask.NameToLayer(capaSueloNombre);
        if (idxCapaSuelo < 0)
        {
            Debug.LogWarning(
                $"[PuertaUnilateral] '{name}': no existe la capa " +
                $"'{capaSueloNombre}'. esPisable no funcionará. " +
                "Revisa Project Settings > Tags and Layers.");
            return;
        }

        helperPisable = new GameObject("PisableHelper");
        helperPisable.transform.SetParent(transform, worldPositionStays: false);
        helperPisable.transform.localPosition = Vector3.zero;
        helperPisable.transform.localRotation = Quaternion.identity;
        helperPisable.transform.localScale    = Vector3.one;
        helperPisable.layer = idxCapaSuelo;

        // Clonar el collider del padre en el hijo.
        if (col is BoxCollider2D box)
        {
            BoxCollider2D hijo = helperPisable.AddComponent<BoxCollider2D>();
            hijo.offset = box.offset;
            hijo.size   = box.size;
        }
        else if (col is CircleCollider2D circ)
        {
            CircleCollider2D hijo = helperPisable.AddComponent<CircleCollider2D>();
            hijo.offset = circ.offset;
            hijo.radius = circ.radius;
        }
        else if (col is CapsuleCollider2D cap)
        {
            CapsuleCollider2D hijo = helperPisable.AddComponent<CapsuleCollider2D>();
            hijo.offset    = cap.offset;
            hijo.size      = cap.size;
            hijo.direction = cap.direction;
        }
        else
        {
            // Fallback: BoxCollider2D con bounds del padre.
            BoxCollider2D hijo = helperPisable.AddComponent<BoxCollider2D>();
            Bounds worldBounds = col.bounds;
            Vector3 lossy = transform.lossyScale;
            hijo.size = new Vector2(
                worldBounds.size.x / Mathf.Max(0.0001f, lossy.x),
                worldBounds.size.y / Mathf.Max(0.0001f, lossy.y));
            Vector3 centerLocal = transform.InverseTransformPoint(worldBounds.center);
            hijo.offset = new Vector2(centerLocal.x, centerLocal.y);
            Debug.LogWarning(
                $"[PuertaUnilateral] '{name}': collider tipo " +
                $"{col.GetType().Name} — usando BoxCollider2D aproximado " +
                "para el helper pisable.");
        }
    }

    // ── Gizmos — vitales para el level design ────────────────

    void OnDrawGizmos()
    {
        Vector3 pos = transform.position;
        Vector3 escala = transform.localScale;

        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        Gizmos.DrawCube(pos, escala);

        Vector3 dir;
        float extent;
        switch (ladoRompible)
        {
            case Lado.Izquierdo: dir = Vector3.left;  extent = escala.x * 0.5f; break;
            case Lado.Derecho:   dir = Vector3.right; extent = escala.x * 0.5f; break;
            case Lado.Abajo:     dir = Vector3.down;  extent = escala.y * 0.5f; break;
            case Lado.Arriba:    dir = Vector3.up;    extent = escala.y * 0.5f; break;
            default:             dir = Vector3.right; extent = escala.x * 0.5f; break;
        }

        // Bola verde del lado rompible.
        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.9f);
        Vector3 origenVerde = pos + dir * (extent + 0.4f);
        Gizmos.DrawSphere(origenVerde, 0.25f);
        Gizmos.DrawLine(origenVerde, origenVerde + dir * 0.5f);

        // Círculo rojo del lado bloqueado.
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
        Vector3 origenRojo = pos - dir * (extent + 0.4f);
        Gizmos.DrawWireSphere(origenRojo, 0.22f);

        // Marca visual para trampillas pisables.
        if (esPisable)
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireCube(pos + Vector3.up * (escala.y * 0.5f + 0.1f),
                                new Vector3(escala.x, 0.05f, 0f));
        }
    }
}
