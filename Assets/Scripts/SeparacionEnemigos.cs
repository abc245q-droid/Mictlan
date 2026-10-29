using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  SeparacionEnemigos — Evita que los enemigos se empalmen
// ============================================================
//
//  La capa Enemy NO colisiona consigo misma (matriz de física), a
//  propósito: si chocaran, se atorarían y se treparían unos encima
//  de otros. El costo: dos o tres enemigos iguales pueden terminar
//  en el mismo punto, verse como uno solo y morir con un solo golpe.
//
//  Ese empalme solo se QUEDA cuando los enemigos se mueven igual
//  (ambos quietos, ambos persiguiendo a Romerito, recién salidos del
//  mismo punto). Dos que se cruzan en sentidos opuestos se atraviesan
//  y siguen su camino, y eso está bien.
//
//  Por eso (v2):
//    • Solo se separan vecinos con velocidad PARECIDA
//      (umbralVelocidadRelativa). Los que se cruzan se ignoran: nunca
//      hay "pared" entre enemigos ni atascos.
//    • El empuje es un desplazamiento SUAVE que se suma a la velocidad
//      de la IA (no la reemplaza), y no se acumula: si la IA no
//      reescribió la velocidad en este paso (salto, knockback), se
//      retira primero el empuje del paso anterior.
//    • Con gravedad: solo horizontal y nunca hacia un precipicio.
//      Voladores (gravityScale = 0): en 2D.
//    • Los jefes no se dejan empujar; los demás sí se apartan de ellos.
//
//  Corre DESPUÉS de la IA (DefaultExecutionOrder 100).
//  SIN SETUP: EnemyDummy lo agrega solo en Start a todo enemigo con
//  Rigidbody2D dinámico. Si ya existe en el prefab, no se duplica.
// ============================================================

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Rigidbody2D))]
public class SeparacionEnemigos : MonoBehaviour
{
    [Tooltip("Velocidad del desplazamiento de separación (unidades/s). " +
             "Menor que la velocidad de patrulla para que se note suave.")]
    public float velocidadEmpuje = 1.5f;
    [Tooltip("Fracción del ancho que dos enemigos pueden solaparse sin separarse. " +
             "0 = ni rozarse; 0.35 = se permite un empalme leve.")]
    [Range(0f, 0.9f)] public float solapamientoPermitido = 0.35f;
    [Tooltip("Solo se separan vecinos cuya diferencia de velocidad sea menor que esto. " +
             "Los que se cruzan más rápido se atraviesan libremente.")]
    public float umbralVelocidadRelativa = 1f;
    [Tooltip("Capas que cuentan como suelo para no empujarlo hacia un precipicio.")]
    public LayerMask capaSuelo;

    Rigidbody2D rb;
    Collider2D cuerpo;
    EnemyDummy dummy;
    ContactFilter2D filtro;
    readonly List<Collider2D> solapados = new List<Collider2D>(8);

    Vector2 empujePrevio;     // lo que se sumó en el paso anterior
    Vector2 velocidadDejada;  // velocidad tal como quedó tras sumarlo

    // ── Alta automática desde EnemyDummy ─────────────────────

    /// <summary>Agrega la separación si el enemigo es apto y aún no la tiene.</summary>
    public static void Asegurar(EnemyDummy enemigo)
    {
        if (enemigo == null || enemigo.esJefe) return;
        if (enemigo.GetComponent<SeparacionEnemigos>() != null) return;
        var cuerpoRigido = enemigo.GetComponent<Rigidbody2D>();
        if (cuerpoRigido == null || cuerpoRigido.bodyType != RigidbodyType2D.Dynamic) return;
        enemigo.gameObject.AddComponent<SeparacionEnemigos>();
    }

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        dummy = GetComponent<EnemyDummy>();
        cuerpo = BuscarCuerpoSolido();

        if (capaSuelo.value == 0)
            capaSuelo = LayerMask.GetMask("Suelo");

