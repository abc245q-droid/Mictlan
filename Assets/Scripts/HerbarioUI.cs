using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  HerbarioUI — Página del Herbario en el Códice de Romerito
// ============================================================
//
//  Izquierda: cuadrícula con una casilla por especie del catálogo.
//  Derecha: detalle de la casilla seleccionada (ilustración, nombre
//  náhuatl, glosa, descripción, nota del ticitl, fuente, muestras).
//
//  QUÉ SE MUESTRA SEGÚN LA ETAPA
//    Desconocida → casilla vacía "???".
//    Avistada    → silueta (la ilustración teñida de oscuro), "?".
//    Recolectada → ilustración. Sin ticitl: nombre "?" y una pista.
//                  Con ticitl: nombre, glosa, descripción y fuente.
//    Estudiada   → además, la nota del ticitl; la casilla se dora.
//
//  SETUP EN UNITY
//  ─────────────────────────────────────────────────────────────
//   1. Pon este componente en 'Pagina_Herbario' (la página del Códice).
//   2. catalogo → el HerbarioCatalogo
//      (Mictlan → Herbario → Crear catálogo inicial lo genera).
//   3. contenedorSlots → un RectTransform con GridLayoutGroup.
//   4. Detalle: arrastra la Image y los TMP que quieras usar. Todos
//      son opcionales; los que dejes vacíos simplemente no se pintan.
//   5. slotPrefab es opcional: sin él, las casillas se crean solas.
//  ─────────────────────────────────────────────────────────────
//
//  El foco del mando lo da CodiceUI al mostrar la página (vía
//  IPaginaCodice): no hace falta 'primerSeleccionable'.
//
// ============================================================

public class HerbarioUI : MonoBehaviour, IPaginaCodice
{
    [Header("Datos")]
    public HerbarioCatalogo catalogo;

    [Header("Cuadrícula")]
    [Tooltip("RectTransform con GridLayoutGroup donde se crean las casillas.")]
    public RectTransform contenedorSlots;
    [Tooltip("Opcional. Prefab con Button + CodiceSlot. Vacío = casillas automáticas.")]
    public CodiceSlot slotPrefab;

    [Header("Detalle (todos opcionales)")]
    public Image ilustracion;
    public TMP_Text nombreLabel;
    public TMP_Text glosaLabel;
    public TMP_Text nombreEspanolLabel;
    public TMP_Text descripcionLabel;
    public TMP_Text notaTicitlLabel;
    public TMP_Text fuenteLabel;
    public TMP_Text muestrasLabel;
    public TMP_Text progresoLabel;

    [Header("Colores")]
    public Color colorSilueta          = new Color(0.12f, 0.09f, 0.07f, 0.85f);
    public Color colorFondoDesconocida = new Color(0.35f, 0.30f, 0.24f, 0.25f);
    public Color colorFondoConocida    = new Color(0.35f, 0.30f, 0.24f, 0.55f);
    public Color colorFondoEstudiada   = new Color(0.90f, 0.70f, 0.25f, 0.85f);
    [Tooltip("Tinte del botón con el foco del mando (se multiplica con el fondo).")]
    public Color colorFoco             = new Color(1.00f, 0.85f, 0.50f, 1.00f);
    [Tooltip("Color del nombre en las casillas.")]
    public Color colorEtiqueta         = new Color(0.23f, 0.16f, 0.10f, 1.00f);
    [Tooltip("Color del '???' de las plantas que aún no has visto.")]
    public Color colorEtiquetaDesconocida = new Color(0.23f, 0.16f, 0.10f, 0.45f);

    [Header("Foco")]
    [Tooltip("Cuánto crece la casilla con el foco del mando (1 = no crece).")]
    [Range(1f, 1.3f)] public float escalaFoco = 1.1f;

