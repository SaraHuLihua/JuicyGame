using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Everything other systems (breakables, juice, audio) need to know about one hit.</summary>
    public struct ImpactInfo
    {
        public WormMaterial material;
        public SurfaceType surface;
        public Collider2D collider;
        public Vector2 point;
        /// <summary>Surface normal, pointing towards the worm.</summary>
        public Vector2 normal;
        /// <summary>Speed along the normal just before the hit (units/s).</summary>
        public float normalSpeed;
        /// <summary>Speed along the normal just after the bounce (units/s).</summary>
        public float outSpeed;
        /// <summary>mass * normalSpeed. This is what Brittle objects compare to their threshold.</summary>
        public float impactForce;
        public bool brokeObject;
    }
}