        filtro = new ContactFilter2D();
        filtro.SetLayerMask(1 << gameObject.layer);   // solo enemigos de la misma capa
        filtro.useTriggers = false;
    }

    void OnDisable()
    {
        empujePrevio = Vector2.zero;
    }

    void FixedUpdate()
    {
        bool conGravedad = rb.gravityScale > 0.01f;

        // 1. Velocidad "propia" de la IA, sin el empuje del paso anterior.
        Vector2 v = RetirarEmpujePrevio(rb.linearVelocity, conGravedad);

        // 2. Nuevo empuje.
        Vector2 empuje = Vector2.zero;
        if (cuerpo != null && cuerpo.enabled &&
            rb.bodyType == RigidbodyType2D.Dynamic &&
            (dummy == null || !dummy.esJefe))
        {
            empuje = CalcularEmpuje(v, conGravedad);
        }

        // 3. Aplicar (solo si algo cambió, para no tocar la física de más).
        if (empuje == Vector2.zero && empujePrevio == Vector2.zero) return;

        rb.linearVelocity = v + empuje;
        velocidadDejada = rb.linearVelocity;
        empujePrevio = empuje;
    }

    // ── Internos ─────────────────────────────────────────────

    // Si la IA reescribió la velocidad en este paso (patrulla, sprint...),
    // el empuje anterior ya no está en ella. Si NO la reescribió (salto
    // balístico, knockback), sigue ahí: lo quitamos para que no se acumule.
    Vector2 RetirarEmpujePrevio(Vector2 v, bool conGravedad)
    {
        if (empujePrevio == Vector2.zero) return v;

        float tolerancia = empujePrevio.magnitude * 0.5f;
        bool sinReescribir = conGravedad
            ? Mathf.Abs(v.x - velocidadDejada.x) < tolerancia          // la gravedad solo cambia Y
            : (v - velocidadDejada).magnitude < tolerancia;

        if (sinReescribir) v -= empujePrevio;
        empujePrevio = Vector2.zero;
        return v;
    }

    Vector2 CalcularEmpuje(Vector2 vPropia, bool conGravedad)
    {
        int n = cuerpo.Overlap(filtro, solapados);
        if (n == 0) return Vector2.zero;

        Bounds mio = cuerpo.bounds;
        Vector2 suma = Vector2.zero;

        for (int i = 0; i < n; i++)
        {
            Collider2D otro = solapados[i];
            if (otro == null) continue;
            Rigidbody2D rbOtro = otro.attachedRigidbody;
            if (rbOtro == null || rbOtro == rb) continue;
            // Solo enemigos de verdad (no proyectiles ni hazards en la capa Enemy).
            if (!rbOtro.TryGetComponent(out EnemyDummy _)) continue;

            // Se están cruzando → que se atraviesen, sin estorbarse.
            Vector2 vRel = vPropia - rbOtro.linearVelocity;
            float rapidezRelativa = conGravedad ? Mathf.Abs(vRel.x) : vRel.magnitude;
            if (rapidezRelativa > umbralVelocidadRelativa) continue;

            Bounds suyo = otro.bounds;
            Vector2 delta = (Vector2)(mio.center - suyo.center);

            if (conGravedad)
            {
                float permitido = (mio.extents.x + suyo.extents.x) * (1f - solapamientoPermitido);
                float distancia = Mathf.Abs(delta.x);
                if (distancia >= permitido) continue;
                suma.x += Desempate(delta.x, rbOtro) * (1f - distancia / Mathf.Max(permitido, 0.0001f));
            }
            else
            {
                float radioMio = Mathf.Max(mio.extents.x, mio.extents.y);
                float radioSuyo = Mathf.Max(suyo.extents.x, suyo.extents.y);
                float permitido = (radioMio + radioSuyo) * (1f - solapamientoPermitido);
                float distancia = delta.magnitude;
                if (distancia >= permitido) continue;
                Vector2 dir = distancia > 0.0001f ? delta / distancia
                                                  : new Vector2(Desempate(0f, rbOtro), 0f);
                suma += dir * (1f - distancia / Mathf.Max(permitido, 0.0001f));
            }
        }

        if (suma == Vector2.zero) return Vector2.zero;

        float intensidad = Mathf.Clamp01(suma.magnitude);
        Vector2 direccion = suma.normalized;

        if (conGravedad)
        {
            direccion = new Vector2(Mathf.Sign(direccion.x), 0f);
            if (HayPrecipicio(direccion.x)) return Vector2.zero;
        }

        return direccion * (velocidadEmpuje * intensidad);
    }

    // Empalme perfecto (mismo X): decide por ID, uno a cada lado, siempre igual.
    float Desempate(float dx, Rigidbody2D otro)
    {
        if (Mathf.Abs(dx) > 0.01f) return Mathf.Sign(dx);
        return gameObject.GetInstanceID() > otro.gameObject.GetInstanceID() ? 1f : -1f;
    }

    bool HayPrecipicio(float dir)
    {
        Bounds b = cuerpo.bounds;
        Vector2 origen = new Vector2(b.center.x + dir * (b.extents.x + 0.05f), b.min.y + 0.05f);
        return !Physics2D.Raycast(origen, Vector2.down, 0.5f, capaSuelo);
    }

    Collider2D BuscarCuerpoSolido()
    {
        foreach (var c in GetComponents<Collider2D>())
            if (!c.isTrigger) return c;
        return null;
    }
}
