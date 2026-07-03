using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Gestiona el Tonalli — el "maná" de Romerito: energía vital y calor solar del alma axolotl.
///
/// DISEÑO: Barra continua (estilo maná clásico), NO segmentada por unidades.
///   Esto es deliberado: los Favores de los Dioses tendrán costos variables
///   (un golpe elemental puede costar 30, un ultimate puede costar 150+).
///   Una barra continua comunica esos costos como proporciones del total,
///   sin la rigidez de "encajar" en vasijas discretas.
///
/// JERARQUÍA EN CANVAS (recomendada):
///   HUD Canvas
///   └── PanelTonalli
///       ├── BarraFondo   (Image — contorno/fondo oscuro, siempre visible)
///       └── BarraFill    (Image — Type=Filled, Fill Method=Horizontal)
///
/// La barra puede tener divisiones visuales decorativas (líneas finas
/// superpuestas) para dar referencia de "tercios", pero NO afectan el
/// cálculo — son puramente cosméticas y opcionales.
///
/// FUENTES DE TONALLI (configurado en EnemyDummy):
///   - Golpe a enemigo  → tonalliPorGolpe  (por defecto 15)
///   - Matar enemigo    → tonalliPorMatar  (por defecto 50)
///
/// GASTO DE TONALLI:
///   - Curar un corazón     → costoTonalliCurar (en RomeritoHealth)
///   - Favores divinos      → costo variable por habilidad (futuro, en RomeritoCombat)
///
/// AMPLIACIÓN DE CAPACIDAD:
///   - Fragmento de Turquesa → AmpliarCapacidad(0.15f) → +15% de maxTonalli permanente
/// </summary>
public class TonalliSystem : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────
    //  SINGLETON — acceso global sin cadenas de referencia
    // ─────────────────────────────────────────────────────────────────────

    public static TonalliSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  CONFIGURACIÓN — INSPECTOR
    // ─────────────────────────────────────────────────────────────────────

    [Header("Capacidad de Tonalli")]
    [Tooltip("Capacidad base de la barra. Las ampliaciones (Fragmento de Turquesa) " +
             "se calculan como porcentaje de este valor.")]
    public float maxTonalliBase = 300f;

    [Tooltip("El GameObject PanelTonalli del HUD. Se oculta hasta recibir el Don de Tlacua.")]
    public GameObject panelTonalli;

    [Header("Referencias UI")]
    [Tooltip("Image con Type=Filled, Fill Method=Horizontal. Representa el Tonalli actual.")]
    public Image barraFill;

    [Tooltip("Opcional: Image de fondo/contorno de la barra (sin animación).")]
    public Image barraFondo;

    [Header("Colores")]
    [Tooltip("Color del relleno con Tonalli normal.")]
    public Color colorTonalli = new Color(1f, 0.78f, 0.1f);   // Ámbar dorado

    [Tooltip("Color de advertencia cuando el Tonalli está muy bajo (por debajo de tonalliBajoUmbral).")]
    public Color colorTonalliBajo = new Color(0.9f, 0.3f, 0.25f);

    [Tooltip("Proporción (0–1) por debajo de la cual se usa colorTonalliBajo.")]
    [Range(0f, 0.5f)]
    public float tonalliBajoUmbral = 0.15f;

    [Header("Feedback Visual")]
    [Tooltip("Segundos que dura el destello al ganar Tonalli.")]
    [Range(0.05f, 0.4f)]
    public float duracionPulso = 0.15f;

    [Tooltip("Velocidad de interpolación del fillAmount hacia el valor real " +
             "(suaviza el llenado/vaciado en vez de saltos instantáneos).")]
    public float velocidadSuavizado = 8f;

    // ─────────────────────────────────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────────────────────────────────

    private float _currentTonalli = 0f;

    // Ampliaciones permanentes (Fragmento de Turquesa), guardadas como
    // porcentaje acumulado sobre maxTonalliBase. Ej: 0.15f = +15%.
    private float _bonusCapacidadPct = 0f;

    private Coroutine _pulsoRoutine;

    // [FAVOR] Override de color por Favor activo (FavorManager).
    // Con Huehueteotl seleccionado la barra arde en rojo-fuego — el único
    // calor cromático del Mictlán. La advertencia de Tonalli bajo SIEMPRE
    // gana sobre el color del favor (información > estética).
    private bool _favorColorActivo = false;
    private Color _colorFavor;

    // ─────────────────────────────────────────────────────────────────────
    //  PROPIEDADES
    // ─────────────────────────────────────────────────────────────────────

    public float CurrentTonalli => _currentTonalli;
    public float MaxTonalli => maxTonalliBase * (1f + _bonusCapacidadPct);
    public float BonusCapacidadPct => _bonusCapacidadPct;

    // ─────────────────────────────────────────────────────────────────────
    //  INICIALIZACIÓN — llamar desde RomeritoHealth.Start()
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configura la capacidad (con ampliaciones del Fragmento de Turquesa ya
    /// aplicadas) y el Tonalli inicial.
    /// </summary>
    /// <param name="bonusCapacidadPct">Acumulado de ampliaciones, ej. 0.15f por cada Fragmento.</param>
    /// <param name="tonalliInicial">Tonalli con el que arranca (normalmente 0).</param>
    public void Inicializar(float bonusCapacidadPct, float tonalliInicial = 0f)
    {
        // Mostrar u ocultar según si tiene el Don de Tlacua
        bool tieneDon = GameManager01.instance != null &&
                    GameManager01.instance.currentData.tieneDonDeTlacua;
        if (panelTonalli != null) panelTonalli.SetActive(tieneDon);


        _bonusCapacidadPct = Mathf.Max(0f, bonusCapacidadPct);
        _currentTonalli = Mathf.Clamp(tonalliInicial, 0f, MaxTonalli);
        ActualizarUI(instantaneo: true);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  API PÚBLICA
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Añade Tonalli (golpear / matar enemigos).
    /// Llamado automáticamente desde EnemyDummy.TakeDamage() y EnemyDummy.Die().
    /// </summary>
    public void GanarTonalli(float cantidad)
    {
        if (cantidad <= 0f) return;
        float antes = _currentTonalli;
        _currentTonalli = Mathf.Clamp(_currentTonalli + cantidad, 0f, MaxTonalli);
        if (_currentTonalli > antes) // solo si realmente ganó algo (no estaba al máximo)
            EjecutarPulso();
    }

    /// <summary>
    /// Intenta gastar Tonalli. Devuelve true si había suficiente y el gasto se realizó.
    /// Llamar ANTES de ejecutar la acción (curar, favor divino, etc.).
    /// El costo es un VALOR ABSOLUTO (no porcentaje) — cada favor define el suyo.
    /// </summary>
    public bool GastarTonalli(float cantidad)
    {
        if (cantidad <= 0f || _currentTonalli < cantidad) return false;
        _currentTonalli = Mathf.Clamp(_currentTonalli - cantidad, 0f, MaxTonalli);
        return true;
    }

    /// <summary>Consulta sin gastar — para habilitar/deshabilitar botones de UI.</summary>
    public bool TieneSuficiente(float cantidad) => _currentTonalli >= cantidad;

    /// <summary>
    /// Tiñe la barra con el color del Favor activo (llamado por FavorManager).
    /// </summary>
    public void SetColorFavor(Color color)
    {
        _colorFavor = color;
        _favorColorActivo = true;
    }

    /// <summary>Regresa la barra al ámbar base (favor Neutro).</summary>
    public void ResetColorFavor()
    {
        _favorColorActivo = false;
    }

    // Color de reposo actual: advertencia de bajo > color de favor > ámbar base.
    private Color ColorBaseActual(float proporcion)
    {
        if (proporcion <= tonalliBajoUmbral) return colorTonalliBajo;
        return _favorColorActivo ? _colorFavor : colorTonalli;
    }

    /// <summary>
    /// Amplía la capacidad máxima permanentemente (recompensa del Fragmento de Turquesa).
    /// El Tonalli actual se preserva (en valor absoluto, no en proporción).
    /// </summary>
    /// <param name="porcentaje">Ej. 0.15f para +15% de maxTonalliBase.</param>
    public void AmpliarCapacidad(float porcentaje)
    {
        _bonusCapacidadPct += porcentaje;
        // Reclamp por si el Tonalli actual ya estaba en el tope anterior
        _currentTonalli = Mathf.Clamp(_currentTonalli, 0f, MaxTonalli);
        Debug.Log($"[TonalliSystem] ¡Capacidad ampliada! Bonus total: " +
                  $"{_bonusCapacidadPct:P0} | Max actual: {MaxTonalli}");
    }

    /// <summary>
    /// Vacía el Tonalli al morir / respawnear.
    /// En Mictlán el Tonalli se gana de nuevo en combate — como el alma en HK al despertar.
    /// </summary>
    public void ResetearAlMorir()
    {
        _currentTonalli = 0f;
        ActualizarUI(instantaneo: true);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  ACTUALIZACIÓN DE UI
    // ─────────────────────────────────────────────────────────────────────

    void Update()
    {
        ActualizarUI(instantaneo: false);
    }

    /// <param name="instantaneo">
    /// true → salta directo al valor real (usado en Inicializar/Reset).
    /// false → interpola suavemente hacia el valor real (uso normal en Update).
    /// </param>
    private void ActualizarUI(bool instantaneo)
    {
        if (barraFill == null) return;

        float targetFill = MaxTonalli > 0f ? _currentTonalli / MaxTonalli : 0f;

        if (instantaneo)
        {
            barraFill.fillAmount = targetFill;
        }
        else
        {
            barraFill.fillAmount = Mathf.Lerp(
                barraFill.fillAmount, targetFill, velocidadSuavizado * Time.deltaTime);
        }

        // Color de reposo (no afecta durante el pulso):
        // advertencia de bajo > color del favor activo > ámbar base.
        if (_pulsoRoutine == null)
        {
            barraFill.color = ColorBaseActual(targetFill);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  FEEDBACK VISUAL — destello al ganar Tonalli
    // ─────────────────────────────────────────────────────────────────────

    private void EjecutarPulso()
    {
        if (barraFill == null) return;
        if (_pulsoRoutine != null) StopCoroutine(_pulsoRoutine);
        _pulsoRoutine = StartCoroutine(PulsoRoutine());
    }

    private IEnumerator PulsoRoutine()
    {
        Color colorBase = ColorBaseActual(_currentTonalli / MaxTonalli);

        // Flash rápido de blanco → color base
        barraFill.color = Color.white;
        float t = 0f;
        while (t < duracionPulso)
        {
            t += Time.deltaTime;
            barraFill.color = Color.Lerp(Color.white, colorBase, t / duracionPulso);
            yield return null;
        }
        barraFill.color = colorBase;
        _pulsoRoutine = null;
    }
}