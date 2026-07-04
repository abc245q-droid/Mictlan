using UnityEngine;

// ============================================================
//  Mictecah Lanza Cráneos — El esqueleto "gordo" de munición
// ============================================================
//
//  Contexto de diseño (Chema):
//    Un Mictecah cuyo torso está atiborrado de cráneos entre las
//    costillas. Los ESCUPE en arco parabólico; cada cráneo que aterriza
//    despierta como un Cráneo Patrullero (Mictecah1_Patrullero) — el
//    mismo enemigo básico del inicio del juego. La munición ES un
//    enemigo que el jugador ya sabe leer: economía de diseño pura.
//
//    Aparece en escena DORMIDO, camuflado como un simple montón de
//    cráneos apilados. Al acercarse Romerito, despierta y arranca su
//    rutina de disparo. Ese "montón que cobra vida" es un telegraph
//    ambiental: enseña al jugador a desconfiar de las pilas de cráneos
//    (Mictlán no explica nada; se aprende por experiencia).
//
//  Comportamiento:
//    • DORMIDO: estático, sprite de pila de cráneos, inofensivo al leer.
//    • DESPIERTA por proximidad (detectionRange de la base).
//    • Rutina: telegraph breve → lanza un cráneo en arco → recarga →
//      repite. Alterna un poco el arco para cubrir distintas distancias.
//    • Es estacionario (no patrulla): es una torreta ósea.
//    • El tope de 4 patrulleros vivos lo gestiona CraneoProyectil.
//
//  Requisitos del prefab:
//    Rigidbody2D + Collider2D + EnemyDummy + este script.
//    • craneoPrefab → el prefab del Mictecah1_Patrullero, que DEBE tener
//      el componente CraneoProyectil (desactivado por defecto).
//    • puntoDisparo → hijo en el pecho/costillas de donde salen los cráneos.
//
// ============================================================

public class MictecahLanzaCraneos : MictecahBase
{
    // ── Sub-estados de la rutina de disparo ──────────────────
    private enum FaseLanza { Dormido, Despertando, Apuntando, Recargando, Huyendo }
    private FaseLanza fase = FaseLanza.Dormido;
    private float faseTimer;

    // ¿Está acorralado contra una pared? Si al huir choca y no puede recuperar
    // distancia, se replanta AHÍ y dispara más rápido (compensa su desventaja).
    private bool acorralado = false;

    // Bandera: recibió un golpe y debe huir EN CUANTO termine el aturdimiento
    // del knockback. No entramos en huida dentro de OnHerido directamente
    // porque eso saltaría el stun de la base; esperamos a que Herido termine.
    private bool quiereHuir = false;

    // Acumulador: cuánto tiempo lleva empujando contra algo sin avanzar.
    private float tiempoAtascado = 0f;

    [Header("── Despertar ──")]
    [Tooltip("Sprite de 'montón de cráneos apilados' mientras duerme. Opcional.")]
    public Sprite spriteDormido;
    [Tooltip("Sprite/estado activo al despertar. Opcional (si usas Animator, deja null).")]
    public Sprite spriteDespierto;
    [Tooltip("Duración del despertar (el montón se reordena y se alza).")]
    public float duracionDespertar = 0.6f;

    [Header("── Disparo ──")]
    [Tooltip("Prefab del Cráneo Patrullero. DEBE incluir CraneoProyectil.")]
    public GameObject craneoPrefab;
    [Tooltip("Hijo desde donde salen los cráneos (pecho/costillas).")]
    public Transform puntoDisparo;
    [Tooltip("Tiempo de aviso ANTES de escupir cada cráneo (telegraph).")]
    public float tiempoApuntado = 0.5f;
    [Tooltip("Pausa tras disparar antes del siguiente ciclo.")]
    public float tiempoRecarga = 1.6f;

