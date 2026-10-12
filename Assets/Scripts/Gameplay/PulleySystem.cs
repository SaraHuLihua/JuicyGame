using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Counterweight rig: two PulleyPlatforms hung from one wheel by a rope.
    /// Controlled (kinematic) movement, NOT a simulated rope: predictable, stable, tunable.
    /// travel > 0 means A is down / B is up. Each side's load = platform.baseWeight +
    /// riding worm's REAL mass (Squishy ~0.3, Heavy ~2.5) x playerWeightMultiplier, so Rocky
    /// always pulls harder than Bouncy with zero duplicated material state.
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
        [Tooltip("Extra load permanently added to side B (positive tips the rig toward B when empty).")]
        public float counterBias = 0f;
        [Tooltip("Scales the worm's mass contribution. 1 = real masses, 0 = rider weight ignored.")]
        public float playerWeightMultiplier = 1f;

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
            initialised = true;
        }

        void FixedUpdate()
        {
            if (!initialised || platformA == null || platformB == null) return;
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
            float prevTravel = travel;
            velocity += (targetVel - velocity) * k;
            travel = Mathf.Clamp(travel + velocity * dt, -maxTravel, maxTravel);
            if ((travel == maxTravel && velocity > 0f) || (travel == -maxTravel && velocity < 0f))
                velocity = 0f;

            platformA.MoveTo(new Vector2(restA.x, restA.y - travel));
            platformB.MoveTo(new Vector2(restB.x, restB.y + travel));

            float ds = travel - prevTravel;
            if (wheel != null && wheelRadius > 0.01f && Mathf.Abs(ds) > 0.00001f)
                wheel.Rotate(0f, 0f, -ds / wheelRadius * Mathf.Rad2Deg);

            if (rope != null)
                rope.SetPoints(
                    (Vector2)platformA.transform.position + Vector2.up * 0.3f,
                    wheel != null ? (Vector2)wheel.position : (Vector2)transform.position,
                    (Vector2)platformB.transform.position + Vector2.up * 0.3f);
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
