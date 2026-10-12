using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Area that changes the camera while the worm is inside it (e.g. zoom out over a big drop, zoom in
    /// for a tight puzzle). Add to an empty GameObject with a BoxCollider2D (auto set to trigger), size it
    /// over the area. Leaving the area blends back to the Main Camera's CameraFollow2D values.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CameraZone : MonoBehaviour
    {
        public bool overrideSize = true;
        [Min(1f)] public float orthographicSize = 18f;
        public bool overrideOffset = false;
        public Vector2 offset = new Vector2(0f, 2f);
        [Tooltip("When zones overlap, the highest priority wins.")]
        public int priority = 0;

        void Reset()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
        }

        void Awake()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c != null && !c.isTrigger)
            {
                Debug.LogWarning("[CameraZone] " + name + ": collider was solid, switched to trigger.", this);
                c.isTrigger = true;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<WormController>() != null) CameraFollow2D.Enter(this);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<WormController>() != null) CameraFollow2D.Exit(this);
        }

        void OnDisable()
        {
            CameraFollow2D.Exit(this);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Collider2D c = GetComponent<Collider2D>();
            if (c == null) return;
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
#endif
    }
}