    [Header("── Huida (reposicionamiento tras ser golpeado) ──")]
    [Tooltip("Tras recibir un golpe, el Lanza Cráneos HUYE en dirección " +
             "opuesta a Romerito hasta recuperar distancia y volver a lanzar. " +
             "Lo convierte en un enemigo de control de espacio, no una torreta fija.")]
    public bool huyeAlSerGolpeado = true;
    [Tooltip("Velocidad de carrera durante la huida.")]
    public float velocidadHuida = 5f;
    [Tooltip("Distancia que intenta poner entre él y Romerito antes de " +
             "replantarse a disparar. Idealmente cerca de detectionRange.")]
    public float distanciaHuidaDeseada = 6f;
    [Tooltip("Seguro: tiempo máximo huyendo antes de replantarse aunque no " +
             "haya alcanzado la distancia (evita huidas eternas).")]
    public float maxTiempoHuida = 2.5f;

    [Header("── Acorralado (pared a la espalda) ──")]
    [Tooltip("MULTIPLICA el tiempo de recarga al estar acorralado. OJO: " +
             "MENOR a 1 = dispara MÁS RÁPIDO (0.5 = doble de velocidad). " +
             "MAYOR a 1 = más lento. Debe ser < 1 para que arrinconarlo sea " +
             "una amenaza, no un alivio.")]
    [Range(0.1f, 1f)]
    public float multiplicadorRecargaAcorralado = 0.5f;
    [Tooltip("Igual para el apuntado. MENOR a 1 = apunta más rápido. " +
             "Mantener < 1.")]
    [Range(0.1f, 1f)]
    public float multiplicadorApuntadoAcorralado = 0.6f;
    [Tooltip("Telegraph del PRIMER disparo justo al quedar acorralado. Es la " +
             "ventana que tiene el jugador para reaccionar al arrinconarlo. " +
             "En segundos, valor ABSOLUTO (no multiplicador). Súbelo si el " +
             "primer cráneo sale demasiado rápido para esquivarlo; bájalo " +
             "para un castigo más severo. 0 = sin aviso (injusto).")]
    public float telegraphPrimerDisparoAcorralado = 0.35f;
    [Tooltip("Distancia extra del sensor de pared durante la huida. La base " +
             "usa wallCheckDistance (~0.6) que suele ser MENOR que el ancho " +
             "del collider, así que el cuerpo choca antes de que el rayo vea " +
             "la pared. Este valor mayor anticipa el muro. Ponlo ~ancho del " +
             "collider + 0.3.")]
    public float sensorParedHuida = 1.2f;
    [Tooltip("Si al huir su velocidad real cae por debajo de esto pese a " +
             "estar empujando, se considera ATASCADO contra una pared → " +
             "acorralado. Red de seguridad si el raycast no alcanza.")]
    public float umbralAtasco = 0.5f;
    [Tooltip("Fracción de tiempo empujando sin avanzar antes de declararlo " +
             "atascado (evita falsos positivos en el primer frame).")]
    public float tiempoParaAtasco = 0.2f;

    [Header("── Cupo de Patrulleros (por este lanzador) ──")]
    [Tooltip("Máximo de patrulleros vivos derivados de ESTE lanzador. " +
             "Contador propio: no lo comparte con otros lanzadores ni con " +
             "los patrulleros nativos del nivel. Al llegar al tope, los " +
             "cráneos nuevos se disuelven al aterrizar en vez de acumularse.")]
    public int topePatrulleros = 4;
    [Tooltip("¿Puede seguir disparando aunque el cupo esté lleno? Si está " +
             "activo, los cráneos extra vuelan y hacen daño en vuelo, pero " +
             "se disuelven al caer (mantiene presión sin saturar la escena).")]
    public bool disparaConCupoLleno = true;

    // Conteo de hijos vivos derivados de ESTE lanzador. Lo modifican los
    // propios CraneoProyectil vía RegistrarHijo/LiberarHijo al aterrizar/morir.
    private int hijosVivos = 0;
    public bool HayCupoLibre() => hijosVivos < topePatrulleros;
    public void RegistrarHijo() => hijosVivos++;
    public void LiberarHijo() => hijosVivos = Mathf.Max(0, hijosVivos - 1);

    [Header("── Física del Arco ──")]
    [Tooltip("Velocidad horizontal base del cráneo lanzado.")]
    public float velocidadHorizontal = 6f;
    [Tooltip("Velocidad vertical (altura del arco). Mayor = arco más alto.")]
    public float velocidadVertical = 8f;
    [Tooltip("Variación aleatoria (±) de la velocidad horizontal, para que " +
             "los cráneos caigan a distancias algo distintas y no se apilen.")]
    public float variacionHorizontal = 1.5f;
    [Tooltip("Si está activo, ajusta el arco según la distancia a Romerito " +
             "(más lejos = lanza más fuerte). Si no, usa valores fijos.")]
    public bool apuntarAlJugador = true;
    [Tooltip("Límite superior del factor de ajuste al apuntar (evita lanzamientos absurdos).")]
    public float factorApuntadoMax = 1.8f;

