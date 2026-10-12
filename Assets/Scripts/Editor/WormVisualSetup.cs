#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// One-click wiring: Project Juice > Wire Worm Sprites.
    /// Assigns the real Idle / Transition / Death strips from Assets/Art/player
    /// to every WormVisual in the open scene AND to the Worm prefab.
    /// Safe to run again. Build targets still need the references saved in the prefab/scene.
    /// </summary>
    public static class WormVisualSetup
    {
        const string IdleDir = "Assets/Art/player/Idle";
        const string TransDir = "Assets/Art/player/Transition Bouncy - Rocky";
        const string DeathDir = "Assets/Art/player/Death";
        const string WormPrefabPath = "Assets/Prefabs/Player/Worm.prefab";

        [MenuItem("Project Juice/Wire Worm Sprites")]
        public static void WireAll()
        {
            Sprite[] bouncyIdle = Load(new[] { "bouncy_idle_1", "bouncy_idle_2", "bouncy_idle_3" }, IdleDir);
            Sprite[] rockyIdle = Load(new[] { "rocky_idle_1", "rocky_idle_2", "rocky_idle_3" }, IdleDir);
            Sprite[] trans = Load(new[] { "Worm_Bouncy_1", "Worm_Bouncy_2", "Worm_Bouncy_3", "Worm_Flat", "Worm_Rocky_1", "Worm_Rocky_2", "Worm_Rocky_3" }, TransDir);
            Sprite[] bouncyDeath = Load(new[] { "bouncy_death_1", "bouncy_death_2", "bouncy_death_3", "bouncy_death_4" }, DeathDir);
            Sprite[] rockyDeath = Load(new[] { "rocky_death_1", "rocky_death_2", "rocky_death_3", "rocky_death_4" }, DeathDir);

            int fixedCount = 0;
            WormVisual[] inScene = Object.FindObjectsOfType<WormVisual>(true);
            foreach (WormVisual w in inScene) { Assign(w, bouncyIdle, rockyIdle, trans, bouncyDeath, rockyDeath); EditorUtility.SetDirty(w); fixedCount++; }

            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(WormPrefabPath);
            if (prefabRoot != null)
            {
                WormVisual pv = prefabRoot.GetComponentInChildren<WormVisual>();
                if (pv != null) { Assign(pv, bouncyIdle, rockyIdle, trans, bouncyDeath, rockyDeath); EditorUtility.SetDirty(prefabRoot); }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("[Project Juice] Wired worm sprites: bouncyIdle={0} rockyIdle={1} trans={2} deaths={3}/{4} -> {5} visuals in scene + prefab.",
                bouncyIdle.Length, rockyIdle.Length, trans.Length, bouncyDeath.Length, rockyDeath.Length, fixedCount));
        }

        static void Assign(WormVisual w, Sprite[] bi, Sprite[] ri, Sprite[] tr, Sprite[] bd, Sprite[] rd)
        {
            if (bi.Length == 3) w.bouncyIdle = bi;
            if (ri.Length == 3) w.rockyIdle = ri;
            if (tr.Length == 7) w.bouncyToRocky = tr;
            if (bd.Length == 4) w.bouncyDeath = bd;
            if (rd.Length == 4) w.rockyDeath = rd;
        }

        static Sprite[] Load(string[] names, string dir)
        {
            System.Collections.Generic.List<Sprite> outList = new System.Collections.Generic.List<Sprite>();
            foreach (string n in names)
            {
                Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + n + ".png");
                if (s != null) outList.Add(s);
                else Debug.LogWarning("[Project Juice] Missing sprite: " + dir + "/" + n + ".png");
            }
            return outList.ToArray();
        }
    }
}
#endif
