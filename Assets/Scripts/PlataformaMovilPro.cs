using UnityEngine;
using System.Collections.Generic;

public class PlataformaMovilPro : MonoBehaviour
{
    [Header("Ruta")]
    public Transform[] waypoints;
    public float velocidad = 3f;
    public float tiempoEspera = 1f;

    private int indiceActual = 0;
    private float timer;
    private bool esperando;

    private Rigidbody2D rbPlataforma;

    // Usamos un Dictionary para guardar referencia directa al script del pasajero
    private Dictionary<Transform, RomeritoMovement> pasajeros = new Dictionary<Transform, RomeritoMovement>();

    void Start()
    {
        rbPlataforma = GetComponent<Rigidbody2D>();
        rbPlataforma.bodyType = RigidbodyType2D.Kinematic;

        if (waypoints.Length > 0)
        {
            foreach (Transform t in waypoints)
            {
                t.SetParent(null);
            }
            transform.position = waypoints[0].position;
        }
    }

    void FixedUpdate()
    {
        if (waypoints.Length == 0) return;

        // 1. Lógica de movimiento de la plataforma
        Vector2 velocidadActual = Vector2.zero;

        if (esperando)
        {
            timer -= Time.fixedDeltaTime;
            if (timer <= 0)
            {
                esperando = false;
                indiceActual = (indiceActual + 1) % waypoints.Length;
            }
        }
        else
        {
            Transform destino = waypoints[indiceActual];
            Vector2 nuevaPosicion = Vector2.MoveTowards(rbPlataforma.position, destino.position, velocidad * Time.fixedDeltaTime);

            // Calculamos el DELTA y la VELOCIDAD real de este frame
            Vector2 delta = nuevaPosicion - rbPlataforma.position;
            velocidadActual = delta / Time.fixedDeltaTime;

            rbPlataforma.MovePosition(nuevaPosicion);

            if (Vector2.Distance(rbPlataforma.position, destino.position) < 0.05f)
            {
                esperando = true;
                timer = tiempoEspera;
                velocidadActual = Vector2.zero; // Al llegar, paramos
            }
        }

        // 2. INYECTAR VELOCIDAD A LOS PASAJEROS
        foreach (var par in pasajeros)
        {
            RomeritoMovement romerito = par.Value;
            if (romerito != null)
            {
                // Le pasamos la velocidad para que su script la sume
                romerito.platformVelocity = velocidadActual;
            }
        }
    }

    // --- DETECCIÓN INTELIGENTE ---

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Verificamos que esté ARRIBA de la plataforma
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y < -0.5f)
                {
                    RomeritoMovement script = collision.gameObject.GetComponent<RomeritoMovement>();
                    if (script != null && !pasajeros.ContainsKey(collision.transform))
                    {
                        pasajeros.Add(collision.transform, script);
                    }
                    break;
                }
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (pasajeros.ContainsKey(collision.transform))
            {
                // IMPORTANTE: Antes de sacarlo, reseteamos su velocidad externa a 0
                // para que no siga "impulsado" eternamente.
                pasajeros[collision.transform].platformVelocity = Vector2.zero;
                pasajeros.Remove(collision.transform);
            }
        }
    }
}