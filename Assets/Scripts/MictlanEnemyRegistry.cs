using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  MictlanEnemyRegistry — Registro por escena de reagrupables
//  REGISTRO_V2 — lazy singleton (fix de orden de inicialización)
// ============================================================
//
//  Singleton POR ESCENA con creación PEREZOSA: el getter Instance
//  crea el GameObject y añade el componente la primera vez que
//  alguien lo pide. Así resolvemos el bug donde OnEnable de los
//  enemigos corría antes de RuntimeInitializeOnLoadMethod y
//  Instance era null.
//
//  CAMBIOS v2:
//  • Instance ahora es un lazy singleton en el getter.
//  • Removimos RuntimeInitializeOnLoadMethod (redundante).
//  • Añadimos _isQuitting para evitar crear registro durante el
//    shutdown de la aplicación (Unity marca objetos como null pero
//    algún OnDestroy tardío podría tocar el singleton).
//
//  NO usa DontDestroyOnLoad — se destruye al descargar la escena
//  y el próximo enemigo de la nueva escena lo re-crea.
// ============================================================

public class MictlanEnemyRegistry : MonoBehaviour
{
    private static MictlanEnemyRegistry _instance;
    private static bool _isQuitting = false;

    public static MictlanEnemyRegistry Instance
    {
        get
        {
            if (_instance == null && !_isQuitting && Application.isPlaying)
            {
                var go = new GameObject("[MictlanEnemyRegistry]");
                _instance = go.AddComponent<MictlanEnemyRegistry>();
            }
            return _instance;
        }
    }

    private readonly HashSet<EnemigoRespawnable> respawnables =
        new HashSet<EnemigoRespawnable>();

    // ── Ciclo de vida ────────────────────────────────────────

    void Awake()
    {
        // Race condition: si dos enemigos disparan el lazy-init casi
        // simultáneamente Unity nos protege (solo un GameObject se crea),
        // pero por higiene también fijamos _instance aquí.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (_instance == this) _instance = null;
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    void OnSceneUnloaded(Scene s)
    {
        respawnables.Clear();
    }

    // ── API ──────────────────────────────────────────────────

    public void Registrar(EnemigoRespawnable e)
    {
        if (e == null) return;
        if (respawnables.Add(e))
            Debug.Log($"[MictlanEnemyRegistry] Registrado: {e.name}. Total: {respawnables.Count}");
    }

    public void Desregistrar(EnemigoRespawnable e)
    {
        if (e != null) respawnables.Remove(e);
    }

    /// <summary>
    /// Reagrupa a TODOS los enemigos dispersos. Llamado por
    /// RomeritoHealth.Respawn() tras reaparecer en el Cihuacalli.
    /// </summary>
    public void RespawnearTodos()
    {
        int reagrupados = 0;
        int total = respawnables.Count;

        // Copiamos porque Respawnear() dispara SetActive(true) → OnEnable
        // → Registrar(this): modificaría el HashSet mientras iteramos.
        var copia = new List<EnemigoRespawnable>(respawnables);
        foreach (var e in copia)
        {
            if (e == null) continue;
            if (e.EstaDisperso)
            {
                e.Respawnear();
                reagrupados++;
            }
        }

        Debug.Log($"[MictlanEnemyRegistry] Reagrupados: {reagrupados}/{total} " +
                  "por el yohual-ehécatl.");
    }
}
