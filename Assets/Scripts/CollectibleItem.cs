using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    public enum CurrencyType { Cacao, Tajadera }

    [Header("Configuración del Item")]
    public CurrencyType type;
    public int value = 1;

    [Header("Física de Caída")]
    public float initialJumpForce = 5f;

    [Header("Efectos Visuales")]
    public float floatSpeed = 2f;
    public float floatAmplitude = 0.1f;
    public float hoverHeight = 0.3f; // <--- NUEVO: Altura base para que no toque el piso
    public GameObject pickupEffect;

    // Variables internas
    private Vector3 startPos;
    private bool isGrounded = false;
    private Rigidbody2D rb;
    private Collider2D col;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();

        // Impulso inicial
        float randomX = Random.Range(-1f, 1f);
        rb.linearVelocity = new Vector2(randomX, initialJumpForce);
    }

    void Update()
    {
        if (isGrounded)
        {
            // FÓRMULA CORREGIDA:
            // Base (Suelo) + Altura de Seguridad + Onda Senoidal
            float newY = startPos.y + hoverHeight + (Mathf.Sin(Time.time * floatSpeed) * floatAmplitude);

            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    // CASO 1: La moneda está cayendo (es SÓLIDA)
    // Detecta el suelo Y al jugador si la atrapa en el aire
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Si choca con Romerito en el aire
        if (collision.gameObject.CompareTag("Player"))
        {
            Collect(collision.gameObject);
        }
        // Si choca con el suelo
        else if (!isGrounded)
        {
            // Verificamos que el choque sea desde abajo (suelo) y no una pared
            if (collision.contacts[0].normal.y > 0.5f)
            {
                LandOnGround();
            }
        }
    }

    // CASO 2: La moneda ya aterrizó (es TRIGGER/FANTASMA)
    // Detecta a Romerito cuando pasa caminando sobre ella
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
        col.isTrigger = true;
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