using UnityEngine;

// ============================================================
//  Mictecah 3 — El Saltarín (Torpe y lento)
// ============================================================
//
//  Comportamiento:
//    • Patrulla normal (pared + precipicio).
//    • Al detectar a Romerito: lo persigue dando SALTOS torpes y lentos.
//    • Entre salto y salto hay una pausa larga (sensación de torpeza).
//    • IGNORA precipicios durante la persecución (los salta).
//    • Sigue respetando paredes (un salto no atraviesa un muro sólido,
//      pero puede saltar por encima de obstáculos bajos según tu nivel).
//    • Retrocede brevemente al recibir daño.
//
//  Técnica: durante el salto soltamos el control de velocidad
//  (controlVelocidad = false) para que el salto sea BALÍSTICO
//  (arco natural). Al aterrizar, recuperamos el control.
//
// ============================================================

public class Mictecah3_Saltarin : MictecahBase
{
    [Header("── Salto (Mictecah 3) ──")]
    [Tooltip("Empuje horizontal de cada salto hacia el jugador.")]
    public float jumpHorizontal = 4f;
    [Tooltip("Empuje vertical de cada salto.")]
    public float jumpVertical = 8f;
    [Tooltip("Pausa en el suelo entre saltos. Súbelo para que sea más torpe/lento.")]
    public float jumpCooldown = 1.2f;
    [Tooltip("Pequeña pausa al aterrizar antes de poder volver a saltar.")]
    public float landRecovery = 0.25f;

    private float jumpTimer;
    private bool enElAire;

    protected override void IniciarPersecucion()
    {
        estado = Estado.Persiguiendo;
        jumpTimer = jumpCooldown * 0.5f;  // reacción inicial algo más rápida
        enElAire = !EstaEnSuelo;
    }

    protected override void LogicaPersecucion()
    {
        // ¿Salió de rango? Volver a patrullar (solo si está en el suelo).
        if (DistanciaAlJugador() > stopChaseRange && EstaEnSuelo)
        {
            controlVelocidad = true;
            targetVelocity = Vector2.zero;
            estado = Estado.Patrullando;
            return;
        }

        // --- En el aire: salto balístico, no tocamos la velocidad ---
        if (!EstaEnSuelo)
        {
            enElAire = true;
            return;
        }

       
        // --- Acaba de aterrizar ---
        // Chequeamos también que la velocidad vertical sea casi cero:
        // esto evita detectar un "aterrizaje" en el mismo frame que ejecutamos el salto.
        if (enElAire && rb.linearVelocity.y <= 0.5f)
        {
            enElAire = false;
            jumpTimer = jumpCooldown + landRecovery;
            controlVelocidad = true;
            targetVelocity = Vector2.zero;
        }

        // --- En el suelo: quieto y mirando al jugador, esperando el siguiente salto ---
        MirarHacia(DireccionAlJugador());
        targetVelocity = Vector2.zero;

        jumpTimer -= Time.deltaTime;
        if (jumpTimer <= 0f)
            Saltar();
    }

    private void Saltar()
    {
        int dir = DireccionAlJugador();
        MirarHacia(dir);

        // Salto balístico: fijamos velocidad y soltamos el control para que vuele.
        controlVelocidad = false;
        rb.linearVelocity = new Vector2(dir * jumpHorizontal, jumpVertical);
        enElAire = true;
    }

    // Si lo golpean en pleno salto, la base lo pasa a Herido.
    // Al recuperarse forzamos un re-chequeo limpio en suelo.
    protected override void OnHerido()
    {
        enElAire = false;
        jumpTimer = jumpCooldown;
    }
}