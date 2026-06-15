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

    // --- MUNDO (Para objetos únicos destruidos o recogidos) ---
    // Guardaremos los IDs (nombres) de los objetos que ya no deben aparecer
    public List<string> collectedItems = new List<string>();

    // Constructor vacío
    public PlayerData() { }
}