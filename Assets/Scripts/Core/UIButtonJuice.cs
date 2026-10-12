using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectJuice
{
    /// <summary>
    /// Feel of the result screen buttons. The button is a filled chalk box sitting on a coloured "base":
    /// it floats gently, lifts and grows on hover, and on click it is pushed down onto its base (and squashes),
    /// like a real push button.
    /// </summary>
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("Moves (float, grow, tilt). The face and the base are inside it.")]
        public RectTransform body;
        [Tooltip("The filled box with the text: pushed down onto the base on press.")]
        public RectTransform face;
        public Image faceImage;
        public Color faceColor = Color.white;
        public Color hoverColor = Color.white;
        [Tooltip("How high the face sits above its base (UI pixels).")]
        public float depth = 14f;
        [Tooltip("Offset of the idle float, so two buttons do not move together.")]
        public float phase;

        float hover, press, click;
        bool over, down;

        public void OnPointerEnter(PointerEventData e) { over = true; }
        public void OnPointerExit(PointerEventData e) { over = false; down = false; }
        public void OnPointerDown(PointerEventData e) { down = true; click = 1f; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        void Update()
        {
            if (body == null) return;
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            hover = Mathf.MoveTowards(hover, over ? 1f : 0f, dt * 7f);
            press = Mathf.MoveTowards(press, down ? 1f : 0f, dt * (down ? 30f : 9f));
            click = Mathf.MoveTowards(click, 0f, dt * 4f);

            float bob = Mathf.Sin(time * 2.4f + phase) * 5f * (1f - hover);
            body.anchoredPosition = new Vector2(0f, bob + 6f * hover);
            float k = 1f + 0.08f * hover;
            float squash = press * 0.06f + Mathf.Sin(click * Mathf.PI * 2f) * click * 0.05f;
            body.localScale = new Vector3(k * (1f + squash), k * (1f - squash), 1f);
            body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.6f + phase) * 1.5f * (1f - hover) + hover * 2.5f * Mathf.Sin(time * 7f));

            if (face != null) face.anchoredPosition = new Vector2(0f, depth * (0.5f - 0.85f * press)); // the base sits at -depth / 2
            if (faceImage != null) faceImage.color = Color.Lerp(faceColor, hoverColor, hover);
        }
    }
}
