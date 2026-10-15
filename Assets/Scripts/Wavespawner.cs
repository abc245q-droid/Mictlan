using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  WaveSpawner — v3 (presagio del tzompantli)
// ============================================================
//
//  CAMBIOS v3:
//  • Si un spawnPoint tiene TzompantliPresagio, el cráneo tiembla y
//    se suelta ANTES de instanciar al enemigo (telegrafeo).
//  • spawnsPendientes: la oleada no cuenta como limpia mientras haya
//    presagios en curso (si no, el último enemigo aún "en camino"
//    podía dar la oleada por terminada).
//  • Se evita repetir un punto que ya está presagiando.
//  • ResetWaves()/StopWaves() cancelan presagios y restauran cráneos.
//
//  CAMBIOS respecto a v1:
//  • Evento público OnAllWavesFinished que MacahuitlRoomManager escucha.
//  • Método público ResetWaves() que detiene todo, limpia el estado
//    y deja el spawner listo para llamar StartWaves() de nuevo.
//
// ============================================================

public class WaveSpawner : MonoBehaviour
{
    // ----------------------------------------------------------
    //  ESTRUCTURAS DE DATOS
    // ----------------------------------------------------------

    [System.Serializable]
    public class Wave
    {
        [Tooltip("Nombre descriptivo de la oleada (solo para el Inspector)")]
        public string waveName = "Oleada";

        [Tooltip("Prefab del enemigo a spawnear en esta oleada")]
        public GameObject enemyPrefab;

        [Tooltip("Cuántos enemigos aparecen en esta oleada")]
        public int enemyCount = 3;

        [Tooltip("Segundos entre cada aparición dentro de la oleada")]
        public float spawnInterval = 2f;

        [Tooltip("Segundos de pausa ANTES de que empiece esta oleada")]
        public float delayBeforeWave = 1.5f;
    }

    // ----------------------------------------------------------
    //  INSPECTOR
    // ----------------------------------------------------------

    [Header("Oleadas")]
    public Wave[] waves;

    [Header("Puntos de Aparición")]
    public Transform[] spawnPoints;

    [Header("Opciones")]
    public bool waitForWaveClear = true;
    public bool startAutomatically = false;

    [Header("Presagio del Tzompantli (telegrafeo)")]
    [Tooltip("Usar TzompantliPresagio en los spawnPoints que lo tengan")]
    public bool usarPresagio = true;
    [Tooltip("No elegir un punto que ya está temblando si hay otro libre")]
    public bool evitarPuntoOcupado = true;

    [Header("Eventos (Opcional)")]
    public GameObject objectToActivateOnFinish;
    public Animator animatorToTriggerOnFinish;
    public string animatorTriggerName = "WavesClear";

    // ----------------------------------------------------------
    //  EVENTOS
    // ----------------------------------------------------------

    /// <summary>
    /// Se dispara cuando TODAS las oleadas se completan exitosamente.
    /// MacahuitlRoomManager se suscribe a este evento.
    /// </summary>
    public event System.Action OnAllWavesFinished;

    // ----------------------------------------------------------
    //  ESTADO INTERNO
    // ----------------------------------------------------------

    private int currentWaveIndex = 0;
    private bool isRunning = false;
    private int enemiesAlive = 0;
    private bool allWavesComplete = false;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private int spawnsPendientes = 0;   // presagios en curso (enemigos "en camino")

    // ----------------------------------------------------------
    //  UNITY MESSAGES
    // ----------------------------------------------------------

    void Start()
    {
        if (startAutomatically)
            StartWaves();
    }

    // ----------------------------------------------------------
    //  API PÚBLICA
    // ----------------------------------------------------------

    public void StartWaves()
    {
        if (isRunning)
        {
            Debug.LogWarning("[WaveSpawner] Ya hay oleadas en curso.");
            return;
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("[WaveSpawner] No hay oleadas configuradas.");
            return;
        }

        currentWaveIndex = 0;
        allWavesComplete = false;
        isRunning = true;

        StartCoroutine(RunAllWaves());
    }

    public void StopWaves()
    {
        StopAllCoroutines();
        isRunning = false;
        enemiesAlive = 0;
        spawnsPendientes = 0;
        activeEnemies.Clear();
        CancelarPresagios();
    }

    /// <summary>
    /// Detiene las oleadas en curso y resetea completamente el estado
    /// para que StartWaves() pueda llamarse de nuevo desde cero.
    /// Llamado por MacahuitlRoomManager cuando Romerito muere.
    /// </summary>
    public void ResetWaves()
    {
        StopAllCoroutines();

        currentWaveIndex = 0;
        isRunning = false;
        allWavesComplete = false;
        enemiesAlive = 0;
        spawnsPendientes = 0;
        activeEnemies.Clear();
        CancelarPresagios();

        Debug.Log("[WaveSpawner] Oleadas reseteadas. Listo para StartWaves().");
    }

    public bool AreAllWavesComplete() => allWavesComplete;

    // ----------------------------------------------------------
    //  CORRUTINAS PRINCIPALES
    // ----------------------------------------------------------

