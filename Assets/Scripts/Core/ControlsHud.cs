using UnityEngine;
using UnityEngine.UI;

namespace ProjectJuice
{
    /// <summary>
    /// The controls in the bottom-left corner, drawn with Untitled_Artwork 13 (sprites set on LevelSettings):
    ///  - the mouse above the launch circle: the mouse clicks and its lines show while you aim;
    ///    the launch circle goes dark while you cannot launch (in the air);
    ///  - the space bar above the Rocky circle, the Squishy circle next to it: the material you are now is
    ///    bright and big, the other one darker and smaller; Space pushes the key down and the new material pops.
    /// TutorialZones in the level call ShowHint: the icon the player needs pulses and the rest fades a little.
    /// Built by LevelFlow, nothing to place.
    /// </summary>
    public class ControlsHud : MonoBehaviour
    {
        static ControlsHud instance;
        static readonly float[] hintUntil = new float[4];

        class Icon
        {
            public RectTransform rect;
            public Image image;
            public Vector2 home;
            public float scale = 1f, pop, press, dim, hint, alpha = 1f;
        }

        Icon launch, heavy, squishy, mouse, space;
        Image[] lines;
        Vector2[] lineHome;
        WormMaterial shown;
        bool hasShown;
        float lineShow;

        /// <summary>Make the icon for this hint pulse (call it every frame while the hint is wanted).</summary>
        public static void ShowHint(TutorialHint h)
        {
            hintUntil[(int)h] = Time.unscaledTime + 0.2f;
        }

        public static void SetVisible(bool on)
        {
            if (instance != null) instance.gameObject.SetActive(on);
        }

        public static ControlsHud Build(Transform canvas, LevelSettings s)
        {
            if (s == null || !s.showControls || s.launchIcon == null) return null;
            GameObject go = new GameObject("Controls", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(40f, 30f);
            root.sizeDelta = new Vector2(10f, 10f);
            root.localScale = Vector3.one * s.controlsScale;
            ControlsHud h = go.AddComponent<ControlsHud>();
            h.Make(root, s);
            return h;
        }

        void Make(RectTransform root, LevelSettings s)
        {
            instance = this;
            // Layout in drawing pixels (circles are 138 px): launch circle, then Rocky and Squishy in a row,
            // mouse above the launch circle, space bar above Rocky.
            launch = MakeIcon(root, "Launch", s.launchIcon, new Vector2(75f, 75f));
            heavy = MakeIcon(root, "Rocky", s.heavyIcon, new Vector2(255f, 75f));
            squishy = MakeIcon(root, "Squishy", s.squishyIcon, new Vector2(405f, 75f));
            mouse = MakeIcon(root, "Mouse", s.mouseIcon, new Vector2(75f, 210f));
            space = MakeIcon(root, "Space", s.spaceIcon, new Vector2(255f, 185f));

            // The click lines keep their place around the mouse as drawn (mouse centre 512.5,73 in the drawing).
            Vector2[] drawn = { new Vector2(-30.5f, 47.5f), new Vector2(-15.5f, 55f), new Vector2(-41f, 35f) };
            int n = s.clickLines != null ? s.clickLines.Length : 0;
            lines = new Image[n];
            lineHome = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                if (s.clickLines[i] == null) continue;
                Icon l = MakeIcon(root, "Click Line " + i, s.clickLines[i], mouse.home + (i < drawn.Length ? drawn[i] : Vector2.zero));
                lines[i] = l.image;
                lineHome[i] = l.home;
            }
        }

