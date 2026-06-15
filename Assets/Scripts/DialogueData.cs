using UnityEngine;

// ============================================================
//  DialogueData — Estructuras de datos del sistema de Lore
// ============================================================
//
//  Sistema para conversaciones IMPORTANTES de Lore: dioses,
//  emisarios, aliados/enemigos relevantes. (Los NPCs comunes
//  y el Pochtecah usarán OTRO sistema, más adelante.)
//
//  Formato: ScriptableObject → todas las líneas quedan PRECARGADAS
//  como assets editables desde el Inspector, sin tocar código.
//
//  Modelo de nodos (soporta árboles de decisión):
//    • Conversation → lista de Node. El Node 0 es SIEMPRE el inicio.
//    • Node → una secuencia de Line + (opcional) una lista de Choice.
//        - Si el nodo NO tiene choices → es lineal: salta a nextNode
//          (o termina si nextNode = -1).
//        - Si el nodo TIENE choices → al acabar las líneas se muestran
//          las opciones; cada una salta al nodo indicado en targetNode.
//
//  La mayoría de las conversaciones serán UN SOLO nodo lineal.
//  Las ramificaciones (raras) solo necesitan añadir más nodos.
//
// ============================================================

[System.Serializable]
public class DialogueLine
{
    [Tooltip("Nombre que se muestra (ej. MICTLANTECUHTLI). Vacío si es acotación.")]
    public string speaker;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("Color del NOMBRE del hablante. Útil para diferenciar a cada dios.")]
    public Color speakerColor = Color.white;

    [Tooltip("Si es una acotación escénica [entre corchetes]: se muestra en " +
             "cursiva, atenuada y sin nombre de hablante.")]
    public bool esAcotacion = false;

    [Header("Retrato (opcional — plantilla de personajes)")]
    [Tooltip("Si se asigna un Personaje, su nombre, color y retrato tienen prioridad " +
             "sobre 'speaker' y 'speakerColor'.")]
    public Personaje personaje;

    [Tooltip("Clave de expresión a usar del Personaje (ej. 'curioso', 'frio'). " +
             "Vacío = retrato neutral.")]
    public string expresion = "";

    [Tooltip("Si está activo, oculta los retratos en esta línea (corte o transición).")]
    public bool limpiarRetratos = false;
}

[System.Serializable]
public class DialogueChoice
{
    [TextArea(1, 3)]
    public string text;

    [Tooltip("Índice del nodo al que salta esta respuesta. -1 = termina la conversación.")]
    public int targetNode = -1;
}

[System.Serializable]
public class DialogueNode
{
    [Tooltip("Nombre interno solo para organizarte (no se muestra al jugador).")]
    public string nodeName = "Nodo";

    public DialogueLine[] lines;

    [Tooltip("Opciones de respuesta. Si está VACÍO, el nodo es lineal y usa 'nextNode'.")]
    public DialogueChoice[] choices;

    [Tooltip("Solo se usa si NO hay choices. Índice del siguiente nodo. -1 = fin.")]
    public int nextNode = -1;
}

[CreateAssetMenu(fileName = "NuevaConversacion", menuName = "Mictlan/Conversación de Lore")]
public class Conversation : ScriptableObject
{
    [TextArea(1, 3)]
    [Tooltip("Nota para ti: a quién pertenece y dónde ocurre. No se muestra al jugador.")]
    public string descripcion;

    [Tooltip("Lista de nodos. El nodo en la posición 0 es SIEMPRE el inicio.")]
    public DialogueNode[] nodes;
}
