using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Puts the project-wide physics values the prototype was tuned for in place.
    /// Drop one in every gameplay scene (the builder does this for you).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrap : MonoBehaviour
    {
        public bool applyOnAwake = true;
        [Tooltip("Units/s^2. Web prototype: 1200 px/s^2 = 37.5 u/s^2. Per-material gravity is a multiplier on this.")]
        public float gravity = GameSettings.Gravity;
        [Tooltip("Fixed physics steps per second. Web prototype: 120.")]
        public float physicsStepRate = GameSettings.PhysicsStepRate;

        void Awake()
        {
            if (applyOnAwake) Apply();
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            Physics2D.gravity = new Vector2(0f, -gravity);
            Time.fixedDeltaTime = 1f / Mathf.Max(30f, physicsStepRate);
        }
    }
}
