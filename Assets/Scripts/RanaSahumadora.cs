using UnityEngine;

// ============================================================
//  RanaSahumadora — Sahumador de barro con forma de rana
// ============================================================
//
//  DIÉGESIS:
//  Un tlemaitl (sahumador) de barro cocido, con forma de rana, que
//  absorbió tonalli disperso en Chicunamictlán y despertó. Por dentro
//  todavía arden brasas de chile: el humo acre que, en el mundo de
//  arriba, se usaba como castigo (Códice Mendocino). No es un animal:
//  es una vasija. Por eso es pesada, salta torpe y no se dobla.
//
//  COMPORTAMIENTO:
//    • Patrulla a saltitos (no camina: es barro). Respeta paredes
//      y precipicios.
//    • Al detectar a Romerito, lo persigue a saltos balísticos
//      (misma técnica que Mictecah3_Saltarin).
//    • HUMADERA: mientras Romerito permanezca dentro de radioHumadera,
//      las brasas se van calentando (el cuerpo se tiñe de ámbar). Si
//      se queda el tiempo suficiente, la rana se planta, el barro se
//      pone al rojo (aviso fuerte con SpriteGlowHDR) y suelta una
//      nube de humo de chile (NubeHumoChile) por la boca superior.
//    • Durante el aviso es "superarmor": recibe daño pero NO
//      retrocede. Pegarle no cancela el humo — la respuesta correcta
//      es alejarse o rematarla antes de que acumule.
//    • Con poca vida cambia al sprite agrietado y las brasas
//      parpadean irregulares.
//    • Hereda de MictecahBase: daño por contacto, retroceso,
//      camuflaje de la Barrera de Copal, EnemigoRespawnable.
//
//  SPRITES: solo 3 (idle, salto, agrietado). Se cambian por código,
//  no hace falta Animator.
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  1. GameObject raíz en layer "Enemy" con:
//       SpriteRenderer (material con shader Mictlan/SpriteGlowHDR)
//       Rigidbody2D (Freeze Rotation Z)
//       Collider2D (Capsule/Box ajustado al cuerpo, NO trigger)
//       EnemyDummy (maxHealth sugerido: 4)
//       EnemigoRespawnable
//       RanaSahumadora (este script)
//  2. El arte mira a la IZQUIERDA: marca "Flip X" en el SpriteRenderer.
//     La base asume que con escala positiva el sprite mira a la derecha.
//  3. Hijo "GroundCheck" en el borde frontal-inferior (como los Mictecah).
//  4. Hijo "BocaHumo" en la boca de carga del lomo. Ahí nace la nube.
//  5. Asigna el prefab de NubeHumoChile en 'nubeHumo'.
//  ─────────────────────────────────────────────────────────────
//
// ============================================================

public class RanaSahumadora : MictecahBase
{
    // ── Sprites ──────────────────────────────────────────────
    [Header("── Sprites (Rana Sahumadora) ──")]
    [Tooltip("Renderer del cuerpo. Vacío = el del propio GameObject.")]
    public SpriteRenderer cuerpo;
    public Sprite spriteIdle;
    public Sprite spriteSalto;
    public Sprite spriteAgrietado;
    [Tooltip("Con esta vida o menos, la rana se ve agrietada.")]
    public int vidaParaAgrietarse = 1;

    // ── Saltos ───────────────────────────────────────────────
    [Header("── Saltos de patrulla ──")]
    public float patrullaSaltoHorizontal = 2f;
    public float patrullaSaltoVertical = 5f;
    [Tooltip("Pausa en el suelo entre saltitos de patrulla.")]
    public float patrullaPausa = 1.8f;

    [Header("── Saltos de persecución ──")]
    public float jumpHorizontal = 3.5f;
    public float jumpVertical = 7f;
    [Tooltip("Pausa en el suelo entre saltos al perseguir.")]
    public float jumpCooldown = 1.4f;
    [Tooltip("Pequeña pausa extra al aterrizar.")]
    public float landRecovery = 0.25f;

