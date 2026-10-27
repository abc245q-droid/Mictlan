using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  CuauhcalliColgante — la jaula del Pochtecah colgada del tzompantli
// ============================================================
//
//  El cuauhcalli ("casa de madera", jaula de vigas amarradas donde
//  los mexicas guardaban prisioneros) cuelga de una cuerda echada
//  por ENCIMA del travesaño más alto del tzompantli. El otro extremo
//  baja al suelo, al ANCLAJE, donde los Mictecah jaladores tiran.
//
//  SIN POLEA: en Mesoamérica no hubo rueda con eje ni roldana. La
//  cuerda simplemente se desliza sobre la viga de madera por
//  fricción — por eso un jalador solo la sube despacio y cada uno
//  extra la acelera.
//
//  REGLAS (fuente de verdad del evento)
//   • Amarrada  → al inicio la cuerda está atada a una estaca: la jaula
//                 se queda quieta a 'alturaInicial'. El primer jalador
//                 que llega desata el amarre.
//   • Subiendo  → con ≥1 jalador en la cuerda. Primero un IZADO RÁPIDO
//                 hasta 'alturaFueraDeAlcance' (la ventana se cierra
//                 limpia) y luego una subida lenta hacia el tope;
//                 cada jalador extra la acelera.
//   • Cayendo   → nadie jala: cae con gravedad.
//   • EnSuelo   → ÚNICO estado golpeable. Golpe seco, polvo, sacudida.
//   • Reclamada → si llega a 'alturaTope' los Mictecah se lo llevan:
//                 las amarras se rehacen y la jaula RECUPERA una etapa
//                 ya rota. Luego baja a 'alturaFueraDeAlcance' y la
//                 subida recomienza (tras 'pausaTrasReclamar').
//
//  ETAPAS (medidas en golpes)
//   golpesPorEtapa = {6, 8, 10} → 3 etapas. Al romperse una etapa la
//   jaula queda BLOQUEADA (rebota los golpes) hasta que vuelva a ser
//   izada: así un solo descenso nunca rompe dos etapas seguidas.
//   Al romperse la última: LIBERADA.
//
//  El Pochtecah NUNCA recibe daño: nada en este script ni en el
//  evento le resta vida. La única amenaza es perder etapas.
//
//  SETUP EN UNITY → ver la guía de setup del Encuentro del Pochtecah.
// ============================================================

public class CuauhcalliColgante : MonoBehaviour, IGolpeableMacahuitl
{
    public enum EstadoJaula { Amarrada, Subiendo, Cayendo, EnSuelo, Liberada }

    // ── Recorrido vertical ───────────────────────────────────
    [Header("Recorrido (alturas en unidades sobre 'puntoSuelo')")]
    [Tooltip("Posición de la jaula cuando descansa en el suelo (pivote de la jaula).")]
    public Transform puntoSuelo;
    [Tooltip("Altura a la que está amarrada antes de empezar el evento.")]
    public float alturaInicial = 4f;
    [Tooltip("Por debajo de esta altura Romerito podría alcanzarla. El izado rápido la sube hasta aquí.")]
    public float alturaFueraDeAlcance = 3.5f;
    [Tooltip("Si llega aquí, los Mictecah la reclaman (pierde una etapa).")]
    public float alturaTope = 7f;

    [Header("Seguridad de alcance (solo validación visual)")]
    [Tooltip("Apex del salto + alcance del hitbox del Macahuitl + margen, medido en Unity. " +
             "El gizmo se pinta en ROJO si alturaFueraDeAlcance queda por debajo.")]
    public float alcanceMaxRomerito = 3f;

    [Header("Velocidades")]
    [Tooltip("Velocidad del izado rápido desde el suelo hasta alturaFueraDeAlcance.")]
    public float velocidadIzado = 9f;
    [Tooltip("Subida lenta con UN jalador (u/s).")]
    public float velocidadSubidaBase = 0.35f;
    [Tooltip("Velocidad extra por cada jalador adicional (u/s).")]
    public float velocidadPorJaladorExtra = 0.25f;
    [Tooltip("Gravedad de la caída (u/s²).")]
    public float gravedadCaida = 30f;
    [Tooltip("Tras ser reclamada baja a alturaFueraDeAlcance en este tiempo y se queda quieta un momento.")]
    public float pausaTrasReclamar = 1.2f;

