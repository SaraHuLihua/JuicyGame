#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// One click to re-apply everything that was lost:
    ///   Project Juice > Apply Lost Fixes (tubi + surface + worm)
    /// Does: rebuild Surface_Dark prefabs + Tube prefab, re-wire worm sprites on the prefab,
    /// set fast death + sane size defaults, add Tube (Traversable) to tubo0..3 sized from YOUR art,
    /// then saves everything. Run it with MechanicsTest open. Undoable (Ctrl+Z) except prefab rebuilds.
    /// What it does NOT guess: which tubes are Solid and which props are deadly — set those by hand after.
    /// </summary>
    public static class BatchRecovery
    {
        static readonly string[] TubeNames = { "tubo0", "tubo1", "tubo2", "tubo3", "tubo2 (2)" };

        [MenuItem("Project Juice/Apply Lost Fixes (tubi + surface + worm)")]
        public static void ApplyAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            SurfacePrefabBuilder.Build();
            LevelTemplateBuilder.CreateTubePrefab();
            WormVisualSetup.WireAll();
            TuneWormPrefab();

            int fixedTubes = 0;
            for (int s = 0; s < EditorSceneManager.sceneCount; s++)
            {
                Scene scene = EditorSceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    fixedTubes += FixTubesRecursive(root);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log(string.Format(
                "[Project Juice] Done. Tubes fixed: {0}. Worm prefab: death fast + size 2.5 + sprites re-wired. " +
                "YOU still need to: (1) set Solid on closed tubes, (2) Add Component > Hazard on saws/spikes, (3) check tube collider sizes in Scene view.",
                fixedTubes));
        }

        static void TuneWormPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Worm.prefab");
            if (prefab == null) return;
            WormVisual v = prefab.GetComponentInChildren<WormVisual>();
            if (v == null) return;
            v.deathFps = 18f;              // fast death (~0.2s for 4 frames)
            v.transitionFps = 10f;         // clean transition, juice frozen while it plays
            v.visualSizeMultiplier = 2.6f; // matches your 800px padded art to the 0.375 collider; fine-tune +-0.3
            v.swapBouncyRockyArt = false;  // canonical wiring: pink=bouncy, purple=rocky
            EditorUtility.SetDirty(prefab);
        }

        static int FixTubesRecursive(GameObject go)
        {
            int n = 0;
            foreach (string t in TubeNames)
            {
                if (go.name == t)
                {
                    Tube tube = go.GetComponent<Tube>();
                    if (tube == null) tube = Undo.AddComponent<Tube>(go);
                    tube.tubeType = Tube.TubeType.Traversable; // default: you flip Solid ones by hand
                    tube.ConfigureFromArt();                   // size walls/blocker from your slices
                    EditorUtility.SetDirty(go);
                    n++;
                    break;
                }
            }
            foreach (Transform child in go.transform) n += FixTubesRecursive(child.gameObject);
            return n;
        }
    }
}
#endif
