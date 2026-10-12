using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Rigidbody2D.velocity was renamed linearVelocity in Unity 6. This keeps one code path.</summary>
    public static class PhysicsCompat
    {
        public static Vector2 GetVelocity(this Rigidbody2D rb)
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        public static void SetVelocity(this Rigidbody2D rb, Vector2 v)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = v;
#else
            rb.velocity = v;
#endif
        }
    }
}
