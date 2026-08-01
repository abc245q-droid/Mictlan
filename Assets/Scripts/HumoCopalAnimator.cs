using UnityEngine;

// ============================================================
//  HumoCopalAnimator — animación por swap del Humo de Copal
// ============================================================
//
//  Fundamento (lore): el copal no es humo quieto — se enrosca,
//  respira y se deshace. Esta animación le da vida a la columna
//  de sahumerio de la Barrera de Copal (Favor de Huehueteotl).
//
//  QUÉ HACE:
//    • Cicla una secuencia de sprites sobre el MISMO SpriteRenderer
//      del humo, a un ritmo configurable (segundos entre frames).
//    • SOLO toca 'sprite'. NUNCA toca 'color'/alpha: de eso se
//      encarga BarreraCopal (fade denso ↔ tenue). Así ambos scripts
//      conviven sin pisarse — uno dueño del frame, otro del alpha.
//
//  POR QUÉ ES AUTÓNOMO (cero cambios a BarreraCopal):
//    BarreraCopal ya hace spriteHumo.gameObject.SetActive(true/false).
//    Si este componente vive EN ESE MISMO GameObject (el hijo
//    "HumoCopal"), arranca y se detiene solo con el SetActive:
//    OnEnable reinicia al primer frame, Update avanza la secuencia,
//    y al apagarse la barrera el Update deja de correr. No hace falta
//    tocar la lógica ya terminada de la barrera.
//
//  SETUP EN UNITY:
//    1. Seleccionar el hijo "HumoCopal" (el del SpriteRenderer que
//       asignaste en BarreraCopal.spriteHumo).
//    2. Add Component → HumoCopalAnimator.
//    3. Arrastrar los frames del humo (SpriteCopal_0..N) al array
//       'frames', EN ORDEN.
//    4. Ajustar 'delayFrame' a gusto (0.10–0.14 s va bien para humo).
//    5. Para humo orgánico, probar 'pingPong = true': recorre
//       0→N→0 y evita el "salto" del último frame al primero.
//
// ============================================================

[RequireComponent(typeof(SpriteRenderer))]
public class HumoCopalAnimator : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("Secuencia de sprites del humo, EN ORDEN. Si está vacío, el " +
             "componente no anima nada (deja el sprite que ya tenga el " +
             "SpriteRenderer) y avisa una vez.")]
    public Sprite[] frames;

    [Header("Ritmo")]
    [Tooltip("Segundos entre frame y frame. Más chico = humo más nervioso.")]
    [Range(0.02f, 0.5f)] public float delayFrame = 0.12f;

    [Header("Modo de recorrido")]
    [Tooltip("false = loop normal (0,1,2,...,N,0,1,...).\n" +
             "true = ping-pong (0,...,N,...,1,0,...): ida y vuelta. " +
             "Recomendado para humo, evita el corte del último al primer frame.")]
    public bool pingPong = false;

    // ── Referencias / estado interno ───────────────────
    private SpriteRenderer sr;
    private int idx = 0;
    private int direccion = 1;   // ping-pong: +1 avanza, -1 retrocede
    private float timer = 0f;
    private bool avisoDado = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    // OnEnable (no Start): BarreraCopal hace SetActive(true) en cada
    // activación, así que aquí reiniciamos la animación limpia.
    void OnEnable()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        if (frames == null || frames.Length == 0)
        {
            if (!avisoDado)
            {
                Debug.LogWarning("[HumoCopalAnimator] Sin frames asignados. " +
                                 "Arrastra los SpriteCopal al array 'frames'. " +
                                 "Por ahora el humo se queda sin animar.");
                avisoDado = true;
            }
            return;
        }

        timer = 0f;
        direccion = 1;
        idx = 0;
        AplicarFrame();
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;

        // Con más de un frame, avanzamos el temporizador. Con uno solo,
        // no hay nada que ciclar pero igual lo dejamos pintado.
        if (frames.Length > 1)
        {
            timer += Time.deltaTime;
            if (timer >= delayFrame)
            {
                timer = 0f;
                Avanzar();
            }
        }

        AplicarFrame();
    }

    private void Avanzar()
    {
        if (!pingPong)
        {
            idx = (idx + 1) % frames.Length;
            return;
        }

        // Ping-pong: rebota en los extremos.
        idx += direccion;
        if (idx >= frames.Length - 1)
        {
            idx = frames.Length - 1;
            direccion = -1;
        }
        else if (idx <= 0)
        {
            idx = 0;
            direccion = 1;
        }
    }

    private void AplicarFrame()
    {
        // Solo tocamos el sprite. El alpha lo maneja BarreraCopal.
        int i = Mathf.Clamp(idx, 0, frames.Length - 1);
        Sprite s = frames[i];
        if (s != null && sr != null && sr.sprite != s)
            sr.sprite = s;
    }
}
