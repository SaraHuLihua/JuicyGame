using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Keyboard shortcuts for testing:
    ///   R = respawn at last landing    T = reset current area (worm, launch count, broken objects)
    ///   [ and ] = previous / next area   1..9, 0 = jump to area
    /// </summary>
    public class DebugTools : MonoBehaviour
    {
        public int currentArea;
        public bool enableShortcuts = true;

        void Start()
        {
            if (TestAreaMarker.All.Count > 0) JumpTo(currentArea);
        }

        void Update()
        {
            WormController w = WormController.Instance;
            if (!enableShortcuts || w == null) return;

            if (GameInput.RespawnDown) w.RespawnAtLastLanding();
            if (GameInput.ResetAreaDown) JumpTo(currentArea);
            if (GameInput.PrevAreaDown) JumpTo(currentArea - 1);
            if (GameInput.NextAreaDown) JumpTo(currentArea + 1);
            for (int i = 0; i < 10; i++)
                if (GameInput.DigitDown(i) && i < TestAreaMarker.All.Count) JumpTo(i);
        }

        public void JumpTo(int index)
        {
            int n = TestAreaMarker.All.Count;
            WormController w = WormController.Instance;
            if (n == 0 || w == null) return;
            currentArea = ((index % n) + n) % n;

            TestAreaMarker m = TestAreaMarker.All[currentArea];
            w.ResetRun(m.transform.position, m.startMaterial);

            BreakableObject[] all = FindBreakables();
            for (int i = 0; i < all.Length; i++) all[i].Restore();

            CameraFollow2D cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
            if (cam != null) cam.Snap();
        }

        static BreakableObject[] FindBreakables()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<BreakableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<BreakableObject>(true);
#endif
        }
    }
}
