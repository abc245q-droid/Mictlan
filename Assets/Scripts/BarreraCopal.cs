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
    [Tooltip("Umbral del stick horizontal por debajo del cual se considera 'quieto'. " +
             "NOTA: se cambió de velocity a input porque el Rigidbody2D en el suelo " +
             "tiene residuales de gravedad que rompían la detección. Ahora quieto = " +
             "'el jugador no está tratando de moverse', que es lo que importa para el humo.")]
    [Range(0f, 0.5f)] public float deadzoneStick = 0.15f;

    [Header("Visual")]
    [Tooltip("SpriteRenderer hijo con el humo. Se activa durante la barrera.")]
    public SpriteRenderer spriteHumo;
    [Tooltip("Alpha del humo cuando la protección ESTÁ cubriendo (quieto + suelo).")]
    [Range(0f, 1f)] public float alphaProtegido = 0.85f;
    [Tooltip("Alpha del humo cuando la protección NO cubre (moviéndose / en el aire).")]
    [Range(0f, 1f)] public float alphaSinEfecto = 0.3f;
    [Tooltip("Velocidad de transición del alpha entre ambos estados.")]
    public float velocidadFade = 6f;

    [Header("Colisión: layer intangible (quieto + suelo)")]
    [Tooltip("Nombre de la layer a la que Romerito pasa mientras la barrera lo " +
             "CUBRE (quieto + suelo). Debe existir en Project Settings > Tags and Layers " +
             "y su intersección con Enemy / EnemyProjectile debe estar DESMARCADA en " +
             "la Layer Collision Matrix. Mantener MARCADA su intersección con el terreno.")]
    public string layerIntangible = "PlayerIntangible";

    [Header("Colisión: cápsula reducida (moviéndose o en el aire)")]
    [Tooltip("CapsuleCollider2D de Romerito. Se encoge cuando la barrera está activa pero " +
             "NO cubriendo, para que el jugador pueda esquivar ataques. Auto-detectado " +
             "si se deja vacío.")]
    public CapsuleCollider2D capsulaJugador;

    [Tooltip("Fracción del tamaño original que la cápsula toma durante la barrera " +
             "activa en movimiento. 1.0 = sin cambio, 0.6 = 60% del original.")]
    [Range(0.4f, 1f)] public float proporcionEnMovimiento = 0.65f;

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

    // Estado guardado al inicio de la barrera — se restaura al terminar.
    private int layerOriginal;
    private Vector2 capsulaSizeOriginal;
    private Vector2 capsulaOffsetOriginal;
    // Detección de cambio de régimen para no reescribir cápsula/layer cada frame.
    private bool aplicoIntangibleActual = false;
    private bool cachedLayerIntangibleValida = false;
    private int cachedLayerIntangibleId = -1;

    void Awake()
    {
        movement = GetComponent<RomeritoMovement>();
        health = GetComponent<RomeritoHealth>();
        rb = GetComponent<Rigidbody2D>();
        if (capsulaJugador == null)
            capsulaJugador = GetComponent<CapsuleCollider2D>();

        // Warnings explícitos si algo falta — así un mal cableado no se
        // convierte en un fallo silencioso ("la barrera no protege pero
        // no dice por qué"). Todos DEBEN estar en el mismo GO que BarreraCopal.
        if (health == null)
            Debug.LogError("[BarreraCopal] Falta RomeritoHealth en el mismo GameObject. " +
                           "La barrera NO podrá activar la invulnerabilidad.");
        if (movement == null)
            Debug.LogError("[BarreraCopal] Falta RomeritoMovement en el mismo GameObject. " +
                           "La barrera NO podrá detectar si Romerito toca el suelo.");
        if (capsulaJugador == null)
            Debug.LogWarning("[BarreraCopal] Sin CapsuleCollider2D. " +
                             "El efecto de 'cápsula reducida al esquivar' no aplicará.");

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

        // Guardar estado original de layer y cápsula — se restaura en Terminar().
        layerOriginal = gameObject.layer;
        if (capsulaJugador != null)
        {
            capsulaSizeOriginal = capsulaJugador.size;
            capsulaOffsetOriginal = capsulaJugador.offset;
        }

        // Resolver la layer intangible UNA VEZ; si no existe, avisar y seguir sin
        // ella (el resto del efecto — invulnerabilidad + cápsula reducida — funciona).
        cachedLayerIntangibleId = LayerMask.NameToLayer(layerIntangible);
        cachedLayerIntangibleValida = cachedLayerIntangibleId >= 0;
        if (!cachedLayerIntangibleValida)
        {
            Debug.LogWarning("[BarreraCopal] La layer '" + layerIntangible + "' no existe. " +
                             "Créala en Project Settings > Tags and Layers y desmárcala contra " +
                             "Enemy/EnemyProjectile en la Layer Collision Matrix. Por ahora " +
                             "la intangibilidad NO se aplicará (solo invulnerabilidad).");
        }

        aplicoIntangibleActual = false;

        if (spriteHumo != null)
        {
            spriteHumo.gameObject.SetActive(true);
            SetAlpha(alphaSinEfecto); // arranca tenue; se densifica al quedarse quieto
        }

        float t = 0f;
        while (t < duracion)
        {
            t += Time.deltaTime;

            // ¿El humo lo cubre? Quieto (sin input horizontal) y tocando el suelo.
            bool quieto = Mathf.Abs(Input.GetAxisRaw("Horizontal")) < deadzoneStick;
            bool enSuelo = movement != null && movement.isGrounded;
            bool cubierto = quieto && enSuelo;

            CamuflajeActivo = cubierto;
            if (health != null) health.SetInvulnerabilidadExterna(cubierto);

            // Régimen de colisión — solo escribimos cuando cambia el estado.
            if (cubierto && !aplicoIntangibleActual)
            {
                AplicarRegimenIntangible();
                aplicoIntangibleActual = true;
            }
            else if (!cubierto && (aplicoIntangibleActual || t <= Time.deltaTime))
            {
                // El segundo bloque cubre el frame 0: si empezamos descubiertos,
                // hay que aplicar la cápsula reducida sí o sí una vez.
                AplicarRegimenReducido();
                aplicoIntangibleActual = false;
            }

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

    /// <summary>
    /// Cubierto: layer intangible (los enemigos atraviesan) + cápsula original.
    /// </summary>
    private void AplicarRegimenIntangible()
    {
        if (cachedLayerIntangibleValida && gameObject.layer != cachedLayerIntangibleId)
            gameObject.layer = cachedLayerIntangibleId;

        if (capsulaJugador != null)
        {
            capsulaJugador.size = capsulaSizeOriginal;
            capsulaJugador.offset = capsulaOffsetOriginal;
        }
    }

    /// <summary>
    /// Descubierto pero barrera activa: layer original (los enemigos SÍ chocan)
    /// + cápsula reducida para esquivar. El offset.y se compensa para que el
    /// pie quede en el mismo Y (si no lo hiciéramos, la cápsula encogida
    /// 'levitaría' o Romerito quedaría medio hundido en el suelo).
    /// </summary>
    private void AplicarRegimenReducido()
    {
        if (gameObject.layer != layerOriginal)
            gameObject.layer = layerOriginal;

        if (capsulaJugador != null)
        {
            Vector2 nuevaSize = capsulaSizeOriginal * proporcionEnMovimiento;
            float deltaY = (capsulaSizeOriginal.y - nuevaSize.y) * 0.5f;
            capsulaJugador.size = nuevaSize;
            capsulaJugador.offset = new Vector2(
                capsulaOffsetOriginal.x,
                capsulaOffsetOriginal.y - deltaY);
        }
    }

    private void Terminar()
    {
        EstaActiva = false;
        CamuflajeActivo = false;
        if (health != null) health.SetInvulnerabilidadExterna(false);

        // Restaurar layer y cápsula al estado que tenían antes de Activar().
        // Importante: OnDisable llama a Terminar, así que muerte / descarga
        // de escena SIEMPRE nos deja en el estado original.
        gameObject.layer = layerOriginal;
        if (capsulaJugador != null)
        {
            capsulaJugador.size = capsulaSizeOriginal;
            capsulaJugador.offset = capsulaOffsetOriginal;
        }
        aplicoIntangibleActual = false;

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
