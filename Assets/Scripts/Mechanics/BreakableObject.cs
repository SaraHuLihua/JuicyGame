using System;
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
    ///
    /// Composite objects (e.g. BreakableChain) put this on their root, turn off disableCollidersOnBreak /
    /// hideSpriteOnBreak and react to the Broken event themselves: the impact rules stay in one place.
    /// </summary>
    public class BreakableObject : MonoBehaviour
    {
        [Tooltip("Impact force (mass * normal speed) needed to break it in one hit. Heavy: 2.5 * (6..26 u/s) = 15..65. Squishy: 0.3 * (6..31) = 2..9 (13 when falling at top speed).")]
        public float breakThreshold = 20f;
        [Range(0f, 1f), Tooltip("Hits at or above this fraction of the threshold crack it and add damage.")]
        public float crackFraction = 0.5f;
        public bool accumulateDamage = true;
        [Tooltip("Hits from a worm lighter than this never crack or break it, however fast (worm mass: Squishy 0.3, Heavy 2.5). 0 = off, only the impact force counts.")]
        public float minimumMass = 0f;
        public int shardCount = 10;
        public Color shardColor = new Color(0.76f, 0.70f, 0.50f);

        public UnityEvent onCracked;
        public UnityEvent onBroken;

        [Header("What happens to this object when it breaks")]
        [Tooltip("Off for objects that handle their own break (BreakableChain keeps its links solid so the pieces can fall and collide).")]
        public bool disableCollidersOnBreak = true;
        [Tooltip("Off for objects that handle their own break.")]
        public bool hideSpriteOnBreak = true;

        [Header("Break VFX (optional, purely visual)")]
        [Tooltip("Spawned at the hit point when this breaks. Leave empty for shards only.")]
        public BreakEffect breakVfx;

        /// <summary>Raised right after the break, with the hit that caused it (code-side twin of onBroken).</summary>
        public event Action<ImpactInfo> Broken;

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

        public bool WouldBreak(float impactForce) { return WouldBreak(impactForce, float.MaxValue); }

        public bool WouldBreak(float impactForce, float wormMass)
        {
            if (wormMass < minimumMass) return false;
            return !IsBroken && (impactForce >= breakThreshold || (accumulateDamage && impactForce >= breakThreshold * crackFraction && Damage + impactForce / breakThreshold >= 1f));
        }

        /// <summary>Returns true if this hit broke the object.</summary>
        public bool TryBreak(ImpactInfo hit)
        {
            if (IsBroken) return false;
            float f = hit.impactForce;
            if (minimumMass > 0f && hit.normalSpeed > 0.0001f && f / hit.normalSpeed < minimumMass) return false; // impactForce = mass x normalSpeed

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
            Bounds b = sr != null ? sr.bounds
                : (colliders != null && colliders.Length > 0 ? colliders[0].bounds : new Bounds(hit.point, Vector3.one * 0.5f)); // read before disabling
            if (disableCollidersOnBreak)
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            if (sr != null && hideSpriteOnBreak) sr.enabled = false;

            Vector2 dir = -hit.normal; // shards continue in the direction of the hit
            for (int i = 0; i < shardCount; i++)
            {
                Vector2 pos = new Vector2(UnityEngine.Random.Range(b.min.x, b.max.x), UnityEngine.Random.Range(b.min.y, b.max.y));
                Vector2 vel = (dir * UnityEngine.Random.Range(2f, 7f)) + UnityEngine.Random.insideUnitCircle * UnityEngine.Random.Range(1f, 5f);
                SimpleDebris.Spawn(pos, vel, shardColor, UnityEngine.Random.Range(0.12f, 0.3f), UnityEngine.Random.Range(0.6f, 1.2f));
            }
            if (GetComponent<BreakableChain>() == null) // chains play their own snap
            {
                GameJuice.Shake(0.12f);
            }
            if (Broken != null) Broken(hit);                     // physical reaction of composite objects first
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
