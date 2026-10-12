#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// Two menus. Run them in order, with the project open in the editor:
    ///   1. Project Juice > Build Chain+Pulley Prefabs  (creates Prefabs/Gameplay/BreakableChain + PulleyRig)
    ///   2. Project Juice > Add Chain+Pulley Test Areas (appends Area_H + Area_I to MechanicsTest.unity)
    /// Both are safe to run again (prefabs are rebuilt, test areas are skipped if already present).
    /// </summary>
    public static class ChainPulleySetup
    {
        const string GameplayDir = "Assets/Prefabs/Gameplay";
        const string ChainPrefabPath = "Assets/Prefabs/Gameplay/BreakableChain.prefab";
        const string PulleyPrefabPath = "Assets/Prefabs/Gameplay/PulleyRig.prefab";
        const string TestScenePath = "Assets/Scenes/MechanicsTest.unity";

        // ---------------------------------------------------------- prefabs

        [MenuItem("Project Juice/Build Chain+Pulley Prefabs")]
        public static void BuildPrefabs()
        {
            System.IO.Directory.CreateDirectory(GameplayDir);

            GameObject chainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChainPrefabPath);
            if (chainPrefab == null)
            {
                GameObject go = new GameObject("BreakableChain");
                BreakableChain chain = go.AddComponent<BreakableChain>();
                chain.upperLength = 2f;
                chain.lowerLength = 2.5f;
                chain.wormImpactThreshold = 15f;
                PrefabUtility.SaveAsPrefabAsset(go, ChainPrefabPath);
                Object.DestroyImmediate(go);
            }

            GameObject pulleyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PulleyPrefabPath);
            if (pulleyPrefab == null)
            {
                GameObject rig = new GameObject("PulleyRig");
                PulleySystem sys = rig.AddComponent<PulleySystem>();
                sys.maxTravel = 3f;
                sys.moveSpeed = 2f;

                Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholders/Circle.png");
                GameObject wheel = new GameObject("Wheel");
                wheel.transform.SetParent(rig.transform, false);
                wheel.transform.localPosition = new Vector3(0f, 2f, 0f);
                SpriteRenderer wheelSr = wheel.AddComponent<SpriteRenderer>();
                wheelSr.sprite = circle;
                wheelSr.color = new Color(0.55f, 0.58f, 0.62f);
                wheelSr.sortingOrder = 5;
                wheel.transform.localScale = Vector3.one * 1.2f;

                GameObject ropeGo = new GameObject("Rope");
                ropeGo.transform.SetParent(rig.transform, false);
                PulleyRope rope = ropeGo.AddComponent<PulleyRope>();

                PulleyPlatform a = MakePlatform(rig.transform, "PlatformA", new Vector3(-4f, 0f, 0f));
                PulleyPlatform b = MakePlatform(rig.transform, "PlatformB", new Vector3(4f, 0f, 0f));

                sys.platformA = a;
                sys.platformB = b;
                sys.wheel = wheel.transform;
                sys.rope = rope;

                PrefabUtility.SaveAsPrefabAsset(rig, PulleyPrefabPath);
                Object.DestroyImmediate(rig);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Project Juice] Prefabs ready: BreakableChain + PulleyRig in " + GameplayDir);
        }

        static PulleyPlatform MakePlatform(Transform parent, string name, Vector3 localPos)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholders/Square.png");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = square;
            sr.color = new Color(0.36f, 0.42f, 0.47f);
            sr.sortingOrder = 3;
            go.transform.localScale = new Vector3(2.4f, 0.4f, 1f);
            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            PulleyPlatform p = go.AddComponent<PulleyPlatform>();
            p.surfaceType = SurfaceType.Metal;
            return p;
        }

        // ---------------------------------------------------------- test areas

        [MenuItem("Project Juice/Add Chain+Pulley Test Areas")]
        public static void AddTestAreas()
        {
            BuildPrefabs();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(TestScenePath);

            GameObject world = GameObject.Find("World");
            if (world == null) { Debug.LogError("[Project Juice] No 'World' in MechanicsTest."); return; }

            GameObject lint = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Surfaces/Surface_Lint.prefab");
            GameObject metal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Surfaces/Surface_Metal.prefab");
            GameObject seal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Surfaces/BrittleSeal.prefab");
            GameObject chainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChainPrefabPath);
            GameObject pulleyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PulleyPrefabPath);

            // ---- H: breakable chain (fresh origin below everything else)
            if (GameObject.Find("World/Area_H  Breakable chain") == null)
            {
                float ox = 0f, oy = -100f;
                GameObject a = new GameObject("Area_H  Breakable chain");
                a.transform.SetParent(world.transform, false);
                Block(lint, new Vector2(ox, oy - 0.5f), new Vector2(26f, 1f), a.transform);
                Block(lint, new Vector2(ox - 13.5f, oy + 7f), new Vector2(1f, 17f), a.transform);
                Block(lint, new Vector2(ox + 6f, oy + 5f), new Vector2(1f, 11f), a.transform); // wall for the lower chain to hit
                Block(metal, new Vector2(ox - 3f, oy + 8f), new Vector2(10f, 0.6f), a.transform); // beam the chain hangs from
                GameObject chain = (GameObject)PrefabUtility.InstantiatePrefab(chainPrefab);
                chain.transform.SetParent(a.transform, false);
                chain.transform.position = new Vector3(ox - 3f, oy + 7.7f, 0f);
                chain.name = "Chain";
                GameObject tube = (GameObject)PrefabUtility.InstantiatePrefab(seal);
                tube.transform.SetParent(a.transform, false);
                tube.transform.position = new Vector3(ox + 8f, oy + 0.5f, 0f);
                tube.transform.localScale = new Vector3(2f, 1f, 1f);
                tube.name = "BrittleTube_VFX";
                BreakEffect tubeVfx = tube.AddComponent<BreakEffect>();
                tubeVfx.color = new Color(0.9f, 0.6f, 0.4f);
                AddMarker("H  Breakable chain", new Vector2(ox - 10f, oy + 0.4f), WormMaterial.Heavy,
                    "Launch into the chain: hinge breaks, lower falls, VFX at the snap. T restores it.", a.transform);
            }

            // ---- I: pulley (next to H)
            if (GameObject.Find("World/Area_I  Pulley") == null)
            {
                float ox = 40f, oy = -100f;
                GameObject a = new GameObject("Area_I  Pulley");
                a.transform.SetParent(world.transform, false);
                Block(lint, new Vector2(ox, oy - 0.5f), new Vector2(26f, 1f), a.transform);
                Block(lint, new Vector2(ox - 13.5f, oy + 7f), new Vector2(1f, 17f), a.transform);
                Block(lint, new Vector2(ox + 13.5f, oy + 7f), new Vector2(1f, 17f), a.transform);
                Block(lint, new Vector2(ox, oy + 15.5f), new Vector2(28f, 1f), a.transform);
                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(pulleyPrefab);
                rig.transform.SetParent(a.transform, false);
                rig.transform.position = new Vector3(ox, oy + 4f, 0f);
                rig.name = "PulleyRig";
                AddMarker("I  Pulley", new Vector2(ox - 4f, oy + 5.2f), WormMaterial.Squishy,
                    "You start above platform A. Stand: Bouncy sinks slowly, switch to Rocky (Space, even mid-air) to slam it down.", a.transform);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), TestScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Project Juice] Area_H (chain) + Area_I (pulley) appended to MechanicsTest. Press Play, jump with [ ] or digits.");
        }

        static GameObject Block(GameObject prefab, Vector2 center, Vector2 size, Transform parent)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        static void AddMarker(string name, Vector2 pos, WormMaterial mat, string desc, Transform parent)
        {
            GameObject go = new GameObject("Start_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            TestAreaMarker m = go.AddComponent<TestAreaMarker>();
            m.areaName = name;
            m.startMaterial = mat;
            m.description = desc;
        }
    }
}
#endif
