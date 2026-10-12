using UnityEngine;

// ============================================================
//  PinchoTileable — tira de pinchos repetible para MICTLÁN
// ============================================================
//
//  Convierte un sprite de pinchos (hueso, roca volcánica, etc.)
//  en una tira que se alarga por MÓDULOS ENTEROS: nunca corta un
//  cráneo a la mitad en el extremo.
//
//  Se encarga de:
//    • SpriteRenderer en Draw Mode = Tiled, ancho = módulos × tile
//    • BoxCollider2D (trigger) ajustado a la banda baja del arte
//      → el daño lo sigue haciendo Trampa.cs, este script solo
//        dimensiona el collider
//    • Capa de brasa opcional (hijo con Mictlan/SpriteGlowHDR):
//      mismo tamaño que la tira + respiración HDR por instancia
//
//  SETUP EN UNITY:
//  ─────────────────────────────────────────────────────────────
//  GameObject "Pincho_X"
//    ├ SpriteRenderer (sprite de pinchos)
//    ├ BoxCollider2D (Is Trigger = true, Auto Tiling = OFF)
//    ├ Trampa (permitePogo = true)
//    ├ PinchoTileable
//    └ hijo "Brasa" (opcional)
//        └ SpriteRenderer + material con Mictlan/SpriteGlowHDR
//  ─────────────────────────────────────────────────────────────

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class PinchoTileable : MonoBehaviour
{
    [Header("Largo")]
    [Tooltip("Cuántas veces se repite el dibujo. Siempre entero: " +
             "así los extremos terminan en un módulo completo.")]
    [Min(1)] public int modulos = 3;

    [Header("Hitbox")]
    [Tooltip("Alto del collider como fracción del alto del arte. " +
             "Más bajo que las puntas = más justo para el jugador.")]
    [Range(0.2f, 1f)] public float altoHitbox = 0.6f;

    [Tooltip("Margen vacío en cada extremo, en fracción del ancho " +
             "de UN módulo (el arte no llega al borde del PNG).")]
    [Range(0f, 0.4f)] public float margenLateral = 0.08f;

    [Header("Brasa (opcional, Chicunamictlán)")]
    [Tooltip("SpriteRenderer hijo con la capa de brasa. Vacío = sin brasa.")]
    public SpriteRenderer brasa;

    [ColorUsage(true, true)]
    public Color colorBrasa = new Color(2f, 0.6f, 0.15f, 1f);

    [Tooltip("Intensidad mínima y máxima del pulso. " +
             "El Bloom corta en ~1.2: max > 1.2 = la brasa 'enciende'.")]
    public float intensidadMin = 0.6f;
    public float intensidadMax = 1.3f;

    [Tooltip("Velocidad del pulso (ruido Perlin, no senoidal).")]
    public float velocidadPulso = 0.8f;

    SpriteRenderer sr;
    BoxCollider2D col;
    MaterialPropertyBlock mpb;
    float desfase;

    static readonly int ID_COLOR = Shader.PropertyToID("_Color");

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        desfase = Random.value * 100f;
        Aplicar();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Diferido: cambiar otros componentes dentro de OnValidate
        // dispara advertencias de Unity.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Aplicar();
        };
    }
#endif

    void LateUpdate()
    {
        if (!Application.isPlaying || brasa == null) return;

        float ruido = Mathf.PerlinNoise(desfase, Time.time * velocidadPulso);
        float k = Mathf.Lerp(intensidadMin, intensidadMax, ruido);

        if (mpb == null) mpb = new MaterialPropertyBlock();
        brasa.GetPropertyBlock(mpb);
        mpb.SetColor(ID_COLOR, colorBrasa * k);
        brasa.SetPropertyBlock(mpb);
    }

    // ── Dimensionado ─────────────────────────────────────────

    /// <summary>
    /// Ajusta tira, collider y brasa al número de módulos.
    /// Se llama solo al cargar y al tocar el Inspector.
    /// </summary>
    public void Aplicar()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (col == null) col = GetComponent<BoxCollider2D>();
        if (sr == null || sr.sprite == null) return;

        Sprite s = sr.sprite;
        float anchoTile = s.rect.width / s.pixelsPerUnit;
        float altoTile = s.rect.height / s.pixelsPerUnit;

        // 1. Tira
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        Vector2 tamano = new Vector2(anchoTile * modulos, altoTile);
        sr.size = tamano;

        // 2. Collider — en Tiled la geometría se reparte alrededor del
        //    pivot en proporción, igual que el sprite original.
        Vector2 pivotN = new Vector2(s.pivot.x / s.rect.width,
                                     s.pivot.y / s.rect.height);

        float izquierda = -pivotN.x * tamano.x;
        float abajo = -pivotN.y * tamano.y;
        float margen = margenLateral * anchoTile;
        float altoCol = tamano.y * altoHitbox;

        col.autoTiling = false;
        col.isTrigger = true;
        col.size = new Vector2(Mathf.Max(0.01f, tamano.x - 2f * margen), altoCol);
        col.offset = new Vector2(izquierda + tamano.x * 0.5f, abajo + altoCol * 0.5f);

        // 3. Brasa — mismo tamaño y mismo modo que la tira
        if (brasa != null && brasa.sprite != null)
        {
            brasa.drawMode = SpriteDrawMode.Tiled;
            brasa.tileMode = SpriteTileMode.Continuous;
            brasa.size = tamano;
            brasa.transform.localPosition = Vector3.zero;
            if (brasa.sortingOrder <= sr.sortingOrder)
                brasa.sortingOrder = sr.sortingOrder + 1;
        }
    }

    // ── Gizmos ───────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (col == null) col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        // Hitbox real (respeta escala y rotación)
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.15f, 0f, 0.9f);
        Gizmos.DrawWireCube(col.offset, col.size);
    }
}
