using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Spring piston (prefab: Prefabs/Gameplay/Trampoline, built from the "Trampoline" object in MechanicsTest).
    /// A heavy worm (mass >= Trigger Mass: Heavy 2.5 yes, Squishy 0.3 no) landing or standing on the head
    /// pushes it down; after Charge Time it springs up and throws the worm at Launch Speed.
    /// While it charges the player can aim as usual (click and drag): when it fires, the worm goes the way
    /// it is aimed, within Max Steer Angle of straight up. Not aiming = straight up. Releasing the drag
    /// before it fires is a normal launch and leaves the trampoline.
    /// The head is moved with MovePosition on a kinematic Rigidbody2D (like Piston), so the worm rides it down.
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
        [Tooltip("Lightest worm that sets it off (worm mass: Heavy 2.5, Squishy 0.3). Lighter worms just stand on it.")]
        public float triggerMass = 1.5f;
        [Tooltip("Seconds from the moment it is set off to the throw. The player can aim during this time.")]
        [Min(0.05f)] public float chargeTime = 1f;

        [Header("Movement (local units, along the object's up)")]
        [Tooltip("How far the head sinks while charging.")]
        public float compressDepth = 0.6f;
        [Tooltip("Seconds to sink down (part of Charge Time).")]
        [Min(0.01f)] public float compressTime = 0.25f;
        [Tooltip("Seconds the head takes to shoot back up when it fires.")]
        [Min(0.01f)] public float fireTime = 0.06f;
        [Tooltip("How far past the rest position it overshoots when it fires, then settles back.")]
        public float fireOvershoot = 0.15f;
        [Tooltip("Seconds to settle back to rest after firing.")]
        [Min(0.01f)] public float returnTime = 0.3f;
        [Tooltip("Seconds after a throw before it can be set off again.")]
        [Min(0f)] public float cooldown = 0.4f;
        [Tooltip("Shake of the head while it is charged (visual tell). 0 = none.")]
        public float chargeShake = 0.03f;

        [Header("Throw")]
        [Tooltip("Speed the worm is thrown at (units/s), the same for every material. Heavy falls fast: 32 throws it about 8 units high.")]
        public float launchSpeed = 32f;
        [Tooltip("How far from straight up the player can steer the throw while aiming (degrees). 0 = always straight up.")]
        [Range(0f, 90f)] public float maxSteerAngle = 60f;
        [Tooltip("On = the aim's power (drag length) also sets the throw speed, from Min Power Share to 100%.")]
        public bool aimPowerSetsSpeed = false;
        [Range(0f, 1f)] public float minPowerShare = 0.5f;

        [Header("Events")]
        public UnityEvent onCharge;
        public UnityEvent onFire;

        Rigidbody2D headBody;
        Vector3 restLocal;
        State state = State.Idle;
        float timer;
        float cooldownUntil;
        float offset; // current head offset along local up (0 = rest, negative = pressed down)

        public State CurrentState { get { return state; } }

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

        /// <summary>Set it off from a script or UnityEvent, whoever stands on it.</summary>
        public void Trigger()
        {
            if (state != State.Idle || Time.time < cooldownUntil) return;
            state = State.Compressing;
            timer = 0f;
            onCharge.Invoke();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            timer += dt;
            WormController w = WormController.Instance;
            bool onHead = WormOnHead(w);

            switch (state)
            {
                case State.Idle:
                    offset = 0f;
                    if (onHead && w.Physics.Mass >= triggerMass) Trigger();
                    break;
                case State.Compressing:
                    offset = -compressDepth * Smooth(Mathf.Clamp01(timer / compressTime));
                    if (timer >= compressTime) state = State.Charged;
                    if (timer >= chargeTime) Fire(w, onHead);
                    break;
                case State.Charged:
                    offset = -compressDepth;
                    if (timer >= chargeTime) Fire(w, onHead);
                    break;
                case State.Firing:
                {
                    float t = Mathf.Clamp01(timer / fireTime);
                    offset = Mathf.Lerp(-compressDepth, fireOvershoot, t);
                    if (t >= 1f) { state = State.Returning; timer = 0f; }
                    break;
                }
                case State.Returning:
                {
                    float t = Mathf.Clamp01(timer / returnTime);
                    offset = Mathf.Lerp(fireOvershoot, 0f, Smooth(t));
                    if (t >= 1f) { state = State.Idle; offset = 0f; }
                    break;
                }
            }

            Vector3 local = restLocal + Vector3.up * offset;
            if (state == State.Charged && chargeShake > 0f) local += (Vector3)(Random.insideUnitCircle * chargeShake);
            Vector3 parentPos = head.parent != null ? head.parent.TransformPoint(local) : local;
            headBody.MovePosition(parentPos);
        }

        void Fire(WormController w, bool onHead)
        {
            state = State.Firing;
            timer = 0f;
            cooldownUntil = Time.time + fireTime + returnTime + cooldown;
            onFire.Invoke();
            if (!onHead) return; // it left during the charge: the spring fires empty

            Vector2 up = transform.up;
            Vector2 dir = up;
            float speed = launchSpeed;
            WormLauncher aim = w.Launcher;
            if (aim != null && aim.IsAiming && aim.AimValid)
            {
                // Steer: the aimed direction, kept within Max Steer Angle of straight up.
                float a = Vector2.SignedAngle(up, aim.AimDirection);
                a = Mathf.Clamp(a, -maxSteerAngle, maxSteerAngle);
                dir = Rotate(up, a);
                if (aimPowerSetsSpeed) speed *= Mathf.Lerp(minPowerShare, 1f, aim.AimPower);
            }
            // Exact speed whatever the mass: impulse = mass x (target velocity - current velocity).
            WormPhysics ph = w.Physics;
            ph.AddExternalImpulse((dir * speed - ph.Velocity) * ph.Mass);
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
