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
        [Tooltip("Falling below this world Y respawns the worm at its last landing. Keep it below the lowest floor of every level (MechanicsTest goes down to about y -70).")]
        public float respawnBelowY = -100f;

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
        /// <summary>True once the worm has touched a Checkpoint (cleared by ResetRun).</summary>
        public bool HasCheckpoint { get; private set; }
        public Vector2 CheckpointPosition { get; private set; }
        /// <summary>Where a death sends the worm back to: the checkpoint if one was reached, else the last landing.</summary>
        public Vector2 RespawnPosition { get { return HasCheckpoint ? CheckpointPosition : LastLandingPosition; } }
        public bool inputLocked;
        public bool IsDead { get; private set; }
        /// <summary>Seconds after a death before coming back at the checkpoint (set by the Checkpoint). 0 = only with R.</summary>
        [HideInInspector] public float checkpointRespawnDelay = 1f;
        float deadTimer;

        public event Action<WormMaterial> MaterialChanged;
        /// <summary>Raised only after the player successfully toggles form through ChangeMaterial().</summary>
        public event Action<WormMaterial> PlayerMaterialSwitched;
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
            GameJuice.Ensure();
            GameAudio.Ensure();
            LevelFlow.Ensure();
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
            if (IsDead) { UpdateDead(); return; }
            if (inputLocked) return;

            bool rightClickSwitch = GameInput.RightDown && !launcher.IsAiming && !launcher.CancelledThisFrame;
            if (GameInput.SwitchDown || rightClickSwitch) ChangeMaterial();

            if (transform.position.y < respawnBelowY) Kill("fell out of the world");
        }

        // ------------------------------------------------------------------ material

        /// <summary>Squishy &lt;-&gt; Heavy. Works ANYTIME, including airborne. Preserves momentum (see WormPhysics.SetMaterial).</summary>
        public bool ChangeMaterial()
        {
            WormMaterial target = physics.Material == WormMaterial.Squishy ? WormMaterial.Heavy : WormMaterial.Squishy;
            if (!SetMaterial(target)) return false;
            if (PlayerMaterialSwitched != null) PlayerMaterialSwitched(target);
            return true;
        }

        public bool SetMaterial(WormMaterial m)
        {
            if (IsDead || inputLocked) return false;
            if (m == physics.Material) return false;
            physics.SetMaterial(m);
            GameAudio.Play(m == WormMaterial.Heavy ? "switch_heavy" : "switch_squishy", 0.6f);
            if (MaterialChanged != null) MaterialChanged(m);
            return true;
        }

        // ------------------------------------------------------------------ launching

        public bool PerformLaunch(Vector2 direction, float power01, float forceMultiplier)
        {
            if (IsDead || !CanLaunch) return false;            // no mid-air relaunch, no launch while dead
            physics.Launch(direction, power01, forceMultiplier);
            counter.Increment();
            GameAudio.Play("launch", Mathf.Lerp(0.45f, 0.9f, power01), Mathf.Lerp(0.9f, 1.15f, power01));
            if (Launched != null) Launched(transform.position, power01);
            return true;
        }

        // ------------------------------------------------------------------ respawn / reset

        /// <summary>
        /// Death: plays the Death anim and blocks input. After LevelSettings Death Delay the worm comes back at the
        /// checkpoint if one was reached, otherwise the whole level restarts from the start (LevelFlow).
        /// </summary>
        public void Die(string reason)
        {
            if (IsDead || LevelFlow.IsComplete) return;
            IsDead = true;
            deadTimer = 0f;
            GameAudio.Play("death", 0.8f);
            GameJuice.Shake(0.12f);
            if (Died != null) Died(reason);
        }

        /// <summary>Same as Die (kept for older hazards: rollers, falling out of the world).</summary>
        public void Kill(string reason)
        {
            Die(reason);
        }

        void UpdateDead()
        {
            LevelSettings s = LevelSettings.Current;
            deadTimer += Time.deltaTime;
            if (HasCheckpoint)
            {
                if (checkpointRespawnDelay > 0f && deadTimer >= checkpointRespawnDelay) RespawnAtLastLanding();
                return;
            }
            float delay = s != null ? s.deathDelay : 0.9f;
            if (deadTimer < delay) return;
            if (s == null || s.restartLevelOnDeath) { deadTimer = float.MinValue; LevelFlow.RestartLevel(); } // runs once
            else RespawnAtLastLanding();
        }

        /// <summary>R key. Dead: back to the checkpoint (or the last landing if none). Alive: back to the last landing.</summary>
        public void RespawnAtLastLanding()
        {
            physics.Teleport(IsDead ? RespawnPosition : LastLandingPosition);
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
            HasCheckpoint = false;
            counter.ResetCount();
            if (Revived != null) Revived();
            if (MaterialChanged != null) MaterialChanged(material);
        }

        /// <summary>Checkpoints call this: deaths now send the worm back here.</summary>
        public void SetCheckpoint(Vector2 position)
        {
            HasCheckpoint = true;
            CheckpointPosition = position;
        }

        public void ClearCheckpoint() { HasCheckpoint = false; }

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
