using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Background image for a level. Attach to a GameObject with a SpriteRenderer.
    /// Put it under Level/Background, pick a Sorting Layer that renders behind gameplay
    /// (default layer "Background" if you created it, else Order in Layer negative),
    /// then position/scale it in the Scene view. For parallax later, add several of these
    /// at different depths and drive them from one small parallax script.
    /// Never put world backgrounds inside a UI Canvas.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteInEditMode]
    public class BackgroundLayer : MonoBehaviour
    {
        [Tooltip("Lower = further back. Gameplay should stay at 0 or above.")]
        public int orderInLayer = -10;
        [Tooltip("Optional: assign a Sorting Layer named e.g. Background once you create it in Project Settings > Tags and Layers.")]
        public string sortingLayerName = "";

        [Tooltip("Fit this sprite to the camera height once (keeps aspect). 0 = off.")]
        public float fitCameraHalfHeight = 0f;

        SpriteRenderer sr;

        void Awake() { Apply(); }
        void OnValidate() { Apply(); }

        public void Apply()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.sortingOrder = orderInLayer;
            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
            if (fitCameraHalfHeight > 0f && sr.sprite != null && Camera.main != null)
            {
                float h = sr.sprite.bounds.size.y;
                if (h > 0.001f) transform.localScale = Vector3.one * (Camera.main.orthographicSize * 2f / h);
            }
        }
    }
}
