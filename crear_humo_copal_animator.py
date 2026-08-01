# -*- coding: utf-8 -*-
"""
Crea HumoCopalAnimator.cs (+ .meta) en Assets/Scripts/.

Animador autonomo por swap de sprites para el Humo de Copal de la
Barrera de Copal (Favor de Huehueteotl). NO toca BarreraCopal.cs:
vive en el hijo "HumoCopal" y solo cicla el 'sprite'; el alpha lo
sigue manejando BarreraCopal.

USO:
    Desde la raiz del repo (C:\\Dev\\Romerito 001):
        python crear_humo_copal_animator.py

Convenciones de la casa respetadas:
  - C# en UTF-8 con BOM (utf-8-sig).
  - .meta minimalista de 2 lineas (fileFormatVersion + guid), sin
    newline final, como el resto de scripts del repo. Unity regenera
    el bloque MonoImporter al importar.
  - Verificacion de balance de llaves antes de escribir.
  - No sobrescribe: si el .cs ya existe, aborta.
"""

import os
import uuid

RUTA_CS = os.path.join("Assets", "Scripts", "HumoCopalAnimator.cs")
RUTA_META = RUTA_CS + ".meta"

CODIGO = '''\
using UnityEngine;

// ============================================================
//  HumoCopalAnimator \u2014 animaci\u00f3n por swap del Humo de Copal
// ============================================================
//
//  Fundamento (lore): el copal no es humo quieto \u2014 se enrosca,
//  respira y se deshace. Esta animaci\u00f3n le da vida a la columna
//  de sahumerio de la Barrera de Copal (Favor de Huehueteotl).
//
//  QU\u00c9 HACE:
//    \u2022 Cicla una secuencia de sprites sobre el MISMO SpriteRenderer
//      del humo, a un ritmo configurable (segundos entre frames).
//    \u2022 SOLO toca 'sprite'. NUNCA toca 'color'/alpha: de eso se
//      encarga BarreraCopal (fade denso \u2194 tenue). As\u00ed ambos scripts
//      conviven sin pisarse \u2014 uno due\u00f1o del frame, otro del alpha.
//
//  POR QU\u00c9 ES AUT\u00d3NOMO (cero cambios a BarreraCopal):
//    BarreraCopal ya hace spriteHumo.gameObject.SetActive(true/false).
//    Si este componente vive EN ESE MISMO GameObject (el hijo
//    "HumoCopal"), arranca y se detiene solo con el SetActive:
//    OnEnable reinicia al primer frame, Update avanza la secuencia,
//    y al apagarse la barrera el Update deja de correr. No hace falta
//    tocar la l\u00f3gica ya terminada de la barrera.
//
//  SETUP EN UNITY:
//    1. Seleccionar el hijo "HumoCopal" (el del SpriteRenderer que
//       asignaste en BarreraCopal.spriteHumo).
//    2. Add Component \u2192 HumoCopalAnimator.
//    3. Arrastrar los frames del humo (SpriteCopal_0..N) al array
//       'frames', EN ORDEN.
//    4. Ajustar 'delayFrame' a gusto (0.10\u20130.14 s va bien para humo).
//    5. Para humo org\u00e1nico, probar 'pingPong = true': recorre
//       0\u2192N\u21920 y evita el "salto" del \u00faltimo frame al primero.
//
// ============================================================

[RequireComponent(typeof(SpriteRenderer))]
public class HumoCopalAnimator : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("Secuencia de sprites del humo, EN ORDEN. Si est\u00e1 vac\u00edo, el " +
             "componente no anima nada (deja el sprite que ya tenga el " +
             "SpriteRenderer) y avisa una vez.")]
    public Sprite[] frames;

    [Header("Ritmo")]
    [Tooltip("Segundos entre frame y frame. M\u00e1s chico = humo m\u00e1s nervioso.")]
    [Range(0.02f, 0.5f)] public float delayFrame = 0.12f;

    [Header("Modo de recorrido")]
    [Tooltip("false = loop normal (0,1,2,...,N,0,1,...).\\n" +
             "true = ping-pong (0,...,N,...,1,0,...): ida y vuelta. " +
             "Recomendado para humo, evita el corte del \u00faltimo al primer frame.")]
    public bool pingPong = false;

    // \u2500\u2500 Referencias / estado interno \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500
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
    // activaci\u00f3n, as\u00ed que aqu\u00ed reiniciamos la animaci\u00f3n limpia.
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

        // Con m\u00e1s de un frame, avanzamos el temporizador. Con uno solo,
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
'''


def verificar_llaves(texto):
    abren = texto.count("{")
    cierran = texto.count("}")
    if abren != cierran:
        raise SystemExit(
            "ABORTADO: llaves desbalanceadas -> {{ = %d, }} = %d" % (abren, cierran)
        )
    return abren


def main():
    if not os.path.isdir(os.path.join("Assets", "Scripts")):
        raise SystemExit("ABORTADO: no encuentro Assets/Scripts. "
                         "Corre este script desde la raiz del repo.")

    if os.path.exists(RUTA_CS):
        raise SystemExit("ABORTADO: %s ya existe. "
                         "Este script solo CREA el archivo nuevo." % RUTA_CS)

    n = verificar_llaves(CODIGO)

    # C# con BOM, como el resto de scripts del repo.
    with open(RUTA_CS, "w", encoding="utf-8-sig", newline="\n") as f:
        f.write(CODIGO)

    # .meta minimalista (2 lineas, sin newline final), GUID nuevo.
    guid = uuid.uuid4().hex
    meta = "fileFormatVersion: 2\nguid: " + guid
    with open(RUTA_META, "w", encoding="utf-8", newline="\n") as f:
        f.write(meta)

    print("OK  creado : %s  (llaves balanceadas: %d/%d)" % (RUTA_CS, n, n))
    print("OK  creado : %s  (guid: %s)" % (RUTA_META, guid))
    print("")
    print("Siguiente paso (git):")
    print("  git add Assets/Scripts/HumoCopalAnimator.cs Assets/Scripts/HumoCopalAnimator.cs.meta")
    print('  git commit -m "Anima el Humo de Copal de la Barrera (Favor de Huehueteotl)"')
    print("  git push origin main")
    print("  git log origin/main --oneline -3   # verificar que llego al remoto")


if __name__ == "__main__":
    main()
