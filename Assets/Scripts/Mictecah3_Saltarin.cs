using UnityEngine;

// ============================================================
//  Mictecah 3 — El Saltarín (Torpe y lento)
// ============================================================
//
//  Comportamiento:
//    • Patrulla normal (pared + precipicio).
//    • Al detectar a Romerito: lo persigue dando SALTOS torpes y lentos.
//    • Entre salto y salto hay una pausa larga (sensación de torpeza).
//    • IGNORA precipicios durante la persecución (los salta).
//    • Sigue respetando paredes (un salto no atraviesa un muro sólido,
//      pero puede saltar por encima de obstáculos bajos según tu nivel).
//    • Retrocede brevemente al recibir daño.
//
//  Técnica: el salto es un IMPULSO único y luego pura física
//  (controlVelocidad = false). Nadie toca la velocidad en el aire.
//
//  [SALTO-NATURAL] Correcciones respecto a la versión anterior:
//    • El suelo se detecta por CONTACTO real del collider (normal hacia
//      arriba), no por el rayo groundedRayLength. El rayo seguía tocando
//      el piso en los primeros frames del despegue → Saltar() se repetía
//      cada frame y anulaba la gravedad (subida lineal, "trazada"). Y en
//      la caída tocaba el piso ANTES de aterrizar → se devolvía el control
//      en el aire, la velocidad X se ponía en 0 y caía en vertical.
//    • Un salto no puede volver a dispararse hasta aterrizar.
//    • Durante el salto se anula el linearDamping del Rigidbody (existe
//      para que decaiga el knockback, pero en un salto frena el arco).
//    • Gravedad extra al caer: sube con impulso, cae con peso.
//
// ============================================================

public class Mictecah3_Saltarin : MictecahBase
{
    [Header("── Salto (Mictecah 3) ──")]
    [Tooltip("Empuje horizontal de cada salto hacia el jugador.")]
    public float jumpHorizontal = 4f;
    [Tooltip("Empuje vertical de cada salto.")]
    public float jumpVertical = 8f;
    [Tooltip("Pausa en el suelo entre saltos. Súbelo para que sea más torpe/lento.")]
    public float jumpCooldown = 1.2f;
    [Tooltip("Pequeña pausa al aterrizar antes de poder volver a saltar.")]
    public float landRecovery = 0.25f;

    [Header("── Sensación del salto ──")]
    [Tooltip("Multiplica la gravedad mientras CAE. 1 = parábola simétrica; " +
             "1.4–1.8 = sube con impulso y cae con peso.")]
    public float multiplicadorGravedadCaida = 1.6f;
    [Tooltip("Anula el linearDamping del Rigidbody durante el salto para que " +
             "el arco no se frene en el aire. Se restaura al aterrizar o al ser golpeado.")]
    public bool anularAmortiguacionEnSalto = true;

    // Tiempo mínimo en el aire antes de aceptar un aterrizaje: el contacto
    // con el suelo del frame del despegue sigue reportándose un paso de física.
    const float TiempoMinimoVuelo = 0.08f;

    private float jumpTimer;
    private bool enSalto;
    private float tiempoEnSalto;

    private float gravedadBase;
    private float amortiguacionBase;
    private ContactFilter2D filtroSuelo;

    // ── Ciclo de vida ────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        gravedadBase      = rb.gravityScale;
        amortiguacionBase = rb.linearDamping;

        // Solo cuentan los contactos con el suelo cuya normal apunta hacia
        // arriba (45°–135°): una pared lateral NO es aterrizar.
        filtroSuelo = new ContactFilter2D();
        filtroSuelo.SetLayerMask(groundLayer);
        filtroSuelo.SetNormalAngle(45f, 135f);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // Al dispersarse a medio salto, deja la física como estaba para el respawn.
        TerminarSalto(devolverControl: false);
    }

    private bool TocandoSuelo => rb.IsTouching(filtroSuelo);

    // ── Persecución ──────────────────────────────────────────

    protected override void IniciarPersecucion()
    {
        estado = Estado.Persiguiendo;
        jumpTimer = jumpCooldown * 0.5f;  // reacción inicial algo más rápida
    }

    protected override void LogicaPersecucion()
    {
        // En el aire manda la física. El aterrizaje se resuelve en FixedUpdate.
        if (enSalto) return;

        // Cayendo sin haber saltado (p. ej. desde una orilla): que termine de caer.
        if (!TocandoSuelo) return;

        // ¿Salió de rango? Volver a patrullar.
        if (DistanciaAlJugador() > stopChaseRange)
        {
            controlVelocidad = true;
            targetVelocity = Vector2.zero;
            estado = Estado.Patrullando;
            return;
        }

        // En el suelo: quieto y mirando al jugador, esperando el siguiente salto.
        MirarHacia(DireccionAlJugador());
        controlVelocidad = true;
        targetVelocity = Vector2.zero;

        jumpTimer -= Time.deltaTime;
        if (jumpTimer <= 0f)
            Saltar();
    }

    private void Saltar()
    {
        int dir = DireccionAlJugador();
        MirarHacia(dir);

        enSalto = true;
        tiempoEnSalto = 0f;
        controlVelocidad = false;   // FixedUpdate de la base ya no toca la velocidad

        rb.gravityScale = gravedadBase;
        if (anularAmortiguacionEnSalto) rb.linearDamping = 0f;

        // Impulso único. Desde aquí, solo gravedad.
        rb.linearVelocity = new Vector2(dir * jumpHorizontal, jumpVertical);
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!enSalto || estado == Estado.Herido) return;

        tiempoEnSalto += Time.fixedDeltaTime;
        float vy = rb.linearVelocity.y;

        // Sube con la gravedad normal; cae con gravedad extra.
        rb.gravityScale = vy < 0f ? gravedadBase * multiplicadorGravedadCaida : gravedadBase;

        if (tiempoEnSalto >= TiempoMinimoVuelo && vy <= 0.01f && TocandoSuelo)
            Aterrizar();
    }

    private void Aterrizar()
    {
        TerminarSalto(devolverControl: true);
        jumpTimer = jumpCooldown + landRecovery;
    }

    private void TerminarSalto(bool devolverControl)
    {
        if (rb != null)
        {
            rb.gravityScale  = gravedadBase;
            rb.linearDamping = amortiguacionBase;
        }
        enSalto = false;

        // Aterriza en cualquier estado salvo Herido (p. ej. Esperando por el copal).
        if (devolverControl && estado != Estado.Herido)
        {
            controlVelocidad = true;
            targetVelocity = Vector2.zero;
        }
    }

    // Si lo golpean en pleno salto, la base lo pasa a Herido. Restauramos la
    // física ANTES del knockback (que depende del linearDamping para decaer).
    protected override void OnHerido()
    {
        TerminarSalto(devolverControl: false);
        jumpTimer = jumpCooldown;
    }
}
