using UnityEngine;
using System.Collections;

public class PlataformaInestable : MonoBehaviour
{
    [Header("Configuración")]
    public float tiempoAntesDeCaer = 0.5f; // Cuánto tarda en caer tras pisarla
    public float tiempoRespawn = 3f;       // Cuánto tarda en volver
    public float velocidadCaida = 2f;      // Gravedad artificial

    [Header("Feedback Visual")]
    public float intensidadTemblor = 0.05f; // Qué tan fuerte tiembla

    private Vector3 posInicial;
    private Rigidbody2D rb;
    private Collider2D col;
    private bool cayendo = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        posInicial = transform.position;

        // Aseguramos configuración inicial
        rb.bodyType = RigidbodyType2D.Kinematic; // Quieta
        rb.linearVelocity = Vector2.zero;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Solo activamos si el jugador la pisa desde ARRIBA y no está cayendo ya
        if (!cayendo && collision.gameObject.CompareTag("Player"))
        {
            // Verificamos contacto superior (Normal hacia abajo)
            foreach (ContactPoint2D contacto in collision.contacts)
            {
                if (contacto.normal.y < -0.5f)
                {
                    StartCoroutine(SecuenciaCaida());
                    break;
                }
            }
        }
    }

    IEnumerator SecuenciaCaida()
    {
        cayendo = true;
        float timer = 0f;

        // 1. FASE DE TEMBLOR (Advertencia)
        while (timer < tiempoAntesDeCaer)
        {
            timer += Time.deltaTime;
            // Movemos la plataforma aleatoriamente muy rápido
            float x = posInicial.x + Random.Range(-intensidadTemblor, intensidadTemblor);
            transform.position = new Vector3(x, posInicial.y, posInicial.z);
            yield return null; // Esperar al siguiente frame
        }

        // 2. FASE DE CAÍDA
        // Desactivamos el collider para que el jugador y la plataforma caigan
        // Opcional: Si quieres que la plataforma caiga CON el jugador encima, no desactives el collider inmediatamente.
        // Pero lo clásico es que la plataforma se vuelva "fantasma" o caiga físicamente.

        // Vamos a hacer que caiga físicamente:
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = velocidadCaida;

        // Desactivamos colisiones con el jugador para que no lo "aplaste" si el jugador cae debajo
        // Physics2D.IgnoreCollision(col, jugadorCollider, true); -> (Opcional avanzado)

        yield return new WaitForSeconds(2f); // Esperar a que salga de pantalla

        // 3. FASE DE APAGADO (Invisible)
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        GetComponent<SpriteRenderer>().enabled = false;
        col.enabled = false;

        yield return new WaitForSeconds(tiempoRespawn);

        // 4. RESPAWN (Reaparecer)
        transform.position = posInicial;
        GetComponent<SpriteRenderer>().enabled = true;
        col.enabled = true;
        cayendo = false;
    }
}