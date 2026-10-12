using UnityEngine;
using UnityEngine.Events;

namespace ProjectJuice
{
    /// <summary>
    /// Checkpoint flag (prefab: Prefabs/Gameplay/Checkpoint, art in Art/Checkpoint).
    /// The worm touching it (any material) lights it up: from then on every death sends the worm back here
    /// instead of to its last landing. Touching another checkpoint moves the respawn there and turns this one off.
    /// After a death (hazard, crushed by a gear...) the worm comes back here by itself after Auto Respawn After
    /// seconds; R still works straight away. T (area reset) clears it.
    /// The trigger is the BoxCollider2D on this object; the flag is never solid.
    /// </summary>
    [DisallowMultipleComponent]
    public class Checkpoint : MonoBehaviour
    {
        public static Checkpoint Current { get; private set; }

        [Header("Respawn")]
        [Tooltip("Where the worm reappears. Empty = just above the base of the flag (Spawn Height).")]
        public Transform spawnPoint;
        [Tooltip("Height above this object's position used when Spawn Point is empty (units).")]
        public float spawnHeight = 0.6f;
        [Tooltip("Seconds after a death before the worm comes back here by itself. 0 = only with R.")]
        [Min(0f)] public float autoRespawnAfter = 1f;

        [Header("Look")]
        public SpriteRenderer flagRenderer;
        public Sprite offSprite;
        public Sprite onSprite;
        [Tooltip("Size of the 'pop' when it lights up (0.25 = 25% bigger for a moment).")]
        [Range(0f, 1f)] public float popScale = 0.3f;
        [Min(0.05f)] public float popTime = 0.35f;
        [Tooltip("Played when it lights up. Empty = silent.")]
        public BreakEffect activateEffect;
        public AudioClip activateSound;
        [Range(0f, 1f)] public float activateVolume = 0.7f;

        [Header("Events")]
        public UnityEvent onActivated;

        Vector3 baseScale;
        float popTimer = -1f;
        float deadTimer;
        AudioSource audioSource;

        public bool IsActive { get { return Current == this; } }
        public Vector2 SpawnPosition
        {
            get { return spawnPoint != null ? (Vector2)spawnPoint.position : (Vector2)(transform.position + transform.up * spawnHeight); }
        }

        void Awake()
        {
            baseScale = transform.localScale;
            if (flagRenderer == null) flagRenderer = GetComponent<SpriteRenderer>();
            Collider2D c = GetComponent<Collider2D>();
            if (c != null) c.isTrigger = true;
            if (activateSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            ShowLit(false);
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            transform.localScale = baseScale;
        }

        void OnTriggerEnter2D(Collider2D other) { TryActivate(other); }
        void OnTriggerStay2D(Collider2D other) { TryActivate(other); }

        void TryActivate(Collider2D other)
        {
            if (Current == this) return;
            WormController w = WormController.Instance;
            if (w == null || w.IsDead || w.Physics == null || other.attachedRigidbody != w.Physics.Body) return;
            Activate();
        }

        /// <summary>Light it up and make it the respawn point (also callable from scripts / UnityEvents).</summary>
        public void Activate()
        {
            WormController w = WormController.Instance;
            if (w == null) return;
            if (Current != null && Current != this) Current.ShowLit(false);
            Current = this;
            w.SetCheckpoint(SpawnPosition);
            ShowLit(true);
            popTimer = 0f;
            deadTimer = 0f;
            GameJuice.Shake(0.1f);
            if (activateEffect != null) activateEffect.Play(flagRenderer != null ? (Vector2)flagRenderer.bounds.center : (Vector2)transform.position, transform.up);
            if (audioSource != null) audioSource.PlayOneShot(activateSound, activateVolume);
            onActivated.Invoke();
        }

        void ShowLit(bool lit)
        {
            if (flagRenderer == null) return;
            Sprite s = lit ? onSprite : offSprite;
            if (s != null) flagRenderer.sprite = s;
        }

        void Update()
        {
            if (popTimer >= 0f)
            {
                popTimer += Time.deltaTime;
                float t = Mathf.Clamp01(popTimer / popTime);
                float k = 1f + popScale * Mathf.Sin(t * Mathf.PI) * (1f - 0.5f * t);
                transform.localScale = new Vector3(baseScale.x * (2f - k), baseScale.y * k, baseScale.z); // stretch up, thin out
                if (t >= 1f) { popTimer = -1f; transform.localScale = baseScale; }
            }

            if (Current != this) return;
            WormController w = WormController.Instance;
            if (w == null) return;
            if (!w.HasCheckpoint) // area reset (T): back to unlit
            {
                Current = null;
                ShowLit(false);
                return;
            }
            if (w.IsDead && autoRespawnAfter > 0f)
            {
                deadTimer += Time.deltaTime;
                if (deadTimer >= autoRespawnAfter) { deadTimer = 0f; w.RespawnAtLastLanding(); }
            }
            else deadTimer = 0f;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(SpawnPosition, 0.375f);
        }
#endif
    }
}
