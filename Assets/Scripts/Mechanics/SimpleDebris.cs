using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Visual-only shard: flies on a ballistic arc and fades. No physics, so it never pushes the worm.
    /// Shards are pooled (reused instead of created and destroyed) and capped at Max Active, so a big
    /// break or a burst of dust never stalls the game.
    /// </summary>
    public class SimpleDebris : MonoBehaviour
    {
        /// <summary>Most shards alive at once. Extra spawns are skipped (the oldest ones are still flying).</summary>
        public static int MaxActive = 150;

        static readonly Stack<SimpleDebris> pool = new Stack<SimpleDebris>();
        static int active;
        static Transform poolRoot;

        Vector2 velocity;
        float life;
        float maxLife;
        float spin;
        SpriteRenderer sr;
        Color color;
        bool live;

        public static void Spawn(Vector2 pos, Vector2 vel, Color color, float size, float life)
        {
            if (active >= MaxActive) return;
            SimpleDebris d = null;
            while (pool.Count > 0 && d == null) d = pool.Pop(); // entries die with their scene
            if (d == null)
            {
                if (poolRoot == null) poolRoot = new GameObject("Debris (pool)").transform;
                GameObject go = new GameObject("Shard");
                go.transform.SetParent(poolRoot, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.sprite = PlaceholderSprites.Square;
                r.sortingOrder = 15;
                d = go.AddComponent<SimpleDebris>();
                d.sr = r;
            }
            Transform t = d.transform;
            t.position = pos;
            t.localScale = new Vector3(size, size * Random.Range(0.5f, 1f), 1f);
            t.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            d.velocity = vel;
            d.life = d.maxLife = life;
            d.spin = Random.Range(-540f, 540f);
            d.color = color;
            d.sr.color = color;
            d.live = true;
            active++;
            d.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            life -= dt;
            if (life <= 0f) { Recycle(); return; }
            velocity += Physics2D.gravity * 0.6f * dt;
            transform.position += (Vector3)(velocity * dt);
            transform.Rotate(0, 0, spin * dt);
            Color c = color;
            c.a = color.a * Mathf.Clamp01(life / maxLife);
            sr.color = c;
        }

        void Recycle()
        {
            if (!live) return;
            live = false;
            active--;
            gameObject.SetActive(false);
            pool.Push(this);
        }

        void OnDestroy()
        {
            if (live) { live = false; active--; }
        }
    }
}