    [Header("Textos")]
    public string textoDesconocida = "Aún no has visto esta planta.";
    public string textoAvistada    = "La has visto, pero no has recogido ninguna muestra.";
    [TextArea(1, 3)]
    public string textoSinNombre   = "No sabes cómo se llama. Alguien en Apanohualoyan podría enseñarte.";
    [Tooltip("{0} = muestras que faltan.")]
    public string textoFaltanEstudio = "Lleva {0} muestra(s) más al ticitl para estudiarla.";

    // ── Estado ───────────────────────────────────────────────
    private readonly List<CodiceSlot> slots = new List<CodiceSlot>();
    private int seleccionado = 0;

    // ── Unity ────────────────────────────────────────────────

    void OnEnable()
    {
        HerbarioManager.OnHerbarioActualizado += Refrescar;
        Refrescar();
    }

    void OnDisable()
    {
        HerbarioManager.OnHerbarioActualizado -= Refrescar;
    }

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
        if (catalogo == null || contenedorSlots == null)
        {
            if (catalogo == null) Debug.LogWarning("[Herbario] Falta asignar el catálogo.", this);
            if (contenedorSlots == null) Debug.LogWarning("[Herbario] Falta contenedorSlots.", this);
            return;
        }

        AsegurarSlots();

        for (int i = 0; i < slots.Count; i++)
            PintarSlot(slots[i], catalogo.especies[i]);

        int total = catalogo.Total;
        int recolectadas = HerbarioManager.Contar(catalogo, EtapaHerbario.Recolectada);
        int estudiadas = HerbarioManager.Contar(catalogo, EtapaHerbario.Estudiada);
        Poner(progresoLabel, "Recolectadas " + recolectadas + "/" + total +
                             "   ·   Estudiadas " + estudiadas + "/" + total);

