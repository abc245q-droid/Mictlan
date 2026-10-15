using UnityEngine;

// ============================================================
//  IGolpeableMacahuitl — objetos que reaccionan al Macahuitl
// ============================================================
//
//  Cualquier objeto en la capa 'destructibleLayer' de RomeritoCombat
//  que implemente esta interfaz recibe el golpe lateral Y el pogo,
//  sin tener que tocar RomeritoCombat cada vez que se agrega un tipo
//  nuevo de objeto golpeable.
//
//  La implementan:
//    • MazorcaCacao (mazorca petrificada que suelta semillas)
// ============================================================

public interface IGolpeableMacahuitl
{
    /// <summary>
    /// Golpe del Macahuitl.
    /// origenAtacante: posición de Romerito (para orientar reacciones).
    /// Devuelve true si el golpe "conectó" (dispara recoil o rebote de pogo).
    /// </summary>
    bool RecibirGolpeMacahuitl(Vector2 origenAtacante, int dano, RomeritoCombat.GodFavor favor);
}