    // ── Humadera ─────────────────────────────────────────────
    [Header("── Humadera ──")]
    [Tooltip("Distancia a la que Romerito 'alimenta' las brasas.")]
    public float radioHumadera = 2.5f;
    [Tooltip("Segundos acumulados cerca de la rana para que suelte el humo.")]
    public float tiempoProximidad = 1.5f;
    [Tooltip("Qué tan rápido se enfría lo acumulado cuando Romerito se aleja (1 = mismo ritmo).")]
    public float ritmoEnfriamiento = 0.6f;
    [Tooltip("Duración del aviso fuerte (barro al rojo) antes de soltar el humo.")]
    public float duracionAviso = 0.6f;
    [Tooltip("Tiempo mínimo entre una humadera y la siguiente.")]
    public float enfriamientoHumadera = 2.5f;
    [Tooltip("Prefab con NubeHumoChile.")]
    public GameObject nubeHumo;
    [Tooltip("Hijo colocado en la boca de carga del lomo.")]
    public Transform bocaHumo;

    // ── Brillo (Mictlan/SpriteGlowHDR) ───────────────────────
    [Header("── Brillo de brasas (SpriteGlowHDR) ──")]
    [Tooltip("Tono de las brasas. Se multiplica por la intensidad HDR.")]
    public Color colorBrasa = new Color(1f, 0.45f, 0.15f);
    [Tooltip("Intensidad HDR en el pico del aviso. Mantenla > 1.4 para que dispare el Bloom.")]
    public float intensidadAviso = 2.6f;
    [Tooltip("Calor (0–1) en reposo: un latido apenas perceptible.")]
    [Range(0f, 0.3f)] public float calorReposo = 0.06f;
    [Tooltip("Velocidad del latido en reposo (ciclos por segundo).")]
    public float velocidadLatido = 0.35f;
    [Tooltip("Calor máximo (0–1) que alcanza mientras Romerito acumula cerca. Debe quedar por debajo del Bloom.")]
    [Range(0f, 0.8f)] public float calorAcumuladoMax = 0.4f;
    [Tooltip("Parpadeo irregular (0–1) cuando está agrietada.")]
    [Range(0f, 0.4f)] public float calorAgrietado = 0.18f;
    [Tooltip("Curva del aviso: de calor acumulado a barro al rojo.")]
    public AnimationCurve curvaAviso = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Qué tan rápido se apagan las brasas tras soltar el humo (calor por segundo).")]
    public float velocidadApagado = 5f;

    [Header("── Vibración del aviso (opcional) ──")]
    [Tooltip("Solo funciona si 'cuerpo' es un HIJO (no el renderer raíz).")]
    public float amplitudVibracion = 0.04f;

    // ── Interno ──────────────────────────────────────────────
    private enum FaseHumadera { Ninguna, Aviso }
    private FaseHumadera fase = FaseHumadera.Ninguna;

    private float acumulado;          // segundos de Romerito cerca
    private float timerAviso;
    private float timerEnfriamiento;
    private float saltoTimer;
    private bool enElAire;
    private float calorActual;

    private MaterialPropertyBlock mpb;
    private static readonly int ID_COLOR = Shader.PropertyToID("_Color");
    private Vector3 posLocalCuerpo;
    private bool cuerpoEsHijo;

    // Superarmor durante el aviso: recibe daño, no retrocede.
    protected override bool IgnoraRetroceso => fase == FaseHumadera.Aviso;

