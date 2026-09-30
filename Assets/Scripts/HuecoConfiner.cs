using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// ============================================================
//  HuecoConfiner — Recorta un hueco en un RoomConfiner
// ============================================================
//
//  La cámara NUNCA encuadra la zona del hueco mientras el confiner
//  padre esté activo. Útil para ocultar una sala interior que no debe
//  verse desde la sala que la rodea.
//
//  FORMA DEL HUECO (una de las dos, en este mismo GameObject)
//   • PolygonCollider2D → forma libre (L, escalones, diagonales…).
//     Es la que se añade por defecto al agregar el componente.
//     Se edita con "Edit Collider": arrastra vértices, clic en una
//     arista para añadir uno, Ctrl+clic en un vértice para borrarlo.
//   • BoxCollider2D     → rectángulo simple (huecos creados antes).
//   Si hay ambos, manda el PolygonCollider2D.
//
//  CÓMO FUNCIONA
//   CinemachineConfiner2D acepta varios caminos en el polígono. Uno
//   DENTRO del contorno exterior y con orientación CONTRARIA es un hueco
//   (Clipper lo exige al encoger el polígono). En Start este componente
//   copia la forma del hueco como camino(s) extra del PolygonCollider2D
//   del RoomConfiner, con la orientación corregida, e invalida la caché
//   del confiner. El collider del hueco solo es una guía visual: se
//   desactiva en Awake y no participa en físicas.
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
//  2. Añade HuecoConfiner (añade un PolygonCollider2D si no hay forma).
//  3. Dibuja el hueco con "Edit Collider". También puedes mover,
//     escalar y rotar el objeto. Se dibuja en naranja.
//  4. Varios huecos = varios hijos con HuecoConfiner.
//
//  REGLAS
//   • El hueco debe quedar COMPLETO dentro del polígono padre.
//   • Los huecos no deben traslaparse entre sí, ni el polígono del
//     hueco cruzarse consigo mismo (EvenOdd: la zona común volvería
//     a ser "dentro").
//   • Deja al menos medio encuadre de cámara entre el hueco y cualquier
//     pasillo por donde camine Romerito. Si no cabe el encuadre, el
//     confiner elimina esa franja y la cámara salta o se queda lejos.
//  ─────────────────────────────────────────────────────────────

public class HuecoConfiner : MonoBehaviour
{
    [Tooltip("PolygonCollider2D del RoomConfiner a recortar. Vacío = el " +
             "del RoomConfiner padre más cercano.")]
    public PolygonCollider2D confinerPadre;

    private Collider2D forma;

    // Editor: al añadir el componente, si no hay forma, poner un polígono.
    void Reset()
    {
        if (GetComponent<PolygonCollider2D>() == null && GetComponent<BoxCollider2D>() == null)
            gameObject.AddComponent<PolygonCollider2D>();
    }

    void Awake()
    {
        forma = ObtenerForma();
        // Solo es una guía visual: fuera de las físicas desde el primer frame.
        if (forma != null) forma.enabled = false;
    }

    void Start()
    {
        if (forma == null)
        {
            Debug.LogError("[HuecoConfiner] '" + name + "' no tiene PolygonCollider2D " +
                           "ni BoxCollider2D que defina la forma del hueco.", this);
            return;
        }

        PolygonCollider2D poligono = ObtenerPoligonoPadre();
        if (poligono == null) return;

        List<List<Vector2>> huecos = CaminosEnEspacioDe(poligono);
        float areaExterior = AreaFirmada(poligono.GetPath(0));

        foreach (List<Vector2> hueco in huecos)
        {
            // ORIENTACIÓN: Confiner2D encoge el polígono con ClipperOffset
            // (EndType.Polygon), que decide qué es hueco por el SENTIDO del
            // camino: el contorno exterior en un sentido, los huecos en el
            // contrario. Con el mismo sentido, Clipper trata el hueco como
            // una isla más, la encoge y la une al resto: el hueco desaparece.
            if (Mathf.Sign(AreaFirmada(hueco)) == Mathf.Sign(areaExterior))
                hueco.Reverse();

            if (!DentroDelContornoExterior(poligono, hueco))
                Debug.LogWarning("[HuecoConfiner] '" + name + "' no queda completo " +
                                 "dentro del polígono de '" + poligono.name + "'. Con " +
                                 "EvenOdd el confiner puede comportarse raro.", this);

            poligono.pathCount += 1;
            poligono.SetPath(poligono.pathCount - 1, hueco);
        }

        InvalidarCacheCamara(poligono);
    }

