using UnityEngine;

// ============================================================
//  RomeritoBodySprites — sprite del cuerpo por estado
// ============================================================
//
//  Extiende el control del sprite del cuerpo con tres estados que
//  el Animator existente aún no maneja: salto, dash e idle-impaciente.
//  Coexiste con el Animator sin romper las animaciones actuales de
//  idle (respirar) y walk.
//
//  CÓMO COEXISTE CON EL ANIMATOR:
//    • En cada LateUpdate se decide qué estado toca. Si es uno
//      manejado por este componente (Salto/Dash/Impaciente), APAGA
//      `bodyAnimator.enabled` y escribe `bodySprite.sprite` a mano.
//    • Si el estado toca al Animator (idle o walk), REACTIVA
//      `bodyAnimator.enabled` y él pinta el sprite según sus
//      parámetros isGrounded / isDashing / Speed (ya seteados por
//      RomeritoMovement).
//    • La transición es un solo write por cambio de estado — nunca
//      pisa al Animator frame a frame estando ambos activos.
//
//  PRIORIDAD (LateUpdate):
//    1) Dash activo   → sprite Dash
//    2) En el aire    → sprite Salto
//    3) Con input     → ceder al Animator (walk/idle)
//    4) Quieto ≥ N s  → animación impaciente
//    5) Fallback      → ceder al Animator (idle)
//
//  COMPATIBILIDAD CON AttackVFXController:
//    Cuando el ataque aéreo hace `bodySprite.enabled = false`, este
//    script sigue escribiendo `.sprite`; queda listo el sprite
//    correcto para el momento en que el ataque reactiva el body.
//
//  SETUP:
//    1. Añadir este componente al GameObject de Romerito.
//    2. Body Sprite    → el SpriteRenderer del cuerpo (el mismo que
//                        ya asignaste en AttackVFXController.bodySprite).
//    3. Body Animator  → auto-detectado si se deja vacío.
//    4. Movement       → auto-detectado si se deja vacío.
//    5. Asignar Sprite Salto, Sprite Dash, y opcionalmente Sprites
//       Impaciente[] con los frames de la secuencia.
//
// ============================================================

public class RomeritoBodySprites : MonoBehaviour
{
    [Header("Referencias (auto-detectadas si se dejan vacías)")]
    [Tooltip("SpriteRenderer del cuerpo. El mismo que asignaste en " +
             "AttackVFXController.bodySprite.")]
    public SpriteRenderer bodySprite;

    [Tooltip("Animator del cuerpo. Se apaga temporalmente cuando un estado " +
             "manual (salto/dash/impaciente) toma control, y se rehabilita " +
             "al volver a idle/walk.")]
    public Animator bodyAnimator;

    [Tooltip("Ref al RomeritoMovement para leer isGrounded.")]
    public RomeritoMovement movement;

    [Header("Estado: Salto / Caída (aire)")]
    [Tooltip("Sprite mientras Romerito NO está en el suelo. Cubre salto, " +
             "caída, doble-salto y wall-jump. Si se deja vacío, se cede al " +
             "Animator también en el aire.")]
    public Sprite spriteSalto;

    [Header("Estado: Dash")]
    [Tooltip("Sprite durante todo el trayecto del dash. Si se deja vacío, " +
             "se cede al Animator también en dash.")]
    public Sprite spriteDash;

    [Header("Estado: Idle Impaciente")]
    [Tooltip("Secuencia de sprites en loop cuando Romerito lleva un rato " +
             "quieto en el suelo sin recibir inputs. Déjalo vacío (o de " +
             "tamaño 0) para desactivar la mecánica.")]
    public Sprite[] spritesImpaciente;

    [Tooltip("Segundos entre frames de la animación impaciente.")]
    [Range(0.05f, 1f)] public float delayFrameImpaciente = 0.15f;

    [Tooltip("Segundos de quietud sin inputs antes de arrancar la animación.")]
    public float tiempoAntesDeImpaciente = 4f;

    [Header("Input")]
    [Tooltip("Umbral de módulo del stick por debajo del cual se considera 'sin input' " +
             "para el temporizador de la animación impaciente. 0.1 va bien con analógico.")]
    [Range(0f, 0.5f)] public float deadzoneStick = 0.1f;

