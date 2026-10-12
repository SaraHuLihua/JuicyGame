using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Bounce pad (prefab: Prefabs/Gameplay/BouncePad, the FG_Props_1_3 mushroom cap with its FG_Props_1_6 spring).
    /// When the worm touches it, it is thrown on an exact arc to the Next pad (or to the Target), so a row of pads
    /// carries it zigzag up to an exit by itself: pure show, no aiming. The arc is computed from the worm's own
    /// gravity at that moment, so Squishy and Rocky follow the same path (Rocky just flies it faster).
    /// Chain them: pad 1 Next = pad 2, ..., last pad Next = empty and Target = the exit point.
    /// The trigger is the BoxCollider2D on this object; the worm passes through the pad, it is never solid.
    /// </summary>
    [DisallowMultipleComponent]
    public class BouncePad : MonoBehaviour
    {
        [Header("Where it throws the worm")]
        [Tooltip("The next pad of the zigzag. The worm is thrown into its Landing Point.")]
        public BouncePad next;
        [Tooltip("Used when Next is empty: the exit (an empty object where the worm should land).")]
        public Transform target;
        [Tooltip("Height of the arc above the higher of the two ends (units). Low = flat, quick hops; high = big loops (watch the ceiling).")]
        [Min(0f)] public float arcHeight = 1.5f;

        [Header("Landing point (where the previous pad aims)")]
        [Tooltip("Distance in front of the cap, along its up, where the worm is aimed (world units).")]
        public float landingOffset = 1.1f;

        [Header("Ride")]
        [Tooltip("No aiming / switching while the pads carry the worm. Given back when it lands or after the last throw.")]
        public bool lockInputDuringRide = true;
        [Tooltip("Seconds before the same pad can throw again.")]
        [Min(0f)] public float rearmTime = 0.35f;

        [Header("Juice")]
        [Tooltip("The spring child (FG_Props_1_6): stretched on every bounce. Empty = none.")]
        public Transform spring;
        [Tooltip("How much the cap squashes on a bounce (0.3 = 30%).")]
        [Range(0f, 0.8f)] public float squash = 0.35f;
        [Tooltip("Seconds of the squash-and-spring wobble.")]
        [Min(0.05f)] public float wobbleTime = 0.35f;
        public AudioClip bounceSound;
        [Range(0f, 1f)] public float bounceVolume = 0.6f;
        public UnityEvent onBounce;

        Vector3 baseScale;
        Vector3 springBaseScale;
        float wobbleTimer = -1f;
        float rearmUntil;
        AudioSource audioSource;
        static float rideUntil;      // shared: the whole zigzag counts as one ride
        static bool rideLocked;

        /// <summary>World point the previous pad aims at.</summary>
        public Vector2 LandingPoint { get { return (Vector2)transform.position + (Vector2)transform.up * landingOffset; } }

        void Awake()
        {
            baseScale = transform.localScale;
            if (spring != null) springBaseScale = spring.localScale;
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
            if (bounceSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        void OnTriggerEnter2D(Collider2D other) { TryBounce(other); }
        void OnTriggerStay2D(Collider2D other) { TryBounce(other); }

        void TryBounce(Collider2D other)
        {
            if (Time.time < rearmUntil) return;
            WormController w = WormController.Instance;
            if (w == null || w.IsDead || w.Physics == null || other.attachedRigidbody != w.Physics.Body) return;
            Vector2 aim;
            if (next != null && next != this) aim = next.LandingPoint;
            else if (target != null) aim = target.position;
            else return;
            Throw(w, aim);
        }

        void Throw(WormController w, Vector2 aim)
        {
            WormPhysics ph = w.Physics;
            Rigidbody2D rb = ph.Body;
            Vector2 from = rb.position;
            float g = Mathf.Max(0.01f, -Physics2D.gravity.y * rb.gravityScale);
            // Ballistic arc whose top is Arc Height above the higher end: up-time from the start, down-time to the aim.
            float apex = Mathf.Max(from.y, aim.y) + arcHeight;
            float vy = Mathf.Sqrt(2f * g * Mathf.Max(0f, apex - from.y));
            float flight = vy / g + Mathf.Sqrt(2f * Mathf.Max(0f, apex - aim.y) / g);
            Vector2 v = new Vector2((aim.x - from.x) / Mathf.Max(0.05f, flight), vy);

            ph.AllowOverspeed(v.magnitude + 1f, flight + 0.1f);
            ph.AddExternalImpulse((v - ph.Velocity) * ph.Mass); // exact velocity whatever the mass
            rearmUntil = Time.time + rearmTime;

            if (lockInputDuringRide)
            {
                bool last = next == null || next == this;
                rideUntil = Time.time + flight + (last ? 0.1f : 0.6f);
                if (!rideLocked)
                {
                    rideLocked = true;
                    w.inputLocked = true;
                }
            }

            wobbleTimer = 0f;
            if (audioSource != null) audioSource.PlayOneShot(bounceSound, bounceVolume);
            onBounce.Invoke();
        }

        void Update()
        {
            if (rideLocked && Time.time >= rideUntil)
            {
                rideLocked = false;
                WormController w = WormController.Instance;
                if (w != null) w.inputLocked = false;
            }

            if (wobbleTimer < 0f) return;
            wobbleTimer += Time.deltaTime;
            float t = Mathf.Clamp01(wobbleTimer / wobbleTime);
            // Squash down, spring past the rest shape, settle (damped wave).
            float wave = Mathf.Cos(t * Mathf.PI * 2.5f) * (1f - t);
            float sy = 1f - squash * wave;
            float sx = 1f + squash * 0.5f * wave;
            transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
            if (spring != null) spring.localScale = new Vector3(springBaseScale.x, springBaseScale.y * (1f + squash * 1.5f * wave), springBaseScale.z);
            if (t >= 1f)
            {
                wobbleTimer = -1f;
                transform.localScale = baseScale;
                if (spring != null) spring.localScale = springBaseScale;
            }
        }

        void OnDisable()
        {
            transform.localScale = baseScale;
            if (spring != null) spring.localScale = springBaseScale;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Vector2 from = LandingPoint;
            Vector2 aim;
            if (next != null && next != this) aim = next.LandingPoint;
            else if (target != null) aim = target.position;
            else return;
            // Arc preview (shape only; it does not depend on gravity).
            float apex = Mathf.Max(from.y, aim.y) + arcHeight;
            float vy = Mathf.Sqrt(2f * Mathf.Max(0f, apex - from.y));
            float flight = vy + Mathf.Sqrt(2f * Mathf.Max(0f, apex - aim.y));
            float vx = (aim.x - from.x) / Mathf.Max(0.05f, flight);
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Vector3 prev = from;
            for (int i = 1; i <= 24; i++)
            {
                float t = flight * i / 24f;
                Vector3 p = new Vector3(from.x + vx * t, from.y + vy * t - 0.5f * t * t, 0f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.DrawWireSphere(aim, 0.4f);
        }
#endif
    }
}
