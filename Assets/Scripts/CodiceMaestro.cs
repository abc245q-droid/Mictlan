using UnityEngine;

// ============================================================
//  CodiceMaestro — Servicio del Códice Maestro (desbloqueo)
// ============================================================
//
//  El Códice Maestro es el amoxtli que el Pochtecah le REGALA a
//  Romerito en agradecimiento por liberarlo del cuauhcalli en el
//  Nivel 1 (EncuentroPochtecah). Es el libro en
//  blanco que se convertirá en el Códice de Romerito: al recibirlo
//  se desbloquea, de una sola vez, todo el menú de progreso
//  (CodiceUI): mapa, inventario, equipamiento, bestiario y herbario.
//
//  Referente: los códices mexicas eran contenedores de distintos
//  registros — mapas (Cuauhtinchan), listas de tributo (Matrícula
//  de Tributos), herbarios (de la Cruz-Badiano) y descripciones de
//  animales y plantas (Libro XI del Códice Florentino).
//
//  Servicio ESTÁTICO, espejo de MapManager: la verdad persistente
//  vive en PlayerData.tieneCodice (GameManager01.currentData).
//
//  Quién llama a esto:
//    • PochtecahNPC (regalaCodiceMaestro = true) → DarCodice().
//    • GameManager01.LoadGame()                  → MigrarSaveAntiguo().
//    • CodiceUI                                  → TieneCodice.
//
// ============================================================

public static class CodiceMaestro
{
    /// <summary>Se dispara una sola vez, cuando Romerito recibe el Códice.</summary>
    public static event System.Action OnCodiceObtenido;

    private static PlayerData D =>
        GameManager01.instance != null ? GameManager01.instance.currentData : null;

    /// <summary>¿Romerito ya tiene el Códice Maestro?</summary>
    public static bool TieneCodice
    {
        get
        {
            var d = D;
            return d != null && d.tieneCodice;
        }
    }

    /// <summary>
    /// Entrega el Códice Maestro. Devuelve true SOLO si es nuevo.
    /// No guarda en disco: el llamador decide cuándo (el Pochtecah
    /// guarda justo después del regalo).
    /// </summary>
    /// <param name="incluirEstuche">Si es true, también enciende el Estuche
    /// de Tlacuilo: el mapa empieza a dibujarse desde ya, y el Estuche
    /// del catálogo del Pochtecah aparecerá como "Ya lo tienes".</param>
    public static bool DarCodice(bool incluirEstuche)
    {
        var d = D;
        if (d == null || d.tieneCodice) return false;

        d.tieneCodice = true;
        Debug.Log("[Códice] Códice Maestro recibido. Menú de progreso desbloqueado.");

        // La sala donde se recibe el regalo cuenta de inmediato para el mapa.
        if (incluirEstuche)
            MapManager.DarEstucheDeTlacuilo(MapManager.SalaActual);

        OnCodiceObtenido?.Invoke();
        return true;
    }

    /// <summary>
    /// Saves escritos ANTES de que existiera tieneCodice: JsonUtility lo
    /// lee como false, y un jugador que ya iba más allá del Pochtecah se
    /// quedaría sin mapa. Para distinguir "false real" de "campo ausente"
    /// miramos el JSON crudo: si el campo ya existe, no se toca nada.
    ///
    /// Si el campo falta y el save muestra progreso (checkpoint tocado,
    /// Estuche o salas registradas), se concede el Códice sin escena.
    /// </summary>
    public static void MigrarSaveAntiguo(PlayerData d, string jsonOriginal)
    {
        if (d == null || d.tieneCodice) return;

        // Save moderno: el false es una decisión real del jugador.
        if (!string.IsNullOrEmpty(jsonOriginal) && jsonOriginal.Contains("\"tieneCodice\""))
            return;

        bool hayProgreso =
            d.tieneCheckpointGuardado ||
            d.tienePincel ||
            (d.salasVisitadas != null && d.salasVisitadas.Count > 0);

        if (!hayProgreso) return;

        d.tieneCodice = true;
        Debug.Log("[Códice] Save anterior al Códice Maestro con progreso: " +
                  "se concede el Códice para no dejarlo sin mapa.");
    }
}
