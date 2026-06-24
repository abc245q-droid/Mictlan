#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
reparar_conversaciones.py
-------------------------
Repara de forma PERMANENTE los assets de Conversation cuya referencia de
script quedó en {fileID: 0} (causa: el asset se creó cuando la clase
Conversation aún vivía dentro de DialogueData.cs).

Qué hace:
  1. Localiza Conversation.cs.meta dentro de Assets/ y extrae su GUID real.
  2. Busca todos los .asset cuyo m_EditorClassIdentifier sea
     'Assembly-CSharp::Conversation'.
  3. Reescribe su línea m_Script para que apunte al GUID real del script.

Uso (con Unity CERRADO):
    python reparar_conversaciones.py [ruta_a_Assets]
Si no se pasa ruta, usa la carpeta actual.

Trabaja a nivel de bytes en la línea m_Script (ASCII puro), así que NO
altera el resto del archivo ni su codificación (cp1252 / utf-8).
"""
import os
import re
import sys

CLASS_ID = b"Assembly-CSharp::Conversation"
OLD_LINE = b"  m_Script: {fileID: 0}"


def encontrar_guid_conversation(assets_dir):
    for root, _, files in os.walk(assets_dir):
        for name in files:
            if name == "Conversation.cs.meta":
                ruta = os.path.join(root, name)
                with open(ruta, "rb") as f:
                    for linea in f:
                        m = re.match(rb"guid:\s*([0-9a-fA-F]{32})", linea.strip())
                        if m:
                            return m.group(1).decode(), ruta
    return None, None


def reparar(assets_dir):
    guid, ruta_meta = encontrar_guid_conversation(assets_dir)
    if not guid:
        print("ERROR: no se encontró Conversation.cs.meta en", assets_dir)
        print("       Asegúrate de que el archivo Conversation.cs existe y Unity")
        print("       generó su .meta (abre Unity una vez tras crearlo).")
        return 1

    print(f"GUID de Conversation.cs  : {guid}")
    print(f"  (desde {ruta_meta})\n")

    nueva_linea = (
        b"  m_Script: {fileID: 11500000, guid: "
        + guid.encode()
        + b", type: 3}"
    )

    reparados = 0
    revisados = 0
    for root, _, files in os.walk(assets_dir):
        for name in files:
            if not name.endswith(".asset"):
                continue
            ruta = os.path.join(root, name)
            with open(ruta, "rb") as f:
                data = f.read()
            if CLASS_ID not in data:
                continue
            revisados += 1
            if OLD_LINE not in data:
                print(f"  OK (ya estaba bien): {name}")
                continue
            data = data.replace(OLD_LINE, nueva_linea, 1)
            with open(ruta, "wb") as f:
                f.write(data)
            print(f"  REPARADO            : {name}")
            reparados += 1

    print(f"\nAssets de Conversation revisados: {revisados}")
    print(f"Assets reparados                : {reparados}")
    return 0


if __name__ == "__main__":
    base = sys.argv[1] if len(sys.argv) > 1 else "."
    sys.exit(reparar(base))
