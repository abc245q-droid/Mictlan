using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  MapManager — El Códice de Romerito (servicio del mapa)
// ============================================================
//
//  Servicio ESTÁTICO: no es un MonoBehaviour ni un objeto en escena.
//  Toda la verdad persistente vive en GameManager01.currentData (PlayerData),
//  serializada con JsonUtility. Así no hay dos copias que se puedan
//  desincronizar (JsonUtility NO serializa HashSet, por eso usamos List).
//
//  Los tres estados del mapa (ver GDD §03):
//    • NoVisitada — amate en blanco.
//    • Borrador   — visitada, memoria fresca (Tonalli). Aún no asentada.
//    • Asentada   — registrada al fuego: tinta + color permanente.
//
//  CONVENCIÓN DE roomId (importante):
//    El id de cada sala empieza con el prefijo de su nivel: "L{n}_".
//    Ejemplos: "L0_entrada", "L0_pozo", "L1_rio_norte".
//    El commit al fuego filtra por ese prefijo para asentar solo las
//    salas del nivel cuyo papel (amate) ya se compró.
//
// ============================================================

public enum EstadoSala { NoVisitada, Borrador, Asentada }

public enum Pigmento { Rojo, Amarillo, Azul, Verde }

public static class MapManager
{
    /// <summary>Se dispara cuando cambia algo del mapa (sala nueva, commit,
    /// compra de papel). La UI del códice se suscribe para refrescarse.</summary>
    public static event System.Action OnMapaActualizado;

    // Atajo a los datos vivos. Null-safe: si no hay GameManager, no hace nada.
    private static PlayerData D =>
        GameManager01.instance != null ? GameManager01.instance.currentData : null;

    // ── Tracking de exploración — Estado 2: Borrador de Tonalli ──
    /// <summary>Llamado al entrar a una sala (desde RoomConfiner).</summary>
    public static void OnRoomEntered(string roomId)
    {
        var d = D;
        if (d == null || string.IsNullOrEmpty(roomId)) return;

        // Filosofía Atlein (GDD §08): sin el Estuche de Tlacuilo, la memoria
        // está apagada. Ni siquiera se genera Borrador hasta tener el pincel.
        if (!d.tienePincel) return;

        if (!d.salasVisitadas.Contains(roomId))
        {
            d.salasVisitadas.Add(roomId);
            Debug.Log("[Mapa] Borrador nuevo: " + roomId);
            OnMapaActualizado?.Invoke();
        }
    }

    // ── Commit al sentarse al fuego — Estado 2 → 3: Asentado ──
    /// <summary>Llamado al encender/usar cualquier Cihuacali. Asienta toda sala
    /// visitada cuyo nivel (leído de su prefijo "L{n}_") ya tenga amate comprado.
    /// El Cihuacali no necesita saber a qué nivel pertenece.</summary>
    public static void OnCihuacaliRest()
    {
        var d = D;
        if (d == null) return;

        int asentadas = 0;
        foreach (var id in d.salasVisitadas)
        {
            if (d.salasAsentadas.Contains(id)) continue;

            int nivel = NivelDeSala(id);
            if (nivel < 0) continue;                       // id mal formado, se ignora
            if (!d.papelComprado.Contains(nivel)) continue; // sin amate de su nivel → sigue en Borrador

            d.salasAsentadas.Add(id);
            asentadas++;
        }

        if (asentadas > 0)
        {
            Debug.Log("[Mapa] Asentadas " + asentadas + " sala(s) a la luz del fuego.");
            OnMapaActualizado?.Invoke();
        }
    }

    /// <summary>Extrae el nivel de un roomId con formato "L{n}_...". Devuelve -1 si no cumple.</summary>
    public static int NivelDeSala(string roomId)
    {
        if (string.IsNullOrEmpty(roomId) || roomId[0] != 'L') return -1;
        int guion = roomId.IndexOf('_');
        if (guion < 2) return -1;                          // necesita ≥1 dígito entre 'L' y '_'
        string num = roomId.Substring(1, guion - 1);
        return int.TryParse(num, out int n) ? n : -1;
    }

    // ── Consulta de estado — la usará el render del mapa (rebanada 3) ──
    public static EstadoSala GetEstado(string roomId)
    {
        var d = D;
        if (d == null || string.IsNullOrEmpty(roomId)) return EstadoSala.NoVisitada;
        if (d.salasAsentadas.Contains(roomId)) return EstadoSala.Asentada;
        if (d.salasVisitadas.Contains(roomId)) return EstadoSala.Borrador;
        return EstadoSala.NoVisitada;
    }

    public static bool TienePapel(int levelId)
    {
        var d = D;
        return d != null && d.papelComprado.Contains(levelId);
    }

    // ── API de adquisición — la llamará el Pochtecah (rebanada 2) ──

    /// <summary>Estuche de Tlacuilo: enciende la capacidad de mapear (compra única).
    /// Marca también la sala actual como visitada, para que el lugar donde
    /// se compró el estuche cuente de inmediato.</summary>
    public static void DarEstucheDeTlacuilo(string salaActualId = null)
    {
        var d = D;
        if (d == null || d.tienePincel) return;

        d.tienePincel = true;
        Debug.Log("[Mapa] Estuche de Tlacuilo obtenido. La memoria de Romerito despierta.");

        if (!string.IsNullOrEmpty(salaActualId) && !d.salasVisitadas.Contains(salaActualId))
            d.salasVisitadas.Add(salaActualId);

        OnMapaActualizado?.Invoke();
    }

    /// <summary>Compra del amate de un nivel (una vez por nivel).</summary>
    public static void ComprarPapel(int levelId)
    {
        var d = D;
        if (d == null || d.papelComprado.Contains(levelId)) return;

        d.papelComprado.Add(levelId);
        Debug.Log("[Mapa] Amate del nivel " + levelId + " adquirido.");
        OnMapaActualizado?.Invoke();
    }

    // ── Pigmentos: cada color enciende una categoría de marcador (GDD §05) ──
    public static void DarPigmento(Pigmento p)
    {
        var d = D;
        if (d == null) return;

        bool nuevo = false;
        switch (p)
        {
            case Pigmento.Rojo: if (!d.tieneRojo) { d.tieneRojo = true; nuevo = true; } break;
            case Pigmento.Amarillo: if (!d.tieneAmarillo) { d.tieneAmarillo = true; nuevo = true; } break;
            case Pigmento.Azul: if (!d.tieneAzul) { d.tieneAzul = true; nuevo = true; } break;
            case Pigmento.Verde: if (!d.tieneVerde) { d.tieneVerde = true; nuevo = true; } break;
        }

        if (nuevo)
        {
            Debug.Log("[Mapa] Pigmento obtenido: " + p);
            OnMapaActualizado?.Invoke();
        }
    }

    public static bool TienePigmento(Pigmento p)
    {
        var d = D;
        if (d == null) return false;
        switch (p)
        {
            case Pigmento.Rojo: return d.tieneRojo;
            case Pigmento.Amarillo: return d.tieneAmarillo;
            case Pigmento.Azul: return d.tieneAzul;
            case Pigmento.Verde: return d.tieneVerde;
        }
        return false;
    }

    public static bool TieneEstuche()
    {
        var d = D;
        return d != null && d.tienePincel;
    }

}
