using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Visual rope for a PulleySystem: Platform A -> over the wheel -> Platform B, drawn with a LineRenderer.
    /// Visual only (no segment physics), so it can never destabilise the rig.
    ///   - The ends are glued to the platforms' attach points every rendered frame (LateUpdate, after
    ///     Rigidbody2D interpolation), so the rope never lags behind or detaches.
    ///   - Over the wheel it leaves/meets the rim at the real tangent points and wraps around the top.
    ///   - Each free side is a curve whose middle can sag and sway on a damped spring driven by the
    ///     platforms' movement: a little elastic life, but the endpoints never move off the platforms.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public class PulleyRope : MonoBehaviour
    {
        [Header("Connections (auto-found from the parent PulleySystem if empty)")]
        public PulleySystem system;
        [Tooltip("Where the rope ties onto each platform, in the platform's local space (0, 0.5) = top centre of a 1x1 box.")]
        public Vector2 attachPointA = new Vector2(0f, 0.5f);
        public Vector2 attachPointB = new Vector2(0f, 0.5f);
        [Tooltip("Rope radius around the wheel (units). 0 = use PulleySystem.wheelRadius.")]
        public float wrapRadius = 0f;

        [Header("Look")]
        public float width = 0.08f;
        public Color color = new Color(0.08f, 0.07f, 0.07f);
        [Tooltip("Optional rope material (e.g. a tiling rope texture). Empty = plain coloured line.")]
        public Material ropeMaterial;
        [Tooltip("Repeat the material's texture along the rope (use with a tiling rope texture).")]
        public bool tileTexture = false;
        public int sortingOrder = 4;
        [Range(2, 32)] public int pointsPerSide = 12;
        [Range(2, 32)] public int pointsOverWheel = 10;

        [Header("Elasticity / smoothing (visual only)")]
        [Tooltip("Constant downward bow in the middle of each side (units). 0 = perfectly straight.")]
        public float sag = 0.04f;
        [Tooltip("How much platform movement makes the rope sway (0 = never sways).")]
        public float swayAmount = 0.035f;
        [Tooltip("Spring stiffness of the sway. Higher = quicker, tighter wobble.")]
        public float swayStiffness = 90f;
        [Tooltip("Damping of the sway. Higher = settles faster (12+ = almost no wobble).")]
        public float swayDamping = 9f;
        [Tooltip("Largest sideways bow the sway can reach (units).")]
        public float maxSway = 0.25f;

        LineRenderer line;
        Vector3[] pts = new Vector3[0];
        Material runtimeMaterial;
        // spring state per side
        float swayA, swayVelA, swayB, swayVelB;
        Vector2 lastA, lastB, lastVelA, lastVelB;
        bool hasLast;

        void Awake()
        {
            Setup();
        }

        void Setup()
        {
            line = GetComponent<LineRenderer>();
            if (line == null)
            {
                if (!Application.isPlaying) return; // never add components to the scene from edit mode
                line = gameObject.AddComponent<LineRenderer>();
            }
            if (system == null) system = GetComponentInParent<PulleySystem>();
            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            ApplyLook();
        }

        void ApplyLook()
        {
            if (line == null) return;
            line.startWidth = width;
            line.endWidth = width;
            line.sortingOrder = sortingOrder;
            line.textureMode = tileTexture ? LineTextureMode.Tile : LineTextureMode.Stretch;
            if (ropeMaterial != null) line.sharedMaterial = ropeMaterial;
            else if (line.sharedMaterial == null)
            {
                if (runtimeMaterial == null)
                {
                    Shader sh = Shader.Find("Sprites/Default");
                    if (sh != null) { runtimeMaterial = new Material(sh); runtimeMaterial.hideFlags = HideFlags.HideAndDontSave; }
                }
                if (runtimeMaterial != null) line.sharedMaterial = runtimeMaterial;
            }
            line.startColor = color;
            line.endColor = color;
        }

        void OnValidate()
        {
            pointsPerSide = Mathf.Clamp(pointsPerSide, 2, 32);
            pointsOverWheel = Mathf.Clamp(pointsOverWheel, 2, 32);
            if (line != null) ApplyLook();
        }

        void OnDestroy()
        {
            if (runtimeMaterial == null) return;
            if (Application.isPlaying) Destroy(runtimeMaterial); else DestroyImmediate(runtimeMaterial);
        }

        /// <summary>Kept for older callers. The rope now reads the rig itself every frame.</summary>
        public void SetPoints(Vector2 a, Vector2 pulley, Vector2 b) { }

        void LateUpdate()
        {
            if (line == null) Setup();
            if (line == null || system == null || system.platformA == null || system.platformB == null) return;

            Vector2 a = system.platformA.transform.TransformPoint(attachPointA);
            Vector2 b = system.platformB.transform.TransformPoint(attachPointB);
            Transform wheel = system.wheel;
            Vector2 c = wheel != null ? (Vector2)wheel.position : (Vector2)system.transform.position;
            float r = wrapRadius > 0f ? wrapRadius : Mathf.Max(0.01f, system.wheelRadius);

            UpdateSway(a, b);

            // Over the top: clockwise when A is on the left, counter-clockwise when A is on the right.
            bool clockwise = a.x <= b.x;
            Vector2 ta = Tangent(a, c, r, true, clockwise);
            Vector2 tb = Tangent(b, c, r, false, clockwise);
            float angA = Mathf.Atan2(ta.y - c.y, ta.x - c.x);
            float angB = Mathf.Atan2(tb.y - c.y, tb.x - c.x);
            if (clockwise) { while (angB > angA) angB -= 2f * Mathf.PI; }
            else { while (angB < angA) angB += 2f * Mathf.PI; }

            int ns = pointsPerSide, nw = pointsOverWheel;
            int total = ns + (nw - 1) + (ns - 1);
            if (pts.Length != total) pts = new Vector3[total];
            int k = 0;
            AddSide(a, ta, swayA, ns, ref k, false);               // A ... tangent A (inclusive)
            for (int i = 1; i < nw; i++)                            // arc, ends exactly on tangent B
            {
                float ang = Mathf.Lerp(angA, angB, i / (float)(nw - 1));
                pts[k++] = new Vector3(c.x + Mathf.Cos(ang) * r, c.y + Mathf.Sin(ang) * r, 0f);
            }
            AddSide(tb, b, swayB, ns, ref k, true);                // tangent B (skipped, already added) ... B
            line.positionCount = k;
            line.SetPositions(pts);
        }

        void AddSide(Vector2 from, Vector2 to, float sway, int n, ref int k, bool skipFirst)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            Vector2 perp = len > 0.0001f ? new Vector2(-d.y, d.x) / len : Vector2.right;
            for (int i = skipFirst ? 1 : 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float bow = 4f * t * (1f - t); // 0 at both ends: endpoints are never moved
                Vector2 p = from + d * t + (perp * sway + Vector2.down * sag) * bow;
                if (k < pts.Length) pts[k++] = p;
            }
        }

        void UpdateSway(Vector2 a, Vector2 b)
        {
            float dt = Application.isPlaying ? Mathf.Min(Time.deltaTime, 1f / 30f) : 0f;
            if (!hasLast || dt <= 0f)
            {
                lastA = a; lastB = b; lastVelA = Vector2.zero; lastVelB = Vector2.zero; hasLast = true;
                return;
            }
            Vector2 velA = (a - lastA) / dt, velB = (b - lastB) / dt;
            Vector2 accA = (velA - lastVelA) / dt, accB = (velB - lastVelB) / dt;
            lastA = a; lastB = b; lastVelA = velA; lastVelB = velB;
            // Vertical jolts (start/stop, a worm landing) kick the bow sideways a little, then it rings out.
            Step(ref swayA, ref swayVelA, -accA.y * swayAmount * 0.01f + accA.x * swayAmount * 0.01f, dt);
            Step(ref swayB, ref swayVelB, accB.y * swayAmount * 0.01f - accB.x * swayAmount * 0.01f, dt);
        }

        void Step(ref float x, ref float v, float force, float dt)
        {
            float acc = force * swayStiffness - swayStiffness * x - swayDamping * v;
            v += acc * dt;
            x += v * dt;
            if (x > maxSway) { x = maxSway; v = Mathf.Min(v, 0f); }
            if (x < -maxSway) { x = -maxSway; v = Mathf.Max(v, 0f); }
        }

        /// <summary>
        /// Tangent point on circle (c, r) for a rope end at p. The rope travels around the wheel
        /// (clockwise or not); p is the start (A side) or the end (B side) of that travel.
        /// </summary>
        static Vector2 Tangent(Vector2 p, Vector2 c, float r, bool pIsStart, bool clockwise)
        {
            Vector2 d = p - c;
            float dist = d.magnitude;
            if (dist <= r + 0.0001f) return c + (pIsStart == clockwise ? Vector2.left : Vector2.right) * r;
            float baseAng = Mathf.Atan2(d.y, d.x);
            float off = Mathf.Acos(r / dist);
            Vector2 t1 = c + new Vector2(Mathf.Cos(baseAng + off), Mathf.Sin(baseAng + off)) * r;
            Vector2 t2 = c + new Vector2(Mathf.Cos(baseAng - off), Mathf.Sin(baseAng - off)) * r;
            return Wraps(t1, p, c, pIsStart, clockwise) ? t1 : t2;
        }

        static bool Wraps(Vector2 t, Vector2 p, Vector2 c, bool pIsStart, bool clockwise)
        {
            Vector2 dir = pIsStart ? t - p : p - t;   // direction of travel at the tangent point
            Vector2 toC = c - t;
            float cross = dir.x * toC.y - dir.y * toC.x; // < 0: wheel centre on the right of travel
            return clockwise ? cross < 0f : cross > 0f;
        }
    }
}
