# -*- coding: utf-8 -*-
"""
Parche: CameraLookControl.cs
Añade "Anticipación de Caída" estilo Hollow Knight al control de cámara.
Reemplazos por anclas exactas. Verifica balance de llaves antes de escribir.
"""
import io, sys

PATH = r"Assets/Scripts/CameraLookControl.cs"

def leer(p):
    for enc in ("utf-8-sig", "cp1252", "latin-1"):
        try:
            with io.open(p, "r", encoding=enc) as f:
                return f.read()
        except UnicodeDecodeError:
            continue
    raise RuntimeError("No se pudo decodificar " + p)

src = leer(PATH)

def reemplazar(texto, ancla, nuevo, nombre):
    if texto.count(ancla) != 1:
        print("FALLO ancla (%s): apariciones = %d" % (nombre, texto.count(ancla)))
        sys.exit(1)
    return texto.replace(ancla, nuevo, 1)

# ── 1) Comentario de cabecera ─────────────────────────────────
ancla1 = """/// Control de "peek" vertical de cámara (estilo Hollow Knight):
/// al mantener arriba/abajo estando quieto, desplaza el encuadre para
/// revelar más terreno en esa dirección. Trabaja sobre el
/// CinemachinePositionComposer de la CinemachineCamera.
/// </summary>"""
nuevo1 = """/// Control vertical de cámara (estilo Hollow Knight). Dos subsistemas
/// que comparten un único punto de escritura sobre ScreenPosition.y:
///
///  1) PEEK MANUAL: al mantener arriba/abajo estando quieto y en suelo,
///     desplaza el encuadre para revelar más terreno en esa dirección.
///  2) ANTICIPACIÓN DE CAÍDA: tras una caída sostenida, el encuadre
///     desciende gradualmente para revelar el terreno de aterrizaje,
///     y regresa al tocar suelo. Réplica del comportamiento del
///     CameraTarget de Hollow Knight (fall offset progresivo).
///
/// Trabaja sobre el CinemachinePositionComposer de la CinemachineCamera.
/// Debe ser el ÚNICO script que escriba Composition.ScreenPosition.y.
/// </summary>"""
src = reemplazar(src, ancla1, nuevo1, "cabecera")

# ── 2) Campos nuevos tras invertVertical ──────────────────────
ancla2 = """    public bool invertVertical = false;

    private CinemachinePositionComposer composer;"""
nuevo2 = """    public bool invertVertical = false;

    [Header("Anticipación de Caída (estilo Hollow Knight)")]
    [Tooltip("Si está activo, el encuadre desciende automáticamente durante " +
             "caídas sostenidas para revelar el terreno de aterrizaje.")]
    public bool anticiparCaida = true;

    [Tooltip("Velocidad vertical (negativa) a partir de la cual se considera caída. " +
             "Valores más negativos = ignora saltos cortos.")]
    public float umbralVelocidadCaida = -2f;

    [Tooltip("Segundos de caída sostenida antes de mover el encuadre. " +
             "Evita que el arco de un salto normal sacuda la cámara.")]
    public float retrasoCaida = 0.25f;

    [Tooltip("Desplazamiento máximo del encuadre durante la caída (fracción de pantalla). " +
             "0.25 ≈ un cuarto de pantalla extra de visión hacia abajo.")]
    public float offsetMaxCaida = 0.25f;

    [Tooltip("Qué tan rápido crece el offset durante la caída (fracción de pantalla por segundo).")]
    public float rampaCaida = 0.6f;

    [Tooltip("Qué tan rápido regresa el encuadre al aterrizar (fracción de pantalla por segundo).")]
    public float rampaRetorno = 1.2f;

    [Tooltip("Invierte el sentido del offset de caída si tu composer usa la convención opuesta. " +
             "Con la convención detectada en el peek (+y revela abajo) debe quedar en false.")]
    public bool invertirOffsetCaida = false;

    private CinemachinePositionComposer composer;"""
src = reemplazar(src, ancla2, nuevo2, "campos publicos")

# ── 3) Campos privados ────────────────────────────────────────
ancla3 = """    private float targetScreenY;
    private float timer;"""
nuevo3 = """    private float targetScreenY;
    private float timer;

    // Anticipación de caída
    private Rigidbody2D playerRb;
    private RomeritoMovement playerMovement;
    private float fallTimer;
    private float offsetCaidaActual;"""
src = reemplazar(src, ancla3, nuevo3, "campos privados")

# ── 4) Start(): localizar al jugador ──────────────────────────
ancla4 = """            defaultScreenY = composer.Composition.ScreenPosition.y;
            targetScreenY = defaultScreenY;
        }
    }"""
nuevo4 = """            defaultScreenY = composer.Composition.ScreenPosition.y;
            targetScreenY = defaultScreenY;
        }

        BuscarJugador();
    }"""
src = reemplazar(src, ancla4, nuevo4, "Start")

