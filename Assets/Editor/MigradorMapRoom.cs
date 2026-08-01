using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// ============================================================
//  MigradorMapRoom — Herramienta de Editor
// ============================================================
//
//  Recorre la escena activa y, para cada RoomConfiner con un
//  mapRoomId no vacío, garantiza que exista un MapRoom en el
//  MISMO GameObject con el mismo id. Preserva el trabajo previo
//  al refactor sin tocar prefabs a mano.
//
//  Uso:
//    Menú Unity → Mictlan → Mapa → Migrar mapRoomId a MapRoom (escena activa)
//
//  Notas:
//    • No borra el mapRoomId del RoomConfiner (queda como campo
//      legado). Podés limpiarlo a mano después de verificar en Play.
//    • Marca la escena como modificada para que Ctrl+S guarde.
//
// ============================================================

public static class MigradorMapRoom
{
    [MenuItem("Mictlan/Mapa/Migrar mapRoomId a MapRoom (escena activa)")]
    public static void Migrar()
    {
        var confiners = Object.FindObjectsByType<RoomConfiner>(FindObjectsSortMode.None);
        int migrados = 0;
        int yaEstaban = 0;
        int sinId = 0;

        foreach (var rc in confiners)
        {
            if (string.IsNullOrEmpty(rc.mapRoomId))
            {
                sinId++;
                continue;
            }

            var existente = rc.GetComponent<MapRoom>();
            if (existente != null)
            {
                if (string.IsNullOrEmpty(existente.mapRoomId))
                {
                    Undo.RecordObject(existente, "Copiar mapRoomId a MapRoom existente");
                    existente.mapRoomId = rc.mapRoomId;
                    EditorUtility.SetDirty(existente);
                    migrados++;
                    Debug.Log($"[Migrador] Copiado id '{rc.mapRoomId}' al MapRoom existente en '{RutaJerarquia(rc.transform)}'.", rc);
                }
                else
                {
                    yaEstaban++;
                }
                continue;
            }

            var nuevo = Undo.AddComponent<MapRoom>(rc.gameObject);
            nuevo.mapRoomId = rc.mapRoomId;
            EditorUtility.SetDirty(nuevo);
            migrados++;
            Debug.Log($"[Migrador] Añadido MapRoom con id '{rc.mapRoomId}' en '{RutaJerarquia(rc.transform)}'.", rc);
        }

        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log($"[Migrador] Escena '{escena.name}': RoomConfiners revisados={confiners.Length}, " +
                  $"migrados/actualizados={migrados}, ya existían={yaEstaban}, sin mapRoomId={sinId}. " +
                  "Recordá guardar la escena (Ctrl+S).");
    }

    [MenuItem("Mictlan/Mapa/Limpiar mapRoomId legado de RoomConfiners (escena activa)")]
    public static void LimpiarLegado()
    {
        if (!EditorUtility.DisplayDialog(
            "Limpiar mapRoomId legado",
            "Esto vaciará el campo 'mapRoomId' de los RoomConfiner de la escena activa " +
            "cuando ya exista un MapRoom con id equivalente en CUALQUIER parte de la escena " +
            "(hermano o en otro GameObject, típicamente un MapShape). ¿Continuar?",
            "Sí, limpiar", "Cancelar"))
        {
            return;
        }

        var confiners = Object.FindObjectsByType<RoomConfiner>(FindObjectsSortMode.None);
        var mapRoomsEnEscena = Object.FindObjectsByType<MapRoom>(FindObjectsSortMode.None);
        int limpiados = 0;
        int saltados = 0;

        foreach (var rc in confiners)
        {
            if (string.IsNullOrEmpty(rc.mapRoomId)) continue;

            // Buscar cualquier MapRoom en la escena que tenga el mismo id.
            bool tieneEquivalente = false;
            foreach (var candidato in mapRoomsEnEscena)
            {
                if (candidato.mapRoomId == rc.mapRoomId) { tieneEquivalente = true; break; }
            }

            if (!tieneEquivalente)
            {
                Debug.LogWarning($"[Migrador] '{RutaJerarquia(rc.transform)}': RoomConfiner tiene id " +
                                 $"'{rc.mapRoomId}' pero no encontré ningún MapRoom equivalente en la escena. Se conserva.", rc);
                saltados++;
                continue;
            }

            Undo.RecordObject(rc, "Limpiar mapRoomId legado de RoomConfiner");
            rc.mapRoomId = "";
            EditorUtility.SetDirty(rc);
            limpiados++;
        }

        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log($"[Migrador] Escena '{escena.name}': limpiados={limpiados}, saltados={saltados}. " +
                  "Recordá guardar la escena (Ctrl+S).");
    }

