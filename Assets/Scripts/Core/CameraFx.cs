using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Screen effects on the Main Camera (built-in render pipeline, works in WebGL):
    ///   Bloom: bright parts of the image (white chalk, sparks, the worm's colours) glow softly.
    ///   Death: when the worm dies the world drains to red with dark red edges, and comes back on respawn.
    /// The UI (counter, controls, result screen) is drawn after, so it is never tinted or blurred.
    /// Shader: Assets/Shaders/CameraFx.shader (keep it assigned so it is included in builds).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class CameraFx : MonoBehaviour
    {
        public Shader shader;

        [Header("Bloom")]
        public bool bloom = true;
        [Tooltip("Brightness (0..1) from which a pixel glows. Lower = more of the image glows.")]
        [Range(0f, 1.5f)] public float threshold = 0.75f;
        [Tooltip("Soft edge around the threshold (0 = hard cut).")]
        [Range(0f, 1f)] public float softKnee = 0.5f;
        [Tooltip("How strong the glow is.")]
        [Range(0f, 3f)] public float intensity = 0.7f;
        [Tooltip("How far the glow spreads.")]
        [Range(1f, 7f)] public float radius = 4f;
        public Color tint = Color.white;

        [Header("Death (screen turns red)")]
        public bool deathTint = true;
        public Color deathColor = new Color(0.85f, 0.08f, 0.1f, 1f);
        [Tooltip("How much of the screen turns red (1 = all of it).")]
        [Range(0f, 1f)] public float deathStrength = 0.85f;
        [Tooltip("Dark red edges.")]
        [Range(0f, 1f)] public float deathVignette = 0.7f;
        [Tooltip("Seconds to turn red when the worm dies.")]
        [Min(0.01f)] public float deathFadeIn = 0.18f;
        [Tooltip("Seconds to get the colours back on respawn.")]
        [Min(0.01f)] public float deathFadeOut = 0.45f;

        Material mat;
        WormController subscribed;
        bool dead;
        float death; // 0..1
        const int MaxLevels = 8;
        readonly RenderTexture[] levels = new RenderTexture[MaxLevels];

        void OnDisable()
        {
            Unsubscribe();
            if (mat != null) DestroyImmediate(mat);
            mat = null;
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                WormController w = WormController.Instance;
                if (w != subscribed)
                {
                    Unsubscribe();
                    subscribed = w;
                    if (w != null) { w.Died += OnDied; w.Revived += OnRevived; dead = w.IsDead; }
                }
            }
            float target = dead && deathTint ? 1f : 0f;
            float speed = 1f / (target > death ? deathFadeIn : deathFadeOut);
            death = Mathf.MoveTowards(death, target, Time.unscaledDeltaTime * speed);
        }

        void Unsubscribe()
        {
            if (subscribed == null) return;
            subscribed.Died -= OnDied;
            subscribed.Revived -= OnRevived;
            subscribed = null;
        }

        void OnDied(string reason) { dead = true; }
        void OnRevived() { dead = false; }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (shader == null) shader = Shader.Find("Hidden/ProjectJuice/CameraFx");
            bool useBloom = bloom && intensity > 0f;
            float deathAmount = death * deathStrength;
            if (shader == null || !shader.isSupported || (!useBloom && deathAmount <= 0f))
            {
                Graphics.Blit(src, dst);
                return;
            }
            if (mat == null) { mat = new Material(shader); mat.hideFlags = HideFlags.HideAndDontSave; }

            float knee = Mathf.Max(threshold * softKnee, 1e-4f);
            mat.SetVector("_Threshold", new Vector4(threshold, threshold - knee, knee * 2f, 0.25f / knee));
            mat.SetFloat("_Intensity", useBloom ? intensity : 0f);
            mat.SetColor("_BloomTint", tint);
            mat.SetColor("_DeathColor", deathColor);
            mat.SetFloat("_DeathAmount", deathAmount);
            mat.SetFloat("_DeathVignette", deathVignette);

            RenderTexture bloomTex = null;
            if (useBloom)
            {
                // Bright pass at half size, then a chain of halvings; going back up, each level is added onto the next.
                int w = Mathf.Max(1, src.width / 2), h = Mathf.Max(1, src.height / 2);
                float logh = Mathf.Log(Mathf.Max(w, h), 2f) + radius - 8f;
                int count = Mathf.Clamp(Mathf.FloorToInt(logh), 1, MaxLevels);
                mat.SetFloat("_SampleScale", 0.5f + logh - Mathf.Floor(logh));

                RenderTextureFormat fmt = src.format;
                levels[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(src, levels[0], mat, 0);
                int made = 1;
                for (int i = 1; i < count; i++)
                {
                    w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
                    if (w < 2 || h < 2) break;
                    levels[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                    Graphics.Blit(levels[i - 1], levels[i], mat, 1);
                    made++;
                }
                for (int i = made - 2; i >= 0; i--) Graphics.Blit(levels[i + 1], levels[i], mat, 2);
                bloomTex = levels[0];
                for (int i = 1; i < made; i++) { RenderTexture.ReleaseTemporary(levels[i]); levels[i] = null; }
            }

            mat.SetTexture("_BloomTex", bloomTex != null ? (Texture)bloomTex : Texture2D.blackTexture);
            Graphics.Blit(src, dst, mat, 3);
            if (bloomTex != null) { RenderTexture.ReleaseTemporary(bloomTex); levels[0] = null; }
        }
    }
}
