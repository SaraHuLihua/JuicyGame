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

        [Tooltip("0 = a rider keeps the rig moving until Max Travel (a real counterweight). Above 0 = it works like a scale: " +
                 "the platform sinks Sink Per Weight units per unit of extra load and stops there (Squishy 0.3 -> 0.3 x this, Heavy 2.5 -> 2.5 x this).")]
        public float sinkPerWeight = 0f;

        [Header("Rest behaviour (nobody on the platforms, e.g. while Squishy is in the air)")]
        [Tooltip("With no rider, ease back towards restOffset (0 = start positions).")]
        public bool autoReturn = true;
        public float restOffset = 0f;
        [Tooltip("How much of the way back it goes each time the rider leaves. 1 = all the way to Rest Offset, 0.5 = halfway, 0 = stays where it was left (like a real balanced pulley).")]
        [Range(0f, 1f)] public float returnAmount = 1f;
        [Tooltip("How hard it is pulled back, per second. Higher = gets back sooner (it never overshoots). It only moves while nobody stands on it, so a short jump only gives it a short time.")]
        public float returnStrength = 4f;
        [Tooltip("Top speed while returning (units/s). 0 = same as Max Speed. With a big Max Travel this is usually what limits how far it gets back during one jump.")]
        public float returnSpeed = 0f;
        [Tooltip("Spin the wheel visual. 0 = keep it still.")]
        public float wheelRadius = 0.6f;

        [Header("Juice")]
        [Tooltip("Platform squash when the worm lands on it (0.25 = 25%, more for harder landings).")]
        [Range(0f, 0.6f)] public float landingSquash = 0.25f;
        [Tooltip("Speed (units/s) a platform must have when it reaches the end of its travel to clunk (shake, dust, sound).")]
        public float clunkFromSpeed = 1.5f;
        [Tooltip("Bounce back at the end of the travel: share of the speed sent back (0 = stops dead).")]
        [Range(0f, 0.6f)] public float endBounce = 0f;
        [Tooltip("Camera shake of the clunk at full speed (units).")]
        public float clunkShake = 0.12f;

        float travel;   // >0: A down, B up
        float velocity;
        float returnTarget;
        bool wasRidden;
        Vector2 restA;
        Vector2 restB;
        bool initialised;
        float wheelBaseAngle;
        WormController subscribedWorm;
        float lastCreak = -99f;
        float lastLandingKick = -99f;
        float peakSpeed;
        float prevSpeed;
        bool wasAtEnd;
        float lastClunk = -99f;

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
            returnTarget = restOffset;
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
            // Only a real landing counts: not the small re-contacts of a worm already riding a moving platform
            // (kicking on those pushed the platform away again and again, so it shook and the worm could not launch).
            if ((p.HasRider && Time.time - p.RiderSince > 0.15f) || Time.time - lastLandingKick < 0.4f || hit.normalSpeed < 3f) return;
            lastLandingKick = Time.time;
            float total = Mathf.Max(0.1f, platformA.baseWeight + platformB.baseWeight + Mathf.Abs(counterBias));
            float kick = hit.impactForce * landingKick / total;
            velocity += p == platformA ? kick : -kick; // A pushed down = positive travel
            velocity = Mathf.Clamp(velocity, -maxSpeed, maxSpeed);
            float k01 = Mathf.Clamp01(hit.impactForce / 60f);
            p.Squash(landingSquash * Mathf.Lerp(0.35f, 1f, k01));
            (p == platformA ? platformB : platformA).Squash(-landingSquash * 0.4f * k01); // the other side jerks up
            GameAudio.PlayAt("rope_twang", p.transform.position, Mathf.Lerp(0.35f, 0.9f, k01), Mathf.Lerp(1.15f, 0.85f, k01));
            Creak(Mathf.Abs(velocity));
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
            float chase = damping;
            bool anyoneRiding = platformA.HasRider || platformB.HasRider;
            if (anyoneRiding)
            {
                wasRidden = true;
                if (sinkPerWeight > 0f)
                    targetVel = Mathf.Clamp((restOffset + (loadA - loadB) * sinkPerWeight - travel) * damping, -maxSpeed, maxSpeed);
                else
                    targetVel = Mathf.Clamp((loadA - loadB) * moveSpeed, -maxSpeed, maxSpeed);
            }
            else if (autoReturn)
            {
                // The rider just left: decide how far back this return goes.
                if (wasRidden) { wasRidden = false; returnTarget = Mathf.Lerp(travel, restOffset, returnAmount); }
                float strength = Mathf.Max(0.01f, returnStrength);
                float cap = returnSpeed > 0f ? Mathf.Min(returnSpeed, maxSpeed) : maxSpeed;
                float goal = returnAmount >= 0.999f ? restOffset : returnTarget;
                targetVel = Mathf.Clamp((goal - travel) * strength, -cap, cap);
                chase = Mathf.Max(damping, 4f * strength); // critically damped: gets there without bouncing past
            }
            else
                targetVel = 0f;

            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, chase) * dt);
            velocity += (targetVel - velocity) * k;
            if (endEase > 0.001f)
            {
                // Ease into the travel limits so the platforms never slam to a stop.
                float room = velocity > 0f ? maxTravel - travel : maxTravel + travel;
                float cap = Mathf.Max(0.05f, room / endEase) * maxSpeed;
                velocity = Mathf.Clamp(velocity, -cap, cap);
            }
            float before = velocity;
            peakSpeed = Mathf.Max(peakSpeed, Mathf.Abs(velocity));
            travel = Mathf.Clamp(travel + velocity * dt, -maxTravel, maxTravel);
            bool atEnd = (travel == maxTravel && velocity > 0f) || (travel == -maxTravel && velocity < 0f);
            if (atEnd)
            {
                // Arriving at the end after a fast run: clunk once (End Ease may have slowed the last bit, so use the run's top speed).
                if (!wasAtEnd && peakSpeed >= clunkFromSpeed) Clunk(peakSpeed);
                peakSpeed = 0f;
                velocity = Mathf.Abs(before) >= clunkFromSpeed ? -before * endBounce : 0f; // only a real slam bounces back
            }
            wasAtEnd = Mathf.Abs(travel) >= maxTravel - 0.01f;
            if (Mathf.Abs(velocity) > 1f && prevSpeed <= 1f) Creak(Mathf.Abs(velocity)); // starts moving
            prevSpeed = Mathf.Abs(velocity);

            platformA.MoveTo(new Vector2(restA.x, restA.y - travel));
            platformB.MoveTo(new Vector2(restB.x, restB.y + travel));
        }

        void Clunk(float speed)
        {
            if (Time.time - lastClunk < 0.3f) return;
            lastClunk = Time.time;
            float k = Mathf.Clamp01(speed / Mathf.Max(0.1f, maxSpeed));
            GameJuice.Shake(clunkShake * k);
            platformA.Squash(0.15f * k);
            platformB.Squash(0.15f * k);
            Vector2 low = travel > 0f ? (Vector2)platformA.transform.position : (Vector2)platformB.transform.position;
            GameJuice.Puff(low, Vector2.up, 6, 0.9f);
            GameAudio.PlayAt("pulley_clunk", low, Mathf.Lerp(0.4f, 1f, k));
        }

        void Creak(float speed)
        {
            if (Time.time - lastCreak < 0.6f) return;
            lastCreak = Time.time;
            Vector2 at = wheel != null ? (Vector2)wheel.position : (Vector2)transform.position;
            GameAudio.PlayAt("pulley_creak", at, Mathf.Clamp(0.25f + speed * 0.08f, 0.25f, 0.7f), Mathf.Clamp(0.85f + speed * 0.04f, 0.85f, 1.3f));
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
