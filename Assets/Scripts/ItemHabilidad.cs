using UnityEngine;
using System.Collections;

// ============================================================
//  ItemHabilidad — refactor Opción B
// ============================================================
//  CAMBIOS respecto a versión previa:
//  - Lógica del pickup extraída a Recoger(GameObject jugador) público.
//  - Nuevo flag pickupAutomatico (default TRUE) para no romper otros
//    pickups existentes. En el altar del Macahuitl debe estar FALSE
//    para que MacahuitlPickup controle el flujo vía "Presiona B".
//  - WaitForSeconds → WaitForSecondsRealtime en la corrutina, para
//    que el ocultamiento no se congele si el modal de tutorial pausa
//    el juego con Time.timeScale = 0 durante 0.5s.
// ============================================================

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

    [Header("Modo de Recogida")]
    [Tooltip("Si es verdadero (default), el objeto se recoge al contacto con el Player. " +
             "Si es falso, otro script (ej: MacahuitlPickup) debe llamar Recoger() manualmente.")]
    public bool pickupAutomatico = true;

    [Header("Reseteable")]
    [Tooltip("Si es verdadero, el objeto puede ser restaurado al altar (ej: sala del Macahuitl). " +
             "Si es falso, se destruye permanentemente al recogerlo como antes.")]
    public bool esReseteable = false;

    private bool recogido = false;

    public bool YaRecogido => recogido;

    // ── Trigger (solo si pickupAutomatico) ────────────────────

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (!pickupAutomatico) return;
        if (!collision.CompareTag("Player") || recogido) return;

        Recoger(collision.gameObject);
    }

    // ── API Pública: Recoger ──────────────────────────────────

    /// <summary>
    /// Ejecuta el pickup completo: aplica la habilidad al Player,
    /// instancia efectos y arranca la secuencia de ocultamiento.
    /// Llamado por OnTriggerEnter2D (pickup automático) o por otro
    /// script como MacahuitlPickup.Interactuar() (pickup manual).
    /// </summary>
    public void Recoger(GameObject jugador)
    {
        if (recogido || jugador == null) return;

        recogido = true;

        // CASO 1: Es el arma
        if (habilidadADesbloquear == HabilidadTipo.Macuahuitl)
        {
            RomeritoCombat combate = jugador.GetComponent<RomeritoCombat>();
            if (combate != null)
            {
                combate.EquiparMacuahuitl();
                Debug.Log("¡Romerito ha obtenido el Macuahuitl!");
            }
        }
        // CASO 2: Es una habilidad de movimiento
        else
        {
            RomeritoMovement movimiento = jugador.GetComponent<RomeritoMovement>();
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

        // Feedback visual
        if (pickUpEffect != null)
            Instantiate(pickUpEffect, transform.position, Quaternion.identity);

        StartCoroutine(SecuenciaObtencion());
    }

    // ── Corrutina de recogida ─────────────────────────────────

    IEnumerator SecuenciaObtencion()
    {
        if (itemSprite != null) itemSprite.enabled = false;
        if (itemCollider != null) itemCollider.enabled = false;

        // Realtime: no se congela con Time.timeScale = 0 (modal tutorial).
        yield return new WaitForSecondsRealtime(0.5f);

        if (esReseteable)
        {
            // No destruir: queda desactivado visualmente hasta que
            // RestoreToAltar() → Restaurar() lo restaure.
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── Restauración (llamada por MacahuitlPickup.RestoreToAltar) ──

    public void Restaurar()
    {
        if (!esReseteable) return;

        recogido = false;

        if (itemSprite != null) itemSprite.enabled = true;
        if (itemCollider != null) itemCollider.enabled = true;

        Debug.Log($"[ItemHabilidad] '{nombreEnPantalla}' restaurado al altar.");
    }
}