    // ── Estado interno ───────────────────────────────────────
    private SpriteRenderer sr;

    // Es una torreta: nunca patrulla ni persigue moviéndose.
    protected override bool ReaccionaAlJugador => true;

    protected override void Awake()
    {
        base.Awake();
        sr = GetComponent<SpriteRenderer>();
    }

    protected override void Start()
    {
        base.Start();
        // Arranca dormido: quieto y con cara de "montón inofensivo".
        estado = Estado.Patrullando;   // la base seguirá llamando ChequearJugador()
        fase = FaseLanza.Dormido;
        controlVelocidad = true;
        targetVelocity = Vector2.zero;
        AplicarSprite(spriteDormido);
        if (anim != null) anim.SetBool("Dormido", true);
    }

    // Mientras duerme NO patrulla: sobreescribimos para quedarnos quietos
    // pero seguir escuchando la proximidad del jugador.
    protected override void Update()
    {
        // Si recibió un golpe y ya terminó el aturdimiento de la base
        // (que lo devuelve a Patrullando), arrancamos la huida diferida.
        // Comprobamos aquí para NO saltarnos el stun del knockback.
        if (quiereHuir && estado != Estado.Herido)
            EntrarHuida();

        base.Update();
    }

    protected override void LogicaPatrulla()
    {
        targetVelocity = Vector2.zero;   // torreta: no se mueve
    }

    // La base llama esto cuando Romerito entra en detectionRange.
    protected override void IniciarPersecucion()
    {
        if (fase != FaseLanza.Dormido) return;   // ya despierta

        estado = Estado.Persiguiendo;            // cede el Update a LogicaPersecucion
        fase = FaseLanza.Despertando;
        faseTimer = duracionDespertar;
        targetVelocity = Vector2.zero;

        AplicarSprite(spriteDespierto);
        if (anim != null) { anim.SetBool("Dormido", false); anim.SetTrigger("Despertar"); }
    }

    // ── Rutina de disparo (corre en Estado.Persiguiendo) ─────
    protected override void LogicaPersecucion()
    {
        // La huida es la ÚNICA fase que se mueve; el resto es estacionaria.
        if (fase == FaseLanza.Huyendo)
        {
            TickHuyendo();
            return;
        }

        targetVelocity = Vector2.zero;   // estacionario en las demás fases

        // Encarar a Romerito (voltea el sprite hacia él).
        if (fase != FaseLanza.Despertando)
            MirarHacia(DireccionAlJugador());

        faseTimer -= Time.deltaTime;

        switch (fase)
        {
            case FaseLanza.Despertando:
                if (faseTimer <= 0f) EntrarApuntado();
                break;

            case FaseLanza.Apuntando:
                if (faseTimer <= 0f) DispararCraneo();
                break;

            case FaseLanza.Recargando:
                if (faseTimer <= 0f) EntrarApuntado();
                break;
        }
    }

