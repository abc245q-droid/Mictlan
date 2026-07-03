using UnityEngine;
using System.Collections;

// ============================================================
//  BarreraCopal — Habilidad 2 del Favor de Huehueteotl
// ============================================================
//
//  Fundamento (lore): el copal es humo ritual — el sahumerio que
//  vela y comunica. Romerito se envuelve en una humareda que lo
//  camufla ante el Mictlán... pero el humo solo lo cubre si él
//  se queda QUIETO dentro de la columna: al moverse, la rompe.
//
//  REGLAS:
//    • Dura 'duracion' segundos (5 por defecto).
//    • El sprite de humo está visible TODO el tiempo de la barrera.
//    • La invulnerabilidad SOLO aplica si Romerito está quieto y
//      tocando el suelo. Feedback: humo denso (protegido) vs humo
//      tenue (el efecto no cubre).
//    • No apilable: FavorManager consulta EstaActiva antes de cobrar.
//
//  SETUP EN UNITY:
//    • Añadir este componente a Romerito (junto a FavorManager).
//    • Crear un hijo "HumoCopal" con un SpriteRenderer del humo
//      (sprite gris-azulado, semitransparente, ordenado ENCIMA del
//      sprite de Romerito en Order in Layer). Desactivado por defecto.
//    • Asignar ese SpriteRenderer en 'spriteHumo'.
//
//  NOTA DE DISEÑO (futuro): cuando el "camuflaje" evolucione,
//  la propiedad estática 'CamuflajeActivo' ya queda expuesta para
//  que los ChequearJugador de MictecahBase / FlyingEnemyAI ignoren
//  a Romerito mientras esté protegido. Por ahora solo otorga
//  invulnerabilidad — un cambio a la vez, una rebanada a la vez.
//
// ============================================================

public class BarreraCopal : MonoBehaviour
{
    [Header("Duración")]
    [Tooltip("Segundos que dura la humareda de copal.")]
    public float duracion = 5f;

    [Header("Condición de efecto")]
    [Tooltip("Velocidad máxima (m/s) para considerarse 'quieto'.")]
    public float deadzoneVelocidad = 0.2f;

    [Header("Visual")]
    [Tooltip("SpriteRenderer hijo con el humo. Se activa durante la barrera.")]
    public SpriteRenderer spriteHumo;
    [Tooltip("Alpha del humo cuando la protección ESTÁ cubriendo (quieto + suelo).")]
    [Range(0f, 1f)] public float alphaProtegido = 0.85f;
    [Tooltip("Alpha del humo cuando la protección NO cubre (moviéndose / en el aire).")]
    [Range(0f, 1f)] public float alphaSinEfecto = 0.3f;
    [Tooltip("Velocidad de transición del alpha entre ambos estados.")]
    public float velocidadFade = 6f;

    // ── Estado público ───────────────────────────────────────
    public bool EstaActiva { get; private set; }

    /// <summary>
    /// true mientras la barrera está cubriendo de verdad (quieto + suelo).
    /// Expuesto para el futuro sistema de camuflaje ante enemigos.
    /// </summary>
    public static bool CamuflajeActivo { get; private set; }

    // ── Referencias internas ─────────────────────────────────
    private RomeritoMovement movement;
    private RomeritoHealth health;
    private Rigidbody2D rb;
    private Coroutine rutina;

    void Awake()
    {
        movement = GetComponent<RomeritoMovement>();
        health = GetComponent<RomeritoHealth>();
        rb = GetComponent<Rigidbody2D>();

        if (spriteHumo != null)
            spriteHumo.gameObject.SetActive(false);
    }

    /// <summary>Enciende la humareda. Llamado por FavorManager (ya cobró el Tonalli).</summary>
    public void Activar()
    {
        if (EstaActiva) return;
        rutina = StartCoroutine(RutinaBarrera());
    }

    private IEnumerator RutinaBarrera()
    {
        EstaActiva = true;

        if (spriteHumo != null)
        {
            spriteHumo.gameObject.SetActive(true);
            SetAlpha(alphaSinEfecto); // arranca tenue; se densifica al quedarse quieto
        }

        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;

            // ¿El humo lo cubre? Quieto (X e Y) y tocando el suelo.
            bool quieto = rb == null ||
                          (Mathf.Abs(rb.linearVelocity.x) < deadzoneVelocidad &&
                           Mathf.Abs(rb.linearVelocity.y) < deadzoneVelocidad);
            bool enSuelo = movement != null && movement.isGrounded;
            bool cubierto = quieto && enSuelo;

            CamuflajeActivo = cubierto;
            if (health != null) health.SetInvulnerabilidadExterna(cubierto);

            // Feedback: humo denso ↔ tenue según cobertura real.
            if (spriteHumo != null)
            {
                float targetAlpha = cubierto ? alphaProtegido : alphaSinEfecto;
                Color c = spriteHumo.color;
                c.a = Mathf.Lerp(c.a, targetAlpha, velocidadFade * Time.deltaTime);
                spriteHumo.color = c;
            }

            yield return null;
        }

        Terminar();
    }

    private void Terminar()
    {
        EstaActiva = false;
        CamuflajeActivo = false;
        if (health != null) health.SetInvulnerabilidadExterna(false);
        if (spriteHumo != null) spriteHumo.gameObject.SetActive(false);
        rutina = null;
    }

    private void SetAlpha(float a)
    {
        if (spriteHumo == null) return;
        Color c = spriteHumo.color;
        c.a = a;
        spriteHumo.color = c;
    }

    // Seguridad: si Romerito muere / se desactiva a media barrera,
    // NUNCA dejar la invulnerabilidad externa encendida.
    void OnDisable()
    {
        if (rutina != null) StopCoroutine(rutina);
        Terminar();
    }
}
