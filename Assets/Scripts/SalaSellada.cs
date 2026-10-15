using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  SalaSellada — sala custodiada con brasero
// ============================================================
//
//  FLUJO
//   1. Romerito cruza el umbral (este trigger, un poco DENTRO de la
//      sala) → la puerta se SELLA tras él.
//   2. Mientras quede un custodio vivo, el Brasero está BLOQUEADO:
//      Tlacua no puede trabajar con ellos cerca.
//   3. Custodios eliminados → el Brasero se desbloquea (OnZonaLimpia).
//   4. Romerito enciende el Brasero → la puerta se vuelve
//      DESTRUCTIBLE (OnLiberada). Se rompe con el Macahuitl.
//
//  MUERTE DENTRO DE LA SALA (sin haber encendido el brasero)
//   La puerta se reabre. El yohual-ehécatl reagrupa a los custodios
//   (EnemigoRespawnable) y el Brasero vuelve a bloquearse solo.
//
//  PERSISTENCIA
//   El ancla es el Brasero (PlayerData.braserosEncendidos). Si al
//   cargar ya está encendido, la sala está resuelta: la puerta
//   aparece rota y no se vuelve a sellar. Los custodios siguen siendo
//   enemigos de mundo normales (se reagrupan en cada Cihuacalli).
//
//   Sin Brasero asignado funciona como arena repetible: al limpiar la
//   zona la puerta se vuelve destructible directamente.
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//   1. GameObject "SalaSellada" con BoxCollider2D (Is Trigger ON),
//      colocado 1–2 unidades DENTRO de la sala pasando la puerta.
//      Así la puerta nunca se cierra encima de Romerito.
//   2. puerta   → RoomDoor con doorType = Sello, en la capa
//                 destructibleLayer de RomeritoCombat.
//   3. custodios → la Tlahuelpuchi y el LanzaCráneos (EnemyDummy).
//                 (O deja la lista vacía y asigna limitesSala.)
//   4. brasero  → el Brasero de la sala.
//  ─────────────────────────────────────────────────────────────

[RequireComponent(typeof(Collider2D))]
public class SalaSellada : MonoBehaviour
{
    [Header("Puerta")]
    [Tooltip("RoomDoor con doorType = Sello")]
    public RoomDoor puerta;
    [Tooltip("Segundos entre cruzar el umbral y el cierre del sello")]
    public float retrasoSello = 0.1f;

    [Header("Custodios")]
    public List<EnemyDummy> custodios = new List<EnemyDummy>();
    [Tooltip("Si 'custodios' está vacía, se recogen los EnemyDummy dentro de este collider al iniciar")]
    public Collider2D limitesSala;
    [Tooltip("Cada cuántos segundos se revisa si siguen vivos")]
    public float intervaloRevision = 0.2f;

    [Header("Brasero (opcional)")]
    public Brasero brasero;

    [Header("Eventos (opcionales)")]
    [Tooltip("La puerta se acaba de sellar: golpe seco, temblor de cámara, polvo")]
    public UnityEvent OnSellada;
    [Tooltip("Último custodio eliminado: Tlacua asoma, brasas en el brasero")]
    public UnityEvent OnZonaLimpia;
    [Tooltip("La puerta ya es destructible")]
    public UnityEvent OnLiberada;

    // ── Estado ───────────────────────────────────────────────
    private bool sellada = false;
    private bool resuelta = false;
    private bool zonaLimpia = false;
    private RomeritoHealth playerHealth;
    private Coroutine selloRutina;
    private Coroutine vigilanciaRutina;

    public bool EstaSellada => sellada;
    public bool EstaResuelta => resuelta;

    // ── Unity ────────────────────────────────────────────────

