using UnityEngine;

// ============================================================
//  Personaje — Plantilla de retrato para el sistema de Lore
// ============================================================
//
//  Un asset por personaje importante (dios, emisario, aliado/enemigo
//  relevante). Centraliza su identidad visual: nombre, color del
//  nombre, retrato base y expresiones.
//
//  Ventaja: defines al personaje UNA vez y lo reutilizas en todas
//  sus líneas. Si cambias su retrato o color, se actualiza en todas
//  las conversaciones a la vez.
//
//  CÓMO CREARLO:
//    Click derecho en el Project ▸ Create ▸ Mictlan ▸ Personaje (Retrato)
//    Rellena nombre, color y arrastra el sprite del retrato.
//
//  EXPRESIONES (opcional):
//    Añade entradas con una 'clave' (ej. "curioso", "frio", "interesado")
//    y su sprite. En la línea de diálogo escribes esa clave en 'expresion'
//    para cambiar la cara. Si la clave está vacía o no existe, usa el
//    retrato neutral.
//
// ============================================================

public enum LadoRetrato { Izquierda, Derecha }

[System.Serializable]
public class ExpresionRetrato
{
    [Tooltip("Identificador que usarás en la línea de diálogo (ej. 'curioso').")]
    public string clave = "neutral";
    public Sprite sprite;
}

[CreateAssetMenu(fileName = "NuevoPersonaje", menuName = "Mictlan/Personaje (Retrato)")]
public class Personaje : ScriptableObject
{
    [Header("Identidad")]
    public string nombreEnPantalla = "NOMBRE";
    public Color colorNombre = Color.white;

    [Header("Retrato")]
    [Tooltip("Retrato por defecto cuando la línea no especifica una expresión.")]
    public Sprite retratoNeutral;

    [Tooltip("Expresiones adicionales. La línea de diálogo elige una por su 'clave'.")]
    public ExpresionRetrato[] expresiones;

    [Header("Presentación")]
    [Tooltip("De qué lado de la pantalla aparece este personaje.")]
    public LadoRetrato lado = LadoRetrato.Izquierda;

    [Tooltip("Tinte sobre el retrato (blanco = sin tinte). Útil para sombras/auras.")]
    public Color tinte = Color.white;

    /// <summary>Devuelve el sprite de la expresión indicada, o el neutral si no existe.</summary>
    public Sprite GetRetrato(string clave)
    {
        if (!string.IsNullOrEmpty(clave) && expresiones != null)
        {
            foreach (var e in expresiones)
                if (e != null && e.clave == clave && e.sprite != null)
                    return e.sprite;
        }
        return retratoNeutral;
    }
}
