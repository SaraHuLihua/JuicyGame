using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectJuice
{
    /// <summary>
    /// Paper confetti for the level finish: little rectangles in the game's colours that pop out, slow down in
    /// the air and flutter down, flipping over (the back side is darker) and swaying side to side.
    /// Drawn on its own overlay canvas just above the result screen (clicks pass through), so the shower stays
    /// bright over the dimmed result panel. Runs on real time, so it keeps moving during the finish slow motion.
    ///   Confetti.Burst(worldPoint, count)   a pop from one point (the worm)
    ///   Confetti.Cannons(count)             two blasts from the bottom corners of the screen
    ///   Confetti.Rain(seconds, perSecond)   a shower from the top of the screen
    /// </summary>
    public class Confetti : MonoBehaviour
    {
        public static int MaxActive = 400;
        public static int SortingOrder = 510; // the result screen is 500
        public static Color[] Palette =
        {
            new Color(1f, 0.655f, 0.875f, 1f),   // Squishy pink
            new Color(0.663f, 0.647f, 1f, 1f),   // Rocky lilac
            new Color(1f, 0.85f, 0.35f, 1f),     // gold
            new Color(0.55f, 0.95f, 0.8f, 1f),   // mint
            new Color(0.55f, 0.8f, 1f, 1f),      // sky
            new Color(1f, 1f, 1f, 1f),           // chalk white
        };

        class Piece
        {
            public RectTransform rt;
            public Image img;
            public Vector2 pos, vel, size;
            public float life, maxLife, flipPhase, flipSpeed, swayPhase, swaySpeed, spin, angle;
            public Color color;
        }

        static Confetti host;
        RectTransform area;
        readonly List<Piece> live = new List<Piece>();
        readonly Stack<Piece> pool = new Stack<Piece>();
        float rainUntil, rainRate, rainDebt;

        static Confetti Host()
        {
            if (host != null) return host;
            GameObject go = new GameObject("Confetti");
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = SortingOrder;
            CanvasScaler cs = go.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
            host = go.AddComponent<Confetti>();
            host.area = (RectTransform)go.transform;
            return host;
        }

        Vector2 Size { get { Vector2 s = area.rect.size; return s.sqrMagnitude > 1f ? s : new Vector2(1920f, 1080f); } }

        /// <summary>A pop from a point in the level (converted to the screen).</summary>
        public static void Burst(Vector2 worldPoint, int count)
        {
            Confetti h = Host();
            Camera cam = Camera.main;
            Vector2 vp = cam != null ? (Vector2)cam.WorldToViewportPoint(worldPoint) : new Vector2(0.5f, 0.5f);
            Vector2 at = Vector2.Scale(vp, h.Size);
            for (int i = 0; i < count; i++)
            {
                float a = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                h.Spawn(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(350f, 950f));
            }
        }

        /// <summary>Two blasts from the bottom corners, aimed up and inward.</summary>
        public static void Cannons(int countEach)
        {
            Confetti h = Host();
            Vector2 s = h.Size;
            for (int i = 0; i < countEach; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float a = Random.Range(58f, 82f) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(a) * -side, Mathf.Sin(a));
                    Vector2 from = new Vector2(side < 0 ? s.x * 0.02f : s.x * 0.98f, -20f);
                    h.Spawn(from, dir * Random.Range(1500f, 2500f));
                }
        }

        /// <summary>A shower from the top edge for a few seconds.</summary>
        public static void Rain(float seconds, float perSecond)
        {
            Confetti h = Host();
            h.rainUntil = Time.unscaledTime + seconds;
            h.rainRate = perSecond;
        }

        void Spawn(Vector2 pos, Vector2 vel)
        {
            if (live.Count >= MaxActive) return;
            Piece p = pool.Count > 0 ? pool.Pop() : null;
            if (p == null)
            {
                GameObject go = new GameObject("Piece", typeof(RectTransform));
                go.transform.SetParent(area, false);
                p = new Piece();
                p.rt = (RectTransform)go.transform;
                p.rt.anchorMin = p.rt.anchorMax = Vector2.zero; // positions are from the bottom-left corner
                p.img = go.AddComponent<Image>();
                p.img.raycastTarget = false;
            }
            p.pos = pos;
            p.vel = vel;
            p.life = p.maxLife = Random.Range(3f, 5f);
            p.size = new Vector2(Random.Range(20f, 34f), Random.Range(11f, 18f));
            p.flipPhase = Random.Range(0f, Mathf.PI * 2f);
            p.flipSpeed = Random.Range(6f, 14f);
            p.swayPhase = Random.Range(0f, Mathf.PI * 2f);
            p.swaySpeed = Random.Range(2f, 4f);
            p.spin = Random.Range(-220f, 220f);
            p.angle = Random.Range(0f, 360f);
            p.color = Palette[Random.Range(0, Palette.Length)];
            p.rt.sizeDelta = p.size;
            p.rt.gameObject.SetActive(true);
            live.Add(p);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            UpdateRain(dt);
            float drag = Mathf.Exp(-2.2f * dt);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Piece p = live[i];
                p.life -= dt;
                if (p.life <= 0f)
                {
                    p.rt.gameObject.SetActive(false);
                    live.RemoveAt(i);
                    pool.Push(p);
                    continue;
                }
                // Paper: strong air drag, gentle gravity, slow terminal fall, swaying once it has slowed down.
                p.vel *= drag;
                p.vel.y -= 380f * dt;
                if (p.vel.y < -150f) p.vel.y = -150f;
                p.swayPhase += p.swaySpeed * dt;
                float sway = Mathf.Sin(p.swayPhase) * 70f * Mathf.Clamp01(1f - p.vel.magnitude / 500f);
                p.pos += (p.vel + new Vector2(sway, 0f)) * dt;
                p.rt.anchoredPosition = p.pos;

                p.flipPhase += p.flipSpeed * dt;
                float flip = Mathf.Cos(p.flipPhase);                   // the piece turning over in 3D
                p.angle += p.spin * dt;
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.angle + Mathf.Sin(p.swayPhase) * 25f);
                p.rt.localScale = new Vector3(Mathf.Max(0.08f, Mathf.Abs(flip)), 1f, 1f);
                Color c = flip >= 0f ? p.color : p.color * 0.72f;      // back side a little darker
                c.a = Mathf.Clamp01(p.life / 0.6f);
                p.img.color = c;
            }
        }

        void UpdateRain(float dt)
        {
            if (Time.unscaledTime > rainUntil) return;
            rainDebt += rainRate * dt;
            Vector2 s = Size;
            while (rainDebt >= 1f)
            {
                rainDebt -= 1f;
                Spawn(new Vector2(Random.Range(0f, s.x), s.y + 30f), new Vector2(Random.Range(-60f, 60f), Random.Range(-80f, 0f)));
            }
        }

        void OnDestroy()
        {
            if (host == this) host = null;
        }
    }
}
