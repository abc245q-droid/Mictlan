#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================
//  HerbarioCatalogoInicial — Genera la flora inicial del Herbario
// ============================================================
//
//  MENÚ  Mictlan → Herbario → Crear catálogo inicial
//    Crea una FloraDefinition por especie en
//      Assets/Mictlan/Herbario/Flora/flora_<id>.asset
//    y el catálogo en
//      Assets/Mictlan/Herbario/HerbarioCatalogo.asset
//
//  Es seguro correrlo varias veces: NO pisa las plantas que ya
//  existen (si editaste textos o asignaste ilustraciones, se
//  respetan). Solo crea las que falten y las agrega al catálogo.
//
//  MENÚ  Mictlan → Herbario → Validar catálogo
//    Revisa ids vacíos o repetidos y plantas sin ilustración.
//
//  CONTENIDO
//    Textos breves basados en Sahagún (Códice Florentino), Francisco
//    Hernández (Historia de las plantas de la Nueva España), el Códice
//    de la Cruz-Badiano y Ruiz de Alarcón. Lo que conviene verificar
//    antes de publicarse está anotado en 'notaDiseno' de cada planta.
//
//    El tenexyetl NO está aquí: no es una planta, es una preparación
//    (picietl + cal). Será una receta del ticitl.
//
//  Este archivo DEBE vivir en una carpeta llamada "Editor".
// ============================================================

public static class HerbarioCatalogoInicial
{
    private const string CARPETA_RAIZ  = "Assets/Mictlan/Herbario";
    private const string CARPETA_FLORA = CARPETA_RAIZ + "/Flora";
    private const string RUTA_CATALOGO = CARPETA_RAIZ + "/HerbarioCatalogo.asset";

    private const RolFlora COL = RolFlora.Coleccionable;
    private const RolFlora OFR = RolFlora.Ofrenda;
    private const RolFlora ENE = RolFlora.Enemigo;
    private const RolFlora DEC = RolFlora.Decoracion;
    private const RolFlora PLA = RolFlora.Plataforma;

    private struct Datos
    {
        public string id, nahuatl, glosa, espanol, cientifico, descripcion, ticitl, fuente, diseno;
        public int muestras, nivel;
        public RolFlora roles;
    }

    private static Datos P(string id, string nahuatl, string glosa, string espanol, string cientifico,
                           string descripcion, string ticitl, string fuente,
                           int muestras, int nivel, RolFlora roles, string diseno = "")
    {
        return new Datos
        {
            id = id, nahuatl = nahuatl, glosa = glosa, espanol = espanol, cientifico = cientifico,
            descripcion = descripcion, ticitl = ticitl, fuente = fuente,
            muestras = muestras, nivel = nivel, roles = roles, diseno = diseno
        };
    }

