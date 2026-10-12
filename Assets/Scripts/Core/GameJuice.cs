using System.Collections;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Shared "feel" layer: camera shake, dust puffs and a tiny hit-stop, driven by what the worm does
    /// (hard impacts, launches) and callable by any mechanism (GameJuice.Shake, GameJuice.Puff).
    /// It creates itself next to the worm (WormController.Awake), so nothing has to be placed in the scene.
    /// To tune it, add a GameJuice component to any object yourself: that one is used instead.
    /// Purely visual: it never changes physics (the hit-stop only slows time for a few hundredths of a second).
    /// </summary>
    public class GameJuice : MonoBehaviour
    {
        public static GameJuice Instance { get; private set; }

        [Header("Camera shake")]
        public bool cameraShake = true;
        [Tooltip("Multiplies every shake (0 = none, 2 = double).")]
        [Min(0f)] public float shakeStrength = 1f;
        [Tooltip("Impact force (worm mass x speed into the surface) where shaking starts. Rocky landing from a normal jump is about 20-40.")]
        public float impactShakeFrom = 22f;
        [Tooltip("Impact force that gives the strongest shake.")]
        public float impactShakeFull = 90f;
        [Tooltip("Strongest impact shake (world units).")]
        public float impactShakeMax = 0.45f;
        [Tooltip("Shake on a full-power launch (units). Scaled by power and by the worm's mass.")]
        public float launchShake = 0.08f;

        [Header("Dust")]
        public bool dust = true;
        public Color dustColor = new Color(0.85f, 0.83f, 0.8f, 0.9f);
        [Tooltip("Speed into the surface under which no dust comes out (units/s).")]
        public float dustFromSpeed = 6f;
        [Min(0)] public int maxDustPuffs = 10;

        [Header("Hit-stop (freeze frame on very hard hits)")]
        public bool hitStop = true;
        [Tooltip("Impact force from which the game freezes for a moment (Rocky falling from high up).")]
        public float hitStopFrom = 80f;
        [Tooltip("Real seconds of the freeze.")]
        [Range(0f, 0.2f)] public float hitStopTime = 0.05f;

        WormController subscribed;
        bool stopping;

        /// <summary>Called by WormController: makes sure one GameJuice exists.</summary>
        public static void Ensure()
        {
            if (Instance != null) return;
            GameJuice existing = FindExisting();
            if (existing != null) { Instance = existing; return; }
            new GameObject("GameJuice (auto)").AddComponent<GameJuice>();
        }

        static GameJuice FindExisting()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<GameJuice>();
#else
            return Object.FindObjectOfType<GameJuice>();
#endif
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
            if (stopping) Time.timeScale = 1f;
        }

        void Update()
        {
            WormController w = WormController.Instance;
            if (w == subscribed) return;
            Unsubscribe();
            subscribed = w;
            if (w != null) { w.Impacted += OnImpact; w.Launched += OnLaunched; }
        }

        void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Impacted -= OnImpact;
            subscribed.Launched -= OnLaunched;
            subscribed = null;
        }

        void OnImpact(ImpactInfo hit)
        {
            float f = hit.impactForce;
            if (f > impactShakeFrom)
                Shake(impactShakeMax * Mathf.Clamp01((f - impactShakeFrom) / Mathf.Max(1f, impactShakeFull - impactShakeFrom)));
            if (hit.normalSpeed > dustFromSpeed)
                Puff(hit.point, hit.normal, Mathf.RoundToInt(Mathf.Lerp(3f, maxDustPuffs, Mathf.Clamp01(hit.normalSpeed / 40f))), 1f);
            if (hitStop && f >= hitStopFrom) HitStop(hitStopTime);
        }

        void OnLaunched(Vector2 pos, float power01)
        {
            if (subscribed == null || subscribed.Physics == null) return;
            float mass = subscribed.Physics.Mass;
            Shake(launchShake * power01 * Mathf.Clamp(mass, 0.3f, 2.5f));
            float r = subscribed.Physics.Collider != null ? subscribed.Physics.Collider.radius : 0.4f;
            Puff(pos + Vector2.down * r, Vector2.up, Mathf.RoundToInt(Mathf.Lerp(2f, maxDustPuffs * 0.6f, power01)), 0.8f);
        }

        // ------------------------------------------------------------------ API for mechanisms

        /// <summary>Shake the camera (amount in world units, roughly the largest offset).</summary>
        public static void Shake(float amount)
        {
            GameJuice j = Instance;
            if (j != null && (!j.cameraShake || j.shakeStrength <= 0f)) return;
            float k = j != null ? j.shakeStrength : 1f;
            if (amount * k > 0.001f) CameraFollow2D.AddShake(amount * k);
        }

        /// <summary>A small burst of dust at a contact, spreading along the surface and away from it.</summary>
        public static void Puff(Vector2 point, Vector2 normal, int count, float size)
        {
            GameJuice j = Instance;
            if (j == null || !j.dust || count <= 0) return;
            if (normal.sqrMagnitude < 0.001f) normal = Vector2.up;
            normal.Normalize();
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            for (int i = 0; i < count; i++)
            {
                float side = (i % 2 == 0 ? 1f : -1f) * Random.Range(2f, 5f);
                Vector2 vel = tangent * side + normal * Random.Range(0.5f, 2.5f);
                SimpleDebris.Spawn(point + normal * 0.05f, vel, j.dustColor, Random.Range(0.12f, 0.28f) * size, Random.Range(0.25f, 0.5f));
            }
        }

        /// <summary>Freeze the game for a few hundredths of a second (big hits).</summary>
        public static void HitStop(float seconds)
        {
            GameJuice j = Instance;
            if (j == null || !j.hitStop || seconds <= 0f || j.stopping || Time.timeScale < 0.99f) return;
            j.StartCoroutine(j.DoHitStop(seconds));
        }

        IEnumerator DoHitStop(float seconds)
        {
            stopping = true;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            stopping = false;
        }
    }
}
