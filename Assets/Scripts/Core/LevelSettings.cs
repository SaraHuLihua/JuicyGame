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

        [Header("Controls tutorial (bottom left)")]
        [Tooltip("Shows the mouse = launch and Space = switch icons. The material you are NOT is darker; TutorialZones in the level make the icon you need pulse.")]
        public bool showControls = true;
        [Tooltip("Launch circle (Untitled_Artwork 13 'butto').")]
        public Sprite launchIcon;
        [Tooltip("Rocky circle (Untitled_Artwork 13_1).")]
        public Sprite heavyIcon;
        [Tooltip("Squishy circle (Untitled_Artwork 13_2).")]
        public Sprite squishyIcon;
        [Tooltip("Mouse (Untitled_Artwork 13_6).")]
        public Sprite mouseIcon;
        [Tooltip("The three click lines around the mouse (Untitled_Artwork 13_3, _4, _5), shown while aiming.")]
        public Sprite[] clickLines;
        [Tooltip("Space bar (Untitled_Artwork 13_7).")]
        public Sprite spaceIcon;
        [Tooltip("Size of the icons (1 = as drawn).")]
        [Range(0.3f, 2f)] public float controlsScale = 0.8f;

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
    }
}
