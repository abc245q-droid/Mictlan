using UnityEngine;

// ============================================================
//  FavorManager — Sistema de Favores de los Dioses (Rebanada 1)
// ============================================================
//
//  Filosofía (GDD): los Favores no son "obedecer a un dios" sino
//  ALINEARSE con un principio cósmico. Mecánicamente: los Plasmids
//  de BioShock — el Macahuitl es el arma, el Favor es la capa
//  elemental que la transforma y añade habilidades activas.
//
//  CONTROLES:
//    LB (JoystickButton4 / tecla Q) → CICLA el favor activo:
//        Macahuitl normal → Favor 1 → Favor 2 → ... → normal
//        (con un solo favor: alterna normal ↔ favor)
//    RB (JoystickButton5 / tecla F) → EJECUTA habilidad del favor:
//        RB solo        → Habilidad 1
//        RB + ↓ (abajo) → Habilidad 2
//    Las teclas 1–4 de RomeritoCombat siguen funcionando como
//    selección directa (debug/teclado); este script se sincroniza.
//
//  HABILIDADES DE HUEHUETEOTL:
//    1. Bola de Fuego  — proyectil en la dirección de la mirada.
//    2. Barrera de Copal — humo ritual: invulnerabilidad 5s,
//       SOLO efectiva quieto y en el suelo (componente BarreraCopal).
//
//  COSTOS: cada habilidad gasta Tonalli (TonalliSystem.GastarTonalli).
//  Referencia de escala: maxTonalliBase = 300, curar corazón = 100.
//
//  HUD: con un favor seleccionado, la barra de Tonalli se tiñe del
//  color del dios (TonalliSystem.SetColorFavor). El fuego es el
//  ÚNICO elemento cromático cálido del Mictlán — la barra ardiendo
//  en rojo-fuego ES lenguaje visual del juego, no decoración.
//
//  EXTENSIÓN (próximos favores):
//    1. Añadir el favor al ciclo en ConstruirCiclo() (ya automático
//       si existe la bandera unlock* en RomeritoCombat).
//    2. Añadir su color en ColorDeFavor().
//    3. Añadir sus dos habilidades en EjecutarHabilidad1/2().
//
//  SETUP EN UNITY:
//    • Añadir este componente a Romerito (junto a RomeritoCombat).
//    • Asignar: prefabBolaDeFuego, barreraCopal (componente en
//      Romerito), puntoLanzamiento (opcional; usa attackPoint si
//      está vacío).
//
// ============================================================

[RequireComponent(typeof(RomeritoCombat))]
public class FavorManager : MonoBehaviour
{
    [Header("── Input ──")]
    [Tooltip("Botón para CICLAR el favor activo. LB en control de Xbox.")]
    public KeyCode botonCambiarFavor = KeyCode.JoystickButton4;
    [Tooltip("Alternativa de teclado para ciclar favor.")]
    public KeyCode teclaCambiarFavor = KeyCode.Q;

    [Tooltip("Botón para EJECUTAR la habilidad del favor. RB en control de Xbox.")]
    public KeyCode botonEjecutarFavor = KeyCode.JoystickButton5;
    [Tooltip("Alternativa de teclado para ejecutar la habilidad.")]
    public KeyCode teclaEjecutarFavor = KeyCode.F;

    [Tooltip("Umbral del eje Vertical para considerar 'flecha abajo' (Habilidad 2).")]
    [Range(0.1f, 0.9f)]
    public float umbralAbajo = 0.5f;

    [Header("── Huehueteotl: Habilidad 1 — Bola de Fuego ──")]
    [Tooltip("Prefab con el script BolaDeFuego.")]
    public GameObject prefabBolaDeFuego;
    [Tooltip("Origen del proyectil. Si está vacío usa el attackPoint de RomeritoCombat.")]
    public Transform puntoLanzamiento;
    [Tooltip("Costo en Tonalli de la Bola de Fuego. (Referencia: curar = 100)")]
    public float costoBolaDeFuego = 45f;

    [Header("── Huehueteotl: Habilidad 2 — Barrera de Copal ──")]
    [Tooltip("Componente BarreraCopal en Romerito.")]
    public BarreraCopal barreraCopal;
    [Tooltip("Costo en Tonalli de la Barrera de Copal.")]
    public float costoBarreraCopal = 90f;

