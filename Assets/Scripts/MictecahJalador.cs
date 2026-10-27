using UnityEngine;

// ============================================================
//  MictecahJalador — el cráneo que tira de la cuerda del cuauhcalli
// ============================================================
//
//  Enemigo del Encuentro del Pochtecah. IGNORA a Romerito: su único
//  objetivo es llegar al anclaje de la cuerda y jalar para subir la
//  jaula hacia el tope del tzompantli.
//
//  • Camina hacia el anclaje (a su "lugar" en la cuerda).
//  • Al llegar se ENGANCHA: se queda quieto mirando hacia la polea y
//    se registra en CuauhcalliPolea (cuenta para la velocidad de subida).
//  • Si recibe un golpe SUELTA la cuerda (el knockback lo aleja) y
//    vuelve a caminar hacia ella al recuperarse. Golpear jaladores
//    compra tiempo aunque no los mates.
//  • Daño por contacto normal (heredado de MictecahBase).
//
//  El EncuentroPochtecah le inyecta 'polea' al instanciarlo; en un
//  prefab suelto puedes asignarla a mano.
//
//  ANIMATOR (opcional): bool "Jalando", float "Speed".
// ============================================================

public class MictecahJalador : MictecahBase
{
    [Header("── Jalador ──")]
    [Tooltip("La jaula. La inyecta EncuentroPochtecah al instanciarlo.")]
    public CuauhcalliPolea polea;
    public float velocidadAlCabo = 3f;
    [Tooltip("Distancia horizontal a su lugar en la cuerda para engancharse.")]
    public float distanciaEnganche = 0.15f;
    [Tooltip("Cada jalador toma un lugar aleatorio detrás del anclaje (0 .. este valor).")]
    public float dispersionEnCuerda = 1.2f;

    [Header("── Jalón (feedback visual sin Animator) ──")]
    [Tooltip("Hijo con el sprite. Se inclina hacia atrás en cada jalón.")]
    public Transform visualJalon;
    public float anguloJalon = 12f;
    public float frecuenciaJalon = 1.6f;

    private bool enganchado = false;
    private float offsetEnCuerda;
    private Quaternion rotVisualOriginal;

    public bool Enganchado => enganchado;

    protected override bool ReaccionaAlJugador => false;

    protected override void Awake()
    {
        base.Awake();
        offsetEnCuerda = Random.Range(0f, dispersionEnCuerda);
        if (visualJalon != null) rotVisualOriginal = visualJalon.localRotation;
    }

    protected override void Start()
    {
        base.Start();
        estado = Estado.Persiguiendo;   // nace yendo directo a la cuerda
    }

    protected override void OnDisable()
    {
        Soltar();
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
        if (polea == null || polea.anclaje == null ||
            polea.Estado == CuauhcalliPolea.EstadoJaula.Liberada)
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
            MirarHacia(polea.DireccionHaciaPolea);
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
        base.Update();
        ActualizarJalon();
    }

    // ── Cuerda ───────────────────────────────────────────────

    float PosicionEnCuerda()
    {
        // Su lugar queda DETRÁS del anclaje (alejándose de la polea).
        int haciaAtras = -polea.DireccionHaciaPolea;
        return polea.anclaje.position.x + haciaAtras * offsetEnCuerda;
    }

    void Enganchar()
    {
        if (enganchado) return;
        enganchado = true;
        targetVelocity = Vector2.zero;
        polea.RegistrarJalador(this);
    }

    void Soltar()
    {
        if (!enganchado) return;
        enganchado = false;
        if (polea != null) polea.DesregistrarJalador(this);
        if (visualJalon != null) visualJalon.localRotation = rotVisualOriginal;
    }

    // ── Visual ───────────────────────────────────────────────

    void ActualizarJalon()
    {
        if (anim != null) anim.SetBool("Jalando", enganchado);
        if (visualJalon == null || !enganchado) return;

        // Se inclina hacia atrás (lejos de la polea) con un ritmo de jalón.
        float k = Mathf.Abs(Mathf.Sin(Time.time * frecuenciaJalon * Mathf.PI));
        visualJalon.localRotation = rotVisualOriginal * Quaternion.Euler(0f, 0f, anguloJalon * k);
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (polea == null || polea.anclaje == null) return;
        Gizmos.color = new Color(0.85f, 0.7f, 0.4f);
        Gizmos.DrawLine(transform.position, polea.anclaje.position);
    }
}
