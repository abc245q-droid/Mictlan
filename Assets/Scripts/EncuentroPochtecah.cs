using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  EncuentroPochtecah — evento "tipo jefe" del Nivel 1 (Chicunamictlán)
// ============================================================
//
//  Los Mictecah capturaron al Pochtecah y lo quieren llevar al
//  tzompantli. Lo tienen encerrado en un cuauhcalli que cuelga de la
//  cuerda echada sobre el travesaño del tzompantli (CuauhcalliColgante). No hay jefe: el rival es
//  el propio tzompantli, que suelta oleadas de cráneos.
//
//  CICLO (se repite hasta liberar las 3 etapas)
//   1. OLEADA   → el tzompantli suelta combatientes + JALADORES.
//                 Los jaladores van a la cuerda e izan la jaula.
//                 Mientras queden combatientes, el tzompantli REPONE
//                 jaladores caídos (cooldown).
//   2. LIMPIA   → sin nadie en la cuerda la jaula CAE: VENTANA.
//                 Dura 'duracionVentana' o hasta romper la etapa.
//   3. SIGUIENTE→ si rompiste una etapa en este ciclo: oleada de la
//                 siguiente etapa. Si NO: 'oleadaExtra' (corta) y
//                 otra ventana. El daño de la etapa se conserva.
//
//  La composición de cada oleada sale de 'oleadas[EtapasRotas]'. Las
//  etapas rotas NUNCA se pierden: la jaula solo sube hasta su tope.
//
//  Las mini-ventanas son emergentes: si matas a todos los jaladores
//  a media oleada la jaula cae y se puede golpear hasta que llegue
//  el siguiente.
//
//  MUERTE DE ROMERITO → reset completo (mismo patrón que
//  MacahuitlRoomManager: rutina inyectada en RomeritoHealth).
//
//  FINAL → el tzompantli se apaga, los cráneos restantes se
//  deshacen, se abren las puertas, se guarda el evento y el
//  Pochtecah libre entrega el Códice Maestro (PochtecahNPC con
//  regalaCodiceMaestro = true).
//
//  PERSISTENCIA → PlayerData.collectedItems contiene 'eventoID'.
// ============================================================

[RequireComponent(typeof(Collider2D))]
public class EncuentroPochtecah : MonoBehaviour
{
    // ── Datos ────────────────────────────────────────────────

    [System.Serializable]
    public class GrupoEnemigos
    {
        public GameObject prefab;
        public int cantidad = 2;
    }

    [System.Serializable]
    public class OleadaPochtecah
    {
        public string nombre = "Oleada";
        [Tooltip("Pausa antes de que tiemble el primer cráneo.")]
        public float retrasoInicial = 1.2f;

        [Header("Combatientes")]
        public List<GrupoEnemigos> combatientes = new List<GrupoEnemigos>();
        [Tooltip("Segundos entre cada combatiente.")]
        public float intervaloCombatientes = 1.2f;

        [Header("Jaladores")]
        public GameObject prefabJalador;
        [Tooltip("Jaladores que salen al inicio de la oleada.")]
        public int jaladoresIniciales = 1;
        [Tooltip("Si un jalador muere y quedan combatientes, el tzompantli suelta otro.")]
        public bool reponerJaladores = false;
        [Tooltip("Máximo de jaladores vivos a la vez (al reponer).")]
        public int jaladoresSimultaneos = 1;
        [Tooltip("Segundos desde que muere un jalador hasta que tiembla el siguiente cráneo.")]
        public float cooldownReposicion = 4f;
    }

    // ── Inspector ────────────────────────────────────────────

    [Header("Identidad / Persistencia")]
    [Tooltip("Se guarda en PlayerData.collectedItems al liberar al Pochtecah.")]
    public string eventoID = "evento_pochtecah_n1";

    [Header("Referencias")]
    public CuauhcalliColgante jaula;
    [Tooltip("Puntos de aparición con TzompantliPresagio (cráneos del rack).")]
    public Transform[] puntosAparicion;
    [Tooltip("Opcional: puntos solo para jaladores (los más cercanos al anclaje). Vacío = puntosAparicion.")]
    public Transform[] puntosJaladores;
    [Tooltip("Área de la sala: en el reset solo se destruyen enemigos dentro.")]
    public Collider2D limitesSala;

    [Header("Puertas")]
    public RoomDoor puertaEntrada;
    [Tooltip("Opcional.")]
    public RoomDoor puertaSalida;

    [Header("Pochtecah libre")]
    [Tooltip("GameObject del Pochtecah con PochtecahNPC (regalaCodiceMaestro = true). " +
             "INACTIVO en la escena: se activa al liberarlo.")]
    public GameObject pochtecahLibre;
    [Tooltip("Al liberarlo arranca solo el diálogo de entrega del Códice (sin pulsar B).")]
    public bool entregarCodiceAutomaticamente = true;
    public float retrasoEntrega = 1.5f;