    private static List<Datos> Especies()
    {
        return new List<Datos>
        {
            // ── Plantas con papel principal ─────────────────────────
            P("toloatzin", "Toloatzin", "la venerable cabeza inclinada", "toloache",
              "Datura spp.",
              "Su flor blanca cuelga como una cabeza que se inclina. Quien la toca pierde el rumbo: la derecha se vuelve izquierda.",
              "Untada con cuidado calma el dolor. Tocada sin respeto, voltea el camino. No la mires de frente cuando se inclina.",
              "Sahagún, Códice Florentino, Libro XI; Hernández",
              3, 1, ENE | COL,
              "Etimología: tolohua, 'inclinar la cabeza' + -tzin reverencial. Enemigo pasivo: péndulo + inversión de controles."),

            P("coaxihuitl", "Coaxihuitl", "hierba de la serpiente", "enredadera del ololiuhqui",
              "Turbina corymbosa",
              "Enredadera que trepa y se enrosca como serpiente. Enreda a quien la cruza; solo el filo del Macahuitl abre paso.",
              "No la arranques con rabia. Corta lo justo y te dará sus semillas.",
              "Hernández; Ruiz de Alarcón, Tratado de las supersticiones (1629)",
              3, 2, ENE | COL,
              "Arena movediza vegetal: ralentiza y bloquea el dash. Al cortarla suelta ololiuhqui."),

            P("ololiuhqui", "Ololiuhqui", "lo que es redondo", "semilla de la virgen",
              "Turbina corymbosa (semillas)",
              "Semillas pequeñas y redondas de la coaxihuitl. Los adivinos las consultaban para hallar lo perdido y conocer el origen de un mal.",
              "Se guardan en cajitas, con ofrenda, como a un abuelo. Quien las trata como piedra no ve nada.",
              "Ruiz de Alarcón (1629); Jacinto de la Serna, Manual de ministros de indios",
              5, 2, OFR | COL,
              "Ofrenda: revela secretos de la sala en el Códice. Verificar el nombre común en español."),

            P("tlitliltzin", "Tlitliltzin", "el venerable negrito", "manto de la virgen",
              "Ipomoea tricolor",
              "Semillas negras, gemelas oscuras del ololiuhqui. Muestran lo que el espejo humeante quiere esconder.",
              "Lo blanco enseña lo perdido. Lo negro enseña lo escondido. Cuidado con lo que te enseñe.",
              "Sahagún; identificación de R. G. Wasson (1963)",
              3, -1, OFR | COL,
              "Late-game: revela rastros de Tezcatlipoca. Verificar identificación botánica y nombre común."),

            P("picietl", "Picietl", "tabaco silvestre", "tabaco cimarrón",
              "Nicotiana rustica",
              "Tabaco fuerte de hoja ancha. Molido con cal se vuelve tenexyetl, que protege contra lo que daña sin dejarse ver.",
              "Golpéalo nueve veces, como se hacía. Ni una más, ni una menos.",
              "Sahagún; Ruiz de Alarcón (1629)",
              3, -1, COL | OFR,
              "Cosecha con 9 golpes (golpesParaCosechar = 9). Verificar en Ruiz de Alarcón el epíteto ritual de 'nueve veces golpeado'."),

            P("peyotl", "Peyotl", "(etimología discutida)", "peyote",
              "Lophophora williamsii",
              "Cactus pequeño y sin espinas que crece a la sombra de otras plantas espinosas. Los teochichimeca lo comían para no sentir hambre ni miedo.",
              "Viene del norte, de tierra chichimeca. Si lo encontraste aquí abajo, alguien lo cargó hasta el Mictlan.",
              "Sahagún, Códice Florentino, Libro X",
              3, 3, COL | OFR,
              "Ofrenda: 'Ánimo', inmunidad a knockback. Escondido entre pinchos de nopal en L3."),

            P("teonanacatl", "Teonanacatl", "hongo divino", "hongo de los volcanes",
              "Psilocybe aztecorum",
              "Hongo de los bosques de pino en las laderas de los volcanes, tierra de Tláloc. Brota después de la lluvia.",
              "Brotan después de la tormenta. Si les hablas bajito, a veces contestan.",
              "Sahagún, Códice Florentino, Libro XI; G. Guzmán, The Genus Psilocybe (1983)",
              3, -1, PLA | COL,
              "Gate del Favor de Tláloc: la lluvia los hace brotar como plataformas de rebote. Verificar el nombre 'apipiltzin' antes de usarlo. Que los hongos 'hablen' es tradición mazateca: acreditarla así."),

            P("sinicuichi", "Sinicuichi", "(origen del nombre discutido)", "sinicuiche",
              "Heimia salicifolia",
              "Arbusto de flores amarillas. Dicen que tiñe la vista de oro, aleja los sonidos y hace recordar lo olvidado.",
              "Huele a algo que ya viviste. No te asustes si recuerdas.",
              "Registros etnobotánicos de los siglos XIX y XX",
              3, -1, COL,
              "Coleccionable narrativo: recuerdos de la vida de Romerito. No hay fuente colonial clara; verificar origen del nombre."),

            P("cacahuaxochitl", "Cacahuaxochitl", "flor del cacao", "rosita de cacao",
              "Quararibea funebris",
              "Árbol cuyas flores secas perfumaban el cacao de los señores. Aquí abajo florece donde los vivos todavía dejan ofrendas.",
              "Huele a fiesta de muertos. Alguien allá arriba todavía se acuerda.",
              "Sahagún; Hernández",
              3, -1, OFR | COL | DEC,
              "Con cacao → cacahuatl (restaura vidas). Verificar el origen del epíteto 'funebris' antes de usarlo en texto."),

            P("quetzalaxochiacatl", "Quetzalaxochiacatl", "flor acuática preciosa (lectura aproximada)", "ninfa, lirio de agua",
              "Nymphaea ampla",
              "Nenúfar que abre con la luz y se cierra en la oscuridad. Aparece en los murales de Tlalocan, en Teotihuacan.",
              "Tus parientes duermen entre sus hojas, ajolote. Pisa con cuidado.",
              "Hernández; murales de Tepantitla, Teotihuacan",
              3, 2, PLA | COL,
              "Plataforma temporizada por luz en Atoctlan. Verificar grafía, glosa e identificación con N. ampla."),

            // ── Flora menor (variedad, Herbario y moneda del ticitl) ──
            P("cempoalxochitl", "Cempoalxochitl", "flor de veinte (pétalos)", "cempasúchil",
              "Tagetes erecta",
              "Flor amarilla de muchos pétalos que adornaba fiestas y ofrendas. Aquí abajo crece por todas partes.",
              "Abunda en el Mictlan. No es casualidad.",
              "Sahagún; Hernández",
              3, 0, DEC | COL),

            P("yauhtli", "Yauhtli", "", "pericón",
              "Tagetes lucida",
              "Hierba de olor anisado. Se arrojaba al fuego como incienso en las fiestas de los dioses de la lluvia.",
              "Tláloc la reconoce por el olor.",
              "Sahagún",
              3, -1, COL | OFR,
              "Zona de Tláloc."),

            P("iztauhyatl", "Iztauhyatl", "", "estafiate",
              "Artemisia ludoviciana",
              "Hierba amarga de hojas plateadas. Se usaba contra los males del estómago.",
              "Amarga, como debe ser lo que cura.",
              "Sahagún; Hernández",
              3, 2, COL,
              "Verificar su uso ritual (ofrenda a los tlaloque) antes de mencionarlo en texto."),

            P("yoloxochitl", "Yoloxochitl", "flor del corazón", "flor de corazón",
              "Magnolia mexicana",
              "Flor grande y blanca cuyos pétalos se cierran como un corazón. Se usaba para los males del corazón.",
              "Guárdala bien. A ti, que andas buscando tu vida, te hará falta.",
              "Códice de la Cruz-Badiano (1552); Hernández",
              3, -1, COL,
              "Rara. Ligada a vidas / fragmentos de corazón."),

            P("oceloxochitl", "Oceloxochitl", "flor de jaguar", "flor de tigre",
              "Tigridia pavonia",
              "Flor manchada como piel de ocelote. Su bulbo se comía cocido.",
              "Tepeyollotl la tiene en su monte. Pídele permiso antes de cortarla.",
              "Hernández",
              3, -1, COL,
              "Zona de Tepeyollotl."),

            P("cacaloxochitl", "Cacaloxochitl", "flor del cuervo", "flor de mayo",
              "Plumeria rubra",
              "Flor de cinco pétalos y perfume intenso, de las más apreciadas por los señores.",
              "Crece junto a la flor del cacao. Las dos se buscan.",
              "Sahagún; Hernández",
              3, -1, DEC | COL,
              "Cerca del cacahuaxochitl."),

            P("chicalotl", "Chicalotl", "", "chicalote",
              "Argemone mexicana",
              "Cardo de flor amarilla y hojas espinosas. Su savia amarilla se usaba como medicina.",
              "Espina por fuera, remedio por dentro. Como casi todo aquí.",
              "Hernández",
              3, 3, COL,
              "L3, junto al peyote y los pinchos de nopal."),

            P("matlalxochitl", "Matlalxochitl", "flor azul verdosa", "hierba del pollo",
              "Commelina sp.",
              "Florecilla de un azul profundo. De su color podría salir el matlalin con que se pinta el Códice.",
              "El tlacuilo que pinta con esto pinta el agua y el cielo.",
              "Hernández",
              3, -1, COL,
              "Vínculo con el pigmento matlalin. Verificar en Hernández la especie y la relación con el pigmento."),

            P("pochotl", "Pochotl", "", "ceiba",
              "Ceiba sp.",
              "Árbol enorme de tronco espinoso. En muchas tradiciones mesoamericanas sus raíces bajan al inframundo y sus ramas sostienen el cielo.",
              "Si alguna vez te pierdes, busca sus raíces. Todas llevan hacia arriba.",
              "Hernández; Códice Fejérváry-Mayer (árboles de las direcciones)",
              1, -1, DEC,
              "Decoración monumental. Con una sola muestra queda estudiado (una astilla de corteza)."),

            P("copalquahuitl", "Copalquahuitl", "árbol del copal", "copal",
              "Bursera spp.",
              "Árbol de resina blanca y olorosa. El copal se quemaba en casi todas las ceremonias.",
              "La barrera que te cubre huele a él. Ya lo conoces.",
              "Sahagún; Hernández",
              3, 1, DEC | COL,
              "Conecta con la Barrera de Copal (Favor de Huehueteotl)."),

            P("huauhtli", "Huauhtli", "", "amaranto",
              "Amaranthus hypochondriacus",
              "Semilla diminuta y abundante. Con ella y miel se modelaban figuras de los dioses para las fiestas.",
              "Pago pequeño, pero honrado. Acepto todo el que traigas.",
              "Sahagún",
              10, -1, COL,
              "Moneda chica del ticitl: común y abundante."),

            P("chian", "Chian", "", "chía",
              "Salvia hispanica",
              "Semilla pequeña que se volvía bebida. Exprimida daba un aceite para lacas y pinturas.",
              "Los tlacuilos la exprimen para que el color no se borre.",
              "Sahagún; Matrícula de Tributos",
              10, -1, COL,
              "Moneda chica del ticitl."),
        };
    }

