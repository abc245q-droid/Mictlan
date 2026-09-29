using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering.PostProcessing;

// ============================================================
//  ImpactoDano — "Impact frames" al recibir daño
// ============================================================
//
//  Escucha RomeritoHealth.OnDanoRecibido y dispara, en el mismo
//  instante, varias capas cortas que dan peso al golpe:
//
//   1. HIT-STOP     El mundo se congela unas décimas de segundo
//                   (PausaMundo.SolicitarCongelacion — no bloquea input
//                   ni cuenta como pausa de gameplay).
//   2. DESTELLO     Romerito y el Macahuitl se vuelven de un color
//                   sólido (shader Mictlan/SpriteFlash) y se desvanecen.
//   3. VIBRACIÓN    Solo del dibujo, durante la congelación. Colliders
//                   y hurtbox no se mueven.
//   4. CÁMARA       Cinemachine Impulse con IgnoreTimeScale: la cámara
//                   tiembla aunque el tiempo esté congelado.
//   5. MUNDO        El golpe le arranca el color a Mictlán: saturación
//                   a -100 + viñeta fría (Post Processing v2), que se
//                   recupera al terminar la congelación.
//   6. AUDIO        Sonido de golpe opcional (sin filtro) + todo lo
//                   demás amortiguado con pasa-bajos durante los i-frames.
//
//  CASOS ESPECIALES
//   • Pinchos: RomeritoHealth teletransporta DESPUÉS del hit-stop.
//   • Último corazón: congelación más larga; RomeritoHealth.DieRoutine
//     espera la congelación antes de ocultar a Romerito. Desaturación y
//     audio amortiguado se sostienen hasta el respawn (OnRespawn).
//   • Si este componente se desactiva o destruye a mitad de un impacto,
//     libera la congelación y restaura materiales, post y audio.
//
//  SETUP
//   Ninguno obligatorio: RomeritoHealth lo añade solo si falta. Para
//   AJUSTAR valores de forma persistente, añádelo al prefab de Romerito
//   y edítalo ahí. Requisitos que se resuelven solos:
//   • Material de destello: Resources/Mat_SpriteFlash.
//   • CinemachineImpulseListener: se añade a las CinemachineCamera que
//     no lo tengan.
//   • Post: usa el PostProcessLayer de la Main Camera. Si la cámara no
//     tiene uno, esta capa simplemente no se ve.
// ============================================================

[DisallowMultipleComponent]
[RequireComponent(typeof(RomeritoHealth))]
public class ImpactoDano : MonoBehaviour
{
    [Header("Hit-stop (segundos reales)")]
    [Tooltip("Golpe normal. 0.12 s ≈ 7 frames a 60 fps.")]
    public float congelacionGolpe = 0.12f;
    [Tooltip("Pinchos y demás Trampas (antes del teletransporte).")]
    public float congelacionPinchos = 0.15f;
    [Tooltip("Golpe que quita el último corazón.")]
    public float congelacionLetal = 0.30f;

    [Header("Destello del sprite")]
    public bool usarDestello = true;
    [Tooltip("Material con shader Mictlan/SpriteFlash. Vacío = Resources/Mat_SpriteFlash.")]
    public Material materialDestello;
    [Tooltip("Blanco hueso frío por defecto: el único color cálido de Mictlán es el fuego.")]
    public Color colorDestello = new Color(0.93f, 0.96f, 1f, 1f);
    [Tooltip("Segundos que tarda el destello en desvanecerse tras la congelación.")]
    public float desvanecerDestello = 0.08f;
    [Tooltip("Sprites que destellan. Vacío = SpriteRenderer raíz + Macahuitl.")]
    public SpriteRenderer[] spritesDestello;

    [Header("Vibración del sprite (solo visual)")]
    [Tooltip("Amplitud en unidades de mundo. Decae a 0 al final de la congelación.")]
    public float amplitudVibracion = 0.06f;

    [Header("Cámara — Cinemachine Impulse")]
    public bool usarImpulso = true;
    public float fuerzaImpulso = 0.35f;
    public float fuerzaImpulsoLetal = 0.7f;
    [Tooltip("Solo aplica a la fuente creada automáticamente.")]
    public float duracionImpulso = 0.3f;

    [Header("Mundo — desaturación y viñeta (Post Processing v2)")]
    public bool usarPost = true;
    [Range(-100f, 0f)] public float saturacion = -100f;
    [Range(0f, 1f)] public float intensidadVineta = 0.35f;
    public Color colorVineta = new Color(0.02f, 0.03f, 0.08f, 1f);
    [Tooltip("Segundos para recuperar el color tras la congelación.")]
    public float recuperacionPost = 0.4f;

