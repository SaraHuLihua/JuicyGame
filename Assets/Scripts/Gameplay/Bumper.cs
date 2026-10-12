using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Pinball bumper (the "Bumper Room" above the saw chamber, FG_Props_1_14).
    /// Touching it fires the worm straight away from its centre at Bump Speed, whatever the material, with a flash,
    /// a pop of the ring, shake, chalk sparks and a "bong". Hitting bumpers one after another inside Combo Time builds
    /// a combo: every hit is a little faster, higher pitched and shakes more, and from Combo For Slow Mo hits the
    /// game slows down for a moment. The look is the white chalk circle (Untitled_Artwork 14_3) on this object's
    /// SpriteRenderer; the trigger is the CircleCollider2D (added at Play if missing). Never solid.
    /// </summary>
    [DisallowMultipleComponent]
    public class Bumper : MonoBehaviour
    {
        [Header("Bump")]
        [Tooltip("Speed the worm leaves with (units/s). Same for Squishy and Rocky.")]
        public float bumpSpeed = 24f;
        [Tooltip("Extra speed per combo hit (units/s).")]
        public float comboSpeedBonus = 2f;
        [Tooltip("Most extra speed the combo can add (units/s).")]
        public float comboSpeedMax = 10f;
        [Tooltip("How much of the bump points up, so the worm keeps floating around the room (0 = straight away from the centre).")]
        [Range(0f, 1f)] public float upwardBias = 0.25f;
        [Tooltip("Seconds before the same bumper can fire again.")]
        [Min(0f)] public float rearmTime = 0.15f;
        [Tooltip("Trigger radius in local units when the object has no CircleCollider2D. 0 = from the sprite.")]
        [Min(0f)] public float radius = 0f;

        [Header("Combo")]
        [Tooltip("Seconds between two bumps for them to count as a combo.")]
        public float comboTime = 1.6f;
        [Tooltip("From this many hits in a row, each bump gets a short slow motion.")]
        [Min(2)] public int comboForSlowMo = 3;
        [Range(0.05f, 1f)] public float slowMoScale = 0.35f;
        [Tooltip("Real seconds of the slow motion.")]
        public float slowMoTime = 0.18f;

        [Header("Look")]
        [Tooltip("How much the ring grows when hit (0.4 = 40%).")]
        [Range(0f, 1f)] public float pop = 0.4f;
        public float popTime = 0.3f;
        public Color idleColor = new Color(1f, 1f, 1f, 0.85f);
        public Color flashColor = new Color(1f, 0.85f, 0.35f, 1f);
        [Tooltip("Slow spin of the ring while idle (degrees/s), so the room never looks dead.")]
        public float idleSpin = 20f;
        public UnityEvent onBump;

        public static int Combo { get; private set; }
        static float lastBumpTime = -99f;
        static bool slowing;

        SpriteRenderer sr;
        Vector3 baseScale;
        float popTimer = -1f;
        float rearmUntil;

        void Awake()
        {
            slowing = false; // a slow motion cut short by a level restart
            sr = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            CircleCollider2D c = GetComponent<CircleCollider2D>();
            if (c == null)
            {
                c = gameObject.AddComponent<CircleCollider2D>();
                float r = radius;
                if (r <= 0f && sr != null && sr.sprite != null) r = sr.sprite.bounds.extents.x;
                if (r > 0f) c.radius = r;
            }
            c.isTrigger = true;
            if (sr != null) sr.color = idleColor;
        }

        void OnTriggerEnter2D(Collider2D other) { TryBump(other); }
        void OnTriggerStay2D(Collider2D other) { TryBump(other); }

        void TryBump(Collider2D other)
        {
            if (Time.time < rearmUntil) return;
            WormController w = WormController.Instance;
            if (w == null || w.IsDead || w.Physics == null || other.attachedRigidbody != w.Physics.Body) return;
            rearmUntil = Time.time + rearmTime;

            Combo = Time.time - lastBumpTime <= comboTime ? Combo + 1 : 1;
            lastBumpTime = Time.time;

            WormPhysics ph = w.Physics;
            Vector2 away = ph.Body.position - (Vector2)transform.position;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            Vector2 dir = (away.normalized + Vector2.up * upwardBias).normalized;
            float speed = bumpSpeed + Mathf.Min(comboSpeedMax, comboSpeedBonus * (Combo - 1));
            Vector2 v = dir * speed;
            ph.AllowOverspeed(speed + 1f, 0.6f);
            ph.AddExternalImpulse((v - ph.Velocity) * ph.Mass); // exact velocity whatever the mass

            // Juice: everything grows with the combo.
            float c01 = Mathf.Clamp01((Combo - 1) / 6f);
            popTimer = 0f;
            GameJuice.Shake(Mathf.Lerp(0.08f, 0.22f, c01));
            Vector2 hitPoint = (Vector2)transform.position + away.normalized * (sr != null && sr.sprite != null ? sr.bounds.extents.x : 0.8f);
            for (int i = 0; i < 8 + Combo * 2; i++)
            {
                Vector2 sv = (dir + Random.insideUnitCircle * 0.9f).normalized * Random.Range(5f, 12f);
                SimpleDebris.Spawn(hitPoint, sv, flashColor, Random.Range(0.08f, 0.18f), Random.Range(0.25f, 0.5f));
            }
            GameAudio.Play("bumper", Mathf.Lerp(0.6f, 0.95f, c01), 1f + 0.06f * Mathf.Min(Combo - 1, 8)); // each combo hit a note higher
            if (Combo >= comboForSlowMo) SlowMo();
            onBump.Invoke();
        }

        void SlowMo()
        {
            if (slowing || Time.timeScale < 0.99f) return;
            GameJuice j = GameJuice.Instance;
            if (j == null) return;
            j.StartCoroutine(DoSlowMo(slowMoScale, slowMoTime));
        }

        static System.Collections.IEnumerator DoSlowMo(float scale, float seconds)
        {
            slowing = true;
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            slowing = false;
        }

        void Update()
        {
            if (idleSpin != 0f) transform.Rotate(0f, 0f, idleSpin * Time.deltaTime);
            if (popTimer < 0f) return;
            popTimer += Time.deltaTime;
            float t = Mathf.Clamp01(popTimer / Mathf.Max(0.01f, popTime));
            float k = 1f + pop * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.6f);
            transform.localScale = baseScale * k;
            if (sr != null) sr.color = Color.Lerp(flashColor, idleColor, t);
            if (t >= 1f) { popTimer = -1f; transform.localScale = baseScale; }
        }

        void OnDisable()
        {
            transform.localScale = baseScale;
            if (slowing) { Time.timeScale = 1f; slowing = false; }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.35f, 0.8f);
            SpriteRenderer s = GetComponent<SpriteRenderer>();
            float r = s != null && s.sprite != null ? s.bounds.extents.x : Mathf.Max(0.5f, radius * Mathf.Abs(transform.lossyScale.x));
            Gizmos.DrawWireSphere(transform.position, r);
        }
#endif
    }
}
