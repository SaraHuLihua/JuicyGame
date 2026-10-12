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
        [Tooltip("Longest frame the physics tries to catch up on (seconds). Unity's 0.333 at 120 steps/s means up to 40 steps in one slow frame, which makes the next frame slow too (lag that feeds itself). 0.1 = at most 12 steps.")]
        [Range(0.02f, 0.333f)] public float maxFrameCatchUp = 0.1f;

        void Awake()
        {
            if (applyOnAwake) Apply();
        }

        [ContextMenu("Apply")]
        public void Apply()
        {
            Physics2D.gravity = new Vector2(0f, -gravity);
            Time.fixedDeltaTime = 1f / Mathf.Max(30f, physicsStepRate);
            Time.maximumDeltaTime = Mathf.Max(Time.fixedDeltaTime, maxFrameCatchUp);
        }
    }
}