    [Header("Diálogos")]
    [Tooltip("Al sellarse la sala, antes de la primera oleada (súplica del Pochtecah). " +
             "Si se deja vacío se usan las 'lineasIntro' de abajo.")]
    public Conversation dialogoIntro;
    [Tooltip("Respaldo sin asset: se arma una conversación con estas líneas.")]
    [TextArea(1, 3)]
    public string[] lineasIntro =
    {
        "¡Ayuda! ¡Ayúdame, pequeño!",
        "Estos mictecah me sorprendieron en el camino y me encerraron en este cuauhcalli.",
        "¡Quieren llevarme al tzompantli! ¡No dejes que me suban!"
    };
    public string nombrePochtecah = "Pochtecah";
    public Color colorNombrePochtecah = new Color(0.95f, 0.75f, 0.35f, 1f);

    [Header("Intro")]
    [Tooltip("Romerito no se puede mover desde que entra hasta que la jaula queda fuera de su alcance.")]
    public bool congelarRomeritoEnIntro = true;
    [Tooltip("Tras el diálogo sale un jalador desde el cráneo más cercano al anclaje y sube la jaula. " +
             "Cuenta como uno de los jaladoresIniciales de la primera oleada.")]
    public bool jaladorDeIntro = true;
    [Tooltip("Seguro: si la jaula no sube en este tiempo, Romerito se libera igual.")]
    public float tiempoMaximoIntro = 8f;

    [Header("Oleadas (índice = etapas rotas)")]
    public OleadaPochtecah[] oleadas = new OleadaPochtecah[3];
    [Tooltip("Se usa cuando la ventana termina SIN romper etapa.")]
    public OleadaPochtecah oleadaExtra;

    [Header("Respuesta al romper una etapa")]
    [Tooltip("Al romperse una etapa el tzompantli suelta DE INMEDIATO un jalador desde el cráneo " +
             "más cercano al anclaje, para que la jaula rota no se quede en el suelo.")]
    public bool jaladorDeRespuesta = true;
    [Tooltip("TODOS los jaladores salen del cráneo libre más cercano al anclaje (llegan rápido " +
             "a la cuerda, el reemplazo se nota). Falso = cráneo al azar.")]
    public bool jaladoresDesdeAnclaje = true;

    [Header("Ventana")]
    public float duracionVentana = 5f;
    [Tooltip("Cada ventana fallida seguida alarga la siguiente estos segundos (piedad acumulativa).")]
    public float extensionPorFallo = 1.5f;
    [Tooltip("Tope de la extensión acumulada.")]
    public float extensionMaxima = 6f;
    [Tooltip("Pausa tras romper una etapa antes de la siguiente oleada.")]
    public float pausaTrasEtapa = 1.2f;

    [Header("Reinicio")]
    [Tooltip("Opcional: si se asigna, Romerito reaparece aquí (umbral) al morir en el evento. " +
             "Vacío = reaparece en su último Cihuacalli.")]
    public Transform puntoReaparicion;
    public float pausaTrasReset = 0.5f;
    public string tagEnemigo = "Enemy";

    [Header("Presagio")]
    [Tooltip("No elegir un cráneo que ya está temblando si hay otro libre.")]
    public bool evitarPuntoOcupado = true;

    [Header("Diagnóstico")]
    [Tooltip("Escribe en consola por qué el evento arranca o no (desactívalo al terminar el setup).")]
    public bool logsDiagnostico = true;

    [Header("Eventos para diseño")]
    public UnityEvent OnSellada;
    public UnityEvent OnOleadaIniciada;
    public UnityEvent OnVentanaAbierta;
    public UnityEvent OnVentanaCerrada;
    public UnityEvent OnPochtecahLiberado;

    // ── Estado ───────────────────────────────────────────────

    public enum Fase { Inactivo, Intro, Oleada, Ventana, Final, Resuelto }
    public Fase FaseActual { get; private set; } = Fase.Inactivo;

    private bool jugadorEnSala;
    private bool reseteando;
    private RomeritoHealth playerHealth;
    private Coroutine rutinaPrincipal;
    private Coroutine rutinaReposicion;

    private readonly List<GameObject> spawneados = new List<GameObject>();
    private int combatientesVivos;
    private int jaladoresVivos;
    private int presagiosPendientes;
    private int jaladoresPendientes;
    private bool oleadaTerminoDeSoltar;
    private float ultimaMuerteJalador = -999f;
    private OleadaPochtecah oleadaActual;
    private int rupturasEnCiclo;      // etapas rotas desde que empezó el ciclo actual
    private int fallosSeguidos;       // ventanas seguidas sin romper etapa
    private bool reposicionActiva => rutinaReposicion != null;

    // ── Unity ────────────────────────────────────────────────

    void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    void Start()
    {
        if (pochtecahLibre != null) pochtecahLibre.SetActive(false);

        if (YaResuelto())
        {
            if (logsDiagnostico)
                Debug.LogWarning($"[EncuentroPochtecah] '{eventoID}' ya está en collectedItems: " +
                                 "el evento se da por resuelto y NO arrancará. Borra el save o cambia eventoID para probar.", this);
            AplicarResuelto();
            return;
        }

        if (logsDiagnostico) DiagnosticarSetup();

        if (jaula != null)
        {
            jaula.ReiniciarCompleta();
            jaula.OnLiberada += AlLiberarse;
            jaula.OnEtapaRota += AlRomperEtapa;
        }
        if (puertaEntrada != null) puertaEntrada.Abrir();
        if (puertaSalida != null) puertaSalida.Bloquear();

        VincularJugador(ResolverJugador(), registrarReset: false);
    }

