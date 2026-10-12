using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Organic conveyor. Fibres lean in a travelling wave; the surface carries light bodies along it.
    /// Squishy is carried strongly, Heavy barely (it flattens the fibres and keeps its place).
    /// Needs a BoxCollider2D (its size is the carpet). Put it on a prefab and place it anywhere, rotated if needed.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class CiliaCarpet : MonoBehaviour, ISurfaceCarrier
    {
        [Header("Carry")]
        [Tooltip("Speed (u/s) of the wave. Squishy is carried at wave speed * squishy influence.")]
        public float waveSpeed = 3f;
        [Tooltip("Local direction of movement. (1,0) = along the carpet's right.")]
        public Vector2 movementDirection = Vector2.right;
        [Tooltip("How quickly the worm is brought up to carry speed (u/s^2) before influence is applied.")]
        public float force = 45f;
        [Range(0f, 1f)] public float squishyInfluence = 1f;
        [Range(0f, 1f)] public float heavyInfluence = 0.15f;

        [Header("Look")]
        public float fibreSpacing = 0.14f;
        public float fibreHeight = 0.5f;
        public float waveLength = 1.6f;
        public float maxLeanDegrees = 35f;
        public Color baseColor = new Color(0.30f, 0.29f, 0.20f);
        public Color fibreColor = new Color(0.56f, 0.54f, 0.35f);

        BoxCollider2D box;
        Transform[] fibres;
        float[] xs;

        public Vector2 DirectionWorld
        {
            get { return transform.TransformDirection(movementDirection.sqrMagnitude > 0f ? movementDirection.normalized : Vector2.right).normalized; }
        }

        float Influence(WormMaterial m) { return m == WormMaterial.Squishy ? squishyInfluence : heavyInfluence; }

        public Vector2 GetCarryVelocity(WormMaterial m) { return DirectionWorld * (waveSpeed * Influence(m)); }
        public float GetCarryAcceleration(WormMaterial m) { return force * Influence(m); }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            Surface s = GetComponent<Surface>();
            if (s == null) s = gameObject.AddComponent<Surface>();
            s.type = SurfaceType.Cilia;
            BuildVisuals();
        }

        void BuildVisuals()
        {
            Vector2 size = box.size;
            Vector2 c = box.offset;
            PlaceholderSprites.Make("Base", transform, PlaceholderSprites.Square, baseColor, c, size, 1);

            int count = Mathf.Max(2, Mathf.RoundToInt(size.x / fibreSpacing));
            fibres = new Transform[count];
            xs = new float[count];
            float top = c.y + size.y * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float x = c.x - size.x * 0.5f + (i + 0.5f) * (size.x / count);
                xs[i] = x;
                GameObject pivot = new GameObject("Fibre");
                pivot.transform.SetParent(transform, false);
                pivot.transform.localPosition = new Vector3(x, top, 0f);
                PlaceholderSprites.Make("v", pivot.transform, PlaceholderSprites.Square, fibreColor, new Vector2(0f, fibreHeight * 0.5f), new Vector2(0.07f, fibreHeight), 2);
                fibres[i] = pivot.transform;
            }
        }

        void Update()
        {
            if (fibres == null) return;
            float sign = Mathf.Sign(Vector2.Dot(DirectionWorld, transform.right));
            float t = Time.time;
            for (int i = 0; i < fibres.Length; i++)
            {
                float phase = (xs[i] / waveLength - sign * t * waveSpeed / waveLength) * Mathf.PI * 2f;
                float lean = maxLeanDegrees * (0.55f + 0.45f * Mathf.Sin(phase)); // always leans with the flow, wave modulates it
                fibres[i].localRotation = Quaternion.Euler(0f, 0f, -sign * lean);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.position;
            Vector3 d = transform.TransformDirection(movementDirection.normalized);
            Gizmos.DrawLine(p, p + d * 1.5f);
        }
    }
}
