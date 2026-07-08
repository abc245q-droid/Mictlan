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

    [Header("Sprite del Cuerpo (para swaps)")]
    [Tooltip("SpriteRenderer del cuerpo/idle de Romerito. Se APAGA mientras dura " +
             "un ataque que requiera swap completo (por ejemplo el aéreo lateral). " +
             "Si se deja vacío, los swaps quedan como VFX encimados — no crashea.")]
    public SpriteRenderer bodySprite;

    [Header("Ataques Laterales en el Aire")]
    [Tooltip("Sprite dedicado al ataque lateral cuando Romerito NO está en el suelo. " +
             "Índice = GodFavor (0=Neutro, 1=Huehuetéotl, 2=Tláloc, 3=Tepeyollotl). " +
             "Si un slot queda vacío, MostrarAtaqueLateralAire cae al slot correspondiente " +
             "de ataquesNormales — así es seguro estrenarlo un favor a la vez.")]
    public AtaqueVisual[] ataquesLateralAire = new AtaqueVisual[4];

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
    private Coroutine[] _rutinasLateralAire;
    private Coroutine _rutinaPogo;

    // Cache de sprites con tag "WeaponSprite" bajo Romerito.
    // Se apagan durante el swap del ataque aéreo — si no, el sprite
    // del Macahuitl en la espalda queda visible encima del ataque.
    private SpriteRenderer[] _weaponSprites = System.Array.Empty<SpriteRenderer>();

    // ─────────────────────────────────────────────────────────────────────────
    //  INICIALIZACIÓN
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _rutinasNormales = new Coroutine[ataquesNormales.Length];
        _rutinasSuperiores = new Coroutine[ataquesSuperiores.Length];
        _rutinasLateralAire = new Coroutine[ataquesLateralAire.Length];

        // Todos los GOs comienzan desactivados.
        // Si se dejaron activos en el Editor, los forzamos a oculto aquí.
        foreach (AtaqueVisual av in ataquesNormales)
            if (av.objeto != null) av.objeto.SetActive(false);

        foreach (AtaqueVisual av in ataquesSuperiores)
            if (av.objeto != null) av.objeto.SetActive(false);

        foreach (AtaqueVisual av in ataquesLateralAire)
            if (av.objeto != null) av.objeto.SetActive(false);

        if (atacquePogo.objeto != null)
            atacquePogo.objeto.SetActive(false);

        RefrescarCacheWeaponSprites();
    }

    /// <summary>
    /// Reconstruye el cache de SpriteRenderer con tag "WeaponSprite"
    /// bajo este transform (típicamente Romerito). Llamarlo solo si
    /// se añaden/quitan sprites de arma en runtime — Awake ya lo hace
    /// una vez para el setup normal.
    /// </summary>
    public void RefrescarCacheWeaponSprites()
    {
        var todos = GetComponentsInChildren<SpriteRenderer>(true);
        int n = 0;
        for (int i = 0; i < todos.Length; i++)
            if (todos[i] != null && todos[i].CompareTag("WeaponSprite")) n++;

        _weaponSprites = new SpriteRenderer[n];
        int k = 0;
        for (int i = 0; i < todos.Length; i++)
            if (todos[i] != null && todos[i].CompareTag("WeaponSprite"))
                _weaponSprites[k++] = todos[i];
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

        _rutinasNormales[favorIndex] = StartCoroutine(MostrarYOcultarConWeapon(av.objeto, av.duracion));
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

        _rutinasSuperiores[favorIndex] = StartCoroutine(MostrarYOcultarConWeapon(av.objeto, av.duracion));
    }

    /// <summary>
    /// Activa el visual del ataque lateral EN EL AIRE (no pogo) para el
    /// favor activo. Si el slot está vacío, cae al ataque normal del
    /// mismo favor — así se puede estrenar un favor a la vez sin
    /// romper los otros.
    /// Llamar desde RomeritoCombat.PerformAttack() cuando enElAire.
    /// </summary>
    /// <param name="favorIndex">Castear GodFavor a int: (int)currentFavor</param>
    public void MostrarAtaqueLateralAire(int favorIndex)
    {
        if (favorIndex < 0 || favorIndex >= ataquesLateralAire.Length)
        {
            Debug.LogWarning($"[AttackVFX] favorIndex {favorIndex} fuera de rango (aire).");
            MostrarAtaqueNormal(favorIndex);
            return;
        }

        AtaqueVisual av = ataquesLateralAire[favorIndex];

        // Fallback al slot de tierra si el aire no está cableado en este favor.
        if (av.objeto == null)
        {
            MostrarAtaqueNormal(favorIndex);
            return;
        }

        if (_rutinasLateralAire[favorIndex] != null)
            StopCoroutine(_rutinasLateralAire[favorIndex]);

        _rutinasLateralAire[favorIndex] = StartCoroutine(MostrarYOcultarConSwap(av.objeto, av.duracion));
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

        _rutinaPogo = StartCoroutine(MostrarYOcultarConWeapon(atacquePogo.objeto, atacquePogo.duracion));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CORRUTINA INTERNA
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activa el GO, espera 'duracion' segundos y lo desactiva.
    /// Simple, sin tocar sprites del jugador. Queda disponible para
    /// VFX puramente decorativos — hoy no la llama ningún método.
    /// </summary>
    private IEnumerator MostrarYOcultar(GameObject go, float duracion)
    {
        go.SetActive(true);
        yield return new WaitForSeconds(duracion);
        go.SetActive(false);
    }

    /// <summary>
    /// Variante SOLO WEAPON: apaga los sprites con tag "WeaponSprite"
    /// (macahuitl en la espalda y sus variantes) mientras dura el
    /// ataque. Coherencia narrativa: el arma sale de la espalda para
    /// golpear. Body idle NO se toca — el sprite del cuerpo sigue
    /// visible como base sobre la que se dibuja el VFX del ataque.
    /// La usan los ataques que NO son sprite-swap completo:
    /// normal (tierra), superior, pogo.
    /// </summary>
    private IEnumerator MostrarYOcultarConWeapon(GameObject go, float duracion)
    {
        bool[] weaponPrev = ApagarWeaponSprites();

        go.SetActive(true);
        yield return new WaitForSeconds(duracion);
        go.SetActive(false);

        RestaurarWeaponSprites(weaponPrev);
    }

    /// <summary>
    /// Variante SWAP COMPLETO: apaga body idle Y weapons durante el
    /// ataque. Al terminar, cada sprite vuelve al estado en que estaba
    /// ANTES — si estaba apagado por otra razón (muerte, I-frames), no
    /// se reenciende por accidente. La usa el ataque aéreo lateral.
    /// </summary>
    private IEnumerator MostrarYOcultarConSwap(GameObject go, float duracion)
    {
        bool bodyPrev = true;
        if (bodySprite != null)
        {
            bodyPrev = bodySprite.enabled;
            bodySprite.enabled = false;
        }

        bool[] weaponPrev = ApagarWeaponSprites();

        go.SetActive(true);
        yield return new WaitForSeconds(duracion);
        go.SetActive(false);

        if (bodySprite != null)
            bodySprite.enabled = bodyPrev;

        RestaurarWeaponSprites(weaponPrev);
    }

    // ─── Helpers compartidos por las dos variantes con weapon ───────

    /// <summary>Guarda el estado previo de cada WeaponSprite y los apaga.
    /// Devuelve el array de estados previos (o null si no hay weapons
    /// cacheados) para RestaurarWeaponSprites().</summary>
    private bool[] ApagarWeaponSprites()
    {
        if (_weaponSprites == null || _weaponSprites.Length == 0) return null;

        bool[] prev = new bool[_weaponSprites.Length];
        for (int i = 0; i < _weaponSprites.Length; i++)
        {
            var sr = _weaponSprites[i];
            if (sr == null) continue;
            prev[i] = sr.enabled;
            sr.enabled = false;
        }
        return prev;
    }

    /// <summary>Restaura cada WeaponSprite al estado guardado por
    /// ApagarWeaponSprites(). Null-safe si no había weapons.</summary>
    private void RestaurarWeaponSprites(bool[] prev)
    {
        if (prev == null || _weaponSprites == null) return;

        for (int i = 0; i < _weaponSprites.Length; i++)
        {
            var sr = _weaponSprites[i];
            if (sr == null) continue;
            sr.enabled = prev[i];
        }
    }
}