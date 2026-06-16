using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    public enum CurrencyType { Cacao, Tajadera }

    [Header("Configuraci�n del Item")]
    public CurrencyType type;
    public int value = 1;

    [Header("F�sica de Ca�da")]
    public float initialJumpForce = 5f;

    [Header("Efectos Visuales")]
    public float floatSpeed = 2f;
    public float floatAmplitude = 0.1f;
    public float hoverHeight = 0.3f; // <--- NUEVO: Altura base para que no toque el piso
    public GameObject pickupEffect;

    [Header("Colliders (FIX Pendiente #2)")]
    [Tooltip("Collider S�LIDO: solo debe colisionar con el suelo. " +
             "En el Inspector, en la secci�n 'Layer Overrides' de este collider, " +
             "configura 'Exclude Layers' = capa del Jugador (Default). " +
             "As� nunca empuja a Romerito, ni en el aire ni en el suelo.")]
    public Collider2D groundCollider;
    [Tooltip("Collider TRIGGER: detecta al jugador para recolectar el �tem, " +
             "tanto en el aire como ya aterrizado. No necesita Layer Overrides.")]
    public Collider2D pickupTrigger;

    // Variables internas
    private Vector3 startPos;
    private bool isGrounded = false;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Impulso inicial
        float randomX = Random.Range(-1f, 1f);
        rb.linearVelocity = new Vector2(randomX, initialJumpForce);
    }

    void Update()
    {
        if (isGrounded)
        {
            // F�RMULA CORREGIDA:
            // Base (Suelo) + Altura de Seguridad + Onda Senoidal
            float newY = startPos.y + hoverHeight + (Mathf.Sin(Time.time * floatSpeed) * floatAmplitude);

            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    // FIX: este callback ya SOLO puede dispararse contra el suelo.
    //   groundCollider tiene excluida la capa del Jugador (Exclude Layers),
    //   as� que Romerito nunca genera respuesta f�sica aqu�,
    //   sin importar si la semilla est� cayendo o ya aterriz�.
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isGrounded)
        {
            // Verificamos que el choque sea desde abajo (suelo) y no una pared
            if (collision.contacts[0].normal.y > 0.5f)
            {
                LandOnGround();
            }
        }
    }

    // FIX: pickupTrigger es SIEMPRE trigger (en el aire y en el suelo),
    //   as� que la recolecci�n nunca genera impulso f�sico sobre Romerito.
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            Collect(collision.gameObject);
        }
    }

    void LandOnGround()
    {
        isGrounded = true;
        startPos = transform.position; // Guardamos el punto exacto de impacto en el suelo

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        // Ya no se cambia isTrigger aqu�: groundCollider nunca interact�a
        // con el Jugador (Exclude Layers) y pickupTrigger siempre fue trigger.
    }

    void Collect(GameObject player)
    {
        Monedero inventory = player.GetComponent<Monedero>();

        if (inventory != null)
        {
            if (type == CurrencyType.Cacao) inventory.AddCacao(value);
            else if (type == CurrencyType.Tajadera) inventory.AddTajadera(value);

            if (pickupEffect != null) Instantiate(pickupEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}