    // ── FASE: Huyendo (corre en dirección opuesta a Romerito) ──
    //   Se aleja de espaldas hasta recuperar distanciaHuidaDeseada, entonces
    //   se replanta y vuelve a lanzar. Si topa con pared/precipicio en su
    //   dirección de huida (acorralado), se replanta AHÍ y dispara más rápido.
    private void TickHuyendo()
    {
        faseTimer -= Time.deltaTime;

        // Dirección de huida: OPUESTA a donde está Romerito.
        int dirHuida = (player != null && player.position.x > transform.position.x) ? -1 : 1;

        // Encara en la dirección de la huida (corre "de frente" hacia su escape).
        MirarHacia(dirHuida);

        // ── DETECCIÓN DE ACORRALAMIENTO (dos vías) ──
        // (A) Anticipada: raycast largo ve la pared antes de chocar.
        bool topóPared = HayParedEnDireccion(dirHuida);
        bool topóBorde = HayPrecipicioEnDireccion(dirHuida);

        // (B) Confirmada por física: está EMPUJANDO pero su velocidad real es
        //     casi nula → el collider topó con un muro que el rayo no alcanzó.
        //     Esta es la red de seguridad que faltaba: el cuerpo choca antes
        //     de que wallCheckDistance (heredado, ~0.6) detecte la pared.
        float velRealX = Mathf.Abs(rb.linearVelocity.x);
        if (velRealX < umbralAtasco)
        {
            tiempoAtascado += Time.deltaTime;
        }
        else
        {
            tiempoAtascado = 0f;
        }
        bool atascado = tiempoAtascado >= tiempoParaAtasco;

        if (topóPared || topóBorde || atascado)
        {
            // No puede seguir huyendo → se replanta ACORRALADO (disparo rápido).
            Replantarse(true);
            return;
        }

        // ¿Ya recuperó distancia suficiente, o se agotó el tiempo de huida?
        float dist = DistanciaAlJugador();
        if (dist >= distanciaHuidaDeseada || faseTimer <= 0f)
        {
            Replantarse(false);   // distancia recuperada: disparo normal
            return;
        }

        // Sigue corriendo.
        targetVelocity = new Vector2(dirHuida * velocidadHuida, 0f);
    }

    // Vuelve a la rutina de disparo tras la huida. acorralado=true acelera el fuego.
    private void Replantarse(bool estaAcorralado)
    {
        acorralado = estaAcorralado;
        targetVelocity = Vector2.zero;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);  // frena el empuje
        MirarHacia(DireccionAlJugador());   // vuelve a encarar a Romerito

