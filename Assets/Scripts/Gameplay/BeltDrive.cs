using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Belt drive: a closed belt wrapped around any number of wheels (pulleys), all turning together.
    /// The belt is a real surface: the worm can land on it and is carried along like a conveyor
    /// (same grip rule as CarrierChain), and it inherits the belt's speed when it launches.
    ///   - Wheels are listed in order around the loop. The belt path is rebuilt from the wheels'
    ///     positions and radii (true tangent lines + arcs), so moving a wheel in the Scene view reshapes
    ///     the belt and its collider, also in Edit mode.
    ///   - A wheel inside the loop turns the same way as the belt. A wheel marked Outside (an idler pressing
    ///     on the belt from outside) turns the other way.
    ///   - Every wheel's spin matches the belt speed at its rim, so nothing visibly slips.
    /// Hierarchy (built by the BeltDrive prefab, all Inspector-editable):
    ///   BeltDrive (this + Surface)
    ///   ├── WheelSmall / WheelMedium / WheelLarge (sprites, spin)
    ///   └── Belt (LineRenderer + EdgeCollider2D)
    /// </summary>
    [ExecuteAlways]
    public class BeltDrive : MonoBehaviour, IPointSurfaceCarrier
    {
        public enum Turn { Clockwise, CounterClockwise }

        [System.Serializable]
        public class Pulley
        {
            public Transform wheel;
            [Tooltip("Radius of the wheel's rim, where the belt sits (units).")]
            public float radius = 1f;
            [Tooltip("Off = the wheel is inside the loop (normal). On = an idler pressing on the belt from outside; it turns the other way.")]
            public bool outside = false;
        }

        [Header("Wheels, in order around the loop")]
        public List<Pulley> pulleys = new List<Pulley>();

        [Header("Belt (auto-found on children if empty)")]
        public LineRenderer beltLine;
        public EdgeCollider2D beltCollider;

        [Header("Movement")]
        [Tooltip("Which way the belt runs around the loop. Wheels inside the loop turn this way too.")]
        public Turn direction = Turn.Clockwise;
        [Tooltip("Belt speed in units/s. It is also the rim speed of every wheel.")]
        public float speed = 2.5f;
        [Tooltip("Off = the belt stands still. Can be switched from UnityEvents with StartBelt() / StopBelt().")]
        public bool running = true;
        [Tooltip("Seconds to get from stopped to full speed (and back). 0 = instant.")]
        public float spinUpTime = 0.4f;

        [Header("Carrying the worm")]
        [Tooltip("Off = the belt is a plain solid surface that does not move the worm.")]
        public bool carriesPlayer = true;
        [Tooltip("u/s^2 pulling Squishy up to belt speed. Must beat the surface friction (about 14 u/s^2 for Squishy on Metal).")]
        public float squishyGrip = 45f;
        [Tooltip("u/s^2 for Heavy. Keep it above about 42 (Heavy's friction on Metal); closer to it = lags more when boarding.")]
        public float heavyGrip = 55f;

        [Header("Look")]
        [Tooltip("Belt thickness (units). Also the collision thickness.")]
        public float thickness = 0.12f;
        public Color color = new Color(0.05f, 0.045f, 0.045f);
        [Tooltip("Optional belt material (e.g. a tiling belt texture). Empty = plain coloured line.")]
        public Material beltMaterial;
        public int sortingOrder = 6;
        [Tooltip("Points for a full turn around a wheel (arcs use a share of it).")]
        [Range(8, 96)] public int pointsPerWheel = 32;

        [Header("Cleats (marks that show the belt moving)")]
        public bool showCleats = true;
        [Tooltip("Distance between two marks along the belt.")]
        public float cleatSpacing = 0.5f;
        [Tooltip("x = length along the belt, y = height across it.")]
        public Vector2 cleatSize = new Vector2(0.07f, 0.09f);
        public Color cleatColor = new Color(0.2f, 0.19f, 0.18f);

        // Path in this object's local space, in belt travel order (clockwise around the loop).
        readonly List<Vector2> path = new List<Vector2>();
        float[] cumulative = new float[0];
        float pathLength;
        int signature;
        bool pathValid;

        float currentSpeed;   // signed: > 0 = clockwise
        float travel;         // distance the belt has moved (for the cleats)
        float[] wheelAngles = new float[0];
        Quaternion[] wheelBaseRotations = new Quaternion[0];
        Transform[] cleats;
        Material runtimeMaterial;
        Vector3[] linePoints = new Vector3[0];

        /// <summary>Signed belt speed right now (eased). Positive = clockwise.</summary>
        public float CurrentSpeed { get { return currentSpeed; } }

        float TargetSpeed
        {
            get { return running ? speed * (direction == Turn.Clockwise ? 1f : -1f) : 0f; }
        }

        // ------------------------------------------------------------------ setup

        void Awake()
        {
            FindParts();
            if (Application.isPlaying)
            {
                Surface s = GetComponent<Surface>();
                if (s == null) { s = gameObject.AddComponent<Surface>(); s.type = SurfaceType.Metal; s.ApplyMaterial(); }
                currentSpeed = TargetSpeed;
                CaptureWheelRotations();
            }
            Rebuild(true);
        }

        void Start()
        {
            if (Application.isPlaying) BuildCleats();
        }

        void FindParts()
        {
            if (beltLine == null) beltLine = GetComponentInChildren<LineRenderer>();
            if (beltCollider == null) beltCollider = GetComponentInChildren<EdgeCollider2D>();
        }

        void CaptureWheelRotations()
        {
            wheelBaseRotations = new Quaternion[pulleys.Count];
            wheelAngles = new float[pulleys.Count];
            for (int i = 0; i < pulleys.Count; i++)
                if (pulleys[i] != null && pulleys[i].wheel != null) wheelBaseRotations[i] = pulleys[i].wheel.localRotation;
        }

        void OnValidate()
        {
            thickness = Mathf.Max(0.01f, thickness);
            cleatSpacing = Mathf.Max(0.05f, cleatSpacing);
            spinUpTime = Mathf.Max(0f, spinUpTime);
            for (int i = 0; i < pulleys.Count; i++)
                if (pulleys[i] != null) pulleys[i].radius = Mathf.Max(0.01f, pulleys[i].radius);
            signature = 0; // force a rebuild
        }

        void OnDestroy()
        {
            if (runtimeMaterial == null) return;
            if (Application.isPlaying) Destroy(runtimeMaterial); else DestroyImmediate(runtimeMaterial);
        }

        // ------------------------------------------------------------------ public controls (UnityEvents)

        public void StartBelt() { running = true; }
        public void StopBelt() { running = false; }
        public void ToggleBelt() { running = !running; }
        public void ReverseDirection() { direction = direction == Turn.Clockwise ? Turn.CounterClockwise : Turn.Clockwise; }
        public void SetSpeed(float unitsPerSecond) { speed = unitsPerSecond; }

        // ------------------------------------------------------------------ per frame

        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            float target = TargetSpeed;
            if (spinUpTime <= 0f) currentSpeed = target;
            else currentSpeed = Mathf.MoveTowards(currentSpeed, target, Mathf.Max(Mathf.Abs(speed), 0.01f) / spinUpTime * Time.fixedDeltaTime);
        }

        void LateUpdate()
        {
            if (beltLine == null || beltCollider == null) FindParts();
            Rebuild(false);
            if (!Application.isPlaying) return;

            float dt = Time.deltaTime;
            travel += currentSpeed * dt;
            SpinWheels(dt);
            PlaceCleats();
        }

        void SpinWheels(float dt)
        {
            if (wheelAngles.Length != pulleys.Count) CaptureWheelRotations();
            for (int i = 0; i < pulleys.Count; i++)
            {
                Pulley p = pulleys[i];
                if (p == null || p.wheel == null || p.radius <= 0f) continue;
                // Clockwise is a negative Z rotation in Unity. An outside idler turns the other way.
                float side = p.outside ? -1f : 1f;
                wheelAngles[i] = Mathf.Repeat(wheelAngles[i] - side * currentSpeed * dt / p.radius * Mathf.Rad2Deg, 360f);
                p.wheel.localRotation = wheelBaseRotations[i] * Quaternion.Euler(0f, 0f, wheelAngles[i]);
            }
        }

        // ------------------------------------------------------------------ belt path

        int ComputeSignature()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + pulleys.Count;
                h = h * 31 + thickness.GetHashCode();
                h = h * 31 + pointsPerWheel;
                h = h * 31 + transform.localToWorldMatrix.GetHashCode();
                for (int i = 0; i < pulleys.Count; i++)
                {
                    Pulley p = pulleys[i];
                    if (p == null || p.wheel == null) { h = h * 31 + 1; continue; }
                    h = h * 31 + p.wheel.position.GetHashCode();
                    h = h * 31 + p.radius.GetHashCode();
                    h = h * 31 + (p.outside ? 3 : 5);
                }
                return h == 0 ? 1 : h;
            }
        }

        void Rebuild(bool force)
        {
            int sig = ComputeSignature();
            if (!force && sig == signature) return;
            signature = sig;
            pathValid = BuildPath();
            ApplyLine();
            ApplyCollider();
        }

        /// <summary>Builds the closed belt path (local space) in clockwise travel order. False if the wheels overlap.</summary>
        bool BuildPath()
        {
            path.Clear();
            List<Vector2> c = new List<Vector2>();
            List<float> r = new List<float>();
            List<float> s = new List<float>();
            for (int i = 0; i < pulleys.Count; i++)
            {
                Pulley p = pulleys[i];
                if (p == null || p.wheel == null) continue;
                c.Add(transform.InverseTransformPoint(p.wheel.position));
                r.Add(p.radius + thickness * 0.5f); // belt centre line sits on the rim
                s.Add(p.outside ? -1f : 1f);        // +1 = wheel on the right of the belt's travel
            }
            int n = c.Count;
            if (n < 2) return false;

            // The path is built clockwise. If the wheels were listed counter-clockwise, walk them backwards.
            float area = 0f;
            for (int i = 0; i < n; i++) { Vector2 a = c[i], b = c[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            if (area > 0f) { c.Reverse(); r.Reverse(); s.Reverse(); }

            Vector2[] pin = new Vector2[n];
            Vector2[] pout = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 D = c[j] - c[i];
                float d2 = D.sqrMagnitude;
                float k = s[j] * r[j] - s[i] * r[i];
                float l2 = d2 - k * k;
                if (l2 <= 1e-6f) return false; // wheels overlap or touch: no straight run between them
                float L = Mathf.Sqrt(l2);
                Vector2 dir = (L * D + k * new Vector2(-D.y, D.x)) / d2;   // travel direction of this straight run
                Vector2 right = new Vector2(dir.y, -dir.x);                 // right-hand normal
                pout[i] = c[i] - s[i] * r[i] * right;
                pin[j] = c[j] - s[j] * r[j] * right;
            }

            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.Atan2(pin[i].y - c[i].y, pin[i].x - c[i].x);
                float a1 = Mathf.Atan2(pout[i].y - c[i].y, pout[i].x - c[i].x);
                bool cw = s[i] > 0f;
                float sweep = cw ? Mathf.Repeat(a0 - a1, 2f * Mathf.PI) : Mathf.Repeat(a1 - a0, 2f * Mathf.PI);
                int seg = Mathf.Max(1, Mathf.CeilToInt(pointsPerWheel * sweep / (2f * Mathf.PI)));
                for (int t = 0; t <= seg; t++)
                {
                    float a = a0 + (cw ? -1f : 1f) * sweep * t / seg;
                    Vector2 pt = c[i] + r[i] * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    if (path.Count == 0 || (path[path.Count - 1] - pt).sqrMagnitude > 1e-8f) path.Add(pt);
                }
            }
            if (path.Count > 1 && (path[0] - path[path.Count - 1]).sqrMagnitude < 1e-8f) path.RemoveAt(path.Count - 1);

            cumulative = new float[path.Count + 1];
            pathLength = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                cumulative[i] = pathLength;
                pathLength += (path[(i + 1) % path.Count] - path[i]).magnitude;
            }
            cumulative[path.Count] = pathLength;
            return path.Count >= 3 && pathLength > 0f;
        }

        void ApplyLine()
        {
            if (beltLine == null) return;
            beltLine.useWorldSpace = true;
            beltLine.loop = true;
            beltLine.startWidth = thickness;
            beltLine.endWidth = thickness;
            beltLine.startColor = color;
            beltLine.endColor = color;
            beltLine.sortingOrder = sortingOrder;
            beltLine.numCornerVertices = 2;
            if (beltMaterial != null) beltLine.sharedMaterial = beltMaterial;
            else if (beltLine.sharedMaterial == null)
            {
                if (runtimeMaterial == null)
                {
                    Shader sh = Shader.Find("Sprites/Default");
                    if (sh != null) { runtimeMaterial = new Material(sh); runtimeMaterial.hideFlags = HideFlags.HideAndDontSave; }
                }
                if (runtimeMaterial != null) beltLine.sharedMaterial = runtimeMaterial;
            }

            if (!pathValid) { beltLine.positionCount = 0; return; }
            if (linePoints.Length != path.Count) linePoints = new Vector3[path.Count];
            float z = transform.position.z;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 w = transform.TransformPoint(path[i]);
                linePoints[i] = new Vector3(w.x, w.y, z);
            }
            beltLine.positionCount = linePoints.Length;
            beltLine.SetPositions(linePoints);
        }

        void ApplyCollider()
        {
            if (beltCollider == null || !pathValid) return;
            Transform ct = beltCollider.transform;
            Vector2[] pts = new Vector2[path.Count + 1];
            for (int i = 0; i <= path.Count; i++)
                pts[i] = ct.InverseTransformPoint(transform.TransformPoint(path[i % path.Count]));
            beltCollider.points = pts;
            beltCollider.edgeRadius = thickness * 0.5f;
        }

        // ------------------------------------------------------------------ cleats

        void BuildCleats()
        {
            if (!showCleats || !pathValid || beltLine == null) return;
            int count = Mathf.Max(2, Mathf.RoundToInt(pathLength / cleatSpacing));
            cleats = new Transform[count];
            Transform holder = new GameObject("Cleats").transform;
            holder.SetParent(transform, false);
            for (int i = 0; i < count; i++)
                cleats[i] = PlaceholderSprites.Make("Cleat", holder, PlaceholderSprites.Square, cleatColor, Vector2.zero, cleatSize, sortingOrder + 1).transform;
        }

        void PlaceCleats()
        {
            if (cleats == null || !pathValid) return;
            float step = pathLength / cleats.Length;
            for (int i = 0; i < cleats.Length; i++)
            {
                Vector2 tangent;
                Vector2 p = PointAt(Mathf.Repeat(i * step + travel, pathLength), out tangent);
                Vector3 w = transform.TransformPoint(p);
                cleats[i].position = new Vector3(w.x, w.y, transform.position.z);
                Vector2 wt = transform.TransformDirection(tangent);
                cleats[i].rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(wt.y, wt.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>Point (local) at a distance along the path, plus the travel direction there.</summary>
        Vector2 PointAt(float distance, out Vector2 tangent)
        {
            int lo = 0, hi = path.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= distance) lo = mid; else hi = mid - 1;
            }
            Vector2 a = path[lo], b = path[(lo + 1) % path.Count];
            float segLen = cumulative[lo + 1] - cumulative[lo];
            float t = segLen > 0f ? (distance - cumulative[lo]) / segLen : 0f;
            tangent = segLen > 0f ? (b - a) / segLen : Vector2.right;
            return Vector2.Lerp(a, b, t);
        }

        // ------------------------------------------------------------------ ISurfaceCarrier

        float Grip(WormMaterial m) { return m == WormMaterial.Squishy ? squishyGrip : heavyGrip; }

        /// <summary>Velocity of the belt where the worm touches it (world space).</summary>
        public Vector2 GetCarryVelocityAt(WormMaterial m, Vector2 worldPoint)
        {
            if (!carriesPlayer || !pathValid || Mathf.Abs(currentSpeed) < 0.0001f) return Vector2.zero;
            Vector2 p = transform.InverseTransformPoint(worldPoint);
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 a = path[i], b = path[(i + 1) % path.Count];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-8f));
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            Vector2 seg = path[(best + 1) % path.Count] - path[best];
            Vector2 dir = ((Vector2)transform.TransformDirection(seg)).normalized;
            return dir * currentSpeed;
        }

        /// <summary>Without a contact point the belt cannot know which way it moves there.</summary>
        public Vector2 GetCarryVelocity(WormMaterial m) { return Vector2.zero; }

        public float GetCarryAcceleration(WormMaterial m) { return carriesPlayer ? Grip(m) : 0f; }

        // ------------------------------------------------------------------ gizmos

        void OnDrawGizmosSelected()
        {
            for (int i = 0; i < pulleys.Count; i++)
            {
                Pulley p = pulleys[i];
                if (p == null || p.wheel == null) continue;
                Gizmos.color = p.outside ? new Color(1f, 0.6f, 0.1f) : Color.cyan;
                Vector3 c = p.wheel.position;
                const int seg = 40;
                for (int k = 0; k < seg; k++)
                {
                    float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                    Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * p.radius, c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * p.radius);
                }
            }
            if (!pathValid || path.Count < 2) return;
            // Arrows along the belt show the running direction.
            Gizmos.color = Color.green;
            float sign = TargetSpeed >= 0f ? 1f : -1f;
            int arrows = Mathf.Max(3, Mathf.RoundToInt(pathLength / 2f));
            for (int i = 0; i < arrows; i++)
            {
                Vector2 tan;
                Vector2 p = PointAt(pathLength * i / arrows, out tan);
                Vector3 w = transform.TransformPoint(p);
                Vector3 d = transform.TransformDirection(tan * sign) * 0.35f;
                Vector3 side = new Vector3(-d.y, d.x) * 0.5f;
                Gizmos.DrawLine(w - d, w + d);
                Gizmos.DrawLine(w + d, w + d * 0.3f + side);
                Gizmos.DrawLine(w + d, w + d * 0.3f - side);
            }
        }
    }
}
