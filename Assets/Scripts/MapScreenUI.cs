using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ============================================================
//  MapScreenUI — El Códice de Romerito (render funcional)
// ============================================================
//
//  Minimapa del nivel actual. Dibuja cada sala coloreada por su estado:
//    • No visitada — amate casi en blanco (apenas visible).
//    • Borrador    — trazo cálido tenue.
//    • Asentada    — color pleno cálido.
//  Resalta la sala actual y marca la posición de Romerito (punto nocheztli).
//
//  FUENTE DE DIBUJO (dos vías, conviven):
//    • MapShape (PolygonCollider2D acomodado a mano) → silueta detallada.
//    • RoomConfiner (Collider2D de cámara) → rectángulo, como fallback
//      para las salas que aún no tienen MapShape.
//  La lógica de estados no cambia: solo cambia QUÉ forma se dibuja.
//
//  Cableado en el inspector:
//    panel            — raíz del panel del mapa (se activa/desactiva).
//    contenedorSalas  — RectTransform donde se proyectan las formas.
//    hudJuego         — HUD a ocultar mientras el mapa está abierto (opcional).
//    tituloLabel      — TMP para el nombre del nivel (opcional).
//
// ============================================================

public class MapScreenUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Refs de UI")]
    public GameObject panel;
    public RectTransform contenedorSalas;
    public GameObject hudJuego;
    public TMP_Text tituloLabel;

    [Header("Input")]
    public KeyCode teclaAbrir = KeyCode.Tab;
    public KeyCode botonAbrir = KeyCode.JoystickButton6;   // View/Back en Xbox
    public KeyCode teclaCerrarAlterna = KeyCode.Escape;

    [Header("Estilo del minimapa")]
    [Tooltip("Margen interno del contenedor, en píxeles.")]
    public float margen = 40f;
    [Tooltip("Factor de tamaño de cada celda rectangular (0.9 deja huecos).")]
    [Range(0.5f, 1f)] public float factorCelda = 0.9f;

    public Color colorNoVisitada = new Color(0.35f, 0.30f, 0.24f, 0.15f);
    public Color colorBorrador   = new Color(0.85f, 0.50f, 0.20f, 0.35f);
    public Color colorAsentada   = new Color(0.90f, 0.55f, 0.20f, 0.95f);
    public Color colorSalaActual = new Color(1.00f, 0.85f, 0.40f, 1.00f);
    public Color colorRomerito   = new Color(0.85f, 0.15f, 0.20f, 1.00f); // nocheztli

    // ── Estado interno ──
    private readonly List<GameObject> elementos = new List<GameObject>();
    private bool bloquearInputUnFrame;

    // Congelado del jugador (espejo de DialogueManager/Shop).
    private RomeritoMovement playerMovement;
    private RomeritoCombat playerCombat;
    private Rigidbody2D playerRb;
    private bool refsCacheadas;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        IsOpen = false;
    }

    void Update()
    {
        if (!IsOpen)
        {
            if (DialogueManager.IsActive || PochtecahShopUI.IsOpen) return;
            if (Input.GetKeyDown(teclaAbrir) || Input.GetKeyDown(botonAbrir))
                Abrir();
            return;
        }

        if (bloquearInputUnFrame) { bloquearInputUnFrame = false; return; }

        if (Input.GetKeyDown(teclaAbrir) || Input.GetKeyDown(botonAbrir) ||
            Input.GetKeyDown(teclaCerrarAlterna))
            Cerrar();
    }

    // ── Apertura / cierre ─────────────────────────────────
    public void Abrir()
    {
        if (IsOpen) return;
        IsOpen = true;
        bloquearInputUnFrame = true;

        CongelarJugador(true);
        if (panel != null) panel.SetActive(true);
        if (hudJuego != null) hudJuego.SetActive(false);

        Construir();
    }

    public void Cerrar()
    {
        if (!IsOpen) return;
        IsOpen = false;

        if (panel != null) panel.SetActive(false);
        if (hudJuego != null) hudJuego.SetActive(true);
        CongelarJugador(false);
    }

    // ── Construcción del minimapa ─────────────────────────
    private void Construir()
    {
        Limpiar();
        if (contenedorSalas == null) { Debug.LogError("[Mapa] Falta contenedorSalas."); return; }

        // 1) Siluetas detalladas (MapShape) y salas-cámara (RoomConfiner).
        var shapes = FindObjectsByType<MapShape>(FindObjectsSortMode.None);
        var idsConShape = new HashSet<string>();
        foreach (var ms in shapes)
            if (!string.IsNullOrEmpty(ms.mapRoomId)) idsConShape.Add(ms.mapRoomId);

        var confiners = FindObjectsByType<RoomConfiner>(FindObjectsSortMode.None);

        // 2) Bounds combinados: siluetas + confiners que NO tengan silueta (fallback).
        bool hayBounds = false;
        Bounds total = new Bounds();
        foreach (var ms in shapes)
        {
            if (string.IsNullOrEmpty(ms.mapRoomId)) continue;
            if (!hayBounds) { total = ms.Bounds; hayBounds = true; }
            else total.Encapsulate(ms.Bounds);
        }
        foreach (var rc in confiners)
        {
            if (string.IsNullOrEmpty(rc.mapRoomId) || idsConShape.Contains(rc.mapRoomId)) continue;
            var col = rc.GetComponent<Collider2D>();
            if (col == null) continue;
            if (!hayBounds) { total = col.bounds; hayBounds = true; }
            else total.Encapsulate(col.bounds);
        }

        if (!hayBounds)
        {
            if (tituloLabel != null) tituloLabel.text = "Códice en blanco";
            Debug.Log("[Mapa] No hay MapShape ni RoomConfiner con mapRoomId en esta escena.");
            return;
        }

        Vector2 mundoMin = total.min;
        Vector2 mundoTam = total.size;
        if (mundoTam.x <= 0f) mundoTam.x = 1f;
        if (mundoTam.y <= 0f) mundoTam.y = 1f;
        Vector2 areaUI = contenedorSalas.rect.size - new Vector2(margen * 2f, margen * 2f);

        // Proyección mundo → UI (origen en el centro del contenedor).
        Vector2 Proyectar(Vector2 mundo)
        {
            Vector2 n = mundo - mundoMin;
            n.x /= mundoTam.x; n.y /= mundoTam.y;
            return (n - new Vector2(0.5f, 0.5f)) * areaUI;
        }

        Color ColorDeEstado(string id)
        {
            EstadoSala e = MapManager.GetEstado(id);
            Color c = e == EstadoSala.Asentada ? colorAsentada
                    : e == EstadoSala.Borrador ? colorBorrador
                    : colorNoVisitada;
            if (id == MapManager.SalaActual && e != EstadoSala.NoVisitada) c = colorSalaActual;
            return c;
        }

        // 3a) Siluetas detalladas.
        foreach (var ms in shapes)
        {
            if (string.IsNullOrEmpty(ms.mapRoomId)) continue;
            Mesh m = ms.CrearMallaMundo();
            if (m == null) continue;

            Vector3[] mv = m.vertices;
            Vector2[] pts = new Vector2[mv.Length];
            for (int i = 0; i < mv.Length; i++) pts[i] = Proyectar(new Vector2(mv[i].x, mv[i].y));
            int[] tris = m.triangles;
            Destroy(m);

            CrearSilueta("Silueta_" + ms.mapRoomId, pts, tris, ColorDeEstado(ms.mapRoomId));
        }

        // 3b) Fallback rectangular para salas sin silueta.
        foreach (var rc in confiners)
        {
            if (string.IsNullOrEmpty(rc.mapRoomId) || idsConShape.Contains(rc.mapRoomId)) continue;
            var col = rc.GetComponent<Collider2D>();
            if (col == null) continue;

            Vector2 pos = Proyectar(new Vector2(col.bounds.center.x, col.bounds.center.y));
            Vector2 tam = new Vector2(col.bounds.size.x / mundoTam.x, col.bounds.size.y / mundoTam.y) * areaUI * factorCelda;
            tam.x = Mathf.Max(tam.x, 6f); tam.y = Mathf.Max(tam.y, 6f);
            CrearCelda("Sala_" + rc.mapRoomId, pos, tam, ColorDeEstado(rc.mapRoomId));
        }

        // 4) Marcador de Romerito (punto nocheztli).
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            Vector2 pos = Proyectar(new Vector2(p.transform.position.x, p.transform.position.y));
            var dot = CrearCelda("Romerito", pos, new Vector2(16f, 16f), colorRomerito);
            dot.transform.SetAsLastSibling();
        }

        // 5) Título.
        string idTitulo = shapes.Length > 0 && !string.IsNullOrEmpty(shapes[0].mapRoomId)
            ? shapes[0].mapRoomId
            : (confiners.Length > 0 ? confiners[0].mapRoomId : "");
        if (tituloLabel != null) tituloLabel.text = "Códice — " + NombreNivel(idTitulo);
    }

    private GameObject CrearSilueta(string nombre, Vector2[] puntos, int[] tris, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(UIPolygon));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(contenedorSalas, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var poly = go.GetComponent<UIPolygon>();
        poly.color = color;
        poly.raycastTarget = false;
        poly.SetMesh(puntos, tris);

        elementos.Add(go);
        return go;
    }

    private GameObject CrearCelda(string nombre, Vector2 anchoredPos, Vector2 size, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(contenedorSalas, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        elementos.Add(go);
        return go;
    }

    private void Limpiar()
    {
        foreach (var e in elementos) if (e != null) Destroy(e);
        elementos.Clear();
    }

    private string NombreNivel(string roomId)
    {
        int n = MapManager.NivelDeSala(roomId);
        return n >= 0 ? "Nivel " + n : "";
    }

    // ── Congelado del jugador (espejo de DialogueManager/Shop) ──
    private void CongelarJugador(bool congelar)
    {
        if (!refsCacheadas)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerMovement = p.GetComponent<RomeritoMovement>();
                playerCombat = p.GetComponent<RomeritoCombat>();
                playerRb = p.GetComponent<Rigidbody2D>();
            }
            refsCacheadas = true;
        }

        if (congelar && playerRb != null) playerRb.linearVelocity = Vector2.zero;
        if (playerMovement != null) playerMovement.enabled = !congelar;
        if (playerCombat != null) playerCombat.enabled = !congelar;
    }
}
