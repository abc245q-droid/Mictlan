using UnityEngine;

public class PersistentPlayer : MonoBehaviour
{
    public static PersistentPlayer instance;

    void Awake()
    {
        // Si no hay un Romerito, YO soy Romerito
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        // Si ya hay un Romerito (porque venimos de otra escena), me destruyo a mí mismo
        // para que no haya duplicados.
        else
        {
            Destroy(gameObject);
        }
    }
}