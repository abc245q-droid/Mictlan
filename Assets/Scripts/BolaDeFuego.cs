using UnityEngine;

// ============================================================
//  BolaDeFuego — Habilidad 1 del Favor de Huehueteotl
// ============================================================
//
//  Proyectil que viaja en línea recta en la dirección de la
//  mirada de Romerito. Daña enemigos (EnemyDummy) y objetos
//  destruibles (con GodFavor.Huehueteotl — las puertas/paredes
//  que solo ceden al fuego reaccionan aquí). Se destruye al
//  impactar algo sólido o al agotar su vida útil.
//
//  SETUP DEL PREFAB:
//    • Sprite de la bola de fuego (SpriteRenderer).
//    • Rigidbody2D → Body Type: Kinematic (se mueve por velocidad,
//      sin gravedad ni empujones).
//    • Collider2D → Is Trigger: TRUE.
//    • Layer del prefab: una que NO colisione consigo misma ni con
//      el Player (p. ej. una layer "PlayerProjectile" con
//      Player × PlayerProjectile desmarcado en la matriz; si no
//      existe, el filtro por tag de abajo también lo protege).
//    • capasImpacto: Enemy + Suelo + Destructible.
//    • (Opcional) hijo con luz/glow usando Mictlan/SpriteGlowHDR —
//      la bola de fuego DEBE ser una fuente cálida que viaja por
//      la oscuridad del Mictlán.
//
// ============================================================

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BolaDeFuego : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad horizontal del proyectil (m/s).")]
    public float velocidad = 12f;
    [Tooltip("Segundos de vida antes de autodestruirse (si no impacta nada).")]
    public float vidaUtil = 2.5f;

    [Header("Daño")]
    [Tooltip("Daño a enemigos y destruibles.")]
    public int dano = 2;

    [Header("Impacto")]
    [Tooltip("Capas contra las que el proyectil explota: Enemy + Suelo + Destructible.")]
    public LayerMask capasImpacto;
    [Tooltip("Prefab del efecto de explosión/chispas al impactar (opcional).")]
    public GameObject efectoImpacto;
    [Tooltip("Segundos que vive el efecto de impacto antes de destruirse.")]
    public float duracionEfecto = 0.6f;

    // ── Internos ─────────────────────────────────────────────
    private Rigidbody2D rb;
    private int direccion = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    void Start()
    {
        Destroy(gameObject, vidaUtil);
    }

    /// <summary>
    /// Dispara el proyectil. Llamado por FavorManager justo tras instanciar.
    /// </summary>
    /// <param name="dir">+1 = derecha, -1 = izquierda (mirada de Romerito).</param>
    public void Lanzar(int dir)
    {
        direccion = (dir >= 0) ? 1 : -1;
        rb.linearVelocity = new Vector2(direccion * velocidad, 0f);

        // Voltear el sprite conservando la escala original.
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * direccion;
        transform.localScale = s;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Nunca reaccionar al propio Romerito (nace en su mano) ni a
        // otros triggers (zonas de diálogo, checkpoints, orbes, etc.).
        if (other.CompareTag("Player") || other.isTrigger) return;

        bool impacto = false;

        // ── Caso A: Enemigo ──────────────────────────────────
        EnemyDummy enemigo = other.GetComponent<EnemyDummy>();
        if (enemigo != null)
        {
            enemigo.TakeDamage(dano);
            impacto = true;
        }

        // ── Caso B: Objeto destruible (fuego abre lo que el filo no) ──
        ObjetoDestruible destruible = other.GetComponent<ObjetoDestruible>();
        if (destruible != null)
        {
            destruible.RecibirGolpe(dano, RomeritoCombat.GodFavor.Huehueteotl);
            impacto = true;
        }

        // ── Caso C: Sólido genérico (suelo/pared) ────────────
        if (!impacto && EstaEnCapas(other.gameObject.layer, capasImpacto))
            impacto = true;

        if (impacto) Explotar();
    }

    private void Explotar()
    {
        if (efectoImpacto != null)
        {
            GameObject fx = Instantiate(efectoImpacto, transform.position, Quaternion.identity);
            Destroy(fx, duracionEfecto);
        }
        Destroy(gameObject);
    }

    private static bool EstaEnCapas(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
