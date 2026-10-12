using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Two toothed rollers turning in opposite directions (mirror images of each other).
    /// When their grooves face each other a gap opens along the pass line (this object's local X axis);
    /// when teeth face each other the gap closes. Touching a roller pushes the worm (Squishy flies, Heavy holds);
    /// being touched by BOTH rollers at once crushes it: the worm respawns at its last landing.
    ///
    /// Everything is generated in Awake from the fields below, so you can tune it in the Inspector and press Play.
    /// </summary>
    public class RollerJaws : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Degrees per second of the top roller (the bottom one mirrors it).")]
        public float rotationSpeed = 45f;
        [Tooltip("If on, rotationSpeed is derived so the gap opens once every cycleTime seconds.")]
        public bool useCycleTime = false;
        [Tooltip("Seconds between two gap openings (only used when useCycleTime is on).")]
        public float cycleTime = 4f;
        [Tooltip("Starting angle, to offset several jaws.")]
        public float startPhase = 0f;

        [Header("Geometry")]
        [Tooltip("Gap (units) between the roller bodies when grooves face each other. The worm diameter is about 0.7.")]
        public float gapSize = 1.5f;
        public float rollerRadius = 1.2f;
        public float toothHeight = 1.0f;
        [Range(2, 6), Tooltip("2 gives the widest open windows. More teeth = shorter windows.")]
        public int teethCount = 2;
        [Range(0.2f, 0.9f)] public float toothWidthFraction = 0.45f;

        [Header("Contact")]
        [Tooltip("Impulse (mass * speed) applied when a roller touches the worm. Squishy gets more delta-v than Heavy.")]
        public float impactForce = 6f;
        public bool crushKills = true;

        [Header("Look")]
        public Color rollerColor = new Color(0.36f, 0.42f, 0.47f);
        public Color toothColor = new Color(0.55f, 0.60f, 0.66f);

        Rigidbody2D rbA, rbB;
        Collider2D colA, colB;
        float angle;
        float crushCooldown;

        /// <summary>Seconds between gap openings with the current settings.</summary>
        public float CycleSeconds { get { return (360f / Mathf.Max(1, teethCount)) / Mathf.Max(0.01f, Mathf.Abs(EffectiveSpeed)); } }
        float EffectiveSpeed { get { return useCycleTime ? (360f / Mathf.Max(1, teethCount)) / Mathf.Max(0.1f, cycleTime) : rotationSpeed; } }
        float CenterDistance { get { return 2f * rollerRadius + gapSize; } }

        void Awake()
        {
            Build();
            angle = startPhase;
        }

        void Build()
        {
            rbA = CreateRoller("RollerTop", new Vector2(0f, CenterDistance * 0.5f), out colA);
            rbB = CreateRoller("RollerBottom", new Vector2(0f, -CenterDistance * 0.5f), out colB);
        }

        Rigidbody2D CreateRoller(string rollerName, Vector2 localPos, out Collider2D collider)
        {
            GameObject go = new GameObject(rollerName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            PolygonCollider2D poly = go.AddComponent<PolygonCollider2D>();
            poly.SetPath(0, GearPoints());
            collider = poly;

            Surface s = go.AddComponent<Surface>();
            s.type = SurfaceType.Metal;

            RollerHit hit = go.AddComponent<RollerHit>();
            hit.owner = this;

            // visuals: a disc plus one rectangle per tooth, all rotating with the body
            PlaceholderSprites.Make("Disc", go.transform, PlaceholderSprites.Circle, rollerColor, Vector2.zero, Vector2.one * rollerRadius * 2f, 5);
            float step = 360f / teethCount;
            float toothWidth = Mathf.Max(0.2f, rollerRadius * step * toothWidthFraction * Mathf.Deg2Rad);
            for (int i = 0; i < teethCount; i++)
            {
                float a = i * step;
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                SpriteRenderer t = PlaceholderSprites.Make("Tooth", go.transform, PlaceholderSprites.Square, toothColor, dir * (rollerRadius + toothHeight * 0.5f - 0.05f), new Vector2(toothHeight + 0.1f, toothWidth), 6);
                t.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            return rb;
        }

        Vector2[] GearPoints()
        {
            int n = Mathf.Max(2, teethCount);
            float step = 2f * Mathf.PI / n;
            float tw = step * toothWidthFraction;
            float rr = rollerRadius;
            float rt = rollerRadius + toothHeight;
            Vector2[] pts = new Vector2[n * 4];
            for (int i = 0; i < n; i++)
            {
                float c = i * step;
                pts[i * 4 + 0] = Polar(rr, c - tw * 0.5f);
                pts[i * 4 + 1] = Polar(rt, c - tw * 0.3f);
                pts[i * 4 + 2] = Polar(rt, c + tw * 0.3f);
                pts[i * 4 + 3] = Polar(rr, c + tw * 0.5f);
            }
            return pts;
        }

        static Vector2 Polar(float r, float a) { return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            angle += EffectiveSpeed * dt;
            float rootZ = transform.eulerAngles.z;
            rbA.MoveRotation(rootZ + angle);
            rbB.MoveRotation(rootZ - angle); // mirror image: teeth meet and grooves meet at the same moments

            crushCooldown -= dt;
            if (crushKills && crushCooldown <= 0f)
            {
                WormController w = WormController.Instance;
                if (w != null && w.Physics.Collider.IsTouching(colA) && w.Physics.Collider.IsTouching(colB))
                {
                    crushCooldown = 0.5f;
                    w.Kill("crushed by roller jaws");
                }
            }
        }

        /// <summary>Called by the roller child when something touches it.</summary>
        public void OnRollerTouched(Collision2D c)
        {
            WormPhysics w = c.collider.GetComponent<WormPhysics>();
            if (w == null || c.contactCount == 0) return;
            ContactPoint2D cp = c.GetContact(0);
            Vector2 n = cp.normal;
            if (Vector2.Dot(n, w.Body.position - cp.point) < 0f) n = -n; // away from the roller
            w.AddExternalImpulse(n * impactForce);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 a = transform.TransformPoint(new Vector3(-4f, 0f, 0f));
            Vector3 b = transform.TransformPoint(new Vector3(4f, 0f, 0f));
            Gizmos.DrawLine(a, b); // pass line
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(0f, CenterDistance * 0.5f, 0f)), rollerRadius);
            Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(0f, -CenterDistance * 0.5f, 0f)), rollerRadius);
        }
    }
}
