using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Looks straight down for support under the worm. Used by WormPhysics every physics step.
    /// "Support" = any solid floor-ish collider; "ValidLanding" = support whose surface allows landing.
    /// </summary>
    public class GroundDetector : MonoBehaviour
    {
        [Tooltip("Extra distance below the worm that still counts as touching.")]
        public float probeDistance = 0.06f;
        [Range(0f, 1f), Tooltip("Minimum normal.y to count as floor (0.6 is about a 53 degree slope).")]
        public float minNormalY = 0.6f;
        [Range(0.5f, 1f)]
        public float castRadiusScale = 0.9f;

        static readonly RaycastHit2D[] hits = new RaycastHit2D[12];

        public bool HasSupport { get; private set; }
        public bool ValidLanding { get; private set; }
        public Vector2 Normal { get; private set; }
        public Collider2D Collider { get; private set; }
        public Surface Surface { get; private set; }
        public SurfaceProfile Profile { get; private set; }
        public ISurfaceCarrier Carrier { get; private set; }
        public Rigidbody2D Body { get; private set; }

        public void Probe(CircleCollider2D own)
        {
            HasSupport = false;
            ValidLanding = false;
            Normal = Vector2.up;
            Collider = null; Surface = null; Profile = null; Carrier = null; Body = null;

            float scale = Mathf.Max(Mathf.Abs(own.transform.lossyScale.x), Mathf.Abs(own.transform.lossyScale.y));
            float r = own.radius * scale;
            Vector2 origin = own.bounds.center;

            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            float dist = r * (1f - castRadiusScale) + probeDistance;
            int n = Physics2D.CircleCast(origin, r * castRadiusScale, Vector2.down, filter, hits, dist);

            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Collider2D c = hits[i].collider;
                if (c == null || c == own) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody == own.attachedRigidbody) continue;
                if (hits[i].normal.y < minNormalY) continue;
                if (hits[i].distance < bestDist) { bestDist = hits[i].distance; best = i; }
            }
            if (best < 0) return;

            Collider = hits[best].collider;
            Normal = hits[best].normal;
            Surface = Surface.Of(Collider);
            Profile = Surface != null ? Surface.Profile : SurfaceProfile.GetDefault(SurfaceType.Metal);
            Carrier = Collider.GetComponentInParent<ISurfaceCarrier>();
            Body = Collider.attachedRigidbody;
            HasSupport = true;

            BreakableObject br = Collider.GetComponentInParent<BreakableObject>();
            bool intact = br == null || !br.IsBroken;
            ValidLanding = Profile.allowsLanding && intact;
        }
    }
}