    // ────────────────────────────────────────────────────────

    [MenuItem("Mictlan/Herbario/Crear catálogo inicial")]
    public static void Crear()
    {
        AsegurarCarpeta(CARPETA_FLORA);

        var catalogo = AssetDatabase.LoadAssetAtPath<HerbarioCatalogo>(RUTA_CATALOGO);
        if (catalogo == null)
        {
            catalogo = ScriptableObject.CreateInstance<HerbarioCatalogo>();
            AssetDatabase.CreateAsset(catalogo, RUTA_CATALOGO);
        }
        if (catalogo.especies == null) catalogo.especies = new List<FloraDefinition>();

        int creadas = 0, agregadas = 0;

        foreach (var d in Especies())
        {
            string ruta = CARPETA_FLORA + "/flora_" + d.id + ".asset";
            var f = AssetDatabase.LoadAssetAtPath<FloraDefinition>(ruta);

            if (f == null)
            {
                f = ScriptableObject.CreateInstance<FloraDefinition>();
                f.id = d.id;
                f.nombreNahuatl = d.nahuatl;
                f.glosa = d.glosa;
                f.nombreEspanol = d.espanol;
                f.nombreCientifico = d.cientifico;
                f.descripcion = d.descripcion;
                f.notaTicitl = d.ticitl;
                f.fuente = d.fuente;
                f.muestrasParaEstudiar = Mathf.Max(1, d.muestras);
                f.nivelPrincipal = d.nivel;
                f.roles = d.roles;
                f.notaDiseno = d.diseno;
                AssetDatabase.CreateAsset(f, ruta);
                creadas++;
            }

            if (!catalogo.especies.Contains(f))
            {
                catalogo.especies.Add(f);
                agregadas++;
            }
        }

        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalogo;
        EditorGUIUtility.PingObject(catalogo);
        Debug.Log("[Herbario] Catálogo listo en " + RUTA_CATALOGO + ": " + creadas +
                  " planta(s) creada(s), " + agregadas + " agregada(s) al catálogo, " +
                  catalogo.Total + " en total.");
    }

