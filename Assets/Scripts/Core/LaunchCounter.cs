using System;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Counts launches. There is no limit; the number is what level scoring will use later.</summary>
    public class LaunchCounter : MonoBehaviour
    {
        public int Count { get; private set; }
        public event Action<int> Changed;

        public void Increment()
        {
            Count++;
            if (Changed != null) Changed(Count);
        }

        public void ResetCount()
        {
            Count = 0;
            if (Changed != null) Changed(Count);
        }
    }
}
