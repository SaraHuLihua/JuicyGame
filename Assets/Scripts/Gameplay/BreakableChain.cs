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
    ///   On a break: joint removed -> BreakEffect at the break point -> onBreak -> the lower links fall,
    ///   swing and collide on their own. Sound lives in ChainSounds (listens to LinkImpact / Broke).
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
        [Tooltip("Collision thickness of each link (world units).")]
        [Min(0.02f)] public float linkWidth = 0.3f;

        [Header("Ends")]
        [Tooltip("PinnedInPlace: the top hangs from this object's position. AttachedToBody: hangs from Top Body (moves with it). Free: nothing holds it.")]
        public EndMode top = EndMode.PinnedInPlace;
        public Rigidbody2D topBody;
        [Tooltip("Pin the bottom too, for chains spanning two points (angled / stretched chains). Free = hanging chain.")]
        public EndMode bottom = EndMode.Free;
        public Rigidbody2D bottomBody;
        [Tooltip("Pinned bottom only: how much shorter the span is than the chain (0.06 = 6%), so it hangs in a soft curve instead of a rigid bar.")]
        [Range(0f, 0.4f)] public float bottomSlack = 0.06f;

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

        [Header("Break point")]
        public bool breakable = true;
        [Tooltip("The joint ABOVE this link breaks (1 = just under the top link, Link Count - 1 = above the last link).")]
        [Min(1)] public int breakAtLink = 6;
        [Tooltip("The break joint also snaps on its own above this force (Infinity = only impacts / Break()).")]
        public float jointBreakForce = Mathf.Infinity;

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
        HingeJoint2D breakJoint;
        BreakableObject breakableObject;
        bool built;
        int builtCount;
        float builtLinkLength;
        float builtWidth;

        /// <summary>World position of the break point (between link breakAtLink-1 and breakAtLink).</summary>
        public Vector2 BreakPointWorld
        {
            get
            {
                int b = ClampedBreakIndex(built ? builtCount : PreviewCount());
                if (built && b < links.Count && links[b] != null)
                    return links[b].transform.TransformPoint(new Vector3(0f, builtLinkLength * 0.5f, 0f));
                return transform.TransformPoint(new Vector3(0f, -PreviewLinkLength() * b, 0f));
            }
        }

        int ClampedBreakIndex(int count) { return Mathf.Clamp(breakAtLink, 1, Mathf.Max(1, count - 1)); }

        // Transform Scale is turned into link count / thickness instead of distorting the bodies.
        int PreviewCount() { return Mathf.Max(2, Mathf.RoundToInt(linkCount * Mathf.Abs(transform.lossyScale.y))); }
        float PreviewLinkLength() { return linkLength; }
        float PreviewWidth() { return linkWidth * Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x)); }

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

            builtCount = PreviewCount();
            builtLinkLength = PreviewLinkLength();
            builtWidth = PreviewWidth();
            if (transform.parent == null || IsUnitScale(transform.parent.lossyScale))
                transform.localScale = Vector3.one; // the scale has been converted, keep bodies undistorted
            else
                Debug.LogWarning("[BreakableChain] " + name + " sits under a scaled parent; link shapes may be distorted. Use Link Count / Link Length instead.", this);

            int layer = linkLayer >= 0 ? linkLayer : gameObject.layer;
            List<Collider2D> cols = new List<Collider2D>();
            Rigidbody2D prev = null;

            for (int i = 0; i < builtCount; i++)
            {
                GameObject go = new GameObject("Link" + i);
                go.layer = layer;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, -builtLinkLength * (i + 0.5f), 0f);
                go.transform.localRotation = Quaternion.identity;

                Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.mass = Mathf.Max(0.01f, linkMass);
                rb.gravityScale = gravityScale;
                SetDamping(rb, linearDamping, angularDamping);
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

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
                    else ConnectToWorldOrBody(j, top == EndMode.AttachedToBody ? topBody : null, transform.position);
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
                spawnPos.Add(go.transform.localPosition);
                spawnRot.Add(0f);
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
                Vector2 topWorld = transform.position;
                Vector2 endWorld = last.transform.TransformPoint(new Vector3(0f, -builtLinkLength * 0.5f, 0f));
                endWorld = topWorld + (endWorld - topWorld) * (1f - bottomSlack);
                ConnectToWorldOrBody(pin, bottom == EndMode.AttachedToBody ? bottomBody : null, endWorld);
            }

            if (!selfCollision)
                for (int a = 0; a < cols.Count; a++)
                    for (int b = a + 1; b < cols.Count; b++)
                        Physics2D.IgnoreCollision(cols[a], cols[b], true);

            if (breakable && builtCount >= 2)
            {
                breakJoint = links[ClampedBreakIndex(builtCount)].GetComponent<HingeJoint2D>();
                if (breakJoint != null) breakJoint.breakForce = jointBreakForce;
            }

            // Friction/landing rules for the worm come from the shared Surface system.
            Surface s = GetComponent<Surface>();
            if (s == null) s = gameObject.AddComponent<Surface>();
            s.type = surfaceType;
            s.ApplyMaterial();

            // Impact-based breaking goes through the project's BreakableObject (same rule as brittles).
            breakableObject = GetComponent<BreakableObject>();
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

        void BuildVisual(Transform parent, int i)
        {
            Sprite sprite = PickSprite(i);
            if (sprite == null)
            {
                PlaceholderSprites.Make("Visual", parent, PlaceholderSprites.Square,
                    (i % 2 == 0) ? tint : tint * 0.75f, Vector2.zero, new Vector2(builtWidth, builtLinkLength), sortingOrder);
                return;
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
            float ll = built ? builtLinkLength : PreviewLinkLength();
            return (2f * ll / pitchUnits) * Mathf.Max(0.01f, artScale);
        }

        // ------------------------------------------------------------------ breaking

        void OnBreakableBroken(ImpactInfo hit) { Break(); }

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

        void HandleBreak()
        {
            if (IsBroken) return;
            IsBroken = true;
            Vector2 at = BreakPointWorld;

            // 1. physics: remove the connection, the lower links are now free bodies.
            if (breakJoint != null) Destroy(breakJoint);
            breakJoint = null;
            int b = ClampedBreakIndex(builtCount);
            for (int i = b; i < links.Count; i++) if (links[i] != null) links[i].WakeUp();

            // 2. visuals and listeners (never touch physics).
            if (breakEffect != null) breakEffect.Play(at, Vector2.down);
            if (Broke != null) Broke(at);
            onBreak.Invoke();
        }

        internal void OnLinkCollision(int index, Collision2D c)
        {
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
            }
            if (breakable && links.Count >= 2)
            {
                int b = ClampedBreakIndex(builtCount);
                HingeJoint2D j = breakJoint;
                if (j == null)
                {
                    j = links[b].gameObject.AddComponent<HingeJoint2D>();
                    j.autoConfigureConnectedAnchor = false;
                    j.enableCollision = false;
                    j.anchor = new Vector2(0f, builtLinkLength * 0.5f);
                    j.connectedBody = links[b - 1];
                    j.connectedAnchor = new Vector2(0f, -builtLinkLength * 0.5f);
                }
                j.breakForce = jointBreakForce;
                breakJoint = j;
            }
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
            Vector3 down = -(transform.rotation * Vector3.up);
            Gizmos.color = new Color(0.8f, 0.8f, 0.9f, 0.6f);
            Gizmos.DrawLine(topW, topW + down * ll * n);
            Gizmos.DrawWireSphere(topW, 0.12f);
            if (bottom != EndMode.Free) Gizmos.DrawWireSphere(topW + down * ll * n, 0.12f);
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