    [MenuItem("Mictlan/Herbario/Validar catálogo")]
    public static void Validar()
    {
        var catalogo = AssetDatabase.LoadAssetAtPath<HerbarioCatalogo>(RUTA_CATALOGO);
        if (catalogo == null)
        {
            Debug.LogWarning("[Herbario] No existe " + RUTA_CATALOGO + ". Corre 'Crear catálogo inicial'.");
            return;
        }

        var vistos = new HashSet<string>();
        int problemas = 0, sinIlustracion = 0;

        for (int i = 0; i < catalogo.Total; i++)
        {
            var f = catalogo.especies[i];
            if (f == null) { Debug.LogWarning("[Herbario] Entrada " + i + " vacía en el catálogo."); problemas++; continue; }
            if (string.IsNullOrEmpty(f.id)) { Debug.LogWarning("[Herbario] '" + f.name + "' sin id.", f); problemas++; continue; }
            if (!vistos.Add(f.id)) { Debug.LogWarning("[Herbario] id repetido: " + f.id, f); problemas++; }
            if (f.ilustracion == null) sinIlustracion++;
        }

        Debug.Log("[Herbario] Validación: " + catalogo.Total + " especies, " + problemas +
                  " problema(s), " + sinIlustracion + " sin ilustración.");
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
