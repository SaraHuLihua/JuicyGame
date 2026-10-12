using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Reusable physical chain built from the FG_Props_2 ring art (prefab: Prefabs/Gameplay/Chain).
    /// One small Rigidbody2D + CapsuleCollider2D per link, joined by HingeJoint2D. The chain hangs from
    /// this object's position along its local DOWN axis, so rotating the object gives an angled chain.
    ///
    ///   Length      = Link Count x Link Length (do NOT stretch with Transform Scale: at Play a Y scale is
    ///                 converted into more/fewer links and X scale into thicker links, then reset to 1).
    ///   Break point = the joint above link "Break At Link". It snaps when:
    ///                 - the worm hits the chain hard enough (the existing BreakableObject on this object
    ///                   decides, same mass x speed rule as every other breakable), or
    ///                 - the joint is overloaded (Joint Break Force), or
    ///                 - any script / UnityEvent calls Break().
    ///                 - a worm at least Break Under Mass heavy stands on it (Heavy 2.5 yes, Squishy 0.3 no).
    ///   On a break: joint removed -> BreakEffect at the break point -> onBreak -> the lower links fall,
    ///   swing and collide on their own. Sound lives in ChainSounds (listens to LinkImpact / Broke).
    ///   Wall to wall: Bottom = PinnedInPlace + a Bottom Anchor (empty object on the other wall). Both pinned
    ///   ends get a small wall plate so the chain reads as bolted to the wall.
    /// Physics never depends on visuals or audio.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BreakableChain : MonoBehaviour
    {
        public enum EndMode { Free, PinnedInPlace, AttachedToBody }

        [Header("Shape")]
        [Tooltip("Number of physical links. Each link is one Rigidbody2D. 6-20 is a good range.")]
        [Min(2)] public int linkCount = 12;
        [Tooltip("Distance between two joints (world units). Chain length = Link Count x Link Length.")]
        [Min(0.05f)] public float linkLength = 0.45f;
        [Tooltip("Collision thickness of each link (world units). Ignored while Collider Matches Art is on and the chain has art.")]
        [Min(0.02f)] public float linkWidth = 0.3f;
        [Tooltip("On = the collision thickness follows the drawn rings (Art Scale included), so the worm rests ON the art instead of floating above it.")]
        public bool colliderMatchesArt = true;
        [Tooltip("Share of the drawn ring height used as collision thickness (0.85 = the worm sinks a little into the ring outline).")]
        [Range(0.3f, 1.2f)] public float artColliderFill = 0.85f;

        [Header("Ends")]
        [Tooltip("PinnedInPlace: the top hangs from this object's position. AttachedToBody: hangs from Top Body (moves with it). Free: nothing holds it.")]
        public EndMode top = EndMode.PinnedInPlace;
        public Rigidbody2D topBody;
        [Tooltip("Pin the bottom too, for chains spanning two points (angled / stretched chains). Free = hanging chain.")]
        public EndMode bottom = EndMode.Free;
        public Rigidbody2D bottomBody;
        [Tooltip("Pinned bottom only: how much shorter the span is than the chain (0.06 = 6%), so it hangs in a soft curve instead of a rigid bar.")]
        [Range(0f, 0.4f)] public float bottomSlack = 0.06f;
        [Tooltip("Pinned bottom only (optional): an empty object placed on the wall. The far end is pinned exactly there, the chain aims at it and " +
                 "Link Count is worked out from the distance and Bottom Slack. Empty = the far end is where the straight chain ends.")]
        public Transform bottomAnchor;

        [Header("Wall plates (visual only)")]
        [Tooltip("Draw a bolted plate at every end pinned in place, so the chain reads as fixed to the wall.")]
        public bool showWallPlates = true;
        [Tooltip("Plate sprite. Empty = a round plate with a bolt.")]
        public Sprite wallPlateSprite;
        [Tooltip("Plate diameter (world units).")]
        public float wallPlateSize = 0.6f;
        public Color wallPlateColor = new Color(0.07f, 0.07f, 0.075f);

        [Header("Look (FG_Props_2 rings, alternating face-on ring / side-on bar)")]
        [Tooltip("Face-on rings (FG_Props_2_11, _12, _14). Used on even links. Empty = plain placeholder squares.")]
        public Sprite[] ringSprites;
        [Tooltip("Side-on links (FG_Props_2_8, _9, _10). Used on odd links. Empty = rings on every link.")]
        public Sprite[] barSprites;
        [Tooltip("Sprite pixels from one ring centre to the next in the art (FG_Props_2_18 = ~190). Sets how big the art is drawn for a given Link Length.")]
        public float artRingPitchPixels = 190f;
        [Tooltip("Extra size multiplier on the art only (physics unchanged).")]
        public float artScale = 1f;
        public Color tint = Color.white;
        public int sortingOrder = 5;

        [Header("Physics")]
        [Tooltip("Mass of ONE link. Worm: Bouncy 0.3, Rocky 2.5. Light links swing more when hit.")]
        public float linkMass = 0.15f;
        public float gravityScale = 0.8f;
        [Tooltip("Air drag. Higher = calmer swinging, settles sooner.")]
        public float linearDamping = 0.25f;
        [Tooltip("Rotational drag on each link. Higher = less wiggle/jitter.")]
        public float angularDamping = 1.5f;
        [Tooltip("How the worm treats the chain (friction / bounce / landing).")]
        public SurfaceType surfaceType = SurfaceType.Metal;
        [Tooltip("Links collide with each other (not just neighbours). Off is calmer and cheaper.")]
        public bool selfCollision = false;
        [Tooltip("Physics layer for the links (-1 = same as this object). Links collide with whatever this layer collides with in Physics 2D settings.")]
        public int linkLayer = -1;

        [Header("Performance")]
        [Tooltip("Let the chain fall asleep once it has stopped moving: a sleeping chain costs almost nothing and wakes up by itself when the worm touches it. Keep it on when you use many chains.")]
        public bool sleepWhenStill = true;
        [Tooltip("Seconds without being touched, and with every link slower than Sleep Speed, before it sleeps.")]
        [Min(0.1f)] public float sleepAfter = 1f;
        [Tooltip("Links slower than this (units/s) count as still.")]
        public float sleepSpeed = 0.4f;
        [Tooltip("Continuous collision on every link (expensive with many links). Off = only the falling part after a break uses it.")]
        public bool continuousCollision = false;
        [Tooltip("Most physics bodies one chain may build. A longer chain keeps all its drawn rings but groups them, several per body " +
                 "(e.g. 125 rings -> 32 bodies of 4 rings). Lower = lighter but stiffer. 0 = one body per ring (heavy).")]
        [Min(0)] public int maxPhysicsLinks = 40;

        [Header("Break point")]
        public bool breakable = true;
        [Tooltip("The joint ABOVE this link breaks (1 = just under the top link, Link Count - 1 = above the last link).")]
        [Min(1)] public int breakAtLink = 6;
        [Tooltip("The break joint also snaps on its own above this force (Infinity = only impacts / Break()). Hard to tune: the chain's own weight already pulls on it.")]
        public float jointBreakForce = Mathf.Infinity;
        [Tooltip("Lightest worm that can break the chain, by standing on it OR by hitting it (worm mass: Heavy 2.5, Squishy 0.3). " +
                 "Squishy's hits then never crack it, however fast. 0 = off: standing never breaks it and any hit hard enough does.")]
        [Min(0f)] public float breakUnderMass = 1.5f;
        [Tooltip("Seconds the heavy worm must stand on the chain before it snaps.")]
        [Min(0f)] public float breakUnderMassDelay = 0.25f;
        [Tooltip("On = it snaps at the link the worm hits or stands on. Off = always at Break At Link.")]
        public bool breakWhereHit = true;

        [Header("Break strength (harder hit = more damage)")]
        [Tooltip("Impact force (worm mass x fall speed: Rocky falling at 20 u/s = 50) needed for each EXTRA snap after the first. " +
                 "Lower = shatters into more pieces. 0 = always a single snap.")]
        [Min(0f)] public float forcePerExtraBreak = 18f;
        [Tooltip("Most joints one hit can snap.")]
        [Min(1)] public int maxBreaks = 5;
        [Tooltip("Links between two snaps of the same hit.")]
        [Min(1)] public int breakSpacing = 3;
        [Tooltip("Kick given to the links near the hit, per unit of impact force (impulse). 0 = they just fall.")]
        [Min(0f)] public float impactPush = 0.012f;
        [Tooltip("How many links around the hit get kicked.")]
        [Min(0)] public int pushRadius = 6;
        [Tooltip("Break VFX size grows with the hit: Effect Scale x (1 + extra snaps x this).")]
        [Min(0f)] public float effectGrowth = 0.35f;

        [Header("Break VFX (optional, purely visual)")]
        [Tooltip("Played exactly at the break point. Leave empty for no effect, or use the one on this prefab.")]
        public BreakEffect breakEffect;

        public UnityEvent onBreak;

        /// <summary>A link hit something: (world point, impact speed, link index). For audio/fx only.</summary>
        public event Action<Vector2, float, int> LinkImpact;
        /// <summary>Raised once when the chain snaps, with the break point. For audio/fx only.</summary>
        public event Action<Vector2> Broke;

        public bool IsBroken { get; private set; }
        public int LinkCountBuilt { get { return links.Count; } }
        public IList<Rigidbody2D> Links { get { return links; } }

        readonly List<Rigidbody2D> links = new List<Rigidbody2D>();
        readonly List<Vector3> spawnPos = new List<Vector3>();
        readonly List<float> spawnRot = new List<float>();
        readonly List<HingeJoint2D> prevJoints = new List<HingeJoint2D>(); // joint of link i to link i-1 (or the top pin)
        HingeJoint2D breakJoint;
        int breakIndex;
        // Set just before HandleBreak by the hit that caused it (force, push direction). Weight / Break() = one plain snap.
        float pendingForce;
        Vector2 pendingDir = Vector2.down;
        float weightTimer;
        float lastDisturbed;
        float nextSleepCheck;
        bool sleeping;
        BreakableObject breakableObject;
        bool built;
        int builtCount;
        float builtLinkLength;
        int builtGroup = 1; // drawn rings per physics body
        float builtWidth;

        /// <summary>World position of the break point (between link breakAtLink-1 and breakAtLink).</summary>
        public Vector2 BreakPointWorld
        {
            get
            {
                int b = built ? breakIndex : ClampedBreakIndex(PreviewCount());
                if (built && b < links.Count && links[b] != null)
                    return links[b].transform.TransformPoint(new Vector3(0f, builtLinkLength * 0.5f, 0f));
                return (Vector2)transform.position + ChainDirection() * PreviewLinkLength() * b;
            }
        }

        int ClampedBreakIndex(int count) { return Mathf.Clamp(Mathf.RoundToInt(breakAtLink / (float)Mathf.Max(1, built ? builtGroup : 1)), 1, Mathf.Max(1, count - 1)); }

        bool UsesAnchor { get { return bottom == EndMode.PinnedInPlace && bottomAnchor != null; } }

        // Transform Scale is turned into link count / thickness instead of distorting the bodies.
        // With a Bottom Anchor the count comes from the distance instead: chain length = span / (1 - slack).
        int PreviewCount()
        {
            if (UsesAnchor)
            {
                float span = Vector2.Distance(transform.position, bottomAnchor.position);
                return Mathf.Max(2, Mathf.CeilToInt(span / Mathf.Max(0.05f, 1f - bottomSlack) / Mathf.Max(0.05f, linkLength)));
            }
            return Mathf.Max(2, Mathf.RoundToInt(linkCount * Mathf.Abs(transform.lossyScale.y)));
        }
        float PreviewLinkLength() { return linkLength; }
        float PreviewWidth()
        {
            float art = colliderMatchesArt ? ArtThickness() : 0f;
            if (art > 0f) return Mathf.Max(0.02f, art * artColliderFill);
            return linkWidth * Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x));
        }

        /// <summary>World direction the chain hangs in from the top: towards the Bottom Anchor, else this object's local down.</summary>
        Vector2 ChainDirection()
        {
            if (UsesAnchor)
            {
                Vector2 d = (Vector2)bottomAnchor.position - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0001f) return d.normalized;
            }
            return transform.rotation * Vector3.down;
        }

        /// <summary>Drawn height of the thickest ring across the chain (world units), 0 when there is no art.</summary>
        float ArtThickness()
        {
            Sprite[] set = ringSprites != null && ringSprites.Length > 0 ? ringSprites : barSprites;
            if (set == null) return 0f;
            float best = 0f;
            for (int i = 0; i < set.Length; i++)
                if (set[i] != null)
                    best = Mathf.Max(best, set[i].rect.height / Mathf.Max(1f, set[i].pixelsPerUnit) * ArtScale(set[i])); // drawn turned 90 degrees: height = across
            return best;
        }

        // ------------------------------------------------------------------ build

        void Awake()
        {
            if (!Application.isPlaying) return;
#if UNITY_EDITOR
            ClearPreview();
#endif
            Build();
        }

        void Build()
        {
            if (built) return;
            built = true;

            int rings = PreviewCount();
            builtGroup = maxPhysicsLinks > 0 ? Mathf.Max(1, Mathf.CeilToInt(rings / (float)maxPhysicsLinks)) : 1;
            builtCount = Mathf.Max(2, Mathf.CeilToInt(rings / (float)builtGroup));
            builtLinkLength = PreviewLinkLength() * rings / builtCount; // same total length as the drawn rings
            builtWidth = PreviewWidth();
            if (transform.parent == null || IsUnitScale(transform.parent.lossyScale))
                transform.localScale = Vector3.one; // the scale has been converted, keep bodies undistorted
            else
                Debug.LogWarning("[BreakableChain] " + name + " sits under a scaled parent; link shapes may be distorted. Use Link Count / Link Length instead.", this);

            int layer = linkLayer >= 0 ? linkLayer : gameObject.layer;
            List<Collider2D> cols = new List<Collider2D>();
            Rigidbody2D prev = null;
            Vector2 topWorld = transform.position;
            Vector2 dir = ChainDirection();
            Quaternion linkRot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f); // link local down = dir

            for (int i = 0; i < builtCount; i++)
            {
                GameObject go = new GameObject("Link" + i);
                go.layer = layer;
                go.transform.SetParent(transform, false);
                go.transform.position = topWorld + dir * builtLinkLength * (i + 0.5f);
                go.transform.rotation = linkRot;

                Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.mass = Mathf.Max(0.01f, linkMass * builtGroup); // same total weight however the rings are grouped
                rb.gravityScale = gravityScale;
                SetDamping(rb, linearDamping, angularDamping);
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = continuousCollision ? CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;

                CapsuleCollider2D col = go.AddComponent<CapsuleCollider2D>();
                col.direction = CapsuleDirection2D.Vertical;
                // Caps reach half a width past each joint, so neighbouring links overlap: no gaps a wall can slip through.
                col.size = new Vector2(builtWidth, builtLinkLength + builtWidth);
                cols.Add(col);

                HingeJoint2D j = go.AddComponent<HingeJoint2D>();
                j.autoConfigureConnectedAnchor = false;
                j.enableCollision = false;
                j.anchor = new Vector2(0f, builtLinkLength * 0.5f);
                if (i == 0)
                {
                    if (top == EndMode.Free) { Destroy(j); j = null; }
                    else ConnectToWorldOrBody(j, top == EndMode.AttachedToBody ? topBody : null, topWorld);
                }
                else
                {
                    j.connectedBody = prev;
                    j.connectedAnchor = new Vector2(0f, -builtLinkLength * 0.5f);
                }

                ChainLinkListener l = go.AddComponent<ChainLinkListener>();
                l.chain = this;
                l.index = i;

                BuildVisual(go.transform, i);
                links.Add(rb);
                prevJoints.Add(j);
                spawnPos.Add(go.transform.localPosition);
                spawnRot.Add(go.transform.localEulerAngles.z);
                prev = rb;
            }

            if (bottom != EndMode.Free && links.Count > 0)
            {
                Rigidbody2D last = links[links.Count - 1];
                HingeJoint2D pin = last.gameObject.AddComponent<HingeJoint2D>();
                pin.autoConfigureConnectedAnchor = false;
                pin.enableCollision = false;
                pin.anchor = new Vector2(0f, -builtLinkLength * 0.5f);
                // Pull the far pin slightly towards the top: the chain starts with a little slack and sags naturally.
                Vector2 endWorld = last.transform.TransformPoint(new Vector3(0f, -builtLinkLength * 0.5f, 0f));
                endWorld = UsesAnchor ? (Vector2)bottomAnchor.position : topWorld + (endWorld - topWorld) * (1f - bottomSlack);
                ConnectToWorldOrBody(pin, bottom == EndMode.AttachedToBody ? bottomBody : null, endWorld);
                if (bottom == EndMode.PinnedInPlace) MakeWallPlate(transform, endWorld, HideFlags.None);
            }
            if (top == EndMode.PinnedInPlace) MakeWallPlate(transform, topWorld, HideFlags.None);

            if (!selfCollision)
            {
                // Only links whose capsules can reach each other need ignoring (neighbours overlap by design).
                // All pairs would be Link Count squared calls: 7750 for a 125-link chain, a hitch on load.
                int reach = Mathf.CeilToInt((builtLinkLength + builtWidth) / Mathf.Max(0.01f, builtLinkLength)) + 1;
                for (int a = 0; a < cols.Count; a++)
                    for (int b = a + 1; b < cols.Count && b <= a + reach; b++)
                        Physics2D.IgnoreCollision(cols[a], cols[b], true);
            }
            if (builtCount > 60 || builtCount * builtGroup > 400)
                Debug.LogWarning("[BreakableChain] " + name + " builds " + builtCount + " physics links (Link Count x Transform Scale Y). " +
                                 "Every link is a Rigidbody2D: lower Link Count (or the Y scale) if several chains make the game slow.", this);
            lastDisturbed = Time.time;

            breakIndex = ClampedBreakIndex(builtCount);
            if (breakable && builtCount >= 2)
            {
                breakJoint = prevJoints[breakIndex];
                if (breakJoint != null) breakJoint.breakForce = jointBreakForce;
            }

            // Friction/landing rules for the worm come from the shared Surface system.
            Surface s = GetComponent<Surface>();
            if (s == null) s = gameObject.AddComponent<Surface>();
            s.type = surfaceType;
            s.ApplyMaterial();

            // Impact-based breaking goes through the project's BreakableObject (same rule as brittles).
            breakableObject = GetComponent<BreakableObject>();
            if (breakableObject != null && breakUnderMass > 0f)
                breakableObject.minimumMass = Mathf.Max(breakableObject.minimumMass, breakUnderMass); // Squishy's hits never crack it
            if (breakableObject != null && !breakable)
            {
                Destroy(breakableObject); // unbreakable chain: impacts must not count as breaks at all
                breakableObject = null;
            }
            if (breakableObject != null)
            {
                breakableObject.disableCollidersOnBreak = false;
                breakableObject.hideSpriteOnBreak = false;
                breakableObject.Broken += OnBreakableBroken;
            }
        }

        static bool IsUnitScale(Vector3 s)
        {
            return Mathf.Abs(s.x - 1f) < 0.001f && Mathf.Abs(s.y - 1f) < 0.001f;
        }

        void ConnectToWorldOrBody(HingeJoint2D j, Rigidbody2D body, Vector2 worldPoint)
        {
            if (body != null)
            {
                j.connectedBody = body;
                j.connectedAnchor = body.transform.InverseTransformPoint(worldPoint);
            }
            else
            {
                j.connectedBody = null;
                j.connectedAnchor = worldPoint; // world space when there is no connected body
            }
        }

        /// <summary>Bolted plate drawn at a pinned end (visual only, no collider).</summary>
        void MakeWallPlate(Transform parent, Vector2 worldPos, HideFlags flags)
        {
            if (!showWallPlates || wallPlateSize <= 0.001f) return;
            float parentScale = Mathf.Max(0.0001f, Mathf.Abs(parent.lossyScale.x));
            Sprite plate = wallPlateSprite != null ? wallPlateSprite : PlaceholderSprites.Circle;
            GameObject p = new GameObject("WallPlate");
            p.hideFlags = flags;
            p.layer = gameObject.layer;
            p.transform.SetParent(parent, false);
            p.transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
            p.transform.localScale = Vector3.one * (wallPlateSize / Mathf.Max(0.0001f, plate.bounds.size.x) / parentScale);
            SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
            sr.sprite = plate;
            sr.color = wallPlateColor;
            sr.sortingOrder = sortingOrder + 2; // over the end link
            if (wallPlateSprite != null) return;
            GameObject bolt = new GameObject("Bolt");
            bolt.hideFlags = flags;
            bolt.layer = gameObject.layer;
            bolt.transform.SetParent(p.transform, false);
            bolt.transform.localScale = Vector3.one * 0.38f;
            SpriteRenderer bsr = bolt.AddComponent<SpriteRenderer>();
            bsr.sprite = PlaceholderSprites.Circle;
            bsr.color = Color.Lerp(wallPlateColor, Color.white, 0.25f);
            bsr.sortingOrder = sortingOrder + 3;
        }

        static void SetDamping(Rigidbody2D rb, float linear, float angular)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = linear;
            rb.angularDamping = angular;
