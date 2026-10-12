using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// End of the level: when the worm enters this trigger (the BoxCollider2D on this object) the level is complete
    /// and LevelFlow shows the "You did it!" screen with the jump count, Retry and Main Menu.
    /// Move / resize the object to change where the level ends.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public class LevelFinish : MonoBehaviour
    {
        public UnityEvent onFinished;

        void Awake()
        {
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other) { TryFinish(other); }
        void OnTriggerStay2D(Collider2D other) { TryFinish(other); }

        void TryFinish(Collider2D other)
        {
            if (LevelFlow.IsComplete) return;
            WormController w = WormController.Instance;
            if (w == null || w.IsDead || w.Physics == null || other.attachedRigidbody != w.Physics.Body) return;
            LevelFlow.CompleteLevel(transform.position);
            onFinished.Invoke();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            BoxCollider2D b = GetComponent<BoxCollider2D>();
            if (b == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.35f);
            Gizmos.DrawCube(b.offset, b.size);
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(b.offset, b.size);
        }
#endif
    }
}
