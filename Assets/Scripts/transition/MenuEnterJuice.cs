using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectJuice
{
    /// <summary>
    /// Main menu: while the menu image zooms in, it feels like entering the machine. The engine starts up
    /// (sound), two big chalk gears slide in from the sides and spin faster and faster, chalk rings rush out of
    /// the centre like the walls of a tunnel, dust streaks past, the edges close in, and a puff of steam
    /// covers the cut to the level. Everything is drawn on its own overlay, under the menu's fade.
    /// ButtonTransitionScript calls Play when the button is clicked.
    /// </summary>
    public class MenuEnterJuice : MonoBehaviour
    {
        [Tooltip("Seconds of the effect (it keeps going under the fade until the level loads).")]
        public float duration = 2.6f;
        [Tooltip("Sorting order of the overlay. Keep it under the fade canvas.")]
        public int sortingOrder = 8;
        public Color chalk = new Color(1f, 1f, 1f, 0.85f);
        [Range(0f, 1f)] public float vignetteStrength = 0.85f;
        [Tooltip("Sound at the start of the transition. Empty = silent.")]
        public AudioClip startClip;
        [Range(0f, 1f)] public float startVolume = 0.9f;
        public int rings = 7;
        public int dust = 40;

        static Sprite ringSprite, gearSprite, dotSprite, vignetteSprite;
        bool playing;

        public void Play()
        {
            if (playing) return;
            playing = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (startClip != null)
            {
                AudioSource a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.PlayOneShot(startClip, startVolume);
            }

            GameObject go = new GameObject("Enter The Machine");
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortingOrder;
            CanvasScaler cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            RectTransform root = (RectTransform)go.transform;

            Image vig = Make(root, "Vignette", Vignette(), Vector2.zero, new Vector2(1920f, 1080f) * 1.6f);
            Image gearL = Make(root, "Gear L", Gear(), new Vector2(-1500f, -150f), new Vector2(900f, 900f));
            Image gearR = Make(root, "Gear R", Gear(), new Vector2(1500f, 260f), new Vector2(700f, 700f));
            Image[] ringImgs = new Image[rings];
            for (int i = 0; i < rings; i++) ringImgs[i] = Make(root, "Ring", Ring(), Vector2.zero, new Vector2(200f, 200f));
            Image[] dustImgs = new Image[dust];
            Vector2[] dustDir = new Vector2[dust];
            float[] dustPhase = new float[dust];
            for (int i = 0; i < dust; i++)
            {
                dustImgs[i] = Make(root, "Dust", Dot(), Vector2.zero, new Vector2(10f, 10f));
                float a = Random.Range(0f, Mathf.PI * 2f);
                dustDir[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                dustPhase[i] = Random.value;
            }
            Image puff = Make(root, "Steam", Dot(), Vector2.zero, new Vector2(400f, 400f));

            float gearAngle = 0f, ringPhase = 0f;
            for (float t = 0f; ; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / duration);
                float speed = 0.3f + 3.5f * k * k;                          // everything accelerates: the engine spins up
                float shake = 6f * k;
                Vector2 jolt = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));

                // Gears slide in from the sides and spin faster and faster.
                float slide = Ease(Mathf.Clamp01(t / 0.7f));
                gearAngle += Time.unscaledDeltaTime * 60f * speed;
                gearL.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-1500f, -150f), new Vector2(-900f, -150f), slide) + jolt;
                gearR.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(1500f, 260f), new Vector2(930f, 260f), slide) + jolt;
                gearL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, gearAngle);
                gearR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -gearAngle * 1.28f + 8f);
                SetAlpha(gearL, 0.55f * slide); SetAlpha(gearR, 0.55f * slide);

                // Tunnel rings rush out of the centre.
                ringPhase += Time.unscaledDeltaTime * 0.45f * speed;
                for (int i = 0; i < rings; i++)
                {
                    float p = Mathf.Repeat(ringPhase + i / (float)rings, 1f);
                    float s = Mathf.Lerp(0.05f, 14f, p * p * p);
                    ringImgs[i].rectTransform.localScale = Vector3.one * s;
                    ringImgs[i].rectTransform.anchoredPosition = jolt * 0.5f;
                    SetAlpha(ringImgs[i], Mathf.Clamp01(t * 2f) * Mathf.Sin(p * Mathf.PI) * 0.8f);
                }

                // Dust streaks past, stretched along their way out.
                for (int i = 0; i < dust; i++)
                {
                    float p = Mathf.Repeat(dustPhase[i] + t * 0.35f * speed, 1f);
                    float dist = Mathf.Lerp(30f, 1300f, p * p);
                    RectTransform r = dustImgs[i].rectTransform;
                    r.anchoredPosition = dustDir[i] * dist;
                    r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dustDir[i].y, dustDir[i].x) * Mathf.Rad2Deg);
                    r.localScale = new Vector3(1f + 6f * p * speed * 0.4f, 1f, 1f) * (0.6f + p);
                    SetAlpha(dustImgs[i], Mathf.Clamp01(t * 2f) * Mathf.Sin(p * Mathf.PI) * 0.9f);
                }

                // The edges close in like going into a pipe.
                vig.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.75f, k);
                SetAlpha(vig, vignetteStrength * Mathf.Clamp01(t * 1.5f));

                // A puff of steam at the end covers the cut.
                float pk = Mathf.Clamp01((t - duration * 0.75f) / (duration * 0.25f));
                puff.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 7f, Ease(pk));
                SetAlpha(puff, pk > 0f ? Mathf.Sin(pk * Mathf.PI) * 0.7f : 0f);
                yield return null;
            }
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        Image Make(RectTransform parent, string n, Sprite s, Vector2 pos, Vector2 size)
        {
            GameObject g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            RectTransform r = (RectTransform)g.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            Image im = g.AddComponent<Image>();
            im.sprite = s;
            im.raycastTarget = false;
            im.color = new Color(chalk.r, chalk.g, chalk.b, 0f);
            if (n == "Vignette") im.color = new Color(0f, 0f, 0f, 0f);
            return im;
        }

        void SetAlpha(Image im, float a)
        {
            Color c = im.color;
            c.a = a * (im.name == "Vignette" ? 1f : chalk.a);
            im.color = c;
        }

        // ------------------------------------------------------------------ procedural chalk sprites

        delegate float Shape(float x, float y, float r, float ang);

        static Sprite Draw(int n, Shape alpha)
        {
            Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 2f - 1f, fy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(fx * fx + fy * fy), ang = Mathf.Atan2(fy, fx);
                    float grain = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.35f, y * 0.35f);   // chalk grain
                    px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(fx, fy, r, ang)) * grain);
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        static float Line(float d, float width) { return Mathf.Clamp01(1f - Mathf.Abs(d) / width); }

        static Sprite Ring() { if (ringSprite == null) ringSprite = Draw(256, (x, y, r, a) => Line(r - 0.9f + 0.015f * Mathf.Sin(a * 7f), 0.035f)); return ringSprite; }
        static Sprite Dot() { if (dotSprite == null) dotSprite = Draw(64, (x, y, r, a) => 1f - r * r); return dotSprite; }
        static Sprite Vignette()
        {
            // Dark everywhere except a soft hole in the middle.
            if (vignetteSprite == null)
            {
                vignetteSprite = Draw(256, (x, y, r, a) => Mathf.SmoothStep(0f, 1f, (r - 0.35f) / 0.5f));
            }
            return vignetteSprite;
        }
        static Sprite Gear()
        {
            if (gearSprite == null)
                gearSprite = Draw(512, (x, y, r, a) =>
                {
                    float teeth = Mathf.Clamp01(Mathf.Sin(a * 14f) * 3f) * 0.08f;            // 14 square-ish teeth
                    float rimOuter = 0.86f + teeth;
                    float outer = Mathf.Clamp01((rimOuter - r) / 0.012f) * Mathf.Clamp01((r - 0.76f) / 0.012f); // filled rim with teeth
                    float inner = Line(r - 0.5f, 0.02f);
                    float hub = Line(r - 0.14f, 0.02f);
                    float spokes = r > 0.16f && r < 0.5f ? Line(Mathf.Sin(a * 3f) * r, 0.025f) : 0f;
                    return Mathf.Max(Mathf.Max(outer, inner), Mathf.Max(hub, spokes));
                });
            return gearSprite;
        }
    }
}