        static Icon MakeIcon(RectTransform parent, string name, Sprite sprite, Vector2 pos)
        {
            Icon ic = new Icon();
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ic.rect = (RectTransform)go.transform;
            ic.rect.anchorMin = ic.rect.anchorMax = Vector2.zero;
            ic.rect.pivot = new Vector2(0.5f, 0.5f);
            ic.rect.anchoredPosition = pos;
            ic.home = pos;
            ic.image = go.AddComponent<Image>();
            ic.image.raycastTarget = false;
            if (sprite != null)
            {
                ic.image.sprite = sprite;
                ic.rect.sizeDelta = sprite.rect.size;
            }
            else ic.image.enabled = false;
            return ic;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        static bool Hinting(TutorialHint h) { return Time.unscaledTime < hintUntil[(int)h]; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            WormController w = WormController.Instance;
            if (w == null || w.Physics == null) return;

            WormMaterial m = w.Material;
            if (!hasShown) { shown = m; hasShown = true; }
            if (m != shown)
            {
                shown = m;
                (m == WormMaterial.Heavy ? heavy : squishy).pop = 1f;
                space.press = 1f;
            }
            if (GameInput.SwitchDown) space.press = 1f;

            bool aiming = w.Launcher != null && w.Launcher.IsAiming;
            bool canLaunch = w.CanLaunch;

            bool hLaunch = Hinting(TutorialHint.Launch);
            bool hSwitch = Hinting(TutorialHint.Switch);
            bool hHeavy = Hinting(TutorialHint.BeHeavy);
            bool hSquishy = Hinting(TutorialHint.BeSquishy);
            bool anyHint = hLaunch || hSwitch || hHeavy || hSquishy;

            // Material circles: the one you are is bright and full size, the other darker and smaller.
            Target(heavy, m == WormMaterial.Heavy ? 1f : 0.78f, m == WormMaterial.Heavy ? 0f : 0.55f, hSwitch || hHeavy, anyHint, dt);
            Target(squishy, m == WormMaterial.Squishy ? 1f : 0.78f, m == WormMaterial.Squishy ? 0f : 0.55f, hSwitch || hSquishy, anyHint, dt);
            Target(space, 1f, 0f, hSwitch || hHeavy || hSquishy, anyHint, dt);
            Target(launch, aiming ? 1.1f : 1f, canLaunch || aiming ? 0f : 0.55f, hLaunch, anyHint, dt);
            Target(mouse, aiming ? 0.92f : 1f, 0f, hLaunch, anyHint, dt);
            if (aiming) mouse.press = Mathf.Max(mouse.press, 0.6f);

            Apply(heavy, time, dt, 0f);
            Apply(squishy, time, dt, 0.4f);
            Apply(space, time, dt, 0.8f);
            Apply(launch, time, dt, 1.2f);
            Apply(mouse, time, dt, 1.6f);

            // Click lines: shown while aiming, blinking during the launch hint.
            float lineTarget = aiming ? 1f : (hLaunch ? (Mathf.Sin(time * 8f) > 0f ? 1f : 0f) : 0f);
            lineShow = Mathf.MoveTowards(lineShow, lineTarget, dt * 10f);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) continue;
                Color c = Color.white;
                c.a = lineShow * mouse.alpha;
                lines[i].color = c;
                Vector2 jitter = aiming ? new Vector2(Mathf.Sin(time * 31f + i), Mathf.Cos(time * 27f + i * 2f)) * 1.5f : Vector2.zero;
                lines[i].rectTransform.anchoredPosition = lineHome[i] + (mouse.rect.anchoredPosition - mouse.home) + jitter;
                lines[i].rectTransform.localScale = Vector3.one * (0.7f + 0.3f * lineShow);
            }
        }

        static void Target(Icon ic, float scale, float dim, bool hinted, bool anyHint, float dt)
        {
            ic.scale = Mathf.Lerp(ic.scale, scale, 1f - Mathf.Exp(-dt * 14f));
            ic.dim = Mathf.Lerp(ic.dim, dim, 1f - Mathf.Exp(-dt * 10f));
            ic.hint = Mathf.MoveTowards(ic.hint, hinted ? 1f : 0f, dt * 4f);
            float alpha = anyHint && !hinted ? 0.55f : 1f;
            ic.alpha = Mathf.MoveTowards(ic.alpha, alpha, dt * 3f);
        }

        static void Apply(Icon ic, float time, float dt, float phase)
        {
            ic.pop = Mathf.MoveTowards(ic.pop, 0f, dt * 3.5f);
            ic.press = Mathf.MoveTowards(ic.press, 0f, dt * 6f);
            float pop = Mathf.Sin(ic.pop * Mathf.PI) * 0.35f;                     // up and back, springy
            float pulse = ic.hint * (0.12f + 0.12f * Mathf.Abs(Mathf.Sin(time * 4.5f)));
            float k = ic.scale * (1f + pop + pulse);
            float press = ic.press * ic.press;
            ic.rect.localScale = new Vector3(k * (1f + 0.08f * press), k * (1f - 0.18f * press), 1f);
            float wobble = ic.hint * 7f * Mathf.Sin(time * 9f + phase) + ic.pop * 10f * Mathf.Sin(ic.pop * 18f);
            ic.rect.localRotation = Quaternion.Euler(0f, 0f, wobble);
            float idle = Mathf.Sin(time * 1.8f + phase) * 3f;                      // a gentle chalk breathing
            ic.rect.anchoredPosition = ic.home + new Vector2(0f, idle - 10f * press + 8f * ic.hint * Mathf.Abs(Mathf.Sin(time * 4.5f)));
            float b = 1f - ic.dim;
            ic.image.color = new Color(b, b, b, ic.alpha);
        }
    }
}
