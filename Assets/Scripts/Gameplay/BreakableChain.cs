using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Hanging chain with a break point in the middle. No hundreds of segments: the links you see
    /// are VISUAL only; the physics is two bodies (static Upper + dynamic Lower) joined by one
    /// HingeJoint2D at the break point.
    ///   BEFORE: Lower hangs from Upper and can swing like a pendulum.
    ///   BREAK (joint auto-break, worm impact, or Break() call): the joint is gone, Lower falls
    ///     with gravity, collides with walls/floor and settles. Upper stays attached.
    /// Drag the BreakableChain prefab into a level, position its top where it should hang from,
    /// tune lengths + break values in the Inspector. Hierarchy is built automatically:
    ///   BreakableChain
    ///   ├── Upper (static body + link visuals)
    ///   └── Lower (dynamic body + hinge + collider + link visuals)
    /// </summary>
    public class BreakableChain : MonoBehaviour
    {
        [Header("Sections (units, downwards from this object)")]
        public float upperLength = 2f;
        public float lowerLength = 2.5f;
        public float width = 0.35f;

        [Header("Links (visual only)")]
        [Tooltip("Optional: sliced chain-link sprite (see FG_Props_2). Empty = plain squares like CarrierChain.")]
        public Sprite linkSprite;
        public float linkSpacing = 0.35f;
        public Color linkColorA = Color.white;
        public Color linkColorB = new Color(0.7f, 0.7f, 0.75f);

        [Header("Lower section physics")]
        public float lowerMass = 1f;
        public float lowerGravityScale = 1f;
        public float lowerFriction = 0.4f;
        public float lowerBounciness = 0.25f;

        [Header("Break point (the hinge between Upper and Lower)")]
        [Tooltip("Unity breaks the hinge automatically above this reaction force. Infinity = never (use impact/manual break).")]
        public float hingeBreakForce = Mathf.Infinity;
        [Tooltip("Same for torque. Infinity = never.")]
        public float hingeBreakTorque = Mathf.Infinity;
        [Tooltip("Also break when the worm hits the lower section this hard (mass * speed). Heavy: 15..65, Squishy: 2..9.")]
        public bool breakOnWormImpact = true;
        public float wormImpactThreshold = 15f;

        [Header("Break VFX (optional, purely visual)")]
        [Tooltip("Played at the break point. Auto-created with defaults if empty; disable its effectEnabled to silence.")]
        public BreakEffect breakEffect;

        public UnityEvent onBreak;

        public bool IsBroken { get; private set; }
        public Rigidbody2D LowerBody { get; private set; }
        HingeJoint2D joint;
        bool built;

        /// <summary>World position of the break point (top of the Lower section).</summary>
        public Vector2 BreakPointWorld
        {
            get { return built && LowerBody != null ? LowerBody.transform.TransformPoint(new Vector3(0f, lowerLength * 0.5f, 0f)) : (Vector2)transform.position; }
        }

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (built) return;
            built = true;
            upperLength = Mathf.Max(0.3f, upperLength);
            lowerLength = Mathf.Max(0.5f, lowerLength);

            // ---- upper: static anchor body with just visuals + landing surface
            GameObject upper = new GameObject("Upper");
            upper.transform.SetParent(transform, false);
            upper.transform.localPosition = new Vector3(0f, -upperLength * 0.5f, 0f);
            Rigidbody2D upperBody = upper.AddComponent<Rigidbody2D>();
            upperBody.bodyType = RigidbodyType2D.Static;
            BoxCollider2D upperCol = upper.AddComponent<BoxCollider2D>();
            upperCol.size = new Vector2(width, upperLength);
            Surface upperSurface = upper.AddComponent<Surface>();
            upperSurface.type = SurfaceType.Metal;
            upperSurface.ApplyMaterial();
            BuildLinks(upper.transform, upperLength);

            // ---- lower: dynamic body hinged under the upper
            GameObject lower = new GameObject("Lower");
            lower.transform.SetParent(transform, false);
            lower.transform.localPosition = new Vector3(0f, -upperLength - lowerLength * 0.5f, 0f);
            LowerBody = lower.AddComponent<Rigidbody2D>();
            LowerBody.bodyType = RigidbodyType2D.Dynamic;
            LowerBody.mass = Mathf.Max(0.05f, lowerMass);
            LowerBody.gravityScale = lowerGravityScale;
            LowerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            BoxCollider2D lowerCol = lower.AddComponent<BoxCollider2D>();
            lowerCol.size = new Vector2(width, lowerLength);
            PhysicsMaterial2D pm = new PhysicsMaterial2D("ChainLower");
            pm.friction = lowerFriction;
            pm.bounciness = lowerBounciness;
            lowerCol.sharedMaterial = pm;
            Surface lowerSurface = lower.AddComponent<Surface>();
            lowerSurface.type = SurfaceType.Metal;
            lowerSurface.ApplyMaterial();
            BuildLinks(lower.transform, lowerLength);

            joint = lower.AddComponent<HingeJoint2D>();
            joint.connectedBody = upperBody;
            joint.anchor = new Vector2(0f, lowerLength * 0.5f);
            joint.connectedAnchor = new Vector2(0f, -upperLength * 0.5f);
            joint.breakForce = hingeBreakForce;
            joint.breakTorque = hingeBreakTorque;

            ChainSegmentListener listener = lower.AddComponent<ChainSegmentListener>();
            listener.chain = this;

            if (breakEffect == null)
            {
                breakEffect = gameObject.AddComponent<BreakEffect>();
                breakEffect.color = new Color(0.8f, 0.8f, 0.85f);
            }
        }

        void BuildLinks(Transform parent, float length)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.05f, linkSpacing)));
            for (int i = 0; i < n; i++)
            {
                float y = length * 0.5f - linkSpacing * (i + 0.5f);
                SpriteRenderer sr;
                if (linkSprite != null)
                {
                    GameObject go = new GameObject("Link" + i);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = new Vector3(0f, y, 0f);
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = linkSprite;
                    sr.sortingOrder = 5;
                    float world = linkSprite.rect.width / Mathf.Max(1f, linkSprite.pixelsPerUnit);
                    if (world > 0.001f) go.transform.localScale = Vector3.one * (width / world);
                }
                else
                {
                    sr = PlaceholderSprites.Make("Link" + i, parent, PlaceholderSprites.Square,
                        Color.white, new Vector2(0f, y), new Vector2(width, Mathf.Min(linkSpacing, width)), 5);
                }
                sr.color = (i % 2 == 0) ? linkColorA : linkColorB;
            }
        }

        void FixedUpdate()
        {
            // The physics joint destroys itself when overstressed: notice and handle it once.
            if (built && !IsBroken && joint == null) HandleBreak(BreakPointWorld);
        }

        void OnValidate()
        {
            if (joint != null)
            {
                joint.breakForce = hingeBreakForce;
                joint.breakTorque = hingeBreakTorque;
            }
            if (LowerBody != null)
            {
                LowerBody.mass = Mathf.Max(0.05f, lowerMass);
                LowerBody.gravityScale = lowerGravityScale;
            }
        }

        /// <summary>Break now (buttons, triggers, other scripts). Lower falls, VFX plays at the break point.</summary>
        public void Break()
        {
            if (!built || IsBroken) return;
            HandleBreak(BreakPointWorld);
        }

        internal void OnLowerCollision(Collision2D c)
        {
            if (!built || IsBroken || !breakOnWormImpact) return;
            WormController worm = c.collider.GetComponentInParent<WormController>();
            if (worm == null || worm.Physics == null) return;
            Vector2 rel = worm.Physics.Velocity - LowerBody.GetVelocity();
            float normalSpeed = 0f;
            Vector2 n = Vector2.up;
            for (int i = 0; i < c.contactCount; i++)
            {
                ContactPoint2D cp = c.GetContact(i);
                float s = Mathf.Abs(Vector2.Dot(rel, cp.normal));
                if (s > normalSpeed) { normalSpeed = s; n = cp.normal; }
            }
            if (worm.Physics.Mass * normalSpeed >= wormImpactThreshold)
                HandleBreak(c.GetContact(0).point);
        }

        void HandleBreak(Vector2 at)
        {
            if (IsBroken) return;
            IsBroken = true;
            if (joint != null) Destroy(joint);
            if (LowerBody != null) LowerBody.WakeUp(); // momentum is preserved automatically
            if (breakEffect != null) breakEffect.Play(at, Vector2.down);
            onBreak.Invoke();
        }

        /// <summary>Put the chain back together (test reset). Re-hangs Lower under Upper.</summary>
        public void Restore()
        {
            if (!built) return;
            IsBroken = false;
            LowerBody.transform.localPosition = new Vector3(0f, -upperLength - lowerLength * 0.5f, 0f);
            LowerBody.transform.localRotation = Quaternion.identity;
            LowerBody.SetVelocity(Vector2.zero);
            LowerBody.angularVelocity = 0f;
            if (joint == null)
            {
                Rigidbody2D upperBody = transform.Find("Upper").GetComponent<Rigidbody2D>();
                joint = LowerBody.gameObject.AddComponent<HingeJoint2D>();
                joint.connectedBody = upperBody;
                joint.anchor = new Vector2(0f, lowerLength * 0.5f);
                joint.connectedAnchor = new Vector2(0f, -upperLength * 0.5f);
            }
            joint.breakForce = hingeBreakForce;
            joint.breakTorque = hingeBreakTorque;
        }
    }

    /// <summary>Plumbing: forwards the Lower section's collisions to its BreakableChain. Auto-added, ignore it.</summary>
    [AddComponentMenu("")]
    public class ChainSegmentListener : MonoBehaviour
    {
        [HideInInspector] public BreakableChain chain;
        void OnCollisionEnter2D(Collision2D c)
        {
            if (chain != null) chain.OnLowerCollision(c);
        }
    }
}
