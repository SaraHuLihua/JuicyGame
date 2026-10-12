using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Turns a gear in mechanical clicks (used on FG_Props_1_45, the 10-tooth gear in Area F):
    /// a quick turn of Step Angle, a small overshoot that settles back (the "clunk"), then a pause.
    /// A KINEMATIC Rigidbody2D is turned with MoveRotation in FixedUpdate (same approach as RotatingChamber),
    /// so a worm standing on a tooth is carried by real contacts and the collider never teleports.
    /// The object keeps its own collider (PolygonCollider2D on FG_Props_1_45).
    /// Crushing: while it turns, a worm squeezed between the gear and anything else (wall, floor, another
    /// mechanism) dies, like touching a Hazard.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class GearStepper : MonoBehaviour
    {
        public enum Direction { Clockwise, CounterClockwise }

        [Header("Clicks")]
        public Direction direction = Direction.Clockwise;
        [Tooltip("Degrees per click. 36 = one tooth of a 10-tooth gear (FG_Props_1_45).")]
        public float stepAngle = 36f;
        [Tooltip("Seconds one click takes to turn.")]
        [Min(0.01f)] public float stepDuration = 0.15f;
        [Tooltip("Seconds the gear stands still between two clicks.")]
        [Min(0f)] public float pauseBetweenSteps = 0.6f;
        [Tooltip("How far past the tooth it swings before settling back, as a share of Step Angle (0 = none, 0.1 = 10%).")]
        [Range(0f, 0.5f)] public float overshoot = 0.08f;
        [Tooltip("Seconds to settle back from the overshoot.")]
        [Min(0.01f)] public float settleTime = 0.1f;

        [Header("Start")]
        [Tooltip("Begin clicking as soon as the level starts. Off = call StartTurning() (UnityEvent, trigger...).")]
        public bool startAutomatically = true;
        [Tooltip("Seconds to wait before the first click.")]
        [Min(0f)] public float startDelay = 0f;

        [Header("Crushing")]
        [Tooltip("The worm dies when the turning gear squeezes it against something else.")]
        public bool crushesPlayer = true;
        [Tooltip("How opposite the two pushes must be (1 = exactly face to face, 0.3 = anything pushing from roughly the other side).")]
        [Range(0f, 1f)] public float crushOpposition = 0.4f;
        [Tooltip("Seconds the worm must stay squeezed before it dies (filters out a tooth just brushing past).")]
        [Min(0f)] public float crushTime = 0.04f;
        public UnityEvent onCrushedPlayer;

        [Header("Sound / events (optional)")]
        [Tooltip("Played on every click. Empty = silent.")]
        public AudioClip clickSound;
        [Range(0f, 1f)] public float clickVolume = 0.5f;
        [Tooltip("Raised at the start of every click (sound, particles, other mechanisms).")]
        public UnityEvent onStep;

        [Header("Performance")]
        [Tooltip("Simplifies the PolygonCollider2D at Play (world units of error). FG_Props_1_45 has ~316 points; 0.03 keeps the teeth and drops most of them. 0 = leave it as is.")]
        [Min(0f)] public float colliderSimplify = 0.03f;

        enum Phase { Waiting, Turning, Settling }

        Rigidbody2D body;
        AudioSource audioSource;
        Phase phase = Phase.Waiting;
        bool turning;
        float timer;
        float fromAngle;   // rotation at the start of the current click
        float toAngle;     // rotation at the end of the current click (unwrapped degrees)
        float angle;
        float crushTimer;
        float sentAngle = float.NaN;
        static readonly ContactPoint2D[] contacts = new ContactPoint2D[16];

        public bool IsTurning { get { return turning; } }
        /// <summary>Clicks done since the start.</summary>
        public int StepCount { get; private set; }

        public void StartTurning() { turning = true; }
        public void StopTurning() { turning = false; } // finishes the current click, then stays still
        public void ToggleTurning() { turning = !turning; }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.StartAwake; // MoveRotation wakes it for every click
            SimplifyCollider();
            angle = body.rotation;
            fromAngle = toAngle = angle;
            turning = startAutomatically;
            timer = pauseBetweenSteps - startDelay; // the first click comes after startDelay
            if (clickSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            timer += dt;
            switch (phase)
            {
                case Phase.Waiting:
                    if (turning && timer >= pauseBetweenSteps) BeginStep();
                    break;
                case Phase.Turning:
                {
                    float t = Mathf.Clamp01(timer / stepDuration);
                    float over = toAngle + (toAngle - fromAngle) * overshoot;
                    angle = Mathf.LerpUnclamped(fromAngle, over, EaseIn(t));
                    if (t >= 1f) { phase = Phase.Settling; timer = 0f; }
                    break;
                }
                case Phase.Settling:
                {
                    float t = Mathf.Clamp01(timer / settleTime);
                    float over = toAngle + (toAngle - fromAngle) * overshoot;
                    angle = Mathf.LerpUnclamped(over, toAngle, Smooth(t));
                    if (t >= 1f) { angle = toAngle; phase = Phase.Waiting; timer = 0f; }
                    break;
                }
            }
            // Only move while the angle changes: a still gear costs the physics nothing between clicks.
            if (angle != sentAngle) { body.MoveRotation(angle); sentAngle = angle; }
            CheckCrush(dt);
        }

        void SimplifyCollider()
        {
            PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
            if (poly == null || colliderSimplify <= 0f) return;
            float scale = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
            float tol = colliderSimplify / scale; // the points are in local space
            List<Vector2> src = new List<Vector2>();
            List<Vector2> dst = new List<Vector2>();
            for (int i = 0; i < poly.pathCount; i++)
            {
                src.Clear();
                poly.GetPath(i, src);
                if (src.Count < 8) continue;
                dst.Clear();
                LineUtility.Simplify(src, tol, dst);
                if (dst.Count >= 3 && dst.Count < src.Count) poly.SetPath(i, dst);
            }
        }

        // The collider in a contact that does not belong to the worm (works whichever way round Unity reports it).
        static Collider2D OtherThan(Rigidbody2D worm, ContactPoint2D c)
        {
            return c.collider != null && c.collider.attachedRigidbody == worm ? c.otherCollider : c.collider;
        }

        void CheckCrush(float dt)
        {
            WormController w = WormController.Instance;
            if (!crushesPlayer || phase == Phase.Waiting || w == null || w.IsDead || w.Physics == null) { crushTimer = 0f; return; }
            Rigidbody2D worm = w.Physics.Body;
            Vector2 centre = worm.position;
            int n = worm.GetContacts(contacts);
            // Push directions (contact point -> worm centre) from the gear and from everything else.
            bool squeezed = false;
            for (int i = 0; i < n && !squeezed; i++)
            {
                Collider2D gearSide = OtherThan(worm, contacts[i]);
                if (gearSide == null || gearSide.attachedRigidbody != body) continue;
                Vector2 fromGear = (centre - contacts[i].point).normalized;
                for (int j = 0; j < n; j++)
                {
                    Collider2D other = OtherThan(worm, contacts[j]);
                    if (other == null || other.isTrigger || other.attachedRigidbody == body) continue;
                    Vector2 fromOther = (centre - contacts[j].point).normalized;
                    if (Vector2.Dot(fromGear, fromOther) <= -crushOpposition) { squeezed = true; break; }
                }
            }
            crushTimer = squeezed ? crushTimer + dt : 0f;
            if (crushTimer >= crushTime && crushTimer > 0f)
            {
                crushTimer = 0f;
                w.Die("crushed: " + name);
                onCrushedPlayer.Invoke();
            }
        }

        void BeginStep()
        {
            fromAngle = toAngle;
            toAngle = fromAngle + (direction == Direction.Clockwise ? -stepAngle : stepAngle);
            phase = Phase.Turning;
            timer = 0f;
            StepCount++;
            // The gear may have slept between clicks: wake whatever rests on it so the teeth push it for real.
            body.WakeUp();
            int n = body.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                Collider2D c = contacts[i].collider != null && contacts[i].collider.attachedRigidbody == body ? contacts[i].otherCollider : contacts[i].collider;
                if (c != null && c.attachedRigidbody != null && c.attachedRigidbody != body) c.attachedRigidbody.WakeUp();
            }
            if (audioSource != null) audioSource.PlayOneShot(clickSound, clickVolume);
            else GameAudio.PlayAt("gear_click", body.position, clickVolume * 0.6f);
            onStep.Invoke();
        }

        // Starts slowly and snaps into place, like a ratchet pawl dropping into the next tooth.
        static float EaseIn(float t) { return t * t * (2f - t) * 0.5f + t * t * 0.5f; }
        static float Smooth(float t) { return t * t * (3f - 2f * t); }
    }
}
