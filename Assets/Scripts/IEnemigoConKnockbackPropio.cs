// ============================================================
//  IEnemigoConKnockbackPropio — Marcador del "nuevo sistema"
// ============================================================
//
//  Cualquier enemigo que gestione SU PROPIO retroceso al recibir
//  daño (suscribiéndose a EnemyDummy.OnHurt) implementa esta
//  interfaz vacía. Sirve como bandera para que RomeritoCombat
//  NO aplique un AddForce externo encima del knockback interno:
//  así nunca hay dos sistemas de física peleando por el mismo
//  Rigidbody2D en el mismo golpe.
//
//  La implementan:
//    • MictecahBase   (todos los Mictecah terrestres)
//    • FlyingEnemyAI  (voladores: mosquito, etc.)
//
//  Los enemigos "dummy" SIN IA propia NO la implementan, y por
//  tanto siguen recibiendo el knockback externo de RomeritoCombat.
//
// ============================================================

public interface IEnemigoConKnockbackPropio
{
}
