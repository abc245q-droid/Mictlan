using UnityEngine;

// ============================================================
//  Conversation — Asset de ScriptableObject para el sistema de Lore
//  IMPORTANTE: esta clase DEBE vivir en un archivo llamado Conversation.cs
//  Unity resuelve los MonoScript por nombre de archivo, no por namespace.
// ============================================================

[CreateAssetMenu(fileName = "NuevaConversacion", menuName = "Mictlan/Conversación de Lore")]
public class Conversation : ScriptableObject
{
    [TextArea(1, 3)]
    [Tooltip("Nota para ti: a quién pertenece y dónde ocurre. No se muestra al jugador.")]
    public string descripcion;

    [Tooltip("Lista de nodos. El nodo en la posición 0 es SIEMPRE el inicio.")]
    public DialogueNode[] nodes;
}