#else
            rb.drag = linear;
            rb.angularDrag = angular;
#endif
        }

        void BuildVisual(Transform parent, int link)
        {
            // One physics link carries builtGroup drawn rings/bars, laid from its top (+y) down.
            float vis = builtLinkLength / builtGroup;
            for (int k = 0; k < builtGroup; k++)
            {
                Transform t = BuildVisualPiece(parent, link * builtGroup + k, vis);
                if (t != null) t.localPosition = new Vector3(0f, builtLinkLength * 0.5f - vis * (k + 0.5f), 0f);
            }
        }

        Transform BuildVisualPiece(Transform parent, int i, float vis)
        {
            Sprite sprite = PickSprite(i);
            if (sprite == null)
            {
                return PlaceholderSprites.Make("Visual", parent, PlaceholderSprites.Square,
                    (i % 2 == 0) ? tint : tint * 0.75f, Vector2.zero, new Vector2(builtWidth, vis), sortingOrder).transform;
            }
            GameObject v = new GameObject("Visual");
            v.layer = parent.gameObject.layer;
            v.transform.SetParent(parent, false);
            v.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // the art is drawn horizontally
            v.transform.localScale = Vector3.one * ArtScale(sprite);
            SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder + (IsRingLink(i) ? 1 : 0); // rings drawn over the bars threading through them
            return v.transform;
        }

        bool IsRingLink(int i)
        {
            return (barSprites == null || barSprites.Length == 0) || i % 2 == 0;
        }

        Sprite PickSprite(int i)
        {
            Sprite[] set = IsRingLink(i) ? ringSprites : barSprites;
            if (set == null || set.Length == 0) return null;
            Sprite s = set[(i / 2) % set.Length];
            return s;
        }

        float ArtScale(Sprite s)
        {
            // One link = half a ring pitch in the art (ring, bar, ring, bar...).
            float pitchUnits = Mathf.Max(1f, artRingPitchPixels) / Mathf.Max(1f, s.pixelsPerUnit);
            float ll = built ? builtLinkLength / builtGroup : PreviewLinkLength(); // one drawn ring/bar, whatever the physics grouping
            return (2f * ll / pitchUnits) * Mathf.Max(0.01f, artScale);
        }

        // ------------------------------------------------------------------ breaking

        void OnBreakableBroken(ImpactInfo hit)
        {
            ChainLinkListener l = hit.collider != null ? hit.collider.GetComponent<ChainLinkListener>() : null;
            pendingForce = hit.impactForce;
            pendingDir = hit.normal.sqrMagnitude > 0.001f ? -hit.normal : Vector2.down; // normal points to the worm: push away from it
            if (breakWhereHit && l != null && l.chain == this) BreakAt(l.index);
            else Break();
        }

        void FixedUpdate()
        {
            if (!Application.isPlaying || !built) return;
            // Is the worm standing on one of our links?
            WormController w = WormController.Instance;
            ChainLinkListener l = null;
            if (w != null && !w.IsDead && w.Physics != null && w.Physics.Ground != null
                && w.Physics.Ground.HasSupport && w.Physics.Ground.Collider != null)
                l = w.Physics.Ground.Collider.GetComponent<ChainLinkListener>();
            bool standing = l != null && l.chain == this;
            if (standing) Disturb();
            UpdateSleep();

            // Weight rule: the worm's REAL mass against a threshold (no "if Heavy"), like every breakable.
            if (IsBroken || !breakable || breakUnderMass <= 0f || !standing || w.Physics.Mass < breakUnderMass) { weightTimer = 0f; return; }
            weightTimer += Time.fixedDeltaTime;
            if (weightTimer >= breakUnderMassDelay) BreakAt(breakWhereHit ? l.index : breakIndex);
        }

        // ------------------------------------------------------------------ sleeping (performance)

        void Disturb()
        {
            lastDisturbed = Time.time;
            sleeping = false;
        }

        void UpdateSleep()
        {
            if (!sleepWhenStill || Time.time < nextSleepCheck) return;
            nextSleepCheck = Time.time + 0.2f;
            if (sleeping)
            {
                // Something woke it (a hit, a joint): watch it again until it is still.
                for (int i = 0; i < links.Count; i++)
                    if (links[i] != null && links[i].IsAwake()) { sleeping = false; lastDisturbed = Time.time; break; }
                return;
            }
            if (Time.time - lastDisturbed < sleepAfter) return;
            float limit = sleepSpeed * sleepSpeed;
            for (int i = 0; i < links.Count; i++)
                if (links[i] != null && links[i].IsAwake() && links[i].GetVelocity().sqrMagnitude > limit) { lastDisturbed = Time.time - sleepAfter * 0.5f; return; }
            // Every link is still: put the whole chain to sleep. Box2D wakes it when anything awake touches it.
            for (int i = 0; i < links.Count; i++) if (links[i] != null) links[i].Sleep();
            sleeping = true;
        }

        /// <summary>Break the joint above link <paramref name="linkIndex"/> (clamped so both ends keep at least one link).</summary>
        public void BreakAt(int linkIndex)
        {
            if (!built || IsBroken || !breakable || links.Count < 2) return;
            breakIndex = Mathf.Clamp(linkIndex, 1, links.Count - 1);
            breakJoint = prevJoints[breakIndex];
            HandleBreak();
        }

        internal void OnLinkJointBroken(Joint2D j)
        {
            if (j != null && j == breakJoint) HandleBreak();
        }

        /// <summary>Break now (buttons, triggers, UnityEvents, other scripts).</summary>
        public void Break()
        {
            if (!built || IsBroken || !breakable) return;
            HandleBreak();
        }

        /// <summary>How many joints a hit of this force snaps (1 at the threshold, one more every Force Per Extra Break).</summary>
        public int BreaksForForce(float force)
        {
            float threshold = breakableObject != null ? breakableObject.breakThreshold : 0f;
            if (forcePerExtraBreak <= 0f || force <= threshold) return 1;
            return Mathf.Clamp(1 + Mathf.FloorToInt((force - threshold) / forcePerExtraBreak), 1, Mathf.Max(1, maxBreaks));
        }

        void HandleBreak()
        {
            if (IsBroken) return;
            IsBroken = true;
            Vector2 at = BreakPointWorld;
            float force = pendingForce;
            Vector2 dir = pendingDir;
            pendingForce = 0f;
            pendingDir = Vector2.down;

            // 1. physics: remove the connection, the lower links are now free bodies.
            if (breakJoint != null) Destroy(breakJoint);
            if (breakIndex < prevJoints.Count && prevJoints[breakIndex] == breakJoint) prevJoints[breakIndex] = null;
            breakJoint = null;
            // A harder hit snaps more joints, spread around the first one (above and below in turn).
            int breaks = BreaksForForce(force);
            List<Vector2> extraPoints = new List<Vector2>();
            for (int k = 1, made = 1; made < breaks && k <= links.Count; k++)
            {
                int[] candidates = { breakIndex + k * breakSpacing, breakIndex - k * breakSpacing };
                for (int c = 0; c < 2 && made < breaks; c++)
                {
                    int idx = candidates[c];
                    if (idx < 1 || idx >= links.Count || prevJoints[idx] == null || links[idx] == null) continue;
                    extraPoints.Add(links[idx].transform.TransformPoint(new Vector3(0f, builtLinkLength * 0.5f, 0f)));
                    Destroy(prevJoints[idx]);
                    prevJoints[idx] = null;
                    made++;
                }
            }
            weightTimer = 0f;
            Disturb();
            for (int i = 0; i < links.Count; i++)
                if (links[i] != null)
                {
                    links[i].WakeUp();
                    links[i].collisionDetectionMode = CollisionDetectionMode2D.Continuous; // falling pieces must not tunnel through floors
                }
            // The links near the hit get kicked away from the worm, harder for harder hits.
            if (impactPush > 0f && force > 0f)
                for (int i = Mathf.Max(0, breakIndex - pushRadius); i <= Mathf.Min(links.Count - 1, breakIndex + pushRadius); i++)
                {
                    if (links[i] == null) continue;
                    float falloff = 1f - Mathf.Abs(i - breakIndex) / (float)(pushRadius + 1);
                    Vector2 jitter = new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.2f, 0.2f));
                    links[i].AddForce((dir.normalized + jitter) * force * impactPush * falloff, ForceMode2D.Impulse);
                    links[i].angularVelocity += UnityEngine.Random.Range(-1f, 1f) * force * 4f * falloff;
                }

            // 2. visuals and listeners (never touch physics).
            if (breakEffect != null)
            {
                float baseScale = breakEffect.effectScale;
                breakEffect.effectScale = baseScale * (1f + (breaks - 1) * effectGrowth);
                breakEffect.Play(at, dir);
                breakEffect.effectScale = baseScale;
                for (int i = 0; i < extraPoints.Count; i++) breakEffect.Play(extraPoints[i], dir);
            }
            GameJuice.Shake(0.12f + 0.08f * breaks);
            if (Broke != null) Broke(at);
            onBreak.Invoke();
        }

        internal void OnLinkCollision(int index, Collision2D c)
        {
            Disturb();
            if (LinkImpact == null || c.contactCount == 0) return;
            float speed = c.relativeVelocity.magnitude;
            LinkImpact(c.GetContact(0).point, speed, index);
        }

        /// <summary>Put the chain back together and re-hang it (test reset, DebugTools T).</summary>
        public void Restore()
        {
            if (!built) return;
            for (int i = 0; i < links.Count; i++)
            {
                Rigidbody2D rb = links[i];
                if (rb == null) continue;
                rb.transform.localPosition = spawnPos[i];
                rb.transform.localRotation = Quaternion.Euler(0f, 0f, spawnRot[i]);
                rb.position = rb.transform.position;
                rb.rotation = rb.transform.eulerAngles.z;
                rb.SetVelocity(Vector2.zero);
                rb.angularVelocity = 0f;
                rb.collisionDetectionMode = continuousCollision ? CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;
            }
            if (breakable && links.Count >= 2)
            {
                for (int b = 1; b < links.Count; b++) // every joint a hit snapped
                {
                    if (prevJoints[b] != null || links[b] == null) continue;
                    HingeJoint2D j = links[b].gameObject.AddComponent<HingeJoint2D>();
                    j.autoConfigureConnectedAnchor = false;
                    j.enableCollision = false;
                    j.anchor = new Vector2(0f, builtLinkLength * 0.5f);
                    j.connectedBody = links[b - 1];
                    j.connectedAnchor = new Vector2(0f, -builtLinkLength * 0.5f);
                    j.breakForce = Mathf.Infinity;
                    prevJoints[b] = j;
                }
                breakIndex = ClampedBreakIndex(builtCount);
                breakJoint = prevJoints[breakIndex];
                if (breakJoint != null) breakJoint.breakForce = jointBreakForce;
            }
            weightTimer = 0f;
            Disturb();
            IsBroken = false;
            if (breakableObject != null && breakableObject.IsBroken) breakableObject.Restore();
        }

        void OnDestroy()
        {
            if (breakableObject != null) breakableObject.Broken -= OnBreakableBroken;
        }

        // ------------------------------------------------------------------ editor preview

