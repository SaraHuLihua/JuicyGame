using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// A streak the worm leaves behind while it moves, in the colour of its outside: pink while Squishy, purple
    /// while Heavy (Rocky). It blends to the new colour when the form changes, and is wiped on death and respawn
    /// so it never draws a line across the level. Lives on its own child, so the Bounce Pad's white streak is untouched.
    /// </summary>
    [RequireComponent(typeof(WormController))]
    public class WormTrail : MonoBehaviour
    {
        [Tooltip("Outside colour of the Squishy worm.")]
        public Color squishyColor = new Color(1f, 0.655f, 0.875f, 1f);
        [Tooltip("Outside colour of the Heavy (Rocky) worm.")]
        public Color heavyColor = new Color(0.663f, 0.647f, 1f, 1f);
        [Range(0f, 1f)] public float opacity = 0.75f;
        [Tooltip("Seconds a point of the streak stays before fading out (longer = longer streak).")]
        [Min(0.01f)] public float length = 0.3f;
        [Tooltip("Streak width at the worm, as a fraction of the worm's width.")]
        [Range(0.1f, 1.5f)] public float width = 0.8f;
        [Tooltip("The streak only shows above this speed (units/s), so it doesn't smear while the worm sits or rolls slowly.")]
        [Min(0f)] public float minSpeed = 4f;
        [Tooltip("Seconds to blend to the new colour when the form changes.")]
        [Min(0f)] public float colorBlend = 0.15f;
        [Tooltip("Sorting order of the streak. The worm body draws at 10, so 9 keeps the streak behind it.")]
        public int sortingOrder = 9;

        WormController controller;
        CircleCollider2D circle;
        TrailRenderer trail;
        Color current;
        Vector2 lastPos;

        void Awake()
        {
            controller = GetComponent<WormController>();
            circle = GetComponent<CircleCollider2D>();

            GameObject go = new GameObject("Trail");
            go.transform.SetParent(transform, false);
            trail = go.AddComponent<TrailRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.sortingOrder = sortingOrder;
            trail.numCapVertices = 4;
            trail.numCornerVertices = 2;
            trail.minVertexDistance = 0.05f;
            trail.emitting = false;
        }

        void OnEnable()
        {
            controller.Died += OnDied;
            controller.Revived += Wipe;
        }

        void OnDisable()
        {
            controller.Died -= OnDied;
            controller.Revived -= Wipe;
        }

        void Start()
        {
            current = Target();
            lastPos = transform.position;
            Wipe();
        }

        Color Target() { return controller.Material == WormMaterial.Heavy ? heavyColor : squishyColor; }

        void OnDied(string reason) { trail.emitting = false; }

        void Wipe()
        {
            trail.Clear();
            lastPos = transform.position;
        }

        void LateUpdate()
        {
            Vector2 pos = transform.position;
            // A respawn or teleport moves the worm far in one frame: don't draw a line across the level.
            if ((pos - lastPos).sqrMagnitude > 9f) trail.Clear();
            lastPos = pos;

            float speed = controller.Physics != null ? controller.Physics.Speed : 0f;
            trail.emitting = !controller.IsDead && speed > minSpeed;

            current = colorBlend > 0f ? Color.Lerp(current, Target(), Time.deltaTime / colorBlend) : Target();
            trail.startColor = new Color(current.r, current.g, current.b, current.a * opacity);
            trail.endColor = new Color(current.r, current.g, current.b, 0f);
            trail.time = length;

            float r = circle != null ? circle.radius * Mathf.Abs(transform.lossyScale.x) : 0.375f;
            trail.widthCurve = AnimationCurve.Linear(0f, 2f * r * width, 1f, 0f);
            trail.sortingOrder = sortingOrder;
        }
    }
}
