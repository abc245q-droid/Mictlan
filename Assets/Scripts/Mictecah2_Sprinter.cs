using UnityEngine;

// ============================================================
//  Mictecah 2 — El Sprinter (Embestida comprometida)
// ============================================================
//
//  Comportamiento:
//    • Patrulla normal (pared + precipicio).
//    • Al detectar a Romerito: arranca un SPRINT en línea recta hacia él.
//    • Una vez iniciado el sprint, NO cambia de decisión hasta terminarlo
//      → por eso se le puede esquivar SALTANDO por encima.
//    • Respeta paredes y precipicios: si choca con uno, frena el sprint.
//    • Tras el sprint hace una breve recuperación y re-evalúa.
//    • Solo el DAÑO interrumpe un sprint (lo maneja la base: estado Herido).
//    • Retrocede brevemente al recibir daño.
//
// ============================================================

public class Mictecah2_Sprinter : MictecahBase
{
    [Header("── Sprint (Mictecah 2) ──")]
    [Tooltip("Velocidad de la embestida. Bastante mayor que patrolSpeed.")]
    public float sprintSpeed = 10f;
    [Tooltip("Duración de la embestida. Define qué tan lejos llega.")]
    public float sprintDuration = 0.6f;
    [Tooltip("Pausa de recuperación tras la embestida (jadeo).")]
    public float sprintRecovery = 0.5f;
    // [TELEGRAPH] Pausa de aviso antes de la embestida.
    [Tooltip("Pausa (s) en que el Sprinter mira a Romerito antes de lanzar el sprint. " +
             "Da tiempo al jugador de reaccionar y esquivar.")]
    public float sprintTelegraphDuration = 0.35f;

    private enum Fase { Telegrafio, Embistiendo, Recuperando }
    private Fase fase;
    private float faseTimer;

    // Arranca el sprint en el instante en que detecta al jugador.
    protected override void IniciarPersecucion()
    {
        estado = Estado.Persiguiendo;
        EntrarTelegrafo(); // [TELEGRAPH] Pausa de aviso antes del primer sprint
    }

    // [TELEGRAPH] Pone al Sprinter en pausa de aviso: quieto, mirando a Romerito.
    // Cuando el timer expira, LogicaPersecucion lanza ArrancarSprint.
    private void EntrarTelegrafo()
    {
        MirarHacia(DireccionAlJugador()); // Se gira hacia el jugador
        fase = Fase.Telegrafio;
        faseTimer = sprintTelegraphDuration;
        controlVelocidad = true;
        targetVelocity = Vector2.zero; // Se detiene
    }

    private void ArrancarSprint()
    {
        // Bloquea la dirección hacia el jugador AL INICIO (compromiso total).
        MirarHacia(DireccionAlJugador());
        fase = Fase.Embistiendo;
        faseTimer = sprintDuration;
        controlVelocidad = true;
        targetVelocity = new Vector2(facing * sprintSpeed, 0f);
    }

    protected override void LogicaPersecucion()
    {
        faseTimer -= Time.deltaTime;

        // [TELEGRAPH] Durante el aviso: quieto, sigue mirando al jugador
        if (fase == Fase.Telegrafio)
        {
            MirarHacia(DireccionAlJugador());
            targetVelocity = Vector2.zero;
            if (faseTimer <= 0f)
                ArrancarSprint();
            return;
        }

        if (fase == Fase.Embistiendo)
        {
            // Mantiene la embestida en la dirección fijada (NO recalcula al jugador).
            targetVelocity = new Vector2(facing * sprintSpeed, 0f);

            // Frena si se topa con pared o con el borde de la plataforma.
            bool obstaculo = HayParedAdelante() || HayPrecipicioAdelante();
            if (obstaculo || faseTimer <= 0f)
                EntrarRecuperacion();
        }
        else // Recuperando
        {
            targetVelocity = Vector2.zero;
            if (faseTimer <= 0f)
            {
                // Re-evalúa: si Romerito sigue en rango, vuelve a embestir.
                if (DistanciaAlJugador() < detectionRange)
                    EntrarTelegrafo(); // [TELEGRAPH] Siempre avisa antes del siguiente sprint
                else
                    estado = Estado.Patrullando;
            }
        }
    }

    private void EntrarRecuperacion()
    {
        fase = Fase.Recuperando;
        faseTimer = sprintRecovery;
        targetVelocity = Vector2.zero;
    }
}