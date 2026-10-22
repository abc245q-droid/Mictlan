#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  EquipamientoCatalogoInicial — Genera las piezas de Equipamiento
// ============================================================
//
//  MENÚ  Mictlan → Equipamiento → Crear catálogo inicial
//    Crea una EquipoDefinition por pieza en
//      Assets/Mictlan/Equipamiento/Piezas/equipo_<id>.asset
//    y el catálogo en
//      Assets/Mictlan/Equipamiento/EquipamientoCatalogo.asset
//
//  Seguro de correr varias veces: no pisa piezas que ya existen.
//
//  No hay sección de dones: la reliquia es la que permite usar el don.
//  Cada habilidad de movimiento entrará como la Reliquia que la concede
//  (con requisito Correr, Dash...), cuando se diseñe.
//
//  Textos de Tláloc y Tepeyollotl son provisionales: sus specs aún
//  no están cerradas. Edítalos en el
//  asset cuando se definan; la notaDiseno lo recuerda.
//
//  Este archivo DEBE vivir en una carpeta llamada "Editor".
// ============================================================

public static class EquipamientoCatalogoInicial
{
    private const string CARPETA_RAIZ   = "Assets/Mictlan/Equipamiento";
    private const string CARPETA_PIEZAS = CARPETA_RAIZ + "/Piezas";
    private const string RUTA_CATALOGO  = CARPETA_RAIZ + "/EquipamientoCatalogo.asset";

    private struct Datos
    {
        public string id, nombre, subtitulo, descripcion, controles, diseno;
        public CategoriaEquipo categoria;
        public RequisitoEquipo requisito;
        public RomeritoCombat.GodFavor favor;
    }

    private static Datos P(string id, CategoriaEquipo cat, RequisitoEquipo req, string nombre, string subtitulo,
                           string descripcion, string controles, string diseno = "",
                           RomeritoCombat.GodFavor favor = RomeritoCombat.GodFavor.Neutro)
    {
        return new Datos
        {
            id = id, categoria = cat, requisito = req, nombre = nombre, subtitulo = subtitulo,
            descripcion = descripcion, controles = controles, diseno = diseno, favor = favor
        };
    }

    private static List<Datos> Piezas()
    {
        return new List<Datos>
        {
            // ── Arma ───────────────────────────────────────────────
            P("macahuitl", CategoriaEquipo.Arma, RequisitoEquipo.Macahuitl,
              "Macahuitl", "espada de madera y obsidiana",
              "Madera dura con filos de obsidiana incrustados. Golpea enemigos, rompe muros y mazorcas petrificadas, y corta las plantas más duras.",
              "X: atacar. Con arriba o abajo, golpe vertical. Hacia abajo en el aire, rebota sobre lo que golpea."),

            // ── Reliquias ──────────────────────────────────────────
            P("jicara_tonalli", CategoriaEquipo.Reliquia, RequisitoEquipo.JicaraTonalli,
              "Jícara de Tonalli", "regalo de Tlacua",
              "Regalo de Tlacua, emisario de Huehueteotl. Concede el don de materializar y guardar el tonalli: el propio y el que dejan los enemigos al caer.",
              "El tonalli se recupera venciendo enemigos y se gasta en los Favores."),

            // ── Favores ────────────────────────────────────────────
            P("favor_huehueteotl", CategoriaEquipo.Favor, RequisitoEquipo.FavorHuehueteotl,
              "Favor de Huehueteotl", "el dios viejo, señor del fuego",
              "Barrera de Copal: quieto, el humo te oculta y nada te toca; en movimiento, te hace más pequeño para esquivar. Con este Favor, el Macahuitl golpea con más fuerza.",
              "LB: cambiar de Favor. B: Barrera de Copal.",
              "Revisar costo/duración de la Barrera y la pasiva de +10% de daño.",
              RomeritoCombat.GodFavor.Huehueteotl),

            P("favor_tlaloc", CategoriaEquipo.Favor, RequisitoEquipo.FavorTlaloc,
              "Favor de Tláloc", "señor de la lluvia",
              "El señor de la lluvia y de Tlalocan concede su fuerza a quien camina el Mictlan.",
              "LB: cambiar de Favor.",
              "Texto provisional: completar con la habilidad cuando se implemente.",
              RomeritoCombat.GodFavor.Tlaloc),

            P("favor_tepeyollotl", CategoriaEquipo.Favor, RequisitoEquipo.FavorTepeyollotl,
              "Favor de Tepeyollotl", "corazón del monte",
              "El jaguar que habita el interior de los montes presta su fuerza a Romerito.",
              "LB: cambiar de Favor.",
              "Texto provisional: completar con la habilidad cuando se implemente.",
              RomeritoCombat.GodFavor.Tepeyollotl),

            // ── Compañeros ─────────────────────────────────────────
            P("companero_tlacua", CategoriaEquipo.Companero, RequisitoEquipo.JicaraTonalli,
              "Tlacua", "emisario de Huehueteotl",
              "Tlacuache emisario del dios viejo. Enciende los braseros, pero necesita que Romerito limpie de enemigos la zona para poder trabajar.",
              ""),

            P("companero_xolo", CategoriaEquipo.Companero, RequisitoEquipo.Xolo,
              "Xolo", "perro guía de los muertos",
              "Xoloitzcuintli. Acompaña a los muertos en su camino por el Mictlan.",
              "",
              "Gate de agua en el Nivel 2. Completar con su mecánica."),

            P("companero_ozomatli", CategoriaEquipo.Companero, RequisitoEquipo.PorDefinir,
              "Ozomatli", "el mono",
              "Compañero de Romerito.",
              "",
              "Aún no existe flag en PlayerData: agregar un RequisitoEquipo cuando se implemente."),
        };
    }

