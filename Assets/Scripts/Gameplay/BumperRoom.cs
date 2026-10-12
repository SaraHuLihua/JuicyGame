using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Reward for reaching the Bumper Room: as soon as the worm enters the box, it goes into SUPER BOUNCE for a few
    /// seconds. Every wall bounces it back at full speed and it gets a first shove toward the nearest bumper,
    /// so it pinballs around the bumpers by itself and the player does not have to spend a launch to enjoy it.
    /// It works again each time the worm comes back in after leaving. Put it on the Bumper Room object;
    /// the box (orange in the Scene view) is Size, moved by Offset.
    /// </summary>
    [DisallowMultipleComponent]
    public class BumperRoom : MonoBehaviour
    {
        [Tooltip("Size of the room box (world units).")]
        public Vector2 size = new Vector2(22f, 18f);
        [Tooltip("Centre of the box from this object (world units).")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("Seconds of super bounce after entering.")]
        [Min(0f)] public float duration = 4f;
        [Tooltip("Every bounce sends the worm back at least this fast (units/s).")]
        public float bounceSpeed = 18f;
        [Tooltip("Speed of the first shove toward the nearest bumper (units/s). 0 = none.")]
        public float entryKick = 20f;
        [Tooltip("Seconds outside the box before entering again starts a new super bounce.")]
        public float rearmAfter = 1f;
        public UnityEvent onSuperBounce;

        bool inside;
        bool used;
        float leftAt = -99f;
        float until;

        public bool Contains(Vector2 p)
        {
            Vector2 c = (Vector2)transform.position + offset;
            return Mathf.Abs(p.x - c.x) <= size.x * 0.5f && Mathf.Abs(p.y - c.y) <= size.y * 0.5f;
        }

        void Update()
        {
            WormController w = WormController.Instance;
            if (w == null || w.Physics == null || w.IsDead) { inside = false; return; }
            bool now = Contains(w.Physics.Body.position);
            if (now && !inside && !used) Begin(w);
            if (!now && inside) leftAt = Time.time;
            if (!now && used && Time.time >= until && Time.time - leftAt >= rearmAfter) used = false; // out long enough: ready again
            inside = now;

            // Glow the bumpers while it lasts.
            if (Time.time < until)
            {
                float k = 1f + 0.06f * Mathf.Sin(Time.time * 14f);
                for (int i = 0; i < bumpers.Length; i++)
                    if (bumpers[i] != null) bumpers[i].transform.localRotation *= Quaternion.Euler(0f, 0f, 120f * Time.deltaTime * k);
            }
        }

        Bumper[] bumpers = new Bumper[0];

        void Start()
        {
            bumpers = GetComponentsInChildren<Bumper>();
        }

        void Begin(WormController w)
        {
            used = true;
            until = Time.time + duration;
            WormPhysics ph = w.Physics;
            ph.SuperBounce(duration, bounceSpeed);

            if (entryKick > 0f)
            {
                Bumper best = null;
                float bestD = float.MaxValue;
                for (int i = 0; i < bumpers.Length; i++)
                {
                    if (bumpers[i] == null) continue;
                    float d = ((Vector2)bumpers[i].transform.position - ph.Body.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = bumpers[i]; }
                }
                if (best != null)
                {
                    Vector2 dir = ((Vector2)best.transform.position - ph.Body.position).normalized;
                    ph.AllowOverspeed(entryKick + 1f, 0.5f);
                    ph.AddExternalImpulse((dir * entryKick - ph.Velocity) * ph.Mass);
                }
            }
            GameJuice.Shake(0.15f);
            onSuperBounce.Invoke();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Vector3 c = (Vector2)transform.position + offset;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.12f);
            Gizmos.DrawCube(c, size);
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(c, size);
        }
#endif
    }
}
