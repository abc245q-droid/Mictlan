using System.Collections.Generic;
using UnityEngine;

// ============================================================
//  PochtecahShop — Comercio del mercader de los muertos
// ============================================================
//
//  REBANADA 2a: solo la capa de transacción (sin UI todavía).
//  Se prueba con los ContextMenu "DEV:" del final.
//
//  El Pochtecah comercia con las ofrendas ardidas que descienden
//  al Mictlan (GDD §01). Vende, en tres pisos:
//    • Estuche de Tlacuilo — compra única, enciende el mapa.
//    • Amate del nivel      — una vez por nivel (lienzo en blanco).
//    • Pigmentos            — compra única c/u, encienden marcadores.
//
//  La navegación NUNCA se gatea (GDD §04): el papel y los pigmentos
//  solo compran información y marcadores, jamás el cruzar.
//
//  Moneda: cacao (común) y tajaderas (rara, ~20 cacao c/u).
//  Afinación de precios (GDD §05): el rojo es barato y temprano;
//  el azul maya es el lujo caro del completista.
//
// ============================================================

public enum TipoProducto
{
    EstucheDeTlacuilo,
    AmateDelNivel,
    PigmentoRojo,
    PigmentoAmarillo,
    PigmentoAzul,
    PigmentoVerde
}

[System.Serializable]
public class EntradaCatalogo
{
    public TipoProducto tipo;
    public string nombreMostrado = "";
    [TextArea(1, 3)] public string descripcion = "";
    public int costoCacao = 10;
    public int costoTajaderas = 0;
}

public class PochtecahShop : MonoBehaviour
{
    [Header("Identidad del mercader")]
    [Tooltip("Nivel donde está este Pochtecah (0 = Atlein, 1 = Chicunamictlan...). " +
             "Define a qué nivel pertenece el amate que vende.")]
    public int nivelDeEstePochtecah = 0;

    [Header("Referencias")]
    [Tooltip("Monedero del jugador. Si se deja vacío, se busca en la escena.")]
    public Monedero monedero;

    [Header("Catálogo")]
    [Tooltip("Productos que ofrece. Reset() lo precarga con los valores del GDD.")]
    public List<EntradaCatalogo> catalogo = new List<EntradaCatalogo>();

    // ── Resultado de un intento de compra ──
    public enum ResultadoCompra { Exito, YaLoTienes, SinFondos, Invalido }

    void Awake()
    {
        if (monedero == null) monedero = FindObjectOfType<Monedero>();
    }

    // ── Compra ────────────────────────────────────────────
    public ResultadoCompra Comprar(TipoProducto tipo)
    {
        EntradaCatalogo e = BuscarEntrada(tipo);
        if (e == null)
        {
            Debug.LogWarning("[Pochtecah] Producto no está en el catálogo: " + tipo);
            return ResultadoCompra.Invalido;
        }

        if (YaComprado(tipo))
        {
            Debug.Log("[Pochtecah] Ya tienes: " + tipo);
            return ResultadoCompra.YaLoTienes;
        }

        if (monedero == null)
        {
            Debug.LogError("[Pochtecah] No hay Monedero en la escena.");
            return ResultadoCompra.Invalido;
        }

        if (!monedero.TryGastar(e.costoCacao, e.costoTajaderas))
        {
            Debug.Log($"[Pochtecah] Sin fondos para {tipo} (cuesta {e.costoCacao} cacao).");
            return ResultadoCompra.SinFondos;
        }

        // Pago realizado → aplicar el efecto.
        AplicarProducto(tipo);

        // Persistir: SaveGame recoge cacao (del Monedero) y las banderas del mapa.
        if (GameManager01.instance != null) GameManager01.instance.SaveGame();

        Debug.Log($"[Pochtecah] Compra exitosa: {tipo}. Restan {monedero.cacaoSeeds} cacao.");
        return ResultadoCompra.Exito;
    }

    private void AplicarProducto(TipoProducto tipo)
    {
        switch (tipo)
        {
            case TipoProducto.EstucheDeTlacuilo: MapManager.DarEstucheDeTlacuilo();        break;
            case TipoProducto.AmateDelNivel:     MapManager.ComprarPapel(nivelDeEstePochtecah); break;
            case TipoProducto.PigmentoRojo:      MapManager.DarPigmento(Pigmento.Rojo);     break;
            case TipoProducto.PigmentoAmarillo:  MapManager.DarPigmento(Pigmento.Amarillo); break;
            case TipoProducto.PigmentoAzul:      MapManager.DarPigmento(Pigmento.Azul);     break;
            case TipoProducto.PigmentoVerde:     MapManager.DarPigmento(Pigmento.Verde);    break;
        }
    }

