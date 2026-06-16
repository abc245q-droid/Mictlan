using UnityEngine;
using System.Collections;

// ============================================================
//  TonalliOrb — Efecto visual: tonalli materializado volando
//  hacia Romerito para ser "absorbido".
// ============================================================
//
//  USO: se instancia desde EnemyDummy (al golpear o matar), en la
//  posición del enemigo. Es puramente cosmético — el Tonalli real
//  ya se otorgó al instante vía TonalliSystem.GanarTonalli(). Este
//  script solo da el feedback visual de esa ganancia.
//
//  ENCAJE NARRATIVO: representa visualmente el Don de Tlacua
//  (la capacidad de materializar y almacenar tonalli, propio y de
//  enemigos). Por eso EnemyDummy solo lo invoca cuando Romerito
//  YA tiene el don — antes de eso, el tonalli no es "visible".
//
//  FASES:
//    1) Impulso — pequeño salto hacia afuera en dirección aleatoria,
//       igual de espíritu al lootScatter del cacao (evita que todos
//       los orbes salgan apilados exactamente en el mismo punto).
//    2) Persecución — vuela hacia Romerito acelerando (efecto
//       "magnético"), y se autodestruye al llegar lo bastante cerca.
//
//  NO usa Rigidbody2D ni Collider2D: es control manual de transform,
//  así no interactúa físicamente con nada (paredes, enemigos, etc.).
//
// ============================================================

public class TonalliOrb : MonoBehaviour
{
    private enum Fase { Impulso, Persiguiendo }

    [Header("Fase 1 — Impulso inicial (como el cacao al dropear)")]
    [Tooltip("Distancia que recorre el impulso inicial hacia afuera, en dirección aleatoria.")]
    public float distanciaImpulso = 0.35f;
    [Tooltip("Segundos que dura el impulso antes de empezar a perseguir a Romerito.")]
    public float duracionImpulso = 0.12f;

    [Header("Fase 2 — Persecución hacia Romerito")]
    [Tooltip("Velocidad inicial de persecución (unidades/segundo).")]
    public float velocidadInicial = 3f;
    [Tooltip("Cuánto aumenta la velocidad de persecución por segundo (efecto 'magnético').")]
    public float aceleracion = 14f;
    [Tooltip("Distancia a la que se considera absorbido y el orbe se destruye.")]
    public float distanciaAbsorcion = 0.2f;

    [Header("Seguridad")]
    [Tooltip("Tiempo máximo de vida por si Romerito no está en escena (cinemáticas, etc.).")]
    public float tiempoMaximoVida = 4f;

    private Transform jugador;
    private Vector3 puntoImpulso;
    private float velocidadActual;
    private float tiempoVivo;
    private Fase faseActual = Fase.Impulso;

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) jugador = player.transform;

        Vector2 direccionAleatoria = Random.insideUnitCircle.normalized;
        puntoImpulso = transform.position + (Vector3)(direccionAleatoria * distanciaImpulso);

        velocidadActual = velocidadInicial;
        StartCoroutine(TerminarImpulso());
    }

    private IEnumerator TerminarImpulso()
    {
        yield return new WaitForSeconds(duracionImpulso);
        faseActual = Fase.Persiguiendo;
    }

    void Update()
    {
        tiempoVivo += Time.deltaTime;
        if (tiempoVivo > tiempoMaximoVida)
        {
            Destroy(gameObject);
            return;
        }

        if (faseActual == Fase.Impulso)
        {
            float velocidadImpulso = distanciaImpulso / Mathf.Max(duracionImpulso, 0.01f);
            transform.position = Vector3.MoveTowards(transform.position, puntoImpulso, velocidadImpulso * Time.deltaTime);
            return;
        }

        // Fase 2: persecución acelerada hacia Romerito
        if (jugador == null) return;

        velocidadActual += aceleracion * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, jugador.position, velocidadActual * Time.deltaTime);

        if (Vector3.Distance(transform.position, jugador.position) <= distanciaAbsorcion)
        {
            Destroy(gameObject);
        }
    }
}