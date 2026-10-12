using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    public enum PistonState { Idle, WindUp, Strike, Return }

    /// <summary>
    /// A piston that cycles Idle -> WindUp (hiss/shake, the tell) -> Strike (fast) -> Return (slow).
    /// A worm standing on the head when the strike starts gets an impulse; delta-v = impulse / mass,
    /// so light Squishy is flung far and Heavy gets a short shove. Landing within 'activeWindow' seconds
    /// after the strike starts still gets a weaker kick.
    /// The head is a child object with a kinematic Rigidbody2D + collider (assign it below).
    /// The piston pushes along its own local up axis.
    /// </summary>
    public class Piston : MonoBehaviour
    {
        [Header("Cycle")]
        [Tooltip("Total seconds for one full cycle (Idle + WindUp + Strike + Return).")]
        public float cycleDuration = 3f;
        public float windupTime = 0.6f;
        [Tooltip("Seconds the head takes to reach full extension.")]
        public float strikeTime = 0.12f;
        [Tooltip("Seconds the head stays out after striking.")]
        public float holdTime = 0.2f;
        [Tooltip("Seconds of the slow return.")]
        public float returnTime = 0.7f;
        [Tooltip("Start the cycle at this many seconds in (to offset several pistons).")]
        public float startOffset = 0f;

        [Header("Strike")]
        [Tooltip("Impulse (mass * speed) added on top of the head's own push (strokeLength / strikeTime). Squishy (0.3) gets 3/0.3 = +10 u/s, Heavy (2.5) only +1.2 u/s.")]
        public float strikeForce = 3f;
        [Tooltip("After the strike starts, a worm that lands within this many seconds still gets kicked.")]
        public float activeWindow = 0.25f;
        [Range(0f, 1f), Tooltip("Strength of a late kick compared to a perfect one.")]
        public float lateKickMultiplier = 0.5f;
        [Tooltip("Extra rotation of the kick direction (degrees, + = counter-clockwise).")]
        public float kickAngleOffset = 0f;
        public float strokeLength = 1.2f;

        [Header("References")]
        public Rigidbody2D head;
        public Color restColor = new Color(0.36f, 0.42f, 0.47f);
        public Color hotColor = new Color(0.95f, 0.55f, 0.15f);

        public PistonState State { get; private set; }
        public event System.Action Struck;

        float phaseTime;
        float cycleTime;
        Vector3 headRestLocal;
        SpriteRenderer headSprite;
        BoxCollider2D headBox;
        readonly HashSet<WormPhysics> kicked = new HashSet<WormPhysics>();
        static readonly Collider2D[] buffer = new Collider2D[16];

        float IdleTime { get { return Mathf.Max(0f, cycleDuration - windupTime - (strikeTime + holdTime) - returnTime); } }

        void Awake()
        {
            if (head == null) { Debug.LogWarning("Piston '" + name + "' has no head assigned.", this); enabled = false; return; }
            headRestLocal = head.transform.localPosition;
            headSprite = head.GetComponent<SpriteRenderer>();
            headBox = head.GetComponent<BoxCollider2D>();
            cycleTime = startOffset;
            SetState(PistonState.Idle);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            cycleTime = Mathf.Repeat(cycleTime + dt, Mathf.Max(0.5f, cycleDuration));

            // Which phase are we in?
            float t = cycleTime;
            PistonState next;
            float local;
            if (t < IdleTime) { next = PistonState.Idle; local = t; }
            else if (t < IdleTime + windupTime) { next = PistonState.WindUp; local = t - IdleTime; }
            else if (t < IdleTime + windupTime + strikeTime + holdTime) { next = PistonState.Strike; local = t - IdleTime - windupTime; }
            else { next = PistonState.Return; local = t - IdleTime - windupTime - strikeTime - holdTime; }

            if (next != State) SetState(next);
            phaseTime = local;

            float extension = 0f;
            float jitter = 0f;
            switch (State)
            {
                case PistonState.Idle: extension = 0f; break;
                case PistonState.WindUp:
                    extension = -0.06f * Mathf.Sin(Mathf.Clamp01(phaseTime / windupTime) * Mathf.PI); // tiny pull-back
                    jitter = Mathf.Sin(Time.time * 70f) * 0.02f * Mathf.Clamp01(phaseTime / windupTime);
                    break;
                case PistonState.Strike:
                    extension = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, strikeTime));
                    extension = 1f - (1f - extension) * (1f - extension); // fast start
                    break;
                case PistonState.Return:
                    float k = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, returnTime));
                    extension = 1f - Mathf.SmoothStep(0f, 1f, k);
                    break;
            }

            Vector3 local3 = headRestLocal + Vector3.up * (strokeLength * extension) + Vector3.right * jitter;
            head.MovePosition(transform.TransformPoint(local3));

            if (headSprite != null)
            {
                float hot = State == PistonState.WindUp ? Mathf.Clamp01(phaseTime / windupTime) : (State == PistonState.Strike ? 1f : 0f);
                headSprite.color = Color.Lerp(restColor, hotColor, hot);
            }

            if (State == PistonState.Strike) KickWormsOnHead(phaseTime);
        }

        void SetState(PistonState s)
        {
            State = s;
            if (s == PistonState.Idle) kicked.Clear();
            if (s == PistonState.Strike && Struck != null) Struck();
        }

        void KickWormsOnHead(float timeSinceStrike)
        {
            if (timeSinceStrike > activeWindow + Time.fixedDeltaTime) return;
            bool perfect = timeSinceStrike <= Time.fixedDeltaTime * 1.5f;

            Vector2 headSize = headBox != null ? Vector2.Scale(headBox.size, head.transform.lossyScale) : new Vector2(1f, 0.4f);
            Vector2 up = transform.up;
            Vector2 center = (Vector2)head.position + up * (headSize.y * 0.5f + 0.2f);
            Vector2 size = new Vector2(headSize.x * 0.95f, 0.45f);

            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            int n = Physics2D.OverlapBox(center, size, transform.eulerAngles.z, filter, buffer);
            for (int i = 0; i < n; i++)
            {
                WormPhysics w = buffer[i].GetComponent<WormPhysics>();
                if (w == null || kicked.Contains(w)) continue;
                kicked.Add(w);

                float strength = perfect ? 1f : lateKickMultiplier;
                Vector2 dir = Quaternion.Euler(0f, 0f, kickAngleOffset) * up;
                w.AddExternalImpulse(dir * (strikeForce * strength));
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 p = transform.position;
            Gizmos.DrawLine(p, p + transform.up * strokeLength);
        }
    }
}