    [Header("Audio")]
    [Tooltip("Sonido seco del golpe. No pasa por el pasa-bajos.")]
    public AudioClip sonidoGolpe;
    [Range(0f, 1f)] public float volumenGolpe = 1f;
    [Tooltip("Amortigua música y efectos durante los i-frames (como Hollow Knight).")]
    public bool amortiguarAudio = true;
    [Tooltip("Frecuencia de corte del pasa-bajos mientras dura el golpe (Hz).")]
    public float cortePasaBajos = 900f;
    [Tooltip("Segundos de la rampa de salida del amortiguado.")]
    public float rampaAudio = 0.25f;

    // ── Estado ───────────────────────────────────────────────

    private const float CORTE_ABIERTO = 22000f;

    private static readonly int ID_FlashAmount = Shader.PropertyToID("_FlashAmount");
    private static readonly int ID_FlashColor  = Shader.PropertyToID("_FlashColor");
    private static readonly int ID_ShakeOffset = Shader.PropertyToID("_ShakeOffset");

    private RomeritoHealth health;
    private MaterialPropertyBlock mpb;

    private Coroutine rutinaImpacto;
    private bool congelacionTomada;

    // Destello
    private SpriteRenderer[] sprites;
    private Material[] materialesOriginales;
    private bool destelloActivo;

    // Cámara
    private CinemachineImpulseSource fuenteImpulso;

    // Post
    private PostProcessVolume volumen;
    private ColorGrading gradacion;
    private Vignette vineta;
    private Coroutine rutinaPost;
    private bool postSostenido;
    private bool avisoSinPost;

    // Audio
    private AudioLowPassFilter filtro;
    private AudioSource fuenteGolpe;
    private Coroutine rutinaAudio;
    private bool audioSostenido;

    // ── Unity ────────────────────────────────────────────────

    void Awake()
    {
        health = GetComponent<RomeritoHealth>();
        mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (health == null) health = GetComponent<RomeritoHealth>();
        health.OnDanoRecibido += Reproducir;
        health.OnRespawn += AlReaparecer;
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnDanoRecibido -= Reproducir;
            health.OnRespawn -= AlReaparecer;
        }

