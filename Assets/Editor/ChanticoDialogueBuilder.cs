#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// ============================================================
//  ChanticoDialogueBuilder — genera el asset de diálogo
// ============================================================
//
//  Crea Assets/Dialogues/Conversacion_Chantico.asset listo para
//  arrastrar al campo 'dialogoChantico' del primer Cihuacalli.
//
//  USO: menú  Mictlan > Diálogos > Crear Diálogo de Chantico
//
//  Usa 'speaker' + 'speakerColor' (sin Personaje). Si más adelante
//  haces un retrato/Personaje de Chantico, puedes asignarlo a mano
//  en cada línea desde el Inspector del asset.
//
//  Este archivo DEBE vivir en una carpeta llamada "Editor".

public static class ChanticoDialogueBuilder
{
    private const string CARPETA = "Assets/Mictlan/Dialogs/Conversations";
    private const string RUTA = CARPETA + "/Conversacion_Chantico.asset";

    [MenuItem("Mictlan/Diálogos/Crear Diálogo de Chantico")]
    public static void Crear()
    {
        // Crea la cadena de carpetas si hiciera falta.
        EnsureFolder(CARPETA);

        // Si ya existe, lo seleccionamos y avisamos (no lo pisamos).
        var existente = AssetDatabase.LoadAssetAtPath<Conversation>(RUTA);
        if (existente != null)
        {
            Selection.activeObject = existente;
            EditorGUIUtility.PingObject(existente);
            Debug.LogWarning("[ChanticoDialogueBuilder] Ya existe " + RUTA +
                             ". Bórralo manualmente si quieres regenerarlo.");
            return;
        }

        Conversation convo = ScriptableObject.CreateInstance<Conversation>();
        convo.descripcion = "Chantico otorga el permiso de usar las Cihuacalli (primer checkpoint).";

        Color fuego = new Color(0.95f, 0.45f, 0.15f);   // ámbar/fuego para el nombre
        Color aco = new Color(0.6f, 0.65f, 0.7f);       // acotaciones (frío)

        DialogueLine Acot(string t) =>
            new DialogueLine { text = t, esAcotacion = true, speakerColor = aco };
        DialogueLine Chan(string t) =>
            new DialogueLine { speaker = "CHANTICO", text = t, speakerColor = fuego };

        var nodo = new DialogueNode
        {
            nodeName = "Chantico - Permiso Cihuacalli",
            nextNode = -1,
            choices = new DialogueChoice[0],
            lines = new DialogueLine[]
            {
                Acot("El fuego dormido del Cihuacalli se agita. De las brasas frías brota la silueta de una mujer coronada de espinas, envuelta en una serpiente roja."),
                Chan("Detente, pequeño portador. Hueles a sol... a algo cálido en este reino de huesos y silencio."),
                Chan("Pocos descienden con fuego propio. Tú lo guardas en el pecho como una brasa que se niega a rendirse."),
                Chan("Soy Chantico, la que mora en la casa, señora del fogón y del fuego que no se apaga. Velo por lo que mi viejo Huehuetéotl encendió antes de que el mundo tuviera nombre."),
                Chan("Has honrado su calor aquí abajo, en la oscuridad. Eso no pasa inadvertido para quien cuida la lumbre."),
                Acot("La serpiente roja se desliza hacia el Cihuacalli, y la piedra fría empieza a respirar un rescoldo anaranjado."),
                Chan("Escucha bien. Estas son las Cihuacalli, las casas donde el tonalli disperso puede reunirse de nuevo. Sin mi permiso, no son más que piedra muerta."),
                Chan("Te lo concedo. Donde encuentres una de estas casas y la enciendas, tu tonalli tendrá adónde volver si la muerte te deshace."),
                Chan("No es perdón, ni descanso eterno, axólotl. Es una tregua. Úsala con cabeza."),
                Acot("Chantico se repliega entre las brasas. El Cihuacalli queda encendido, latiendo con un calor tenue y constante."),
                Chan("Anda. El fuego que llevas todavía tiene camino por recorrer."),
            }
        };

        convo.nodes = new DialogueNode[] { nodo };

        AssetDatabase.CreateAsset(convo, RUTA);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = convo;
        EditorGUIUtility.PingObject(convo);
        Debug.Log("[ChanticoDialogueBuilder] Diálogo creado en " + RUTA);
    }

    // Crea recursivamente una ruta de carpetas tipo "Assets/A/B/C".
    private static void EnsureFolder(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = System.IO.Path.GetDirectoryName(ruta).Replace("\\", "/");
        string nombre = System.IO.Path.GetFileName(ruta);
        EnsureFolder(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }
}
#endif