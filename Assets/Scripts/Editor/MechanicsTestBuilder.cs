#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// Builds everything that cannot live in plain .cs files: ScriptableObject profiles, prefabs and the two scenes.
    /// Menu: Project Juice / Build All.  Safe to run again (profiles are kept, prefabs and scenes are rebuilt).
    /// </summary>
    public static class MechanicsTestBuilder
    {
        const string SceneDir = "Assets/Scenes";
        const string PrefabDir = "Assets/Prefabs";
        const string SoDir = "Assets/ScriptableObjects";
        const string ArtDir = "Assets/Art/Placeholders";

        static Sprite squareSprite;
        static Sprite circleSprite;

        [InitializeOnLoadMethod]
        static void AutoBuildOnce()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(SceneDir + "/MechanicsTest.unity"))
                {
                    Debug.Log("[Project Juice] MechanicsTest scene not found, building it now (Project Juice > Build All).");
                    BuildAll();
                }
            };
        }

        [MenuItem("Project Juice/Build All (profiles, prefabs, scenes)")]
        public static void BuildAll()
        {
            EnsureFolders();
            CreateSprites();
            CreateProfiles();
            CreatePrefabs();
            BuildMechanicsTestScene();
            BuildTestArenaScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Project Juice] Build finished. Open Assets/Scenes/MechanicsTest.unity and press Play.");
        }

        [MenuItem("Project Juice/Rebuild scenes only")]
        public static void RebuildScenes()
        {
            EnsureFolders();
            BuildMechanicsTestScene();
            BuildTestArenaScene();
        }

        // ------------------------------------------------------------------ folders / sprites / profiles

        static void EnsureFolders()
        {
            string[] dirs = { SceneDir, PrefabDir + "/Player", PrefabDir + "/Surfaces", PrefabDir + "/Mechanics", SoDir + "/Surfaces", SoDir + "/Worm", ArtDir, "Assets/Materials", "Assets/Audio" };
            for (int i = 0; i < dirs.Length; i++) Directory.CreateDirectory(dirs[i]);
            AssetDatabase.Refresh();
        }

        static void CreateSprites()
        {
            squareSprite = MakeSpriteAsset(ArtDir + "/Square.png", 4, false);
            circleSprite = MakeSpriteAsset(ArtDir + "/Circle.png", 64, true);
        }

        static Sprite MakeSpriteAsset(string path, int size, bool circle)
        {
            if (!File.Exists(path))
            {
                Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float a = 1f;
                        if (circle) a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)));
                        t.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spritePixelsPerUnit = size;      // 1 unit across
                ti.filterMode = circle ? FilterMode.Bilinear : FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static SurfaceProfile surfaceLint, surfaceMetal, surfaceMembrane, surfaceGrease, surfaceCilia, surfaceBrittle;
        static WormMaterialProfile wormSquishy, wormHeavy;

        static void CreateProfiles()
        {
            surfaceLint = SurfaceAsset(SurfaceType.Lint);
            surfaceMetal = SurfaceAsset(SurfaceType.Metal);
            surfaceMembrane = SurfaceAsset(SurfaceType.Membrane);
            surfaceGrease = SurfaceAsset(SurfaceType.Grease);
            surfaceCilia = SurfaceAsset(SurfaceType.Cilia);
            surfaceBrittle = SurfaceAsset(SurfaceType.Brittle);
            wormSquishy = WormAsset(WormMaterial.Squishy);
            wormHeavy = WormAsset(WormMaterial.Heavy);
        }

        static SurfaceProfile SurfaceAsset(SurfaceType t)
        {
            string path = SoDir + "/Surfaces/Surface_" + t + ".asset";
            SurfaceProfile p = AssetDatabase.LoadAssetAtPath<SurfaceProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<SurfaceProfile>();
                SurfaceProfile.FillDefaults(p, t);
                AssetDatabase.CreateAsset(p, path);
            }
            return p;
        }

        static WormMaterialProfile WormAsset(WormMaterial m)
        {
            string path = SoDir + "/Worm/Worm_" + m + ".asset";
            WormMaterialProfile p = AssetDatabase.LoadAssetAtPath<WormMaterialProfile>(path);
            if (p == null)
            {
                p = ScriptableObject.CreateInstance<WormMaterialProfile>();
                WormMaterialProfile.FillDefaults(p, m);
                AssetDatabase.CreateAsset(p, path);
            }
            return p;
        }

        static SurfaceProfile ProfileFor(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Lint: return surfaceLint;
                case SurfaceType.Metal: return surfaceMetal;
                case SurfaceType.Membrane: return surfaceMembrane;
                case SurfaceType.Grease: return surfaceGrease;
                case SurfaceType.Cilia: return surfaceCilia;
                default: return surfaceBrittle;
            }
        }

        // ------------------------------------------------------------------ prefabs

        static GameObject wormPrefab, brittlePrefab, pistonPrefab, chainPrefab, ciliaPrefab, jawsPrefab;
        static GameObject[] surfacePrefabs = new GameObject[6];

        static GameObject SavePrefab(GameObject go, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static SpriteRenderer AddSprite(GameObject go, Sprite sprite, Color color, int order)
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        static void CreatePrefabs()
        {
            // --- surfaces: 1x1 blocks, scale them in the scene
            SurfaceType[] types = { SurfaceType.Lint, SurfaceType.Metal, SurfaceType.Membrane, SurfaceType.Grease, SurfaceType.Cilia, SurfaceType.Brittle };
            for (int i = 0; i < 6; i++)
            {
                SurfaceType t = types[i];
                GameObject go = new GameObject("Surface_" + t);
                AddSprite(go, squareSprite, ProfileFor(t).color, 0);
                go.AddComponent<BoxCollider2D>().size = Vector2.one;
                Surface s = go.AddComponent<Surface>();
                s.type = t;
                s.profileOverride = ProfileFor(t);
                surfacePrefabs[i] = SavePrefab(go, PrefabDir + "/Surfaces/Surface_" + t + ".prefab");
            }

            // --- brittle seal (breakable)
            {
                GameObject go = new GameObject("BrittleSeal");
                AddSprite(go, squareSprite, surfaceBrittle.color, 1);
                go.AddComponent<BoxCollider2D>().size = Vector2.one;
                Surface s = go.AddComponent<Surface>();
                s.type = SurfaceType.Brittle;
                s.profileOverride = surfaceBrittle;
                BreakableObject b = go.AddComponent<BreakableObject>();
                b.breakThreshold = 20f;
                brittlePrefab = SavePrefab(go, PrefabDir + "/Surfaces/BrittleSeal.prefab");
            }

            // --- worm
            {
                GameObject go = new GameObject("Worm");
                Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                rb.freezeRotation = true;
                CircleCollider2D cc = go.AddComponent<CircleCollider2D>();
                cc.radius = 12f / 32f;
                go.AddComponent<GroundDetector>();
                WormPhysics wp = go.AddComponent<WormPhysics>();
                wp.squishyProfile = wormSquishy;
                wp.heavyProfile = wormHeavy;
                go.AddComponent<LaunchCounter>();
                go.AddComponent<WormLauncher>();
                go.AddComponent<WormController>();

                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(go.transform, false);
                visual.AddComponent<WormVisual>();
                wormPrefab = SavePrefab(go, PrefabDir + "/Player/Worm.prefab");
            }

            // --- piston
            {
                GameObject root = new GameObject("Piston");
                GameObject baseGo = new GameObject("Base");
                baseGo.transform.SetParent(root.transform, false);
                baseGo.transform.localPosition = new Vector3(0f, -0.7f, 0f);
                baseGo.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
                AddSprite(baseGo, squareSprite, new Color(0.23f, 0.25f, 0.28f), 1);
                baseGo.AddComponent<BoxCollider2D>().size = Vector2.one;
                Surface bs = baseGo.AddComponent<Surface>(); bs.type = SurfaceType.Metal; bs.profileOverride = surfaceMetal;

                GameObject head = new GameObject("Head");
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                head.transform.localScale = new Vector3(1.2f, 0.4f, 1f);
                SpriteRenderer hs = AddSprite(head, squareSprite, new Color(0.36f, 0.42f, 0.47f), 2);
                head.AddComponent<BoxCollider2D>().size = Vector2.one;
                Rigidbody2D hrb = head.AddComponent<Rigidbody2D>();
                hrb.bodyType = RigidbodyType2D.Kinematic;
                Surface hsf = head.AddComponent<Surface>(); hsf.type = SurfaceType.Metal; hsf.profileOverride = surfaceMetal;

                Piston p = root.AddComponent<Piston>();
                p.head = hrb;
                pistonPrefab = SavePrefab(root, PrefabDir + "/Mechanics/Piston.prefab");
            }

            // --- carrier chain
            {
                GameObject go = new GameObject("CarrierChain");
                BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(8f, 0.5f);
                Surface s = go.AddComponent<Surface>(); s.type = SurfaceType.Metal; s.profileOverride = surfaceMetal;
                go.AddComponent<CarrierChain>();
                chainPrefab = SavePrefab(go, PrefabDir + "/Mechanics/CarrierChain.prefab");
            }

            // --- cilia carpet
            {
                GameObject go = new GameObject("CiliaCarpet");
                BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(8f, 0.4f);
                Surface s = go.AddComponent<Surface>(); s.type = SurfaceType.Cilia; s.profileOverride = surfaceCilia;
                go.AddComponent<CiliaCarpet>();
                ciliaPrefab = SavePrefab(go, PrefabDir + "/Mechanics/CiliaCarpet.prefab");
            }

            // --- roller jaws (rollers are generated at runtime from the inspector values)
            {
                GameObject go = new GameObject("RollerJaws");
                go.AddComponent<RollerJaws>();
                jawsPrefab = SavePrefab(go, PrefabDir + "/Mechanics/RollerJaws.prefab");
            }
        }

        // ------------------------------------------------------------------ scenes

        static GameObject Place(GameObject prefab, Vector2 pos, Vector2? size, Transform parent)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            if (size.HasValue) go.transform.localScale = new Vector3(size.Value.x, size.Value.y, 1f);
            return go;
        }

        static GameObject Block(SurfaceType t, Vector2 center, Vector2 size, Transform parent)
        {
            GameObject go = Place(surfacePrefabs[(int)t], center, size, parent);
            return go;
        }

        static void SetupCommon(Scene scene)
        {
            GameObject sys = new GameObject("Systems");
            sys.AddComponent<GameBootstrap>();
            sys.AddComponent<DebugHUD>();
            sys.AddComponent<DebugTools>();

            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.32f);
            camGo.transform.position = new Vector3(0f, 3f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CameraFollow2D>();
        }

        static GameObject AddMarker(string name, Vector2 pos, WormMaterial mat, string desc, Transform parent)
        {
            GameObject go = new GameObject("Start_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            TestAreaMarker m = go.AddComponent<TestAreaMarker>();
            m.areaName = name;
            m.startMaterial = mat;
            m.description = desc;
            return go;
        }

        /// <summary>Walls + a floor of the given surface, 26 wide, 16 tall.</summary>
        static GameObject Arena(string name, float ox, SurfaceType floor, Transform root)
        {
            GameObject a = new GameObject("Area_" + name);
            a.transform.SetParent(root, false);
            Transform t = a.transform;
            Block(floor, new Vector2(ox, -0.5f), new Vector2(26f, 1f), t);
            Block(SurfaceType.Lint, new Vector2(ox - 13.5f, 7f), new Vector2(1f, 17f), t);
            Block(SurfaceType.Lint, new Vector2(ox + 13.5f, 7f), new Vector2(1f, 17f), t);
            Block(SurfaceType.Lint, new Vector2(ox, 15.5f), new Vector2(28f, 1f), t);
            return a;
        }

        static void BuildMechanicsTestScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupCommon(scene);

            GameObject world = new GameObject("World");
            const float spacing = 30f;
            int idx = 0;
            float ox;

            // A / B: launch -> lint -> landing
            for (int k = 0; k < 2; k++)
            {
                WormMaterial m = k == 0 ? WormMaterial.Squishy : WormMaterial.Heavy;
                ox = idx * spacing;
                GameObject a = Arena(k == 0 ? "A  Squishy -> Lint" : "B  Heavy -> Lint", ox, SurfaceType.Lint, world.transform);
                Block(SurfaceType.Lint, new Vector2(ox + 2f, 1f), new Vector2(4f, 2f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox + 8f, 2f), new Vector2(4f, 4f), a.transform);
                AddMarker(k == 0 ? "A  Squishy -> Lint" : "B  Heavy -> Lint", new Vector2(ox - 10f, 0.4f), m, "Launch, land, check settle and bounce on Lint.", a.transform);
                idx++;
            }

            // C / D: vs metal
            for (int k = 0; k < 2; k++)
            {
                WormMaterial m = k == 0 ? WormMaterial.Squishy : WormMaterial.Heavy;
                ox = idx * spacing;
                string n = k == 0 ? "C  Squishy vs Metal" : "D  Heavy vs Metal";
                GameObject a = Arena(n, ox, SurfaceType.Metal, world.transform);
                Block(SurfaceType.Metal, new Vector2(ox + 4f, 4f), new Vector2(1f, 8f), a.transform);
                Block(SurfaceType.Metal, new Vector2(ox - 2f, 11f), new Vector2(10f, 1f), a.transform);
                AddMarker(n, new Vector2(ox - 10f, 0.4f), m, "Shoot at the metal wall and ceiling: compare the bounce.", a.transform);
                idx++;
            }

            // E: membrane
            ox = idx * spacing;
            {
                GameObject a = Arena("E  Squishy vs Membrane", ox, SurfaceType.Lint, world.transform);
                Block(SurfaceType.Membrane, new Vector2(ox + 1f, 0.25f), new Vector2(7f, 0.5f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox - 8f, 3f), new Vector2(4f, 6f), a.transform);
                AddMarker("E  Squishy vs Membrane", new Vector2(ox - 8f, 6.4f), WormMaterial.Squishy, "Bounce on the membrane. It never gives launch control (rescue after 2 s).", a.transform);
                idx++;
            }

            // F: material switching
            ox = idx * spacing;
            {
                GameObject a = Arena("F  Material switching", ox, SurfaceType.Lint, world.transform);
                Block(SurfaceType.Metal, new Vector2(ox - 6f, 0.15f), new Vector2(8f, 0.3f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox - 8f, 4f), new Vector2(4f, 8f), a.transform);
                Block(SurfaceType.Membrane, new Vector2(ox + 6f, 0.25f), new Vector2(5f, 0.5f), a.transform);
                AddMarker("F  Material switching", new Vector2(ox - 8f, 8.4f), WormMaterial.Squishy, "Space / right click switches only while grounded. Drop from the ledge as each material; try it in the air and on the membrane.", a.transform);
                idx++;
            }

            // G: brittle
            ox = idx * spacing;
            {
                GameObject a = Arena("G  Brittle objects", ox, SurfaceType.Lint, world.transform);
                float[] thresholds = { 16f, 30f, 50f };
                for (int i = 0; i < 3; i++)
                {
                    GameObject s = Place(brittlePrefab, new Vector2(ox - 3f + i * 4f, 2.5f), new Vector2(0.6f, 5f), a.transform);
                    s.GetComponent<BreakableObject>().breakThreshold = thresholds[i];
                    s.name = "BrittleSeal_threshold" + thresholds[i];
                    float shade = 0.8f - i * 0.2f;
                    s.GetComponent<SpriteRenderer>().color = new Color(shade, shade * 0.9f, shade * 0.6f);
                }
                AddMarker("G  Brittle objects", new Vector2(ox - 10f, 0.4f), WormMaterial.Heavy, "Thresholds 16 / 30 / 50 (impact force = mass x speed). Compare Squishy and Heavy.", a.transform);
                idx++;
            }

            // H: cilia
            ox = idx * spacing;
            {
                GameObject a = new GameObject("Area_H  Cilia carpet");
                a.transform.SetParent(world.transform, false);
                // floor split: lint - carpet - lint
                Block(SurfaceType.Lint, new Vector2(ox - 9f, -0.5f), new Vector2(8f, 1f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox + 9f, -0.5f), new Vector2(8f, 1f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox, -2.5f), new Vector2(10f, 1f), a.transform);
                Place(ciliaPrefab, new Vector2(ox, -0.2f), null, a.transform).GetComponent<BoxCollider2D>().size = new Vector2(10f, 0.4f);
                Block(SurfaceType.Lint, new Vector2(ox - 13.5f, 7f), new Vector2(1f, 17f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox + 13.5f, 7f), new Vector2(1f, 17f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox, 15.5f), new Vector2(28f, 1f), a.transform);
                AddMarker("H  Cilia carpet", new Vector2(ox - 10f, 0.4f), WormMaterial.Squishy, "Land on the carpet: Squishy is carried, Heavy mostly stays put.", a.transform);
                idx++;
            }

            // I: piston
            ox = idx * spacing;
            {
                GameObject a = Arena("I  Piston", ox, SurfaceType.Lint, world.transform);
                GameObject pis = Place(pistonPrefab, new Vector2(ox - 1f, 0.7f), null, a.transform);
                pis.GetComponent<Piston>().kickAngleOffset = -35f; // tilt the kick towards the high ledge
                Block(SurfaceType.Lint, new Vector2(ox + 8f, 3f), new Vector2(10f, 6f), a.transform);
                AddMarker("I  Piston", new Vector2(ox - 10f, 0.4f), WormMaterial.Squishy, "Land on the head while it hisses/strikes for a free launch. Try Heavy: much shorter kick.", a.transform);
                idx++;
            }

            // J: carrier chain
            ox = idx * spacing;
            {
                GameObject a = Arena("J  Carrier chain", ox, SurfaceType.Lint, world.transform);
                Block(SurfaceType.Lint, new Vector2(ox, 0.25f), new Vector2(8f, 0.5f), a.transform); // plinth under chain
                Place(chainPrefab, new Vector2(ox, 0.75f), null, a.transform);
                AddMarker("J  Carrier chain", new Vector2(ox - 10f, 0.4f), WormMaterial.Squishy, "Land on the chain, ride it, launch: you inherit its speed.", a.transform);
                idx++;
            }

            // K: roller jaws
            ox = idx * spacing;
            {
                GameObject a = Arena("K  Roller jaws", ox, SurfaceType.Lint, world.transform);
                Block(SurfaceType.Lint, new Vector2(ox - 9f, 2.25f), new Vector2(8f, 4.5f), a.transform);
                Block(SurfaceType.Lint, new Vector2(ox + 9f, 2.25f), new Vector2(8f, 4.5f), a.transform);
                Place(jawsPrefab, new Vector2(ox, 5.0f), null, a.transform);
                AddMarker("K  Roller jaws", new Vector2(ox - 10f, 4.9f), WormMaterial.Heavy, "Wait for the gap and launch through. Touch both rollers = crushed (respawn at last landing).", a.transform);
                idx++;
            }

            // worm at the first area
            GameObject worm = (GameObject)PrefabUtility.InstantiatePrefab(wormPrefab);
            worm.transform.position = new Vector3(-10f, 0.4f, 0f);

            string path = SceneDir + "/MechanicsTest.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
        }

        static void BuildTestArenaScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupCommon(scene);
            GameObject world = new GameObject("World");
            GameObject a = Arena("TestArena", 0f, SurfaceType.Lint, world.transform);
            Block(SurfaceType.Lint, new Vector2(-4f, 2f), new Vector2(5f, 4f), a.transform);
            Block(SurfaceType.Metal, new Vector2(4f, 3f), new Vector2(4f, 0.6f), a.transform);
            Block(SurfaceType.Membrane, new Vector2(9f, 0.25f), new Vector2(4f, 0.5f), a.transform);
            GameObject worm = (GameObject)PrefabUtility.InstantiatePrefab(wormPrefab);
            worm.transform.position = new Vector3(-9f, 0.4f, 0f);
            string path = SceneDir + "/TestArena.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddToBuildSettings(path);
            // leave MechanicsTest open
            EditorSceneManager.OpenScene(SceneDir + "/MechanicsTest.unity");
        }

        static void AddToBuildSettings(string path)
        {
            EditorBuildSettingsScene[] old = EditorBuildSettings.scenes;
            for (int i = 0; i < old.Length; i++) if (old[i].path == path) return;
            EditorBuildSettingsScene[] next = new EditorBuildSettingsScene[old.Length + 1];
            for (int i = 0; i < old.Length; i++) next[i] = old[i];
            next[old.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = next;
        }
    }
}
#endif