    [MenuItem("Mictlan/Mapa/Validar consistencia de IDs (escena activa)")]
    public static void Validar()
    {
        var mapRooms  = Object.FindObjectsByType<MapRoom>(FindObjectsSortMode.None);
        var mapShapes = Object.FindObjectsByType<MapShape>(FindObjectsSortMode.None);
        var confiners = Object.FindObjectsByType<RoomConfiner>(FindObjectsSortMode.None);

        var escena = EditorSceneManager.GetActiveScene();
        var reporte = new System.Text.StringBuilder();
        reporte.AppendLine("════════════════════════════════════════════════");
        reporte.AppendLine($"[Validador Mapa] Escena '{escena.name}'");
        reporte.AppendLine("════════════════════════════════════════════════");
        reporte.AppendLine($"  MapRooms encontrados     : {mapRooms.Length}");
        reporte.AppendLine($"  MapShapes encontrados    : {mapShapes.Length}");
        reporte.AppendLine($"  RoomConfiners encontrados: {confiners.Length}");
        reporte.AppendLine();

        int totalProblemas = 0;

        // ── (1) MapRooms sin id / con collider no-trigger ──
        var mrSinId = new System.Collections.Generic.List<string>();
        var mrColliderNoTrigger = new System.Collections.Generic.List<string>();
        foreach (var mr in mapRooms)
        {
            if (string.IsNullOrEmpty(mr.mapRoomId))
                mrSinId.Add(RutaJerarquia(mr.transform));

            var col = mr.GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
                mrColliderNoTrigger.Add(RutaJerarquia(mr.transform));
        }
        if (mrSinId.Count > 0)
        {
            totalProblemas += mrSinId.Count;
            reporte.AppendLine($"[!] {mrSinId.Count} MapRoom(s) SIN mapRoomId:");
            foreach (var s in mrSinId) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }
        if (mrColliderNoTrigger.Count > 0)
        {
            totalProblemas += mrColliderNoTrigger.Count;
            reporte.AppendLine($"[!] {mrColliderNoTrigger.Count} MapRoom(s) con Collider2D que NO es Trigger:");
            foreach (var s in mrColliderNoTrigger) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }

        // ── (2) MapShapes sin id ──
        var msSinId = new System.Collections.Generic.List<string>();
        foreach (var ms in mapShapes)
            if (string.IsNullOrEmpty(ms.mapRoomId))
                msSinId.Add(RutaJerarquia(ms.transform));
        if (msSinId.Count > 0)
        {
            totalProblemas += msSinId.Count;
            reporte.AppendLine($"[!] {msSinId.Count} MapShape(s) SIN mapRoomId:");
            foreach (var s in msSinId) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }

        // ── (3) RoomConfiners con mapRoomId legado sin MapRoom hermano ──
        var rcMigracionPendiente = new System.Collections.Generic.List<string>();
        foreach (var rc in confiners)
        {
            if (string.IsNullOrEmpty(rc.mapRoomId)) continue;
            var mr = rc.GetComponent<MapRoom>();
            if (mr == null || mr.mapRoomId != rc.mapRoomId)
                rcMigracionPendiente.Add($"{rc.mapRoomId}  @  {RutaJerarquia(rc.transform)}");
        }
        if (rcMigracionPendiente.Count > 0)
        {
            totalProblemas += rcMigracionPendiente.Count;
            reporte.AppendLine($"[!] {rcMigracionPendiente.Count} RoomConfiner(s) con mapRoomId legado sin MapRoom hermano:");
            reporte.AppendLine("    → Corré 'Mictlan → Mapa → Migrar mapRoomId a MapRoom (escena activa)'.");
            foreach (var s in rcMigracionPendiente) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }

        // ── (4) IDs desalineados entre MapShape y MapRoom (case-sensitive) ──
        var setIdsMapRoom = new System.Collections.Generic.HashSet<string>();
        foreach (var mr in mapRooms)
            if (!string.IsNullOrEmpty(mr.mapRoomId)) setIdsMapRoom.Add(mr.mapRoomId);

        var setIdsMapShape = new System.Collections.Generic.HashSet<string>();
        foreach (var ms in mapShapes)
            if (!string.IsNullOrEmpty(ms.mapRoomId)) setIdsMapShape.Add(ms.mapRoomId);

        var msSinMr = new System.Collections.Generic.List<string>();
        foreach (var ms in mapShapes)
        {
            if (string.IsNullOrEmpty(ms.mapRoomId)) continue;
            if (!setIdsMapRoom.Contains(ms.mapRoomId))
                msSinMr.Add($"{ms.mapRoomId}  @  {RutaJerarquia(ms.transform)}");
        }
        if (msSinMr.Count > 0)
        {
            totalProblemas += msSinMr.Count;
            reporte.AppendLine($"[!] {msSinMr.Count} MapShape(s) con ID SIN MapRoom equivalente:");
            reporte.AppendLine("    → La silueta se pintará siempre como 'NoVisitada'. Revisá typos/mayúsculas.");
            foreach (var s in msSinMr) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }

        var mrSinMs = new System.Collections.Generic.List<string>();
        foreach (var mr in mapRooms)
        {
            if (string.IsNullOrEmpty(mr.mapRoomId)) continue;
            if (!setIdsMapShape.Contains(mr.mapRoomId))
                mrSinMs.Add($"{mr.mapRoomId}  @  {RutaJerarquia(mr.transform)}");
        }
        if (mrSinMs.Count > 0)
        {
            // No cuenta como "problema" — es benigno.
            reporte.AppendLine($"[i] {mrSinMs.Count} MapRoom(s) sin MapShape equivalente (fallback rectangular; benigno si es intencional):");
            foreach (var s in mrSinMs) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }

        // ── (5) IDs duplicados ──
        var dupMr = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
        foreach (var mr in mapRooms)
        {
            if (string.IsNullOrEmpty(mr.mapRoomId)) continue;
            if (!dupMr.ContainsKey(mr.mapRoomId)) dupMr[mr.mapRoomId] = new System.Collections.Generic.List<string>();
            dupMr[mr.mapRoomId].Add(RutaJerarquia(mr.transform));
        }
        int dupMrCount = 0;
        foreach (var kv in dupMr) if (kv.Value.Count > 1) dupMrCount++;
        if (dupMrCount > 0)
        {
            totalProblemas += dupMrCount;
            reporte.AppendLine($"[!] {dupMrCount} ID(s) duplicado(s) entre MapRooms (registro/render ambiguo):");
            foreach (var kv in dupMr)
            {
                if (kv.Value.Count <= 1) continue;
                reporte.AppendLine($"      '{kv.Key}' x{kv.Value.Count}:");
                foreach (var r in kv.Value) reporte.AppendLine("        " + r);
            }
            reporte.AppendLine();
        }

        var dupMs = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
        foreach (var ms in mapShapes)
        {
            if (string.IsNullOrEmpty(ms.mapRoomId)) continue;
            if (!dupMs.ContainsKey(ms.mapRoomId)) dupMs[ms.mapRoomId] = new System.Collections.Generic.List<string>();
            dupMs[ms.mapRoomId].Add(RutaJerarquia(ms.transform));
        }
        int dupMsCount = 0;
        foreach (var kv in dupMs) if (kv.Value.Count > 1) dupMsCount++;
        if (dupMsCount > 0)
        {
            totalProblemas += dupMsCount;
            reporte.AppendLine($"[!] {dupMsCount} ID(s) duplicado(s) entre MapShapes (siluetas superpuestas):");
            foreach (var kv in dupMs)
            {
                if (kv.Value.Count <= 1) continue;
                reporte.AppendLine($"      '{kv.Key}' x{kv.Value.Count}:");
                foreach (var r in kv.Value) reporte.AppendLine("        " + r);
            }
            reporte.AppendLine();
        }

        // ── (6) Formato de ID: debe cumplir 'L{n}_...' ──
        var idsMalFormados = new System.Collections.Generic.List<string>();
        foreach (var mr in mapRooms)
        {
            if (string.IsNullOrEmpty(mr.mapRoomId)) continue;
            if (MapManager.NivelDeSala(mr.mapRoomId) < 0)
                idsMalFormados.Add($"MapRoom  '{mr.mapRoomId}'  @  {RutaJerarquia(mr.transform)}");
        }
        foreach (var ms in mapShapes)
        {
            if (string.IsNullOrEmpty(ms.mapRoomId)) continue;
            if (MapManager.NivelDeSala(ms.mapRoomId) < 0)
                idsMalFormados.Add($"MapShape '{ms.mapRoomId}'  @  {RutaJerarquia(ms.transform)}");
        }
        if (idsMalFormados.Count > 0)
        {
            totalProblemas += idsMalFormados.Count;
            reporte.AppendLine($"[!] {idsMalFormados.Count} ID(s) con formato incorrecto (convención: 'L{{n}}_nombre'):");
            reporte.AppendLine("    → Los Cihuacali no podrán asentar estas salas (nivel no reconocido).");
            foreach (var s in idsMalFormados) reporte.AppendLine("      " + s);
            reporte.AppendLine();
        }

        // ── Cierre ──
        reporte.AppendLine("────────────────────────────────────────────────");
        if (totalProblemas == 0)
        {
            reporte.AppendLine("✓ Sin problemas detectados.");
            Debug.Log(reporte.ToString());
        }
        else
        {
            reporte.AppendLine($"✗ {totalProblemas} problema(s) detectado(s). Ver detalle arriba.");
            Debug.LogWarning(reporte.ToString());
        }
    }