    void Reset()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c != null) c.isTrigger = true;
    }

    void Start()
    {
        RecogerCustodios();

        if (puerta != null && puerta.doorType != RoomDoor.DoorType.Sello)
            Debug.LogWarning($"[SalaSellada] '{puerta.name}' no es doorType = Sello.");

        // ¿Ya resuelta en una sesión anterior?
        if (brasero != null && GameManager01.instance != null &&
            GameManager01.instance.BraseroEncendido(brasero.braseroID))
        {
            resuelta = true;
            if (puerta != null) puerta.RestaurarRota();
            Debug.Log($"[SalaSellada] '{name}' ya resuelta: puerta rota.");
            return;
        }

        if (brasero != null)
        {
            brasero.SetBloqueado(true);
            brasero.OnEncendido.AddListener(AlEncenderBrasero);
        }

        VincularJugador(ResolverJugador());
        vigilanciaRutina = StartCoroutine(Vigilar());
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnPlayerDied -= AlMorirRomerito;
        if (brasero != null) brasero.OnEncendido.RemoveListener(AlEncenderBrasero);
    }

    // ── Umbral ───────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;
        VincularJugador(other.GetComponentInParent<RomeritoHealth>());

        if (resuelta || sellada) return;

        sellada = true;
        selloRutina = StartCoroutine(RutinaSello());
    }

    IEnumerator RutinaSello()
    {
        if (retrasoSello > 0f) yield return new WaitForSeconds(retrasoSello);
        if (puerta != null) puerta.Cerrar();
        selloRutina = null;
        Debug.Log($"[SalaSellada] '{name}' sellada.");
        OnSellada?.Invoke();
    }

    // ── Vigilancia de custodios ──────────────────────────────

    IEnumerator Vigilar()
    {
        var espera = new WaitForSeconds(intervaloRevision);
        while (!resuelta)
        {
            bool limpia = CustodiosVivos() == 0;
            if (limpia != zonaLimpia)
            {
                zonaLimpia = limpia;
                if (brasero != null) brasero.SetBloqueado(!limpia);

                if (limpia)
                {
                    Debug.Log($"[SalaSellada] '{name}' zona limpia.");
                    OnZonaLimpia?.Invoke();
                    if (brasero == null) Liberar();
                }
            }
            yield return espera;
        }
        vigilanciaRutina = null;
    }

    public int CustodiosVivos()
    {
        int vivos = 0;
        foreach (EnemyDummy d in custodios)
        {
            if (d == null) continue;                          // destruido = muerto
            if (!d.gameObject.activeInHierarchy) continue;    // disperso
            EnemigoRespawnable r = d.GetComponent<EnemigoRespawnable>();
            if (r != null && r.EstaDisperso) continue;
            vivos++;
        }
        return vivos;
    }

    void RecogerCustodios()
    {
        custodios.RemoveAll(d => d == null);
        if (custodios.Count > 0) return;

        if (limitesSala != null)
        {
            foreach (EnemyDummy d in Object.FindObjectsByType<EnemyDummy>(FindObjectsSortMode.None))
                if (limitesSala.OverlapPoint(d.transform.position))
                    custodios.Add(d);
        }

        if (custodios.Count == 0)
            Debug.LogWarning($"[SalaSellada] '{name}' sin custodios: la zona cuenta como limpia.");
    }

    // ── Brasero / liberación ─────────────────────────────────

    void AlEncenderBrasero()
    {
        if (resuelta) return;
        resuelta = true;
        Liberar();
    }

    void Liberar()
    {
        // Solo tiene sentido romper un sello cerrado. Si por alguna
        // razón la puerta sigue abierta, se queda abierta.
        if (sellada && puerta != null) puerta.HacerDestructible();
        Debug.Log($"[SalaSellada] '{name}' liberada: la puerta se puede romper.");
        OnLiberada?.Invoke();
    }

    // ── Muerte de Romerito ───────────────────────────────────

    void AlMorirRomerito()
    {
        if (!sellada || resuelta) return;

        if (selloRutina != null) { StopCoroutine(selloRutina); selloRutina = null; }
        sellada = false;
        if (puerta != null) puerta.Abrir();
        Debug.Log($"[SalaSellada] Romerito cayó en '{name}': sello levantado.");
        // Los custodios los reagrupa RomeritoHealth.Respawn() →
        // MictlanEnemyRegistry; Vigilar() re-bloquea el brasero solo.
    }

    // ── Vinculación con Romerito (mismo patrón que MacahuitlRoomManager)

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

    void VincularJugador(RomeritoHealth h)
    {
        if (h == null || h == playerHealth) return;
        if (playerHealth != null) playerHealth.OnPlayerDied -= AlMorirRomerito;
        playerHealth = h;
        playerHealth.OnPlayerDied += AlMorirRomerito;
    }

    // ── Gizmos ───────────────────────────────────────────────

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.9f, 0.3f, 1f, 0.8f);
        if (puerta != null) Gizmos.DrawLine(transform.position, puerta.transform.position);
        Gizmos.color = new Color(1f, 0.5f, 0.05f, 0.8f);
        if (brasero != null) Gizmos.DrawLine(transform.position, brasero.transform.position);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        foreach (EnemyDummy d in custodios)
            if (d != null) Gizmos.DrawLine(transform.position, d.transform.position);
    }
}
