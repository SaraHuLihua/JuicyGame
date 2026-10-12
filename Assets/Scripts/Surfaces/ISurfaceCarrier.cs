using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// A surface that moves whatever stands on it without the collider itself moving
    /// (cilia wave, chain, conveyor). The worm asks it each physics step.
    /// </summary>
    public interface ISurfaceCarrier
    {
        /// <summary>World-space velocity the surface tries to give a worm in this material (already scaled by influence).</summary>
        Vector2 GetCarryVelocity(WormMaterial material);

        /// <summary>How fast (units/s^2) the worm is brought up to that velocity.</summary>
        float GetCarryAcceleration(WormMaterial material);
    }
}
