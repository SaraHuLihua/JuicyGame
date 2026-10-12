using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// One visual tube, two gameplay roles (Inspector-switchable, no duplicate assets).
    ///   Solid / Closed: interior blocks the worm (obstacle / wall).
    ///   Traversable / Open: only the walls collide; entrance/exit stay open so the worm can pass through.
    /// Setup: put wall colliders under a child called "Walls" and the blocking collider
    /// under a child called "Blocker". This script just toggles which group is active.
    /// If those children are missing it builds a default box pair so the prefab works out of the box.
    /// </summary>
    [ExecuteInEditMode]
    public class Tube : MonoBehaviour
    {
        public enum TubeType { Solid, Traversable }

        [Tooltip("Solid = closed tube (blocks passage). Traversable = open tube (worm can travel through).")]
        public TubeType tubeType = TubeType.Traversable;

        [Tooltip("Outer size of the tube in units. Used only when auto-building missing colliders.")]
        public Vector2 size = new Vector2(2f, 2f);
        [Tooltip("Thickness of each wall in units (auto-build only).")]
        public float wallThickness = 0.3f;

        GameObject wallsRoot;
        GameObject blockerRoot;
        TubeType applied = (TubeType)(-1);

        void Awake() { EnsureChildren(); Apply(); }
        void OnValidate() { EnsureChildren(); Apply(); }

        void EnsureChildren()
        {
            Transform w = transform.Find("Walls");
            Transform b = transform.Find("Blocker");
            if (w == null)
            {
                wallsRoot = new GameObject("Walls");
                wallsRoot.transform.SetParent(transform, false);
                BuildWalls(wallsRoot.transform);
            }
            else wallsRoot = w.gameObject;
            if (b == null)
            {
                blockerRoot = new GameObject("Blocker");
                blockerRoot.transform.SetParent(transform, false);
                var box = blockerRoot.AddComponent<BoxCollider2D>();
                box.size = size;
            }
            else blockerRoot = b.gameObject;
        }

        void BuildWalls(Transform parent)
        {
            // Two side walls with a gap in the middle (top/bottom open by default).
            // Designer can move/scale them to match the artwork; this is just a sane default.
            GameObject left = new GameObject("WallLeft");
            left.transform.SetParent(parent, false);
            var l = left.AddComponent<BoxCollider2D>();
            l.size = new Vector2(wallThickness, size.y);
            l.offset = new Vector2(-(size.x * 0.5f - wallThickness * 0.5f), 0f);

            GameObject right = new GameObject("WallRight");
            right.transform.SetParent(parent, false);
            var r = right.AddComponent<BoxCollider2D>();
            r.size = new Vector2(wallThickness, size.y);
            r.offset = new Vector2(size.x * 0.5f - wallThickness * 0.5f, 0f);
        }

        void Apply()
        {
            if (wallsRoot == null || blockerRoot == null) return;
            if (tubeType == applied && Application.isPlaying) return;
            applied = tubeType;
            // Walls always collide. Blocker only in Solid mode.
            wallsRoot.SetActive(true);
            blockerRoot.SetActive(tubeType == TubeType.Solid);
        }

        /// <summary>Switch role at runtime if a level needs it (doors, etc).</summary>
        public void SetTubeType(TubeType t) { tubeType = t; Apply(); }

        /// <summary>
        /// Sizes this tube from the artwork already parented under it (ignores Walls/Blocker colliders,
        /// which have no renderers). Call after composing a tube by hand so the default colliders match.
        /// </summary>
        public void ConfigureFromArt()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool hasAny = false;
            Bounds local = new Bounds();
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Bounds wb = r.bounds;
                Vector3[] corners = new Vector3[8];
                Vector3 c = wb.center, e = wb.extents;
                int k = 0;
                for (int ix = -1; ix <= 1; ix += 2)
                    for (int iy = -1; iy <= 1; iy += 2)
                        for (int iz = -1; iz <= 1; iz += 2)
                            corners[k++] = transform.InverseTransformPoint(c + new Vector3(e.x * ix, e.y * iy, e.z * iz));
                foreach (Vector3 p in corners)
                {
                    if (!hasAny) { local = new Bounds(p, Vector3.zero); hasAny = true; }
                    else local.Encapsulate(p);
                }
            }
            if (!hasAny) return;
            size = new Vector2(Mathf.Max(0.2f, local.size.x), Mathf.Max(0.2f, local.size.y));
            Vector3 ctr = local.center;
            EnsureChildren();
            // Rebuild walls/blocker to the measured size, centred on the art.
            System.Collections.Generic.List<GameObject> oldWalls = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in wallsRoot.transform) oldWalls.Add(child.gameObject);
            foreach (GameObject g in oldWalls) Object.DestroyImmediate(g);
            BuildWalls(wallsRoot.transform);
            BoxCollider2D blocker = blockerRoot.GetComponent<BoxCollider2D>();
            if (blocker == null) blocker = blockerRoot.AddComponent<BoxCollider2D>();
            blocker.size = size;
            blocker.offset = new Vector2(ctr.x, ctr.y);
            foreach (BoxCollider2D w in wallsRoot.GetComponentsInChildren<BoxCollider2D>())
                w.offset += new Vector2(ctr.x, ctr.y);
            Apply();
        }
    }
}
