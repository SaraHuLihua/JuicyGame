using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Visual-only shard: flies on a ballistic arc and fades. No physics, so it never pushes the worm.</summary>
    public class SimpleDebris : MonoBehaviour
    {
        Vector2 velocity;
        float life;
        float maxLife;
        float spin;
        SpriteRenderer sr;
        Color color;

        public static void Spawn(Vector2 pos, Vector2 vel, Color color, float size, float life)
        {
            GameObject go = new GameObject("Shard");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size, size * Random.Range(0.5f, 1f), 1f);
            go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sprite = PlaceholderSprites.Square;
            r.color = color;
            r.sortingOrder = 15;
            SimpleDebris d = go.AddComponent<SimpleDebris>();
            d.velocity = vel;
            d.life = d.maxLife = life;
            d.spin = Random.Range(-540f, 540f);
            d.sr = r;
            d.color = color;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }
            velocity += Physics2D.gravity * 0.6f * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            transform.Rotate(0, 0, spin * Time.deltaTime);
            Color c = color;
            c.a = Mathf.Clamp01(life / maxLife);
            sr.color = c;
        }
    }
}