#if UNITY_EDITOR
        const string PreviewName = "~ChainPreview";
        GameObject preview;

        void OnEnable()
        {
            if (!Application.isPlaying) RequestPreview();
        }

        void OnDisable()
        {
            if (!Application.isPlaying) ClearPreview();
        }

        Vector3 previewTop, previewAnchor;

        void Update()
        {
            // Edit mode: rebuild the preview when the chain or its Bottom Anchor is moved in the Scene view.
            if (Application.isPlaying || !UsesAnchor) return;
            if (transform.position != previewTop || bottomAnchor.position != previewAnchor)
            {
                previewTop = transform.position;
                previewAnchor = bottomAnchor.position;
                RequestPreview();
            }
        }

        void OnValidate()
        {
            linkCount = Mathf.Max(2, linkCount);
            breakAtLink = Mathf.Clamp(breakAtLink, 1, Mathf.Max(1, linkCount - 1));
            if (!Application.isPlaying) RequestPreview();
        }

        void RequestPreview()
        {
            UnityEditor.EditorApplication.delayCall -= RebuildPreview;
            UnityEditor.EditorApplication.delayCall += RebuildPreview;
        }

        /// <summary>Edit-mode only: shows the real link art in the Scene/Game view. Never saved, never simulated.</summary>
        void RebuildPreview()
        {
            if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
            if (UnityEditor.EditorUtility.IsPersistent(this) || !gameObject.scene.IsValid()) return; // prefab asset itself
            ClearPreview();
            preview = new GameObject(PreviewName);
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.SetParent(transform, false);
            // Undo the root scale on the preview so it matches what Play will build.
            Vector3 ls = transform.lossyScale;
            preview.transform.localScale = new Vector3(SafeInv(ls.x), SafeInv(ls.y), 1f);
            Vector2 dir = ChainDirection();
            preview.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f); // local down = dir
            int n = PreviewCount();
            float ll = PreviewLinkLength();
            float w = PreviewWidth();
            for (int i = 0; i < n; i++)
            {
                Sprite s = PickSprite(i);
                GameObject v = new GameObject("Link" + i);
                v.hideFlags = HideFlags.HideAndDontSave;
                v.transform.SetParent(preview.transform, false);
                v.transform.localPosition = new Vector3(0f, -ll * (i + 0.5f), 0f);
                SpriteRenderer sr = v.AddComponent<SpriteRenderer>();
                if (s != null)
                {
                    v.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    v.transform.localScale = Vector3.one * ArtScale(s);
                    sr.sprite = s;
                    sr.color = tint;
                    sr.sortingOrder = sortingOrder + (IsRingLink(i) ? 1 : 0);
                }
                else
                {
                    sr.sprite = PlaceholderSprites.Square;
                    v.transform.localScale = new Vector3(w, ll, 1f);
                    sr.color = (i % 2 == 0) ? tint : tint * 0.75f;
                    sr.sortingOrder = sortingOrder;
                }
            }
            Vector2 topWorld = transform.position;
            if (top == EndMode.PinnedInPlace) MakeWallPlate(preview.transform, topWorld, HideFlags.HideAndDontSave);
            if (bottom == EndMode.PinnedInPlace)
                MakeWallPlate(preview.transform, UsesAnchor ? (Vector2)bottomAnchor.position : topWorld + dir * ll * n * (1f - bottomSlack), HideFlags.HideAndDontSave);
        }

        static float SafeInv(float v) { return Mathf.Abs(v) < 0.0001f ? 1f : 1f / v; }

        void ClearPreview()
        {
            if (preview != null) DestroyImmediate(preview);
            preview = null;
            // Also clean up previews left over from a script reload.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform c = transform.GetChild(i);
                if (c.name == PreviewName && (c.gameObject.hideFlags & HideFlags.DontSave) != 0) DestroyImmediate(c.gameObject);
            }
        }

        void OnDrawGizmos()
        {
            int n = built ? builtCount : PreviewCount();
            float ll = built ? builtLinkLength : PreviewLinkLength();
            if (built && Application.isPlaying) return; // the real links are visible
            Vector3 topW = transform.position;
            Vector3 down = ChainDirection();
            Gizmos.color = new Color(0.8f, 0.8f, 0.9f, 0.6f);
            Gizmos.DrawLine(topW, topW + down * ll * n);
            Gizmos.DrawWireSphere(topW, 0.12f);
            if (UsesAnchor) Gizmos.DrawWireSphere(bottomAnchor.position, 0.12f);
            else if (bottom != EndMode.Free) Gizmos.DrawWireSphere(topW + down * ll * n, 0.12f);
            if (breakable)
            {
                Vector3 bp = topW + down * ll * ClampedBreakIndex(n);
                Gizmos.color = Color.red;
                float s = 0.2f;
                Gizmos.DrawLine(bp + new Vector3(-s, -s), bp + new Vector3(s, s));
                Gizmos.DrawLine(bp + new Vector3(-s, s), bp + new Vector3(s, -s));
            }
        }
#endif
    }

    /// <summary>Plumbing: forwards one link's physics callbacks to its BreakableChain. Auto-added, ignore it.</summary>
    [AddComponentMenu("")]
    public class ChainLinkListener : MonoBehaviour
    {
        [HideInInspector] public BreakableChain chain;
        [HideInInspector] public int index;

        void OnCollisionEnter2D(Collision2D c)
        {
            if (chain != null) chain.OnLinkCollision(index, c);
        }

        void OnJointBreak2D(Joint2D j)
        {
            if (chain != null) chain.OnLinkJointBroken(j);
        }
    }
}
