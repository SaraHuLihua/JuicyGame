using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Anything the worm can smash. It only compares the IMPACT FORCE (worm mass * speed into the surface)
    /// with its threshold; there is no "if Heavy" anywhere. Heavy breaks things because it is dense, and
    /// Squishy cannot because even at top speed its mass * speed stays below normal thresholds.
    ///
    /// Weak hits (above crackFraction * threshold) crack it and add damage; two medium hits can finish it.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BreakableObject : MonoBehaviour
    {
        [Tooltip("Impact force (mass * normal speed) needed to break it in one hit. Heavy: 2.5 * (6..26 u/s) = 15..65. Squishy: 0.3 * (6..31) = 2..9 (13 when falling at top speed).")]
        public float breakThreshold = 20f;
        [Range(0f, 1f), Tooltip("Hits at or above this fraction of the threshold crack it and add damage.")]
        public float crackFraction = 0.5f;
        public bool accumulateDamage = true;
        public int shardCount = 10;
        public Color shardColor = new Color(0.76f, 0.70f, 0.50f);

        public UnityEvent onCracked;
        public UnityEvent onBroken;

        [Header("Break VFX (optional, purely visual)")]
        [Tooltip("Spawned at the hit point when this breaks. Leave empty for shards only.")]
        public BreakEffect breakVfx;

        public bool IsBroken { get; private set; }
        public float Damage { get; private set; }   // 0..1
        public int CrackLevel { get; private set; }

        SpriteRenderer sr;
        Collider2D[] colliders;
        Color baseColor = Color.white;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
            colliders = GetComponentsInChildren<Collider2D>();
        }

        public bool WouldBreak(float impactForce)
        {
            return !IsBroken && (impactForce >= breakThreshold || (accumulateDamage && impactForce >= breakThreshold * crackFraction && Damage + impactForce / breakThreshold >= 1f));
        }

        /// <summary>Returns true if this hit broke the object.</summary>
        public bool TryBreak(ImpactInfo hit)
        {
            if (IsBroken) return false;
            float f = hit.impactForce;

            if (f >= breakThreshold) { Break(hit); return true; }

            if (f >= breakThreshold * crackFraction)
            {
                if (accumulateDamage) Damage += f / breakThreshold;
                CrackLevel++;
                if (accumulateDamage && Damage >= 1f) { Break(hit); return true; }
                if (sr != null) sr.color = Color.Lerp(baseColor, baseColor * 0.55f, Mathf.Clamp01(Damage)) ;
                onCracked.Invoke();
            }
            return false;
        }

        void Break(ImpactInfo hit)
        {
            IsBroken = true;
            Bounds b = sr != null ? sr.bounds : colliders[0].bounds; // read before disabling
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            if (sr != null) sr.enabled = false;

            Vector2 dir = -hit.normal; // shards continue in the direction of the hit
            for (int i = 0; i < shardCount; i++)
            {
                Vector2 pos = new Vector2(Random.Range(b.min.x, b.max.x), Random.Range(b.min.y, b.max.y));
                Vector2 vel = (dir * Random.Range(2f, 7f)) + Random.insideUnitCircle * Random.Range(1f, 5f);
                SimpleDebris.Spawn(pos, vel, shardColor, Random.Range(0.12f, 0.3f), Random.Range(0.6f, 1.2f));
            }
            if (breakVfx != null) breakVfx.Play(hit.point, dir); // visual only, after the physical change
            onBroken.Invoke();
        }

        /// <summary>Put it back together (used by the test reset).</summary>
        public void Restore()
        {
            IsBroken = false;
            Damage = 0f;
            CrackLevel = 0;
            if (colliders == null) colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = true;
            if (sr != null) { sr.enabled = true; sr.color = baseColor; }
        }
    }
}
