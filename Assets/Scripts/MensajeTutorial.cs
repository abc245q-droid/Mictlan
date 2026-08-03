using UnityEngine;

// ============================================================
//  MensajeTutorial — ScriptableObject
// ============================================================
//  Asset de datos reutilizable. Crear en:
//  Assets/Data/Tutoriales/msj_XXX.asset
// ============================================================

public enum TipoMensajeTutorial
{
    Toast,   // No bloqueante: fade in, hold, fade out
    Modal    // Pausa el juego, requiere botón Aceptar
}

[CreateAssetMenu(fileName = "msj_nuevo", menuName = "Mictlan/Mensaje Tutorial")]
public class MensajeTutorial : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("ID único. Marca este mensaje como 'visto'. No cambiar tras publicar.")]
    public string id = "msj_id_unico";

    [Header("Contenido")]
    [TextArea(2, 5)]
    public string texto = "Texto del mensaje";

    [Tooltip("Sprite del botón a mostrar (opcional). Ej: ícono de X, A, B.")]
    public Sprite iconoBoton;

    [Header("Comportamiento")]
    public TipoMensajeTutorial tipo = TipoMensajeTutorial.Toast;

    [Tooltip("Solo Toast: cuánto dura visible en pantalla.")]
    public float duracionToast = 4f;
}