    // ── Etapas ───────────────────────────────────────────────
    [Header("Etapas (golpes por etapa)")]
    public int[] golpesPorEtapa = { 6, 8, 10 };
    [Tooltip("Ignora golpes durante este tiempo tras uno (mismo swing tocando dos veces).")]
    public float invulnerabilidadTrasGolpe = 0.15f;

    // ── Visual ───────────────────────────────────────────────
    [Header("Visual")]
    [Tooltip("Hijo que tiembla al golpear (el sprite de la jaula). El collider va en la raíz.")]
    public Transform visual;
    public SpriteRenderer spriteJaula;
    [Tooltip("[0] intacta · [1] 1 etapa rota · [2] 2 etapas rotas · [3] abierta/rota")]
    public Sprite[] spritesPorEtapa;
    [Tooltip("Sprite del Pochtecah dentro de la jaula (hijo de 'visual'). Se oculta al liberarlo.")]
    public SpriteRenderer pochtecahEnJaula;
    public float duracionTemblor = 0.14f;
    public float amplitudTemblor = 0.07f;
    [Tooltip("Balanceo leve mientras cuelga (grados).")]
    public float balanceoColgada = 3f;
    public float frecuenciaBalanceo = 1.4f;

    [Header("Cuerda (LineRenderer: gancho → travesaño → anclaje)")]
    public LineRenderer cuerda;
    [Tooltip("Hijo de la jaula donde se amarra la cuerda (arriba al centro).")]
    public Transform gancho;
    [Tooltip("Punto del travesaño del tzompantli sobre el que se desliza la cuerda.")]
    [FormerlySerializedAs("puntoTravesano")]
    public Transform puntoTravesano;
    [Tooltip("Punto en el suelo donde los jaladores toman la cuerda.")]
    public Transform anclaje;

    [Header("Feedback")]
    public GameObject efectoGolpe;
    public GameObject efectoEtapaRota;
    public GameObject efectoCaida;
    public GameObject efectoLiberada;
    [Tooltip("Opcional: sacudida de cámara al caer y al romper etapa.")]
    public CinemachineImpulseSource impulso;
    public float fuerzaImpulsoCaida = 0.6f;
    public float fuerzaImpulsoEtapa = 1f;

    [Header("Audio (opcional)")]
    public AudioSource fuenteAudio;
    public AudioClip sonidoGolpe;
    public AudioClip sonidoBloqueado;
    public AudioClip sonidoEtapaRota;
    public AudioClip sonidoCaida;
    public AudioClip sonidoReclamada;
    public AudioClip sonidoLiberada;

    [Header("Eventos para diseño (barks, VFX extra)")]
    public UnityEvent OnCayoAlSuelo;
    public UnityEvent OnIzada;
    public UnityEvent OnEtapaRotaUE;
    public UnityEvent OnReclamadaUE;
    public UnityEvent OnLiberadaUE;

    // ── Eventos de código (EncuentroPochtecah) ───────────────
    public event System.Action<int> OnEtapaRota;   // etapas rotas tras el golpe
    public event System.Action OnReclamada;
    public event System.Action OnLiberada;
    public event System.Action OnTocaSuelo;

    // ── Estado ───────────────────────────────────────────────
    public EstadoJaula Estado { get; private set; } = EstadoJaula.Amarrada;
    public int EtapasRotas { get; private set; } = 0;
    public int TotalEtapas => golpesPorEtapa != null ? golpesPorEtapa.Length : 0;
    public bool Bloqueada { get; private set; } = false;
    public int JaladoresEnCuerda => jaladores.Count;
    public float Altura => altura;
    public bool EsGolpeable => Estado == EstadoJaula.EnSuelo && !Bloqueada;

    /// <summary>Dirección x (±1) hacia la que miran los jaladores (hacia el tzompantli).</summary>
    public int DireccionHaciaTravesano =>
        (puntoTravesano != null && anclaje != null && puntoTravesano.position.x < anclaje.position.x) ? -1 : 1;

