using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Put this on any object with solid colliders to say what it is made of.
    /// It applies the friction to its colliders (bounce is handled by the worm, so bounciness stays 0 here).
    /// The worm never reads names or colours, only this component.
    /// </summary>
    [DisallowMultipleComponent]
    public class Surface : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Lint;
        [Tooltip("Optional. Leave empty to use the built-in defaults for the type.")]
        public SurfaceProfile profileOverride;

        PhysicsMaterial2D material;

        public SurfaceProfile Profile
        {
            get { return profileOverride != null ? profileOverride : SurfaceProfile.GetDefault(type); }
        }

        void Awake()
        {
            ApplyMaterial();
        }

        [ContextMenu("Apply physics material")]
        public void ApplyMaterial()
        {
            material = new PhysicsMaterial2D(name + "_" + type);
            material.friction = Profile.friction;
            material.bounciness = 0f;
            Collider2D[] cols = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].isTrigger) continue;
                if (cols[i].GetComponentInParent<Surface>() != this) continue; // belongs to a nested Surface
                cols[i].sharedMaterial = material;
            }
        }

        public static Surface Of(Collider2D c)
        {
            return c != null ? c.GetComponentInParent<Surface>() : null;
        }

        /// <summary>Profile for a collider; unmarked colliders behave like Metal (the old prototype's generic wall).</summary>
        public static SurfaceProfile ProfileOf(Collider2D c)
        {
            Surface s = Of(c);
            return s != null ? s.Profile : SurfaceProfile.GetDefault(SurfaceType.Metal);
        }
    }
}
