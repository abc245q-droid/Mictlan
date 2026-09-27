using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PausaMundo — Pausa del mundo por conteo de motivos
// ============================================================
//
//  Varios sistemas necesitan congelar el mundo (enemigos, físicas,
//  proyectiles) y pueden solaparse:
//    • Diálogo con NPC  → al terminar abre la tienda del Pochtecah
//    • Diálogo de Tlacua → al terminar abre el modal de la Jícara
//    • Menú de pausa abierto encima de un modal de tutorial
//  Si cada uno escribiera Time.timeScale a mano, el primero en
//  terminar reanudaría el mundo aunque el otro siguiera abierto.
//
//  Aquí cada sistema pide la pausa con SU motivo y la libera al
//  terminar. Time.timeScale = 0 mientras quede al menos un motivo.
//
//  USO:
//    PausaMundo.Solicitar(PausaMundo.Dialogo);
//    PausaMundo.Liberar(PausaMundo.Dialogo);
//    if (PausaMundo.Activa) ...
//
//  Lo que deba seguir vivo durante la pausa (typewriter, fades de UI,
//  animaciones de NPCs) debe usar Time.unscaledDeltaTime /
//  WaitForSecondsRealtime / Animator en modo "Unscaled Time".
// ============================================================

public static class PausaMundo
{
    // Motivos conocidos — constantes para no escribir strings sueltos.
    public const string Dialogo       = "Dialogo";
    public const string Tienda        = "Tienda";
    public const string ModalTutorial = "ModalTutorial";
    public const string MenuPausa     = "MenuPausa";

    private static readonly HashSet<string> motivos = new HashSet<string>();

    /// <summary>
    /// Frame en que el mundo se reanudó por última vez (el último motivo
    /// se liberó). Sirve para descartar el botón que cerró la UI: A es
    /// Submit y también Jump.
    /// </summary>
    public static int FrameReanudacion { get; private set; } = -1;

    /// <summary>True si hay al menos un motivo de pausa activo.</summary>
    public static bool Activa => motivos.Count > 0;

    public static void Solicitar(string motivo)
    {
        if (motivos.Add(motivo)) Aplicar();
    }

    public static void Liberar(string motivo)
    {
        if (!motivos.Remove(motivo)) return;
        if (motivos.Count == 0) FrameReanudacion = Time.frameCount;
        Aplicar();
    }

    /// <summary>
    /// Borra todos los motivos y reanuda. Solo para desmontar la sesión
    /// (volver al menú) — nunca para cerrar un panel concreto.
    /// </summary>
    public static void Resetear()
    {
        motivos.Clear();
        Time.timeScale = 1f;
    }

    private static void Aplicar()
    {
        Time.timeScale = motivos.Count > 0 ? 0f : 1f;
    }

    // Con "Enter Play Mode Options" (sin recarga de dominio) los estáticos
    // sobreviven entre Plays: limpiamos al arrancar.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarAlArrancar()
    {
        motivos.Clear();
        FrameReanudacion = -1;
    }
}
