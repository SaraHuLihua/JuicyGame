using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Physical behaviour of one surface type. Create assets from the menu, or leave the Surface component's
    /// override empty to use the built-in defaults below.
    /// The worm material decides HOW MUCH it bounces on each surface (see WormMaterialProfile);
    /// this profile decides friction, landing validity and grip.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Juice/Surface Profile", fileName = "SurfaceProfile")]
    public class SurfaceProfile : ScriptableObject
    {
        public SurfaceType type = SurfaceType.Lint;

        [Tooltip("Unity PhysicsMaterial2D friction. Combined with the worm body friction as sqrt(a*b).")]
        public float friction = 0.9f;

        [Tooltip("Multiplies the worm's per-surface restitution. 1 = use the worm value as is.")]
        public float restitutionMultiplier = 1f;

        [Tooltip("If false the worm can bounce on it but does NOT regain launch control (Membrane).")]
        public bool allowsLanding = true;

        [Range(0f, 1f), Tooltip("How much of the sideways speed is killed on a hard landing (1 = full, 0 = none). Web prototype: 'landingFriction'.")]
        public float landingGrip = 1f;

        [Tooltip("Greybox colour used by the builder.")]
        public Color color = new Color(0.43f, 0.39f, 0.35f);

        static readonly Dictionary<SurfaceType, SurfaceProfile> defaults = new Dictionary<SurfaceType, SurfaceProfile>();

        public static SurfaceProfile GetDefault(SurfaceType t)
        {
            SurfaceProfile p;
            if (!defaults.TryGetValue(t, out p) || p == null)
            {
                p = CreateInstance<SurfaceProfile>();
                p.hideFlags = HideFlags.HideAndDontSave;
                FillDefaults(p, t);
                defaults[t] = p;
            }
            return p;
        }

        public static void FillDefaults(SurfaceProfile p, SurfaceType t)
        {
            p.type = t;
            p.restitutionMultiplier = 1f;
            p.allowsLanding = true;
            switch (t)
            {
                case SurfaceType.Lint:      // soft, grippy, damps bounce
                    p.friction = 0.9f; p.landingGrip = 1f; p.color = new Color(0.43f, 0.39f, 0.35f); break;
                case SurfaceType.Metal:     // hard, springier (old prototype 'wall')
                    p.friction = 0.5f; p.landingGrip = 0.6f; p.color = new Color(0.36f, 0.42f, 0.47f); break;
                case SurfaceType.Membrane:  // very bouncy, slippery, never a landing
                    p.friction = 0.1f; p.landingGrip = 0.1f; p.allowsLanding = false; p.color = new Color(0.35f, 0.65f, 0.35f); break;
                case SurfaceType.Grease:    // almost frictionless
                    p.friction = 0.02f; p.landingGrip = 0f; p.color = new Color(0.78f, 0.62f, 0.2f); break;
                case SurfaceType.Cilia:     // carries Squishy (see CiliaCarpet)
                    p.friction = 0.35f; p.landingGrip = 1f; p.color = new Color(0.56f, 0.54f, 0.35f); break;
                case SurfaceType.Brittle:   // old prototype 'glass'
                    p.friction = 0.6f; p.landingGrip = 0.6f; p.color = new Color(0.76f, 0.70f, 0.50f); break;
            }
        }
    }
}
