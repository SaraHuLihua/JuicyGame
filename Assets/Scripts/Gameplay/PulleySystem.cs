using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Counterweight rig: two PulleyPlatforms hung from one wheel by a rope.
    /// Controlled (kinematic) movement, NOT a simulated rope: predictable, stable, tunable.
    /// travel > 0 means A is down / B is up. Each side's load = platform.baseWeight +
    /// riding worm's REAL mass (Squishy ~0.3, Heavy ~2.5) x playerWeightMultiplier, so Rocky
    /// always pulls harder than Bouncy with zero duplicated material state.
    /// Landing on a platform also gives it a dip proportional to the hit (impact force = worm mass x speed,
    /// from the worm's own Impacted event), so a Rocky landing visibly yanks the rig and Bouncy barely does.
    /// The rope (PulleyRope) and wheel spin are drawn every rendered frame from the interpolated platforms.
    /// Hierarchy (built by the prefab / setup menu, all Inspector-editable):
    ///   PulleyRig (this)
    ///   ├── Wheel (visual, spins with travel)
    ///   ├── Rope (PulleyRope: A top -> wheel -> B top)
    ///   ├── PlatformA (PulleyPlatform)
    ///   └── PlatformB (PulleyPlatform)
    /// </summary>
    public class PulleySystem : MonoBehaviour
    {
        [Header("Rig (auto-found on children if empty)")]
        public PulleyPlatform platformA;
        public PulleyPlatform platformB;
        public Transform wheel;
        public PulleyRope rope;

        [Header("Movement")]
        [Tooltip("How far each platform can move from its start position, up or down.")]
        public float maxTravel = 3f;
        [Tooltip("Platform speed per unit of load difference. Heavy-vs-empty ≈ 2.5x Bouncy-vs-empty.")]
        public float moveSpeed = 2f;
        [Tooltip("Hard cap on platform speed so Heavy can never break the level.")]
        public float maxSpeed = 6f;
        [Tooltip("How fast velocity chases the target (higher = snappier, lower = floatier).")]
        public float damping = 4f;
        [Tooltip("Distance before each travel limit over which the platforms ease to a stop instead of halting dead (units). 0 = hard stop.")]
        public float endEase = 0.6f;
        [Tooltip("Extra load permanently added to side B (positive tips the rig toward B when empty).")]
        public float counterBias = 0f;
        [Tooltip("Scales the worm's mass contribution. 1 = real masses, 0 = rider weight ignored.")]
        public float playerWeightMultiplier = 1f;
        [Tooltip("Dip when the worm lands on a platform: platform speed added per unit of impact force (mass x speed), divided by the rig's total load. Rocky lands ~8x harder than Bouncy. 0 = off.")]
        public float landingKick = 0.35f;

        [Header("Rest behaviour")]
        [Tooltip("With no rider, ease back to restOffset (0 = start positions).")]
        public bool autoReturn = true;
        public float restOffset = 0f;
        [Tooltip("Spin the wheel visual. 0 = keep it still.")]
        public float wheelRadius = 0.6f;

        float travel;   // >0: A down, B up
        float velocity;
        Vector2 restA;
        Vector2 restB;
        bool initialised;
        float wheelBaseAngle;
        WormController subscribedWorm;

        public float Travel { get { return travel; } }

        void Awake()
        {
            if (platformA == null || platformB == null)
            {
                PulleyPlatform[] found = GetComponentsInChildren<PulleyPlatform>();
                if (platformA == null && found.Length > 0) platformA = found[0];
                if (platformB == null && found.Length > 1) platformB = found[1];
            }
            if (wheel == null)
            {
                Transform w = transform.Find("Wheel");
                if (w != null) wheel = w;
            }
            if (rope == null) rope = GetComponentInChildren<PulleyRope>();
        }

        void Start()
        {
            if (platformA != null) restA = platformA.transform.position;
            if (platformB != null) restB = platformB.transform.position;
            if (wheel != null) wheelBaseAngle = wheel.localEulerAngles.z;
            initialised = true;
            Subscribe();
        }

        void OnEnable() { if (initialised) Subscribe(); }

        void OnDisable()
        {
            if (subscribedWorm != null) subscribedWorm.Impacted -= OnWormImpact;
            subscribedWorm = null;
        }

        void Subscribe()
        {
            WormController w = WormController.Instance;
            if (w == subscribedWorm) return;
            if (subscribedWorm != null) subscribedWorm.Impacted -= OnWormImpact;
            subscribedWorm = w;
            if (w != null) w.Impacted += OnWormImpact;
        }

        void OnWormImpact(ImpactInfo hit)
        {
            if (landingKick <= 0f || hit.collider == null || hit.normal.y < 0.5f) return; // landings only
            PulleyPlatform p = hit.collider.GetComponentInParent<PulleyPlatform>();
            if (p == null || (p != platformA && p != platformB)) return;
            float total = Mathf.Max(0.1f, platformA.baseWeight + platformB.baseWeight + Mathf.Abs(counterBias));
            float kick = hit.impactForce * landingKick / total;
            velocity += p == platformA ? kick : -kick; // A pushed down = positive travel
            velocity = Mathf.Clamp(velocity, -maxSpeed, maxSpeed);
        }

        void FixedUpdate()
        {
            if (!initialised || platformA == null || platformB == null) return;
            if (subscribedWorm == null && WormController.Instance != null) Subscribe(); // worm spawned later
            float dt = Time.fixedDeltaTime;
            maxTravel = Mathf.Max(0.1f, maxTravel);

            float loadA = platformA.CurrentLoad(playerWeightMultiplier);
            float loadB = platformB.CurrentLoad(playerWeightMultiplier) + counterBias;

            float targetVel;
            bool anyoneRiding = platformA.HasRider || platformB.HasRider;
            if (anyoneRiding)
                targetVel = Mathf.Clamp((loadA - loadB) * moveSpeed, -maxSpeed, maxSpeed);
            else if (autoReturn)
                targetVel = Mathf.Clamp((restOffset - travel) * damping, -maxSpeed, maxSpeed);
            else
                targetVel = 0f;

            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, damping) * dt);
            velocity += (targetVel - velocity) * k;
            if (endEase > 0.001f)
            {
                // Ease into the travel limits so the platforms never slam to a stop.
                float room = velocity > 0f ? maxTravel - travel : maxTravel + travel;
                float cap = Mathf.Max(0.05f, room / endEase) * maxSpeed;
                velocity = Mathf.Clamp(velocity, -cap, cap);
            }
            travel = Mathf.Clamp(travel + velocity * dt, -maxTravel, maxTravel);
            if ((travel == maxTravel && velocity > 0f) || (travel == -maxTravel && velocity < 0f))
                velocity = 0f;

            platformA.MoveTo(new Vector2(restA.x, restA.y - travel));
            platformB.MoveTo(new Vector2(restB.x, restB.y + travel));
        }

        void LateUpdate()
        {
            // Wheel spin from the INTERPOLATED platform position: smooth at any frame rate, no drift.
            if (!initialised || wheel == null || platformA == null || wheelRadius <= 0.01f) return;
            float drawnTravel = restA.y - platformA.transform.position.y;
            Vector3 e = wheel.localEulerAngles;
            e.z = wheelBaseAngle - drawnTravel / wheelRadius * Mathf.Rad2Deg;
            wheel.localEulerAngles = e;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (platformA == null || platformB == null) return;
            Vector2 a = Application.isPlaying && initialised ? restA : (Vector2)platformA.transform.position;
            Vector2 b = Application.isPlaying && initialised ? restB : (Vector2)platformB.transform.position;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Gizmos.DrawLine(new Vector3(a.x, a.y - maxTravel, 0f), new Vector3(a.x, a.y + maxTravel, 0f));
            Gizmos.DrawLine(new Vector3(b.x, b.y - maxTravel, 0f), new Vector3(b.x, b.y + maxTravel, 0f));
        }
#endif
    }
}
