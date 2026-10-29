using UnityEngine;
using Unity.Cinemachine;

// ============================================================
//  TemblorCamara — Temblores de cámara de mundo (Cinemachine Impulse)
// ============================================================
//
//  Utilidad estática para sacudir la cámara desde cualquier sistema
//  (sellos de sala, derrumbes, golpes de jefe...) sin setup:
//
//      TemblorCamara.Sacudir(Vector2.down * 0.5f, 0.45f);
//
//  • Crea sola una fuente de impulso persistente la primera vez.
//    SesionMictlan la destruye con el resto de objetos persistentes al
//    volver al menú; se recrea sola al siguiente uso.
//  • Añade CinemachineImpulseListener (canal 1) a las CinemachineCamera
//    que no lo tengan — el mismo canal que usa ImpactoDano.
//  • Usa tiempo NO escalado (IgnoreTimeScale), igual que ImpactoDano.
// ============================================================

public static class TemblorCamara
{
    const int Canal = 1;
    static CinemachineImpulseSource fuente;

    /// <summary>
    /// Sacude la cámara. 'velocidad' da dirección y fuerza (p. ej.
    /// Vector2.down * 0.5f para un golpe seco vertical).
    /// </summary>
    public static void Sacudir(Vector2 velocidad, float duracion,
        CinemachineImpulseDefinition.ImpulseShapes forma = CinemachineImpulseDefinition.ImpulseShapes.Bump)
    {
        if (velocidad == Vector2.zero || duracion <= 0f) return;

        AsegurarFuente();
        AsegurarListeners();

        fuente.ImpulseDefinition.ImpulseShape = forma;
        fuente.ImpulseDefinition.ImpulseDuration = duracion;

        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;

        Vector3 origen = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        fuente.GenerateImpulseAtPositionWithVelocity(origen, velocidad);
    }

    static void AsegurarFuente()
    {
        if (fuente != null) return;

        var go = new GameObject("[TemblorCamara]");
        Object.DontDestroyOnLoad(go);
        fuente = go.AddComponent<CinemachineImpulseSource>();

        // Creada por código: Reset() NO corre, así que la definición se
        // llena a mano (mismo criterio que ImpactoDano).
        fuente.ImpulseDefinition = new CinemachineImpulseDefinition
        {
            ImpulseChannel = Canal,
            ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
            CustomImpulseShape = new AnimationCurve(),
            ImpulseDuration = 0.4f,
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
            DissipationDistance = 100f,
            DissipationRate = 0.25f,
            PropagationSpeed = 343f
        };
    }

    static void AsegurarListeners()
    {
        foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
        {
            if (cam.GetComponent<CinemachineImpulseListener>() != null) continue;

            var l = cam.gameObject.AddComponent<CinemachineImpulseListener>();
            l.ApplyAfter = CinemachineCore.Stage.Noise;
            l.ChannelMask = Canal;
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
}