    // ── Consultas (las usará la UI en la rebanada 2b) ──────
    public bool YaComprado(TipoProducto tipo)
    {
        switch (tipo)
        {
            case TipoProducto.EstucheDeTlacuilo: return MapManager.TieneEstuche();
            case TipoProducto.AmateDelNivel:     return MapManager.TienePapel(nivelDeEstePochtecah);
            case TipoProducto.PigmentoRojo:      return MapManager.TienePigmento(Pigmento.Rojo);
            case TipoProducto.PigmentoAmarillo:  return MapManager.TienePigmento(Pigmento.Amarillo);
            case TipoProducto.PigmentoAzul:      return MapManager.TienePigmento(Pigmento.Azul);
            case TipoProducto.PigmentoVerde:     return MapManager.TienePigmento(Pigmento.Verde);
        }
        return false;
    }

    /// <summary>¿Se puede comprar ahora? (no lo tienes y alcanza el dinero).</summary>
    public bool PuedeComprar(TipoProducto tipo)
    {
        if (YaComprado(tipo)) return false;
        EntradaCatalogo e = BuscarEntrada(tipo);
        return e != null && monedero != null && monedero.PuedePagar(e.costoCacao, e.costoTajaderas);
    }

    private EntradaCatalogo BuscarEntrada(TipoProducto tipo)
    {
        foreach (var e in catalogo)
            if (e != null && e.tipo == tipo) return e;
        return null;
    }

    // ── Catálogo por defecto (precios afinados al GDD §05) ──
    // Unity llama Reset() al añadir el componente o desde el menú del Inspector.
    void Reset()
    {
        catalogo = new List<EntradaCatalogo>
        {
            new EntradaCatalogo { tipo = TipoProducto.EstucheDeTlacuilo, costoCacao = 15,
                nombreMostrado = "Estuche de Tlacuilo",
                descripcion = "Pincel y concha-tintero. Despierta tu memoria: podrás cartografiar." },
            new EntradaCatalogo { tipo = TipoProducto.AmateDelNivel, costoCacao = 10,
                nombreMostrado = "Amate",
                descripcion = "Papel de ofrenda. El lienzo en blanco donde asentar este nivel." },
            new EntradaCatalogo { tipo = TipoProducto.PigmentoRojo, costoCacao = 5,
                nombreMostrado = "Nocheztli (rojo)",
                descripcion = "Grana cochinilla. Marca Cihuacallis y viaje rápido." },
            new EntradaCatalogo { tipo = TipoProducto.PigmentoAmarillo, costoCacao = 12,
                nombreMostrado = "Zacatlaxcalli (amarillo)",
                descripcion = "Marca tesoros y coleccionables." },
            new EntradaCatalogo { tipo = TipoProducto.PigmentoVerde, costoCacao = 12,
                nombreMostrado = "Matlalin (verde)",
                descripcion = "Marca Pochtecah y altares de dioses." },
            new EntradaCatalogo { tipo = TipoProducto.PigmentoAzul, costoCacao = 30,
                nombreMostrado = "Azul Maya",
                descripcion = "El lujo del tlacuilo. Marca puertas selladas y pasos bloqueados." },
        };
    }

    // ────────────────────────────────────────────────────────
    //  DEV — pruebas sin UI (menú de los tres puntos del componente)
    // ────────────────────────────────────────────────────────
    [ContextMenu("DEV: +50 cacao")]
    void DevDarCacao() { if (monedero == null) monedero = FindObjectOfType<Monedero>(); if (monedero) monedero.AddCacao(50); }

    [ContextMenu("DEV: Comprar Estuche de Tlacuilo")]
    void DevComprarEstuche() => Comprar(TipoProducto.EstucheDeTlacuilo);

    [ContextMenu("DEV: Comprar Amate (este nivel)")]
    void DevComprarAmate() => Comprar(TipoProducto.AmateDelNivel);

    [ContextMenu("DEV: Comprar Pigmento Rojo")]
    void DevComprarRojo() => Comprar(TipoProducto.PigmentoRojo);

    [ContextMenu("DEV: Comprar Azul Maya")]
    void DevComprarAzul() => Comprar(TipoProducto.PigmentoAzul);
}
