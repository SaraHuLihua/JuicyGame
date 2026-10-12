using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// A chain that runs continuously. The worm lands on it, is carried, and inherits the chain's velocity when it launches.
    /// Both materials are carried at full chain speed; Heavy takes longer to get up to speed (more inertia).
    /// Needs a BoxCollider2D (its size is the chain run).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CarrierChain : MonoBehaviour, ISurfaceCarrier
    {
        [Header("Carry")]
        [Tooltip("Chain speed in units/s along the chain's local right (negative = reverse).")]
        public float speed = 3.5f;
        [Tooltip("u/s^2 pulling Squishy up to chain speed. Must beat the surface friction (about 14 u/s^2 for Squishy on Metal).")]
        public float squishyGrip = 45f;
        [Tooltip("u/s^2 for Heavy. Heavy fights more friction (about 42 u/s^2 on Metal), so keep this above that; closer to it = lags more when boarding.")]
        public float heavyGrip = 55f;

        [Header("Look")]
        public float linkSpacing = 0.5f;
        public Color bodyColor = new Color(0.23f, 0.25f, 0.28f);
        public Color linkColorA = new Color(0.45f, 0.48f, 0.52f);
        public Color linkColorB = new Color(0.30f, 0.32f, 0.35f);

        BoxCollider2D box;
        Transform[] links;
        float total;

        public Vector2 DirectionWorld { get { return (Vector2)transform.right * Mathf.Sign(speed == 0f ? 1f : speed); } }

        public Vector2 GetCarryVelocity(WormMaterial m) { return (Vector2)transform.right * speed; }
        public float GetCarryAcceleration(WormMaterial m) { return m == WormMaterial.Squishy ? squishyGrip : heavyGrip; }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            Surface s = GetComponent<Surface>();
            if (s == null) s = gameObject.AddComponent<Surface>();
            s.type = SurfaceType.Metal;
            BuildVisuals();
        }

        void BuildVisuals()
        {
            Vector2 size = box.size;
            Vector2 c = box.offset;
            PlaceholderSprites.Make("Body", transform, PlaceholderSprites.Square, bodyColor, c, size, 1);

            int count = Mathf.Max(2, Mathf.RoundToInt(size.x / linkSpacing));
            total = count * linkSpacing;
            links = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                Color col = (i % 2 == 0) ? linkColorA : linkColorB;
                SpriteRenderer sr = PlaceholderSprites.Make("Link", transform, PlaceholderSprites.Square, col, Vector2.zero, new Vector2(linkSpacing * 0.8f, size.y * 0.6f), 2);
                links[i] = sr.transform;
            }
        }

        void Update()
        {
            if (links == null) return;
            Vector2 size = box.size;
            Vector2 c = box.offset;
            float start = c.x - total * 0.5f;
            for (int i = 0; i < links.Length; i++)
            {
                float x = start + Mathf.Repeat(i * linkSpacing + Time.time * speed + linkSpacing * 0.5f, total);
                links[i].localPosition = new Vector3(x, c.y + size.y * 0.1f, 0f);
            }
        }
    }
}
