using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  EquipamientoCatalogo — Lista de piezas de la página Equipamiento
// ============================================================
//
//  La página se arma por secciones (Arma, Reliquias, Favores, Dones,
//  Compañeros) en el orden del enum CategoriaEquipo; dentro de cada
//  sección se respeta el orden de esta lista.
//
// ============================================================

[CreateAssetMenu(fileName = "EquipamientoCatalogo", menuName = "Mictlan/Equipamiento/Catálogo")]
public class EquipamientoCatalogo : ScriptableObject
{
    public List<EquipoDefinition> piezas = new List<EquipoDefinition>();
}
