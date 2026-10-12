using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Fan that blows along its local UP (prefab: Prefabs/Gameplay/Fan, built from the "Fan" object in MechanicsTest).
    /// The air is a real FORCE, the same for every material, so the worm's mass decides what happens
    /// (no "if Squishy" anywhere):
    ///   - Squishy (mass 0.3) gets 0.3 / 2.5 = 8x more acceleration than Heavy and floats up. The force fades
    ///     towards the top of the column, so Squishy stops where the air exactly holds its weight and bobs there.
    ///   - Heavy (mass 2.5, gravity x1.6) is barely slowed and stays down.
    /// The Scene view shows the column (cyan) and the height where Squishy floats (green) when selected.
    /// Visual air streaks and the housing shake are purely visual and never touch physics.
    /// Hierarchy (prefab):
    ///   Fan (this + BoxCollider2D housing + Surface)
    ///   └── Housing (sprites: FG_Props_1_10 grille x3, FG_Props_1_41 body; shaken while running)
    /// </summary>
    [DisallowMultipleComponent]
    public class FanAirflow : MonoBehaviour
    {
        [Header("Air column (this object's local units; the air blows along local UP)")]
        [Tooltip("Middle of the grille, where the air comes out.")]
        public Vector2 outletCenter = new Vector2(1.78f, 0.35f);
        [Tooltip("Width of the air column.")]
        public float columnWidth = 5f;
        [Tooltip("How high the air reaches above the grille.")]
        public float columnHeight = 9f;

        [Header("Lift")]
        [Tooltip("Off = the fan is stopped. Can be switched from UnityEvents with StartFan() / StopFan() / ToggleFan().")]
        public bool running = true;
        [Tooltip("Upward force at the grille, the same for every material. Acceleration = force / worm mass. Squishy weighs 0.3 x 41.25 = 12.4, Heavy 2.5 x 60 = 150: anything between ~13 and ~150 lifts Squishy and not Heavy. Higher = Squishy floats higher.")]
        public float liftForce = 31.5f;
        [Tooltip("Share of the force left at the top of the column (0..1). 0 = fades to nothing, so Squishy floats inside the column. 1 = full force all the way up (Squishy shoots out of the top).")]
        [Range(0f, 1f)] public float topStrength = 0f;
        [Tooltip("Air resistance inside the column (force per unit of speed, divided by mass). Calms Squishy's bobbing a lot, slows Heavy only a little. 0 = none.")]
        public float airDrag = 1.2f;
        [Tooltip("Sideways pull towards the middle of the column, per unit off-centre. Keeps a floating Squishy from drifting out. 0 = none.")]
        public float centering = 0.6f;
        [Tooltip("Gentle pulsing of the force so the floating worm bobs (0 = steady).")]
        [Range(0f, 0.5f)] public float flutter = 0.12f;
        [Tooltip("Pulses per second of the flutter.")]
        public float flutterSpeed = 1.5f;
        [Tooltip("Seconds from off to full strength (and back). 0 = instant.")]
        public float spinUpTime = 0.5f;

        [Header("Launching while floating")]
        [Tooltip("On = a worm floating in the air can aim and launch as if it stood on the ground.")]
        public bool allowLaunchWhileFloating = true;
        [Tooltip("It counts as floating once its speed stays under this (units/s)...")]
        public float floatMaxSpeed = 4f;
        [Tooltip("...for this many seconds. Stops a worm that was just launched up the column from launching again at once.")]
        public float floatSettleTime = 0.2f;

        [Header("Air VFX (visual only)")]
        public bool showAir = true;
        [Tooltip("Number of air streaks rising from the grille.")]
        [Range(0, 80)] public int streakCount = 22;
        [Tooltip("Rising speed of the streaks (units/s) at full strength.")]
        public float streakSpeed = 7f;
        [Tooltip("Min / max length of a streak.")]
        public Vector2 streakLength = new Vector2(0.6f, 1.5f);
        [Tooltip("Min / max thickness of a streak.")]
        public Vector2 streakWidth = new Vector2(0.05f, 0.12f);
        [Tooltip("Colour and transparency of the air.")]
        public Color airColor = new Color(0.85f, 0.9f, 1f, 0.25f);
        [Tooltip("How much the streaks wave sideways while rising.")]
        public float wobble = 0.2f;
        [Tooltip("Optional sprite for a streak (drawn vertically). Empty = a soft generated line.")]
        public Sprite streakSprite;
        public int sortingOrder = 3;

        [Header("Housing shake (visual only)")]
        [Tooltip("Shaken while the fan runs. Leave empty for no shake. Never put the collider on it.")]
        public Transform shakeTarget;
        public float shakeAmount = 0.02f;

        [Header("Events")]
        public UnityEvent onStarted;
        public UnityEvent onStopped;

        float strength;          // 0..1, eased by spinUpTime
        bool wasRunning;
        Vector3 shakeBase;
        Transform airRoot;
        float floatTimer;

        class Streak { public Transform t; public SpriteRenderer sr; public float x, y, speed, length, width, phase; }
        readonly List<Streak> streaks = new List<Streak>();
        static Sprite softLine;

        /// <summary>0..1: how hard the fan is blowing right now.</summary>
        public float Strength { get { return strength; } }

        public void StartFan() { running = true; }
        public void StopFan() { running = false; }
        public void ToggleFan() { running = !running; }

        void Start()
        {
            strength = running ? 1f : 0f;
            wasRunning = running;
            if (shakeTarget != null) shakeBase = shakeTarget.localPosition;
            if (showAir) BuildStreaks();
        }

        // ------------------------------------------------------------------ physics

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            float goal = running ? 1f : 0f;
            strength = spinUpTime > 0.001f ? Mathf.MoveTowards(strength, goal, dt / spinUpTime) : goal;
            if (running != wasRunning)
            {
                wasRunning = running;
                if (running) onStarted.Invoke(); else onStopped.Invoke();
            }
            if (strength <= 0.0001f) { floatTimer = 0f; return; }

            WormController w = WormController.Instance;
            if (w == null || w.IsDead || w.Physics == null) return;
            Rigidbody2D rb = w.Physics.Body;

            float h01, side;
            if (!InColumn(rb.position, w.Physics.Collider.radius * Mathf.Abs(w.transform.lossyScale.x), out h01, out side)) { floatTimer = 0f; return; }

            // Floating = slow inside the air for a moment: then the worm may aim and launch from mid-air.
            if (allowLaunchWhileFloating && strength > 0.5f && rb.GetVelocity().magnitude <= floatMaxSpeed) floatTimer += dt;
            else floatTimer = 0f;
            if (floatTimer >= floatSettleTime) w.Physics.AllowLaunchInAir();

            Vector2 up = transform.up;
            Vector2 right = transform.right;
            float pulse = 1f + flutter * Mathf.Sin(Time.time * flutterSpeed * Mathf.PI * 2f);
            float lift = liftForce * Mathf.Lerp(1f, topStrength, h01) * pulse * strength;

            Vector2 force = up * lift - right * (side * centering * strength);
            // Air resistance: a force against the motion, so it slows light worms much more than heavy ones.
            force -= rb.GetVelocity() * (airDrag * strength);
            rb.AddForce(force);
        }

        /// <summary>Is a world point inside the air column? h01 = 0 at the grille, 1 at the top; side = world distance off the middle line.</summary>
        public bool InColumn(Vector2 worldPos, float margin, out float h01, out float side)
        {
            Vector2 lp = (Vector2)transform.InverseTransformPoint(worldPos) - outletCenter;
            Vector3 s = transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(s.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(s.y));
            float halfW = columnWidth * 0.5f + margin / sx;
            h01 = Mathf.Clamp01(lp.y / Mathf.Max(0.01f, columnHeight));
            side = lp.x * sx;
            return Mathf.Abs(lp.x) <= halfW && lp.y >= -margin / sy && lp.y <= columnHeight;
        }

        // ------------------------------------------------------------------ visuals

        void BuildStreaks()
        {
            GameObject root = new GameObject("AirFX");
            root.layer = gameObject.layer;
            airRoot = root.transform;
            airRoot.SetParent(transform, false);
            Sprite sprite = streakSprite != null ? streakSprite : SoftLine();
            for (int i = 0; i < streakCount; i++)
            {
                GameObject go = new GameObject("Streak" + i);
                go.layer = gameObject.layer;
                go.transform.SetParent(airRoot, false);
                Streak s = new Streak();
                s.t = go.transform;
                s.sr = go.AddComponent<SpriteRenderer>();
                s.sr.sprite = sprite;
                s.sr.sortingOrder = sortingOrder;
                Respawn(s, Random.value); // spread over the column from the start
                streaks.Add(s);
            }
        }

        void Respawn(Streak s, float startY)
        {
            s.x = Random.Range(-0.5f, 0.5f);
            s.y = startY;
            s.speed = Random.Range(0.75f, 1.25f);
            s.length = Random.Range(streakLength.x, streakLength.y);
            s.width = Random.Range(streakWidth.x, streakWidth.y);
            s.phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (shakeTarget != null)
                shakeTarget.localPosition = shakeBase + (Vector3)(Random.insideUnitCircle * shakeAmount * strength);

            if (airRoot == null) return;
            float dt = Time.deltaTime;
            float hgt = Mathf.Max(0.01f, columnHeight);
            for (int i = 0; i < streaks.Count; i++)
            {
                Streak s = streaks[i];
                s.y += streakSpeed * s.speed * strength * dt / hgt;
                if (s.y > 1f) Respawn(s, 0f);
                float t = Time.time * 2.2f + s.phase;
                float px = outletCenter.x + s.x * columnWidth + Mathf.Sin(t) * wobble * s.y;
                float py = outletCenter.y + s.y * hgt;
                s.t.localPosition = new Vector3(px, py, 0f);
                // Longer while blowing hard, thinner and fading out near the top.
                s.t.localScale = new Vector3(s.width * (1f - 0.5f * s.y), s.length * (0.4f + 0.6f * strength), 1f);
                Color c = airColor;
                c.a *= Mathf.Sin(Mathf.PI * s.y) * strength; // fade in at the grille, out at the top
                s.sr.color = c;
            }
        }

        static Sprite SoftLine()
        {
            if (softLine != null) return softLine;
            const int w = 8, h = 32;
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                    float v = (y + 0.5f) / h;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, (1f - u * u) * Mathf.Sin(Mathf.PI * v)));
                }
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            // Pixels per unit = width: the sprite is 1 x 4 units, scaled to streak width x length.
            softLine = Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            softLine.hideFlags = HideFlags.HideAndDontSave;
            return softLine;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.7f);
            Vector3 c = new Vector3(outletCenter.x, outletCenter.y + columnHeight * 0.5f, 0f);
            Gizmos.DrawWireCube(c, new Vector3(columnWidth, columnHeight, 0f));

            // Height where Squishy floats: lift x falloff = its weight.
            WormMaterialProfile p = WormMaterialProfile.GetDefault(WormMaterial.Squishy);
            WormController w = WormController.Instance;
            if (w != null && w.Physics != null && w.Physics.squishyProfile != null) p = w.Physics.squishyProfile;
            float weight = p.mass * Physics2D.gravity.magnitude * p.gravityScale;
            if (liftForce > weight && topStrength < 1f)
            {
                float f = weight / liftForce; // share of the force needed
                float h01 = Mathf.Clamp01((1f - f) / Mathf.Max(0.0001f, 1f - topStrength));
                float y = outletCenter.y + h01 * columnHeight;
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                Gizmos.DrawLine(new Vector3(outletCenter.x - columnWidth * 0.5f, y, 0f), new Vector3(outletCenter.x + columnWidth * 0.5f, y, 0f));
            }
        }
#endif
    }
}