    void OnDestroy()
    {
        if (jaula != null)
        {
            jaula.OnLiberada -= AlLiberarse;
            jaula.OnEtapaRota -= AlRomperEtapa;
        }
        if (playerHealth != null)
        {
            playerHealth.OnPlayerDied -= AlMorirRomerito;
            if (jugadorEnSala) playerHealth.ClearResetRoutine();
        }
    }

    // ── Umbral ───────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (logsDiagnostico)
            Debug.Log($"[EncuentroPochtecah] Trigger tocado por '{other.name}' " +
                      $"(tag '{other.tag}', capa '{LayerMask.LayerToName(other.gameObject.layer)}', " +
                      $"isTrigger {other.isTrigger}). Fase: {FaseActual}", this);

        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (FaseActual == Fase.Resuelto) return;

        VincularJugador(other.GetComponentInParent<RomeritoHealth>(), registrarReset: true);
        jugadorEnSala = true;

        if (FaseActual != Fase.Inactivo || reseteando)
        {
            if (logsDiagnostico)
                Debug.Log($"[EncuentroPochtecah] Romerito entró pero no arranca: fase {FaseActual}, reseteando {reseteando}.", this);
            return;
        }

        if (logsDiagnostico) Debug.Log("[EncuentroPochtecah] ¡Arranca el evento!", this);

        if (puntoReaparicion != null && GameManager01.instance != null)
            GameManager01.instance.UpdateCheckPoint(puntoReaparicion.position);

        rutinaPrincipal = StartCoroutine(RutinaEncuentro());
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        // Falso exit al morir (collider apagado) o con la sala sellada: ignorar.
        if (reseteando) return;
        if (FaseActual != Fase.Inactivo && FaseActual != Fase.Resuelto) return;

