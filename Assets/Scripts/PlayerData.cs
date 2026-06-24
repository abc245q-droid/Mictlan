using System.Collections.Generic;

[System.Serializable]
public class PlayerData
{
    // --- POSICIÓN ---
    public float positionX;
    public float positionY;
    public string currentScene; // Nombre de la escena actual

    // --- HABILIDADES DE MOVIMIENTO ---
    public bool unlockDoubleJump;
    public bool unlockRun;
    public bool unlockWallClimb;
    public bool unlockWallJump;
    public bool unlockDash;
    public bool tieneDonDeTlacua = false;
    public bool tieneXolo = false;            // Compañero Xolo (gate de agua, Nivel 2)

    // --- COMBATE ---
    // ── Favores de los Dioses ──────────────────
    public bool tieneMacuahuitl = false;
    public bool unlockHuehueteotl = false;   // ← AÑADIR
    public bool unlockTlaloc = false;   // ← AÑADIR
    public bool unlockTepeyollotl = false;   // ← AÑADIR

    // --- TONALLI ---
    // currentTonalli NO se guarda — se reinicia al morir/respawnear (igual que
    // el Alma en Hollow Knight: la ganás de nuevo en combate). Solo persiste
    // la ampliación de capacidad acumulada (Fragmento de Turquesa, +15% c/u).
    public float bonusCapacidadTonalli = 0f;

    // --- ECONOMÍA ---
    public int cacao;
    public int tajaderas;


    // --- CÓDICE / MAPA (El Códice de Romerito) ---
    // Herramientas y pigmentos: banderas de una sola vez para todo el juego.
    public bool tienePincel = false;     // Estuche de Tlacuilo: enciende la capacidad de mapear
    public bool tieneRojo = false;       // nocheztli  → Cihuacallis + viaje rápido
    public bool tieneAmarillo = false;   // zacatlaxcalli → tesoros y coleccionables
    public bool tieneAzul = false;       // azul maya  → puertas selladas
    public bool tieneVerde = false;      // matlalin   → Pochtecah y altares de dioses

    // Papel (amate): una compra por nivel. Guardamos los levelId con papel.
    public List<int> papelComprado = new List<int>();

    // Estado de las salas (roomId con prefijo "L{n}_").
    public List<string> salasVisitadas = new List<string>();  // Estado 2: Borrador de Tonalli
    public List<string> salasAsentadas = new List<string>();  // Estado 3: asentadas al fuego



    // --- MUNDO (Para objetos únicos destruidos o recogidos) ---
    // Guardaremos los IDs (nombres) de los objetos que ya no deben aparecer
    public List<string> collectedItems = new List<string>();

    // --- PROGRESO DEL MUNDO (Cihuacallis, Braseros, etc.) ---
    // Solo UN Cihuacalli está encendido a la vez: el checkpoint actual.
    // Guardamos su ID; al activar otro, el anterior se apaga.
    // Vacío ("") = ningún Cihuacalli encendido todavía.
    public string cihuacalliActivoID = "";

    // Preparado para la próxima sesión de Braseros (estos sí son varios a la vez).
    public List<string> braserosEncendidos = new List<string>();

    // Registra un ID en una lista de progreso del mundo.
    // Devuelve true SOLO si el ID era nuevo (se acaba de añadir) —
    // útil para disparar el evento de "sala completada" una sola vez.
    public bool RegistrarID(List<string> lista, string id)
    {
        if (lista == null || string.IsNullOrEmpty(id)) return false;
        if (lista.Contains(id)) return false;
        lista.Add(id);
        return true;
    }

    // Constructor vacío
    public PlayerData() { }
}