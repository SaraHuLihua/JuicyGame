#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// Two menus. Run them in order, with the project open in the editor:
    ///   1. Project Juice > Build Chain+Pulley Prefabs  (creates Prefabs/Gameplay/Chain + PulleyRig if missing)
    ///   2. Project Juice > Add Chain+Pulley Test Areas (appends Area_H + Area_I to MechanicsTest.unity)
    /// Both are safe to run again (prefabs are rebuilt, test areas are skipped if already present).
    /// </summary>
    public static class ChainPulleySetup
    {
        const string GameplayDir = "Assets/Prefabs/Gameplay";
        const string ChainPrefabPath = "Assets/Prefabs/Gameplay/Chain.prefab";
        const string PropsSheet2 = "Assets/Art/pieces/FG_Props_2.png";
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
                GameObject go = new GameObject("Chain");
                BreakableObject br = go.AddComponent<BreakableObject>();
                br.breakThreshold = 15f;
                br.shardCount = 0;
                br.disableCollidersOnBreak = false;
                br.hideSpriteOnBreak = false;
                BreakEffect fx = go.AddComponent<BreakEffect>();
                fx.color = new Color(0.12f, 0.12f, 0.13f);
                fx.shardCount = 10;
                BreakableChain chain = go.AddComponent<BreakableChain>();
                chain.linkCount = 12;
                chain.linkLength = 0.45f;
                chain.breakAtLink = 6;
                chain.ringSprites = SheetSprites(PropsSheet2, "FG_Props_2_11", "FG_Props_2_12", "FG_Props_2_14");
                chain.barSprites = SheetSprites(PropsSheet2, "FG_Props_2_8", "FG_Props_2_9", "FG_Props_2_10");
                chain.breakEffect = fx;
                go.AddComponent<ChainSounds>();
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

                // Pulley wheel art: FG_Props_1_40 (big spoked wheel), drawn 1.2 units wide. Placeholder circle if missing.
                Sprite[] wheelArt = SheetSprites("Assets/Art/pieces/FG_Props_1.png", "FG_Props_1_40");
                Sprite circle = wheelArt.Length > 0 ? wheelArt[0] : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Placeholders/Circle.png");
                GameObject wheel = new GameObject("Wheel");
                wheel.transform.SetParent(rig.transform, false);
                wheel.transform.localPosition = new Vector3(0f, 2f, 0f);
                SpriteRenderer wheelSr = wheel.AddComponent<SpriteRenderer>();
                wheelSr.sprite = circle;
                wheelSr.color = wheelArt.Length > 0 ? Color.white : new Color(0.55f, 0.58f, 0.62f);
                wheelSr.sortingOrder = 5;
                float artWidth = circle != null ? circle.rect.width / circle.pixelsPerUnit : 1f;
                wheel.transform.localScale = Vector3.one * (1.2f / Mathf.Max(0.01f, artWidth));

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

        static Sprite[] SheetSprites(string sheet, params string[] names)
        {
            Object[] all = AssetDatabase.LoadAllAssetsAtPath(sheet);
            System.Collections.Generic.List<Sprite> found = new System.Collections.Generic.List<Sprite>();
            foreach (string n in names)
                foreach (Object o in all)
                    if (o is Sprite && o.name == n) { found.Add((Sprite)o); break; }
            return found.ToArray();
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
                // short, long (breaks near the wall), angled (pinned at both ends)
                PlaceChain(chainPrefab, a.transform, "Chain_Short", new Vector2(ox - 7f, oy + 7.7f), 0f, 6, 3, BreakableChain.EndMode.Free);
                PlaceChain(chainPrefab, a.transform, "Chain_Long", new Vector2(ox - 1f, oy + 7.7f), 0f, 14, 7, BreakableChain.EndMode.Free);
                PlaceChain(chainPrefab, a.transform, "Chain_Angled", new Vector2(ox + 3f, oy + 7.7f), 35f, 9, 5, BreakableChain.EndMode.PinnedInPlace);
                GameObject tube = (GameObject)PrefabUtility.InstantiatePrefab(seal);
                tube.transform.SetParent(a.transform, false);
                tube.transform.position = new Vector3(ox + 8f, oy + 0.5f, 0f);
                tube.transform.localScale = new Vector3(2f, 1f, 1f);
                tube.name = "BrittleTube_VFX";
                BreakEffect tubeVfx = tube.AddComponent<BreakEffect>();
                tubeVfx.color = new Color(0.9f, 0.6f, 0.4f);
                AddMarker("H  Breakable chain", new Vector2(ox - 10f, oy + 0.4f), WormMaterial.Heavy,
                    "Hit a chain as Rocky: it snaps at its break point (red X in Scene view), the lower part falls against the wall. T restores.", a.transform);
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

        static void PlaceChain(GameObject prefab, Transform parent, string name, Vector2 top, float angle, int links, int breakAt, BreakableChain.EndMode bottom)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(top.x, top.y, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.name = name;
            BreakableChain c = go.GetComponent<BreakableChain>();
            c.linkCount = links;
            c.breakAtLink = breakAt;
            c.bottom = bottom;
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
