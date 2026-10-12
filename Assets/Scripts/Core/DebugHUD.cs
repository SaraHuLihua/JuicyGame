using UnityEngine;

namespace ProjectJuice
{
    /// <summary>Simple on-screen debug readout (IMGUI, no canvas needed).</summary>
    public class DebugHUD : MonoBehaviour
    {
        [Tooltip("Off = no on-screen text. Re-enable from the Inspector when you need to debug.")]
        public bool show = false;
        public bool showAreaLabels = true;
        GUIStyle style;
        GUIStyle small;
        GUIStyle label;

        void OnGUI()
        {
            if (!show) return;
            WormController w = WormController.Instance;
            if (w == null) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                style.normal.textColor = Color.white;
                small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                small.normal.textColor = new Color(1, 1, 1, 0.85f);
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                label.normal.textColor = Color.yellow;
            }

            WormPhysics ph = w.Physics;
            string surface = ph.GroundSurfaceType.HasValue ? ph.GroundSurfaceType.Value.ToString() : "-";

            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.Box(new Rect(8, 8, 400, 168), GUIContent.none);
            GUI.color = Color.white;

            GUI.Label(new Rect(16, 12, 380, 26), "Launches: " + w.LaunchCount, style);
            GUI.Label(new Rect(16, 36, 380, 26), "Material: " + w.Material.ToString().ToUpper(), style);
            GUI.Label(new Rect(16, 60, 380, 26), "Grounded: " + (w.IsGrounded ? "YES" : "NO") + "    CanLaunch: " + (w.CanLaunch ? "YES" : "NO"), style);
            GUI.Label(new Rect(16, 84, 380, 22), "Speed: " + ph.Speed.ToString("0.0") + " u/s    Surface: " + surface + (w.Launcher.IsAiming ? "    AIMING " + Mathf.RoundToInt(w.Launcher.AimPower * 100f) + "%" : ""), small);
            GUI.Label(new Rect(16, 104, 390, 70), "LMB drag = aim/launch   RMB or Space = switch material\nR = respawn   T = reset area   [ ] or 1-9,0 = areas   Shift = angle snap\nRMB/Esc while aiming = cancel", small);

            if (showAreaLabels && Camera.main != null)
            {
                for (int i = 0; i < TestAreaMarker.All.Count; i++)
                {
                    TestAreaMarker m = TestAreaMarker.All[i];
                    Vector3 sp = Camera.main.WorldToScreenPoint(m.transform.position + Vector3.up * 5f);
                    if (sp.z < 0f) continue;
                    GUI.Label(new Rect(sp.x - 200, Screen.height - sp.y - 14, 400, 28), (i + 1) + ". " + m.areaName, label);
                }
            }
        }
    }
}
