using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace ProjectJuice
{
    /// <summary>
    /// Adaptive music for the worm's two forms. Add this to one GameObject in a
    /// gameplay scene and assign the clips in the Inspector.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music tracks")]
        [SerializeField] AudioClip squishyMusic;
        [SerializeField] AudioClip heavyMusic;

        [Header("Music settings")]
        [Min(0f)] [SerializeField] float formChangeDelay = 1.5f;
        [Min(0f)] [SerializeField] float crossfadeDuration = 1f;
        [Range(0f, 1f)] [SerializeField] float squishyVolume = 1f;
        [Range(0f, 1f)] [SerializeField] float heavyVolume = 1f;
        [SerializeField] AudioMixerGroup musicMixerGroup;

        [Header("SFX - Launch")]
        [SerializeField] AudioClip launchSfx;
        [Range(0f, 1f)] [SerializeField] float launchSfxVolume = 0.8f;

        [Header("SFX - Gameplay clips")]
        [SerializeField] AudioClip[] squishyImpactSfx;
        [SerializeField] AudioClip[] heavyImpactSfx;
        [HideInInspector] [SerializeField] AudioClip[] landingSfx;
        [Tooltip("Played when the worm dies while Squishy. Leave empty until a suitable clip is chosen.")]
        [SerializeField] AudioClip[] squishyDeathSfx;
        [Tooltip("Played when the worm dies while Heavy. Leave empty until a suitable clip is chosen.")]
        [SerializeField] AudioClip[] heavyDeathSfx;
        [Tooltip("Legacy fallback used only when the material-specific death list is empty.")]
        [HideInInspector] [SerializeField] AudioClip[] deathSfx;
        [HideInInspector] [SerializeField] AudioClip[] respawnSfx;
        [Tooltip("Played only for a successful player-initiated form toggle. Leave empty until a switch sound is chosen.")]
        [SerializeField] AudioClip materialSwitchSfx;
        [SerializeField] AudioClip[] rockDestructionSfx;

        [Header("SFX - Gameplay volumes")]
        [Range(0f, 1f)] [SerializeField] float squishyImpactVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] float heavyImpactVolume = 0.85f;
        [HideInInspector] [Range(0f, 1f)] [SerializeField] float landingVolume = 0.65f;
        [Range(0f, 1f)] [SerializeField] float squishyDeathVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] float heavyDeathVolume = 0.85f;
        [HideInInspector] [Range(0f, 1f)] [SerializeField] float deathVolume = 0.85f;
        [HideInInspector] [Range(0f, 1f)] [SerializeField] float respawnVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] float materialSwitchVolume = 0.7f;
        [Range(0f, 1f)] [SerializeField] float rockDestructionVolume = 0.9f;

        [Header("SFX - Impact filtering")]
        [Tooltip("Impacts below this incoming normal speed do not make a sound.")]
        [Min(0f)] [SerializeField] float minimumImpactSpeed = 1.2f;
        [Tooltip("Minimum volume multiplier at the impact threshold.")]
        [Range(0f, 1f)] [SerializeField] float minimumImpactVolumeScale = 0.35f;
        [Tooltip("Impact speed that reaches full event volume.")]
        [Min(0.01f)] [SerializeField] float fullVolumeImpactSpeed = 12f;
        [Tooltip("Prevents rapid repeated collision sounds.")]
        [Min(0f)] [SerializeField] float impactCooldown = 0.08f;
        [Tooltip("Suppresses a landing cue shortly after its matching collision cue.")]
        [HideInInspector] [Min(0f)] [SerializeField] float landingImpactDeduplicationWindow = 0.35f;
        [Tooltip("Ignores contact-enter callbacks immediately caused by a form's collider/body update.")]
        [Min(0f)] [SerializeField] float materialChangeImpactSuppression = 0.08f;

        [Header("SFX - Variation")]
        [Tooltip("Random pitch offset applied to each gameplay SFX playback.")]
        [Range(0f, 0.25f)] [SerializeField] float sfxPitchVariation = 0.04f;
        [Tooltip("Random pitch offset used only by Rock Destruction SFX.")]
        [Range(0f, 0.25f)] [SerializeField] float rockDestructionPitchVariation = 0.05f;
        [SerializeField] AudioMixerGroup sfxMixerGroup;

        AudioSource squishySource;
        AudioSource heavySource;
        AudioSource sfxSource;
        WormController subscribedController;
        WormMaterial desiredMaterial;
        WormMaterial activeMaterial;
        bool crossfadeInProgress;
        Coroutine transitionRoutine;
        float lastImpactSoundTime = float.NegativeInfinity;
        float ignoreImpactsUntil = float.NegativeInfinity;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateSources();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            BindToCurrentController();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindController();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void CreateSources()
        {
            squishySource = CreateSource("Music - Squishy", squishyMusic);
            heavySource = CreateSource("Music - Heavy", heavyMusic);
            sfxSource = CreateSource("SFX", null);
            sfxSource.loop = false;
            // Music sources are faded by changing their AudioSource volume. SFX
            // uses PlayOneShot volume instead, so its source must stay audible.
            sfxSource.volume = 1f;
            sfxSource.outputAudioMixerGroup = sfxMixerGroup;
        }

        AudioSource CreateSource(string sourceName, AudioClip clip)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.name = sourceName;
            source.playOnAwake = false;
            source.loop = true;
            source.clip = clip;
            source.outputAudioMixerGroup = musicMixerGroup;
            source.volume = 0f;
            return source;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindToCurrentController();
        }

        void BindToCurrentController()
        {
            WormController controller = WormController.Instance;
            if (controller == null) controller = FindObjectOfType<WormController>();
            if (controller == subscribedController) return;

            UnbindController();
            if (controller == null) return;

            subscribedController = controller;
            subscribedController.MaterialChanged += OnMaterialChanged;
            subscribedController.PlayerMaterialSwitched += OnPlayerMaterialSwitched;
            subscribedController.Launched += OnLaunched;
            subscribedController.Impacted += OnImpacted;
            subscribedController.Died += OnDied;
            desiredMaterial = subscribedController.Material;
            SetImmediate(desiredMaterial);
        }

        void UnbindController()
        {
            if (subscribedController != null)
            {
                subscribedController.MaterialChanged -= OnMaterialChanged;
                subscribedController.PlayerMaterialSwitched -= OnPlayerMaterialSwitched;
                subscribedController.Launched -= OnLaunched;
                subscribedController.Impacted -= OnImpacted;
                subscribedController.Died -= OnDied;
            }
            subscribedController = null;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }
        }

        void OnLaunched(Vector2 position, float power01)
        {
            PlayClip(launchSfx, launchSfxVolume, false);
        }

        [ContextMenu("Test Launch SFX")]
        void TestLaunchSfx()
        {
            PlayClip(launchSfx, launchSfxVolume, false);
        }

        void OnImpacted(ImpactInfo info)
        {
            // WormPhysics marks the one collision that successfully broke a
            // BreakableObject. Give that event its own sound instead of also
            // playing the material-impact cue.
            if (info.brokeObject)
            {
                if (PlayRandomClip(rockDestructionSfx, rockDestructionVolume, rockDestructionPitchVariation))
                    lastImpactSoundTime = Time.time;
                return;
            }

            if (Time.time < ignoreImpactsUntil || info.normalSpeed < minimumImpactSpeed || Time.time < lastImpactSoundTime + impactCooldown) return;

            AudioClip[] clips = info.material == WormMaterial.Heavy ? heavyImpactSfx : squishyImpactSfx;
            float baseVolume = info.material == WormMaterial.Heavy ? heavyImpactVolume : squishyImpactVolume;
            float range = Mathf.Max(0.01f, fullVolumeImpactSpeed - minimumImpactSpeed);
            float intensity01 = Mathf.Clamp01((info.normalSpeed - minimumImpactSpeed) / range);
            float volumeScale = Mathf.Lerp(minimumImpactVolumeScale, 1f, intensity01);
            if (PlayRandomClip(clips, baseVolume * volumeScale)) lastImpactSoundTime = Time.time;
        }

        void OnDied(string reason)
        {
            // Read the material from the controller at the instant Died fires;
            // this preserves the form that caused the death event.
            WormMaterial material = subscribedController != null
                ? subscribedController.Material
                : WormMaterial.Squishy;
            AudioClip[] clips = material == WormMaterial.Heavy ? heavyDeathSfx : squishyDeathSfx;
            float volume = material == WormMaterial.Heavy ? heavyDeathVolume : squishyDeathVolume;
            PlayRandomClip(clips, volume);
        }

        bool PlayRandomClip(AudioClip[] clips, float volume)
        {
            return PlayRandomClip(clips, volume, sfxPitchVariation);
        }

        bool PlayRandomClip(AudioClip[] clips, float volume, float pitchVariation)
        {
            if (clips == null || clips.Length == 0) return false;
            int start = Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(start + i) % clips.Length];
                if (clip == null) continue;
                PlayClip(clip, volume, pitchVariation);
                return true;
            }
            return false;
        }

        void PlayClip(AudioClip clip, float volume, bool varyPitch)
        {
            PlayClip(clip, volume, varyPitch ? sfxPitchVariation : 0f);
        }

        void PlayClip(AudioClip clip, float volume, float pitchVariation)
        {
            if (sfxSource == null || clip == null) return;
            // PlayOneShot uses this source's volume as well, so keep it explicitly audible.
            sfxSource.volume = 1f;
            sfxSource.pitch = pitchVariation > 0f ? Random.Range(1f - pitchVariation, 1f + pitchVariation) : 1f;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        [ContextMenu("Test Squishy Impact SFX")]
        void TestSquishyImpactSfx() { PlayRandomClip(squishyImpactSfx, squishyImpactVolume); }

        [ContextMenu("Test Heavy Impact SFX")]
        void TestHeavyImpactSfx() { PlayRandomClip(heavyImpactSfx, heavyImpactVolume); }

        [ContextMenu("Test Material Switch SFX")]
        void TestMaterialSwitchSfx() { PlayClip(materialSwitchSfx, materialSwitchVolume, true); }

        [ContextMenu("Test Rock Destruction SFX")]
        void TestRockDestructionSfx() { PlayRandomClip(rockDestructionSfx, rockDestructionVolume, rockDestructionPitchVariation); }

        void OnMaterialChanged(WormMaterial material)
        {
            if (material != WormMaterial.Squishy && material != WormMaterial.Heavy) return;
            // Changing a collider/body while in contact can generate an enter
            // callback on the next physics step. It is not a player impact.
            ignoreImpactsUntil = Time.time + materialChangeImpactSuppression;
            desiredMaterial = material;

            // A switch back during the delay cancels the request completely.
            // During a fade, let the single routine reverse smoothly instead.
            if (!crossfadeInProgress && material == activeMaterial && transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
                return;
            }
            if (transitionRoutine == null)
                transitionRoutine = StartCoroutine(TransitionMusic());
        }

        void OnPlayerMaterialSwitched(WormMaterial material)
        {
            PlayClip(materialSwitchSfx, materialSwitchVolume, true);
        }

        void SetImmediate(WormMaterial material)
        {
            AudioSource selected = SourceFor(material);
            AudioSource other = OtherSourceFor(material);
            EnsurePlaying(selected);
            EnsurePlaying(other);
            selected.volume = VolumeFor(material);
            other.volume = 0f;
            activeMaterial = material;
            crossfadeInProgress = false;
        }

        IEnumerator TransitionMusic()
        {
            WormMaterial requested = desiredMaterial;
            float elapsed = 0f;
            while (elapsed < formChangeDelay)
            {
                if (desiredMaterial != requested)
                {
                    requested = desiredMaterial;
                    elapsed = 0f;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            while (true)
            {
                requested = desiredMaterial;
                AudioSource target = SourceFor(requested);
                AudioSource other = OtherSourceFor(requested);
                EnsurePlaying(target);
                EnsurePlaying(other);

                float startTarget = target.volume;
                float startOther = other.volume;
                elapsed = 0f;
                crossfadeInProgress = true;
                while (elapsed < crossfadeDuration)
                {
                    if (desiredMaterial != requested) break;
                    elapsed += Time.unscaledDeltaTime;
                    float t = crossfadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / crossfadeDuration);
                    target.volume = Mathf.Lerp(startTarget, VolumeFor(requested), t);
                    other.volume = Mathf.Lerp(startOther, 0f, t);
                    yield return null;
                }

                if (desiredMaterial == requested)
                {
                    target.volume = VolumeFor(requested);
                    other.volume = 0f;
                    activeMaterial = requested;
                    crossfadeInProgress = false;
                    transitionRoutine = null;
                    yield break;
                }
                // A new request arrived during the fade. Keep the current levels
                // and reverse/continue from them without restarting either clip.
            }
        }

        AudioSource SourceFor(WormMaterial material)
        {
            return material == WormMaterial.Squishy ? squishySource : heavySource;
        }

        AudioSource OtherSourceFor(WormMaterial material)
        {
            return material == WormMaterial.Squishy ? heavySource : squishySource;
        }

        float VolumeFor(WormMaterial material)
        {
            return material == WormMaterial.Squishy ? squishyVolume : heavyVolume;
        }

        static void EnsurePlaying(AudioSource source)
        {
            if (source != null && source.clip != null && !source.isPlaying) source.Play();
        }
    }
}
