using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
//  WaveSpawner — v2 (con ResetWaves y evento OnAllWavesFinished)
// ============================================================
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
        activeEnemies.Clear();
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
        activeEnemies.Clear();

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
            SpawnOneEnemy(wave.enemyPrefab);

            if (i < wave.enemyCount - 1)
                yield return new WaitForSeconds(wave.spawnInterval);
        }
    }

    void SpawnOneEnemy(GameObject prefab)
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        enemiesAlive++;
        activeEnemies.Add(enemy);

        EnemyDummy enemyScript = enemy.GetComponent<EnemyDummy>();
        if (enemyScript != null)
            enemyScript.OnDeath += HandleEnemyDeath;
        else
            Debug.LogWarning($"[WaveSpawner] '{prefab.name}' no tiene EnemyDummy. Usando polling.");
    }

    IEnumerator WaitUntilWaveClear()
    {
        while (true)
        {
            activeEnemies.RemoveAll(e => e == null);

            if (enemiesAlive <= 0 || activeEnemies.Count == 0)
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