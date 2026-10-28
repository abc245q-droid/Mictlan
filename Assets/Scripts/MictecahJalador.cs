using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  MictecahJalador — el cráneo que tira de la cuerda del cuauhcalli
// ============================================================
//
//  Enemigo del Encuentro del Pochtecah. IGNORA a Romerito: su único
//  objetivo es llegar al anclaje de la cuerda y jalar para subir la
//  jaula hacia el tope del tzompantli.
//
//  • Camina hacia el anclaje (a su "lugar" en la cuerda).
//  • Al llegar se ENGANCHA: se queda quieto mirando hacia el
//    tzompantli y se registra en CuauhcalliColgante (cuenta para la velocidad de subida).
//  • Si recibe un golpe SUELTA la cuerda (el knockback lo aleja) y
//    vuelve a caminar hacia ella al recuperarse. Golpear jaladores
//    compra tiempo aunque no los mates.
//  • Daño por contacto normal (heredado de MictecahBase).
//
//  ENTERRADO (mientras queden combatientes en la oleada)
//  • Se mete bajo tierra: INVULNERABLE, sin contacto con Romerito,
//    solo se ve un montículo de tierra. Sigue caminando hacia la
//    cuerda y sigue jalando desde abajo.
//  • Cuando el último combatiente cae, EMERGE: vuelve a ser visible
//    y vulnerable. Hay que matarlo para que la jaula caiga.
//  • Quien decide es EncuentroPochtecah.CombatientesEnJuego.
//
//  El EncuentroPochtecah le inyecta 'jaula' al instanciarlo; en un
//  prefab suelto puedes asignarla a mano.
//
//  ANIMATOR (opcional): bool "Jalando", float "Speed".
// ============================================================

public class MictecahJalador : MictecahBase
{
    [Header("── Jalador ──")]
    [Tooltip("La jaula. La inyecta EncuentroPochtecah al instanciarlo.")]
    [FormerlySerializedAs("polea")]
    public CuauhcalliColgante jaula;
    public float velocidadAlCabo = 3f;
    [Tooltip("Distancia horizontal a su lugar en la cuerda para engancharse.")]
    public float distanciaEnganche = 0.15f;
    [Tooltip("Cada jalador toma un lugar aleatorio detrás del anclaje (0 .. este valor).")]
    public float dispersionEnCuerda = 1.2f;

    [Tooltip("La inyecta EncuentroPochtecah. Sin encuentro, nunca se entierra.")]
    public EncuentroPochtecah encuentro;

    [Header("── Enterrado ──")]
    [Tooltip("Hijo con el sprite de un montículo de tierra. Se muestra mientras está enterrado.")]
    public GameObject monticulo;
    [Tooltip("Hijo con el sprite del cuerpo. Se hunde al enterrarse. Vacío = solo se desvanece " +
             "(si el sprite está en la raíz no se puede mover sin mover la física).")]
    public Transform visualCuerpo;
    public float profundidadEntierro = 0.6f;
    public float duracionTransicion = 0.25f;
    [Tooltip("Polvo/tierra al enterrarse y al emerger (opcional).")]
    public GameObject efectoTierra;
    public AudioSource fuenteAudio;
    public AudioClip sonidoEnterrar;
    public AudioClip sonidoEmerger;

    [Header("── Jalón (feedback visual sin Animator) ──")]
    [Tooltip("Hijo con el sprite. Se inclina hacia atrás en cada jalón.")]
    public Transform visualJalon;
    public float anguloJalon = 12f;
    public float frecuenciaJalon = 1.6f;

    private bool enganchado = false;
    private float offsetEnCuerda;
    private Quaternion rotVisualOriginal;

    private bool enterrado = false;
    private Coroutine transicionRutina;
    private SpriteRenderer[] spritesCuerpo;
    private Color[] coloresOriginales;
    private Vector3 posCuerpoOriginal;
    private Collider2D[] misColliders;
    private Collider2D[] collidersJugador;

    public bool Enganchado => enganchado;
    public bool Enterrado => enterrado;

    protected override bool ReaccionaAlJugador => false;

