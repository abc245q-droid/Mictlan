using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  InteractionManager — Singleton
// ============================================================
//  Registra el interactuable actual (el que tocó el trigger del
//  Player) y gestiona el prompt "Presiona B". Bloquea el uso de
//  Favores mientras haya un interactuable activo, vía la propiedad
//  estática HayInteractuableActivo consultada por FavorManager.
// ============================================================

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Referencias UI")]
    public GameObject promptRoot;          // Padre activable/desactivable
    public RectTransform promptRect;       // Se posiciona sobre el interactuable
    public Image iconoBoton;               // Ícono opcional del botón B
    public TextMeshProUGUI textoPrompt;    // "Presiona B"

    [Header("Input")]
    [Tooltip("Fire2 = B en pad Xbox por default.")]
    public string botonInteraccion = "Fire2";

    [Header("Ajustes visuales")]
    public Vector2 offsetPantalla = new Vector2(0f, 60f);
    public Camera camaraReferencia;

    private IInteractuable interactuableActual;

    /// <summary>Consultado por FavorManager para saber si debe ignorar B.</summary>
    public static bool HayInteractuableActivo =>
        Instance != null && Instance.interactuableActual != null;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (promptRoot != null) promptRoot.SetActive(false);
        if (camaraReferencia == null) camaraReferencia = Camera.main;
    }

    void LateUpdate()
    {
        if (interactuableActual == null) return;

        // Refrescar cámara si cambió de escena
        if (camaraReferencia == null) camaraReferencia = Camera.main;

        // Posicionar prompt sobre el interactuable
        if (promptRect != null && camaraReferencia != null)
        {
            Vector3 pantalla = camaraReferencia.WorldToScreenPoint(
                interactuableActual.PosicionMundo);
            promptRect.position = pantalla + (Vector3)offsetPantalla;
        }

        // Detectar B
        if (Input.GetButtonDown(botonInteraccion))
        {
            var actual = interactuableActual;
            Ocultar();
            actual.Interactuar();
        }
    }

    public void Registrar(IInteractuable interactuable)
    {
        interactuableActual = interactuable;
        if (promptRoot != null) promptRoot.SetActive(true);
        if (textoPrompt != null) textoPrompt.text = interactuable.TextoPrompt;
    }

    public void Desregistrar(IInteractuable interactuable)
    {
        if (interactuableActual == interactuable)
            Ocultar();
    }

    private void Ocultar()
    {
        interactuableActual = null;
        if (promptRoot != null) promptRoot.SetActive(false);
    }
}

// ============================================================
//  Contrato para cualquier objeto interactuable
// ============================================================
public interface IInteractuable
{
    string TextoPrompt { get; }
    Vector3 PosicionMundo { get; }
    void Interactuar();
}