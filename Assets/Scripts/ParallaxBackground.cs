using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Header("Configuración Básica")]
    public GameObject cam;
    [Range(0f, 1f)] // Slider para hacerlo más fácil de ajustar
    public float parallaxEffect; // 0 = Estático (fondo pegado), 1 = Se mueve con la cámara (frente)

    [Header("Control de Repetición (Loop)")]
    [Tooltip("Actívalo para cielo/nubes. Desactívalo para cuartos/paredes específicas.")]
    public bool infiniteHorizontal = true;
    public bool infiniteVertical = true;

    private float lengthX;
    private float lengthY;
    private float startposX;
    private float startposY;

    // Guardamos la posición inicial de la cámara para calcular el offset relativo
    private float startCamX;
    private float startCamY;

    void Start()
    {
        if (cam == null) cam = Camera.main.gameObject; // Autodetectar cámara si se te olvida

        startposX = transform.position.x;
        startposY = transform.position.y;

        startCamX = cam.transform.position.x;
        startCamY = cam.transform.position.y;

        // Obtenemos el tamaño del sprite para saber cuándo repetir
        if (GetComponent<SpriteRenderer>() != null)
        {
            lengthX = GetComponent<SpriteRenderer>().bounds.size.x;
            lengthY = GetComponent<SpriteRenderer>().bounds.size.y;
        }
    }

    void LateUpdate()
    {
        // --- CÁLCULOS MATEMÁTICOS ---
        // Distancia que la cámara se ha movido desde el inicio
        float distMovedX = cam.transform.position.x - startCamX;
        float distMovedY = cam.transform.position.y - startCamY;

        // Cuánto debe moverse el fondo (Distancia * Efecto)
        float parallaxX = distMovedX * parallaxEffect;
        float parallaxY = distMovedY * parallaxEffect;

        // La posición "temporal" es lo que sobra del movimiento (para el loop infinito)
        float tempX = cam.transform.position.x * (1 - parallaxEffect);
        float tempY = cam.transform.position.y * (1 - parallaxEffect);

        // --- APLICAR MOVIMIENTO ---
        // Movemos el objeto desde su posición inicial + el efecto parallax calculado
        transform.position = new Vector3(startposX + parallaxX, startposY + parallaxY, transform.position.z);

        // --- LÓGICA DE REPETICIÓN (LOOP) ---

        // Solo repetimos si la casilla "infiniteHorizontal" está marcada
        if (infiniteHorizontal)
        {
            // Nota: Aquí usamos la posición de la cámara (tempX) para recalcular el startPos
            if (tempX > startposX + lengthX) startposX += lengthX;
            else if (tempX < startposX - lengthX) startposX -= lengthX;
        }

        // Lo mismo para vertical (útil para torres o pozos)
        if (infiniteVertical)
        {
            if (tempY > startposY + lengthY) startposY += lengthY;
            else if (tempY < startposY - lengthY) startposY -= lengthY;
        }
    }

    // --- OPTIMIZACIÓN ---

    // Unity llama a esto automáticamente cuando la cámara ve el Sprite
    void OnBecameVisible()
    {
        enabled = true; // Reactiva el Update/LateUpdate
    }

    // Unity llama a esto cuando el Sprite sale totalmente de la pantalla
    void OnBecameInvisible()
    {
        enabled = false; // Apaga el script para que deje de moverse
    }

}