    // ── Ciclo de vida ────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        if (cuerpo == null) cuerpo = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        cuerpoEsHijo = cuerpo != null && cuerpo.transform != transform;
        if (cuerpoEsHijo) posLocalCuerpo = cuerpo.transform.localPosition;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // EnemigoRespawnable reactiva el GameObject al reagrupar:
        // limpiamos todo lo que pudo quedar a medias.
        ReiniciarEstado();
    }

    private void ReiniciarEstado()
    {
        fase = FaseHumadera.Ninguna;
        acumulado = 0f;
        timerAviso = 0f;
        timerEnfriamiento = 0f;
        saltoTimer = patrullaPausa * 0.5f;
        enElAire = false;
        calorActual = 0f;
        controlVelocidad = true;
        targetVelocity = Vector2.zero;
        estado = Estado.Patrullando;
        if (cuerpoEsHijo) cuerpo.transform.localPosition = posLocalCuerpo;
        ActualizarVisual();
    }

    protected override void Update()
    {
        if (timerEnfriamiento > 0f) timerEnfriamiento -= Time.deltaTime;

        // Durante el aviso la rana está plantada: la máquina de estados
        // de la base no corre (ni persecución, ni pérdida de rastro por
        // camuflaje). El humo sale sí o sí.
        if (fase == FaseHumadera.Aviso)
        {
            targetVelocity = Vector2.zero;
            ActualizarAviso();
        }
        else
        {
            base.Update();
        }

        ActualizarVisual();
    }

    // ── PATRULLA A SALTITOS ──────────────────────────────────
    protected override void LogicaPatrulla()
    {
        if (!EstaEnSuelo)
        {
            enElAire = true;
            return;
        }

        if (Aterrizo()) saltoTimer = patrullaPausa;

        targetVelocity = Vector2.zero;
        if (!SensoresListos) return;

        // Romerito puede acercarse por la espalda sin ser "detectado"
        // aún (detección pasa por ChequearJugador): las brasas igual
        // se calientan si está cerca.
        AcumularProximidad();
        if (PuedeIniciarHumadera()) { IniciarAviso(); return; }

        saltoTimer -= Time.deltaTime;
        if (saltoTimer > 0f) return;

        if (HayParedAdelante() || HayPrecipicioAdelante())
        {
            Voltear();
            saltoTimer = patrullaPausa * 0.5f;
            return;
        }

        Saltar(facing, patrullaSaltoHorizontal, patrullaSaltoVertical);
    }

    // ── PERSECUCIÓN A SALTOS ─────────────────────────────────
    protected override void IniciarPersecucion()
    {
        estado = Estado.Persiguiendo;
        saltoTimer = jumpCooldown * 0.5f;
        enElAire = !EstaEnSuelo;
    }

    protected override void LogicaPersecucion()
    {
        AcumularProximidad();

        if (DistanciaAlJugador() > stopChaseRange && EstaEnSuelo)
        {
            controlVelocidad = true;
            targetVelocity = Vector2.zero;
            saltoTimer = patrullaPausa;
            estado = Estado.Patrullando;
            return;
        }

        if (!EstaEnSuelo)
        {
            enElAire = true;
            return;
        }

        if (Aterrizo()) saltoTimer = jumpCooldown + landRecovery;

        MirarHacia(DireccionAlJugador());
        targetVelocity = Vector2.zero;

        if (PuedeIniciarHumadera()) { IniciarAviso(); return; }

        saltoTimer -= Time.deltaTime;
        if (saltoTimer <= 0f)
            Saltar(DireccionAlJugador(), jumpHorizontal, jumpVertical);
    }

    private void Saltar(int dir, float horizontal, float vertical)
    {
        MirarHacia(dir);
        // Salto balístico: fijamos velocidad y soltamos el control.
        controlVelocidad = false;
        rb.linearVelocity = new Vector2(dir * horizontal, vertical);
        enElAire = true;
    }

    // True una sola vez, en el frame en que toca suelo tras un salto.
    // La condición de velocidad evita detectar "aterrizaje" en el mismo
    // frame del despegue (igual que el Saltarín).
    private bool Aterrizo()
    {
        if (!enElAire || rb.linearVelocity.y > 0.5f) return false;
        enElAire = false;
        controlVelocidad = true;
        targetVelocity = Vector2.zero;
        return true;
    }

    protected override void OnHerido()
    {
        enElAire = false;
        saltoTimer = jumpCooldown;
    }

    // ── HUMADERA ─────────────────────────────────────────────
    private void AcumularProximidad()
    {
        bool cerca = player != null
                     && !BarreraCopal.CamuflajeActivo
                     && DistanciaAlJugador() <= radioHumadera;

        if (cerca)
            acumulado = Mathf.Min(acumulado + Time.deltaTime, tiempoProximidad);
        else
            acumulado = Mathf.Max(0f, acumulado - Time.deltaTime * ritmoEnfriamiento);
    }

    private bool PuedeIniciarHumadera()
    {
        return EstaEnSuelo
               && timerEnfriamiento <= 0f
               && acumulado >= tiempoProximidad;
    }

    private void IniciarAviso()
    {
        fase = FaseHumadera.Aviso;
        timerAviso = 0f;
        controlVelocidad = true;
        targetVelocity = Vector2.zero;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void ActualizarAviso()
    {
        timerAviso += Time.deltaTime;
        if (timerAviso >= duracionAviso)
            SoltarHumo();
    }

    private void SoltarHumo()
    {
        if (nubeHumo != null)
        {
            Vector3 pos = bocaHumo != null ? bocaHumo.position : transform.position;
            Instantiate(nubeHumo, pos, Quaternion.identity);
        }

        fase = FaseHumadera.Ninguna;
        acumulado = 0f;
        timerEnfriamiento = enfriamientoHumadera;
        saltoTimer = jumpCooldown;
        if (cuerpoEsHijo) cuerpo.transform.localPosition = posLocalCuerpo;
        // Vuelve a la máquina de estados normal: si Romerito sigue cerca,
        // ChequearJugador() reinicia la persecución en el siguiente frame.
        estado = Estado.Patrullando;
    }

    // ── VISUAL: sprite + brillo ──────────────────────────────
    private bool EstaAgrietada =>
        dummy != null && dummy.VidaActual > 0 && dummy.VidaActual <= vidaParaAgrietarse;

    private void ActualizarVisual()
    {
        if (cuerpo == null) return;

        // 1. Sprite
        Sprite s;
        if (!EstaEnSuelo && fase == FaseHumadera.Ninguna) s = spriteSalto;
        else if (EstaAgrietada) s = spriteAgrietado;
        else s = spriteIdle;
        if (s != null && cuerpo.sprite != s) cuerpo.sprite = s;

        // 2. Calor objetivo (0 = sprite tal cual, 1 = barro al rojo)
        float calorBase;
        if (EstaAgrietada)
            calorBase = Mathf.PerlinNoise(Time.time * 3f, 0.37f) * calorAgrietado;
        else
            calorBase = (Mathf.Sin(Time.time * velocidadLatido * Mathf.PI * 2f) + 1f) * 0.5f * calorReposo;

        float calorAcum = (tiempoProximidad > 0f ? acumulado / tiempoProximidad : 0f) * calorAcumuladoMax;
        float objetivo = Mathf.Max(calorBase, calorAcum);

        if (fase == FaseHumadera.Aviso)
        {
            float k = curvaAviso.Evaluate(Mathf.Clamp01(timerAviso / duracionAviso));
            objetivo = Mathf.Lerp(Mathf.Max(calorAcum, calorAcumuladoMax), 1f, k);
            calorActual = objetivo;                      // subida exacta
        }
        else if (objetivo < calorActual)
        {
            // Apagado brusco tras soltar el humo
            calorActual = Mathf.MoveTowards(calorActual, objetivo, velocidadApagado * Time.deltaTime);
        }
        else
        {
            calorActual = objetivo;
        }

        // 3. Color HDR por MaterialPropertyBlock (sin GC, conserva batching)
        Color c = Color.Lerp(Color.white, colorBrasa * intensidadAviso, calorActual);
        c.a = 1f;
        cuerpo.GetPropertyBlock(mpb);
        mpb.SetColor(ID_COLOR, c);
        cuerpo.SetPropertyBlock(mpb);

        // 4. Vibración (solo si el cuerpo es un hijo)
        if (cuerpoEsHijo)
        {
            if (fase == FaseHumadera.Aviso)
                cuerpo.transform.localPosition = posLocalCuerpo + (Vector3)(Random.insideUnitCircle * amplitudVibracion * calorActual);
            else
                cuerpo.transform.localPosition = posLocalCuerpo;
        }
    }

    // ── DEBUG ────────────────────────────────────────────────
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = new Color(1f, 0.45f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, radioHumadera);
        if (bocaHumo != null)
            Gizmos.DrawWireSphere(bocaHumo.position, 0.12f);
    }
}
