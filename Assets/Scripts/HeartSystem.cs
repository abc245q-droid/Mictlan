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

        AplicarVisibilidad();
    }

    // ── [HUD-N0] Corazones ocultos hasta el primer combate ────────────

    [Header("Aparición (primer combate)")]
    [Tooltip("Pausa tras el sello de la puerta antes del primer corazón.")]
    public float retrasoAparicion = 0.35f;
    [Tooltip("Segundos entre un corazón y el siguiente.")]
    public float intervaloAparicion = 0.18f;
    [Tooltip("Duración del 'pop' de cada corazón.")]
    public float duracionPop = 0.22f;

    private CanvasGroup grupo;
    private Coroutine revelarRutina;

    /// <summary>¿Ya se ganaron los corazones del HUD? (sin GameManager: sí).</summary>
    public static bool CorazonesDesbloqueados
    {
        get
        {
            var gm = GameManager01.instance;
            if (gm == null || gm.currentData == null) return true;
            return gm.currentData.hudCorazonesDesbloqueado || gm.currentData.tieneMacuahuitl;
        }
    }

    CanvasGroup Grupo
    {
        get
        {
            if (grupo == null && heartContainer != null)
            {
                grupo = heartContainer.GetComponent<CanvasGroup>();
                if (grupo == null) grupo = heartContainer.gameObject.AddComponent<CanvasGroup>();
            }
            return grupo;
        }
    }

    void AplicarVisibilidad()
    {
        if (revelarRutina != null || Grupo == null) return;
        Grupo.alpha = CorazonesDesbloqueados ? 1f : 0f;
    }

    /// <summary>
    /// Marca los corazones como ganados y los hace aparecer uno a uno.
    /// Si ya estaban visibles, no hace nada (p. ej. al reintentar el combate).
    /// </summary>
    public void RevelarUnoAUno()
    {
        var gm = GameManager01.instance;
        if (gm != null && gm.currentData != null)
            gm.currentData.hudCorazonesDesbloqueado = true;

        if (Grupo == null || Grupo.alpha > 0.99f) return;
        if (revelarRutina != null) StopCoroutine(revelarRutina);
        revelarRutina = StartCoroutine(RutinaRevelar());
    }

    private IEnumerator RutinaRevelar()
    {
        foreach (Image h in hearts)
            if (h != null) h.rectTransform.localScale = Vector3.zero;
        Grupo.alpha = 1f;

        yield return new WaitForSecondsRealtime(retrasoAparicion);

        foreach (Image h in hearts)
        {
            if (h == null) continue;
            StartCoroutine(Pop(h.rectTransform));
            yield return new WaitForSecondsRealtime(intervaloAparicion);
        }

        yield return new WaitForSecondsRealtime(duracionPop);
        revelarRutina = null;
    }

    private IEnumerator Pop(RectTransform rt)
    {
        float t = 0f;
        while (t < duracionPop)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracionPop);
            // 0 → 1.25 (rebote) → 1
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.25f, k / 0.6f)
                               : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
            rt.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // 2. ACTUALIZAR VISUALES (Se llama cada vez que nos golpean)
    public void UpdateHearts(int currentHealth, bool sacudir = true)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            if (hearts[i] == null) continue;

            // L�gica simple:
            // Si el �ndice 'i' es menor que la vida actual, este coraz�n debe estar lleno.
            hearts[i].sprite = (i < currentHealth) ? fullHeart : emptyHeart;
        }

        // Micro-shake del contenedor para feedback táctil de daño
        // (no al restaurar la vida, p. ej. al despertar el teyolía).
        if (!sacudir) return;
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
            // [IMPACTO] Tiempo real: vibra también durante el hit-stop.
            t += Time.unscaledDeltaTime;
            float fuerza = Mathf.Lerp(shakeFuerza, 0f, t / shakeDuracion);
            rt.anchoredPosition = posOriginal + Random.insideUnitCircle * fuerza;
            yield return null;
        }

        rt.anchoredPosition = posOriginal;
    }
}