using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  HerbarioManager — Servicio del Herbario (estático)
// ============================================================
//
//  Espejo de MapManager / CodiceMaestro: no es un MonoBehaviour.
//  La verdad persistente vive en PlayerData (GameManager01.currentData):
//    • herbario       → List<EntradaHerbario> (JsonUtility no serializa
//                       Dictionary, por eso es lista)
//    • conocioTicitl  → si ya se conoció al ticitl de Apanohualoyan
//
//  ETAPAS POR PLANTA
//    Desconocida → nunca vista. Hueco vacío en el Herbario.
//    Avistada    → Romerito pasó cerca. Silueta.
//    Recolectada → tiene (o tuvo) al menos una muestra. Ilustración.
//                  El nombre náhuatl solo aparece si ya conoce al ticitl:
//                  el ticitl es quien le enseña a nombrar las plantas.
//    Estudiada   → entregó al ticitl las muestras necesarias. Nota del ticitl.
//
//  MUESTRAS
//    Las muestras son la moneda del ticitl, separada del cacao. Se
//    recolectan en el mundo (FloraEnMundo) y se gastan en el ticitl
//    (estudio o recetas).
//
//  GUARDADO
//    Ninguna llamada guarda en disco: el progreso se escribe con el
//    siguiente guardado normal (Cihuacalli), igual que las mazorcas.
//
// ============================================================

public enum EtapaHerbario
{
    Desconocida = 0,
    Avistada    = 1,
    Recolectada = 2,
    Estudiada   = 3
}

[System.Serializable]
public class EntradaHerbario
{
    public string id;
    public EtapaHerbario etapa;
    public int muestras;            // en posesión (moneda del ticitl)
    public int muestrasEntregadas;  // acumuladas para el estudio
}

public static class HerbarioManager
{
    /// <summary>Cualquier cambio (etapa, muestras, ticitl). La UI se refresca con esto.</summary>
    public static event System.Action OnHerbarioActualizado;

    /// <summary>Una planta subió de etapa. Útil para avisos ("Nueva planta").</summary>
    public static event System.Action<FloraDefinition, EtapaHerbario> OnEtapaNueva;

    private static PlayerData D =>
        GameManager01.instance != null ? GameManager01.instance.currentData : null;

    // ── Consultas ────────────────────────────────────────────

    public static bool ConocioTicitl
    {
        get { var d = D; return d != null && d.conocioTicitl; }
    }

    public static EtapaHerbario GetEtapa(FloraDefinition f)
    {
        var e = Buscar(f, crear: false);
        return e != null ? e.etapa : EtapaHerbario.Desconocida;
    }

    public static int Muestras(FloraDefinition f)
    {
        var e = Buscar(f, crear: false);
        return e != null ? e.muestras : 0;
    }

    public static int MuestrasEntregadas(FloraDefinition f)
    {
        var e = Buscar(f, crear: false);
        return e != null ? e.muestrasEntregadas : 0;
    }

    /// <summary>Muestras que faltan entregar al ticitl para estudiarla.</summary>
    public static int FaltanParaEstudio(FloraDefinition f)
    {
        if (f == null) return 0;
        if (GetEtapa(f) == EtapaHerbario.Estudiada) return 0;
        return Mathf.Max(0, f.muestrasParaEstudiar - MuestrasEntregadas(f));
    }

    /// <summary>¿Se puede mostrar el nombre náhuatl, la glosa y la descripción?</summary>
    public static bool NombreRevelado(FloraDefinition f)
    {
        return ConocioTicitl && GetEtapa(f) >= EtapaHerbario.Recolectada;
    }

    /// <summary>Cuántas especies del catálogo están en 'minimo' o más.</summary>
    public static int Contar(HerbarioCatalogo catalogo, EtapaHerbario minimo)
    {
        if (catalogo == null || catalogo.especies == null) return 0;
        int n = 0;
        foreach (var f in catalogo.especies)
            if (f != null && GetEtapa(f) >= minimo) n++;
        return n;
    }

    // ── Progreso ─────────────────────────────────────────────