    IEnumerator RunAllWaves()
    {
        for (int i = 0; i < waves.Length; i++)
        {
            currentWaveIndex = i;
            Wave wave = waves[i];

            if (wave.delayBeforeWave > 0f)
                yield return new WaitForSeconds(wave.delayBeforeWave);

            Debug.Log($"[WaveSpawner] Iniciando {wave.waveName} ({wave.enemyCount} enemigos)");
            yield return StartCoroutine(SpawnWave(wave));

            if (waitForWaveClear)
            {
                yield return StartCoroutine(WaitUntilWaveClear());
                Debug.Log($"[WaveSpawner] {wave.waveName} limpiada.");
            }
        }

        // Que ningún enemigo quede "en camino" al cerrar el combate.
        while (spawnsPendientes > 0)
            yield return null;

        OnAllWavesComplete();
    }

    IEnumerator SpawnWave(Wave wave)
    {
        if (wave.enemyPrefab == null)
        {
            Debug.LogError($"[WaveSpawner] La oleada '{wave.waveName}' no tiene enemyPrefab.");
            yield break;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[WaveSpawner] No hay spawnPoints configurados.");
            yield break;
        }

        for (int i = 0; i < wave.enemyCount; i++)
        {
            // El intervalo cuenta desde el INICIO del presagio: el ritmo de la
            // oleada no cambia, el enemigo llega 'duracion' segundos después.
            StartCoroutine(PresagiarYSpawnear(wave.enemyPrefab));

            if (i < wave.enemyCount - 1)
                yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    IEnumerator PresagiarYSpawnear(GameObject prefab)
    {
        Transform spawnPoint = ElegirPuntoDeAparicion();
        spawnsPendientes++;

        TzompantliPresagio presagio = usarPresagio
            ? spawnPoint.GetComponent<TzompantliPresagio>()
            : null;

        if (presagio != null)
            yield return presagio.Presagiar();

        SpawnOneEnemy(prefab, spawnPoint);
        spawnsPendientes = Mathf.Max(0, spawnsPendientes - 1);
    }

    Transform ElegirPuntoDeAparicion()
    {
        if (usarPresagio && evitarPuntoOcupado)
        {
            List<Transform> libres = new List<Transform>();
            foreach (Transform sp in spawnPoints)
            {
                if (sp == null) continue;
                TzompantliPresagio p = sp.GetComponent<TzompantliPresagio>();
                if (p == null || !p.Ocupado) libres.Add(sp);
            }
            if (libres.Count > 0)
                return libres[Random.Range(0, libres.Count)];
        }
        return spawnPoints[Random.Range(0, spawnPoints.Length)];
    }

    void CancelarPresagios()
    {
        if (spawnPoints == null) return;
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            TzompantliPresagio p = sp.GetComponent<TzompantliPresagio>();
            if (p != null) p.Cancelar();
        }
    }

    void SpawnOneEnemy(GameObject prefab, Transform spawnPoint)
    {
        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        enemiesAlive++;
        activeEnemies.Add(enemy);

        EnemyDummy enemyScript = enemy.GetComponent<EnemyDummy>();
        if (enemyScript != null)
        {
            // Marcamos como enemigo de oleada — al morir se destruye
            // normalmente, sin pasar por el sistema de reagrupación
            // del yohual-ehécatl (que es solo para enemigos de mundo).
            enemyScript.esDeOleada = true;
            enemyScript.OnDeath += HandleEnemyDeath;
        }
        else
            Debug.LogWarning($"[WaveSpawner] '{prefab.name}' no tiene EnemyDummy. Usando polling.");
    }

    IEnumerator WaitUntilWaveClear()
    {
        while (true)
        {
            activeEnemies.RemoveAll(e => e == null);

            if (spawnsPendientes <= 0 &&
                (enemiesAlive <= 0 || activeEnemies.Count == 0))
                break;

            yield return new WaitForSeconds(0.25f);
        }

        enemiesAlive = 0;
        activeEnemies.Clear();
    }

    // ----------------------------------------------------------
    //  CALLBACKS
    // ----------------------------------------------------------

    void HandleEnemyDeath()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);
    }

    void OnAllWavesComplete()
    {
        isRunning = false;
        allWavesComplete = true;

        Debug.Log("[WaveSpawner] ¡Todas las oleadas completadas!");

        // Notificar a quien esté suscrito (MacahuitlRoomManager)
        OnAllWavesFinished?.Invoke();

        if (objectToActivateOnFinish != null)
            objectToActivateOnFinish.SetActive(true);

        if (animatorToTriggerOnFinish != null)
            animatorToTriggerOnFinish.SetTrigger(animatorTriggerName);
    }

    // ----------------------------------------------------------
    //  GIZMOS
    // ----------------------------------------------------------

    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        Gizmos.color = Color.red;
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireSphere(sp.position, 0.3f);
            Gizmos.DrawLine(sp.position, sp.position + Vector3.up * 0.6f);
        }
    }
}