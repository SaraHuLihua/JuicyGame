using System;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// All worm physics: material properties, bounce rules, landing detection, launching, surface carry.
    /// Uses a normal Rigidbody2D. Bounce is done here (not by PhysicsMaterial2D) so every worm-material /
    /// surface pair can have its own restitution, exactly like the web prototype.
    /// Knows nothing about visuals or input.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(GroundDetector))]
    public class WormPhysics : MonoBehaviour
    {
        [Header("Material profiles (empty = built-in defaults from the web prototype)")]
        public WormMaterialProfile squishyProfile;
        public WormMaterialProfile heavyProfile;

        [Header("Landing")]
        [Tooltip("Speed (relative to the ground) under which the worm counts as settled. Web: 40 px/s.")]
        public float restMaxSpeed = 40f / 32f;
        [Tooltip("Seconds the worm must stay settled before it can launch again.")]
        public float settleTime = 0.05f;
        [Tooltip("After a launch/kick the ground is ignored this long so the worm is not re-grounded instantly.")]
        public float ungroundAfterLaunch = 0.12f;
        [Tooltip("Safety net: if the worm rests on a non-landing surface (Membrane) this long, launching is allowed. 0 = off.")]
        public float rescueSeconds = 2f;

        [Header("Safety")]
        public float maxSpeed = GameSettings.MaxWormSpeed;

        Rigidbody2D rb;
        CircleCollider2D col;
        GroundDetector ground;
        WormMaterialProfile profile;
        bool initialised;

        Vector2 prevVelocity;
        float settledTimer;
        float nonLandingTimer;
        float ungroundUntil;
        bool canLaunch;

        public WormMaterial Material { get; private set; }
        public WormMaterialProfile Profile { get { EnsureInit(); return profile; } }
        public Rigidbody2D Body { get { EnsureInit(); return rb; } }
        public CircleCollider2D Collider { get { EnsureInit(); return col; } }
        public GroundDetector Ground { get { EnsureInit(); return ground; } }
        public float Mass { get { EnsureInit(); return rb.mass; } }
        public Vector2 Velocity { get { EnsureInit(); return rb.GetVelocity(); } }
        public float Speed { get { return Velocity.magnitude; } }

        /// <summary>Touching a surface that allows landing (or rescued). Material switching needs this.</summary>
        public bool IsGrounded { get; private set; }
        /// <summary>Grounded AND settled: the player may aim and launch.</summary>
        public bool CanLaunch { get { return canLaunch; } }
        public SurfaceType? GroundSurfaceType { get { return ground != null && ground.HasSupport && ground.Surface != null ? ground.Surface.type : (SurfaceType?)null; } }

        public event Action<WormMaterial> MaterialApplied;
        public event Action<ImpactInfo> Impact;
        /// <summary>Raised once each time the worm becomes launch-ready after having been in motion.</summary>
        public event Action<Vector2> Landed;
        public event Action<Vector2> Launched;

        void Awake()
        {
            EnsureInit();
        }

        void EnsureInit()
        {
            if (initialised) return;
            initialised = true;
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();
            ground = GetComponent<GroundDetector>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
            ApplyMaterial(WormMaterial.Squishy, false);
        }

        // ------------------------------------------------------------------ material

        public void SetMaterial(WormMaterial m)
        {
            EnsureInit();
            if (m == Material && profile != null) return;
            ApplyMaterial(m, true);
        }

        void ApplyMaterial(WormMaterial m, bool notify)
        {
            Material = m;
            WormMaterialProfile p = m == WormMaterial.Squishy ? squishyProfile : heavyProfile;
            profile = p != null ? p : WormMaterialProfile.GetDefault(m);

            float oldRadius = col.radius;
            rb.mass = profile.mass;
            rb.gravityScale = profile.gravityScale;
            col.radius = profile.radius;
            if (profile.radius > oldRadius && IsGrounded)
                rb.position += Vector2.up * (profile.radius - oldRadius); // do not grow into the floor

            PhysicsMaterial2D pm = new PhysicsMaterial2D("Worm_" + m);
            pm.friction = profile.bodyFriction;
            pm.bounciness = 0f;
            col.sharedMaterial = pm;

            if (notify && MaterialApplied != null) MaterialApplied(m);
        }

        // ------------------------------------------------------------------ physics step

        void FixedUpdate()
        {
            EnsureInit();
            float dt = Time.fixedDeltaTime;

            ground.Probe(col);
            bool support = ground.HasSupport && Time.time >= ungroundUntil;
            bool valid = support && ground.ValidLanding;
            Vector2 v = rb.GetVelocity();

            // Surfaces that move the worm (cilia wave, chain).
            Vector2 groundVel = Vector2.zero;
            if (support && ground.Carrier != null)
            {
                Vector2 target = ground.Carrier.GetCarryVelocity(Material);
                float accel = ground.Carrier.GetCarryAcceleration(Material);
                float targetSpeed = target.magnitude;
                if (targetSpeed > 0.001f && accel > 0f)
                {
                    Vector2 dir = target / targetSpeed;
                    float along = Vector2.Dot(v, dir);
                    float next = Mathf.MoveTowards(along, targetSpeed, accel * dt);
                    v += dir * (next - along);
                }
                groundVel = target;
            }
            if (support && ground.Body != null && ground.Body.bodyType != RigidbodyType2D.Static)
                groundVel += ground.Body.GetVelocity();

            // Settling is measured relative to whatever the worm stands on.
            float relSpeed = (v - groundVel).magnitude;
            float limit = canLaunch ? restMaxSpeed * 1.5f : restMaxSpeed; // small hysteresis against jitter
            if (valid && relSpeed <= limit) settledTimer += dt; else settledTimer = 0f;

            // Never get stuck forever resting on a membrane.
            if (support && !ground.ValidLanding && relSpeed <= restMaxSpeed) nonLandingTimer += dt; else nonLandingTimer = 0f;
            bool rescued = rescueSeconds > 0f && nonLandingTimer >= rescueSeconds;

            IsGrounded = valid || rescued;
            bool ready = settledTimer >= settleTime || rescued;
            if (ready && !canLaunch)
            {
                canLaunch = true;
                if (Landed != null) Landed(rb.position);
            }
            else if (!ready && canLaunch)
            {
                canLaunch = false;
            }

            // Global speed cap (web: 1400 px/s).
            if (v.sqrMagnitude > maxSpeed * maxSpeed) v = v.normalized * maxSpeed;
            rb.SetVelocity(v);

            // Remember the velocity the solver is about to use: impacts are computed from it.
            prevVelocity = v + Physics2D.gravity * rb.gravityScale * dt;
        }

        // ------------------------------------------------------------------ launching

        /// <summary>Velocity a launch would give right now. The launcher's preview uses the same function.</summary>
        public Vector2 PredictLaunchVelocity(Vector2 direction, float power01, float forceMultiplier)
        {
            EnsureInit();
            float speed = Mathf.Lerp(profile.minLaunchSpeed, profile.maxLaunchSpeed, Mathf.Clamp01(power01)) * forceMultiplier;
            // Launching from a moving surface inherits its movement (chain, cilia).
            return direction.normalized * speed + rb.GetVelocity();
        }

        public void Launch(Vector2 direction, float power01, float forceMultiplier)
        {
            EnsureInit();
            Vector2 v = PredictLaunchVelocity(direction, power01, forceMultiplier);
            // Same result as AddForce(Impulse, mass * speed) but independent of mass, as in the web prototype.
            rb.SetVelocity(v);
            BeginAirborne();
            if (Launched != null) Launched(rb.position);
        }

        /// <summary>Instant push from a mechanism (piston, roller). Delta-v = impulse / mass: light worms fly further.</summary>
        public void AddExternalImpulse(Vector2 impulse)
        {
            EnsureInit();
            Vector2 v = rb.GetVelocity() + impulse / Mathf.Max(0.01f, rb.mass);
            if (v.sqrMagnitude > maxSpeed * maxSpeed) v = v.normalized * maxSpeed;
            rb.SetVelocity(v);
            BeginAirborne();
        }

        void BeginAirborne()
        {
            ungroundUntil = Time.time + ungroundAfterLaunch;
            settledTimer = 0f;
            nonLandingTimer = 0f;
            canLaunch = false;
            IsGrounded = false;
        }

        public void Teleport(Vector2 position)
        {
            EnsureInit();
            rb.position = position;
            transform.position = position;
            rb.SetVelocity(Vector2.zero);
            prevVelocity = Vector2.zero;
            settledTimer = 0f;
            nonLandingTimer = 0f;
            canLaunch = false;
            IsGrounded = false;
        }

        // ------------------------------------------------------------------ bounce

        void OnCollisionEnter2D(Collision2D c)
        {
            if (profile == null) return;

            int best = -1;
            float bestSpeed = 0f;
            Vector2 bestNormal = Vector2.up;
            for (int i = 0; i < c.contactCount; i++)
            {
                ContactPoint2D cp = c.GetContact(i);
                Vector2 n = cp.normal;
                if (Vector2.Dot(n, rb.position - cp.point) < 0f) n = -n; // make it point towards the worm
                float ns = -Vector2.Dot(prevVelocity, n);
                if (ns > bestSpeed) { bestSpeed = ns; best = i; bestNormal = n; }
            }
            if (best < 0 || bestSpeed < 0.05f) return;

            ContactPoint2D contact = c.GetContact(best);
            SurfaceProfile sp = Surface.ProfileOf(c.collider);

            ImpactInfo info = new ImpactInfo();
            info.material = Material;
            info.surface = sp.type;
            info.collider = c.collider;
            info.point = contact.point;
            info.normal = bestNormal;
            info.normalSpeed = bestSpeed;
            info.impactForce = rb.mass * bestSpeed;

            // Brittle objects decide for themselves from the impact force (no 'if Heavy').
            BreakableObject breakable = c.collider.GetComponentInParent<BreakableObject>();
            if (breakable != null && breakable.TryBreak(info))
            {
                rb.SetVelocity(prevVelocity * profile.breakSpeedKeep); // plough through, slightly slowed
                info.brokeObject = true;
                info.outSpeed = prevVelocity.magnitude * profile.breakSpeedKeep;
                if (Impact != null) Impact(info);
                return;
            }

            float restitution = profile.GetRestitution(sp.type) * sp.restitutionMultiplier;
            float outSpeed = bestSpeed * restitution;
            if (outSpeed < profile.stopBounceSpeed) outSpeed = 0f;

            Vector2 tangent = new Vector2(-bestNormal.y, bestNormal.x);
            float vt = Vector2.Dot(prevVelocity, tangent);
            bool isImpact = bestSpeed > profile.stopBounceSpeed;
            if (isImpact && bestNormal.y > 0.5f)
                vt *= Mathf.Lerp(1f, profile.landingTangentialKeep, sp.landingGrip);

            rb.SetVelocity(tangent * vt + bestNormal * outSpeed);

            info.outSpeed = outSpeed;
            if (isImpact && Impact != null) Impact(info);
        }
    }
}
