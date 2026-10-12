using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// All numbers that make Squishy and Heavy feel different. Values come from the web prototype
    /// (converted from px to units, 1 unit = 32 px); the few marked NEW had no equivalent there.
    /// </summary>
    [CreateAssetMenu(menuName = "Project Juice/Worm Material Profile", fileName = "WormMaterialProfile")]
    public class WormMaterialProfile : ScriptableObject
    {
        public WormMaterial material = WormMaterial.Squishy;

        [Header("Body")]
        [Tooltip("NEW. Used for impact force (mass * speed) and for impulses from pistons/rollers.")]
        public float mass = 0.3f;
        [Tooltip("Multiplier on the global gravity. Web: Squishy 0.7, Heavy 1.6. Squishy is now 1.1 (faster hops).")]
        public float gravityScale = 0.7f;
        [Tooltip("Collider radius in units. Web: 12 px / 11 px.")]
        public float radius = 0.375f;
        [Tooltip("Body friction (PhysicsMaterial2D). Combined with the surface as sqrt(a*b).")]
        public float bodyFriction = 0.6f;

        [Header("Launch (speeds in units/s, not forces: the launch impulse is mass * speed)")]
        public float minLaunchSpeed = 6.25f;
        public float maxLaunchSpeed = 31.25f;

        [Header("Contact")]
        [Tooltip("Bounces slower than this become 0, so the worm settles. Web: 25 / 60 px/s.")]
        public float stopBounceSpeed = 0.78f;
        [Range(0f, 1f), Tooltip("Share of sideways speed kept on a floor impact (1 = slides on, 0.3 = grips). Web 'landingFriction': 0.92 / 0.3.")]
        public float landingTangentialKeep = 0.92f;
        [Range(0f, 1f), Tooltip("Share of speed kept after smashing a Brittle object. Web: 0.85 for Heavy.")]
        public float breakSpeedKeep = 1f;

        [Header("Restitution per surface (energy kept on a bounce, 1 = perfectly elastic)")]
        public float restitutionLint = 0.35f;     // NEW (soft)
        public float restitutionMetal = 0.7f;     // web 'wall'
        public float restitutionMembrane = 1.05f; // NEW (slight boost)
        public float restitutionGrease = 0.5f;    // NEW
        public float restitutionCilia = 0.2f;     // NEW
        public float restitutionBrittle = 0.97f;  // web 'glass'

        [Header("Look")]
        public Color color = new Color(0.66f, 0.65f, 0.95f);
        [Range(0f, 1f), Tooltip("How much the visual squashes on impact. Web: 0.5 / 0.")]
        public float squashAmount = 0.5f;

        public float GetRestitution(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Lint: return restitutionLint;
                case SurfaceType.Metal: return restitutionMetal;
                case SurfaceType.Membrane: return restitutionMembrane;
                case SurfaceType.Grease: return restitutionGrease;
                case SurfaceType.Cilia: return restitutionCilia;
                case SurfaceType.Brittle: return restitutionBrittle;
            }
            return restitutionMetal;
        }

        static WormMaterialProfile defaultSquishy;
        static WormMaterialProfile defaultHeavy;

        public static WormMaterialProfile GetDefault(WormMaterial m)
        {
            if (m == WormMaterial.Squishy)
            {
                if (defaultSquishy == null) { defaultSquishy = CreateInstance<WormMaterialProfile>(); defaultSquishy.hideFlags = HideFlags.HideAndDontSave; FillDefaults(defaultSquishy, m); }
                return defaultSquishy;
            }
            if (defaultHeavy == null) { defaultHeavy = CreateInstance<WormMaterialProfile>(); defaultHeavy.hideFlags = HideFlags.HideAndDontSave; FillDefaults(defaultHeavy, m); }
            return defaultHeavy;
        }

        public static void FillDefaults(WormMaterialProfile p, WormMaterial m)
        {
            p.material = m;
            if (m == WormMaterial.Squishy)
            {
                // Snappier than the web prototype (0.7 / 31.25): about the same reach, but each hop takes a third less time.
                p.mass = 0.3f; p.gravityScale = 1.1f; p.radius = 12f / 32f; p.bodyFriction = 0.6f;
                p.minLaunchSpeed = 8f; p.maxLaunchSpeed = 40f;
                p.stopBounceSpeed = 25f / 32f; p.landingTangentialKeep = 0.92f; p.breakSpeedKeep = 1f;
                p.restitutionLint = 0.35f; p.restitutionMetal = 0.7f; p.restitutionMembrane = 1.05f;
                p.restitutionGrease = 0.5f; p.restitutionCilia = 0.2f; p.restitutionBrittle = 0.97f;
                p.color = new Color(0.66f, 0.65f, 0.95f); p.squashAmount = 0.5f;
            }
            else
            {
                p.mass = 2.5f; p.gravityScale = 1.6f; p.radius = 11f / 32f; p.bodyFriction = 1.0f;
                p.minLaunchSpeed = 180f / 32f; p.maxLaunchSpeed = 850f / 32f;
                p.stopBounceSpeed = 60f / 32f; p.landingTangentialKeep = 0.3f; p.breakSpeedKeep = 0.85f;
                p.restitutionLint = 0f; p.restitutionMetal = 0.08f; p.restitutionMembrane = 0.45f;
                p.restitutionGrease = 0f; p.restitutionCilia = 0f; p.restitutionBrittle = 0.1f;
                p.color = new Color(0.44f, 0.41f, 0.77f); p.squashAmount = 0f;
            }
        }
    }
}
