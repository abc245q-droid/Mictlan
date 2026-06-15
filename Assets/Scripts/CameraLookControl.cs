using UnityEngine;
using Unity.Cinemachine; // O 'using Cinemachine;' si usas la versión 2.x

public class CameraLookControl : MonoBehaviour
{
    [Header("Referencias")]
 
    public CinemachineCamera virtualCamera;
    

    [Header("Configuración")]
    public float lookOffsetAmount = 0.3f; // Cuánto desplazar (0.3 es un 30% de pantalla)
    public float timeToTrigger = 0.25f;    // Tiempo manteniendo botón para activar
    public float smoothTime = 2f;         // Velocidad del desplazamiento

    private CinemachinePositionComposer framingTransposer; 
    private float defaultScreenY;
    private float targetScreenY;
    private float timer;

    // Inputs
    private float yInput;
    private float xInput;

    void Start()
    {
        if (virtualCamera != null)
        {
          
            framingTransposer = virtualCamera.GetComponent<CinemachinePositionComposer>();

            if (framingTransposer != null)
            {
                defaultScreenY = framingTransposer.Composition.ScreenPosition.y; // Guardamos el valor inicial (usualmente 0.5)
                targetScreenY = defaultScreenY;
            }
        }
    }

    void Update()
    {
        if (framingTransposer == null) return;

        xInput = Input.GetAxisRaw("Horizontal");
        yInput = Input.GetAxisRaw("Vertical");

        // 1. Detectar si estamos quietos y presionando arriba/abajo
        if (xInput == 0 && yInput != 0)
        {
            timer += Time.deltaTime;

            if (timer >= timeToTrigger)
            {
                // MIRAR ARRIBA: El personaje baja en la pantalla (ScreenY disminuye)
                if (yInput < 0)
                {
                    targetScreenY = defaultScreenY - lookOffsetAmount;
                }
                // MIRAR ABAJO: El personaje sube en la pantalla (ScreenY aumenta)
                else if (yInput > 0)
                {
                    targetScreenY = defaultScreenY + lookOffsetAmount;
                }
            }
        }
        else
        {
            // Resetear si nos movemos o soltamos
            timer = 0;
            targetScreenY = defaultScreenY;
        }

        // 2. Aplicar el cambio suavemente
        // Accedemos a Composition.ScreenPosition en Cinemachine 3.x
        Vector2 currentPos = framingTransposer.Composition.ScreenPosition;

        currentPos.y = Mathf.Lerp(currentPos.y, targetScreenY, smoothTime * Time.deltaTime);

        framingTransposer.Composition.ScreenPosition = currentPos;
    }
}