using UnityEngine;
using System.Collections;

public class ObjetoDestruible : MonoBehaviour
{
    [Header("Resistencia")]
    public int golpesParaRomper = 3;
    [Tooltip("Si es Neutro, se rompe con cualquier ataque. Si eliges otro, necesitas ese favor específico.")]
    public RomeritoCombat.GodFavor debilidadEspecifica = RomeritoCombat.GodFavor.Neutro;

    [Header("Feedback Visual")]
    public GameObject efectoDestruccion;
    public GameObject efectoGolpe;
    public SpriteRenderer spriteRenderer;

    private int golpesActuales;
    private bool esInvulnerable = false; // <--- EL CANDADO DE SEGURIDAD

    void Start()
    {
        golpesActuales = golpesParaRomper;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void RecibirGolpe(int damage, RomeritoCombat.GodFavor tipoAtaque)
    {
        // 1. SI YA FUE GOLPEADO HACE POCO, IGNORAMOS ESTE NUEVO GOLPE
        if (esInvulnerable) return;

        // 2. Validar debilidades (si aplica)
        if (debilidadEspecifica != RomeritoCombat.GodFavor.Neutro)
        {
            if (tipoAtaque != debilidadEspecifica)
            {
                Debug.Log("¡Ataque ineficaz!");
                return;
            }
        }

        // 3. Aplicar daño
        golpesActuales -= damage;
        Debug.Log($"¡PUM! Vida restante pared: {golpesActuales}");

        // 4. Activar candado de seguridad (Invencibilidad temporal)
        esInvulnerable = true;
        StartCoroutine(ResetInvencibilidad());

        // 5. Feedback Visual
        StartCoroutine(FlashFeedback());
        if (efectoGolpe != null)
            Instantiate(efectoGolpe, transform.position, Quaternion.identity);

        // 6. Verificar destrucción
        if (golpesActuales <= 0)
        {
            DestruirObjeto();
        }
    }

    // Rutina para quitar el candado después de un breve tiempo
    IEnumerator ResetInvencibilidad()
    {
        // 0.2 segundos es suficiente para evitar el "doble hit" del mismo ataque
        // pero lo bastante rápido para permitir combos rápidos del jugador.
        yield return new WaitForSeconds(0.2f);
        esInvulnerable = false;
    }

    void DestruirObjeto()
    {
        if (efectoDestruccion != null)
            Instantiate(efectoDestruccion, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }

    IEnumerator FlashFeedback()
    {
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            spriteRenderer.color = originalColor;
        }
    }
}