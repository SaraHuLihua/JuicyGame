using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Per-level settings (one per gameplay scene, on the Systems object):
    ///  - launch power and gravity per material, on top of the Worm Material Profiles, so each level can be tuned
    ///    without touching the shared profiles (1 = as in the profile);
    ///  - what happens on death and at the end of the level (LevelFlow);
    ///  - texts and fonts of the jump counter and of the "level complete" screen.
    /// Without one, everything uses its default.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public class LevelSettings : MonoBehaviour
    {
        public static LevelSettings Current { get; private set; }

        [Header("Launch tuning for this level")]
        [Tooltip("Multiplies Squishy's launch speed (min and max). 1.1 = 10% stronger shots.")]
        [Min(0.1f)] public float squishyLaunch = 1f;
        [Tooltip("Multiplies Rocky's launch speed (min and max).")]
        [Min(0.1f)] public float heavyLaunch = 1f;
        [Tooltip("Multiplies Squishy's gravity. Higher = faster, snappier hops (raise the launch too, or it reaches less far).")]
        [Min(0.1f)] public float squishyGravity = 1f;
        [Tooltip("Multiplies Rocky's gravity.")]
        [Min(0.1f)] public float heavyGravity = 1f;

        [Header("Death")]
        [Tooltip("On = dying without a checkpoint restarts the level from the start (broken things, chains and the jump counter too). Off = back to the last landing.")]
        public bool restartLevelOnDeath = true;
        [Tooltip("Seconds of the death animation before the restart or the respawn at the checkpoint.")]
        [Min(0f)] public float deathDelay = 0.9f;

        [Header("Level complete")]
        [Tooltip("Scene loaded by the Main Menu button (must be in File > Build Profiles).")]
        public string mainMenuScene = "MainMenu";
        public string completeTitle = "You did it!";
        [Tooltip("{0} = number of jumps.")]
        public string jumpsText = "Jumps: {0}";
        [Tooltip("{0} = fewest jumps ever on this level (saved on this computer).")]
        public string bestText = "Best: {0}";
        public string newBestText = "New best!";
        public string retryText = "Retry";
        public string menuText = "Main Menu";

        [Header("Jump counter (top left)")]
        public bool showJumpCounter = true;
        [Tooltip("{0} = number of jumps.")]
        public string counterText = "Jumps {0}";

        [Header("Fonts (empty = TextMeshPro default)")]
        [Tooltip("Big title (DarumadropOne-Regular SDF, like the main menu title).")]
        public TMPro.TMP_FontAsset titleFont;
        [Tooltip("Counter and buttons (PaulaNotes-Regular SDF, like 'Click anywhere to play').")]
        public TMPro.TMP_FontAsset textFont;

        void Awake()
        {
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public static float LaunchMultiplier(WormMaterial m)
        {
            LevelSettings s = Current;
            if (s == null) return 1f;
            return m == WormMaterial.Squishy ? s.squishyLaunch : s.heavyLaunch;
        }

        public static float GravityMultiplier(WormMaterial m)
        {
            LevelSettings s = Current;
            if (s == null) return 1f;
            return m == WormMaterial.Squishy ? s.squishyGravity : s.heavyGravity;
        }
    }
}
