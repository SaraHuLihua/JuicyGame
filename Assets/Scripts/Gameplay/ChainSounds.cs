using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Metallic clinks for a BreakableChain. Audio only: it listens to the chain's LinkImpact / Broke
    /// events and never touches physics. Put it next to BreakableChain (the Chain prefab has one).
    ///
    /// Spam control: a hit must be faster than Min Impact Speed, the whole chain plays at most one
    /// sound per Min Interval and Max Per Second, and each link has its own cooldown. Volume follows
    /// impact speed. Assign your own clips in the arrays; while they are empty the chain is silent.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChainSounds : MonoBehaviour
    {
        [Header("Clips (empty = silent)")]
        [Tooltip("Played when links hit walls, floors, the worm... One is picked at random.")]
        public AudioClip[] impactClips;
        [Tooltip("Played once when the chain snaps.")]
        public AudioClip[] breakClips;

        [Header("Volume")]
        [Range(0f, 1f)] public float impactVolume = 0.55f;
        [Range(0f, 1f)] public float breakVolume = 0.9f;
        [Tooltip("Random pitch range (1 = original).")]
        public Vector2 pitchRange = new Vector2(0.9f, 1.15f);
        [Range(0f, 1f), Tooltip("0 = plain 2D sound, 1 = fully positional.")]
        public float spatialBlend = 0f;

        [Header("When to play")]
        [Tooltip("Impacts slower than this (relative speed, units/s) are silent: resting contacts never click.")]
        public float minImpactSpeed = 2f;
        [Tooltip("Impact speed that plays at full Impact Volume.")]
        public float fullVolumeSpeed = 12f;
        [Tooltip("Seconds between two impact sounds of this chain.")]
        public float minInterval = 0.07f;
        [Tooltip("Hard cap on impact sounds per second for this chain.")]
        public int maxPerSecond = 8;
        [Tooltip("Seconds before the SAME link can clink again.")]
        public float perLinkCooldown = 0.25f;

        BreakableChain chain;
        AudioSource[] voices;
        int nextVoice;
        float lastPlay = -10f;
        float windowStart;
        int windowCount;
        float[] linkLast = new float[0];

        void Awake()
        {
            chain = GetComponent<BreakableChain>();
            voices = new AudioSource[3]; // a few voices so overlapping clinks don't cut each other off
            for (int i = 0; i < voices.Length; i++)
            {
                AudioSource a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = spatialBlend;
                voices[i] = a;
            }
        }

        void OnEnable()
        {
            if (chain == null) chain = GetComponent<BreakableChain>();
            if (chain == null) return;
            chain.LinkImpact += OnLinkImpact;
            chain.Broke += OnBroke;
        }

        void OnDisable()
        {
            if (chain == null) return;
            chain.LinkImpact -= OnLinkImpact;
            chain.Broke -= OnBroke;
        }

        void OnLinkImpact(Vector2 point, float speed, int link)
        {
            if (speed < minImpactSpeed) return;
            float now = Time.time;
            if (now - lastPlay < minInterval) return;
            if (now - windowStart >= 1f) { windowStart = now; windowCount = 0; }
            if (windowCount >= maxPerSecond) return;
            if (link >= linkLast.Length) System.Array.Resize(ref linkLast, link + 1);
            if (linkLast[link] > 0f && now - linkLast[link] < perLinkCooldown) return;

            AudioClip clip = Pick(impactClips, false);
            if (clip == null) return;
            float t = Mathf.InverseLerp(minImpactSpeed, Mathf.Max(minImpactSpeed + 0.01f, fullVolumeSpeed), speed);
            Play(clip, point, impactVolume * Mathf.Lerp(0.25f, 1f, t));

            lastPlay = now;
            windowCount++;
            linkLast[link] = now;
        }

        void OnBroke(Vector2 point)
        {
            AudioClip clip = Pick(breakClips, true);
            if (clip != null) Play(clip, point, breakVolume);
            lastPlay = Time.time; // the snap covers the first clinks
        }

        void Play(AudioClip clip, Vector2 point, float volume)
        {
            AudioSource a = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            a.spatialBlend = spatialBlend;
            a.pitch = Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
            a.PlayOneShot(clip, volume);
        }

        AudioClip Pick(AudioClip[] set, bool snap)
        {
            if (set == null || set.Length == 0) return null;
            return set[Random.Range(0, set.Length)];
        }
    }
}
