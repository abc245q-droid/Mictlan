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
//    • AGONÍA: al llegar a vidaParaAgrietarse, cambia al sprite
//      agrietado, se vuelve INVULNERABLE y deja de moverse (si iba
//      en el aire, primero aterriza). Tiembla cada vez más fuerte
//      mientras las brasas suben al rojo y, al final, revienta en
//      una última llamarada (deathEffect de EnemyDummy) que daña en
//      radioExplosion. Muere por la ruta normal de EnemyDummy: loot,
//      Tonalli y dispersión de EnemigoRespawnable intactos.
//      Nota de balance: con vidaParaAgrietarse = 1, el golpe que la
//      deja en 1 ya es el "golpe final" (maxHealth 4 = 3 golpes).
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
    [Tooltip("Parpadeo irregular (0–1) de las brasas durante la agonía.")]
    [Range(0f, 0.4f)] public float calorAgrietado = 0.18f;
    [Tooltip("Curva del aviso: de calor acumulado a barro al rojo.")]
    public AnimationCurve curvaAviso = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Qué tan rápido se apagan las brasas tras soltar el humo (calor por segundo).")]
    public float velocidadApagado = 5f;

    [Header("── Vibración del aviso (opcional) ──")]
    [Tooltip("Solo funciona si 'cuerpo' es un HIJO (no el renderer raíz).")]
    public float amplitudVibracion = 0.04f;

    [Header("── Agonía y última llamarada ──")]
    [Tooltip("Segundos que tiembla, invulnerable, antes de reventar.")]
    public float duracionAgonia = 1.4f;
    [Tooltip("Temblor al empezar la agonía (unidades de mundo).")]
    public float temblorInicial = 0.015f;
    [Tooltip("Temblor justo antes de reventar.")]
    public float temblorFinal = 0.08f;
    [Tooltip("Radio de daño de la llamarada. Debe coincidir con el tamaño del VFX (deathEffect).")]
    public float radioExplosion = 1.6f;
    [Tooltip("Daño de la llamarada a Romerito. 0 = solo visual.")]
    public int danioExplosion = 1;
    [Tooltip("Empuje que la llamarada da a Romerito.")]
    public float empujeExplosion = 9f;

    // ── Interno ──────────────────────────────────────────────
    private enum FaseHumadera { Ninguna, Aviso, Agonia }
    private FaseHumadera fase = FaseHumadera.Ninguna;

    private bool temblando;           // false = agonía esperando aterrizar
    private float timerAgonia;
    private Vector3 posAgonia;

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

    // Superarmor durante el aviso y la agonía: no retrocede.
    // EstaAgrietada cubre el golpe que la agrieta: OnHurt se dispara
    // después de restar la vida, así que ese golpe ya no la empuja.
    protected override bool IgnoraRetroceso => fase != FaseHumadera.Ninguna || EstaAgrietada;

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

        // Deshacer la agonía (respawn tras dispersión)
        temblando = false;
        timerAgonia = 0f;
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        if (dummy != null) dummy.SetInvulnerable(false);

        // El golpe final dispara FlashWhite (rojo) y la dispersión corta
        // la corrutina antes de volver a blanco: limpiamos al reaparecer.
        if (cuerpo != null) cuerpo.color = Color.white;

        ActualizarVisual();
    }

    protected override void Update()
    {
        if (timerEnfriamiento > 0f) timerEnfriamiento -= Time.deltaTime;

        // La agonía tiene prioridad sobre todo (cancela un aviso a medias:
        // ya no hay humo, solo la última llamarada).
        if (fase != FaseHumadera.Agonia && EstaAgrietada)
            IniciarAgonia();

        if (fase == FaseHumadera.Agonia)
        {
            ActualizarAgonia();
            ActualizarVisual();
            return;
        }

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

    // ── AGONÍA ───────────────────────────────────────────────
    private void IniciarAgonia()
    {
        fase = FaseHumadera.Agonia;
        temblando = false;
        timerAgonia = 0f;
        if (dummy != null) dummy.SetInvulnerable(true);
        if (cuerpoEsHijo) cuerpo.transform.localPosition = posLocalCuerpo;

        // Suelta el control: si iba en el aire termina su caída natural.
        controlVelocidad = false;
        targetVelocity = Vector2.zero;

        if (CheckGroundBelow() && rb.linearVelocity.y <= 0.5f)
            ComenzarTemblor();
    }

    private void ActualizarAgonia()
    {
        if (!temblando)
        {
            // Esperando aterrizar (la agrietaron a medio salto)
            if (CheckGroundBelow() && rb.linearVelocity.y <= 0.5f)
                ComenzarTemblor();
            return;
        }

        timerAgonia += Time.deltaTime;
        float k = Mathf.Clamp01(timerAgonia / duracionAgonia);
        float amp = Mathf.Lerp(temblorInicial, temblorFinal, k * k);
        transform.position = posAgonia + (Vector3)(Random.insideUnitCircle * amp);

        if (timerAgonia >= duracionAgonia)
            Reventar();
    }

    private void ComenzarTemblor()
    {
        temblando = true;
        timerAgonia = 0f;
        // Plantada: kinematic para que nada la empuje mientras tiembla.
        // Sigue teniendo collider, así que tocarla todavía quema.
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        posAgonia = transform.position;
    }

    private void Reventar()
    {
        transform.position = posAgonia;

        // Última llamarada: daño + empuje radial a Romerito
        if (danioExplosion > 0 && player != null && playerHealth != null
            && DistanciaAlJugador() <= radioExplosion && playerHealth.PuedeRecibirDano)
        {
            Rigidbody2D prb = player.GetComponent<Rigidbody2D>();
            if (prb != null)
            {
                Vector2 dir = ((Vector2)(player.position - transform.position)).normalized;
                dir.y = Mathf.Max(dir.y, 0.5f);
                prb.linearVelocity = Vector2.zero;
                prb.AddForce(dir.normalized * empujeExplosion, ForceMode2D.Impulse);
            }
            playerHealth.TakeDamage(danioExplosion);
        }

        // Muerte por la ruta normal de EnemyDummy (loot, Tonalli,
        // deathEffect = la llamarada, dispersión/respawn).
        if (dummy != null)
        {
            dummy.SetInvulnerable(false);
            dummy.TakeDamage(Mathf.Max(1, dummy.VidaActual));
        }
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
        float calorBase = (Mathf.Sin(Time.time * velocidadLatido * Mathf.PI * 2f) + 1f) * 0.5f * calorReposo;

        float calorAcum = (tiempoProximidad > 0f ? acumulado / tiempoProximidad : 0f) * calorAcumuladoMax;
        float objetivo = Mathf.Max(calorBase, calorAcum);

        if (fase == FaseHumadera.Agonia)
        {
            // Las brasas suben al rojo durante el temblor, con parpadeo
            // irregular encima: el barro está por reventar.
            float k = temblando ? Mathf.Clamp01(timerAgonia / duracionAgonia) : 0f;
            float parpadeo = Mathf.PerlinNoise(Time.time * 6f, 0.37f) * calorAgrietado;
            calorActual = Mathf.Clamp01(Mathf.Lerp(calorAcumuladoMax, 1f, k * k) + parpadeo);
        }
        else if (fase == FaseHumadera.Aviso)
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radioExplosion);
    }
}
