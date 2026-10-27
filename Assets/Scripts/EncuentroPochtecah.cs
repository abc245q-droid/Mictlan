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
//  cuerda del tzompantli (CuauhcalliPolea). No hay jefe: el rival es
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
//  La composición de cada oleada sale de 'oleadas[EtapasRotas]', así
//  que si los Mictecah reclaman la jaula (pierdes una etapa) la
//  siguiente oleada también "retrocede".
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
    public CuauhcalliPolea jaula;
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

    [Header("Diálogos (opcionales)")]
    [Tooltip("Al sellarse la sala, antes de la primera oleada (súplica del Pochtecah).")]
    public Conversation dialogoIntro;

    [Header("Oleadas (índice = etapas rotas)")]
    public OleadaPochtecah[] oleadas = new OleadaPochtecah[3];
    [Tooltip("Se usa cuando la ventana termina SIN romper etapa.")]
    public OleadaPochtecah oleadaExtra;

    [Header("Ventana")]
    public float duracionVentana = 5f;
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
            AplicarResuelto();
            return;
        }

        if (jaula != null)
        {
            jaula.ReiniciarCompleta();
            jaula.OnLiberada += AlLiberarse;
        }
        if (puertaEntrada != null) puertaEntrada.Abrir();
        if (puertaSalida != null) puertaSalida.Bloquear();

        VincularJugador(ResolverJugador(), registrarReset: false);
    }

    void OnDestroy()
    {
        if (jaula != null) jaula.OnLiberada -= AlLiberarse;
        if (playerHealth != null)
        {
            playerHealth.OnPlayerDied -= AlMorirRomerito;
            if (jugadorEnSala) playerHealth.ClearResetRoutine();
        }
    }

    // ── Umbral ───────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        if (FaseActual == Fase.Resuelto) return;

        VincularJugador(other.GetComponentInParent<RomeritoHealth>(), registrarReset: true);
        jugadorEnSala = true;

        if (FaseActual != Fase.Inactivo || reseteando) return;

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
        if (puertaSalida != null) puertaSalida.Bloquear();
        OnSellada?.Invoke();
        Debug.Log("[EncuentroPochtecah] Sala sellada.");

        // 2. Súplica del Pochtecah
        if (dialogoIntro != null && DialogueManager.Instance != null)
        {
            bool termino = false;
            DialogueManager.Instance.StartConversation(dialogoIntro, () => termino = true);
            while (!termino) yield return null;
        }

        // 3. Ciclos oleada → ventana
        bool falloUltimaVentana = false;

        while (jaula != null && jaula.Estado != CuauhcalliPolea.EstadoJaula.Liberada)
        {
            int etapasAlInicio = jaula.EtapasRotas;

            OleadaPochtecah oleada = (falloUltimaVentana && oleadaExtra != null && TieneContenido(oleadaExtra))
                ? oleadaExtra
                : oleadas[Mathf.Clamp(jaula.EtapasRotas, 0, oleadas.Length - 1)];

            yield return StartCoroutine(RutinaOleada(oleada));
            if (Liberada()) break;

            // ¿Ya rompió la etapa a media oleada (mini-ventana)?
            if (jaula.EtapasRotas > etapasAlInicio)
            {
                falloUltimaVentana = false;
                yield return new WaitForSeconds(pausaTrasEtapa);
                continue;
            }

            yield return StartCoroutine(RutinaVentana(etapasAlInicio));
            if (Liberada()) break;

            falloUltimaVentana = jaula.EtapasRotas <= etapasAlInicio;
            if (!falloUltimaVentana) yield return new WaitForSeconds(pausaTrasEtapa);
        }

        rutinaPrincipal = null;
        // El final lo dispara AlLiberarse (puede ocurrir en cualquier momento).
    }

    IEnumerator RutinaOleada(OleadaPochtecah oleada)
    {
        FaseActual = Fase.Oleada;
        oleadaTerminoDeSoltar = false;
        OnOleadaIniciada?.Invoke();
        Debug.Log($"[EncuentroPochtecah] {oleada.nombre} (etapas rotas: {jaula.EtapasRotas})");

        if (oleada.retrasoInicial > 0f) yield return new WaitForSeconds(oleada.retrasoInicial);

        // Jaladores primero: la amenaza principal arranca de inmediato.
        for (int i = 0; i < oleada.jaladoresIniciales; i++)
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
            if (jaladoresTotales < oleada.jaladoresSimultaneos &&
                Time.time - ultimaMuerteJalador >= oleada.cooldownReposicion)
            {
                SoltarJalador(oleada.prefabJalador);
            }
            yield return new WaitForSeconds(0.25f);
        }
        rutinaReposicion = null;
    }

    IEnumerator RutinaVentana(int etapasAlInicio)
    {
        // Si ningún jalador llegó a desatarla, la estaca cede ahora.
        jaula.SoltarAmarre();

        // Esperar a que la jaula termine de caer.
        while (!Liberada() && jaula.Estado != CuauhcalliPolea.EstadoJaula.EnSuelo)
            yield return null;
        if (Liberada()) yield break;

        FaseActual = Fase.Ventana;
        OnVentanaAbierta?.Invoke();
        Debug.Log("[EncuentroPochtecah] Ventana abierta.");

        float t = 0f;
        while (t < duracionVentana && !Liberada() && jaula.EtapasRotas <= etapasAlInicio)
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
        StartCoroutine(PresagiarYSoltar(prefab, ElegirPunto(lista), esJalador));
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
            if (j != null) j.polea = jaula;
            else Debug.LogWarning($"[EncuentroPochtecah] '{prefab.name}' no tiene MictecahJalador.");
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

    void AlLiberarse()
    {
        if (FaseActual == Fase.Final || FaseActual == Fase.Resuelto) return;
        StartCoroutine(RutinaFinal());
    }

    IEnumerator RutinaFinal()
    {
        FaseActual = Fase.Final;
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

    // ── Utilidades ───────────────────────────────────────────

    bool Liberada() => jaula == null || jaula.Estado == CuauhcalliPolea.EstadoJaula.Liberada;

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
