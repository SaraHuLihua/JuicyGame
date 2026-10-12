#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// Project Juice > Create Level Template Structure.
    /// Builds the standard hierarchy in the OPEN scene (nothing is deleted):
    ///   Level / Background, Gameplay, Environment, Player, Foreground, UI
    /// Plus: Project Juice > Create Tube Prefab (Solid/Traversable example in Assets/Prefabs/Gameplay).
    /// Run these once, then build levels by dragging assets under the right group.
    /// </summary>
    public static class LevelTemplateBuilder
    {
        [MenuItem("Project Juice/Create Level Template Structure")]
        public static void CreateStructure()
        {
            GameObject level = GameObject.Find("Level");
            if (level == null) { level = new GameObject("Level"); Undo.RegisterCreatedObjectUndo(level, "Create Level"); }

            EnsureChild(level, "Background");
            EnsureChild(level, "Gameplay");
            EnsureChild(level, "Environment");
            EnsureChild(level, "Player");
            EnsureChild(level, "Foreground");

            GameObject ui = GameObject.Find("UI");
            if (ui == null)
            {
                ui = new GameObject("UI");
                Undo.RegisterCreatedObjectUndo(ui, "Create UI");
            }
            if (ui.GetComponent<Canvas>() == null)
            {
                Canvas c = ui.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                // uGUI components by name (no hard compile dependency on com.unity.ugui):
                // if the package is installed they are added, otherwise you still get a working Canvas.
                AddUiComponentByName(ui, "UnityEngine.UI.CanvasScaler, UnityEngine.UI");
                AddUiComponentByName(ui, "UnityEngine.UI.GraphicRaycaster, UnityEngine.UI");
            }

            // Make sure the main camera is 2D.
            if (Camera.main != null)
            {
                Camera.main.orthographic = true;
                if (Camera.main.orthographicSize < 1f) Camera.main.orthographicSize = 7f;
                if (Camera.main.GetComponent<CameraFollow2D>() == null)
                    Camera.main.gameObject.AddComponent<CameraFollow2D>();
            }

            Debug.Log("[Project Juice] Level structure ready: Level/{Background,Gameplay,Environment,Player,Foreground} + UI Canvas + 2D camera. Put background sprites under Background (add BackgroundLayer), worm prefab instance under Player.");
        }

        [MenuItem("Project Juice/Create Tube Prefab")]
        public static void CreateTubePrefab()
        {
            const string path = "Assets/Prefabs/Gameplay/Tube.prefab";
            GameObject go = new GameObject("Tube");
            go.AddComponent<Tube>().tubeType = Tube.TubeType.Traversable;
            // Children are auto-built by Tube.Awake/OnValidate; force them now.
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(go.transform, false);
            GameObject wl = new GameObject("WallLeft"); wl.transform.SetParent(walls.transform, false);
            var l = wl.AddComponent<BoxCollider2D>(); l.size = new Vector2(0.3f, 2f); l.offset = new Vector2(-0.85f, 0f);
            GameObject wr = new GameObject("WallRight"); wr.transform.SetParent(walls.transform, false);
            var r = wr.AddComponent<BoxCollider2D>(); r.size = new Vector2(0.3f, 2f); r.offset = new Vector2(0.85f, 0f);
            GameObject blocker = new GameObject("Blocker"); blocker.transform.SetParent(go.transform, false);
            var b = blocker.AddComponent<BoxCollider2D>(); b.size = new Vector2(2f, 2f);
            blocker.SetActive(false);

            System.IO.Directory.CreateDirectory("Assets/Prefabs/Gameplay");
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.Refresh();
            Debug.Log("[Project Juice] Tube prefab created at " + path + ". Inspector > Tube Type = Solid (blocks) or Traversable (pass through).");
        }

        static void AddUiComponentByName(GameObject go, string qualifiedTypeName)
        {
            System.Type t = System.Type.GetType(qualifiedTypeName);
            if (t == null) return; // com.unity.ugui not installed: Canvas alone still works
            if (go.GetComponent(t) == null) Undo.AddComponent(go, t);
        }

        static GameObject EnsureChild(GameObject parent, string name)
        {
            Transform t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }
    }
}
#endif