    protected override void Awake()
    {
        base.Awake();
        offsetEnCuerda = Random.Range(0f, dispersionEnCuerda);
        if (visualJalon != null) rotVisualOriginal = visualJalon.localRotation;

        if (visualCuerpo == transform) visualCuerpo = null;
        if (visualCuerpo != null) posCuerpoOriginal = visualCuerpo.localPosition;

        // Sprites del cuerpo = todos menos los del montículo.
        var lista = new List<SpriteRenderer>();
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
            if (monticulo == null || !sr.transform.IsChildOf(monticulo.transform)) lista.Add(sr);
        spritesCuerpo = lista.ToArray();
        coloresOriginales = new Color[spritesCuerpo.Length];
        for (int i = 0; i < spritesCuerpo.Length; i++) coloresOriginales[i] = spritesCuerpo[i].color;

        misColliders = GetComponentsInChildren<Collider2D>(true);
        if (monticulo != null) monticulo.SetActive(false);
    }

    protected override void Start()
    {
        base.Start();
        estado = Estado.Persiguiendo;   // nace yendo directo a la cuerda
        if (jaula == null)
            Debug.LogWarning($"[MictecahJalador] '{name}' sin jaula asignada: no sabe a qué cuerda ir.", this);
        else if (jaula.anclaje == null)
            Debug.LogWarning($"[MictecahJalador] La jaula no tiene 'anclaje'.", this);
    }

    protected override void OnDisable()
    {
        Soltar();
        if (enterrado) IgnorarJugador(false);
        base.OnDisable();
    }

    void OnDestroy() => Soltar();

    // Nunca patrulla: si la base lo devuelve a Patrullando (tras un
    // golpe o una espera), regresa de inmediato a buscar la cuerda.
    protected override void LogicaPatrulla()
    {
        estado = Estado.Persiguiendo;
    }

    protected override void ChequearJugador() { }

    protected override void LogicaPersecucion()
    {
        if (jaula == null || jaula.anclaje == null ||
            jaula.Estado == CuauhcalliColgante.EstadoJaula.Liberada)
        {
            Soltar();
            targetVelocity = Vector2.zero;
            return;
        }

        float objetivoX = PosicionEnCuerda();
        float dx = objetivoX - transform.position.x;

        if (enganchado)
        {
            targetVelocity = Vector2.zero;
            MirarHacia(jaula.DireccionHaciaTravesano);
            // Si algo lo empujó lejos de su lugar, suelta.
            if (Mathf.Abs(dx) > distanciaEnganche * 4f) Soltar();
            return;
        }

        if (Mathf.Abs(dx) <= distanciaEnganche)
        {
            Enganchar();
            return;
        }

        int dir = dx > 0f ? 1 : -1;
        MirarHacia(dir);
        targetVelocity = new Vector2(dir * velocidadAlCabo, 0f);
    }

    protected override void OnHerido()
    {
        Soltar();
    }

    protected override void Update()
    {
        bool debeEnterrarse = encuentro != null && encuentro.CombatientesEnJuego;
        if (debeEnterrarse != enterrado)
        {
            if (debeEnterrarse) Enterrar(); else Emerger();
        }

        base.Update();
        ActualizarJalon();
    }

