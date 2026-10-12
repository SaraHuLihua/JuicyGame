using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Very small smooth follow camera for the test scenes.</summary>
    public class CameraFollow2D : MonoBehaviour
    {
        public Transform target;
        public Vector2 offset = new Vector2(0f, 2f);
        [Tooltip("Higher = snappier.")]
        public float followSpeed = 6f;

        void LateUpdate()
        {
            if (target == null)
            {
                if (WormController.Instance != null) target = WormController.Instance.transform;
                else return;
            }
            Vector3 goal = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, goal, k);
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        }
    }
}
