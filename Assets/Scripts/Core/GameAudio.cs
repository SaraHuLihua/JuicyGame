using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Sound effects for the whole game, by name: GameAudio.Play("launch"), GameAudio.PlayAt("chain_snap", pos).
    /// The clips are the files in Assets/Resources/Audio (name = file name without extension). To change a sound,
    /// replace that file keeping its name, or drop a clip on the matching slot of a GameAudio component you add
    /// to the scene (that one is used instead of the auto-created one). Sources and licences: Assets/Resources/Audio/SOURCES.md.
    /// It also plays the worm's landing sounds itself (soft for Squishy, a thud for Rocky, louder for harder hits).
    /// Every sound gets a little random pitch so repeats never sound mechanical.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        [System.Serializable]
        public class Slot
        {
            public string name;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Tooltip("Random pitch change on every play (0.08 = up to 8% higher or lower).")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.08f;
        [Tooltip("Sounds farther than this from the camera are not played (units). Closer ones get quieter with distance.")]
        public float hearingDistance = 28f;
        [Tooltip("Clips set here replace the file of the same name in Resources/Audio.")]
        public List<Slot> overrides = new List<Slot>();

        [Header("Landing sounds")]
        [Tooltip("Speed into the surface under which a landing makes no sound (units/s).")]
        public float landFromSpeed = 4f;
        [Tooltip("Seconds between two landing sounds.")]
        public float landCooldown = 0.08f;

        const int Voices = 12;
        readonly AudioSource[] voices = new AudioSource[Voices];
        int nextVoice;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        WormController subscribed;
        float lastLand = -99f;

        /// <summary>Called by WormController: makes sure one GameAudio exists.</summary>
        public static void Ensure()
        {
            if (Instance != null) return;
#if UNITY_2023_1_OR_NEWER
            GameAudio existing = Object.FindFirstObjectByType<GameAudio>();
#else
            GameAudio existing = Object.FindObjectOfType<GameAudio>();
#endif
            if (existing != null) { Instance = existing; return; }
            new GameObject("GameAudio (auto)").AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            for (int i = 0; i < Voices; i++)
            {
                AudioSource a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                voices[i] = a;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unsubscribe();
        }

        void Update()
        {
            WormController w = WormController.Instance;
            if (w == subscribed) return;
            Unsubscribe();
            subscribed = w;
            if (w != null) w.Impacted += OnImpact;
        }

        void Unsubscribe()
        {
            if (subscribed != null) subscribed.Impacted -= OnImpact;
            subscribed = null;
        }

        void OnImpact(ImpactInfo hit)
        {
            if (hit.normalSpeed < landFromSpeed || Time.time - lastLand < landCooldown || subscribed == null) return;
            lastLand = Time.time;
            bool heavy = subscribed.Material == WormMaterial.Heavy;
            float v = Mathf.Clamp01((hit.normalSpeed - landFromSpeed) / 25f);
            Play(heavy ? "land_heavy" : "land_squishy", Mathf.Lerp(0.25f, heavy ? 0.85f : 0.7f, v), heavy ? Mathf.Lerp(1.05f, 0.9f, v) : Mathf.Lerp(1.1f, 0.95f, v));
        }

        // ------------------------------------------------------------------ API

        /// <summary>Play a sound by name (file in Resources/Audio). Volume 0..1, pitch 1 = as recorded.</summary>
        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            GameAudio a = Instance;
            if (a == null) { Ensure(); a = Instance; }
            if (a != null) a.PlayInternal(name, volume, pitch);
        }

        /// <summary>Same as Play, but quieter far from the camera and silent beyond Hearing Distance.</summary>
        public static void PlayAt(string name, Vector2 position, float volume = 1f, float pitch = 1f)
        {
            GameAudio a = Instance;
            if (a == null) { Ensure(); a = Instance; }
            if (a == null) return;
            Camera cam = Camera.main;
            if (cam != null)
            {
                float d = Vector2.Distance(cam.transform.position, position);
                if (d > a.hearingDistance) return;
                volume *= Mathf.Lerp(1f, 0.2f, d / a.hearingDistance);
            }
            a.PlayInternal(name, volume, pitch);
        }

        void PlayInternal(string name, float volume, float pitch)
        {
            float slotVolume;
            AudioClip clip = Clip(name, out slotVolume);
            if (clip == null || volume <= 0.001f) return;
            float last;
            if (lastPlayed.TryGetValue(name, out last) && Time.unscaledTime - last < 0.03f) return; // same sound twice in one frame
            lastPlayed[name] = Time.unscaledTime;
            AudioSource v = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            v.pitch = pitch * (1f + Random.Range(-pitchVariation, pitchVariation));
            v.PlayOneShot(clip, Mathf.Clamp01(volume * slotVolume * masterVolume));
        }

        AudioClip Clip(string name, out float volume)
        {
            volume = 1f;
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i] != null && overrides[i].clip != null && overrides[i].name == name) { volume = overrides[i].volume; return overrides[i].clip; }
            AudioClip c;
            if (!cache.TryGetValue(name, out c))
            {
                c = Resources.Load<AudioClip>("Audio/" + name);
                cache[name] = c;
            }
            return c;
        }
    }
}