    // Enterrado no hace daño por contacto (además, las colisiones con
    // Romerito están ignoradas).
    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        if (enterrado) return;
        base.OnCollisionEnter2D(collision);
    }

    // ── Enterrar / emerger ───────────────────────────────────

    void Enterrar()
    {
        enterrado = true;
        if (dummy != null) dummy.SetInvulnerable(true);
        IgnorarJugador(true);
        Efecto(sonidoEnterrar);
        if (anim != null) anim.SetBool("Enterrado", true);
        IniciarTransicion(haciaAbajo: true);
    }

    void Emerger()
    {
        enterrado = false;
        if (dummy != null) dummy.SetInvulnerable(false);
        IgnorarJugador(false);
        Efecto(sonidoEmerger);
        if (anim != null) anim.SetBool("Enterrado", false);
        IniciarTransicion(haciaAbajo: false);
    }

    void IgnorarJugador(bool ignorar)
    {
        if (collidersJugador == null || collidersJugador.Length == 0)
        {
            GameObject p = player != null ? player.gameObject : GameObject.FindGameObjectWithTag("Player");
            if (p != null) collidersJugador = p.GetComponentsInChildren<Collider2D>(true);
        }
        if (collidersJugador == null || misColliders == null) return;

        foreach (Collider2D mio in misColliders)
            foreach (Collider2D suyo in collidersJugador)
                if (mio != null && suyo != null) Physics2D.IgnoreCollision(mio, suyo, ignorar);
    }

    void Efecto(AudioClip clip)
    {
        if (efectoTierra != null) Instantiate(efectoTierra, transform.position, Quaternion.identity);
        if (fuenteAudio != null && clip != null) fuenteAudio.PlayOneShot(clip);
    }

    void IniciarTransicion(bool haciaAbajo)
    {
        if (transicionRutina != null) StopCoroutine(transicionRutina);
        if (!isActiveAndEnabled) { AplicarTransicion(haciaAbajo ? 1f : 0f); FinTransicion(haciaAbajo); return; }
        transicionRutina = StartCoroutine(RutinaTransicion(haciaAbajo));
    }

    IEnumerator RutinaTransicion(bool haciaAbajo)
    {
        if (!haciaAbajo)
        {
            // Al emerger: el cuerpo reaparece desde abajo y el montículo se va.
            foreach (SpriteRenderer sr in spritesCuerpo) if (sr != null) sr.enabled = true;
            if (monticulo != null) monticulo.SetActive(false);
        }
        else if (monticulo != null) monticulo.SetActive(true);

        float t = 0f;
        while (t < duracionTransicion)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, duracionTransicion));
            AplicarTransicion(haciaAbajo ? k : 1f - k);
            yield return null;
        }
        AplicarTransicion(haciaAbajo ? 1f : 0f);
        FinTransicion(haciaAbajo);
        transicionRutina = null;
    }

    // 0 = en la superficie, 1 = enterrado
    void AplicarTransicion(float k)
    {
        if (visualCuerpo != null)
            visualCuerpo.localPosition = posCuerpoOriginal + Vector3.down * profundidadEntierro * k;

        for (int i = 0; i < spritesCuerpo.Length; i++)
        {
            if (spritesCuerpo[i] == null) continue;
            Color c = coloresOriginales[i];
            c.a *= 1f - k;
            spritesCuerpo[i].color = c;
        }
    }

    void FinTransicion(bool enterradoAhora)
    {
        foreach (SpriteRenderer sr in spritesCuerpo) if (sr != null) sr.enabled = !enterradoAhora;
        if (monticulo != null) monticulo.SetActive(enterradoAhora);
        if (!enterradoAhora)
            for (int i = 0; i < spritesCuerpo.Length; i++)
                if (spritesCuerpo[i] != null) spritesCuerpo[i].color = coloresOriginales[i];
    }

    // ── Cuerda ───────────────────────────────────────────────

    float PosicionEnCuerda()
    {
        // Su lugar queda DETRÁS del anclaje (alejándose del tzompantli).
        int haciaAtras = -jaula.DireccionHaciaTravesano;
        return jaula.anclaje.position.x + haciaAtras * offsetEnCuerda;
    }

    void Enganchar()
    {
        if (enganchado) return;
        enganchado = true;
        targetVelocity = Vector2.zero;
        jaula.RegistrarJalador(this);
        Debug.Log($"[MictecahJalador] '{name}' enganchado a la cuerda ({jaula.JaladoresEnCuerda} jalando).", this);
    }

    void Soltar()
    {
        if (!enganchado) return;
        enganchado = false;
        if (jaula != null) jaula.DesregistrarJalador(this);
        if (visualJalon != null) visualJalon.localRotation = rotVisualOriginal;
    }

    // ── Visual ───────────────────────────────────────────────

    void ActualizarJalon()
    {
        if (anim != null) anim.SetBool("Jalando", enganchado);
        if (visualJalon == null || !enganchado) return;

        // Se inclina hacia atrás (lejos del tzompantli) con un ritmo de jalón.
        float k = Mathf.Abs(Mathf.Sin(Time.time * frecuenciaJalon * Mathf.PI));
        visualJalon.localRotation = rotVisualOriginal * Quaternion.Euler(0f, 0f, anguloJalon * k);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (jaula == null || jaula.anclaje == null) return;
        Gizmos.color = new Color(0.85f, 0.7f, 0.4f);
        Gizmos.DrawLine(transform.position, jaula.anclaje.position);
    }
}