        if (slots.Count > 0)
            MostrarDetalle(Mathf.Clamp(seleccionado, 0, slots.Count - 1));
    }

    /// <summary>Lo llama la casilla (CodiceSlot) al recibir el foco.</summary>
    public void AlSeleccionarSlot(int indice)
    {
        seleccionado = indice;
        MostrarDetalle(indice);
    }

    // ── Casillas ─────────────────────────────────────────────

    private void AsegurarSlots()
    {
        int n = catalogo.Total;
        if (slots.Count == n) return;

        foreach (var s in slots) if (s != null) Destroy(s.gameObject);
        slots.Clear();

        for (int i = 0; i < n; i++)
        {
            CodiceSlot s = slotPrefab != null
                ? Instantiate(slotPrefab, contenedorSlots)
                : CodiceSlot.Crear(contenedorSlots, colorFoco);

            s.name = "Slot_" + i;
            s.indice = i;
            s.alSeleccionar = AlSeleccionarSlot;
            s.escalaFoco = escalaFoco;
            CodiceSlot.AplicarColorFoco(s.GetComponent<Button>(), colorFoco);
            slots.Add(s);
        }
    }

    private void PintarSlot(CodiceSlot s, FloraDefinition f)
    {
        if (s == null) return;
        s.escalaFoco = escalaFoco;   // permite ajustarlo en Play desde el Inspector
        if (f == null) { s.Pintar(null, Color.white, "", colorEtiquetaDesconocida, colorFondoDesconocida); return; }

        EtapaHerbario etapa = HerbarioManager.GetEtapa(f);
        bool nombre = HerbarioManager.NombreRevelado(f);

        switch (etapa)
        {
            case EtapaHerbario.Desconocida:
                s.Pintar(null, Color.white, "???", colorEtiquetaDesconocida, colorFondoDesconocida);
                break;
            case EtapaHerbario.Avistada:
                s.Pintar(f.ilustracion, colorSilueta, "?", colorEtiqueta, colorFondoConocida);
                break;
            default:
                s.Pintar(f.ilustracion, Color.white,
                         nombre ? f.nombreNahuatl : "?",
                         colorEtiqueta,
                         etapa == EtapaHerbario.Estudiada ? colorFondoEstudiada : colorFondoConocida);
                break;
        }
    }

    // ── Detalle ──────────────────────────────────────────────

    private void MostrarDetalle(int i)
    {
        if (catalogo == null || i < 0 || i >= catalogo.Total) return;
        FloraDefinition f = catalogo.especies[i];
        if (f == null) return;

        EtapaHerbario etapa = HerbarioManager.GetEtapa(f);
        bool nombre = HerbarioManager.NombreRevelado(f);

        // Ilustración
        if (ilustracion != null)
        {
            bool visible = etapa >= EtapaHerbario.Avistada && f.ilustracion != null;
            ilustracion.enabled = visible;
            ilustracion.sprite = f.ilustracion;
            ilustracion.preserveAspect = true;
            ilustracion.color = etapa == EtapaHerbario.Avistada ? colorSilueta : Color.white;
        }

        // Nombres
        Poner(nombreLabel, etapa == EtapaHerbario.Desconocida ? "???" : (nombre ? f.nombreNahuatl : "?"));
        Poner(glosaLabel, nombre && !string.IsNullOrEmpty(f.glosa) ? "«" + f.glosa + "»" : "");
        Poner(nombreEspanolLabel, nombre ? f.nombreEspanol : "");

        // Descripción
        string desc;
        if (etapa == EtapaHerbario.Desconocida) desc = textoDesconocida;
        else if (etapa == EtapaHerbario.Avistada) desc = textoAvistada;
        else if (!nombre) desc = textoSinNombre;
        else desc = f.descripcion;
        Poner(descripcionLabel, desc);

        // Nota del ticitl
        string nota = "";
        if (etapa == EtapaHerbario.Estudiada) nota = f.notaTicitl;
        else if (nombre) nota = string.Format(textoFaltanEstudio, HerbarioManager.FaltanParaEstudio(f));
        Poner(notaTicitlLabel, nota);

        // Pie: nombre científico · fuente
        string pie = "";
        if (nombre)
        {
            pie = f.nombreCientifico;
            if (!string.IsNullOrEmpty(f.fuente))
                pie = string.IsNullOrEmpty(pie) ? f.fuente : pie + "  ·  " + f.fuente;
        }
        Poner(fuenteLabel, pie);

        // Muestras
        Poner(muestrasLabel, etapa >= EtapaHerbario.Recolectada
            ? "Muestras: " + HerbarioManager.Muestras(f)
            : "");
    }

    private static void Poner(TMP_Text label, string texto)
    {
        if (label != null) label.text = texto ?? "";
    }

    // ────────────────────────────────────────────────────────
    //  DEV — pruebas sin ticitl ni plantas en escena
    //  (menú ⋮ del componente, en Play)
    // ────────────────────────────────────────────────────────

    [ContextMenu("DEV: Avistar todas")]
    void DevAvistarTodas()
    {
        if (catalogo == null) return;
        foreach (var f in catalogo.especies) if (f != null) HerbarioManager.Avistar(f);
    }

    [ContextMenu("DEV: Recolectar 1 de cada una")]
    void DevRecolectarTodas()
    {
        if (catalogo == null) return;
        foreach (var f in catalogo.especies) if (f != null) HerbarioManager.Recolectar(f, 1);
    }

    [ContextMenu("DEV: Conocer al ticitl")]
    void DevConocerTicitl() => HerbarioManager.ConocerTicitl();

    [ContextMenu("DEV: Estudiar todas (recolecta y entrega lo necesario)")]
    void DevEstudiarTodas()
    {
        if (catalogo == null) return;
        HerbarioManager.ConocerTicitl();
        foreach (var f in catalogo.especies)
        {
            if (f == null) continue;
            int faltan = HerbarioManager.FaltanParaEstudio(f);
            if (faltan <= 0) continue;
            HerbarioManager.Recolectar(f, faltan);
            HerbarioManager.EntregarParaEstudio(f, faltan);
        }
    }

    [ContextMenu("DEV: Reiniciar Herbario")]
    void DevReiniciar() => HerbarioManager.DevReiniciar();
}
