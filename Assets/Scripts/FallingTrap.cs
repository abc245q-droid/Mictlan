using UnityEngine;
using System.Collections; // Necesario para IEnumerator

public class FallingTrap : MonoBehaviour
{
    [Header("Detección")]
    public float detectionRange = 5f;
    public float detectionWidth = 0.8f;
    public float rayOffset = 0.5f;
    public LayerMask playerLayer;

    [Header("Feedback de Advertencia")]
    public float shakeDuration = 0.5f;   // Tiempo que tiembla antes de caer
    public float shakeIntensity = 0.05f; // Qué tan fuerte se mueve (0.05 es sutil, 0.1 es fuerte)

    [Header("Física")]
    public float gravityScale = 4f;

    private Rigidbody2D rb;
    private bool isActivated = false; // Para que no se active dos veces
    private Vector3 initialPos;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; // Quieto al inicio
        initialPos = transform.position;
    }

    void Update()
    {
        // Si ya se activó, dejamos de buscar
        if (isActivated) return;

        // 1. Definir área de detección
        Vector2 origin = (Vector2)transform.position + (Vector2.down * rayOffset);
        Vector2 boxCenter = origin + (Vector2.down * (detectionRange / 2f));
        Vector2 boxSize = new Vector2(detectionWidth, detectionRange);

        // 2. Escanear área
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0, playerLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                // En vez de caer directo, iniciamos la secuencia
                StartCoroutine(ShakeAndFall());
                break;
            }
        }
    }

    // --- LA SECUENCIA DE CAÍDA ---
    IEnumerator ShakeAndFall()
    {
        isActivated = true; // Bloqueamos para no volver a entrar aquí

        float elapsed = 0f;

        // FASE 1: TEMBLOR
        while (elapsed < shakeDuration)
        {
            // Movemos la posición aleatoriamente alrededor del punto inicial
            float x = initialPos.x + Random.Range(-shakeIntensity, shakeIntensity);
            float y = initialPos.y + Random.Range(-shakeIntensity, shakeIntensity);

            transform.position = new Vector3(x, y, initialPos.z);

            elapsed += Time.deltaTime;
            yield return null; // Esperar al siguiente frame
        }

        // Aseguramos que vuelva al centro antes de soltarla (opcional, pero se ve mejor)
        transform.position = initialPos;

        // FASE 2: CAÍDA FÍSICA
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = gravityScale;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground") ||
            collision.gameObject.layer == LayerMask.NameToLayer("Suelo") ||
            collision.gameObject.CompareTag("Ground"))
        {
            // InstanciarParticulas(transform.position); // Aquí irían tus partículas
            Destroy(gameObject, 0.1f);
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            RomeritoHealth hp = collision.gameObject.GetComponent<RomeritoHealth>();
            if (hp != null) hp.TakeDamage(1);

            Destroy(gameObject, 0.1f);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isActivated ? Color.red : Color.yellow; // Cambia a rojo si ya se activó
        Vector3 origin = transform.position + (Vector3.down * rayOffset);
        Gizmos.DrawWireCube(origin + (Vector3.down * (detectionRange / 2)), new Vector3(detectionWidth, detectionRange, 0));
    }
}