    // Piezas que generaron versiones anteriores y ya no existen en el
    // diseño. Crear() las borra del disco y del catálogo.
    private static readonly string[] OBSOLETAS =
    {
        "don_correr", "don_doble_salto", "don_dash", "don_trepar", "don_salto_pared"
    };

    [MenuItem("Mictlan/Equipamiento/Crear catálogo inicial")]
    public static void Crear()
    {
        AsegurarCarpeta(CARPETA_PIEZAS);

        var catalogo = AssetDatabase.LoadAssetAtPath<EquipamientoCatalogo>(RUTA_CATALOGO);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<EquipamientoCatalogo>();
            AssetDatabase.CreateAsset(catalogo, RUTA_CATALOGO);
        }
        if (catalogo.piezas == null) catalogo.piezas = new List<EquipoDefinition>();

        int creadas = 0, agregadas = 0, borradas = 0;

        foreach (string id in OBSOLETAS)
        {
            string ruta = CARPETA_PIEZAS + "/equipo_" + id + ".asset";
            var vieja = AssetDatabase.LoadAssetAtPath<EquipoDefinition>(ruta);
            if (vieja == null) continue;
            catalogo.piezas.Remove(vieja);
            AssetDatabase.DeleteAsset(ruta);
            borradas++;
        }
        catalogo.piezas.RemoveAll(p => p == null);

        foreach (var d in Piezas())
        {
            string ruta = CARPETA_PIEZAS + "/equipo_" + d.id + ".asset";
            var e = AssetDatabase.LoadAssetAtPath<EquipoDefinition>(ruta);

            if (e == null)
            {
                e = ScriptableObject.CreateInstance<EquipoDefinition>();
                e.id = d.id;
                e.nombre = d.nombre;
                e.subtitulo = d.subtitulo;
                e.categoria = d.categoria;
                e.requisito = d.requisito;
                e.favor = d.favor;
                e.descripcion = d.descripcion;
                e.controles = d.controles;
                e.notaDiseno = d.diseno;
                AssetDatabase.CreateAsset(e, ruta);
                creadas++;
            }

            if (!catalogo.piezas.Contains(e))
            {
                catalogo.piezas.Add(e);
                agregadas++;
            }
        }

        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalogo;
        EditorGUIUtility.PingObject(catalogo);
        Debug.Log("[Equipamiento] Catálogo listo en " + RUTA_CATALOGO + ": " + creadas +
                  " pieza(s) creada(s), " + agregadas + " agregada(s), " + borradas + " obsoleta(s) borrada(s), " +
                  catalogo.piezas.Count + " en total.");
    }

    private static void AsegurarCarpeta(string ruta)
    {
        string[] partes = ruta.Split('/');
        string actual = partes[0];
        for (int i = 1; i < partes.Length; i++)
        {
            string siguiente = actual + "/" + partes[i];
            if (!AssetDatabase.IsValidFolder(siguiente))
                AssetDatabase.CreateFolder(actual, partes[i]);
            actual = siguiente;
        }
    }
}
#endif
