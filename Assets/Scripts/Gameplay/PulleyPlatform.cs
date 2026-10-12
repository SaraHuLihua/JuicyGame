using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// One side of a PulleySystem. Kinematic body moved by the system (MovePosition, like Piston),
    /// so a riding worm is carried through the normal ground-velocity path and can launch normally.
    /// Standing is detected robustly: the worm counts as riding only while grounded AND its
    /// GroundDetector.Carrier is this platform — brushing past from below/side never triggers it.
    /// Needs a (non-trigger) BoxCollider2D; Surface + Rigidbody2D are auto-added if missing.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PulleyPlatform : MonoBehaviour, ISurfaceCarrier
    {
        [Tooltip("Extra dead weight of this platform itself (both sides default equal, so they cancel).")]
        public float baseWeight = 1f;
        [Tooltip("Surface type for friction/bounce. Lint/Metal allow landing (launch works from here).")]
        public SurfaceType surfaceType = SurfaceType.Metal;

        Rigidbody2D body;

        /// <summary>Worm standing on this platform right now (null if none).</summary>
        public WormController Rider { get; private set; }
        public bool HasRider { get { return Rider != null; } }

        void Awake()
        {
            EnsureBody();
            Surface s = GetComponent<Surface>();
            if (s == null) { s = gameObject.AddComponent<Surface>(); s.type = surfaceType; s.ApplyMaterial(); }
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col.isTrigger) Debug.LogWarning("[PulleyPlatform] " + name + " is a trigger: the worm cannot stand on it.", this);
        }

        void EnsureBody()
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        }

        void FixedUpdate()
        {
            if (body == null) EnsureBody();
            WormController w = WormController.Instance;
            if (w != null && !w.IsDead && w.Physics.IsGrounded
                && w.Physics.Ground != null && w.Physics.Ground.Carrier == (ISurfaceCarrier)this)
                Rider = w;
            else
                Rider = null;
        }

        /// <summary>Effective load on this side: platform + riding worm's REAL mass (Squishy 0.3 / Heavy 2.5).</summary>
        public float CurrentLoad(float wormWeightMultiplier)
        {
            float load = baseWeight;
            if (Rider != null && Rider.Physics != null)
                load += Rider.Physics.Mass * wormWeightMultiplier;
            return load;
        }

        internal void MoveTo(Vector2 worldPos)
        {
            if (body == null) EnsureBody();
            body.MovePosition(worldPos);
        }

        // ISurfaceCarrier: the platform itself adds no slide; the worm inherits the body's motion.
        public Vector2 GetCarryVelocity(WormMaterial material) { return Vector2.zero; }
        public float GetCarryAcceleration(WormMaterial material) { return 0f; }
    }
}
