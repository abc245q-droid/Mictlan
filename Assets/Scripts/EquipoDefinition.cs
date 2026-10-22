using UnityEngine;

// ============================================================
//  EquipoDefinition — Un objeto, Favor, don o compañero
// ============================================================
//
//  Una entrada de la página de Equipamiento del Códice. No guarda
//  progreso: si Romerito "lo tiene" se lee en vivo del estado del
//  juego a través de 'requisito' (ver EquipamientoEstado).
//
//  Crear en:  Create → Mictlan → Equipamiento → Equipo
//  o generar el catálogo inicial con:
//             Mictlan → Equipamiento → Crear catálogo inicial
//
// ============================================================

// Valores explícitos: se serializan como número en los assets. El 3 era
// "Don" (eliminado: la reliquia es la que permite usar el don). No reusar
// el 3 ni renumerar, o los assets existentes cambiarían de categoría.
public enum CategoriaEquipo
{
    Arma      = 0,
    Reliquia  = 1,
    Favor     = 2,
    Companero = 4
}

/// <summary>
/// Qué flag del juego decide si Romerito ya tiene la pieza. Las
/// habilidades de movimiento (Correr, Dash...) se usan como requisito de
/// la RELIQUIA que concede ese don, no como pieza propia. Para añadir
/// algo nuevo (un Tecpatl, Ehécatl...), agrega el valor aquí y su lectura
/// en EquipamientoEstado.Tiene().
/// </summary>
public enum RequisitoEquipo
{
    Siempre,
    Macahuitl,
    JicaraTonalli,
    FavorHuehueteotl,
    FavorTlaloc,
    FavorTepeyollotl,
    Correr,
    DobleSalto,
    Dash,
    TreparMuros,
    SaltoPared,
    Xolo,
    PorDefinir      // aún no existe en el juego: se muestra como "???"
}

[CreateAssetMenu(fileName = "equipo_nuevo", menuName = "Mictlan/Equipamiento/Equipo")]
public class EquipoDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string id = "";
    public string nombre = "";
    [Tooltip("Línea bajo el nombre: origen o epíteto (ej. 'Don de Tlacua').")]
    public string subtitulo = "";
    public Sprite icono;

    [Header("Clasificación")]
    public CategoriaEquipo categoria = CategoriaEquipo.Arma;
    public RequisitoEquipo requisito = RequisitoEquipo.Siempre;

    [Tooltip("Solo Favores: a qué favor de RomeritoCombat corresponde, para marcarlo como activo.")]
    public RomeritoCombat.GodFavor favor = RomeritoCombat.GodFavor.Neutro;

    [Header("Texto")]
    [TextArea(2, 5)] public string descripcion = "";
    [TextArea(1, 3)]
    [Tooltip("Cómo se usa (botones). Vacío = no se muestra.")]
    public string controles = "";

    [Header("Diseño (no se muestra al jugador)")]
    [TextArea(1, 3)] public string notaDiseno = "";

    void OnValidate()
    {
        if (id != null) id = id.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