    // ── Estado interno ─────────────────────────────────────
    private float tiempoQuieto = 0f;
    private int idxImpaciente = 0;
    private float timerFrameImpaciente = 0f;
    private bool animatorApagadoPorMi = false;

    // ── Unity ──────────────────────────────────────────────
    void Start()
    {
        if (bodySprite == null) bodySprite = GetComponent<SpriteRenderer>();
        if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
        if (movement == null) movement = GetComponent<RomeritoMovement>();

        if (bodySprite == null)
            Debug.LogWarning("[RomeritoBodySprites] Falta bodySprite. Component desactivado.");
        if (movement == null)
            Debug.LogWarning("[RomeritoBodySprites] Falta RomeritoMovement. Component desactivado.");
    }

    // LateUpdate para correr DESPUÉS del Animator y de RomeritoMovement,
    // así lo que decidamos aquí queda como última palabra del frame.
    void LateUpdate()
    {
        if (bodySprite == null || movement == null) return;

        bool enDash = LeerDashEnAnimator();
        bool grounded = movement.isGrounded;
        bool haInput = HayInputAlgun();

        // 1) Dash activo.
        if (enDash && spriteDash != null)
        {
            TomarControl(spriteDash);
            ResetImpaciente();
            return;
        }

        // 2) En el aire.
        if (!grounded && spriteSalto != null)
        {
            TomarControl(spriteSalto);
            ResetImpaciente();
            return;
        }

        // 3) Con input (movimiento o acción) → Animator maneja walk/idle.
        if (haInput)
        {
            CederAlAnimator();
            ResetImpaciente();
            return;
        }

        // 4) En suelo, sin input: contar quietud y correr impaciente si aplica.
        if (spritesImpaciente != null && spritesImpaciente.Length > 0)
        {
            tiempoQuieto += Time.deltaTime;
            if (tiempoQuieto >= tiempoAntesDeImpaciente)
            {
                timerFrameImpaciente += Time.deltaTime;
                if (timerFrameImpaciente >= delayFrameImpaciente)
                {
                    timerFrameImpaciente = 0f;
                    idxImpaciente = (idxImpaciente + 1) % spritesImpaciente.Length;
                }
                TomarControl(spritesImpaciente[idxImpaciente]);
                return;
            }
        }

        // 5) Fallback: idle a cargo del Animator (respirar).
        CederAlAnimator();
    }

    // ── Control del Animator ───────────────────────────────
    private void TomarControl(Sprite s)
    {
        if (bodyAnimator != null && bodyAnimator.enabled)
        {
            bodyAnimator.enabled = false;
            animatorApagadoPorMi = true;
        }
        if (s != null && bodySprite.sprite != s)
            bodySprite.sprite = s;
    }

    private void CederAlAnimator()
    {
        if (bodyAnimator != null && animatorApagadoPorMi)
        {
            bodyAnimator.enabled = true;
            animatorApagadoPorMi = false;
        }
    }

    private void ResetImpaciente()
    {
        tiempoQuieto = 0f;
        timerFrameImpaciente = 0f;
        idxImpaciente = 0;
    }

    // ── Lectura de estado ──────────────────────────────────
    // RomeritoMovement.isDashing es private, pero el Animator ya expone el
    // valor porque RomeritoMovement.StartDash hace SetBool("isDashing", ...).
    // Reutilizamos ese parámetro para no tocar la interfaz de Movement.
    private bool LeerDashEnAnimator()
    {
        if (bodyAnimator == null) return false;
        // GetBool con parámetro inexistente devuelve false + warning en editor.
        return bodyAnimator.GetBool("isDashing");
    }

    private bool HayInputAlgun()
    {
        // Stick / teclado horizontal o vertical.
        if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > deadzoneStick) return true;
        if (Mathf.Abs(Input.GetAxisRaw("Vertical"))   > deadzoneStick) return true;

        // Botones de acción típicos.
        if (Input.GetButton("Jump"))  return true;
        if (Input.GetButton("Fire1")) return true;
        if (Input.GetButton("Dash"))  return true;
        // "Curar" (Y) también cuenta como input — mantener Y durante el
        // canal de curación no debe dejar correr el timer de impaciente.
        if (Input.GetButton("Curar")) return true;

        return false;
    }
}