        jugadorEnSala = false;
        if (playerHealth != null) playerHealth.ClearResetRoutine();
    }

    // ── Flujo principal ──────────────────────────────────────

    IEnumerator RutinaEncuentro()
    {
        // 1. Sello
        FaseActual = Fase.Intro;
        if (puertaEntrada != null) puertaEntrada.Cerrar();
        else Debug.LogWarning("[EncuentroPochtecah] No hay 'puertaEntrada' asignada: la sala no se sella.", this);
        if (puertaSalida != null) puertaSalida.Bloquear();
        OnSellada?.Invoke();
        Debug.Log("[EncuentroPochtecah] Sala sellada.");

        // Romerito se detiene en cuanto cruza el umbral.
        if (congelarRomeritoEnIntro) CongelarRomerito(true);

        // 2. Súplica del Pochtecah
        // FIX: StartConversation puede rechazar la conversación (vacía, o
        // ya hay otra activa) SIN llamar al callback: antes el evento se
        // quedaba atorado aquí con la sala sellada.
        Conversation intro = dialogoIntro != null ? dialogoIntro : ConstruirIntroDeRespaldo();
        if (intro != null && DialogueManager.Instance != null)
        {
            while (DialogueManager.IsActive) yield return null;

            bool termino = false;
            DialogueManager.Instance.StartConversation(intro, () =>
            {
                termino = true;
                // DialogueManager devuelve el control al terminar: se
                // vuelve a congelar en el MISMO frame (sin hueco).
                if (congelarRomeritoEnIntro) CongelarRomerito(true);
            });

            if (!DialogueManager.IsActive)
                Debug.LogWarning("[EncuentroPochtecah] El diálogo de intro no pudo iniciar (¿vacío?). Se continúa sin él.", this);
            else
                while (!termino) yield return null;
        }
        else if (intro != null)
        {
            Debug.LogWarning("[EncuentroPochtecah] Hay diálogo de intro pero no DialogueManager en la escena.", this);
        }

        // 3. Los mictecah se lo llevan: primer jalador desde el cráneo más
        //    cercano al anclaje. Romerito recupera el control cuando la
        //    jaula queda fuera de su alcance.
        int jaladoresEnCampo = 0;
        if (jaladorDeIntro && jaula != null)
        {
            GameObject prefab = oleadas != null && oleadas.Length > 0 && oleadas[0] != null
                ? oleadas[0].prefabJalador : null;
            Transform[] lista = (puntosJaladores != null && puntosJaladores.Length > 0) ? puntosJaladores : puntosAparicion;
            Transform punto = PuntoMasCercanoAlAnclaje(lista);

            if (prefab != null && punto != null)
            {
                FaseActual = Fase.Oleada;   // para que PresagiarYSoltar instancie
                yield return StartCoroutine(PresagiarYSoltar(prefab, punto, esJalador: true));
                jaladoresEnCampo = 1;

                float t = 0f;
                while (t < tiempoMaximoIntro && !Liberada() &&
                       !(jaula.Estado == CuauhcalliColgante.EstadoJaula.Subiendo &&
                         jaula.Altura >= jaula.alturaFueraDeAlcance))
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                if (t >= tiempoMaximoIntro)
                    Debug.LogWarning("[EncuentroPochtecah] El jalador de intro no subió la jaula a tiempo " +
                                     "(¿llega al anclaje?). Se libera a Romerito igual.", this);
            }
            else Debug.LogWarning("[EncuentroPochtecah] Sin prefabJalador en la oleada 0 o sin puntos: no hay jalador de intro.", this);
        }

        if (congelarRomeritoEnIntro) CongelarRomerito(false);

        // 4. Ciclos oleada → ventana
        // El éxito del ciclo se mide CONTANDO rupturas (OnEtapaRota).
        bool falloUltimaVentana = false;
        fallosSeguidos = 0;

        while (jaula != null && jaula.Estado != CuauhcalliColgante.EstadoJaula.Liberada)
        {
            rupturasEnCiclo = 0;

            OleadaPochtecah oleada = (falloUltimaVentana && oleadaExtra != null && TieneContenido(oleadaExtra))
                ? oleadaExtra
                : oleadas[Mathf.Clamp(jaula.EtapasRotas, 0, oleadas.Length - 1)];

            // La oleada extra hereda el jalador de la etapa actual si no tiene uno propio.
            if (oleada == oleadaExtra && oleada.prefabJalador == null)
                oleada.prefabJalador = oleadas[Mathf.Clamp(jaula.EtapasRotas, 0, oleadas.Length - 1)].prefabJalador;

            yield return StartCoroutine(RutinaOleada(oleada, jaladoresEnCampo));
            jaladoresEnCampo = 0;
            if (Liberada()) break;

            // ¿Ya rompió la etapa a media oleada (mini-ventana)?
            if (rupturasEnCiclo > 0)
            {
                falloUltimaVentana = false;
                fallosSeguidos = 0;
                yield return new WaitForSeconds(pausaTrasEtapa);
                continue;
            }

            yield return StartCoroutine(RutinaVentana());
            if (Liberada()) break;

            falloUltimaVentana = rupturasEnCiclo == 0;
            if (falloUltimaVentana)
            {
                fallosSeguidos++;
                Debug.Log($"[EncuentroPochtecah] Ventana sin romper etapa (golpes {jaula.GolpesEnEtapa}/{jaula.GolpesNecesarios}). " +
                          $"Oleada extra; la próxima ventana dura {DuracionVentanaActual():0.#} s.", this);
            }
            else
            {
                fallosSeguidos = 0;
                yield return new WaitForSeconds(pausaTrasEtapa);
            }
        }

        rutinaPrincipal = null;
        // El final lo dispara AlLiberarse (puede ocurrir en cualquier momento).
    }

    IEnumerator RutinaOleada(OleadaPochtecah oleada, int jaladoresYaEnCampo = 0)
    {
        FaseActual = Fase.Oleada;
        oleadaTerminoDeSoltar = false;
        OnOleadaIniciada?.Invoke();
        Debug.Log($"[EncuentroPochtecah] {oleada.nombre} (etapas rotas: {jaula.EtapasRotas})");

        oleadaActual = oleada;
        if (oleada.retrasoInicial > 0f) yield return new WaitForSeconds(oleada.retrasoInicial);

        // Jaladores primero: la amenaza principal arranca de inmediato.
        for (int i = jaladoresYaEnCampo; i < oleada.jaladoresIniciales; i++)
        {
            SoltarJalador(oleada.prefabJalador);
            yield return new WaitForSeconds(0.35f);
        }

        if (oleada.reponerJaladores)
            rutinaReposicion = StartCoroutine(RutinaReposicion(oleada));

        foreach (GrupoEnemigos g in oleada.combatientes)
        {
            if (g == null || g.prefab == null) continue;
            for (int i = 0; i < g.cantidad; i++)
            {
                SoltarEnemigo(g.prefab, esJalador: false);
                yield return new WaitForSeconds(oleada.intervaloCombatientes);
                if (Liberada()) yield break;
            }
        }
        oleadaTerminoDeSoltar = true;

        // Esperar a que no quede nadie (ni "en camino").
        while (!Liberada() &&
               (combatientesVivos > 0 || jaladoresVivos > 0 || presagiosPendientes > 0))
        {
            yield return null;
        }

        DetenerReposicion();
    }

    IEnumerator RutinaReposicion(OleadaPochtecah oleada)
    {
        while (true)
        {
            bool quedanCombatientes = !oleadaTerminoDeSoltar || combatientesVivos > 0 ||
                                      (presagiosPendientes - jaladoresPendientes) > 0;
            if (!quedanCombatientes) break;

            int jaladoresTotales = jaladoresVivos + jaladoresPendientes;
            if (jaladoresTotales < Mathf.Max(1, oleada.jaladoresSimultaneos) &&
                Time.time - ultimaMuerteJalador >= oleada.cooldownReposicion)
            {
                SoltarJalador(oleada.prefabJalador);
            }
            yield return new WaitForSeconds(0.25f);
        }
        rutinaReposicion = null;
    }

    float DuracionVentanaActual() =>
        duracionVentana + Mathf.Min(extensionMaxima, fallosSeguidos * extensionPorFallo);

    IEnumerator RutinaVentana()
    {
        // Si ningún jalador llegó a desatarla, la estaca cede ahora.
        jaula.SoltarAmarre();

        // Esperar a que la jaula termine de caer.
        while (!Liberada() && jaula.Estado != CuauhcalliColgante.EstadoJaula.EnSuelo)
            yield return null;
        if (Liberada()) yield break;

        FaseActual = Fase.Ventana;
        OnVentanaAbierta?.Invoke();
        Debug.Log("[EncuentroPochtecah] Ventana abierta.");

        float t = 0f;
        float duracion = DuracionVentanaActual();
        while (t < duracion && !Liberada() && rupturasEnCiclo == 0)
        {
            t += Time.deltaTime;
            yield return null;
        }

        OnVentanaCerrada?.Invoke();
        Debug.Log("[EncuentroPochtecah] Ventana cerrada.");
    }

    // ── Spawn ────────────────────────────────────────────────

    void SoltarJalador(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[EncuentroPochtecah] Oleada sin prefabJalador.");
            return;
        }
        SoltarEnemigo(prefab, esJalador: true);
    }

    void SoltarEnemigo(GameObject prefab, bool esJalador)
    {
        Transform[] lista = (esJalador && puntosJaladores != null && puntosJaladores.Length > 0)
            ? puntosJaladores : puntosAparicion;
        if (lista == null || lista.Length == 0)
        {
            Debug.LogError("[EncuentroPochtecah] No hay puntos de aparición.");
            return;
        }
        Transform punto = (esJalador && jaladoresDesdeAnclaje) ? PuntoMasCercanoAlAnclaje(lista) : ElegirPunto(lista);
        StartCoroutine(PresagiarYSoltar(prefab, punto, esJalador));
    }

    void ReportarMuerteJalador()
    {
        if (!logsDiagnostico || oleadaActual == null) return;
        string n = "[EncuentroPochtecah] Jalador eliminado → ";
        if (!oleadaActual.reponerJaladores)
            Debug.Log(n + $"sin reemplazo: '{oleadaActual.nombre}' tiene reponerJaladores desactivado.", this);
        else if (!reposicionActiva)
            Debug.Log(n + "sin reemplazo: ya no quedan combatientes en la oleada (la reposición terminó).", this);
        else
            Debug.Log(n + $"reemplazo en {oleadaActual.cooldownReposicion:0.#} s + presagio.", this);
    }

    IEnumerator PresagiarYSoltar(GameObject prefab, Transform punto, bool esJalador)
    {
        presagiosPendientes++;
        if (esJalador) jaladoresPendientes++;

        TzompantliPresagio presagio = punto.GetComponent<TzompantliPresagio>();
        if (presagio != null) yield return presagio.Presagiar();

        presagiosPendientes = Mathf.Max(0, presagiosPendientes - 1);
        if (esJalador) jaladoresPendientes = Mathf.Max(0, jaladoresPendientes - 1);

        if (Liberada() || FaseActual == Fase.Inactivo) yield break;

        GameObject e = Instantiate(prefab, punto.position, punto.rotation);
        spawneados.Add(e);

        if (esJalador)
        {
            jaladoresVivos++;
            MictecahJalador j = e.GetComponent<MictecahJalador>();
            if (j != null) j.jaula = jaula;
            else Debug.LogError($"[EncuentroPochtecah] '{prefab.name}' NO tiene MictecahJalador: no irá a la cuerda.", this);
            if (logsDiagnostico)
                Debug.Log($"[EncuentroPochtecah] Jalador '{e.name}' soltado en {punto.name} {punto.position}.", e);
        }
        else combatientesVivos++;

        EnemyDummy d = e.GetComponent<EnemyDummy>();
        if (d != null)
        {
            d.esDeOleada = true;
            d.OnDeath += () =>
            {
                if (esJalador)
                {
                    jaladoresVivos = Mathf.Max(0, jaladoresVivos - 1);
                    ultimaMuerteJalador = Time.time;
                    ReportarMuerteJalador();
                }
                else combatientesVivos = Mathf.Max(0, combatientesVivos - 1);
            };
        }
        else
        {
            Debug.LogWarning($"[EncuentroPochtecah] '{prefab.name}' no tiene EnemyDummy: nunca contará como muerto.");
        }
    }

    Transform ElegirPunto(Transform[] lista)
    {
        if (evitarPuntoOcupado)
        {
            List<Transform> libres = new List<Transform>();
            foreach (Transform p in lista)
            {
                if (p == null) continue;
                TzompantliPresagio tp = p.GetComponent<TzompantliPresagio>();
                if (tp == null || !tp.Ocupado) libres.Add(p);
            }
            if (libres.Count > 0) return libres[Random.Range(0, libres.Count)];
        }
        return lista[Random.Range(0, lista.Length)];
    }

    // ── Final ────────────────────────────────────────────────

    void AlRomperEtapa(int etapasRotas)
    {
        if (FaseActual != Fase.Oleada && FaseActual != Fase.Ventana) return;
        rupturasEnCiclo++;

        // La reposición normal no espera su cooldown tras una etapa rota.
        ultimaMuerteJalador = -999f;

        if (!jaladorDeRespuesta) return;
        OleadaPochtecah o = oleadas[Mathf.Clamp(etapasRotas, 0, oleadas.Length - 1)];
        if (o == null || o.prefabJalador == null) return;

        Transform[] lista = (puntosJaladores != null && puntosJaladores.Length > 0) ? puntosJaladores : puntosAparicion;
        Transform punto = PuntoMasCercanoAlAnclaje(lista);
        if (punto == null) return;

        if (logsDiagnostico) Debug.Log("[EncuentroPochtecah] Etapa rota → jalador de respuesta.", this);
        StartCoroutine(PresagiarYSoltar(o.prefabJalador, punto, esJalador: true));
    }

    Transform PuntoMasCercanoAlAnclaje(Transform[] lista)
    {
        if (lista == null || lista.Length == 0) return null;
        Vector3 anc = (jaula != null && jaula.anclaje != null) ? jaula.anclaje.position : transform.position;
        Transform mejor = null;
        float mejorD = float.MaxValue;
        foreach (Transform p in lista)
        {
            if (p == null) continue;
            TzompantliPresagio tp = p.GetComponent<TzompantliPresagio>();
            float d = Mathf.Abs(p.position.x - anc.x) + ((tp != null && tp.Ocupado) ? 1000f : 0f);
            if (d < mejorD) { mejorD = d; mejor = p; }
        }
        return mejor;
    }

    void AlLiberarse()
    {
        if (FaseActual == Fase.Final || FaseActual == Fase.Resuelto) return;
        StartCoroutine(RutinaFinal());
    }

    IEnumerator RutinaFinal()
    {
        FaseActual = Fase.Final;
        LiberarRomeritoSiCongelado();
        Debug.Log("[EncuentroPochtecah] ¡Pochtecah liberado!");

        if (rutinaPrincipal != null) { StopCoroutine(rutinaPrincipal); rutinaPrincipal = null; }
        DetenerReposicion();
        CancelarPresagios();

        // El tzompantli se apaga: los cráneos que quedan se deshacen.
        DeshacerEnemigos(conEfecto: true);

        // Persistencia
        MarcarResuelto();

        if (puertaEntrada != null) puertaEntrada.Abrir();
        if (puertaSalida != null) puertaSalida.Abrir();

        if (jugadorEnSala && playerHealth != null) playerHealth.ClearResetRoutine();
        FaseActual = Fase.Resuelto;

        if (pochtecahLibre != null) pochtecahLibre.SetActive(true);
        OnPochtecahLiberado?.Invoke();

        if (!entregarCodiceAutomaticamente || pochtecahLibre == null) yield break;

        yield return new WaitForSeconds(retrasoEntrega);
        PochtecahNPC npc = pochtecahLibre.GetComponentInChildren<PochtecahNPC>();
        if (npc != null) npc.ForzarInteraccion();
    }

    void DeshacerEnemigos(bool conEfecto)
    {
        foreach (GameObject e in spawneados)
        {
            if (e == null) continue;
            if (conEfecto)
            {
                EnemyDummy d = e.GetComponent<EnemyDummy>();
                if (d != null && d.deathEffect != null)
                    Instantiate(d.deathEffect, e.transform.position, Quaternion.identity);
            }
            Destroy(e);
        }
        spawneados.Clear();
        combatientesVivos = jaladoresVivos = presagiosPendientes = jaladoresPendientes = 0;
    }

    // ── Muerte de Romerito / reset ───────────────────────────

    void AlMorirRomerito()
    {
        if (FaseActual == Fase.Inactivo || FaseActual == Fase.Resuelto) return;
        Debug.Log("[EncuentroPochtecah] Romerito cayó: se reiniciará el evento.");
    }

    IEnumerator RutinaReset()
    {
        if (reseteando)
        {
            yield return new WaitUntil(() => !reseteando);
            yield break;
        }

        if (FaseActual == Fase.Inactivo || FaseActual == Fase.Resuelto)
        {
            yield return new WaitForSeconds(pausaTrasReset);
            yield break;
        }

        reseteando = true;
        LiberarRomeritoSiCongelado();

        if (rutinaPrincipal != null) { StopCoroutine(rutinaPrincipal); rutinaPrincipal = null; }
        DetenerReposicion();
        StopAllCoroutines();   // presagios en vuelo (esta rutina corre en RomeritoHealth)
        CancelarPresagios();

        DeshacerEnemigos(conEfecto: false);

        // Por si algún enemigo de la sala no salió de este evento (p. ej. cráneos bateados).
        if (limitesSala != null)
        {
            foreach (GameObject e in GameObject.FindGameObjectsWithTag(tagEnemigo))
            {
                if (!limitesSala.OverlapPoint(e.transform.position)) continue;
                EnemyDummy d = e.GetComponent<EnemyDummy>();
                if (d != null && d.esDeOleada) Destroy(e);
            }
        }

        if (jaula != null) jaula.ReiniciarCompleta();
        if (puertaEntrada != null) puertaEntrada.Abrir();
        if (puertaSalida != null) puertaSalida.Bloquear();

        if (puntoReaparicion != null && GameManager01.instance != null)
            GameManager01.instance.UpdateCheckPoint(puntoReaparicion.position);

        FaseActual = Fase.Inactivo;

        // Si reaparece FUERA (Cihuacalli), el trigger no verá el exit:
        // se limpia el registro. Si reaparece en el umbral, el trigger
        // lo vuelve a registrar al entrar.
        jugadorEnSala = false;
        if (playerHealth != null) playerHealth.ClearResetRoutine();

        yield return new WaitForSeconds(pausaTrasReset);
        reseteando = false;
        Debug.Log("[EncuentroPochtecah] Reset completo.");
    }

    void DetenerReposicion()
    {
        if (rutinaReposicion != null) { StopCoroutine(rutinaReposicion); rutinaReposicion = null; }
    }

    void CancelarPresagios()
    {
        CancelarEn(puntosAparicion);
        CancelarEn(puntosJaladores);
    }

    static void CancelarEn(Transform[] lista)
    {
        if (lista == null) return;
        foreach (Transform p in lista)
        {
            if (p == null) continue;
            TzompantliPresagio tp = p.GetComponent<TzompantliPresagio>();
            if (tp != null) tp.Cancelar();
        }
    }

    // ── Vinculación con Romerito (patrón de MacahuitlRoomManager) ──

    RomeritoHealth ResolverJugador()
    {
        if (PersistentPlayer.instance != null)
        {
            RomeritoHealth h = PersistentPlayer.instance.GetComponentInChildren<RomeritoHealth>();
            if (h != null) return h;
        }
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        return p != null ? p.GetComponentInParent<RomeritoHealth>() : null;
    }

    void VincularJugador(RomeritoHealth h, bool registrarReset)
    {
        if (h == null) return;
        if (h != playerHealth)
        {
            if (playerHealth != null) playerHealth.OnPlayerDied -= AlMorirRomerito;
            playerHealth = h;
            playerHealth.OnPlayerDied += AlMorirRomerito;
        }
        if (registrarReset) playerHealth.SetResetRoutine(RutinaReset);
    }

    // ── Persistencia ─────────────────────────────────────────

    bool YaResuelto()
    {
        return !string.IsNullOrEmpty(eventoID) &&
               GameManager01.instance != null &&
               GameManager01.instance.currentData != null &&
               GameManager01.instance.currentData.collectedItems.Contains(eventoID);
    }

    void MarcarResuelto()
    {
        if (string.IsNullOrEmpty(eventoID) || GameManager01.instance == null ||
            GameManager01.instance.currentData == null) return;

        var items = GameManager01.instance.currentData.collectedItems;
        if (!items.Contains(eventoID)) items.Add(eventoID);
        GameManager01.instance.SaveGame();
    }

    void AplicarResuelto()
    {
        FaseActual = Fase.Resuelto;
        if (jaula != null) jaula.Liberar(instantaneo: true);
        if (puertaEntrada != null) puertaEntrada.Abrir();
        if (puertaSalida != null) puertaSalida.Abrir();
        if (pochtecahLibre != null) pochtecahLibre.SetActive(true);
        Debug.Log($"[EncuentroPochtecah] '{eventoID}' ya resuelto.");
    }

    // ── Intro: congelar a Romerito ───────────────────────────
    //
    //  Mismo método que DialogueManager: apagar RomeritoMovement y
    //  RomeritoCombat y anular la velocidad. La gravedad sigue
    //  actuando (si entra saltando, aterriza).

    private RomeritoMovement movRomerito;
    private RomeritoCombat combRomerito;
    private bool romeritoCongelado;

    void CongelarRomerito(bool congelar)
    {
        if (movRomerito == null || combRomerito == null)
        {
            GameObject p = playerHealth != null ? playerHealth.gameObject : GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                movRomerito = p.GetComponentInChildren<RomeritoMovement>();
                combRomerito = p.GetComponentInChildren<RomeritoCombat>();
                if (congelar)
                {
                    Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
                }
            }
        }
        else if (congelar)
        {
            Rigidbody2D rb = movRomerito.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
        }

        if (movRomerito != null) movRomerito.enabled = !congelar;
        if (combRomerito != null) combRomerito.enabled = !congelar;
        romeritoCongelado = congelar;
    }

    void LiberarRomeritoSiCongelado()
    {
        if (romeritoCongelado) CongelarRomerito(false);
    }

    Conversation ConstruirIntroDeRespaldo()
    {
        if (lineasIntro == null || lineasIntro.Length == 0) return null;

        var lineas = new List<DialogueLine>();
        foreach (string texto in lineasIntro)
        {
            if (string.IsNullOrWhiteSpace(texto)) continue;
            lineas.Add(new DialogueLine
            {
                speaker = nombrePochtecah,
                text = texto,
                speakerColor = colorNombrePochtecah
            });
        }
        if (lineas.Count == 0) return null;

        Conversation c = ScriptableObject.CreateInstance<Conversation>();
        c.name = "IntroPochtecah_Runtime";
        c.nodes = new[] { new DialogueNode { nodeName = "Intro", lines = lineas.ToArray(), nextNode = -1 } };
        return c;
    }

    void OnDisable() => LiberarRomeritoSiCongelado();

    // ── Diagnóstico ──────────────────────────────────────────

    void DiagnosticarSetup()
    {
        string n = "[EncuentroPochtecah] ";
        Collider2D c = GetComponent<Collider2D>();
        if (c == null) Debug.LogError(n + "Falta un Collider2D en este objeto.", this);
        else
        {
            if (!c.isTrigger) Debug.LogError(n + "El Collider2D NO está en Is Trigger: nunca detectará la entrada.", this);
            if (!c.enabled) Debug.LogError(n + "El Collider2D está desactivado.", this);
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) Debug.LogWarning(n + "No hay ningún objeto con tag 'Player' en la escena (aún).", this);
        else if (Physics2D.GetIgnoreLayerCollision(gameObject.layer, p.layer))
            Debug.LogError(n + $"La capa '{LayerMask.LayerToName(gameObject.layer)}' de este objeto NO colisiona con " +
                           $"la capa '{LayerMask.LayerToName(p.layer)}' de Romerito (Layer Collision Matrix). " +
                           "Ponlo en Default.", this);

        if (jaula == null) Debug.LogError(n + "Falta asignar 'jaula'.", this);
        else
        {
            if (jaula.puntoSuelo == null) Debug.LogError(n + "La jaula no tiene 'puntoSuelo'.", jaula);
            if (jaula.anclaje == null) Debug.LogError(n + "La jaula no tiene 'anclaje': los jaladores no sabrán a dónde ir.", jaula);
        }

        if (puntosAparicion == null || puntosAparicion.Length == 0)
            Debug.LogError(n + "No hay 'puntosAparicion'.", this);

        if (oleadas == null || oleadas.Length == 0)
            Debug.LogError(n + "No hay oleadas configuradas.", this);
        else
            for (int i = 0; i < oleadas.Length; i++)
            {
                var o = oleadas[i];
                if (o == null) { Debug.LogError(n + $"Oleada {i} vacía.", this); continue; }
                if (o.jaladoresIniciales > 0 && o.prefabJalador == null)
                    Debug.LogError(n + $"Oleada {i}: falta prefabJalador.", this);
                if (o.prefabJalador != null && o.prefabJalador.GetComponent<MictecahJalador>() == null)
                    Debug.LogError(n + $"Oleada {i}: el prefabJalador '{o.prefabJalador.name}' no tiene MictecahJalador.", this);
                if (o.jaladoresIniciales <= 0 && !(o.reponerJaladores && o.jaladoresSimultaneos > 0))
                    Debug.LogError(n + $"Oleada {i}: jaladoresIniciales = 0 y sin reposición → NINGÚN jalador " +
                                   "(los elementos nuevos de una lista en el Inspector nacen en 0).", this);
                if (o.prefabJalador != null && o.prefabJalador.GetComponent<Mictecah1_Patrullero>() != null)
                    Debug.LogError(n + $"Oleada {i}: el prefabJalador aún tiene Mictecah1_Patrullero; quítalo.", this);
                if (o.combatientes != null)
                    foreach (var g in o.combatientes)
                        if (g != null && g.prefab == null)
                            Debug.LogError(n + $"Oleada {i}: un grupo de combatientes no tiene prefab.", this);
            }

        if (puertaEntrada == null)
            Debug.LogError(n + "Falta asignar 'puertaEntrada': la sala no se sellará.", this);
        else
        {
            Collider2D pc = puertaEntrada.GetComponent<Collider2D>();
            if (pc == null)
                Debug.LogError(n + $"La puerta '{puertaEntrada.name}' no tiene Collider2D en el MISMO objeto que RoomDoor " +
                               "(RoomDoor no busca en hijos): no bloqueará el paso.", puertaEntrada);
            else if (pc.isTrigger)
                Debug.LogError(n + $"El Collider2D de '{puertaEntrada.name}' está en Is Trigger: Romerito la atraviesa.", puertaEntrada);
            if (puertaEntrada.GetComponent<SpriteRenderer>() == null)
                Debug.LogWarning(n + $"La puerta '{puertaEntrada.name}' no tiene SpriteRenderer en el mismo objeto: no se verá.", puertaEntrada);
        }

        if (pochtecahLibre != null && pochtecahLibre.GetComponentInChildren<PochtecahNPC>(true) == null)
            Debug.LogWarning(n + "'pochtecahLibre' no tiene PochtecahNPC: no entregará el Códice.", this);
    }

    /// <summary>Arranca el evento sin pasar por el trigger (clic derecho en el componente, en Play).</summary>
    [ContextMenu("Debug: Iniciar evento")]
    void IniciarDesdeMenu()
    {
        if (!Application.isPlaying || FaseActual != Fase.Inactivo) return;
        VincularJugador(ResolverJugador(), registrarReset: true);
        jugadorEnSala = true;
        rutinaPrincipal = StartCoroutine(RutinaEncuentro());
    }

    // ── Utilidades ───────────────────────────────────────────

    bool Liberada() => jaula == null || jaula.Estado == CuauhcalliColgante.EstadoJaula.Liberada;

    static bool TieneContenido(OleadaPochtecah o) =>
        o.jaladoresIniciales > 0 || (o.combatientes != null && o.combatientes.Count > 0);

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
        if (jaula != null) Gizmos.DrawLine(transform.position, jaula.transform.position);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        if (puntosAparicion != null)
            foreach (Transform p in puntosAparicion)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.25f);
        Gizmos.color = new Color(0.85f, 0.7f, 0.4f, 0.9f);
        if (puntosJaladores != null)
            foreach (Transform p in puntosJaladores)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
    }
}