    // ── Forma del hueco ──────────────────────────────────────

    /// <summary>PolygonCollider2D si existe; si no, BoxCollider2D.</summary>
    Collider2D ObtenerForma()
    {
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        if (poly != null) return poly;
        return GetComponent<BoxCollider2D>();
    }

    /// <summary>
    /// Vértices de la forma en su espacio local (sin offset aplicado).
    /// Un polígono puede tener varios caminos; una caja, uno de 4 esquinas.
    /// </summary>
    static List<Vector2[]> CaminosLocales(Collider2D c)
    {
        var caminos = new List<Vector2[]>();

        if (c is PolygonCollider2D poly)
        {
            for (int i = 0; i < poly.pathCount; i++)
            {
                Vector2[] camino = poly.GetPath(i);
                for (int k = 0; k < camino.Length; k++)
                    camino[k] += poly.offset;   // los puntos del polígono son relativos a su offset
                if (camino.Length >= 3) caminos.Add(camino);
            }
        }
        else if (c is BoxCollider2D box)
        {
            Vector2 centro = box.offset;
            Vector2 m = box.size * 0.5f;
            caminos.Add(new[]
            {
                centro + new Vector2(-m.x, -m.y),
                centro + new Vector2( m.x, -m.y),
                centro + new Vector2( m.x,  m.y),
                centro + new Vector2(-m.x,  m.y),
            });
        }

        return caminos;
    }

    /// <summary>
    /// Caminos del hueco en el espacio de los puntos del polígono padre
    /// (PolygonCollider2D guarda sus puntos relativos a su offset).
    /// </summary>
    List<List<Vector2>> CaminosEnEspacioDe(PolygonCollider2D poligono)
    {
        var resultado = new List<List<Vector2>>();
        foreach (Vector2[] camino in CaminosLocales(forma))
        {
            var puntos = new List<Vector2>(camino.Length);
            foreach (Vector2 p in camino)
            {
                Vector3 mundo = transform.TransformPoint(p);
                Vector2 local = poligono.transform.InverseTransformPoint(mundo);
                puntos.Add(local - poligono.offset);
            }
            resultado.Add(puntos);
        }
        return resultado;
    }

    // ── Polígono padre ───────────────────────────────────────

    PolygonCollider2D ObtenerPoligonoPadre()
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

    static bool DentroDelContornoExterior(PolygonCollider2D poligono, List<Vector2> puntos)
    {
        Vector2[] exterior = poligono.GetPath(0);
        foreach (Vector2 p in puntos)
            if (!PuntoEnPoligono(p, exterior)) return false;
        return true;
    }

    // Fórmula del área de Gauss (shoelace). Positiva = antihorario.
    static float AreaFirmada(IList<Vector2> camino)
    {
        float a = 0f;
        for (int i = 0, j = camino.Count - 1; i < camino.Count; j = i++)
            a += (camino[j].x * camino[i].y) - (camino[i].x * camino[j].y);
        return a * 0.5f;
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
        Collider2D c = forma != null ? forma : ObtenerForma();
        if (c == null) return;

        Color linea = new Color(1f, 0.55f, 0f, 0.9f);
        foreach (Vector2[] camino in CaminosLocales(c))
        {
            Vector3 suma = Vector3.zero;
            Gizmos.color = linea;
            for (int i = 0; i < camino.Length; i++)
            {
                Vector3 a = transform.TransformPoint(camino[i]);
                Vector3 b = transform.TransformPoint(camino[(i + 1) % camino.Length]);
                Gizmos.DrawLine(a, b);
                suma += a;
            }

            // Aspa pequeña en el centroide: "aquí la cámara no entra".
            Vector3 centro = suma / camino.Length;
            float r = 0.35f;
            Gizmos.DrawLine(centro + new Vector3(-r, -r), centro + new Vector3(r, r));
            Gizmos.DrawLine(centro + new Vector3(-r, r), centro + new Vector3(r, -r));
        }
    }
}
