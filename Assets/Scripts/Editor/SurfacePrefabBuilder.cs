#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ProjectJuice.EditorTools
{
    /// <summary>
    /// Builds relaunchable surface prefabs from your art.
    /// Menu: Project Juice > Create Relaunch Surfaces.
    /// Each prefab = SpriteRenderer (your art) + solid BoxCollider2D + Surface (Lint = grippy, allowsLanding).
    /// Standing on it the worm regains launch. Safe to run again (rebuilds the two prefabs).
    /// After building, tighten the collider: select the prefab, set BoxCollider2D Size/Offset to the
    /// visible part, or swap it for a PolygonCollider2D shaped in Sprite Editor > Physics Shape.
    /// </summary>
    public static class SurfacePrefabBuilder
    {
        struct Entry { public string spritePath; public string prefabPath; public string goName; }

        [MenuItem("Project Juice/Create Relaunch Surfaces")]
        public static void Build()
        {
            Entry[] entries = new Entry[]
            {
                new Entry { spritePath = "Assets/Art/Environment/Surface_Dark_01.png", prefabPath = "Assets/Prefabs/Environment/Surface_Dark_01.prefab", goName = "Surface_Dark_01" },
                new Entry { spritePath = "Assets/Art/Environment/Surface_Dark_02.png", prefabPath = "Assets/Prefabs/Environment/Surface_Dark_02.prefab", goName = "Surface_Dark_02" },
            };

            SurfaceProfile lint = AssetDatabase.LoadAssetAtPath<SurfaceProfile>("Assets/ScriptableObjects/Surfaces/Surface_Lint.asset");

            foreach (Entry e in entries)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(e.spritePath);
                if (sprite == null)
                {
                    // spriteMode Multiple: pick the first sub-sprite.
                    Object[] all = AssetDatabase.LoadAllAssetsAtPath(e.spritePath);
                    foreach (Object o in all) if (o is Sprite) { sprite = (Sprite)o; break; }
                }
                if (sprite == null)
                {
                    Debug.LogWarning("[Project Juice] Sprite not found: " + e.spritePath + " (is it imported as Sprite?)");
                    continue;
                }

                GameObject go = new GameObject(e.goName);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = Color.white;

                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                // Default to the visible pixels, not the whole texture with padding.
                box.size = sprite.bounds.size;
                box.offset = sprite.bounds.center;

                Surface surface = go.AddComponent<Surface>();
                surface.type = SurfaceType.Lint; // grippy + allowsLanding => relaunch
                surface.profileOverride = lint;

                System.IO.Directory.CreateDirectory("Assets/Prefabs/Environment");
                PrefabUtility.SaveAsPrefabAsset(go, e.prefabPath);
                Object.DestroyImmediate(go);
                Debug.Log("[Project Juice] Built " + e.prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
