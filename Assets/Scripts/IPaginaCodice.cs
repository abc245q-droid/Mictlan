using UnityEngine;

// ============================================================
//  IPaginaCodice — Página del Códice con contenido dinámico
// ============================================================
//
//  Las páginas que construyen sus botones en tiempo de ejecución
//  (Herbario, Bestiario...) no pueden asignar 'primerSeleccionable'
//  en el Inspector. Implementando esta interfaz en cualquier
//  componente de la página, CodiceUI le pregunta a qué botón darle
//  el foco del mando cada vez que la página se muestra.
//
// ============================================================

public interface IPaginaCodice
{
    /// <summary>
    /// Se llama cada vez que la página se vuelve visible. Refresca el
    /// contenido y devuelve el objeto que debe recibir el foco del
    /// mando (o null si no hay nada seleccionable).
    /// </summary>
    GameObject AlMostrarPagina();
}
