using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// ============================================================
//  HuecoConfiner — Recorta un hueco rectangular en un RoomConfiner
// ============================================================
//
//  La cámara NUNCA encuadra la zona del hueco mientras el confiner
//  padre esté activo. Útil para ocultar una sala interior que no debe
//  verse desde la sala que la rodea.
//
//  CÓMO FUNCIONA
//   CinemachineConfiner2D une los caminos del polígono con la regla
//   EvenOdd: un segundo camino DENTRO del primero es un hueco. En Start
//   este componente agrega el rectángulo de su BoxCollider2D como un
//   camino extra del PolygonCollider2D del RoomConfiner e invalida la
//   caché del confiner. El BoxCollider2D solo sirve para dibujar el
//   hueco en el editor: se desactiva en Awake y no participa en físicas.
//
//   Efecto secundario deseable: el TRIGGER del RoomConfiner también
//   queda hueco. Si el hueco es una sala con su propio RoomConfiner,
//   al salir de ella se vuelve a entrar al trigger padre y la cámara
//   regresa sola al confiner grande.
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//  1. Crea un GameObject vacío HIJO del RoomConfiner (debe usar
//     PolygonCollider2D — con BoxCollider2D no se pueden hacer huecos).
//  2. Añade HuecoConfiner (añade el BoxCollider2D automáticamente).
//  3. Ajusta la caja con "Edit Collider" o moviendo/escalando el objeto.
//     Admite rotación. Se dibuja en naranja.
//  4. Varios huecos = varios hijos con HuecoConfiner.
//
//  REGLAS
//   • El hueco debe quedar COMPLETO dentro del polígono padre.
//   • Los huecos no deben traslaparse entre sí (EvenOdd: la zona común
//     volvería a ser "dentro").
//   • Deja al menos medio encuadre de cámara entre el hueco y cualquier
//     pasillo por donde camine Romerito. Si no cabe el encuadre, el
//     confiner elimina esa franja y la cámara salta o se queda lejos.
//  ─────────────────────────────────────────────────────────────

[RequireComponent(typeof(BoxCollider2D))]
public class HuecoConfiner : MonoBehaviour
{
    [Tooltip("PolygonCollider2D del RoomConfiner a recortar. Vacío = el " +
             "del RoomConfiner padre más cercano.")]
    public PolygonCollider2D confinerPadre;

    private BoxCollider2D caja;

    void Awake()
    {
        caja = GetComponent<BoxCollider2D>();
        // Solo es una guía visual: fuera de las físicas desde el primer frame.
        caja.enabled = false;
    }

    void Start()
    {
        PolygonCollider2D poligono = ObtenerPoligono();
        if (poligono == null) return;

        List<Vector2> hueco = EsquinasEnEspacioDe(poligono);

        if (!DentroDelContornoExterior(poligono, hueco))
            Debug.LogWarning("[HuecoConfiner] '" + name + "' no queda completo " +
                             "dentro del polígono de '" + poligono.name + "'. Con " +
                             "EvenOdd el confiner puede comportarse raro.", this);

        poligono.pathCount += 1;
        poligono.SetPath(poligono.pathCount - 1, hueco);

        InvalidarCacheCamara(poligono);
    }

    // ── Construcción del hueco ───────────────────────────────

    PolygonCollider2D ObtenerPoligono()
    {
        if (confinerPadre != null) return confinerPadre;

        RoomConfiner room = GetComponentInParent<RoomConfiner>();
        if (room == null)
        {
            Debug.LogError("[HuecoConfiner] '" + name + "' no tiene un " +
                           "RoomConfiner padre ni confinerPadre asignado.", this);
            return null;
        }

        confinerPadre = room.GetComponent<PolygonCollider2D>();
        if (confinerPadre == null)
            Debug.LogError("[HuecoConfiner] El RoomConfiner '" + room.name +
                           "' no usa PolygonCollider2D. Los huecos solo " +
                           "funcionan en polígonos: cámbialo a PolygonCollider2D.", this);
        return confinerPadre;
    }

    /// <summary>
    /// Las 4 esquinas de la caja, en el espacio local de los puntos del
    /// polígono (PolygonCollider2D guarda sus puntos relativos a su offset).
    /// </summary>
    List<Vector2> EsquinasEnEspacioDe(PolygonCollider2D poligono)
    {
        Vector2 c = caja.offset;
        Vector2 m = caja.size * 0.5f;
        Vector2[] localCaja =
        {
            c + new Vector2(-m.x, -m.y),
            c + new Vector2( m.x, -m.y),
            c + new Vector2( m.x,  m.y),
            c + new Vector2(-m.x,  m.y),
        };

        var puntos = new List<Vector2>(4);
        foreach (Vector2 p in localCaja)
        {
            Vector3 mundo = transform.TransformPoint(p);
            Vector2 local = poligono.transform.InverseTransformPoint(mundo);
            puntos.Add(local - poligono.offset);
        }
        return puntos;
    }

    static bool DentroDelContornoExterior(PolygonCollider2D poligono, List<Vector2> puntos)
    {
        Vector2[] exterior = poligono.GetPath(0);
        foreach (Vector2 p in puntos)
            if (!PuntoEnPoligono(p, exterior)) return false;
        return true;
    }

    // Ray casting clásico.
    static bool PuntoEnPoligono(Vector2 p, Vector2[] poly)
    {
        bool dentro = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) /
                      (poly[j].y - poly[i].y) + poly[i].x)
                dentro = !dentro;
        }
        return dentro;
    }

    // ── Cámara ───────────────────────────────────────────────

    /// <summary>
    /// Confiner2D precalcula la forma (caché). Si la cámara ya está usando
    /// este polígono, hay que invalidarla para que vea el hueco.
    /// </summary>
    static void InvalidarCacheCamara(PolygonCollider2D poligono)
    {
        foreach (var confiner in FindObjectsByType<CinemachineConfiner2D>(FindObjectsSortMode.None))
        {
            if (confiner.BoundingShape2D == poligono)
                confiner.InvalidateBoundingShapeCache();
        }
    }

    // ── Gizmos ───────────────────────────────────────────────

    void OnDrawGizmos()
    {
        BoxCollider2D b = caja != null ? caja : GetComponent<BoxCollider2D>();
        if (b == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 centro = b.offset;
        Vector3 tam = b.size;

        Gizmos.color = new Color(1f, 0.55f, 0f, 0.15f);
        Gizmos.DrawCube(centro, tam);
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);
        Gizmos.DrawWireCube(centro, tam);

        // Aspa: "aquí la cámara no entra".
        Vector3 m = tam * 0.5f;
        Gizmos.DrawLine(centro + new Vector3(-m.x, -m.y), centro + new Vector3(m.x, m.y));
        Gizmos.DrawLine(centro + new Vector3(-m.x, m.y), centro + new Vector3(m.x, -m.y));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
