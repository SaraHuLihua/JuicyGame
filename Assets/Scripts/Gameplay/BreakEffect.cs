using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Reusable break visual effect. Purely visual: it never touches physics, collision,
    /// Rigidbody2D, materials or launching. Gameplay detects the break first, then calls Play().
    /// Use it two ways:
    ///   1. Assign it to BreakableObject.breakVfx / BreakableChain.breakEffect (spawned at the hit point).
    ///   2. Wire its PlaySelf() into any UnityEvent (e.g. BreakableObject.onBroken) — no arguments needed.
    /// The burst is built from SimpleDebris shards (square sprites, no colliders) and disappears by itself.
    /// </summary>
    public class BreakEffect : MonoBehaviour
    {
        [Tooltip("Master switch. Off = Play() does nothing (handy for testing without effects).")]
        public bool effectEnabled = true;
        [Tooltip("Offset added to the spawn position, in world units.")]
        public Vector2 offset;
        [Tooltip("Multiplies shard count, speeds, sizes and lifetimes below.")]
        public float effectScale = 1f;

        [Header("Burst")]
        public int shardCount = 12;
        public Color color = Color.white;
        public float minSpeed = 2f;
        public float maxSpeed = 7f;
        public float minSize = 0.12f;
        public float maxSize = 0.3f;
        public float minLife = 0.6f;
        public float maxLife = 1.2f;

        /// <summary>Directional burst at pos, travelling mostly along dir (e.g. -hit.normal).</summary>
        public void Play(Vector2 pos, Vector2 dir)
        {
            if (!effectEnabled) return;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.up;
            dir = dir.normalized;
            Vector2 at = pos + offset * effectScale;
            int n = Mathf.Max(1, Mathf.RoundToInt(shardCount * effectScale));
            for (int i = 0; i < n; i++)
            {
                Vector2 vel = dir * Random.Range(minSpeed, maxSpeed) * effectScale
                    + Random.insideUnitCircle * Random.Range(1f, 5f) * effectScale;
                SimpleDebris.Spawn(at, vel, color,
                    Random.Range(minSize, maxSize) * effectScale,
                    Random.Range(minLife, maxLife));
            }
        }

        /// <summary>No-argument version for UnityEvents (onBroken): bursts at this object's position, upwards.</summary>
        public void PlaySelf()
        {
            Play(transform.position, Vector2.up);
        }
    }
}
