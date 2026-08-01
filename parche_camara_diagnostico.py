# -*- coding: utf-8 -*-
"""
Parche 2: CameraLookControl.cs — Autolocalización + advertencias
Ejecutar desde la raíz del proyecto (C:\Dev\Romerito 001).
Funciona igual antes o después del parche de anticipación de caída
(las anclas existen en ambas versiones).
"""
import io, sys

PATH = r"Assets/Scripts/CameraLookControl.cs"

def leer(p):
    for enc in ("utf-8-sig", "cp1252", "latin-1"):
        try:
            with io.open(p, "r", encoding=enc) as f:
                return f.read()
        except UnicodeDecodeError:
            continue
    raise RuntimeError("No se pudo decodificar " + p)

src = leer(PATH)

def reemplazar(texto, ancla, nuevo, nombre):
    if texto.count(ancla) != 1:
        print("FALLO ancla (%s): apariciones = %d" % (nombre, texto.count(ancla)))
        sys.exit(1)
    return texto.replace(ancla, nuevo, 1)

# ── 1) Autolocalización de la CinemachineCamera ───────────────
ancla1 = """    void Start()
    {
        if (virtualCamera == null) return;
"""
nuevo1 = """    void Start()
    {
        // Autolocalización: si la referencia no quedó asignada en esta
        // escena, buscamos la CinemachineCamera activa (mismo patrón que
        // CameraFinder). Así el peek sobrevive entre niveles aunque el
        // Inspector local esté incompleto.
        if (virtualCamera == null)
            virtualCamera = FindFirstObjectByType<CinemachineCamera>();

        if (virtualCamera == null)
        {
            Debug.LogWarning(
                "[CameraLookControl] No hay ninguna CinemachineCamera en la " +
                "escena. Peek y anticipación de caída DESACTIVADOS.", this);
            return;
        }
"""
src = reemplazar(src, ancla1, nuevo1, "autolocalizacion")

# ── 2) Advertencia si falta el PositionComposer ───────────────
ancla2 = """        composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
        if (composer != null)
"""
nuevo2 = """        composer = virtualCamera.GetComponent<CinemachinePositionComposer>();

        if (composer == null)
        {
            Debug.LogWarning(
                "[CameraLookControl] La CinemachineCamera '" + virtualCamera.name +
                "' NO tiene CinemachinePositionComposer. Peek y anticipación de " +
                "caída DESACTIVADOS en esta escena. Compárala con la cámara del " +
                "Nivel 0 (instancia del prefab CinemachineCamera).", virtualCamera);
        }

        if (composer != null)
"""
src = reemplazar(src, ancla2, nuevo2, "advertencia composer")

# ── Verificación de balance de llaves ─────────────────────────
if src.count("{") != src.count("}"):
    print("FALLO: llaves desbalanceadas (%d abre, %d cierra)" % (src.count("{"), src.count("}")))
    sys.exit(1)

with io.open(PATH, "w", encoding="utf-8-sig", newline="\n") as f:
    f.write(src)

print("OK - parche de diagnostico aplicado. Llaves: %d/%d" % (src.count("{"), src.count("}")))