    /// <summary>Romerito vio la planta. Devuelve true solo la primera vez.</summary>
    public static bool Avistar(FloraDefinition f)
    {
        var e = Buscar(f, crear: true);
        if (e == null || e.etapa >= EtapaHerbario.Avistada) return false;
        SubirEtapa(e, f, EtapaHerbario.Avistada);
        OnHerbarioActualizado?.Invoke();
        return true;
    }

    /// <summary>Suma muestras. La primera la deja como 'Recolectada'.</summary>
    public static void Recolectar(FloraDefinition f, int cantidad = 1)
    {
        var e = Buscar(f, crear: true);
        if (e == null || cantidad <= 0) return;

        e.muestras += cantidad;
        if (e.etapa < EtapaHerbario.Recolectada)
            SubirEtapa(e, f, EtapaHerbario.Recolectada);

        OnHerbarioActualizado?.Invoke();
    }

    /// <summary>Gasta muestras (recetas del ticitl). False si no alcanzan.</summary>
    public static bool GastarMuestras(FloraDefinition f, int cantidad)
    {
        var e = Buscar(f, crear: false);
        if (e == null || cantidad <= 0 || e.muestras < cantidad) return false;
        e.muestras -= cantidad;
        OnHerbarioActualizado?.Invoke();
        return true;
    }

    /// <summary>
    /// Entrega muestras al ticitl para estudiarla. Entrega como máximo
    /// las que faltan. Devuelve cuántas entregó. Al completar, la planta
    /// queda 'Estudiada'.
    /// </summary>
    public static int EntregarParaEstudio(FloraDefinition f, int cantidad)
    {
        var e = Buscar(f, crear: false);
        if (e == null || cantidad <= 0) return 0;

        int faltan = FaltanParaEstudio(f);
        int entrega = Mathf.Min(cantidad, faltan, e.muestras);
        if (entrega <= 0) return 0;

        e.muestras -= entrega;
        e.muestrasEntregadas += entrega;

        if (e.muestrasEntregadas >= f.muestrasParaEstudiar)
            SubirEtapa(e, f, EtapaHerbario.Estudiada);

        OnHerbarioActualizado?.Invoke();
        return entrega;
    }

    /// <summary>
    /// Primer encuentro con el ticitl: a partir de aquí se revelan los
    /// nombres de todo lo que Romerito ya recolectó.
    /// </summary>
    public static void ConocerTicitl()
    {
        var d = D;
        if (d == null || d.conocioTicitl) return;
        d.conocioTicitl = true;
        Debug.Log("[Herbario] Romerito conoció al ticitl: ya puede nombrar las plantas.");
        OnHerbarioActualizado?.Invoke();
    }

    // ── Internos ─────────────────────────────────────────────

    private static void SubirEtapa(EntradaHerbario e, FloraDefinition f, EtapaHerbario nueva)
    {
        if (nueva <= e.etapa) return;
        e.etapa = nueva;
        Debug.Log("[Herbario] " + f.id + " → " + nueva);
        OnEtapaNueva?.Invoke(f, nueva);
    }

    private static EntradaHerbario Buscar(FloraDefinition f, bool crear)
    {
        var d = D;
        if (d == null || f == null) return null;
        if (string.IsNullOrEmpty(f.id))
        {
            Debug.LogWarning("[Herbario] '" + f.name + "' no tiene id: no se puede registrar.");
            return null;
        }

        // Saves anteriores al Herbario: el campo puede llegar nulo.
        if (d.herbario == null) d.herbario = new List<EntradaHerbario>();

        foreach (var e in d.herbario)
            if (e != null && e.id == f.id) return e;

        if (!crear) return null;
        var nueva = new EntradaHerbario { id = f.id, etapa = EtapaHerbario.Desconocida };
        d.herbario.Add(nueva);
        return nueva;
    }

    // ── DEV ──────────────────────────────────────────────────

    /// <summary>Borra todo el progreso del Herbario (solo para pruebas).</summary>
    public static void DevReiniciar()
    {
        var d = D;
        if (d == null) return;
        d.herbario = new List<EntradaHerbario>();
        d.conocioTicitl = false;
        OnHerbarioActualizado?.Invoke();
    }
}