    [Header("── HUD: color de la barra de Tonalli por favor ──")]
    [Tooltip("Rojo-fuego de Huehueteotl. El único calor cromático del Mictlán.")]
    public Color colorHuehueteotl = new Color(1f, 0.35f, 0.1f);
    public Color colorTlaloc = new Color(0.25f, 0.65f, 0.95f);
    public Color colorTepeyollotl = new Color(0.55f, 0.42f, 0.25f);

    // ── Referencias internas ─────────────────────────────────
    private RomeritoCombat combat;
    private RomeritoCombat.GodFavor ultimoFavorVisto = RomeritoCombat.GodFavor.Neutro;

    // ── Ciclo de vida ────────────────────────────────────────
    void Awake()
    {
        combat = GetComponent<RomeritoCombat>();
    }

    void Start()
    {
        SincronizarColorHUD(combat.currentFavor);
        ultimoFavorVisto = combat.currentFavor;
    }

    void Update()
    {
        // ── Gates: mismas reglas que el resto de la UI/combate ──
        if (DialogueManager.IsActive) return;
        if (PochtecahShopUI.IsOpen) return;
        if (MapScreenUI.IsOpen) return;
        if (!combat.tieneMacuahuitl) return;   // sin arma no hay favores

        // Sincroniza el color del HUD si el favor cambió por OTRA vía
        // (teclas 1–4 de RomeritoCombat, o el altar al otorgar el favor).
        if (combat.currentFavor != ultimoFavorVisto)
        {
            SincronizarColorHUD(combat.currentFavor);
            ultimoFavorVisto = combat.currentFavor;
        }

        // ── LB: ciclar favor ─────────────────────────────────
        if (Input.GetKeyDown(botonCambiarFavor) || Input.GetKeyDown(teclaCambiarFavor))
            CiclarFavor();

        // ── RB: ejecutar habilidad ───────────────────────────
        if (Input.GetKeyDown(botonEjecutarFavor) || Input.GetKeyDown(teclaEjecutarFavor))
        {
            bool presionaAbajo = Input.GetAxisRaw("Vertical") < -umbralAbajo;
            if (presionaAbajo) EjecutarHabilidad2();
            else EjecutarHabilidad1();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  CICLO DE FAVORES (LB)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Construye el ciclo actual: Neutro + favores desbloqueados en
    /// orden fijo. Se reconstruye en cada pulsación (barato) para que
    /// un favor recién desbloqueado entre al ciclo sin recargar nada.
    /// </summary>
    private RomeritoCombat.GodFavor[] ConstruirCiclo()
    {
        var lista = new System.Collections.Generic.List<RomeritoCombat.GodFavor>
        {
            RomeritoCombat.GodFavor.Neutro
        };
        if (combat.unlockHuehueteotl) lista.Add(RomeritoCombat.GodFavor.Huehueteotl);
        if (combat.unlockTlaloc) lista.Add(RomeritoCombat.GodFavor.Tlaloc);
        if (combat.unlockTepeyollotl) lista.Add(RomeritoCombat.GodFavor.Tepeyollotl);
        return lista.ToArray();
    }

    private void CiclarFavor()
    {
        RomeritoCombat.GodFavor[] ciclo = ConstruirCiclo();

        // Sin favores desbloqueados: no hay nada que ciclar.
        if (ciclo.Length <= 1)
        {
            Debug.Log("[Favor] Romerito aún no tiene ningún Favor de los Dioses.");
            return;
        }

        // Posición actual dentro del ciclo → siguiente (con vuelta a Neutro).
        int idx = System.Array.IndexOf(ciclo, combat.currentFavor);
        if (idx < 0) idx = 0; // favor actual fuera del ciclo (caso raro): reinicia
        RomeritoCombat.GodFavor siguiente = ciclo[(idx + 1) % ciclo.Length];

        combat.CambiarFavor(siguiente);
        SincronizarColorHUD(siguiente);
        ultimoFavorVisto = siguiente;
    }

    // ─────────────────────────────────────────────────────────
    //  HABILIDADES (RB)
    // ─────────────────────────────────────────────────────────

    private void EjecutarHabilidad1()
    {
        switch (combat.currentFavor)
        {
            case RomeritoCombat.GodFavor.Huehueteotl:
                LanzarBolaDeFuego();
                break;

            case RomeritoCombat.GodFavor.Neutro:
                // Sin favor seleccionado, RB no hace nada (a propósito):
                // el jugador aprende que RB pertenece al mundo divino.
                break;

            // Tlaloc / Tepeyollotl: habilidades pendientes de diseño.
            default:
                Debug.Log($"[Favor] Habilidad 1 de {combat.currentFavor} aún no implementada.");
                break;
        }
    }

    private void EjecutarHabilidad2()
    {
        switch (combat.currentFavor)
        {
            case RomeritoCombat.GodFavor.Huehueteotl:
                ActivarBarreraCopal();
                break;

            case RomeritoCombat.GodFavor.Neutro:
                break;

            default:
                Debug.Log($"[Favor] Habilidad 2 de {combat.currentFavor} aún no implementada.");
                break;
        }
    }

    // ── Huehueteotl 1: Bola de Fuego ─────────────────────────
    private void LanzarBolaDeFuego()
    {
        if (prefabBolaDeFuego == null)
        {
            Debug.LogWarning("[Favor] No hay prefabBolaDeFuego asignado en FavorManager.");
            return;
        }

        // Cobrar ANTES de ejecutar (patrón de TonalliSystem.GastarTonalli).
        if (TonalliSystem.Instance == null ||
            !TonalliSystem.Instance.GastarTonalli(costoBolaDeFuego))
        {
            Debug.Log("[Favor] Tonalli insuficiente para la Bola de Fuego.");
            return;
        }

        // Dirección de la mirada: signo de localScale.x (+1 derecha).
        int direccion = transform.localScale.x >= 0f ? 1 : -1;

        Transform origen = puntoLanzamiento != null ? puntoLanzamiento
                          : (combat.attackPoint != null ? combat.attackPoint : transform);

        GameObject go = Instantiate(prefabBolaDeFuego, origen.position, Quaternion.identity);
        BolaDeFuego bola = go.GetComponent<BolaDeFuego>();
        if (bola != null) bola.Lanzar(direccion);
        else Debug.LogWarning("[Favor] El prefab de Bola de Fuego no tiene el script BolaDeFuego.");
    }

    // ── Huehueteotl 2: Barrera de Copal ──────────────────────
    private void ActivarBarreraCopal()
    {
        if (barreraCopal == null)
        {
            Debug.LogWarning("[Favor] No hay componente BarreraCopal asignado en FavorManager.");
            return;
        }

        // No apilar barreras: si ya está activa, ni se cobra ni se reinicia.
        if (barreraCopal.EstaActiva)
        {
            Debug.Log("[Favor] La Barrera de Copal ya está activa.");
            return;
        }

        if (TonalliSystem.Instance == null ||
            !TonalliSystem.Instance.GastarTonalli(costoBarreraCopal))
        {
            Debug.Log("[Favor] Tonalli insuficiente para la Barrera de Copal.");
            return;
        }

        barreraCopal.Activar();
    }

    // ─────────────────────────────────────────────────────────
    //  HUD — color de la barra de Tonalli según el favor
    // ─────────────────────────────────────────────────────────

    private void SincronizarColorHUD(RomeritoCombat.GodFavor favor)
    {
        if (TonalliSystem.Instance == null) return;

        Color? c = ColorDeFavor(favor);
        if (c.HasValue) TonalliSystem.Instance.SetColorFavor(c.Value);
        else TonalliSystem.Instance.ResetColorFavor();
    }

    private Color? ColorDeFavor(RomeritoCombat.GodFavor favor)
    {
        switch (favor)
        {
            case RomeritoCombat.GodFavor.Huehueteotl: return colorHuehueteotl;
            case RomeritoCombat.GodFavor.Tlaloc: return colorTlaloc;
            case RomeritoCombat.GodFavor.Tepeyollotl: return colorTepeyollotl;
            default: return null; // Neutro → color ámbar base
        }
    }
}
