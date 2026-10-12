using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Taut visual rope: Platform A -> pulley wheel -> Platform B, redrawn every frame.
    /// Visual only (no segment physics): stable, cheap, always connected. Uses a LineRenderer
    /// in world space; assign your rope material/sprite later, the default is a plain white line.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PulleyRope : MonoBehaviour
    {
        public float width = 0.12f;
        public Material ropeMaterial;
        public int sortingOrder = 4;

        LineRenderer line;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 3;
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.sortingOrder = sortingOrder;
            if (ropeMaterial != null) line.sharedMaterial = ropeMaterial;
            else line.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
        }

        void OnValidate()
        {
            if (line != null)
            {
                line.startWidth = width;
                line.endWidth = width;
                line.sortingOrder = sortingOrder;
                if (ropeMaterial != null) line.sharedMaterial = ropeMaterial;
            }
        }

        public void SetPoints(Vector2 a, Vector2 pulley, Vector2 b)
        {
            if (line == null) return;
            line.SetPosition(0, new Vector3(a.x, a.y, 0f));
            line.SetPosition(1, new Vector3(pulley.x, pulley.y, 0f));
            line.SetPosition(2, new Vector3(b.x, b.y, 0f));
        }
    }
}
