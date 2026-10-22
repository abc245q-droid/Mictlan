using UnityEngine;

// ============================================================
//  EquipamientoEstado — ¿Qué tiene Romerito ahora mismo?
// ============================================================
//
//  Lee el estado EN VIVO de los componentes del jugador:
//    • RomeritoMovement → unlockRun, unlockDoubleJump, unlockDash,
//                         unlockWallClimb, unlockWallJump
//    • RomeritoCombat   → tieneMacuahuitl, unlockHuehueteotl/Tlaloc/
//                         Tepeyollotl, currentFavor
//  y de PlayerData lo que solo vive ahí (tieneDonDeTlacua, tieneXolo).
//
//  Por qué no solo PlayerData: GameManager01 copia los componentes
//  a PlayerData únicamente al guardar (GatherDataFromPlayer). Un
//  Favor recién obtenido no aparecería hasta el siguiente Cihuacalli.
//  Si no hay jugador en escena (menú), se usa PlayerData como respaldo.
//
// ============================================================

public static class EquipamientoEstado
{
    private static PlayerData D =>
        GameManager01.instance != null ? GameManager01.instance.currentData : null;

    private static void Jugador(out RomeritoMovement mov, out RomeritoCombat combate)
    {
        mov = null; combate = null;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        mov = p.GetComponent<RomeritoMovement>();
        combate = p.GetComponent<RomeritoCombat>();
    }

    public static bool Tiene(RequisitoEquipo req)
    {
        Jugador(out var mov, out var combate);
        var d = D;

        switch (req)
        {
            case RequisitoEquipo.Siempre:          return true;
            case RequisitoEquipo.PorDefinir:       return false;

            case RequisitoEquipo.Macahuitl:
                return combate != null ? combate.tieneMacuahuitl : d != null && d.tieneMacuahuitl;
            case RequisitoEquipo.FavorHuehueteotl:
                return combate != null ? combate.unlockHuehueteotl : d != null && d.unlockHuehueteotl;
            case RequisitoEquipo.FavorTlaloc:
                return combate != null ? combate.unlockTlaloc : d != null && d.unlockTlaloc;
            case RequisitoEquipo.FavorTepeyollotl:
                return combate != null ? combate.unlockTepeyollotl : d != null && d.unlockTepeyollotl;

            case RequisitoEquipo.Correr:
                return mov != null ? mov.unlockRun : d != null && d.unlockRun;
            case RequisitoEquipo.DobleSalto:
                return mov != null ? mov.unlockDoubleJump : d != null && d.unlockDoubleJump;
            case RequisitoEquipo.Dash:
                return mov != null ? mov.unlockDash : d != null && d.unlockDash;
            case RequisitoEquipo.TreparMuros:
                return mov != null ? mov.unlockWallClimb : d != null && d.unlockWallClimb;
            case RequisitoEquipo.SaltoPared:
                return mov != null ? mov.unlockWallJump : d != null && d.unlockWallJump;

            case RequisitoEquipo.JicaraTonalli:    return d != null && d.tieneDonDeTlacua;
            case RequisitoEquipo.Xolo:             return d != null && d.tieneXolo;
        }
        return false;
    }

    public static bool Tiene(EquipoDefinition e)
    {
        return e != null && Tiene(e.requisito);
    }

    /// <summary>¿Es el Favor que Romerito tiene seleccionado ahora?</summary>
    public static bool EsFavorActivo(EquipoDefinition e)
    {
        if (e == null || e.categoria != CategoriaEquipo.Favor) return false;
        Jugador(out _, out var combate);
        return combate != null && combate.currentFavor == e.favor && Tiene(e);
    }

    /// <summary>
    /// Línea de estado del detalle: "Equipado", "Favor activo", la
    /// capacidad de la Jícara, etc. Vacío si no aplica.
    /// </summary>
    public static string LineaEstado(EquipoDefinition e)
    {
        if (e == null || !Tiene(e)) return "";

        switch (e.requisito)
        {
            case RequisitoEquipo.JicaraTonalli:
                if (TonalliSystem.Instance != null)
                {
                    float max = TonalliSystem.Instance.MaxTonalli;
                    float bonus = TonalliSystem.Instance.BonusCapacidadPct * 100f;
                    return bonus > 0.5f
                        ? "Capacidad: " + Mathf.RoundToInt(max) + " de tonalli (+" + Mathf.RoundToInt(bonus) + "%)"
                        : "Capacidad: " + Mathf.RoundToInt(max) + " de tonalli";
                }
                return "Equipada";

            case RequisitoEquipo.Macahuitl:
                return "Equipado";
        }

        if (e.categoria == CategoriaEquipo.Favor)
            return EsFavorActivo(e) ? "Favor activo" : "Disponible: cámbialo con LB";

        return "";
    }
}
