using UnityEngine;
using System.Collections;

public class ItemHabilidad : MonoBehaviour
{
    public enum HabilidadTipo
    {
        DoubleJump,
        Run,
        WallClimb,
        WallJump,
        Dash,
        Macuahuitl
    }

    [Header("Configuración")]
    public HabilidadTipo habilidadADesbloquear;
    public string nombreEnPantalla = "Objeto Clave";
    [TextArea] public string descripcion = "Descripción del objeto...";

    [Header("Efectos Visuales")]
    public GameObject pickUpEffect;
    public SpriteRenderer itemSprite;
    public Collider2D itemCollider;

    [Header("Reseteable")]
    [Tooltip("Si es verdadero, el objeto puede ser restaurado al altar (ej: sala del Macahuitl). " +
             "Si es falso, se destruye permanentemente al recogerlo como antes.")]
    public bool esReseteable = false;

    private bool recogido = false;

    // ── Trigger ──────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player") || recogido) return;

        recogido = true;

        // CASO 1: Es el arma
        if (habilidadADesbloquear == HabilidadTipo.Macuahuitl)
        {
            RomeritoCombat combate = collision.GetComponent<RomeritoCombat>();
            if (combate != null)
            {
                combate.EquiparMacuahuitl();
                Debug.Log("¡Romerito ha obtenido el Macuahuitl!");
            }
        }
        // CASO 2: Es una habilidad de movimiento
        else
        {
            RomeritoMovement movimiento = collision.GetComponent<RomeritoMovement>();
            if (movimiento != null)
            {
                string abilityString = habilidadADesbloquear switch
                {
                    HabilidadTipo.DoubleJump => "DoubleJump",
                    HabilidadTipo.Run => "Run",
                    HabilidadTipo.WallClimb => "WallClimb",
                    HabilidadTipo.WallJump => "WallJump",
                    HabilidadTipo.Dash => "Dash",
                    _ => ""
                };
                if (abilityString != "") movimiento.UnlockAbility(abilityString);
            }
        }

        // Feedback y ocultamiento
        if (pickUpEffect != null)
            Instantiate(pickUpEffect, transform.position, Quaternion.identity);

        StartCoroutine(SecuenciaObtencion());
    }

    // ── Corrutina de recogida ─────────────────────────────────

    IEnumerator SecuenciaObtencion()
    {
        if (itemSprite != null) itemSprite.enabled = false;
        if (itemCollider != null) itemCollider.enabled = false;

        yield return new WaitForSeconds(0.5f);

        if (esReseteable)
        {
            // ★ CAMBIO CLAVE: en lugar de destruir, dejamos el GameObject
            //   en escena pero desactivado. RestoreToAltar() lo reactiva.
            //   El sprite y el collider ya están apagados desde arriba.
            //   No hacemos nada más aquí.
        }
        else
        {
            // Comportamiento original para ítems de un solo uso (habilidades, etc.)
            Destroy(gameObject);
        }
    }

    // ── API Pública ───────────────────────────────────────────

    /// <summary>
    /// Llamado por MacahuitlPickup.RestoreToAltar() (o directamente por
    /// MacahuitlRoomManager) cuando la sala se resetea.
    /// Devuelve el objeto a su estado inicial sin moverlo de posición.
    /// </summary>
    public void Restaurar()
    {
        if (!esReseteable) return;  // Seguridad: solo resetear los que deben serlo

        recogido = false;

        if (itemSprite != null) itemSprite.enabled = true;
        if (itemCollider != null) itemCollider.enabled = true;

        Debug.Log($"[ItemHabilidad] '{nombreEnPantalla}' restaurado al altar.");
    }
}