    private readonly HashSet<MictecahJalador> jaladores = new HashSet<MictecahJalador>();
    private float altura;
    private float velCaida;
    private int golpesEnEtapa;
    private float proximoGolpeT;
    private float pausaRestante;
    private Vector3 visualPosOriginal;
    private Quaternion visualRotOriginal;
    private Coroutine temblorRutina;

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        if (spriteJaula == null) spriteJaula = GetComponentInChildren<SpriteRenderer>();
        if (visual == null && spriteJaula != null) visual = spriteJaula.transform;
        if (visual != null)
        {
            visualPosOriginal = visual.localPosition;
            visualRotOriginal = visual.localRotation;
        }
        ReiniciarCompleta();
    }

    void Update()
    {
        // Limpieza defensiva (un jalador destruido sin desregistrarse).
        jaladores.RemoveWhere(j => j == null);

        switch (Estado)
        {
            case EstadoJaula.Amarrada:
                if (jaladores.Count > 0) Estado = EstadoJaula.Subiendo;
                break;

            case EstadoJaula.Subiendo:
                ActualizarSubida();
                break;

            case EstadoJaula.Cayendo:
                ActualizarCaida();
                break;

            case EstadoJaula.EnSuelo:
                if (jaladores.Count > 0) Izar();
                break;
        }

        AplicarPosicion();
        ActualizarBalanceo();
    }

    void LateUpdate() => ActualizarCuerda();

    // ── Movimiento ───────────────────────────────────────────

    void ActualizarSubida()
    {
        if (jaladores.Count == 0)
        {
            Estado = EstadoJaula.Cayendo;
            velCaida = 0f;
            return;
        }

        if (pausaRestante > 0f) { pausaRestante -= Time.deltaTime; return; }

        float vel = altura < alturaFueraDeAlcance
            ? velocidadIzado
            : velocidadSubidaBase + velocidadPorJaladorExtra * (jaladores.Count - 1);

        altura = Mathf.Min(alturaTope, altura + vel * Time.deltaTime);

        if (altura >= alturaTope) Reclamar();
    }

    void ActualizarCaida()
    {
        if (jaladores.Count > 0)
        {
            Estado = EstadoJaula.Subiendo;
            return;
        }

        velCaida += gravedadCaida * Time.deltaTime;
        altura -= velCaida * Time.deltaTime;

        if (altura <= 0f)
        {
            altura = 0f;
            velCaida = 0f;
            TocarSuelo();
        }
    }

    void Izar()
    {
        Estado = EstadoJaula.Subiendo;
        // Se acabó el bloqueo: la próxima vez que caiga, la siguiente
        // etapa ya acepta golpes.
        Bloqueada = false;
        OnIzada?.Invoke();
    }

    void TocarSuelo()
    {
        Estado = EstadoJaula.EnSuelo;
        Sonar(sonidoCaida);
        Instanciar(efectoCaida);
        Sacudir(fuerzaImpulsoCaida);
        Temblar();
        OnCayoAlSuelo?.Invoke();
        OnTocaSuelo?.Invoke();
    }

    void Reclamar()
    {
        // Las amarras se rehacen: se pierde una etapa ya rota y los
        // golpes de la etapa en curso.
        if (EtapasRotas > 0) EtapasRotas--;
        golpesEnEtapa = 0;
        Bloqueada = false;
        ActualizarSprite();

        altura = alturaFueraDeAlcance;
        pausaRestante = pausaTrasReclamar;

        Sonar(sonidoReclamada);
        Sacudir(fuerzaImpulsoEtapa);
        Temblar();
        Debug.Log($"[Cuauhcalli] ¡Reclamada! Etapas rotas: {EtapasRotas}/{TotalEtapas}");
        OnReclamadaUE?.Invoke();
        OnReclamada?.Invoke();
    }

    void AplicarPosicion()
    {
        if (puntoSuelo == null) return;
        transform.position = puntoSuelo.position + Vector3.up * altura;
    }

    void ActualizarBalanceo()
    {
        if (visual == null || temblorRutina != null) return;
        float k = (Estado == EstadoJaula.EnSuelo || Estado == EstadoJaula.Liberada) ? 0f : 1f;
        float ang = Mathf.Sin(Time.time * frecuenciaBalanceo * Mathf.PI * 2f) * balanceoColgada * k;
        visual.localRotation = visualRotOriginal * Quaternion.Euler(0f, 0f, ang);
    }

    void ActualizarCuerda()
    {
        if (cuerda == null) return;
        bool visible = Estado != EstadoJaula.Liberada && gancho != null && puntoTravesano != null;
        cuerda.enabled = visible;
        if (!visible) return;

        cuerda.positionCount = anclaje != null ? 3 : 2;
        cuerda.SetPosition(0, gancho.position);
        cuerda.SetPosition(1, puntoTravesano.position);
        if (anclaje != null) cuerda.SetPosition(2, anclaje.position);
    }

    // ── Jaladores ────────────────────────────────────────────

    public void RegistrarJalador(MictecahJalador j)
    {
        if (j == null || Estado == EstadoJaula.Liberada) return;
        jaladores.Add(j);
    }

    public void DesregistrarJalador(MictecahJalador j)
    {
        if (j == null) return;
        jaladores.Remove(j);
    }

    // ── IGolpeableMacahuitl ──────────────────────────────────

    public bool RecibirGolpeMacahuitl(Vector2 origenAtacante, int dano, RomeritoCombat.GodFavor favor)
    {
        // Colgada: no "conecta" (en teoría ni se alcanza).
        if (Estado != EstadoJaula.EnSuelo) return false;

        if (Time.time < proximoGolpeT) return true;
        proximoGolpeT = Time.time + invulnerabilidadTrasGolpe;

        // Etapa recién rota: rebota hasta que la vuelvan a izar.
        if (Bloqueada)
        {
            Sonar(sonidoBloqueado);
            Temblar(0.5f);
            return true;
        }

        // El daño se mide en GOLPES, no en puntos: cualquier swing = 1.
        golpesEnEtapa++;
        Sonar(sonidoGolpe);
        Instanciar(efectoGolpe);
        Temblar();

        int necesarios = (EtapasRotas < TotalEtapas) ? golpesPorEtapa[EtapasRotas] : 1;
        if (golpesEnEtapa >= necesarios) RomperEtapa();
        return true;
    }

    void RomperEtapa()
    {
        EtapasRotas++;
        golpesEnEtapa = 0;

        if (EtapasRotas >= TotalEtapas)
        {
            Liberar(instantaneo: false);
            return;
        }

        Bloqueada = true;
        ActualizarSprite();
        Sonar(sonidoEtapaRota);
        Instanciar(efectoEtapaRota);
        Sacudir(fuerzaImpulsoEtapa);
        Debug.Log($"[Cuauhcalli] Etapa rota: {EtapasRotas}/{TotalEtapas}");
        OnEtapaRotaUE?.Invoke();
        OnEtapaRota?.Invoke(EtapasRotas);
    }

    // ── API del evento ───────────────────────────────────────

    /// <summary>Estado inicial: amarrada en lo alto, intacta. Usado al iniciar y al resetear.</summary>
    public void ReiniciarCompleta()
    {
        jaladores.Clear();
        Estado = EstadoJaula.Amarrada;
        EtapasRotas = 0;
        golpesEnEtapa = 0;
        Bloqueada = false;
        altura = alturaInicial;
        velCaida = 0f;
        pausaRestante = 0f;
        if (temblorRutina != null) { StopCoroutine(temblorRutina); temblorRutina = null; }
        if (visual != null)
        {
            visual.localPosition = visualPosOriginal;
            visual.localRotation = visualRotOriginal;
        }
        if (pochtecahEnJaula != null) pochtecahEnJaula.enabled = true;
        ActualizarSprite();
        AplicarPosicion();
    }

    /// <summary>
    /// Si sigue amarrada a la estaca (ningún jalador llegó a desatarla),
    /// la suelta y cae. El evento lo llama al abrir la ventana.
    /// </summary>
    public void SoltarAmarre()
    {
        if (Estado != EstadoJaula.Amarrada) return;
        Estado = EstadoJaula.Cayendo;
        velCaida = 0f;
    }

    /// <summary>
    /// Abre la jaula. 'instantaneo' = carga de partida con el evento ya
    /// resuelto (sin VFX, sin eventos de código).
    /// </summary>
    public void Liberar(bool instantaneo)
    {
        jaladores.Clear();
        Estado = EstadoJaula.Liberada;
        EtapasRotas = TotalEtapas;
        Bloqueada = false;
        altura = 0f;
        AplicarPosicion();
        ActualizarSprite();
        if (pochtecahEnJaula != null) pochtecahEnJaula.enabled = false;
        if (cuerda != null) cuerda.enabled = false;

        if (instantaneo) return;

        Sonar(sonidoLiberada);
        Instanciar(efectoLiberada);
        Sacudir(fuerzaImpulsoEtapa);
        Debug.Log("[Cuauhcalli] ¡Liberada!");
        OnLiberadaUE?.Invoke();
        OnLiberada?.Invoke();
    }

    // ── Feedback ─────────────────────────────────────────────

    void ActualizarSprite()
    {
        if (spriteJaula == null || spritesPorEtapa == null || spritesPorEtapa.Length == 0) return;
        int i = Mathf.Clamp(EtapasRotas, 0, spritesPorEtapa.Length - 1);
        if (spritesPorEtapa[i] != null) spriteJaula.sprite = spritesPorEtapa[i];
    }

    void Temblar(float escala = 1f)
    {
        if (visual == null) return;
        if (temblorRutina != null) StopCoroutine(temblorRutina);
        temblorRutina = StartCoroutine(RutinaTemblor(escala));
    }

    IEnumerator RutinaTemblor(float escala)
    {
        float t = 0f;
        while (t < duracionTemblor)
        {
            t += Time.deltaTime;
            float k = 1f - t / Mathf.Max(0.0001f, duracionTemblor);
            Vector2 r = Random.insideUnitCircle * amplitudTemblor * escala * k;
            visual.localPosition = visualPosOriginal + new Vector3(r.x, r.y, 0f);
            yield return null;
        }
        visual.localPosition = visualPosOriginal;
        temblorRutina = null;
    }

    void Sonar(AudioClip clip)
    {
        if (fuenteAudio != null && clip != null) fuenteAudio.PlayOneShot(clip);
    }

    void Instanciar(GameObject fx)
    {
        if (fx != null) Instantiate(fx, transform.position, Quaternion.identity);
    }

    void Sacudir(float fuerza)
    {
        if (impulso != null && fuerza > 0f) impulso.GenerateImpulse(fuerza);
    }

    // ── Gizmos ───────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (puntoSuelo == null) return;
        Vector3 b = puntoSuelo.position;

        // Suelo
        Gizmos.color = Color.green;
        Gizmos.DrawLine(b + Vector3.left, b + Vector3.right);

        // Alcance de Romerito
        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
        Vector3 a = b + Vector3.up * alcanceMaxRomerito;
        Gizmos.DrawLine(a + Vector3.left * 1.5f, a + Vector3.right * 1.5f);

        // Fuera de alcance (rojo si NO está por encima del alcance)
        Gizmos.color = alturaFueraDeAlcance > alcanceMaxRomerito ? Color.cyan : Color.red;
        Vector3 f = b + Vector3.up * alturaFueraDeAlcance;
        Gizmos.DrawLine(f + Vector3.left, f + Vector3.right);

        // Inicial
        Gizmos.color = Color.yellow;
        Vector3 i = b + Vector3.up * alturaInicial;
        Gizmos.DrawWireSphere(i, 0.15f);

        // Tope (reclamada)
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f);
        Vector3 tp = b + Vector3.up * alturaTope;
        Gizmos.DrawLine(tp + Vector3.left, tp + Vector3.right);
        Gizmos.DrawLine(b, tp);

        // Cuerda
        if (puntoTravesano != null)
        {
            Gizmos.color = new Color(0.85f, 0.7f, 0.4f);
            Gizmos.DrawLine(i, puntoTravesano.position);
            if (anclaje != null) Gizmos.DrawLine(puntoTravesano.position, anclaje.position);
        }
    }
}
