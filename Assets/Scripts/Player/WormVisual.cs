using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Worm look. LISTENS to WormController only; no physics depends on it.
    /// Gameplay state is WormMaterial (Squishy/Heavy) — never inferred from colour.
    /// Wire sprites via Inspector or Project Juice > Wire Worm Sprites:
    ///   bouncyIdle / rockyIdle, bouncyToRocky (7 frames, reverse = Rocky->Bouncy),
    ///   bouncyDeath / rockyDeath.
    /// Juice (jump stretch + landing squash) works for BOTH materials with your art:
    /// Squishy = big wobbly, Heavy = small snappy. Tune under "Juice".
    /// </summary>
    public class WormVisual : MonoBehaviour
    {
        [Header("Bouncy / Squishy Idle (loop)")]
        public Sprite[] bouncyIdle;
        [Header("Rocky / Heavy Idle (loop)")]
        public Sprite[] rockyIdle;
        [Header("Transition Bouncy -> Rocky (forward)")]
        [Tooltip("7 frames: Worm_Bouncy_1,2,3 + Worm_Flat + Worm_Rocky_1,2,3. Reverse is used for Rocky->Bouncy.")]
        public Sprite[] bouncyToRocky;
        [Header("Transition Rocky -> Bouncy (optional)")]
        [Tooltip("If empty, bouncyToRocky is played in reverse. Fill this only if you draw a dedicated strip.")]
        public Sprite[] rockyToBouncy;
        [Header("Death")]
        public Sprite[] bouncyDeath;
        public Sprite[] rockyDeath;

        [Header("Playback")]
        public float idleFps = 6f;
        public float transitionFps = 10f;
        public float deathFps = 8f;
        [Tooltip("While the transition strip plays, ignore squash/stretch/pop/tilt/breathing so your hand-drawn frames show clean.")]
        public bool freezeJuiceDuringTransition = true;

        [Header("Size")]
        [Tooltip("Your PNGs are 800px with lots of padding. 1 = art diameter matches collider diameter. Raise if the worm looks smaller than its collisions, lower if bigger. Use the Scene gizmo to compare.")]
        public float visualSizeMultiplier = 2.6f;
        [Tooltip("If your Bouncy/Rocky art looks swapped vs the material, tick this ONCE instead of swapping arrays by hand — it swaps Idle AND Death AND transition direction together so they stay consistent.")]
        public bool swapBouncyRockyArt = false;

        [Header("Juice — Squishy (big wobbly)")]
        public float squishyJumpStretch = 0.42f;
        public float squishyLandSquash = 0.5f;
        public float squishyRecover = 5f;
        [Header("Juice — Heavy (small snappy)")]
        public float heavyJumpStretch = 0.22f;
        public float heavyLandSquash = 0.28f;
        public float heavyRecover = 9f;

        [Header("Juice — shared")]
        [Tooltip("Extra stretch while flying fast, along the velocity. 0 = off.")]
        public float airStretchMax = 0.22f;
        [Tooltip("Gentle idle breathing scale. 0 = off.")]
        public float breathingAmount = 0.03f;
        public float breathingSpeed = 3f;
        [Tooltip("Max lean of the sprite toward horizontal speed (degrees). Placeholder ball rolls instead.")]
        public float tiltMaxDegrees = 10f;
        public float popScale = 1.35f;
        public float popSeconds = 0.12f;

        WormController controller;
        SpriteRenderer body;
        GameObject placeholderCoreGo;
        GameObject placeholderEyeGo;
        Transform rollPivot;

        // frame animation
        enum VisState { Idle, Transition, Dead }
        VisState state;
        WormMaterial deadMaterial; // material at the moment of death: the death strip never changes mid-animation
        int frame;
        float frameTimer;
        Sprite[] transitionStrip;

        // juice: squash along a normal, stretch along a direction, pop uniform
        float squash;
        Vector2 squashNormal = Vector2.up;
        float stretch;
        Vector2 stretchDir = Vector2.up;
        float pop;
        float roll; // placeholder only
        float breathePhase;

        bool HasSprites { get { return (bouncyIdle != null && bouncyIdle.Length > 0) || (rockyIdle != null && rockyIdle.Length > 0); } }
        bool IsHeavy { get { return controller != null && controller.Material == WormMaterial.Heavy; } }

        // All art mapping goes through here so idle/death/transition can never disagree.
        bool ShowAsHeavy(WormMaterial m)
        {
            bool heavy = (m == WormMaterial.Heavy);
            return swapBouncyRockyArt ? !heavy : heavy;
        }
        Sprite[] IdleFor(WormMaterial m) { return ShowAsHeavy(m) ? rockyIdle : bouncyIdle; }
        Sprite[] DeathFor(WormMaterial m) { return ShowAsHeavy(m) ? rockyDeath : bouncyDeath; }

        void Awake()
        {
            controller = GetComponentInParent<WormController>();

            GameObject pivot = new GameObject("Roll");
            pivot.transform.SetParent(transform, false);
            rollPivot = pivot.transform;
            body = PlaceholderSprites.Make("Body", rollPivot, PlaceholderSprites.Circle, Color.white, Vector2.zero, Vector2.one, 10);
            placeholderCoreGo = PlaceholderSprites.Make("Core", rollPivot, PlaceholderSprites.Circle, new Color(0.96f, 0.63f, 0.77f), new Vector2(0.06f, 0.08f), new Vector2(0.55f, 0.5f), 11).gameObject;
            placeholderEyeGo = PlaceholderSprites.Make("Eye", rollPivot, PlaceholderSprites.Circle, Color.black, new Vector2(-0.22f, -0.12f), new Vector2(0.14f, 0.14f), 12).gameObject;
        }

        void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<WormController>();
            if (controller != null)
            {
                controller.MaterialChanged += OnMaterial;
                controller.Impacted += OnImpact;
                controller.Landed += OnLanded;
                controller.Launched += OnLaunched;
                controller.Died += OnDied;
                controller.Revived += OnRevived;
            }
        }

        void OnDisable()
        {
            if (controller == null) return;
            controller.MaterialChanged -= OnMaterial;
            controller.Impacted -= OnImpact;
            controller.Landed -= OnLanded;
            controller.Launched -= OnLaunched;
            controller.Died -= OnDied;
            controller.Revived -= OnRevived;
        }

        void Start() { ApplyLook(); }

        // ---------------------------------------------------------- events

        void OnMaterial(WormMaterial m)
        {
            if (!HasSprites || (controller != null && controller.IsDead)) { pop = 1f; ApplyLook(); return; }
            // Build a clean strip with idle caps so there is no snap ball->horizontal:
            // [current idle frame] + middle + [target idle frame].
            System.Collections.Generic.List<Sprite> seq = new System.Collections.Generic.List<Sprite>();
            Sprite fromIdle = FirstOf(IdleFor(m == WormMaterial.Heavy ? WormMaterial.Squishy : WormMaterial.Heavy));
            Sprite toIdle = FirstOf(IdleFor(m));
            Sprite[] middle = MiddleStripFor(m);
            if (fromIdle != null) seq.Add(fromIdle);
            if (middle != null) for (int i = 0; i < middle.Length; i++) if (middle[i] != null) seq.Add(middle[i]);
            if (toIdle != null) seq.Add(toIdle);
            if (seq.Count > 0)
            {
                transitionStrip = seq.ToArray();
                state = VisState.Transition;
                frame = 0;
                frameTimer = 0f;
                squash = 0f;
                stretch = 0f;
                pop = 0f; // no code-pop on top of hand-drawn transition
            }
            else state = VisState.Idle;
            ApplyLook();
        }

        Sprite FirstOf(Sprite[] strip)
        {
            if (strip == null) return null;
            for (int i = 0; i < strip.Length; i++) if (strip[i] != null) return strip[i];
            return null;
        }

        Sprite[] MiddleStripFor(WormMaterial m)
        {
            // swap flag also flips which direction is "forward"
            bool toHeavy = ShowAsHeavy(m);
            if (toHeavy)
            {
                if (bouncyToRocky != null && bouncyToRocky.Length > 0) return bouncyToRocky;
            }
            else
            {
                if (rockyToBouncy != null && rockyToBouncy.Length > 0) return rockyToBouncy;
                if (bouncyToRocky != null && bouncyToRocky.Length > 0)
                {
                    Sprite[] rev = new Sprite[bouncyToRocky.Length];
                    for (int i = 0; i < bouncyToRocky.Length; i++)
                        rev[i] = bouncyToRocky[bouncyToRocky.Length - 1 - i];
                    return rev;
                }
            }
            return null;
        }

        void OnImpact(ImpactInfo info)
        {
            if (controller == null || controller.IsDead) return;
            float maxLand = IsHeavy ? heavyLandSquash : squishyLandSquash;
            // Keep the old profile amount as a whisper so tuning the asset still matters, but never zero.
            float profileK = controller.Physics.Profile.squashAmount;
            float k = Mathf.Max(maxLand, maxLand * profileK * 2f);
            squash = Mathf.Max(squash, Mathf.Clamp01(info.normalSpeed / Mathf.Max(0.1f, controller.Physics.Profile.maxLaunchSpeed)) * k);
            squashNormal = info.normal;
            stretch = Mathf.Min(stretch, 0.05f); // impact kills stretch
        }

        void OnLanded(Vector2 pos)
        {
            if (controller == null || controller.IsDead) return;
            Vector2 n = Vector2.up;
            if (controller.Physics.Ground != null && controller.Physics.Ground.HasSupport)
                n = controller.Physics.Ground.Normal;
            float speed = controller.Physics.Speed;
            float maxLaunch = Mathf.Max(0.1f, controller.Physics.Profile.maxLaunchSpeed);
            float k = (IsHeavy ? heavyLandSquash : squishyLandSquash) * 0.7f;
            squash = Mathf.Max(squash, Mathf.Clamp01(speed / maxLaunch + 0.25f) * k);
            squashNormal = n;
        }

        void OnLaunched(Vector2 pos, float power01)
        {
            if (controller == null || controller.IsDead) return;
            float maxStretch = IsHeavy ? heavyJumpStretch : squishyJumpStretch;
            stretch = Mathf.Max(stretch, Mathf.Lerp(maxStretch * 0.4f, maxStretch, Mathf.Clamp01(power01)));
            Vector2 v = controller.Physics.Velocity;
            stretchDir = v.sqrMagnitude > 0.5f ? v.normalized : Vector2.up;
            squash = 0f;
            pop = Mathf.Max(pop, 0.4f);
        }

        void OnDied(string reason)
        {
            if (controller != null) deadMaterial = controller.Material;
            state = VisState.Dead;
            frame = 0;
            frameTimer = 0f;
            squash = 0f;
            stretch = 0f;
            ApplyLook();
        }

        void OnRevived()
        {
            state = VisState.Idle;
            frame = 0;
            frameTimer = 0f;
            squash = 0f;
            stretch = 0f;
            ApplyLook();
        }

        // ---------------------------------------------------------- frames

        void ApplyLook()
        {
            if (controller == null) return;
            bool useSprites = HasSprites;
            if (placeholderCoreGo != null) placeholderCoreGo.SetActive(!useSprites);
            if (placeholderEyeGo != null) placeholderEyeGo.SetActive(!useSprites);
            if (!useSprites)
            {
                body.sprite = PlaceholderSprites.Circle;
                body.color = controller.Physics.Profile.color;
                return;
            }
            body.color = Color.white;
            body.sprite = CurrentIdleSprite();
        }

        Sprite CurrentIdleSprite()
        {
            if (controller == null) return FirstOf(bouncyIdle);
            Sprite[] strip = IdleFor(controller.Material);
            if (strip == null || strip.Length == 0) return null;
            return strip[Mathf.Abs(frame) % strip.Length];
        }

        float SpriteWorldSize()
        {
            if (body != null && body.sprite != null && body.sprite.pixelsPerUnit > 0f)
                return body.sprite.rect.width / body.sprite.pixelsPerUnit;
            return 1f;
        }

        // ---------------------------------------------------------- per-frame

        void Update()
        {
            if (controller == null) return;
            WormMaterialProfile p = controller.Physics.Profile;
            float diameter = p.radius * 2f;

            if (HasSprites)
            {
                frameTimer += Time.deltaTime;
                if (state == VisState.Idle)
                {
                    float step = 1f / Mathf.Max(1f, idleFps);
                    bool advanced = false;
                    while (frameTimer >= step) { frameTimer -= step; frame++; advanced = true; }
                    if (advanced || body.sprite == null) body.sprite = CurrentIdleSprite();
                }
                else if (state == VisState.Transition && transitionStrip != null && transitionStrip.Length > 0)
                {
                    float step = 1f / Mathf.Max(1f, transitionFps);
                    while (frameTimer >= step)
                    {
                        frameTimer -= step;
                        frame++;
                        if (frame >= transitionStrip.Length) { state = VisState.Idle; frame = 0; break; }
                    }
                    if (state == VisState.Transition) body.sprite = transitionStrip[Mathf.Min(frame, transitionStrip.Length - 1)];
                    else body.sprite = CurrentIdleSprite();
                }
                else if (state == VisState.Dead)
                {
                    Sprite[] strip = DeathFor(deadMaterial);
                    if (strip != null && strip.Length > 0)
                    {
                        float step = 1f / Mathf.Max(1f, deathFps);
                        while (frameTimer >= step && frame < strip.Length - 1) { frameTimer -= step; frame++; }
                        body.sprite = strip[Mathf.Min(frame, strip.Length - 1)];
                    }
                }
            }

            // ---- juice recover (heavy = faster/snappier, squishy = slower/wobblier)
            float recover = IsHeavy ? heavyRecover : squishyRecover;
            if (squash > 0f) squash = Mathf.MoveTowards(squash, 0f, Time.deltaTime * recover * Mathf.Max(squash, 0.25f));
            if (pop > 0f) pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime / Mathf.Max(0.01f, popSeconds));

            // air stretch follows speed (both materials, heavy less)
            Vector2 vel = controller.Physics.Velocity;
            float speedK = Mathf.Clamp01(vel.magnitude / Mathf.Max(0.1f, p.maxLaunchSpeed));
            float airTarget = controller.IsGrounded ? 0f : speedK * airStretchMax * (IsHeavy ? 0.6f : 1f);
            if (!controller.IsDead)
            {
                stretch = Mathf.MoveTowards(stretch, airTarget, Time.deltaTime * recover * 0.6f);
                if (vel.sqrMagnitude > 1f && stretch > 0.02f) stretchDir = vel.normalized;
            }
            else stretch = 0f;

            // ---- compose scale: base size corrected for real sprite PPU + volume-preserving deform
            float worldSize = HasSprites ? SpriteWorldSize() : 1f;
            float baseScale = (diameter * visualSizeMultiplier) / Mathf.Max(0.01f, worldSize);
            if (!HasSprites) baseScale = diameter; // placeholder sprite is 1 unit

            bool frozen = HasSprites && freezeJuiceDuringTransition && state == VisState.Transition;
            float net = frozen ? 0f : (stretch - squash); // >0 stretch along move dir, <0 squash along impact normal
            Vector2 axis = net >= 0f ? stretchDir : squashNormal;

            float sx = 1f, sy = 1f;
            if (Mathf.Abs(axis.y) >= Mathf.Abs(axis.x)) { sy = 1f + net; sx = 1f - net * 0.55f; }
            else { sx = 1f + net; sy = 1f - net * 0.55f; }
            sx = Mathf.Max(0.3f, sx);
            sy = Mathf.Max(0.3f, sy);

            // breathing (idle only, not dead, not frozen transition)
            if (!frozen && state != VisState.Dead && HasSprites)
            {
                breathePhase += Time.deltaTime * breathingSpeed;
                float b = Mathf.Sin(breathePhase) * breathingAmount * (IsHeavy ? 0.4f : 1f);
                sx += b;
                sy -= b * 0.7f;
            }

            float popK = frozen ? 1f : (1f + (popScale - 1f) * pop);
            transform.localScale = new Vector3(baseScale * sx * popK, baseScale * sy * popK, 1f);

            // ---- orientation: placeholder rolls like a ball, real art just leans
            if (rollPivot != null)
            {
                if (frozen) rollPivot.localRotation = Quaternion.identity;
                else if (HasSprites)
                {
                    float targetTilt = Mathf.Clamp(-vel.x * 0.6f, -tiltMaxDegrees, tiltMaxDegrees);
                    rollPivot.localRotation = Quaternion.Euler(0f, 0f, targetTilt);
                }
                else
                {
                    roll -= vel.x / Mathf.Max(0.05f, p.radius) * Mathf.Rad2Deg * Time.deltaTime;
                    rollPivot.localRotation = Quaternion.Euler(0f, 0f, roll);
                }
            }
        }
    }
}
