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

        [Header("Sound / events (optional)")]
        [Tooltip("Played on every click. Empty = silent.")]
        public AudioClip clickSound;
        [Range(0f, 1f)] public float clickVolume = 0.5f;
        [Tooltip("Raised at the start of every click (sound, particles, other mechanisms).")]
        public UnityEvent onStep;

        enum Phase { Waiting, Turning, Settling }

        Rigidbody2D body;
        AudioSource audioSource;
        Phase phase = Phase.Waiting;
        bool turning;
        float timer;
        float fromAngle;   // rotation at the start of the current click
        float toAngle;     // rotation at the end of the current click (unwrapped degrees)
        float angle;

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
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
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
            body.MoveRotation(angle);
        }

        void BeginStep()
        {
            fromAngle = toAngle;
            toAngle = fromAngle + (direction == Direction.Clockwise ? -stepAngle : stepAngle);
            phase = Phase.Turning;
            timer = 0f;
            StepCount++;
            if (audioSource != null) audioSource.PlayOneShot(clickSound, clickVolume);
            onStep.Invoke();
        }

        // Starts slowly and snaps into place, like a ratchet pawl dropping into the next tooth.
        static float EaseIn(float t) { return t * t * (2f - t) * 0.5f + t * t * 0.5f; }
        static float Smooth(float t) { return t * t * (3f - 2f * t); }
    }
}