# ── 5) Update(): arbitraje peek + caída ───────────────────────
ancla5 = """        if (horizontalIdle && (pressingUp || pressingDown))
        {
            timer += Time.deltaTime;

            if (timer >= timeToTrigger)
            {
                // Por defecto (comportamiento actual): ARRIBA revela abajo, ABAJO revela arriba.
                // invertVertical = true -> convención Hollow Knight.
                float dir = (pressingUp ? 1f : -1f) * (invertVertical ? -1f : 1f);
                targetScreenY = defaultScreenY + dir * lookOffsetAmount;
            }
        }
        else
        {
            // Al movernos horizontalmente o soltar, volvemos al encuadre base.
            timer = 0f;
            targetScreenY = defaultScreenY;
        }"""
nuevo5 = """        // ── 1) PEEK MANUAL (solo con Romerito en suelo, como Hollow Knight) ──
        bool enSuelo = (playerMovement == null) || playerMovement.isGrounded;
        float peekOffset = 0f;

        if (enSuelo && horizontalIdle && (pressingUp || pressingDown))
        {
            timer += Time.deltaTime;

            if (timer >= timeToTrigger)
            {
                // Por defecto (comportamiento actual): ARRIBA revela abajo, ABAJO revela arriba.
                // invertVertical = true -> convención Hollow Knight.
                float dir = (pressingUp ? 1f : -1f) * (invertVertical ? -1f : 1f);
                peekOffset = dir * lookOffsetAmount;
            }
        }
        else
        {
            timer = 0f;
        }

        // ── 2) ANTICIPACIÓN DE CAÍDA ──
        ActualizarOffsetCaida();

        // Ambos offsets se suman sobre el encuadre base. Durante una caída el
        // peek no puede activarse (requiere suelo), así que no compiten.
        float dirCaida = invertirOffsetCaida ? -1f : 1f;
        targetScreenY = defaultScreenY + peekOffset + dirCaida * offsetCaidaActual;"""
src = reemplazar(src, ancla5, nuevo5, "Update")

# ── 6) Métodos nuevos al final de la clase ────────────────────
ancla6 = """        composer.Composition.ScreenPosition = pos;
    }
}"""
nuevo6 = """        composer.Composition.ScreenPosition = pos;
    }

    /// <summary>
    /// Ramp del offset de caída. Tras 'retrasoCaida' segundos cayendo, el
    /// encuadre baja gradualmente hasta 'offsetMaxCaida' para revelar el
    /// terreno de aterrizaje; al tocar suelo regresa a ritmo 'rampaRetorno'.
    /// Mismo principio que el CameraTarget de Hollow Knight: el offset es
    /// PROGRESIVO (caídas cortas apenas mueven la cámara, caídas largas
    /// revelan cada vez más), y el suavizado exponencial de Update() se
    /// encarga de que nunca haya saltos bruscos.
    ///
    /// Nota de detección: aquí SÍ usamos rb.linearVelocity.y porque una
    /// caída es movimiento físico real con umbral holgado (-2), a
    /// diferencia del pitfall de detección de "quieto", donde los residuos
    /// de gravedad rompían la comparación contra cero.
    /// </summary>
    private void ActualizarOffsetCaida()
    {
        if (!anticiparCaida)
        {
            offsetCaidaActual = 0f;
            return;
        }

        // Reintento perezoso por si el jugador aún no existía en Start()
        // (respawn, orden de carga de escena, etc.).
        if (playerRb == null) BuscarJugador();

        bool cayendo = playerRb != null
                       && playerRb.linearVelocity.y < umbralVelocidadCaida
                       && (playerMovement == null || !playerMovement.isGrounded);

        if (cayendo)
        {
            fallTimer += Time.deltaTime;

            if (fallTimer >= retrasoCaida)
                offsetCaidaActual = Mathf.MoveTowards(
                    offsetCaidaActual, offsetMaxCaida, rampaCaida * Time.deltaTime);
        }
        else
        {
            fallTimer = 0f;
            offsetCaidaActual = Mathf.MoveTowards(
                offsetCaidaActual, 0f, rampaRetorno * Time.deltaTime);
        }
    }

    private void BuscarJugador()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        playerRb = player.GetComponent<Rigidbody2D>();
        playerMovement = player.GetComponent<RomeritoMovement>();
    }
}"""
src = reemplazar(src, ancla6, nuevo6, "metodos finales")

# ── Verificación de balance de llaves ─────────────────────────
if src.count("{") != src.count("}"):
    print("FALLO: llaves desbalanceadas (%d abre, %d cierra)" % (src.count("{"), src.count("}")))
    sys.exit(1)

with io.open(PATH, "w", encoding="utf-8-sig", newline="\n") as f:
    f.write(src)

print("OK - parche aplicado. Llaves: %d/%d" % (src.count("{"), src.count("}")))
