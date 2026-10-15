using UnityEngine;

// ============================================================
//  NavegacionAerea — utilidades de vuelo para enemigos voladores
// ============================================================
//
//  Compartido por FlyingEnemyAI (mosquito) y Tlahuelpuchi_Nexhualli
//  (bola de fuego). Tres piezas:
//
//  1. ElegirDireccion — "context steering". Si el camino directo
//     está libre, devuelve la dirección deseada tal cual (no cambia
//     el carácter del vuelo). Si está bloqueado, prueba N direcciones
//     con CircleCast del TAMAÑO DEL CUERPO y elige la que mejor
//     combina: ir hacia el objetivo + espacio libre + seguir la
//     dirección previa. Esa continuidad hace que rodee la plataforma
//     por UN lado en vez de oscilar debajo/encima de ella (el atasco
//     clásico del "rebote por reflexión" con un solo raycast).
//
//  2. Desplazamiento — para quien se mueve con MovePosition: recorta
//     el movimiento antes del muro y desliza el resto por la
//     superficie. Un Kinematic con MovePosition atraviesa tiles; un
//     Dynamic se mete y rebota. Así ninguno se incrusta.
//
//  3. Despenetrar — si de todos modos quedó dentro de un tile
//     (knockback, spawn mal puesto), lo saca por el lado más corto.
// ============================================================

public static class NavegacionAerea
{
    private static readonly Collider2D[] solapes = new Collider2D[8];

    /// <summary>
    /// Dirección normalizada hacia 'deseada' rodeando obstáculos.
    /// Devuelve Vector2.zero si todo está bloqueado (usar Despenetrar).
    /// </summary>
    public static Vector2 ElegirDireccion(Vector2 centro, Vector2 deseada, Vector2 previa,
                                          float radio, float alcance, LayerMask capas,
                                          int muestras = 16, float pesoContinuidad = 0.35f)
    {
        if (deseada.sqrMagnitude < 0.0001f) return Vector2.zero;
        deseada.Normalize();

        // Camino directo libre → no tocar nada.
        if (!Physics2D.CircleCast(centro, radio, deseada, alcance, capas))
            return deseada;

        bool hayPrevia = previa.sqrMagnitude > 0.0001f;
        if (hayPrevia) previa.Normalize();

        Vector2 mejor = Vector2.zero;
        float mejorPuntaje = float.NegativeInfinity;
        muestras = Mathf.Max(4, muestras);

        for (int i = 0; i < muestras; i++)
        {
            float ang = i * Mathf.PI * 2f / muestras;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

            RaycastHit2D h = Physics2D.CircleCast(centro, radio, dir, alcance, capas);
            float libre = (h.collider == null) ? 1f : h.distance / alcance;   // 0 = pegado
            if (libre < 0.15f) continue;                                       // bloqueada

            float interes = (Vector2.Dot(dir, deseada) + 1f) * 0.5f;           // 0..1
            float puntaje = interes * libre;
            if (hayPrevia) puntaje += pesoContinuidad * Vector2.Dot(dir, previa);

            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                mejor = dir;
            }
        }
        return mejor;
    }

    /// <summary>
    /// Recorta 'delta' antes del primer obstáculo y desliza el resto por
    /// la superficie (un rebote de proyección). Para MovePosition.
    /// </summary>
    public static Vector2 Desplazamiento(Vector2 centro, Vector2 delta, float radio,
                                         LayerMask capas, float piel = 0.02f)
    {
        float dist = delta.magnitude;
        if (dist < 0.00001f) return Vector2.zero;
        Vector2 dir = delta / dist;

        RaycastHit2D h = Physics2D.CircleCast(centro, radio, dir, dist + piel, capas);
        if (h.collider == null) return delta;

        Vector2 mov = dir * Mathf.Max(0f, h.distance - piel);

        // El resto, proyectado sobre la superficie (deslizar).
        Vector2 resto = delta - mov;
        Vector2 tangente = resto - Vector2.Dot(resto, h.normal) * h.normal;
        float dt = tangente.magnitude;
        if (dt > 0.00001f)
        {
            Vector2 dir2 = tangente / dt;
            RaycastHit2D h2 = Physics2D.CircleCast(centro + mov, radio, dir2, dt + piel, capas);
            mov += (h2.collider == null) ? tangente : dir2 * Mathf.Max(0f, h2.distance - piel);
        }
        return mov;
    }

    /// <summary>
    /// Si 'propio' está metido en algún collider de 'capas', mueve el
    /// Rigidbody por el camino más corto para sacarlo. Devuelve true si corrigió.
    /// </summary>
    public static bool Despenetrar(Rigidbody2D rb, Collider2D propio, LayerMask capas)
    {
        if (rb == null || propio == null || !propio.enabled) return false;

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(capas);
        filtro.useTriggers = false;

        int n = propio.Overlap(filtro, solapes);
        if (n <= 0) return false;

        Vector2 correccion = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            Collider2D otro = solapes[i];
            if (otro == null || otro == propio) continue;
            ColliderDistance2D d = otro.Distance(propio);
            if (d.isValid && d.isOverlapped)
                correccion += d.pointA - d.pointB;   // mueve 'propio' fuera de 'otro'
        }

        if (correccion.sqrMagnitude < 0.000001f) return false;
        rb.position += correccion;
        return true;
    }
}
