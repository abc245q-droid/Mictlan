using UnityEngine;
using UnityEngine.Events;
using System.Collections;

// ============================================================
//  AltarHuehueteotl — Entrega del primer Favor de los Dioses
// ============================================================
//
//  Momento (GDD, Chicunamictlán): al encender el ÚLTIMO brasero
//  del grupo, Huehueteotl — el Dios Viejo, el fuego que precede
//  a todos los dioses — habla con Romerito y le otorga su Favor.
//  El fuego es el único calor del Mictlán; que el primer poder
//  divino nazca de encender braseros es el sistema entero
//  diciéndose a sí mismo en voz alta.
//
//  FLUJO:
//    1. BraseroGrupo.OnGrupoCompletado → EntregarFavor()  [cablear]
//    2. Pausa breve (que el glow del último brasero respire)
//    3. Conversación con Huehueteotl (DialogueManager)
//    4. Al terminar → OtorgarFavor():
//         • unlockHuehueteotl en PlayerData + RomeritoCombat
//         • Guarda partida
//         • Equipa el favor de inmediato (la barra de Tonalli
//           arde en rojo-fuego: el jugador VE lo que ganó)
//         • Efecto visual + evento OnFavorOtorgado
//
//  SETUP EN UNITY:
//    1. GameObject vacío "AltarHuehueteotl" en la sala del grupo
//       de braseros, con este componente.
//    2. Asignar 'conversacion' (asset Conversation de Huehueteotl).
//    3. En el BraseroGrupo, Inspector:
//         OnGrupoCompletado  → AltarHuehueteotl.EntregarFavor
//         OnYaCompletadoAlCargar → NADA de este script (el favor ya
//         se restauró desde el save; no repetir el momento).
//    4. (Opcional) efectoFavor: partículas de fuego sobre Romerito.
//
// ============================================================

public class AltarHuehueteotl : MonoBehaviour
{
    [Header("Diálogo")]
    [Tooltip("Asset Conversation con las palabras de Huehueteotl.")]
    public Conversation conversacion;

    [Tooltip("Pausa (s) entre el último brasero encendido y el inicio del diálogo. " +
             "Deja que el momento visual del grupo completado respire.")]
    public float retrasoAntesDeDialogo = 1.2f;

    [Header("Feedback Visual (opcional)")]
    [Tooltip("Efecto de fuego/partículas que aparece sobre Romerito al recibir el favor.")]
    public GameObject efectoFavor;
    public float duracionEfecto = 2.5f;

    [Header("Evento al otorgar el favor")]
    [Tooltip("Se dispara tras otorgar el favor: abrir un paso, animar el altar, música, etc.")]
    public UnityEvent OnFavorOtorgado;

    // ── Estado interno ───────────────────────────────────────
    private bool yaOtorgado = false;

    void Start()
    {
        // Si ya lo tiene (cargó partida), este altar ya cumplió su función.
        if (YaTieneFavor()) yaOtorgado = true;
    }

    // ─────────────────────────────────────────────────────────
    //  API PÚBLICA — cablear a BraseroGrupo.OnGrupoCompletado
    // ─────────────────────────────────────────────────────────

    public void EntregarFavor()
    {
        if (yaOtorgado || YaTieneFavor())
        {
            yaOtorgado = true;
            return;
        }

        yaOtorgado = true; // marcar YA — evita dobles disparos del evento
        StartCoroutine(RutinaEntrega());
    }

    private IEnumerator RutinaEntrega()
    {
        yield return new WaitForSeconds(retrasoAntesDeDialogo);

        // Si otro diálogo está corriendo (Chantico, lore…), esperar a que termine.
        while (DialogueManager.IsActive) yield return null;

        if (conversacion != null && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartConversation(conversacion, OtorgarFavor);
        }
        else
        {
            if (conversacion == null)
                Debug.LogWarning("[AltarHuehueteotl] Sin Conversation asignada — " +
                                 "se otorga el favor sin diálogo.");
            OtorgarFavor();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  OTORGAR — llamado al terminar el diálogo
    // ─────────────────────────────────────────────────────────

    private void OtorgarFavor()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        RomeritoCombat combat = player != null ? player.GetComponent<RomeritoCombat>() : null;

        // 1. Desbloquear en el componente vivo…
        if (combat != null)
        {
            combat.UnlockFavor("Huehueteotl");

            // …y equiparlo de inmediato: el jugador VE la barra arder.
            // (FavorManager detecta el cambio y tiñe el HUD.)
            combat.CambiarFavor(RomeritoCombat.GodFavor.Huehueteotl);
        }

        // 2. …y en los datos persistentes.
        if (GameManager01.instance != null)
        {
            GameManager01.instance.currentData.unlockHuehueteotl = true;
            GameManager01.instance.SaveGame();
            Debug.Log("[AltarHuehueteotl] 🔥 Favor de Huehueteotl otorgado y guardado.");
        }
        else
        {
            Debug.LogWarning("[AltarHuehueteotl] No hay GameManager — el favor no se guardó.");
        }

        // 3. Efecto visual sobre Romerito.
        if (efectoFavor != null)
        {
            Vector3 pos = player != null ? player.transform.position : transform.position;
            GameObject fx = Instantiate(efectoFavor, pos, Quaternion.identity);
            Destroy(fx, duracionEfecto);
        }

        // 4. Evento externo (abrir paso, animar altar, cue musical…).
        OnFavorOtorgado?.Invoke();
    }

    // ── Utilidades ───────────────────────────────────────────

    private bool YaTieneFavor()
    {
        return GameManager01.instance != null &&
               GameManager01.instance.currentData.unlockHuehueteotl;
    }
}
