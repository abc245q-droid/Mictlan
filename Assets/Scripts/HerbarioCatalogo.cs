using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  HerbarioCatalogo — Lista ordenada de especies del Herbario
// ============================================================
//
//  Define QUÉ plantas existen y en qué orden aparecen en la página
//  del Herbario. El porcentaje de completado se calcula contra esta
//  lista, así que agregar una especie aquí la vuelve "faltante".
//
//  Crear en:  Create → Mictlan → Herbario → Catálogo
//  (el menú Mictlan → Herbario → Crear catálogo inicial lo genera).
//
// ============================================================

[CreateAssetMenu(fileName = "HerbarioCatalogo", menuName = "Mictlan/Herbario/Catálogo")]
public class HerbarioCatalogo : ScriptableObject
{
    [Tooltip("Especies en el orden en que aparecen en el Herbario.")]
    public List<FloraDefinition> especies = new List<FloraDefinition>();

    public int Total => especies != null ? especies.Count : 0;

    public FloraDefinition Buscar(string id)
    {
        if (especies == null || string.IsNullOrEmpty(id)) return null;
        foreach (var f in especies)
            if (f != null && f.id == id) return f;
        return null;
    }
}
