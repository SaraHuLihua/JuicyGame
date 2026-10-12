using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectJuice
{
    /// <summary>Hover / press feel for the result screen buttons (grow, tilt, squash on click).</summary>
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        float hover, press;
        bool over, down;

        public void OnPointerEnter(PointerEventData e) { over = true; GameAudio.Play("tick", 0.35f, 1.4f); }
        public void OnPointerExit(PointerEventData e) { over = false; }
        public void OnPointerDown(PointerEventData e) { down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hover = Mathf.MoveTowards(hover, over ? 1f : 0f, dt * 6f);
            press = Mathf.MoveTowards(press, down ? 1f : 0f, dt * 12f);
            if (transform.localScale.x < 0.99f && hover <= 0f && press <= 0f) return; // still popping in
            float k = 1f + 0.12f * hover - 0.1f * press;
            transform.localScale = new Vector3(k * (1f + 0.05f * press), k * (1f - 0.05f * press), 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, hover * 3f * Mathf.Sin(Time.unscaledTime * 6f));
        }
    }
}
