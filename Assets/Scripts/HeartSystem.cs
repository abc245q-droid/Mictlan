using UnityEngine;
using UnityEngine.UI; // Necesario para trabajar con UI
using System.Collections;
using System.Collections.Generic; // Para usar Lists

public class HeartSystem : MonoBehaviour
{
    [Header("Referencias")]
    public GameObject heartPrefab;   // El prefab que creaste (la Imagen)
    public Transform heartContainer; // El objeto con el Horizontal Layout Group

    [Header("Sprites")]
    public Sprite fullHeart;  // Imagen roja
    public Sprite emptyHeart; // Imagen gris/vac�a

    [Header("Feedback Visual")]
    [Tooltip("Segundos que dura el micro-shake del corazón al recibir daño.")]
    public float shakeDuracion = 0.25f;
    [Tooltip("Intensidad del shake en píxeles de UI.")]
    public float shakeFuerza = 6f;

    // Lista interna para guardar los corazones creados
    private List<Image> hearts = new List<Image>();
    // Guardamos las posiciones originales para restaurarlas después del shake
    private List<Vector2> heartPosOriginales = new List<Vector2>();
    private Coroutine _shakeRoutine;

    // 1. INICIALIZAR (Se llama al empezar el juego)
    public void InitHearts(int maxHealth)
    {
        // Limpiar por si acaso hab�a algo
        foreach (Transform child in heartContainer)
        {
            Destroy(child.gameObject);
        }
        hearts.Clear();
        heartPosOriginales.Clear();

        // Crear tantos corazones como vida m�xima tengamos
        for (int i = 0; i < maxHealth; i++)
        {
            // Instanciar el prefab dentro del contenedor
            GameObject newHeart = Instantiate(heartPrefab, heartContainer);

            Image img = newHeart.GetComponent<Image>();
            hearts.Add(img);

            // Guardar la posición ancla original para restaurar tras el shake
            RectTransform rt = newHeart.GetComponent<RectTransform>();
            heartPosOriginales.Add(rt != null ? rt.anchoredPosition : Vector2.zero);
        }
    }

    // 2. ACTUALIZAR VISUALES (Se llama cada vez que nos golpean)
    public void UpdateHearts(int currentHealth)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            if (hearts[i] == null) continue;

            // L�gica simple:
            // Si el �ndice 'i' es menor que la vida actual, este coraz�n debe estar lleno.
            hearts[i].sprite = (i < currentHealth) ? fullHeart : emptyHeart;
        }

        // Micro-shake del contenedor para feedback táctil de daño
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeContainer());
    }

    // 3. SHAKE — micro-vibración del contenedor de corazones al recibir daño
    private IEnumerator ShakeContainer()
    {
        RectTransform rt = heartContainer.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 posOriginal = rt.anchoredPosition;
        float t = 0f;

        while (t < shakeDuracion)
        {
            t += Time.deltaTime;
            float fuerza = Mathf.Lerp(shakeFuerza, 0f, t / shakeDuracion);
            rt.anchoredPosition = posOriginal + Random.insideUnitCircle * fuerza;
            yield return null;
        }

        rt.anchoredPosition = posOriginal;
    }
}