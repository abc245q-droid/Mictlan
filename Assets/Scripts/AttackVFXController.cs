using UnityEngine;
using System.Collections;

/// <summary>
/// Gestiona la aparición y desaparición de GameObjects placeholder
/// que simulan animaciones de ataque mientras no existen sprites animados.
///
/// JERARQUÍA RECOMENDADA EN UNITY:
/// ─ Romerito (RomeritoCombat, AttackVFXController)
///   ├── AttackPoint               ← ya existe en tu escena
///   │     ├── VFX_Ataque_Neutro   ← hijo nuevo (SpriteRenderer, colócalo aquí)
///   │     ├── VFX_Ataque_Fuego    ← hijo nuevo
///   │     ├── VFX_Ataque_Rayos    ← hijo nuevo
///   │     └── VFX_Ataque_Tierra   ← hijo nuevo
///   └── PogoPoint                 ← ya existe en tu escena
///         └── VFX_Pogo            ← hijo nuevo
///
/// Por ser hijos de AttackPoint/PogoPoint, heredan el flip de Romerito
/// sin ningún código extra. El sprite siempre apunta en la dirección correcta.
/// </summary>
public class AttackVFXController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  ESTRUCTURAS DE DATOS
    // ─────────────────────────────────────────────────────────────────────────

    [System.Serializable]
    public class AtaqueVisual
    {
        [Tooltip("Nombre descriptivo (solo para el Inspector)")]
        public string nombre = "Ataque";

        [Tooltip("GameObject hijo que representa este ataque visualmente. " +
                 "Debe ser hijo de AttackPoint o PogoPoint.")]
        public GameObject objeto;

        [Tooltip("Segundos que permanece visible. " +
                 "Ajusta según el ritmo de ataque (attackRate).")]
        [Range(0.05f, 1f)]
        public float duracion = 0.15f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Ataques Normales")]
    [Tooltip("Índice debe coincidir con GodFavor:\n" +
             "  0 = Neutro\n  1 = Huehuetéotl\n  2 = Tláloc\n  3 = Tepeyollotl")]
    public AtaqueVisual[] ataquesNormales = new AtaqueVisual[4];

    [Header("Ataques Superiores (Arriba + Fire1)")]
    [Tooltip("Índice debe coincidir con GodFavor:\n" +
             "  0 = Neutro\n  1 = Huehuetéotl\n  2 = Tláloc\n  3 = Tepeyollotl")]
    public AtaqueVisual[] ataquesSuperiores = new AtaqueVisual[4];

    [Header("Ataque Pogo")]
    public AtaqueVisual atacquePogo;

    // ─────────────────────────────────────────────────────────────────────────
    //  ESTADO INTERNO
    // ─────────────────────────────────────────────────────────────────────────

    // Guardamos las corrutinas activas por slot para poder cancelarlas
    // si el jugador ataca de nuevo antes de que el visual desaparezca.
    // Así el timer siempre se reinicia limpio.
    private Coroutine[] _rutinasNormales;
    private Coroutine[] _rutinasSuperiores;
    private Coroutine _rutinaPogo;

    // ─────────────────────────────────────────────────────────────────────────
    //  INICIALIZACIÓN
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _rutinasNormales = new Coroutine[ataquesNormales.Length];
        _rutinasSuperiores = new Coroutine[ataquesSuperiores.Length];

        // Todos los GOs comienzan desactivados.
        // Si se dejaron activos en el Editor, los forzamos a oculto aquí.
        foreach (AtaqueVisual av in ataquesNormales)
            if (av.objeto != null) av.objeto.SetActive(false);

        foreach (AtaqueVisual av in ataquesSuperiores)
            if (av.objeto != null) av.objeto.SetActive(false);

        if (atacquePogo.objeto != null)
            atacquePogo.objeto.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  API PÚBLICA — Llamados desde RomeritoCombat
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activa el visual del ataque normal correspondiente al Favor activo.
    /// Llamar desde RomeritoCombat.PerformAttack().
    /// </summary>
    /// <param name="favorIndex">Castear GodFavor a int: (int)currentFavor</param>
    public void MostrarAtaqueNormal(int favorIndex)
    {
        if (favorIndex < 0 || favorIndex >= ataquesNormales.Length)
        {
            Debug.LogWarning($"[AttackVFX] favorIndex {favorIndex} fuera de rango.");
            return;
        }

        AtaqueVisual av = ataquesNormales[favorIndex];
        if (av.objeto == null) return; // No hay GO para este favor, sin error

        // Cancelar la corrutina previa del mismo slot antes de reiniciarla
        if (_rutinasNormales[favorIndex] != null)
            StopCoroutine(_rutinasNormales[favorIndex]);

        _rutinasNormales[favorIndex] = StartCoroutine(MostrarYOcultar(av.objeto, av.duracion));
    }

    /// <summary>
    /// Activa el visual del ataque superior correspondiente al Favor activo.
    /// Llamar desde RomeritoCombat.PerformAttackUp().
    /// </summary>
    /// <param name="favorIndex">Castear GodFavor a int: (int)currentFavor</param>
    public void MostrarAtaqueSuperior(int favorIndex)
    {
        if (favorIndex < 0 || favorIndex >= ataquesSuperiores.Length)
        {
            Debug.LogWarning($"[AttackVFX] favorIndex {favorIndex} fuera de rango (superior).");
            return;
        }

        AtaqueVisual av = ataquesSuperiores[favorIndex];
        if (av.objeto == null) return;

        if (_rutinasSuperiores[favorIndex] != null)
            StopCoroutine(_rutinasSuperiores[favorIndex]);

        _rutinasSuperiores[favorIndex] = StartCoroutine(MostrarYOcultar(av.objeto, av.duracion));
    }

    /// <summary>
    /// Activa el visual del Pogo.
    /// Llamar desde RomeritoCombat.EjecutarPogo() cuando hay rebote.
    /// </summary>
    public void MostrarPogo()
    {
        if (atacquePogo.objeto == null) return;

        if (_rutinaPogo != null)
            StopCoroutine(_rutinaPogo);

        _rutinaPogo = StartCoroutine(MostrarYOcultar(atacquePogo.objeto, atacquePogo.duracion));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CORRUTINA INTERNA
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activa el GO, espera 'duracion' segundos y lo desactiva.
    /// Simple y sin allocations — reutiliza el mismo GO en cada ataque.
    /// </summary>
    private IEnumerator MostrarYOcultar(GameObject go, float duracion)
    {
        go.SetActive(true);
        yield return new WaitForSeconds(duracion);
        go.SetActive(false);
    }
}