using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// A heavy wheel the worm pushes into a hole to cross it (FG_Props_1_55 (1) in Area F).
    /// It stays parked where it was placed (even on a slope) until a heavy enough worm (mass >= Min Pusher Mass:
    /// Heavy 2.5 yes, Squishy 0.3 no) pushes it from the side. From then on it is a real rolling body:
    /// it rolls down the slope, drops into the hole and, once it has come to rest lower than where it started,
    /// it locks in place (Static) so the worm can walk across it.
    /// Rigidbody2D and CircleCollider2D are added at Play if the object has none (radius from the sprite).
    /// T (DebugTools reset) puts it back.
    /// </summary>
    [DisallowMultipleComponent]
    public class PushableWheel : MonoBehaviour
    {
        public static readonly List<PushableWheel> All = new List<PushableWheel>();

        [Header("Push")]
        [Tooltip("Lightest worm that can get it moving (worm mass: Heavy 2.5, Squishy 0.3). 0 = anyone.")]
        [Min(0f)] public float minPusherMass = 1.5f;
        [Tooltip("Extra spin given when the push frees it (degrees/s), so it starts rolling at once.")]
        public float startSpin = 120f;

        [Header("Body")]
        [Tooltip("Mass of the wheel. Rocky is 2.5: a heavier wheel feels heavier to push.")]
        [Min(0.1f)] public float mass = 6f;
        public float gravityScale = 1.6f;
        [Tooltip("Slows the rolling (higher = it stops sooner on flat ground).")]
        [Min(0f)] public float rollingResistance = 0.6f;
        [Tooltip("Grip on the floor: high = it rolls instead of sliding.")]
        [Range(0f, 1f)] public float friction = 0.9f;
        [Tooltip("Collider radius in local units. 0 = taken from the sprite.")]
        [Min(0f)] public float radius = 0f;

        [Header("Lock in the hole")]
        [Tooltip("Becomes Static (a solid bridge) once it has stopped at least this far below where it started.")]
        public bool lockWhenSettled = true;
        [Min(0f)] public float minDrop = 2f;
        [Tooltip("Speed under which it counts as stopped (units/s).")]
        [Min(0f)] public float settleSpeed = 0.4f;
        [Tooltip("Seconds it must stay stopped before locking.")]
        [Min(0f)] public float settleTime = 0.4f;

        [Header("Events")]
        public UnityEvent onPushed;
        public UnityEvent onLocked;

        Rigidbody2D body;
        Vector3 startPos;
        Quaternion startRot;
        bool released;
        bool locked;
        float stillTimer;
        static readonly ContactPoint2D[] contacts = new ContactPoint2D[8];

        public bool IsReleased { get { return released; } }
        public bool IsLocked { get { return locked; } }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Awake()
        {
            startPos = transform.position;
            startRot = transform.rotation;

            CircleCollider2D col = GetComponent<CircleCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<CircleCollider2D>();
                float r = radius;
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (r <= 0f && sr != null && sr.sprite != null) r = Mathf.Min(sr.sprite.bounds.extents.x, sr.sprite.bounds.extents.y) * 0.985f;
                if (r > 0f) col.radius = r;
            }
            else if (radius > 0f) col.radius = radius;
            PhysicsMaterial2D mat = new PhysicsMaterial2D(name + " (wheel)");
            mat.friction = friction;
            mat.bounciness = 0f;
            col.sharedMaterial = mat;

            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.mass = mass;
            body.gravityScale = gravityScale;
#if UNITY_6000_0_OR_NEWER
            body.angularDamping = rollingResistance;
            body.linearDamping = 0.05f;
#else
            body.angularDrag = rollingResistance;
            body.drag = 0.05f;
#endif
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Park();
        }

        void Park()
        {
            released = false;
            locked = false;
            stillTimer = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.constraints = RigidbodyConstraints2D.FreezeAll; // solid, but does not roll down the slope by itself
        }

        void OnCollisionStay2D(Collision2D c) { TryRelease(c); }
        void OnCollisionEnter2D(Collision2D c) { TryRelease(c); }

        void TryRelease(Collision2D c)
        {
            if (released) return;
            WormController w = WormController.Instance;
            if (w == null || w.IsDead || w.Physics == null || c.rigidbody != w.Physics.Body) return;
            if (w.Physics.Mass < minPusherMass) return;
            int n = c.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                Vector2 fromWorm = (Vector2)transform.position - contacts[i].point;
                if (Mathf.Abs(fromWorm.normalized.x) < 0.5f) continue; // landing on top does not push it
                float side = Mathf.Sign(fromWorm.x);
                Release(side);
                return;
            }
        }

        /// <summary>Free it (also from scripts / UnityEvents). Side = +1 rolls right, -1 left.</summary>
        public void Release(float side)
        {
            if (released) return;
            released = true;
            body.constraints = RigidbodyConstraints2D.None;
            body.WakeUp();
            body.angularVelocity = -side * startSpin; // clockwise rolls right
            onPushed.Invoke();
        }

        void FixedUpdate()
        {
            if (!released || locked || !lockWhenSettled) return;
            bool still = body.GetVelocity().magnitude < settleSpeed && Mathf.Abs(body.angularVelocity) < settleSpeed * 30f;
            bool dropped = startPos.y - body.position.y >= minDrop;
            stillTimer = still && dropped ? stillTimer + Time.fixedDeltaTime : 0f;
            if (stillTimer >= settleTime) Lock();
        }

        void Lock()
        {
            locked = true;
            body.SetVelocity(Vector2.zero);
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Static;
            onLocked.Invoke();
        }

        /// <summary>Back to where it was placed (DebugTools T).</summary>
        public void Restore()
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.SetVelocity(Vector2.zero);
            body.angularVelocity = 0f;
            transform.SetPositionAndRotation(startPos, startRot);
            body.position = startPos;
            body.rotation = startRot.eulerAngles.z;
            Park();
        }
    }
}
