using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================
//  SesionMictlan — Arranque y desmontaje de una sesión de juego
// ============================================================
//
//  EL PROBLEMA QUE RESUELVE
//  ─────────────────────────────────────────────────────────────
//  Mictlán tiene cuatro objetos con DontDestroyOnLoad:
//      • GameManager01      (dueño del save)
//      • PersistentPlayer   (Romerito, con Monedero/Tonalli/Heart encima)
//      • InteractionManager (prompt "Presiona B")
//      • TutorialManager    (mensajes ya vistos)
//
//  Eso funciona perfecto mientras sólo se salta entre niveles. Pero
//  "Volver al Inicio" rompe la suposición: al cargar MenuPrincipal
//  esos cuatro objetos SOBREVIVEN. Romerito seguiría vivo, invisible,
//  detrás del menú. Y al pulsar "Nueva Partida", el Romerito nuevo de
//  la escena se auto-destruiría (su Awake ve que instance != null) —
//  dejando en pie al Romerito viejo con TODAS las habilidades de la
//  partida anterior. Nueva Partida entregaría un Romerito con doble
//  salto, dash y Macahuitl desde el primer segundo.
//
//  Por eso volver al menú NO es un LoadScene normal: es un desmontaje
//  completo de la sesión.
//
//  POR QUÉ UNA SONDA EN VEZ DE UNA LISTA DE REFERENCIAS
//  ─────────────────────────────────────────────────────────────
//  Unity no expone la escena DontDestroyOnLoad por nombre, pero sí
//  a través de cualquier objeto que viva en ella. Creamos un GameObject
//  vacío, lo marcamos DontDestroyOnLoad y le preguntamos por su escena:
//  ahí están todos los persistentes, presentes y futuros. Si mañana
//  añadimos un AudioManager persistente, este código lo limpia solo,
//  sin que nadie tenga que acordarse de registrarlo.
// ============================================================

public enum IntencionArranque
{
    Ninguna,        // arranque directo en una escena de juego (editor)
    NuevaPartida,   // save borrado; Romerito nace donde lo puso el diseñador
    Continuar       // hay que colocarlo en su último Cihuacalli
}

public static class SesionMictlan
{
    /// <summary>
    /// Sobrevive a los cambios de escena (es static) y lo consume
    /// GameManager01.OnSceneLoaded. Es el único puente entre el menú
    /// y el juego, porque el menú no tiene GameManager.
    /// </summary>
    public static IntencionArranque intencion = IntencionArranque.Ninguna;

    /// <summary>
    /// Destruye todos los objetos persistentes de la sesión actual.
    ///
    /// OJO con el orden: Destroy() es diferido a final de frame, igual
    /// que LoadScene(). Llamar a los dos seguidos deja una ventana en la
    /// que Unity podría cargar la escena nueva ANTES de haber destruido
    /// los persistentes viejos — y volveríamos al bug de los duplicados.
    /// Por eso el desmontaje completo va en la corrutina de abajo, con
    /// un frame de separación.
    /// </summary>
    public static void DestruirPersistentes()
    {
        // DialogueManager.IsActive es un bool ESTÁTICO: no muere con el
        // objeto. Si el jugador pausa durante un diálogo y sale al menú,
        // quedaría en true y bloquearía todo el input de la siguiente
        // partida. Se resetea solo en el Awake del DialogueManager de la
        // escena nueva, pero lo dejamos explícito por si el menú llegara
        // a necesitar input antes de eso.
        Time.timeScale = 1f;

        var sonda = new GameObject("[SondaDDOL]");
        Object.DontDestroyOnLoad(sonda);

        foreach (GameObject raiz in sonda.scene.GetRootGameObjects())
            Object.Destroy(raiz);   // la propia sonda se destruye aquí también
    }

    /// <summary>
    /// Desmonta la sesión y carga el menú principal. Lánzalo con
    /// StartCoroutine desde cualquier MonoBehaviour de la escena
    /// (PauseMenu lo hace).
    /// </summary>
    public static IEnumerator RutinaVolverAlMenu(string escenaMenu)
    {
        intencion = IntencionArranque.Ninguna;
        DestruirPersistentes();

        // Un frame para que Unity complete las destrucciones pendientes.
        // WaitForSecondsRealtime no serviría: queremos un FRAME, no tiempo.
        yield return null;

        SceneManager.LoadScene(escenaMenu);
    }

    /// <summary>
    /// Comprueba que una escena esté realmente en Build Settings antes de
    /// intentar cargarla. Sin esto, un save que apunta a una escena
    /// renombrada deja el juego en pantalla negra sin ningún error visible.
    /// </summary>
    public static bool EscenaValida(string nombre)
    {
        return !string.IsNullOrEmpty(nombre) &&
               Application.CanStreamedLevelBeLoaded(nombre);
    }
}
