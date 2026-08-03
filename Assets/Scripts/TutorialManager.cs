using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  TutorialManager — Singleton persistente
// ============================================================
//  Gestiona toasts (no bloqueantes) y modales (pausan el juego).
//  DontDestroyOnLoad: mensajes ya vistos NO reaparecen tras respawn.
//  ResetearVistos() se llama desde GameManager01 al iniciar
//  Nueva Partida.
// ============================================================

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Referencias — Toast")]
    public CanvasGroup panelToast;
    public TextMeshProUGUI textoToast;
    public Image iconoToast;

    [Header("Referencias — Modal")]
    public GameObject panelModal;
    public CanvasGroup grupoModal;
    public TextMeshProUGUI textoModal;
    public Image iconoModal;
    public Button botonAceptarModal;

    [Header("Ajustes")]
    public float duracionFadeToast = 0.35f;
    public float duracionFadeModal = 0.25f;
    [Tooltip("Botón del InputManager para cerrar el modal (además del click).")]
    public string botonSubmit = "Submit";

    private readonly HashSet<string> mensajesVistos = new HashSet<string>();
    private readonly Queue<MensajeTutorial> colaToasts = new Queue<MensajeTutorial>();
    private Coroutine rutinaToast;
    private bool modalActivo;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (panelToast != null) panelToast.alpha = 0f;
        if (panelModal != null) panelModal.SetActive(false);
        if (botonAceptarModal != null) botonAceptarModal.onClick.AddListener(CerrarModal);
    }

    void Update()
    {

        if (Input.GetKeyDown(KeyCode.T))
        {
            var msj = ScriptableObject.CreateInstance<MensajeTutorial>();
            msj.id = "test_" + Time.time;
            msj.texto = "Toast de prueba";
            msj.tipo = TipoMensajeTutorial.Toast;
            msj.duracionToast = 3f;
            Mostrar(msj);
        }

        if (modalActivo && Input.GetButtonDown(botonSubmit))
            CerrarModal();
    }

    // ========================================================
    //  API pública
    // ========================================================

    public bool YaVisto(string id) => mensajesVistos.Contains(id);

    public void Mostrar(MensajeTutorial msj)
    {
        if (msj == null || string.IsNullOrEmpty(msj.id)) return;
        if (mensajesVistos.Contains(msj.id)) return;

        mensajesVistos.Add(msj.id);

        if (msj.tipo == TipoMensajeTutorial.Toast)
            EncolarToast(msj);
        else
            AbrirModal(msj);
    }

    /// <summary>Resetea todos los "vistos". Llamar en Nueva Partida.</summary>
    public void ResetearVistos() => mensajesVistos.Clear();

    // ========================================================
    //  Toast
    // ========================================================

    private void EncolarToast(MensajeTutorial msj)
    {
        colaToasts.Enqueue(msj);
        if (rutinaToast == null)
            rutinaToast = StartCoroutine(ProcesarColaToasts());
    }

    private IEnumerator ProcesarColaToasts()
    {
        while (colaToasts.Count > 0)
        {
            var msj = colaToasts.Dequeue();
            textoToast.text = msj.texto;
            if (iconoToast != null)
            {
                iconoToast.sprite = msj.iconoBoton;
                iconoToast.enabled = msj.iconoBoton != null;
            }

            yield return FadeCanvasEscalado(panelToast, 0f, 1f, duracionFadeToast);
            yield return new WaitForSeconds(msj.duracionToast);
            yield return FadeCanvasEscalado(panelToast, 1f, 0f, duracionFadeToast);
        }
        rutinaToast = null;
    }

    private IEnumerator FadeCanvasEscalado(CanvasGroup cg, float desde, float hasta, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(desde, hasta, t / dur);
            yield return null;
        }
        cg.alpha = hasta;
    }

    // ========================================================
    //  Modal
    // ========================================================

    private void AbrirModal(MensajeTutorial msj)
    {
        textoModal.text = msj.texto;
        if (iconoModal != null)
        {
            iconoModal.sprite = msj.iconoBoton;
            iconoModal.enabled = msj.iconoBoton != null;
        }

        panelModal.SetActive(true);
        grupoModal.alpha = 0f;
        modalActivo = true;
        Time.timeScale = 0f;
        StartCoroutine(FadeModal(0f, 1f));
    }

    private void CerrarModal()
    {
        if (!modalActivo) return;
        modalActivo = false;
        StartCoroutine(CerrarModalRoutine());
    }

    private IEnumerator CerrarModalRoutine()
    {
        yield return FadeModal(1f, 0f);
        panelModal.SetActive(false);
        Time.timeScale = 1f;
    }

    private IEnumerator FadeModal(float desde, float hasta)
    {
        float t = 0f;
        // ¡Ojo! timeScale = 0 → usar unscaledDeltaTime.
        while (t < duracionFadeModal)
        {
            t += Time.unscaledDeltaTime;
            grupoModal.alpha = Mathf.Lerp(desde, hasta, t / duracionFadeModal);
            yield return null;
        }
        grupoModal.alpha = hasta;
    }
}
