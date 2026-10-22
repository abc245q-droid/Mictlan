using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  EquipamientoUI — Página de Equipamiento del Códice
// ============================================================
//
//  Izquierda: secciones creadas por código, una por categoría que
//  tenga piezas en el catálogo:
//      Arma · Reliquias · Favores de los dioses · Compañeros
//  (Los dones no tienen sección: la reliquia es la que permite el don.)
//  Cada sección = título ("Favores de los dioses · 1/3") + fila de
//  casillas. Derecha: detalle de la casilla con foco.
//
//  ESTADOS DE CADA CASILLA
//    No obtenida → "???" tenue.
//    Obtenida    → nombre (o icono, cuando haya arte).
//    Favor activo→ fondo dorado.
//
//  El estado se lee en vivo cada vez que se abre la página
//  (EquipamientoEstado): no guarda nada propio.
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//   1. Pon este componente en 'Pagina_Equipamiento'.
//   2. catalogo → EquipamientoCatalogo
//      (Mictlan → Equipamiento → Crear catálogo inicial lo genera).
//   3. contenedorSecciones → RectTransform vacío en la mitad izquierda.
//      Si no tiene Vertical Layout Group, se le agrega uno solo.
//   4. Detalle: Image + TMP de UI (no 3D). Todos opcionales.
//  ─────────────────────────────────────────────────────────────
//
// ============================================================

public class EquipamientoUI : MonoBehaviour, IPaginaCodice
{
    [Header("Datos")]
    public EquipamientoCatalogo catalogo;

    [Header("Secciones")]
    [Tooltip("RectTransform donde se crean títulos y filas de casillas.")]
    public RectTransform contenedorSecciones;
    public Vector2 tamanoCasilla = new Vector2(110f, 110f);
    public Vector2 espacioCasillas = new Vector2(12f, 12f);
    [Min(1)] public int columnas = 6;
    public float tamanoTitulo = 24f;
    public Color colorTitulo = new Color(0.23f, 0.16f, 0.10f, 1f);

    [Header("Detalle (todos opcionales)")]
    public Image icono;
    public TMP_Text nombreLabel;
    public TMP_Text subtituloLabel;
    public TMP_Text descripcionLabel;
    public TMP_Text controlesLabel;
    public TMP_Text estadoLabel;

    [Header("Colores")]
    public Color colorFondoNoObtenida = new Color(0.35f, 0.30f, 0.24f, 0.25f);
    public Color colorFondoObtenida   = new Color(0.35f, 0.30f, 0.24f, 0.55f);
    public Color colorFondoActiva     = new Color(0.90f, 0.70f, 0.25f, 0.85f);
    public Color colorFoco            = new Color(1.00f, 0.85f, 0.50f, 1.00f);
    public Color colorEtiqueta        = new Color(0.23f, 0.16f, 0.10f, 1.00f);
    public Color colorEtiquetaNoObtenida = new Color(0.23f, 0.16f, 0.10f, 0.45f);

    [Header("Foco")]
    [Range(1f, 1.3f)] public float escalaFoco = 1.1f;

    [Header("Textos")]
    public string textoNoObtenida = "Aún no lo tienes.";

    // ── Estado ───────────────────────────────────────────────
    private readonly List<CodiceSlot> slots = new List<CodiceSlot>();
    private readonly List<EquipoDefinition> piezasEnOrden = new List<EquipoDefinition>();
    private readonly Dictionary<CategoriaEquipo, TMP_Text> titulos = new Dictionary<CategoriaEquipo, TMP_Text>();
    private readonly List<GameObject> creados = new List<GameObject>();
    private int construidoCon = -1;
    private int seleccionado = 0;

    // ── Unity ────────────────────────────────────────────────

    void OnEnable() => Refrescar();

    // ── IPaginaCodice ────────────────────────────────────────

    public GameObject AlMostrarPagina()
    {
        Refrescar();
        if (slots.Count == 0) return null;
        seleccionado = Mathf.Clamp(seleccionado, 0, slots.Count - 1);
        MostrarDetalle(seleccionado);
        return slots[seleccionado].gameObject;
    }

    // ── API ──────────────────────────────────────────────────

    public void Refrescar()
    {
        if (catalogo == null || contenedorSecciones == null)
        {
            if (catalogo == null) Debug.LogWarning("[Equipamiento] Falta asignar el catálogo.", this);
            if (contenedorSecciones == null) Debug.LogWarning("[Equipamiento] Falta contenedorSecciones.", this);
            return;
        }

        int n = catalogo.piezas != null ? catalogo.piezas.Count : 0;
        if (construidoCon != n) Construir();

        for (int i = 0; i < slots.Count; i++)
            PintarSlot(slots[i], piezasEnOrden[i]);

        ActualizarTitulos();

        if (slots.Count > 0)
            MostrarDetalle(Mathf.Clamp(seleccionado, 0, slots.Count - 1));
    }

