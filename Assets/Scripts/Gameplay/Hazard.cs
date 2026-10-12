using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Deadly obstacle (spikes, saws, spiked gears...). Attach to any visual asset:
    /// on touch the worm dies (Die, no instant teleport). Respawn via WormController.RespawnAtLastLanding().
    /// Works with both collider (OnCollision) and trigger setups.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        [Tooltip("If true, only Heavy/Bouncy-specific? Leave off: any material dies.")]
        public bool killOnTouch = true;
        [Tooltip("Seconds between kills so a corpse resting on spikes does not spam the event.")]
        public float killCooldown = 1f;

        float lastKill = -99f;

        void OnCollisionEnter2D(Collision2D c) { TryKill(c.collider); }
        void OnTriggerEnter2D(Collider2D c) { TryKill(c); }

        void TryKill(Collider2D c)
        {
            if (!killOnTouch) return;
            if (Time.time - lastKill < killCooldown) return;
            WormController worm = c.GetComponentInParent<WormController>();
            if (worm == null) return;
            lastKill = Time.time;
            worm.Die("hazard: " + name);
        }
    }
}
