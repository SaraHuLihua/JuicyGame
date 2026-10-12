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

    /// <summary>
    /// A carrier whose direction depends on where the worm touches it (a belt running around wheels).
    /// WormPhysics passes the ground contact point.
    /// </summary>
    public interface IPointSurfaceCarrier : ISurfaceCarrier
    {
        /// <summary>World-space carry velocity at a world contact point.</summary>
        Vector2 GetCarryVelocityAt(WormMaterial material, Vector2 worldPoint);
    }
}
