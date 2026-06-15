using UnityEngine;

// ============================================================
//  Mictecah 1 — El Patrullero
// ============================================================
//
//  Comportamiento:
//    • Patrulla horizontal.
//    • Al toparse con PARED: se detiene un instante y gira.
//    • Al detectar PRECIPICIO: gira (no se cae).
//    • Retrocede brevemente al recibir daño.
//    • NO reacciona a la cercanía de Romerito (ignora al jugador).
//    • No choca con otros enemigos (Matriz de Colisiones Enemy×Enemy).
//
//  Es el enemigo más simple: toda su lógica ya vive en MictecahBase.
//  Solo desactivamos la percepción del jugador.
//
// ============================================================

public class Mictecah1_Patrullero : MictecahBase
{
    // No reacciona al jugador: nunca entra en persecución.
    protected override bool ReaccionaAlJugador => false;

    // Nunca se usa (jamás entra en Persiguiendo), pero es obligatorio
    // implementarlo por ser abstracto en la base.
    protected override void LogicaPersecucion()
    {
        // Por seguridad, si algo lo metiera aquí, vuelve a patrullar.
        estado = Estado.Patrullando;
    }
}