using System;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Entry point other systems talk to. Owns no physics and no visuals:
    ///   WormController -> WormPhysics, WormLauncher, GroundDetector, LaunchCounter, (WormVisual on a child)
    /// </summary>
    [RequireComponent(typeof(WormPhysics), typeof(WormLauncher), typeof(LaunchCounter))]
    public class WormController : MonoBehaviour
    {
        public static WormController Instance { get; private set; }

        public WormMaterial startMaterial = WormMaterial.Squishy;
        [Tooltip("Falling below this world Y respawns the worm at its last landing.")]
        public float respawnBelowY = -40f;

        WormPhysics physics;
        WormLauncher launcher;
        LaunchCounter counter;

        public WormPhysics Physics { get { return physics; } }
        public WormLauncher Launcher { get { return launcher; } }
        public LaunchCounter Counter { get { return counter; } }

        // ---- the small API the rest of the game reads ----
        public WormMaterial Material { get { return physics.Material; } }
        public bool IsGrounded { get { return physics.IsGrounded; } }
        public bool CanLaunch { get { return physics.CanLaunch && !inputLocked && !IsDead; } }
        public int LaunchCount { get { return counter.Count; } }
        public Vector2 LastLandingPosition { get; private set; }
        public bool inputLocked;
        public bool IsDead { get; private set; }

        public event Action<WormMaterial> MaterialChanged;
        public event Action<Vector2> Landed;
        public event Action<Vector2, float> Launched;   // position, power 0..1
        public event Action<ImpactInfo> Impacted;
        public event Action<string> Died;
        public event Action Revived;

        void Awake()
        {
            Instance = this;
            physics = GetComponent<WormPhysics>();
            launcher = GetComponent<WormLauncher>();
            counter = GetComponent<LaunchCounter>();
            physics.SetMaterial(startMaterial);
            LastLandingPosition = transform.position;

            physics.Landed += OnPhysicsLanded;
            physics.Impact += OnPhysicsImpact;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (inputLocked || IsDead) return;

            bool rightClickSwitch = GameInput.RightDown && !launcher.IsAiming && !launcher.CancelledThisFrame;
            if (GameInput.SwitchDown || rightClickSwitch) ChangeMaterial();

            if (transform.position.y < respawnBelowY) Kill("fell out of the world");
        }

        // ------------------------------------------------------------------ material

        /// <summary>Squishy &lt;-&gt; Heavy. Works ANYTIME, including airborne. Preserves momentum (see WormPhysics.SetMaterial).</summary>
        public bool ChangeMaterial()
        {
            return SetMaterial(physics.Material == WormMaterial.Squishy ? WormMaterial.Heavy : WormMaterial.Squishy);
        }

        public bool SetMaterial(WormMaterial m)
        {
            if (IsDead || inputLocked) return false;
            if (m == physics.Material) return false;
            physics.SetMaterial(m);
            if (MaterialChanged != null) MaterialChanged(m);
            return true;
        }

        // ------------------------------------------------------------------ launching

        public bool PerformLaunch(Vector2 direction, float power01, float forceMultiplier)
        {
            if (IsDead || !CanLaunch) return false;            // no mid-air relaunch, no launch while dead
            physics.Launch(direction, power01, forceMultiplier);
            counter.Increment();
            if (Launched != null) Launched(transform.position, power01);
            return true;
        }

        // ------------------------------------------------------------------ respawn / reset

        /// <summary>Proper death: plays Death anim, blocks input/launch/switch until respawn. Call RespawnAtLastLanding() later.</summary>
        public void Die(string reason)
        {
            if (IsDead) return;
            IsDead = true;
            if (Died != null) Died(reason);
        }

        /// <summary>Hazards call this (rollers crushing, falling out of the world, ...). Costs nothing extra: the launch was already counted.</summary>
        public void Kill(string reason)
        {
            // Legacy instant-respawn behaviour used by existing hazards/tests.
            // Fires Died so the Death anim can flash, then immediately revives at last landing.
            if (!IsDead) Die(reason);
            physics.Teleport(LastLandingPosition);
            IsDead = false;
            if (Revived != null) Revived();
        }

        public void RespawnAtLastLanding()
        {
            physics.Teleport(LastLandingPosition);
            if (IsDead)
            {
                IsDead = false;
                if (Revived != null) Revived();
            }
        }

        /// <summary>Hard reset used by test tools: position, material, launch counter.</summary>
        public void ResetRun(Vector2 position, WormMaterial material)
        {
            IsDead = false;
            physics.SetMaterial(material);
            physics.Teleport(position);
            LastLandingPosition = position;
            counter.ResetCount();
            if (Revived != null) Revived();
            if (MaterialChanged != null) MaterialChanged(material);
        }

        // ------------------------------------------------------------------ forwarding

        void OnPhysicsLanded(Vector2 pos)
        {
            LastLandingPosition = pos;
            if (Landed != null) Landed(pos);
        }

        void OnPhysicsImpact(ImpactInfo info)
        {
            if (Impacted != null) Impacted(info);
        }
    }
}
