using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Marks the start of one test area in the MechanicsTest scene.</summary>
    public class TestAreaMarker : MonoBehaviour
    {
        public static readonly List<TestAreaMarker> All = new List<TestAreaMarker>();

        public string areaName = "Test";
        [TextArea] public string description = "";
        public WormMaterial startMaterial = WormMaterial.Squishy;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
