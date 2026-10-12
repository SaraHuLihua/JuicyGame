using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Runtime-generated white square / circle sprites (1 unit big) for greybox visuals created from code.
    /// Replace with real art later.
    /// </summary>
    public static class PlaceholderSprites
    {
        static Sprite square;
        static Sprite circle;

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    Texture2D t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    Color[] px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    t.SetPixels(px);
                    t.Apply();
                    t.hideFlags = HideFlags.HideAndDontSave;
                    square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    square.hideFlags = HideFlags.HideAndDontSave;
                }
                return square;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int s = 64;
                    Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
                    t.filterMode = FilterMode.Bilinear;
                    float r = s * 0.5f;
                    for (int y = 0; y < s; y++)
                        for (int x = 0; x < s; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                            float a = Mathf.Clamp01(r - d);
                            t.SetPixel(x, y, new Color(1, 1, 1, a));
                        }
                    t.Apply();
                    t.hideFlags = HideFlags.HideAndDontSave;
                    circle = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
                    circle.hideFlags = HideFlags.HideAndDontSave;
                }
                return circle;
            }
        }

        public static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color, Vector2 localPos, Vector2 size, int order = 0)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
