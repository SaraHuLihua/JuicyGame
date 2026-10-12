using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Forwards collisions of a roller to its RollerJaws.</summary>
    public class RollerHit : MonoBehaviour
    {
        public RollerJaws owner;

        void OnCollisionEnter2D(Collision2D c)
        {
            if (owner != null) owner.OnRollerTouched(c);
        }
    }
}
