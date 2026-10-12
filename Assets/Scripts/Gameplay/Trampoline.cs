using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Spring piston (prefab: Prefabs/Gameplay/Trampoline, built from the "Trampoline" object in MechanicsTest).
    /// A heavy worm (mass >= Trigger Mass: Heavy 2.5 yes, Squishy 0.3 no) landing or standing on the head
    /// pushes it down and CHARGES it. While charged the player launches exactly as usual (click, drag, release)
    /// and the launch is multiplied by Power Multiplier: same aim, same preview arc (it shows the boosted
    /// launch), just much stronger. The head springs up with the launch. If the player doesn't launch within
    /// Auto Fire After, the trampoline throws the worm by itself (along the aim if one is being held).
    /// The head is moved with MovePosition on a kinematic Rigidbody2D (like Piston), so the worm rides it down,
    /// and it never rises more than Max Rise above its rest position, so the rod stays inside the base.
    /// Hierarchy (prefab):
    ///   Trampoline (this)
    ///   ├── FG_Props_1_41 (base, static collider)
    ///   └── FG_Props_1_3 (head: moves, collider; Rigidbody2D added at Play)
    ///       └── FG_Props_1_42 (rod, moves with the head)
    /// </summary>
    [DisallowMultipleComponent]
    public class Trampoline : MonoBehaviour
    {
        public enum State { Idle, Compressing, Charged, Firing, Returning }

        [Header("References")]
        [Tooltip("The part that moves (the mushroom top). Its children (the rod) move with it.")]
        public Transform head;

        [Header("Trigger")]
        [Tooltip("Lightest worm that charges it (worm mass: Heavy 2.5, Squishy 0.3). Lighter worms just stand on it.")]
        public float triggerMass = 1.5f;

        [Header("Super launch")]
        [Tooltip("The player's own launch is multiplied by this while the trampoline is charged. Rocky's strongest normal launch is 26.6 u/s: x2.2 = 58 u/s, about 28 units high.")]
        public float powerMultiplier = 2.2f;
        [Tooltip("Hard limit on the boosted speed (units/s). The normal limit for the worm is 43.75.")]
        public float maxLaunchSpeed = 70f;
        [Tooltip("Steering limit: the launch is kept within this many degrees of straight up (90 = anywhere in the upper half).")]
        [Range(0f, 90f)] public float maxSteerAngle = 90f;
        [Tooltip("Seconds after charging before it throws the worm by itself. 0 = waits for the player forever.")]
        [Min(0f)] public float autoFireAfter = 1.5f;
        [Tooltip("Speed of that automatic throw (units/s). It goes along the aim if the player is holding one, else straight up.")]
        public float autoLaunchSpeed = 45f;

        [Header("Head movement (local units, along the object's up)")]
        [Tooltip("How far the head sinks while charging.")]
        public float compressDepth = 0.6f;
        [Tooltip("Seconds to sink down.")]
        [Min(0.01f)] public float compressTime = 0.25f;
        [Tooltip("Seconds the head takes to shoot back up when it fires.")]
        [Min(0.01f)] public float fireTime = 0.06f;
        [Tooltip("How far past the rest position it overshoots when it fires (the 'pop'), then settles back. Clamped to Max Rise.")]
        public float fireOvershoot = 0.15f;
        [Tooltip("The head never goes higher than this above its rest position, so the rod never leaves the base.")]
        public float maxRise = 0.25f;
        [Tooltip("Seconds to settle back to rest after firing.")]
        [Min(0.01f)] public float returnTime = 0.3f;
        [Tooltip("Seconds after a throw before it can be charged again.")]
        [Min(0f)] public float cooldown = 0.4f;
        [Tooltip("Shake of the head while it is charged (visual tell). 0 = none.")]
        public float chargeShake = 0.02f;

        [Header("Events")]
        public UnityEvent onCharge;
        public UnityEvent onFire;

        Rigidbody2D headBody;
        Vector3 restLocal;
        State state = State.Idle;
        float timer;
        float chargeTimer;
        float offTimer;
        float cooldownUntil;
        float offset; // current head offset along local up (0 = rest, negative = pressed down)
        WormController subscribed;
        WormLauncher boostedLauncher;
        float launcherBaseMultiplier = 1f;

        public State CurrentState { get { return state; } }
        bool IsCharging { get { return state == State.Compressing || state == State.Charged; } }

        void Awake()
        {
            if (head == null) { Debug.LogWarning("[Trampoline] " + name + " has no head assigned.", this); enabled = false; return; }
            restLocal = head.localPosition;
            headBody = head.GetComponent<Rigidbody2D>();
            if (headBody == null) headBody = head.gameObject.AddComponent<Rigidbody2D>();
            headBody.bodyType = RigidbodyType2D.Kinematic;
            headBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            headBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        void OnDisable()
        {
            EndBoost();
            if (subscribed != null) subscribed.Launched -= OnWormLaunched;
            subscribed = null;
        }

        /// <summary>Charge it from a script or UnityEvent, whoever stands on it.</summary>
        public void Trigger()
        {
            if (state != State.Idle || Time.time < cooldownUntil) return;
            state = State.Compressing;
            timer = 0f;
            chargeTimer = 0f;
            offTimer = 0f;
            onCharge.Invoke();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            timer += dt;
            WormController w = WormController.Instance;
            if (w != subscribed)
            {
                if (subscribed != null) subscribed.Launched -= OnWormLaunched;
                subscribed = w;
                if (w != null) w.Launched += OnWormLaunched;
            }
            bool onHead = WormOnHead(w);

            switch (state)
            {
                case State.Idle:
                    offset = 0f;
                    if (onHead && w.Physics.Mass >= triggerMass) Trigger();
                    break;
                case State.Compressing:
                case State.Charged:
                    chargeTimer += dt;
                    offset = -compressDepth * Smooth(Mathf.Clamp01(chargeTimer / compressTime));
                    if (state == State.Compressing && chargeTimer >= compressTime) state = State.Charged;
                    if (onHead)
                    {
                        offTimer = 0f;
                        Boost(w);
                        if (autoFireAfter > 0f && chargeTimer >= autoFireAfter) AutoThrow(w);
                    }
                    else
                    {
                        EndBoost();
                        offTimer += dt;
                        if (offTimer >= 0.3f) Fire(); // it left without launching: release empty
                    }
                    break;
                case State.Firing:
                {
                    float top = Mathf.Min(fireOvershoot, maxRise);
                    float t = Mathf.Clamp01(timer / fireTime);
                    offset = Mathf.Lerp(-compressDepth, top, t);
                    if (t >= 1f) { state = State.Returning; timer = 0f; }
                    break;
                }
                case State.Returning:
                {
                    float top = Mathf.Min(fireOvershoot, maxRise);
                    float t = Mathf.Clamp01(timer / returnTime);
                    offset = Mathf.Lerp(top, 0f, Smooth(t));
                    if (t >= 1f) { state = State.Idle; offset = 0f; }
                    break;
                }
            }

            offset = Mathf.Min(offset, maxRise);
            Vector3 local = restLocal + Vector3.up * offset;
            if (state == State.Charged && chargeShake > 0f) local += (Vector3)(Random.insideUnitCircle * chargeShake);
            Vector3 worldPos = head.parent != null ? head.parent.TransformPoint(local) : local;
            headBody.MovePosition(worldPos);
        }

        // While charged the worm may aim and launch even though the head moves under it, the launch is
        // multiplied (the launcher's own preview shows the boosted arc) and the speed cap is lifted for the flight.
        void Boost(WormController w)
        {
            w.Physics.AllowLaunchInAir();
            w.Physics.AllowOverspeed(maxLaunchSpeed, 0.2f);
            WormLauncher l = w.Launcher;
            if (l != null && boostedLauncher != l)
            {
                EndBoost();
                boostedLauncher = l;
                launcherBaseMultiplier = l.launchForceMultiplier;
                l.launchForceMultiplier = launcherBaseMultiplier * powerMultiplier;
            }
        }

        void EndBoost()
        {
            if (boostedLauncher != null) boostedLauncher.launchForceMultiplier = launcherBaseMultiplier;
            boostedLauncher = null;
        }

        void OnWormLaunched(Vector2 position, float power01)
        {
            if (!IsCharging || boostedLauncher == null) return;
            WormPhysics ph = subscribed.Physics;
            Vector2 v = ph.Velocity;
            Vector2 steered = Steer(v);
            if (steered != v) ph.Body.SetVelocity(steered);
            ph.AllowOverspeed(maxLaunchSpeed, 3f); // keep the boosted speed for the whole flight
            Fire();
        }

        void AutoThrow(WormController w)
        {
            Vector2 dir = transform.up;
            WormLauncher aim = w.Launcher;
            if (aim != null && aim.AimValid) dir = aim.AimDirection;
            WormPhysics ph = w.Physics;
            ph.AllowOverspeed(maxLaunchSpeed, 3f);
            // Exact speed whatever the mass: impulse = mass x (target velocity - current velocity).
            ph.AddExternalImpulse((Steer(dir.normalized * Mathf.Min(autoLaunchSpeed, maxLaunchSpeed)) - ph.Velocity) * ph.Mass);
            Fire();
        }

        void Fire()
        {
            EndBoost();
            state = State.Firing;
            timer = 0f;
            cooldownUntil = Time.time + fireTime + returnTime + cooldown;
            GameJuice.Shake(0.3f);
            GameJuice.Puff(head.position, transform.up, 8, 1.2f);
            onFire.Invoke();
        }

        /// <summary>Keep a launch velocity within Max Steer Angle of the trampoline's up, and under Max Launch Speed.</summary>
        Vector2 Steer(Vector2 v)
        {
            Vector2 up = transform.up;
            float speed = Mathf.Min(v.magnitude, maxLaunchSpeed);
            if (speed < 0.001f) return v;
            float a = Vector2.SignedAngle(up, v);
            if (Mathf.Abs(a) <= maxSteerAngle && speed == v.magnitude) return v;
            return Rotate(up, Mathf.Clamp(a, -maxSteerAngle, maxSteerAngle)) * speed;
        }

        bool WormOnHead(WormController w)
        {
            if (w == null || w.IsDead || w.Physics == null || w.Physics.Ground == null) return false;
            GroundDetector g = w.Physics.Ground;
            return g.HasSupport && g.Collider != null && g.Collider.attachedRigidbody == headBody;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float Smooth(float t) { return t * t * (3f - 2f * t); }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (head == null) return;
            Vector3 p = head.position;
            Vector3 up = transform.up;
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.9f);
            Gizmos.DrawLine(p, p + up * 3f);
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.4f);
            Gizmos.DrawLine(p, p + (Vector3)Rotate(up, maxSteerAngle) * 3f);
            Gizmos.DrawLine(p, p + (Vector3)Rotate(up, -maxSteerAngle) * 3f);
        }
#endif
    }
}
