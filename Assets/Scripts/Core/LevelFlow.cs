using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectJuice
{
    /// <summary>
    /// Level start / death / end, and the in-game UI that goes with it (built here at Play, nothing to place):
    ///  - the jump counter, top left, which pops at every launch;
    ///  - a fade to black and back when the level restarts (a death without checkpoint, or Retry);
    ///  - the "You did it!" screen when the worm reaches a LevelFinish: title, the jumps counted up one by one,
    ///    the best score for this level, and the Retry / Main Menu buttons.
    /// Texts, fonts and scene names come from the scene's LevelSettings. Creates itself next to the worm.
    /// </summary>
    public class LevelFlow : MonoBehaviour
    {
        public static LevelFlow Instance { get; private set; }
        /// <summary>True from the moment the worm reaches the finish until the scene changes.</summary>
        public static bool IsComplete { get; private set; }

        static bool fadeInOnLoad;

        Canvas canvas;
        Image fade;
        TextMeshProUGUI counter;
        RectTransform counterRect;
        float counterPop;
        WormController subscribed;
        bool loading;

        public static void Ensure()
        {
            if (Instance != null) return;
            new GameObject("LevelFlow (auto)").AddComponent<LevelFlow>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            IsComplete = false;
            Time.timeScale = 1f; // a hit-stop or slow motion cut short by a restart
            BuildHud();
            if (fadeInOnLoad) { fadeInOnLoad = false; StartCoroutine(Fade(1f, 0f, 0.35f)); }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (subscribed != null) subscribed.Counter.Changed -= OnCounterChanged;
        }

        void Update()
        {
            WormController w = WormController.Instance;
            if (w != subscribed && w != null && w.Counter != null)
            {
                if (subscribed != null) subscribed.Counter.Changed -= OnCounterChanged;
                subscribed = w;
                w.Counter.Changed += OnCounterChanged;
                OnCounterChanged(w.Counter.Count);
                counterPop = 0f;
            }
            if (counterRect != null)
            {
                counterPop = Mathf.MoveTowards(counterPop, 0f, Time.unscaledDeltaTime * 4f);
                float k = 1f + 0.45f * counterPop * counterPop;
                counterRect.localScale = new Vector3(k, k, 1f);
                counterRect.localRotation = Quaternion.Euler(0f, 0f, 6f * counterPop * Mathf.Sin(Time.unscaledTime * 30f));
            }
        }

        void OnCounterChanged(int n)
        {
            LevelSettings s = LevelSettings.Current;
            if (counter == null) return;
            counter.text = string.Format(s != null ? s.counterText : "Jumps {0}", n);
            if (n > 0) counterPop = 1f;
        }

        // ------------------------------------------------------------------ restart / scenes

        /// <summary>Fade out and reload this level from the start.</summary>
        public static void RestartLevel()
        {
            Ensure();
            Scene sc = SceneManager.GetActiveScene();
            Instance.Load(sc.buildIndex >= 0 ? null : sc.name, sc.buildIndex);
        }

        public static void GoToMainMenu()
        {
            Ensure();
            LevelSettings s = LevelSettings.Current;
            Instance.Load(s != null && !string.IsNullOrEmpty(s.mainMenuScene) ? s.mainMenuScene : "MainMenu", -1);
        }

        void Load(string sceneName, int buildIndex)
        {
            if (loading) return;
            loading = true;
            StartCoroutine(FadeAndLoad(sceneName, buildIndex));
        }

        IEnumerator FadeAndLoad(string sceneName, int buildIndex)
        {
            yield return Fade(fade != null ? fade.color.a : 0f, 1f, 0.3f);
            fadeInOnLoad = true;
            Time.timeScale = 1f;
            if (sceneName != null) SceneManager.LoadScene(sceneName);
            else SceneManager.LoadScene(buildIndex);
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            if (fade == null) yield break;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFade(to);
        }

        void SetFade(float a)
        {
            Color c = fade.color;
            c.a = a;
            fade.color = c;
            fade.raycastTarget = a > 0.5f;
        }

        // ------------------------------------------------------------------ level complete

        /// <summary>The worm reached the finish: celebrate, then show the result screen.</summary>
        public static void CompleteLevel(Vector2 where)
        {
            if (IsComplete) return;
            Ensure();
            IsComplete = true;
            Instance.StartCoroutine(Instance.Celebrate(where));
        }

        IEnumerator Celebrate(Vector2 where)
        {
            WormController w = WormController.Instance;
            if (w != null) w.inputLocked = true;
            int jumps = w != null ? w.LaunchCount : 0;
            Vector2 at = w != null && w.Physics != null ? w.Physics.Body.position : where;

            // Chalk confetti burst + shake + short slow motion.
            GameJuice.Shake(0.25f);
            GameAudio.Play("win", 0.9f);
            for (int i = 0; i < 46; i++)
            {
                Vector2 v = new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.6f)).normalized * Random.Range(6f, 16f);
                Color c = i % 3 == 0 ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(1f, 1f, 1f, 0.95f);
                SimpleDebris.Spawn(at, v, c, Random.Range(0.1f, 0.24f), Random.Range(0.7f, 1.3f));
            }
            Time.timeScale = 0.3f;
            yield return new WaitForSecondsRealtime(0.45f);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(0.25f);

            // Best score for this level (fewest jumps).
            string key = "ProjectJuice.Best." + SceneManager.GetActiveScene().name;
            int best = PlayerPrefs.GetInt(key, 0);
            bool newBest = best <= 0 || jumps < best;
            if (newBest) { PlayerPrefs.SetInt(key, jumps); PlayerPrefs.Save(); }

            yield return ShowResult(jumps, newBest ? jumps : best, newBest);
        }

        IEnumerator ShowResult(int jumps, int best, bool newBest)
        {
            LevelSettings s = LevelSettings.Current;
            EnsureEventSystem();
            if (counter != null) counter.gameObject.SetActive(false);

            RectTransform panel = Rect("Result", canvas.transform);
            Stretch(panel);
            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0f);

            TextMeshProUGUI title = Text("Title", panel, s != null ? s.completeTitle : "You did it!", TitleFont(), 170f, Color.white);
            Place(title.rectTransform, new Vector2(0f, 190f), new Vector2(1600f, 260f));
            TextMeshProUGUI count = Text("Jumps", panel, "", BodyFont(), 96f, Color.white);
            Place(count.rectTransform, new Vector2(0f, 10f), new Vector2(1200f, 140f));
            TextMeshProUGUI bestLine = Text("Best", panel, "", BodyFont(), 56f, new Color(1f, 1f, 1f, 0.75f));
            Place(bestLine.rectTransform, new Vector2(0f, -85f), new Vector2(1200f, 90f));
            RectTransform retry = MakeButton(panel, s != null ? s.retryText : "Retry", new Vector2(-300f, -250f), () => { GameAudio.Play("ui_click"); RestartLevel(); });
            RectTransform menu = MakeButton(panel, s != null ? s.menuText : "Main Menu", new Vector2(300f, -250f), () => { GameAudio.Play("ui_click"); GoToMainMenu(); });
            title.rectTransform.localScale = Vector3.zero;
            count.alpha = 0f;
            bestLine.alpha = 0f;
            retry.localScale = Vector3.zero;
            menu.localScale = Vector3.zero;

            // 1. dim + title drops in with an overshoot and a wobble
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.5f;
                dim.color = new Color(0f, 0f, 0f, 0.7f * Mathf.Clamp01(k * 1.6f));
                title.rectTransform.localScale = Vector3.one * Overshoot(k);
                title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -10f * (1f - k) * Mathf.Cos(k * 12f));
                yield return null;
            }
            title.rectTransform.localScale = Vector3.one;
            title.rectTransform.localRotation = Quaternion.identity;

            // 2. jumps counted up one by one, a tick each, a pop at the end
            string fmt = s != null ? s.jumpsText : "Jumps: {0}";
            count.alpha = 1f;
            float step = jumps > 0 ? Mathf.Clamp(1.2f / jumps, 0.035f, 0.12f) : 0f;
            for (int i = 0; i <= jumps; i++)
            {
                count.text = string.Format(fmt, i);
                count.rectTransform.localScale = Vector3.one * 1.12f;
                if (i > 0) GameAudio.Play("tick", 0.5f, 1f + Mathf.Min(i, 30) * 0.015f);
                for (float t = 0f; t < step; t += Time.unscaledDeltaTime)
                {
                    count.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t / step);
                    yield return null;
                }
            }
            count.text = string.Format(fmt, jumps);
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                count.rectTransform.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(t / 0.3f * Mathf.PI));
                yield return null;
            }
            count.rectTransform.localScale = Vector3.one;

            // 3. best score, then the buttons pop in
            bestLine.text = newBest ? (s != null ? s.newBestText : "New best!") : string.Format(s != null ? s.bestText : "Best: {0}", best);
            if (newBest) { bestLine.color = new Color(1f, 0.85f, 0.35f, 1f); GameAudio.Play("checkpoint", 0.7f, 1.2f); }
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { bestLine.alpha = t / 0.25f; yield return null; }
            bestLine.alpha = 1f;
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                retry.localScale = Vector3.one * Overshoot(Mathf.Clamp01(t / 0.3f));
                menu.localScale = Vector3.one * Overshoot(Mathf.Clamp01((t - 0.1f) / 0.3f));
                yield return null;
            }
            retry.localScale = Vector3.one;
            menu.localScale = Vector3.one;

            // 4. idle: the title floats gently
            while (true)
            {
                float b = Time.unscaledTime;
                title.rectTransform.anchoredPosition = new Vector2(0f, 190f + Mathf.Sin(b * 2.2f) * 8f);
                title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(b * 1.7f) * 1.5f);
                yield return null;
            }
        }

        static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 2.2f; // easeOutBack
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        // ------------------------------------------------------------------ UI building

        void BuildHud()
        {
            GameObject go = new GameObject("LevelFlow UI");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            LevelSettings s = LevelSettings.Current;
            if (s == null || s.showJumpCounter)
            {
                counter = Text("Jump Counter", canvas.transform, "", BodyFont(), 72f, Color.white);
                counter.alignment = TextAlignmentOptions.TopLeft;
                counterRect = counter.rectTransform;
                counterRect.anchorMin = counterRect.anchorMax = new Vector2(0f, 1f);
                counterRect.pivot = new Vector2(0.2f, 0.6f);
                counterRect.anchoredPosition = new Vector2(70f, -70f);
                counterRect.sizeDelta = new Vector2(600f, 110f);
                OnCounterChanged(0);
            }

            RectTransform f = Rect("Fade", canvas.transform);
            Stretch(f);
            fade = f.gameObject.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, fadeInOnLoad ? 1f : 0f);
            fade.raycastTarget = false;
        }

        RectTransform MakeButton(RectTransform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform r = Rect(label, parent);
            Place(r, pos, new Vector2(460f, 130f));
            Image hit = r.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // invisible, only catches the clicks
            Button b = r.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(onClick);
            r.gameObject.AddComponent<UIButtonJuice>();

            TextMeshProUGUI t = Text("Label", r, label, BodyFont(), 76f, Color.white);
            Stretch(t.rectTransform);
            RectTransform line = Rect("Chalk Line", r);
            line.anchorMin = new Vector2(0.15f, 0f);
            line.anchorMax = new Vector2(0.85f, 0f);
            line.sizeDelta = new Vector2(0f, 6f);
            line.anchoredPosition = new Vector2(0f, 14f);
            Image li = line.gameObject.AddComponent<Image>();
            li.color = new Color(1f, 1f, 1f, 0.8f);
            li.raycastTarget = false;
            return r;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color)
        {
            RectTransform r = Rect(name, parent);
            TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        static TMP_FontAsset TitleFont()
        {
            LevelSettings s = LevelSettings.Current;
            return s != null && s.titleFont != null ? s.titleFont : null;
        }

        static TMP_FontAsset BodyFont()
        {
            LevelSettings s = LevelSettings.Current;
            return s != null && s.textFont != null ? s.textFont : TitleFont();
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }
    }
}
