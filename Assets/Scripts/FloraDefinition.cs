using UnityEngine;

// ============================================================
//  FloraDefinition — Una especie del Herbario (ScriptableObject)
// ============================================================
//
//  Una sola definición alimenta todo lo que tenga que ver con una
//  planta: su entrada en el Herbario del Códice, la planta colocada
//  en el mundo (FloraEnMundo) y, más adelante, el enemigo, la
//  ofrenda o la receta del ticitl que la usen.
//
//  Crear en:  Create → Mictlan → Herbario → Planta
//  o generar el catálogo inicial con:
//             Mictlan → Herbario → Crear catálogo inicial
//
//  Referente visual del Herbario: el Códice de la Cruz-Badiano
//  (1552), primer herbario de América — nombre náhuatl, ilustración
//  con la raíz visible y uso medicinal.
//
// ============================================================

[System.Flags]
public enum RolFlora
{
    Ninguno       = 0,
    Decoracion    = 1 << 0,
    Coleccionable = 1 << 1,
    Ofrenda       = 1 << 2,
    Enemigo       = 1 << 3,
    Plataforma    = 1 << 4
}

[CreateAssetMenu(fileName = "flora_nueva", menuName = "Mictlan/Herbario/Planta")]
public class FloraDefinition : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("ID único y permanente: se guarda en el save. Minúsculas, sin espacios. " +
             "No cambiarlo después de publicar.")]
    public string id = "";

    [Tooltip("Nombre en náhuatl. Solo se muestra después de conocer al ticitl.")]
    public string nombreNahuatl = "";

    [Tooltip("Traducción o lectura del nombre (ej. 'flor del corazón').")]
    public string glosa = "";

    [Tooltip("Nombre común en español (ej. 'toloache').")]
    public string nombreEspanol = "";

    [Tooltip("Nombre científico. Se muestra pequeño, junto a la fuente.")]
    public string nombreCientifico = "";

    [Header("Entrada del Herbario")]
    [TextArea(2, 5)]
    [Tooltip("Descripción. Se muestra al recolectarla, si ya conoces al ticitl.")]
    public string descripcion = "";

    [TextArea(2, 4)]
    [Tooltip("Línea del ticitl. Se muestra al estudiarla (entregar suficientes muestras).")]
    public string notaTicitl = "";

    [Tooltip("Fuente histórica (Sahagún, Hernández, de la Cruz-Badiano...).")]
    public string fuente = "";

    [Tooltip("Ilustración estilo Cruz-Badiano. Al estar solo 'avistada' se dibuja como silueta.")]
    public Sprite ilustracion;

    [Header("Progreso")]
    [Min(1)]
    [Tooltip("Muestras que hay que entregar al ticitl para que quede 'estudiada'.")]
    public int muestrasParaEstudiar = 3;

    [Tooltip("Nivel donde aparece principalmente (-1 = varios niveles o por definir).")]
    public int nivelPrincipal = -1;

    [Tooltip("Qué papeles juega en el juego. Solo informativo por ahora.")]
    public RolFlora roles = RolFlora.Coleccionable;

    [Header("Diseño (no se muestra al jugador)")]
    [TextArea(2, 4)]
    [Tooltip("Notas para ti: datos por verificar, decisiones pendientes.")]
    public string notaDiseno = "";

    void OnValidate()
    {
        if (id != null) id = id.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}
