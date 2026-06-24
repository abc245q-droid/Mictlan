using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

// ============================================================
//  BraseroGrupo — puzzle de braseros (sala / zona)
// ============================================================
//
//  Agrupa varios Brasero. Cuando TODOS están encendidos, dispara
//  OnGrupoCompletado (la primera vez, en vivo) o OnYaCompletadoAlCargar
//  (si ya estaban todos encendidos de una sesión previa).
//
//  CÓMO USARLO:
//   • Lo más simple: pon este componente en un GameObject padre y
//     cuelga los Brasero como HIJOS. Se auto-recogen y se les asigna
//     este grupo automáticamente.
//   • O arrastra manualmente los Brasero a la lista 'braseros'.
//
//  CABLEAR EL COMPLETADO (Inspector):
//   • OnGrupoCompletado → momento clave: abre una puerta
//     (arrastra la puerta y elige GameObject.SetActive(false) para
//     quitarla, o el método de tu script de puerta), reproduce una
//     fanfarria, activa un Cihuacalli, etc.
//   • OnYaCompletadoAlCargar → restaura ese estado SIN repetir el
//     momento (ej. dejar la puerta ya abierta al cargar partida).

public class BraseroGrupo : MonoBehaviour
{
    [Header("Braseros del grupo")]
    [Tooltip("Si lo dejas vacío, se auto-recogen los Brasero hijos.")]
    public List<Brasero> braseros = new List<Brasero>();

    [Header("Eventos")]
    [Tooltip("PRIMERA vez que se encienden todos (en vivo): abrir puerta, fanfarria, cutscene.")]
    public UnityEvent OnGrupoCompletado;

    [Tooltip("Si el grupo YA estaba completo al cargar partida: restaura el estado " +
             "(puerta ya abierta) SIN repetir el momento.")]
    public UnityEvent OnYaCompletadoAlCargar;

    private bool completado = false;

    void Awake()
    {
        // Auto-recoger braseros hijos y asignarles este grupo.
        if (braseros.Count == 0)
            braseros.AddRange(GetComponentsInChildren<Brasero>(true));

        foreach (var b in braseros)
            if (b != null) b.grupo = this;
    }

    void Start()
    {
        // Esperamos un frame para que todos los Brasero hayan corrido su
        // Start (y leído su estado del save) antes de comprobar el grupo.
        StartCoroutine(VerificarAlCargar());
    }

    private IEnumerator VerificarAlCargar()
    {
        yield return null;
        if (TodosEncendidos())
        {
            completado = true;
            Debug.Log("[BraseroGrupo] '" + name + "' ya estaba completo al cargar.");
            OnYaCompletadoAlCargar?.Invoke();
        }
    }

    /// <summary>Llamado por cada Brasero al encenderse.</summary>
    public void NotificarEncendido()
    {
        if (completado) return;
        if (TodosEncendidos())
        {
            completado = true;
            Debug.Log("[BraseroGrupo] '" + name + "' COMPLETADO: todos los braseros encendidos.");
            OnGrupoCompletado?.Invoke();
        }
    }

    public bool TodosEncendidos()
    {
        if (braseros.Count == 0) return false;
        foreach (var b in braseros)
            if (b == null || !b.EstaEncendido) return false;
        return true;
    }
}
