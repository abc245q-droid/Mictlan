using UnityEngine;
using UnityEngine.UI;

// ============================================================
//  UIPolygon — dibuja un polígono arbitrario en la UI
// ============================================================
//
//  Un Image solo dibuja rectángulos. Este Graphic acepta una malla
//  (vértices + triángulos en espacio local del RectTransform) y la
//  pinta con su color. Lo usa MapScreenUI para las siluetas de sala.
//
//  No requiere sprite: usa el color del Graphic como relleno plano.
//  Más adelante (3c) se puede cambiar por un sprite de amate pintado.
//
// ============================================================

[RequireComponent(typeof(CanvasRenderer))]
public class UIPolygon : Graphic
{
    private Vector2[] puntos;   // coords locales del RectTransform
    private int[] triangulos;

    public void SetMesh(Vector2[] puntos, int[] triangulos)
    {
        this.puntos = puntos;
        this.triangulos = triangulos;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (puntos == null || triangulos == null || puntos.Length < 3) return;

        UIVertex v = UIVertex.simpleVert;
        v.color = color;

        for (int i = 0; i < puntos.Length; i++)
        {
            v.position = puntos[i];
            vh.AddVert(v);
        }

        for (int i = 0; i + 2 < triangulos.Length; i += 3)
            vh.AddTriangle(triangulos[i], triangulos[i + 1], triangulos[i + 2]);
    }
}
