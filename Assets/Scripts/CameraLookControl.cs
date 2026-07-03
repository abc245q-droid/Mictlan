using UnityEngine;
using Unity.Cinemachine; // Cinemachine 3.x (Unity 6)

/// <summary>
/// Control de "peek" vertical de cámara (estilo Hollow Knight):
/// al mantener arriba/abajo estando quieto, desplaza el encuadre para
/// revelar más terreno en esa dirección. Trabaja sobre el
/// CinemachinePositionComposer de la CinemachineCamera.
/// </summary>
public class CameraLookControl : MonoBehaviour
{
    [Header("Referencias")]
    public CinemachineCamera virtualCamera;

    [Header("Configuración")]
    [Tooltip("Cuánto se desplaza el encuadre (fracción de pantalla).")]
    public float lookOffsetAmount = 0.3f;

    [Tooltip("Segundos manteniendo la dirección antes de activar el peek.")]
    public float timeToTrigger = 0.25f;

    [Tooltip("Nitidez del suavizado (mayor = más rápido). Ahora es independiente de los FPS.")]
    public float smoothTime = 2f;

    [Tooltip("Zona muerta del stick para ignorar el drift analógico del mando.")]
    public float deadzone = 0.2f;

    [Tooltip("Invertir el eje vertical del peek. false = comportamiento actual; " +
             "true = convención Hollow Knight (arriba revela arriba).")]
    public bool invertVertical = false;

    private CinemachinePositionComposer composer;
    private float defaultScreenY;
    private float targetScreenY;
    private float timer;

    void Start()
    {
        if (virtualCamera == null) return;

        composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
        if (composer != null)
        {
            // Guardamos el encuadre base real (robusto ante cualquier convención).
            defaultScreenY = composer.Composition.ScreenPosition.y;
            targetScreenY = defaultScreenY;
        }
    }

    void Update()
    {
        if (composer == null) return;

        float xInput = Input.GetAxisRaw("Horizontal");
        float yInput = Input.GetAxisRaw("Vertical");

        // Umbrales con zona muerta en vez de igualdad exacta (fix del mando).
        bool horizontalIdle = Mathf.Abs(xInput) < deadzone;
        bool pressingUp = yInput > deadzone;
        bool pressingDown = yInput < -deadzone;

        if (horizontalIdle && (pressingUp || pressingDown))
        {
            timer += Time.deltaTime;

            if (timer >= timeToTrigger)
            {
                // Por defecto (comportamiento actual): ARRIBA revela abajo, ABAJO revela arriba.
                // invertVertical = true -> convención Hollow Knight.
                float dir = (pressingUp ? 1f : -1f) * (invertVertical ? -1f : 1f);
                targetScreenY = defaultScreenY + dir * lookOffsetAmount;
            }
        }
        else
        {
            // Al movernos horizontalmente o soltar, volvemos al encuadre base.
            timer = 0f;
            targetScreenY = defaultScreenY;
        }

        // Suavizado exponencial independiente del framerate.
        float k = 1f - Mathf.Exp(-smoothTime * Time.deltaTime);

        Vector2 pos = composer.Composition.ScreenPosition;
        pos.y = Mathf.Lerp(pos.y, targetScreenY, k);

        // Anclamos al llegar para no reescribir micro-valores eternamente.
        if (Mathf.Abs(pos.y - targetScreenY) < 0.0005f)
            pos.y = targetScreenY;

        composer.Composition.ScreenPosition = pos;
    }
}
