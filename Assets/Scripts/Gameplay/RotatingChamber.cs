using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Rotating passage / chamber (used on FG_Props_1_1, the hairy C-shaped ring in Area_B).
    /// A KINEMATIC Rigidbody2D turned with MoveRotation in FixedUpdate, so the worm inside is moved by real
    /// contacts and friction (it rides up the wall a little and settles), never teleported, and the walls
    /// cannot be tunnelled through. The wall collider is generated as a clean arc (inner wall + outer wall,
    /// no hair spikes to snag on) with a real gap where the art's opening is.
    ///
    /// Modes:
    ///   PlayerTriggered (default): waits with the opening facing Entry Direction. When the worm has been
    ///     inside for Enter Delay, turns until the opening faces Exit Direction, the worm drops out, and
    ///     after it has left the chamber turns back to wait for the next visit.
    ///   Continuous: turns forever at Rotation Speed.
    ///   StopAtAngles: turns and pauses at each angle of Stop Angles (opening directions) in order.
    /// All directions are WORLD angles of the opening: 0 = right, 90 = up, 180 = left, -90 = down.
    /// The Scene view shows the opening (cyan), entry (green) and exit (orange) when selected.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class RotatingChamber : MonoBehaviour
    {
        public enum Mode { PlayerTriggered, Continuous, StopAtAngles }
        public enum Direction { Clockwise, CounterClockwise }

        [Header("Rotation")]
        public Mode mode = Mode.PlayerTriggered;
        [Tooltip("Top turning speed, degrees per second.")]
        public float rotationSpeed = 70f;
        [Tooltip("Direction used when turning towards the exit / in Continuous and StopAtAngles.")]
        public Direction direction = Direction.Clockwise;
        [Tooltip("Degrees per second squared. Lower = softer starts and stops (keeps the worm stable).")]
        public float acceleration = 160f;
        [Tooltip("Start with the opening facing Start Direction (otherwise keeps the rotation it has in the scene). PlayerTriggered always starts at Entry Direction.")]
        public bool useStartDirection = false;
        [Tooltip("World direction of the opening at start (degrees).")]
        public float startDirection = 0f;
        [Tooltip("Continuous / StopAtAngles: begin turning as soon as the level starts. Off = call StartRotating() (UnityEvent, trigger...).")]
        public bool startAutomatically = true;

        [Header("PlayerTriggered")]
        [Tooltip("Opening direction while waiting for the worm (face it towards where the worm arrives).")]
        public float entryDirection = 0f;
        [Tooltip("Opening direction that lets the worm fall out (-90 = straight down). A little past -90 in the turning direction helps, because the worm rides up the wall while it turns.")]
        public float exitDirection = -100f;
        [Tooltip("Seconds the worm must stay inside before the chamber starts turning (ignores quick fly-throughs).")]
        public float enterDelay = 0.4f;
        [Tooltip("Seconds to wait with the opening at the exit before checking if the worm left.")]
        public float exitHoldTime = 0.6f;
        [Tooltip("Seconds after the worm has left before turning back to the entry.")]
        public float returnDelay = 0.8f;
        [Tooltip("If the worm is still inside this long after reaching the exit, turn back to the entry anyway (0 = wait forever).")]
        public float giveUpAfter = 6f;

        [Header("StopAtAngles")]
        [Tooltip("Opening directions to pause at, in turning order (degrees).")]
        public float[] stopAngles = { 0f, -90f };
        [Tooltip("Seconds to pause at each stop.")]
        public float pauseAtStop = 1.5f;

        [Header("Shape (in the sprite's local units, before Transform Scale)")]
        [Tooltip("Build the wall collider from the values below on Awake (recommended). Off = keep your own collider.")]
        public bool generateCollider = true;
        [Tooltip("Centre of the ring relative to the sprite pivot. FG_Props_1_1: (-0.28, 0).")]
        public Vector2 ringCenter = new Vector2(-0.28f, 0f);
        [Tooltip("Radius of the inside wall. FG_Props_1_1: 2.75.")]
        public float innerRadius = 2.75f;
        [Tooltip("Wall thickness (solid part, without the hair). FG_Props_1_1: 0.55.")]
        public float wallThickness = 0.55f;
        [Tooltip("Direction of the opening in the sprite itself (degrees, local). FG_Props_1_1: 180 (opens to the left).")]
        public float openingLocalAngle = 180f;
        [Tooltip("Width of the opening (degrees). FG_Props_1_1: 54.")]
        public float openingWidth = 54f;
        [Range(8, 128)] public int arcSegments = 48;
        [Tooltip("How the worm treats the walls (bounce, landing).")]
        public SurfaceType surfaceType = SurfaceType.Lint;
        [Tooltip("Wall friction. Higher = the worm is carried further up the wall while turning. Keep it low enough that the worm slides back into the opening (0.25 lets it ride ~25 degrees up, inside a 54 degree opening).")]
        [Range(0f, 1f)] public float wallFriction = 0.25f;

        [Header("Events")]
        public UnityEvent onPlayerEntered;
        public UnityEvent onStartTurning;
        public UnityEvent onOpeningAtExit;
        public UnityEvent onPlayerLeft;

        enum State { Idle, WaitingForPlayer, TurningToExit, HoldingAtExit, WaitingToReturn, TurningToEntry, Spinning, TurningToStop, Paused }

        Rigidbody2D body;
        float angle;        // current Z rotation (unwrapped)
        float angVel;       // deg/s
        float target;       // target Z rotation for eased moves
        State state = State.Idle;
        float timer;
        float insideTime;
        int stopIndex;
        bool playerWasInside;

        /// <summary>World direction the opening currently faces (degrees).</summary>
        public float OpeningDirection { get { return Normalize(angle + openingLocalAngle); } }
        public bool PlayerInside { get; private set; }
        public bool IsTurning { get { return Mathf.Abs(angVel) > 0.01f; } }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            // The old setup rotated this object with a legacy Animation: that teleports the collider and traps the worm.
            Animation legacy = GetComponent<Animation>();
            if (legacy != null) { legacy.Stop(); legacy.enabled = false; }
            Animator animator = GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            if (generateCollider) BuildCollider();
            Surface s = GetComponent<Surface>();
            if (s == null) { s = gameObject.AddComponent<Surface>(); s.type = surfaceType; }
            s.ApplyMaterial();
            ApplyWallFriction();

            angle = transform.eulerAngles.z;
            if (mode == Mode.PlayerTriggered) angle = RotationFor(entryDirection);
            else if (useStartDirection) angle = RotationFor(startDirection);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            body.rotation = angle;
            target = angle;
        }

        void ApplyWallFriction()
        {
            PhysicsMaterial2D pm = new PhysicsMaterial2D(name + "_ChamberWall");
            pm.friction = wallFriction;
            pm.bounciness = 0f; // the worm computes its own bounce from the Surface type
            Collider2D[] cols = GetComponents<Collider2D>();
            for (int i = 0; i < cols.Length; i++) if (!cols[i].isTrigger) cols[i].sharedMaterial = pm;
        }

        void Start()
        {
            ApplyWallFriction(); // again after every Awake (Surface applies its own friction in its Awake)
            if (mode == Mode.PlayerTriggered) state = State.WaitingForPlayer;
            else if (startAutomatically) StartRotating();
        }

        /// <summary>Begin turning (Continuous / StopAtAngles), e.g. from a UnityEvent when startAutomatically is off.</summary>
        public void StartRotating()
        {
            if (mode == Mode.Continuous) { state = State.Spinning; onStartTurning.Invoke(); }
            else if (mode == Mode.StopAtAngles) { BeginNextStop(); onStartTurning.Invoke(); }
            else if (state == State.Idle) state = State.WaitingForPlayer;
        }

        /// <summary>Stop turning smoothly where it is.</summary>
        public void StopRotating()
        {
            state = State.Idle;
            target = angle + Mathf.Sign(angVel) * angVel * angVel / (2f * Mathf.Max(1f, acceleration));
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            UpdatePlayerInside(dt);

            switch (state)
            {
                case State.WaitingForPlayer:
                    MoveTowardsTarget(dt);
                    if (insideTime >= enterDelay)
                    {
                        onPlayerEntered.Invoke();
                        target = Unwrap(RotationFor(exitDirection), DirSign());
                        state = State.TurningToExit;
                        onStartTurning.Invoke();
                    }
                    break;

                case State.TurningToExit:
                    if (MoveTowardsTarget(dt)) { state = State.HoldingAtExit; timer = 0f; onOpeningAtExit.Invoke(); }
                    break;

                case State.HoldingAtExit:
                    MoveTowardsTarget(dt);
                    timer += dt;
                    if (timer >= exitHoldTime && !PlayerInside) { state = State.WaitingToReturn; timer = 0f; }
                    else if (giveUpAfter > 0f && timer >= exitHoldTime + giveUpAfter) { state = State.WaitingToReturn; timer = returnDelay; }
                    break;

                case State.WaitingToReturn:
                    MoveTowardsTarget(dt);
                    timer += dt;
                    if (timer >= returnDelay)
                    {
                        target = Unwrap(RotationFor(entryDirection), -DirSign()); // go back the way it came
                        state = State.TurningToEntry;
                    }
                    break;

                case State.TurningToEntry:
                    if (MoveTowardsTarget(dt)) { state = State.WaitingForPlayer; insideTime = 0f; }
                    break;

                case State.Spinning:
                    angVel = Mathf.MoveTowards(angVel, DirSign() * rotationSpeed, acceleration * dt);
                    angle += angVel * dt;
                    break;

                case State.TurningToStop:
                    if (MoveTowardsTarget(dt)) { state = State.Paused; timer = 0f; }
                    break;

                case State.Paused:
                    timer += dt;
                    if (timer >= pauseAtStop) BeginNextStop();
                    break;

                default:
                    MoveTowardsTarget(dt);
                    break;
            }

            if (Mathf.Abs(angle) > 3600f) { float w = Mathf.Repeat(angle, 360f) - angle; angle += w; target += w; }
            body.MoveRotation(angle);
        }

        void BeginNextStop()
        {
            if (stopAngles == null || stopAngles.Length == 0) { state = State.Spinning; return; }
            stopIndex = stopIndex % stopAngles.Length;
            target = Unwrap(RotationFor(stopAngles[stopIndex]), DirSign());
            if (Mathf.Abs(target - angle) < 0.5f) target += DirSign() * 360f; // already there: go round once
            stopIndex++;
            state = State.TurningToStop;
        }

        /// <summary>Eased move to target: accelerates, cruises at rotationSpeed, brakes to land exactly. True when arrived.</summary>
        bool MoveTowardsTarget(float dt)
        {
            float remaining = target - angle;
            if (Mathf.Abs(remaining) < 0.05f && Mathf.Abs(angVel) < 1f) { angle = target; angVel = 0f; return true; }
            float accel = Mathf.Max(1f, acceleration);
            float brakeSpeed = Mathf.Sqrt(2f * accel * Mathf.Abs(remaining));
            float desired = Mathf.Sign(remaining) * Mathf.Min(rotationSpeed, brakeSpeed);
            angVel = Mathf.MoveTowards(angVel, desired, accel * dt);
            float step = angVel * dt;
            if (Mathf.Abs(step) > Mathf.Abs(remaining) && Mathf.Sign(step) == Mathf.Sign(remaining)) { angle = target; angVel = 0f; return true; }
            angle += step;
            return false;
        }

        void UpdatePlayerInside(float dt)
        {
            WormController w = WormController.Instance;
            bool inside = false;
            if (w != null && !w.IsDead)
            {
                float scale = WorldScale();
                Vector2 c = transform.TransformPoint(ringCenter);
                float d = Vector2.Distance(w.transform.position, c);
                if (d < innerRadius * scale) inside = true;                                   // inside the inner wall
                else if (d < (innerRadius + wallThickness + 0.3f) * scale) inside = playerWasInside; // passing the doorway
            }
            if (playerWasInside && !inside) onPlayerLeft.Invoke();
            PlayerInside = inside;
            playerWasInside = inside;
            insideTime = inside ? insideTime + dt : 0f;
        }

        float DirSign() { return direction == Direction.Clockwise ? -1f : 1f; }

        /// <summary>Z rotation that makes the opening face a world direction.</summary>
        float RotationFor(float openingWorldDir) { return openingWorldDir - openingLocalAngle; }

        /// <summary>Equivalent of rot closest to the current angle, travelling in the given direction (or shortest if 0).</summary>
        float Unwrap(float rot, float sign)
        {
            float delta = Mathf.Repeat(rot - angle, 360f); // 0..360 counter-clockwise
            if (sign < 0f) delta = delta > 0.001f ? delta - 360f : 0f;
            return angle + delta;
        }

        static float Normalize(float a) { a = Mathf.Repeat(a + 180f, 360f) - 180f; return a; }

        float WorldScale()
        {
            Vector3 s = transform.lossyScale;
            return (Mathf.Abs(s.x) + Mathf.Abs(s.y)) * 0.5f;
        }

        // ------------------------------------------------------------------ collider

        /// <summary>Rebuilds the PolygonCollider2D as a clean arc band with the opening (also in the editor: right-click the component).</summary>
        [ContextMenu("Rebuild Collider")]
        public void BuildCollider()
        {
            PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
            if (poly == null) poly = gameObject.AddComponent<PolygonCollider2D>();
            poly.isTrigger = false;
            poly.offset = Vector2.zero;
            poly.pathCount = 1;
            poly.SetPath(0, ArcBand());
        }

        Vector2[] ArcBand()
        {
            int n = Mathf.Clamp(arcSegments, 8, 128);
            float half = Mathf.Clamp(openingWidth, 1f, 300f) * 0.5f;
            float start = openingLocalAngle + half;           // wall begins after the opening...
            float end = openingLocalAngle + 360f - half;      // ...and goes round to the other side of it
            float ro = innerRadius + Mathf.Max(0.05f, wallThickness);
            Vector2[] pts = new Vector2[(n + 1) * 2];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(start, end, i / (float)n);
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                pts[i] = ringCenter + dir * ro;               // outer wall, forwards
                pts[pts.Length - 1 - i] = ringCenter + dir * innerRadius; // inner wall, backwards
            }
            return pts;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Vector2 c = transform.TransformPoint(ringCenter);
            float s = WorldScale();
            float r = innerRadius * s;
            float cur = Application.isPlaying ? OpeningDirection : Normalize(transform.eulerAngles.z + openingLocalAngle);
            DrawDir(c, cur, r * 1.15f, Color.cyan);
            if (mode == Mode.PlayerTriggered)
            {
                DrawDir(c, entryDirection, r * 1.35f, Color.green);
                DrawDir(c, exitDirection, r * 1.35f, new Color(1f, 0.55f, 0f));
            }
            else if (mode == Mode.StopAtAngles && stopAngles != null)
                for (int i = 0; i < stopAngles.Length; i++) DrawDir(c, stopAngles[i], r * 1.35f, Color.yellow);
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            Vector2[] band = ArcBand();
            for (int i = 0; i < band.Length; i++)
                Gizmos.DrawLine(transform.TransformPoint(band[i]), transform.TransformPoint(band[(i + 1) % band.Length]));
        }

        static void DrawDir(Vector2 c, float deg, float len, Color col)
        {
            Gizmos.color = col;
            Vector2 d = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            Vector2 tip = c + d * len;
            Gizmos.DrawLine(c, tip);
            Vector2 side = new Vector2(-d.y, d.x) * len * 0.08f;
            Gizmos.DrawLine(tip, tip - d * len * 0.12f + side);
            Gizmos.DrawLine(tip, tip - d * len * 0.12f - side);
        }
#endif
    }
}
