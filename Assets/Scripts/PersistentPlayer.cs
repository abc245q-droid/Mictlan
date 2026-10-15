using UnityEngine;

// [Z-FIX] Corre antes que el resto: la guardia de Z debe ganarle a
// CinemachineBrain en LateUpdate para que la cámara nunca vea el frame malo.
[DefaultExecutionOrder(-1000)]
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

    // ══════════════════════════════════════════════════════════════
    //  [Z-FIX] Guardia de profundidad
    //
    //  Cinemachine coloca la cámara en  Romerito.z − CameraDistance.
    //  Si Romerito queda en Z ≠ 0 (un spawn o entrada con Z, un prefab
    //  arrastrado en vista 3D), la cámara termina DELANTE del Tilemap
    //  (Z = 0) y el mundo desaparece en Game, aunque las colisiones 2D
    //  sigan funcionando. Aquí lo devolvemos a Z = 0 cada frame y
    //  avisamos UNA vez para poder encontrar al culpable.
    // ══════════════════════════════════════════════════════════════

    private Transform cuerpo;
    private bool avisadoZ = false;

    void LateUpdate()
    {
        if (instance != this) return;

        FijarZ(transform);

        if (cuerpo == null)
        {
            RomeritoHealth h = GetComponentInChildren<RomeritoHealth>();
            cuerpo = (h != null) ? h.transform : transform;
        }
        if (cuerpo != transform) FijarZ(cuerpo);
    }

    void FijarZ(Transform t)
    {
        Vector3 p = t.position;
        if (Mathf.Abs(p.z) < 0.0001f) return;

        if (!avisadoZ)
        {
            Debug.LogWarning($"[PersistentPlayer] '{t.name}' tenía Z = {p.z:F3}. " +
                             "Corregido a 0. Revisa la Z del último SceneEntrance / " +
                             "EntradaVertical / spawn por el que pasó Romerito.");
            avisadoZ = true;
        }
        p.z = 0f;
        t.position = p;
    }
}