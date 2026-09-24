using System.IO;
using UnityEngine;

// ============================================================
//  SaveSystem — Punto único de verdad del archivo de guardado
// ============================================================
//
//  Antes la ruta "romerito_save.json" estaba escrita a mano en
//  cinco sitios distintos de GameManager01. Cualquier cambio de
//  nombre obligaba a tocarlos todos y un despiste dejaba una
//  partida huérfana en disco.
//
//  Ahora la ruta vive aquí y sólo aquí. El menú principal puede
//  además consultar si hay partida SIN necesidad de que exista
//  un GameManager01 en la escena — eso es lo que permite que
//  MenuPrincipal sea una escena limpia, sin objetos persistentes.
//
//  Ubicación real en Steam Deck (Proton):
//    ~/.steam/steam/steamapps/compatdata/<appid>/pfx/drive_c/
//        users/steamuser/AppData/LocalLow/<Company>/<Producto>/
// ============================================================

public static class SaveSystem
{
    public const string NOMBRE_ARCHIVO = "romerito_save.json";

    public static string Ruta =>
        Path.Combine(Application.persistentDataPath, NOMBRE_ARCHIVO);

    /// <summary>¿Existe una partida guardada en disco?</summary>
    public static bool ExistePartida()
    {
        return File.Exists(Ruta);
    }

    /// <summary>
    /// Lee el save y lo devuelve como PlayerData sin tocar el estado
    /// del juego. Lo usa MainMenu para decidir si "Continuar" está
    /// disponible y para mostrar en qué zona quedó Romerito.
    /// Devuelve null si no hay archivo o si está corrupto.
    /// </summary>
    public static PlayerData LeerPartida()
    {
        if (!ExistePartida()) return null;

        try
        {
            string json = File.ReadAllText(Ruta);
            return JsonUtility.FromJson<PlayerData>(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[SaveSystem] Save ilegible: " + e.Message);
            return null;
        }
    }

    public static void Escribir(PlayerData datos)
    {
        if (datos == null) return;
        File.WriteAllText(Ruta, JsonUtility.ToJson(datos, true));
    }

    public static void Borrar()
    {
        if (File.Exists(Ruta))
        {
            File.Delete(Ruta);
            Debug.Log("[SaveSystem] Partida borrada: " + Ruta);
        }
    }
}
