using UnityEngine;

// ============================================================
//  NubeHumoChile — Ataque "humadera" de la Rana Sahumadora
// ============================================================
//
//  Un sprite de nube que aparece rápido, crece un poco, sube
//  despacio y se desvanece. Solo hace daño durante la PRIMERA parte
//  de su vida (fraccionDanina): cuando ya se ve tenue, ya no lastima.
//  Que el humo dañe hasta volverse invisible se siente injusto.
//
//  El color lo trae el arte (gris acre con base de óxido). Este
//  script solo anima el alpha de SpriteRenderer.color.
//
//  SETUP EN UNITY (prefab):
//  ─────────────────────────────────────────────────────────────
//  1. Sprite de la nube con PIVOTE en la base al centro
//     (Sprite Editor ▸ Pivot: Bottom). Así nace de la boca de la
//     rana y crece hacia arriba.
//  2. SpriteRenderer con Sorting Order por ENCIMA de la rana.
//  3. Collider2D (Box o Capsule) con Is Trigger = true, ajustado
//     SOLO a la masa densa (~70% del ancho, sin las volutas de
//     arriba). Asígnalo en 'zonaDanio'.
//  4. Layer que colisione con la de Romerito (p. ej. "Enemy" o
//     "Default"; NO "Suelo").
//  ─────────────────────────────────────────────────────────────
//
// ============================================================

[RequireComponent(typeof(SpriteRenderer))]
public class NubeHumoChile : MonoBehaviour
{
    [Header("Daño")]
    public int danio = 1;
    [Tooltip("Trigger del área dañina. Vacío = el primer Collider2D del objeto.")]
    public Collider2D zonaDanio;
    [Tooltip("Fracción de la vida de la nube durante la cual hace daño (0–1).")]
    [Range(0f, 1f)] public float fraccionDanina = 0.5f;

    [Header("Tiempo")]
    [Tooltip("Vida total de la nube en segundos.")]
    public float duracion = 2.2f;
    [Tooltip("Segundos del fade-in inicial (aparición casi instantánea).")]
    public float tiempoAparicion = 0.15f;
    [Tooltip("Forma del desvanecimiento después de aparecer (1 → 0).")]
    public AnimationCurve curvaDesvanecer = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Movimiento")]
    [Tooltip("Escala relativa al nacer (se multiplica por la escala del prefab).")]
    public float escalaInicial = 0.75f;
    [Tooltip("Escala relativa al final.")]
    public float escalaFinal = 1.3f;
    [Tooltip("Unidades que sube la nube a lo largo de su vida.")]
    public float subida = 0.5f;

    private SpriteRenderer sr;
    private Vector3 escalaBase;
    private Vector3 posInicial;
    private Color colorBase;
    private float t;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (zonaDanio == null) zonaDanio = GetComponent<Collider2D>();
        escalaBase = transform.localScale;
        colorBase = sr.color;
    }

    void Start()
    {
        posInicial = transform.position;
        Aplicar(0f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float n = duracion > 0f ? Mathf.Clamp01(t / duracion) : 1f;
        Aplicar(n);

        if (zonaDanio != null && zonaDanio.enabled && n >= fraccionDanina)
            zonaDanio.enabled = false;

        if (t >= duracion)
            Destroy(gameObject);
    }

    private void Aplicar(float n)
    {
        // Alpha: aparición rápida, luego desvanecimiento lento
        float alpha;
        if (t < tiempoAparicion)
            alpha = tiempoAparicion > 0f ? t / tiempoAparicion : 1f;
        else
        {
            float resto = Mathf.Max(0.0001f, duracion - tiempoAparicion);
            alpha = curvaDesvanecer.Evaluate(Mathf.Clamp01((t - tiempoAparicion) / resto));
        }

        Color c = colorBase;
        c.a = colorBase.a * alpha;
        sr.color = c;

        // Crece y sube (ease-out: se expande rápido y luego flota)
        float e = 1f - (1f - n) * (1f - n);
        transform.localScale = escalaBase * Mathf.Lerp(escalaInicial, escalaFinal, e);
        transform.position = posInicial + Vector3.up * (subida * e);
    }

    // Stay además de Enter: si Romerito entra durante i-frames o con la
    // Barrera de Copal activa, Enter no se repite (mismo criterio que Trampa).
    void OnTriggerEnter2D(Collider2D other) => Contacto(other);
    void OnTriggerStay2D(Collider2D other) => Contacto(other);

    private void Contacto(Collider2D other)
    {
        // Solo el collider físico de Romerito (no sus triggers internos)
        if (!other.CompareTag("Player") || other.isTrigger) return;

        RomeritoHealth health = other.GetComponent<RomeritoHealth>();
        if (health == null || !health.PuedeRecibirDano) return;

        health.TakeDamage(danio);
    }
}