    [MenuItem("Mictlan/Mapa/Sincronizar MapRoom con MapShape (escena activa)")]
    public static void SincronizarMapRoomConMapShape()
    {
        var mapShapes = Object.FindObjectsByType<MapShape>(FindObjectsSortMode.None);
        int agregados = 0;
        int actualizados = 0;
        int yaEstabanOk = 0;
        int sinId = 0;
        int triggerCorregidos = 0;
        var layersUsadas = new System.Collections.Generic.HashSet<int>();

        foreach (var ms in mapShapes)
        {
            if (string.IsNullOrEmpty(ms.mapRoomId))
            {
                sinId++;
                continue;
            }

            layersUsadas.Add(ms.gameObject.layer);

            // (a) Forzar collider en modo Trigger — MapRoom depende de OnTriggerEnter2D.
            var col = ms.GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
            {
                Undo.RecordObject(col, "MapShape: collider a trigger");
                col.isTrigger = true;
                EditorUtility.SetDirty(col);
                triggerCorregidos++;
            }

            // (b) Añadir o alinear MapRoom.
            var existente = ms.GetComponent<MapRoom>();
            if (existente != null)
            {
                if (existente.mapRoomId != ms.mapRoomId)
                {
                    Undo.RecordObject(existente, "Sincronizar mapRoomId con MapShape");
                    existente.mapRoomId = ms.mapRoomId;
                    EditorUtility.SetDirty(existente);
                    actualizados++;
                    Debug.Log($"[Sync] Actualizado id del MapRoom en '{RutaJerarquia(ms.transform)}' → '{ms.mapRoomId}'.", ms);
                }
                else
                {
                    yaEstabanOk++;
                }
                continue;
            }

            var nuevo = Undo.AddComponent<MapRoom>(ms.gameObject);
            nuevo.mapRoomId = ms.mapRoomId;
            EditorUtility.SetDirty(nuevo);
            agregados++;
            Debug.Log($"[Sync] Añadido MapRoom '{ms.mapRoomId}' en '{RutaJerarquia(ms.transform)}'.", ms);
        }

        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log($"[Sync] Escena '{escena.name}': MapShapes revisados={mapShapes.Length}, " +
                  $"MapRoom añadidos={agregados}, actualizados={actualizados}, ya estaban OK={yaEstabanOk}, " +
                  $"sin mapRoomId={sinId}, colliders forzados a trigger={triggerCorregidos}. " +
                  "Recordá guardar la escena (Ctrl+S).");

        // Reporte de capas: el usuario debe verificar la Physics 2D Matrix.
        if (layersUsadas.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Sync] Capas usadas por los MapShapes procesados:");
            foreach (var l in layersUsadas)
            {
                string nombre = UnityEngine.LayerMask.LayerToName(l);
                if (string.IsNullOrEmpty(nombre)) nombre = "<sin nombre>";
                sb.AppendLine($"    • Layer {l} ('{nombre}')");
            }
            sb.AppendLine("→ Verificá en Edit → Project Settings → Physics 2D → Layer Collision Matrix");
            sb.AppendLine("  que la capa del Player tenga tildado el cruce con estas capas.");
            sb.AppendLine("  Si no lo tiene, OnTriggerEnter2D no dispara aunque el collider sea trigger.");
            Debug.LogWarning(sb.ToString());
        }
    }

    [MenuItem("Mictlan/Mapa/Eliminar MapRoom duplicado en RoomConfiners (escena activa)")]
    public static void EliminarMapRoomDuplicadoEnRoomConfiners()
    {
        if (!EditorUtility.DisplayDialog(
            "Eliminar MapRoom duplicado",
            "Esto removerá el componente MapRoom del GameObject de cada RoomConfiner " +
            "cuando exista otro MapRoom con el mismo id en OTRO GameObject de la escena " +
            "(típicamente el MapShape correspondiente).\n\nEl RoomConfiner queda intacto " +
            "(sigue siendo el confiner de cámara). ¿Continuar?",
            "Sí, eliminar duplicados", "Cancelar"))
        {
            return;
        }

        var confiners = Object.FindObjectsByType<RoomConfiner>(FindObjectsSortMode.None);
        var mapRoomsEnEscena = Object.FindObjectsByType<MapRoom>(FindObjectsSortMode.None);
        int eliminados = 0;
        int conservados = 0;
        int sinMapRoomHermano = 0;

        foreach (var rc in confiners)
        {
            var mrHermano = rc.GetComponent<MapRoom>();
            if (mrHermano == null)
            {
                sinMapRoomHermano++;
                continue;
            }
            if (string.IsNullOrEmpty(mrHermano.mapRoomId))
            {
                Debug.LogWarning($"[Limpieza] '{RutaJerarquia(rc.transform)}': MapRoom hermano sin mapRoomId. Se conserva.", rc);
                conservados++;
                continue;
            }

            // ¿Existe otro MapRoom con el mismo id, en otro GameObject?
            MapRoom otroConMismoId = null;
            foreach (var candidato in mapRoomsEnEscena)
            {
                if (candidato == mrHermano) continue;
                if (candidato.mapRoomId == mrHermano.mapRoomId)
                {
                    otroConMismoId = candidato;
                    break;
                }
            }

            if (otroConMismoId == null)
            {
                // No hay duplicado — este MapRoom es el único portador de la sala. Conservar.
                conservados++;
                continue;
            }

            Debug.Log($"[Limpieza] Removiendo MapRoom duplicado '{mrHermano.mapRoomId}' de " +
                      $"'{RutaJerarquia(rc.transform)}' " +
                      $"(equivalente en '{RutaJerarquia(otroConMismoId.transform)}').", rc);
            Undo.DestroyObjectImmediate(mrHermano);
            eliminados++;
        }

        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log($"[Limpieza] Escena '{escena.name}': RoomConfiners revisados={confiners.Length}, " +
                  $"MapRoom duplicados eliminados={eliminados}, conservados={conservados}, " +
                  $"sin MapRoom hermano={sinMapRoomHermano}. Recordá guardar la escena (Ctrl+S).");
    }

    private static string RutaJerarquia(Transform t)
    {
        if (t == null) return "<null>";
        string s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