        // Al quedar acorralado, la respuesta es INMEDIATA: primer disparo casi
        // sin telegraph (el jugador debe SENTIR el castigo de arrinconarlo).
        // Los disparos siguientes usan el multiplicador normal de acorralado.
        if (estaAcorralado)
        {
            // Primer disparo: telegraph corto pero SIEMPRE presente (legible).
            // Usa el valor absoluto del Inspector, no un multiplicador oculto.
            fase = FaseLanza.Apuntando;
            faseTimer = telegraphPrimerDisparoAcorralado;
            if (anim != null) anim.SetTrigger("Apuntar");
        }
        else
        {
            EntrarApuntado();
        }
    }

    // Chequeo de pared en una dirección ARBITRARIA (no la de mirada).
    // Necesario porque al huir el enemigo se mueve de espaldas a Romerito.
    private bool HayParedEnDireccion(int dir)
    {
        Vector2 origen = (Vector2)transform.position + new Vector2(dir * 0.2f, 0f);
        // Usa el sensor de huida (mayor que wallCheckDistance) para anticipar
        // el muro ANTES de que el collider choque físicamente contra él.
        return Physics2D.Raycast(origen, Vector2.right * dir, sensorParedHuida, groundLayer);
    }

    // Chequeo de precipicio en una dirección arbitraria: proyecta el groundCheck
    // hacia el lado de la huida y mira si hay suelo debajo. Evita que el enemigo
    // se lance a un abismo al huir de espaldas.
    private bool HayPrecipicioEnDireccion(int dir)
    {
        if (groundCheck == null) return false;
        // Reflejamos la X del groundCheck respecto al centro para el lado de huida.
        float offsetX = Mathf.Abs(groundCheck.position.x - transform.position.x);
        Vector2 sondaOrigen = new Vector2(transform.position.x + dir * offsetX,
                                          groundCheck.position.y);
        return !Physics2D.Raycast(sondaOrigen, Vector2.down, groundCheckDistance, groundLayer);
    }

    private void EntrarApuntado()
    {
        fase = FaseLanza.Apuntando;
        // Acorralado → apunta más rápido (dispara con más frecuencia).
        faseTimer = tiempoApuntado *
            (acorralado ? multiplicadorApuntadoAcorralado : 1f);
        if (anim != null) anim.SetTrigger("Apuntar");
    }

    private void DispararCraneo()
    {
        // Si el cupo está lleno y NO se permite disparar de más, saltamos el
        // disparo pero seguimos el ciclo (recarga) — el lanzador "amaga" pero
        // no escupe hasta que muera alguno de sus hijos y se libere una plaza.
        bool puedeDisparar = disparaConCupoLleno || HayCupoLibre();

        if (puedeDisparar && craneoPrefab != null && puntoDisparo != null)
        {
            GameObject craneo = Instantiate(
                craneoPrefab, puntoDisparo.position, Quaternion.identity);

            CraneoProyectil proyectil = craneo.GetComponent<CraneoProyectil>();
            if (proyectil != null)
            {
                proyectil.enabled = true;               // activar modo proyectil
                proyectil.Lanzar(CalcularArco(), this);  // ← "this" = dueño del cráneo
            }
            else
            {
                Debug.LogWarning("[LanzaCraneos] El craneoPrefab no tiene " +
                                 "CraneoProyectil — no se puede lanzar en arco.");
            }

            if (anim != null) anim.SetTrigger("Disparar");
        }

        // Pasar a recarga (haya disparado o amagado).
        // Acorralado → recarga más rápido: compensa su desventaja posicional.
        fase = FaseLanza.Recargando;
        faseTimer = tiempoRecarga *
            (acorralado ? multiplicadorRecargaAcorralado : 1f);
    }

    // ── Cálculo del arco ─────────────────────────────────────
    private Vector2 CalcularArco()
    {
        float dirX = facing;   // hacia donde encara (hacia Romerito)
        float vx = velocidadHorizontal + Random.Range(-variacionHorizontal, variacionHorizontal);
        float vy = velocidadVertical;

        // Ajuste opcional por distancia: cuanto más lejos, un poco más fuerte.
        if (apuntarAlJugador && player != null)
        {
            float dist = Mathf.Abs(player.position.x - transform.position.x);
            // Normalizamos contra detectionRange como referencia de "lejos".
            float factor = Mathf.Clamp(dist / Mathf.Max(1f, detectionRange),
                                       0.6f, factorApuntadoMax);
            vx *= factor;
        }

        return new Vector2(dirX * Mathf.Abs(vx), vy);
    }

    // ── Al recibir daño: despierta si dormía, y HUYE a reposicionarse ──
    protected override void OnHerido()
    {
        // Si lo golpean estando dormido, despierta de golpe (sin telegraph
        // largo). Recompensa al jugador que ataca la pila antes de que actúe.
        if (fase == FaseLanza.Dormido)
        {
            estado = Estado.Persiguiendo;
            AplicarSprite(spriteDespierto);
            if (anim != null) { anim.SetBool("Dormido", false); anim.SetTrigger("Despertar"); }
        }

        // Tras CUALQUIER golpe (salvo mientras despierta), marcamos que debe
        // huir. NO entramos en huida aquí: dejamos que el aturdimiento del
        // knockback (Estado.Herido de la base) ocurra primero, y arrancamos
        // la huida cuando ese stun termine (ver Update).
        if (huyeAlSerGolpeado && fase != FaseLanza.Despertando)
            quiereHuir = true;
    }

    private void EntrarHuida()
    {
        quiereHuir = false;
        estado = Estado.Persiguiendo;   // asegura que corra LogicaPersecucion
        fase = FaseLanza.Huyendo;
        faseTimer = maxTiempoHuida;
        acorralado = false;             // se recalcula al toparse (o no) con pared
        tiempoAtascado = 0f;            // reinicia el detector de atasco
        if (anim != null) anim.SetTrigger("Huir");
    }

    // ── Utilidad ─────────────────────────────────────────────
    private void AplicarSprite(Sprite s)
    {
        if (sr != null && s != null) sr.sprite = s;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        // Dibuja una previsualización aproximada del arco (solo editor).
        if (puntoDisparo == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);

        Vector2 pos = puntoDisparo.position;
        float dirX = (transform.localScale.x < 0) ? -1f : 1f;
        Vector2 vel = new Vector2(dirX * velocidadHorizontal, velocidadVertical);
        float g = Mathf.Abs(Physics2D.gravity.y);   // aprox (gravityScale = 1)

        Vector2 prev = pos;
        for (int i = 1; i <= 20; i++)
        {
            float t = i * 0.08f;
            Vector2 p = pos + vel * t + 0.5f * new Vector2(0f, -g) * t * t;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // [HUIDA] Radio de distancia deseada de huida (verde).
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, distanciaHuidaDeseada);
    }
}
