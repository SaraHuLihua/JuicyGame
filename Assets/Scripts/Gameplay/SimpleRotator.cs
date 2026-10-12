using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Reusable rotation for gears / rotating elements. Visual only: it does not push the worm.
    /// Combine with Hazard (spiked gear) or a carrier surface if it should also affect gameplay.
    /// </summary>
    public class SimpleRotator : MonoBehaviour
    {
        [Tooltip("Degrees per second. Negative = counter-clockwise.")]
        public float degreesPerSecond = 45f;
        public bool rotateInFixedUpdate = false;

        void Update() { if (!rotateInFixedUpdate) transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime); }
        void FixedUpdate() { if (rotateInFixedUpdate) transform.Rotate(0f, 0f, degreesPerSecond * Time.fixedDeltaTime); }
    }
}