    public void AlSeleccionarSlot(int indice)
    {
        seleccionado = indice;
        MostrarDetalle(indice);
    }

    // ── Construcción por secciones ───────────────────────────

    private void Construir()
    {
        foreach (var go in creados) if (go != null) Destroy(go);
        creados.Clear();
        slots.Clear();
        piezasEnOrden.Clear();
        titulos.Clear();

        AsegurarLayoutVertical();

        foreach (CategoriaEquipo cat in System.Enum.GetValues(typeof(CategoriaEquipo)))
        {
            var dePieza = new List<EquipoDefinition>();
            foreach (var p in catalogo.piezas)
                if (p != null && p.categoria == cat) dePieza.Add(p);
            if (dePieza.Count == 0) continue;

            titulos[cat] = CrearTitulo(cat);
            RectTransform fila = CrearFila(cat);

            foreach (var p in dePieza)
            {
                CodiceSlot s = CodiceSlot.Crear(fila, colorFoco);
                s.name = "Slot_" + p.id;
                s.indice = slots.Count;
                s.alSeleccionar = AlSeleccionarSlot;
                s.escalaFoco = escalaFoco;
                slots.Add(s);
                piezasEnOrden.Add(p);
            }
        }

        construidoCon = catalogo.piezas.Count;
    }

    private void AsegurarLayoutVertical()
    {
        var v = contenedorSecciones.GetComponent<VerticalLayoutGroup>();
        if (v != null) return;
        v = contenedorSecciones.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
    }

    private TMP_Text CrearTitulo(CategoriaEquipo cat)
    {
        var go = new GameObject("Titulo_" + cat, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(contenedorSecciones, false);
        creados.Add(go);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = tamanoTitulo;
        tmp.color = colorTitulo;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    private RectTransform CrearFila(CategoriaEquipo cat)
    {
        var go = new GameObject("Fila_" + cat, typeof(RectTransform), typeof(GridLayoutGroup));
        go.transform.SetParent(contenedorSecciones, false);
        creados.Add(go);

        var grid = go.GetComponent<GridLayoutGroup>();
        grid.cellSize = tamanoCasilla;
        grid.spacing = espacioCasillas;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columnas;
        return go.GetComponent<RectTransform>();
    }

    private void ActualizarTitulos()
    {
        foreach (var par in titulos)
        {
            int total = 0, tiene = 0;
            foreach (var p in piezasEnOrden)
            {
                if (p.categoria != par.Key) continue;
                total++;
                if (EquipamientoEstado.Tiene(p)) tiene++;
            }
            // El total solo tiene sentido donde se coleccionan varias.
            par.Value.text = total > 1
                ? TituloDe(par.Key) + "  ·  " + tiene + "/" + total
                : TituloDe(par.Key);
        }
    }

    private static string TituloDe(CategoriaEquipo cat)
    {
        switch (cat)
        {
            case CategoriaEquipo.Arma:      return "Arma";
            case CategoriaEquipo.Reliquia:  return "Reliquias";
            case CategoriaEquipo.Favor:     return "Favores de los dioses";
            case CategoriaEquipo.Companero: return "Compañeros";
        }
        return cat.ToString();
    }

    // ── Pintado ──────────────────────────────────────────────

    private void PintarSlot(CodiceSlot s, EquipoDefinition e)
    {
        if (s == null || e == null) return;
        s.escalaFoco = escalaFoco;

        if (!EquipamientoEstado.Tiene(e))
        {
            s.Pintar(null, Color.white, "???", colorEtiquetaNoObtenida, colorFondoNoObtenida);
            return;
        }

        Color fondo = EquipamientoEstado.EsFavorActivo(e) ? colorFondoActiva : colorFondoObtenida;
        s.Pintar(e.icono, Color.white, e.nombre, colorEtiqueta, fondo);
    }

    private void MostrarDetalle(int i)
    {
        if (i < 0 || i >= piezasEnOrden.Count) return;
        EquipoDefinition e = piezasEnOrden[i];
        if (e == null) return;

        bool tiene = EquipamientoEstado.Tiene(e);

        if (icono != null)
        {
            icono.enabled = tiene && e.icono != null;
            icono.sprite = e.icono;
            icono.preserveAspect = true;
        }

        Poner(nombreLabel,      tiene ? e.nombre : "???");
        Poner(subtituloLabel,   tiene ? e.subtitulo : "");
        Poner(descripcionLabel, tiene ? e.descripcion : textoNoObtenida);
        Poner(controlesLabel,   tiene ? e.controles : "");
        Poner(estadoLabel,      EquipamientoEstado.LineaEstado(e));
    }

    private static void Poner(TMP_Text label, string texto)
    {
        if (label != null) label.text = texto ?? "";
    }
}