        // Las corrutinas mueren con el componente: soltar TODO lo que
        // tuvieran tomado, o el mundo se quedaría congelado.
        rutinaImpacto = null;
        rutinaPost = null;
        rutinaAudio = null;
        SoltarCongelacion();
        RestaurarDestello();
        ApagarPost();
        ApagarAudio();
    }

    void OnDestroy()
    {
        // QuickVolume usa HideAndDontSave: sobrevive a los cambios de escena
        // y hay que destruirlo a mano.
        if (volumen != null)
            RuntimeUtilities.DestroyVolume(volumen, true, true);
    }

    // ── Entrada ──────────────────────────────────────────────

    void Reproducir(RomeritoHealth.InfoDano info)
    {
        if (!isActiveAndEnabled) return;

        if (rutinaImpacto != null)
        {
            StopCoroutine(rutinaImpacto);
            SoltarCongelacion();
            RestaurarDestello();
        }

        float congelacion = info.letal ? congelacionLetal
                          : info.porTrampa ? congelacionPinchos
                          : congelacionGolpe;

        rutinaImpacto = StartCoroutine(RutinaImpacto(info, Mathf.Max(0f, congelacion)));
    }

    void AlReaparecer()
    {
        // Fin del golpe letal: devolver color y sonido al mundo.
        if (postSostenido)
        {
            postSostenido = false;
            if (volumen != null && volumen.enabled)
            {
                if (rutinaPost != null) StopCoroutine(rutinaPost);
                rutinaPost = StartCoroutine(RecuperarPost());
            }
        }
        audioSostenido = false;
    }

    // ── Secuencia principal ──────────────────────────────────

    IEnumerator RutinaImpacto(RomeritoHealth.InfoDano info, float congelacion)
    {
        if (congelacion > 0f)
        {
            PausaMundo.SolicitarCongelacion();
            congelacionTomada = true;
        }

        SonarGolpe();
        LanzarImpulso(info.letal);
        IniciarPost(info.letal);
        IniciarAudio(info.letal);
        ActivarDestello();

        // 1. Congelación: destello pleno + vibración que decae.
        float t = 0f;
        while (t < congelacion)
        {
            t += Time.unscaledDeltaTime;
            float decae = 1f - Mathf.Clamp01(t / congelacion);
            AplicarDestello(1f, Random.insideUnitCircle * (amplitudVibracion * decae));
            yield return null;
        }

        SoltarCongelacion();

        // 2. El mundo ya corre: el destello se desvanece.
        float d = 0f;
        while (d < desvanecerDestello)
        {
            d += Time.unscaledDeltaTime;
            AplicarDestello(1f - Mathf.Clamp01(d / desvanecerDestello), Vector2.zero);
            yield return null;
        }

        RestaurarDestello();
        rutinaImpacto = null;
    }

    void SoltarCongelacion()
    {
        if (!congelacionTomada) return;
        congelacionTomada = false;
        PausaMundo.LiberarCongelacion();
    }

    // ── 2-3. Destello + vibración ────────────────────────────

    void ActivarDestello()
    {
        if (!usarDestello || destelloActivo) return;

        Material mat = materialDestello != null
            ? materialDestello
            : (materialDestello = Resources.Load<Material>("Mat_SpriteFlash"));
        if (mat == null)
        {
            Debug.LogWarning("[ImpactoDano] No se encontró Resources/Mat_SpriteFlash — sin destello.", this);
            usarDestello = false;
            return;
        }

        ResolverSprites();
        materialesOriginales = new Material[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            materialesOriginales[i] = sprites[i].sharedMaterial;
            sprites[i].sharedMaterial = mat;
        }
        destelloActivo = true;
    }

    void AplicarDestello(float cantidad, Vector2 desplazamientoMundo)
    {
        if (!destelloActivo) return;

        foreach (SpriteRenderer sr in sprites)
        {
            if (sr == null) continue;
            sr.GetPropertyBlock(mpb);
            mpb.SetFloat(ID_FlashAmount, cantidad);
            mpb.SetColor(ID_FlashColor, colorDestello);
            // El shader desplaza en espacio de objeto: convertir por renderer
            // (escala y flip propios de Romerito y del Macahuitl).
            Vector3 local = sr.transform.InverseTransformVector(desplazamientoMundo);
            mpb.SetVector(ID_ShakeOffset, new Vector4(local.x, local.y, 0f, 0f));
            sr.SetPropertyBlock(mpb);
        }
    }

    void RestaurarDestello()
    {
        if (!destelloActivo) return;

        AplicarDestello(0f, Vector2.zero);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            sprites[i].sharedMaterial = materialesOriginales[i];
        }
        destelloActivo = false;
    }

    void ResolverSprites()
    {
        if (spritesDestello != null && spritesDestello.Length > 0)
        {
            sprites = spritesDestello;
            return;
        }

        SpriteRenderer raiz = GetComponent<SpriteRenderer>();
        RomeritoCombat combate = GetComponent<RomeritoCombat>();
        SpriteRenderer arma = combate != null ? combate.macahuitlSprite : null;

        sprites = (arma != null && arma != raiz)
            ? new[] { raiz, arma }
            : new[] { raiz };
    }

    // ── 4. Cámara ────────────────────────────────────────────

    void LanzarImpulso(bool letal)
    {
        if (!usarImpulso) return;

        AsegurarListeners();

        if (fuenteImpulso == null)
        {
            fuenteImpulso = GetComponent<CinemachineImpulseSource>();
            if (fuenteImpulso == null)
            {
                fuenteImpulso = gameObject.AddComponent<CinemachineImpulseSource>();
                // Creada por código: Reset() NO corre, y los valores por
                // defecto (Legacy + Custom sin curva) no generan nada.
                fuenteImpulso.ImpulseDefinition = new CinemachineImpulseDefinition
                {
                    ImpulseChannel = 1,
                    ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                    CustomImpulseShape = new AnimationCurve(),
                    ImpulseDuration = duracionImpulso,
                    ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                    DissipationDistance = 100f,
                    DissipationRate = 0.25f,
                    PropagationSpeed = 343f
                };
            }
        }

        // La cámara tiembla aunque el tiempo esté congelado (hit-stop).
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;

        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.down;
        float fuerza = letal ? fuerzaImpulsoLetal : fuerzaImpulso;
        fuenteImpulso.GenerateImpulseWithVelocity(dir * fuerza);
    }

    static void AsegurarListeners()
    {
        foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            if (cam.GetComponent<CinemachineImpulseListener>() != null) continue;

            var l = cam.gameObject.AddComponent<CinemachineImpulseListener>();
            // Creado por código: Reset() NO corre — con ChannelMask = 0
            // no escucharía ningún impulso. Valores de Reset():
            l.ApplyAfter = CinemachineCore.Stage.Noise;
            l.ChannelMask = 1;
            l.Gain = 1f;
            l.Use2DDistance = true;
            l.UseCameraSpace = true;
            l.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            l.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
            {
                AmplitudeGain = 1f,
                FrequencyGain = 1f,
                Duration = 1f
            };
        }
    }

    // ── 5. Mundo: desaturación + viñeta ──────────────────────

    bool AsegurarVolumen()
    {
        if (volumen != null) return true;

        Camera cam = Camera.main;
        PostProcessLayer capa = cam != null ? cam.GetComponent<PostProcessLayer>() : null;
        int mascara = capa != null ? capa.volumeLayer.value : 0;
        if (mascara == 0)
        {
            if (!avisoSinPost)
            {
                avisoSinPost = true;
                Debug.Log("[ImpactoDano] La Main Camera no tiene PostProcessLayer con " +
                          "volume layer — sin desaturación en esta escena.");
            }
            return false;
        }

        int layer = 0;
        while (((mascara >> layer) & 1) == 0) layer++;

        gradacion = ScriptableObject.CreateInstance<ColorGrading>();
        gradacion.enabled.Override(true);
        gradacion.saturation.Override(saturacion);

        vineta = ScriptableObject.CreateInstance<Vignette>();
        vineta.enabled.Override(true);
        vineta.intensity.Override(intensidadVineta);
        vineta.color.Override(colorVineta);
        vineta.smoothness.Override(0.45f);

        volumen = PostProcessManager.instance.QuickVolume(layer, 100f, gradacion, vineta);
        volumen.weight = 0f;
        volumen.enabled = false;
        return true;
    }

    void IniciarPost(bool letal)
    {
        if (!usarPost || !AsegurarVolumen()) return;

        // Por si se ajustaron en el Inspector durante Play.
        gradacion.saturation.value = saturacion;
        vineta.intensity.value = intensidadVineta;
        vineta.color.value = colorVineta;

        if (rutinaPost != null) StopCoroutine(rutinaPost);
        volumen.enabled = true;
        volumen.weight = 1f;

        postSostenido = letal;
        if (!letal)
            rutinaPost = StartCoroutine(RecuperarPost());
    }

    IEnumerator RecuperarPost()
    {
        // El color vuelve cuando el mundo vuelve a moverse.
        while (PausaMundo.Congelado) yield return null;

        float t = 0f;
        while (t < recuperacionPost)
        {
            t += Time.unscaledDeltaTime;
            if (volumen == null) yield break;
            volumen.weight = 1f - Mathf.Clamp01(t / recuperacionPost);
            yield return null;
        }
        ApagarPost();
        rutinaPost = null;
    }

    void ApagarPost()
    {
        if (volumen == null) return;
        volumen.weight = 0f;
        volumen.enabled = false;
    }

    // ── 6. Audio ─────────────────────────────────────────────

    void SonarGolpe()
    {
        if (sonidoGolpe == null) return;

        if (fuenteGolpe == null)
        {
            fuenteGolpe = gameObject.AddComponent<AudioSource>();
            fuenteGolpe.playOnAwake = false;
            fuenteGolpe.spatialBlend = 0f;
            // Nítido aunque el resto del mundo quede amortiguado.
            fuenteGolpe.bypassListenerEffects = true;
        }
        fuenteGolpe.PlayOneShot(sonidoGolpe, volumenGolpe);
    }

    void IniciarAudio(bool letal)
    {
        if (!amortiguarAudio) return;

        AudioListener oyente = FindFirstObjectByType<AudioListener>();
        if (oyente == null) return;

        // El pasa-bajos solo filtra la mezcla completa si vive junto al
        // AudioListener. La cámara puede cambiar entre escenas.
        if (filtro == null || filtro.gameObject != oyente.gameObject)
        {
            filtro = oyente.GetComponent<AudioLowPassFilter>();
            if (filtro == null)
            {
                filtro = oyente.gameObject.AddComponent<AudioLowPassFilter>();
                filtro.cutoffFrequency = CORTE_ABIERTO;
                filtro.enabled = false;
            }
        }

        if (rutinaAudio != null) StopCoroutine(rutinaAudio);
        audioSostenido = letal;
        rutinaAudio = StartCoroutine(RutinaAudio(letal ? -1f : health.iFramesDuration));
    }

    IEnumerator RutinaAudio(float mantener)
    {
        filtro.enabled = true;
        yield return RampaCorte(filtro.cutoffFrequency, cortePasaBajos, 0.05f);

        if (mantener < 0f)
        {
            while (audioSostenido) yield return null;   // hasta el respawn
        }
        else
        {
            yield return new WaitForSecondsRealtime(mantener);
        }

        yield return RampaCorte(cortePasaBajos, CORTE_ABIERTO, rampaAudio);
        ApagarAudio();
        rutinaAudio = null;
    }

    IEnumerator RampaCorte(float desde, float hasta, float duracion)
    {
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            if (filtro == null) yield break;
            // Interpolación logarítmica: el oído percibe la frecuencia así.
            float k = Mathf.Clamp01(t / duracion);
            filtro.cutoffFrequency = Mathf.Exp(Mathf.Lerp(Mathf.Log(desde), Mathf.Log(hasta), k));
            yield return null;
        }
        if (filtro != null) filtro.cutoffFrequency = hasta;
    }

    void ApagarAudio()
    {
        if (filtro == null) return;
        filtro.cutoffFrequency = CORTE_ABIERTO;
        filtro.enabled = false;
    }
}
