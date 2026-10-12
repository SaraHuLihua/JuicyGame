using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    public enum AimRangeMode { Full, Upward, Custom }

    /// <summary>
    /// Slingshot aiming: press on the ground, drag back, release. Shows a dotted trajectory preview.
    /// Only works while the worm CanLaunch (grounded and settled), so there is no mid-air relaunch.
    /// </summary>
    public class WormLauncher : MonoBehaviour
    {
        [Header("Launch")]
        [Tooltip("Global multiplier on the per-material Min/Max Launch Speed (see the Worm Material Profiles).")]
        public float launchForceMultiplier = 1f;
        [Tooltip("Screen pixels of drag for full power. Web: 220.")]
        public float dragForMaxPower = 220f;
        [Tooltip("Multiplies the drag. >1 = shorter drag needed.")]
        public float aimSensitivity = 1f;
        [Tooltip("Drags shorter than this (pixels) do nothing. Web: 12.")]
        public float deadZone = 12f;

        [Header("Aim range")]
        [Tooltip("The web prototype only allowed upward shots. Full allows any direction.")]
        public AimRangeMode aimRange = AimRangeMode.Full;
        [Range(0f, 180f)] public float customMinAngle = 15f;
        [Range(0f, 180f)] public float customMaxAngle = 165f;
        [Tooltip("Hold Shift to snap the angle to this step (degrees). Web: 5.")]
        public float snapStep = 5f;

        [Header("Trajectory preview")]
        public bool showPreview = true;
        [Tooltip("Length of the preview path in units.")]
        public float trajectoryPreviewLength = 30f;
        [Range(0, 5)] public int previewBounces = 3;
        public float previewStep = 1f / 60f;
        public float dotSpacing = 0.55f;
        public float dotSize = 0.16f;

        WormController controller;
        bool aiming;
        Vector2 startPx;
        Vector2 currentPx;
        bool aimValid;
        Vector2 aimDir;
        float aimPower;
        int cancelFrame = -10;

        readonly List<Vector2> path = new List<Vector2>();
        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
        readonly List<Collider2D> ignored = new List<Collider2D>();
        SpriteRenderer band;
        Transform visualRoot;
        static readonly RaycastHit2D[] castHits = new RaycastHit2D[8];

        public bool IsAiming { get { return aiming; } }
        public bool CancelledThisFrame { get { return cancelFrame == Time.frameCount; } }
        public float AimPower { get { return aimPower; } }
        public Vector2 AimDirection { get { return aimDir; } }
        /// <summary>The current drag is long enough to be a real aim (outside the dead zone).</summary>
        public bool AimValid { get { return aiming && aimValid; } }

        void Awake()
        {
            controller = GetComponent<WormController>();
            GameObject root = new GameObject("AimPreview");
            root.transform.SetParent(transform, false);
            visualRoot = root.transform;
            band = PlaceholderSprites.Make("Band", visualRoot, PlaceholderSprites.Square, new Color(1, 1, 1, 0.8f), Vector2.zero, new Vector2(1, 0.12f), 30);
            band.enabled = false;
        }

        void Update()
        {
            if (!aiming)
            {
                if (GameInput.LeftDown && controller.CanLaunch) Begin();
                return;
            }

            if (GameInput.RightDown || GameInput.EscapeDown) { Cancel(); cancelFrame = Time.frameCount; return; }
            if (!controller.CanLaunch) { Cancel(); return; } // lost footing while aiming

            currentPx = GameInput.PointerPosition;
            UpdateAim();
            if (GameInput.LeftUp) Release();
        }

        void LateUpdate()
        {
            // The preview lives in world space; keep it from inheriting the worm's movement
            // AND from inheriting any scale on the worm root (dots/band would become giant).
            // IMPORTANT: keep the Worm root scale at 1,1,1 — size the art via WormVisual.visualSizeMultiplier
            // and the physics via WormMaterialProfile.radius instead.
            if (visualRoot != null)
            {
                visualRoot.rotation = Quaternion.identity;
                Vector3 ls = transform.lossyScale;
                visualRoot.localScale = new Vector3(
                    1f / Mathf.Max(0.001f, Mathf.Abs(ls.x)),
                    1f / Mathf.Max(0.001f, Mathf.Abs(ls.y)), 1f);
            }
        }

        void Begin()
        {
            aiming = true;
            startPx = GameInput.PointerPosition;
            currentPx = startPx;
            aimValid = false;
        }

        void Cancel()
        {
            aiming = false;
            aimValid = false;
            HidePreview();
        }

        void Release()
        {
            bool fire = aimValid && controller.CanLaunch;
            Vector2 d = aimDir;
            float p = aimPower;
            Cancel();
            if (fire) controller.PerformLaunch(d, p, launchForceMultiplier);
        }

        void UpdateAim()
        {
            Vector2 drag = (currentPx - startPx) * aimSensitivity;
            float len = drag.magnitude;
            if (len < deadZone) { aimValid = false; HidePreview(); return; }

            Vector2 dir = -drag / len; // slingshot: launch opposite to the pull
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            angle = ClampAngle(angle);
            if (GameInput.SnapHeld && snapStep > 0.01f) angle = ClampAngle(Mathf.Round(angle / snapStep) * snapStep);
            float rad = angle * Mathf.Deg2Rad;
            aimDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            aimPower = Mathf.Clamp01((len - deadZone) / Mathf.Max(1f, dragForMaxPower - deadZone));
            aimValid = true;

            DrawBand();
            if (showPreview) DrawPreview(); else HidePreview();
        }

        float ClampAngle(float angleDeg)
        {
            if (aimRange == AimRangeMode.Full) return angleDeg;
            float a = Mathf.Repeat(angleDeg, 360f);
            if (a > 180f) a = (a - 180f) < (360f - a) ? 180f : 0f; // lower half: snap to nearest horizontal
            if (aimRange == AimRangeMode.Custom) a = Mathf.Clamp(a, customMinAngle, customMaxAngle);
            return a;
        }

        // ------------------------------------------------------------------ drawing

        void DrawBand()
        {
            Vector2 origin = controller.Physics.Body.position;
            float length = 0.4f + aimPower * 1.8f;
            Vector2 end = origin - aimDir * length;
            Vector2 mid = (origin + end) * 0.5f;
            band.enabled = true;
            band.transform.position = new Vector3(mid.x, mid.y, 0f);
            band.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg);
            band.transform.localScale = new Vector3(length, 0.12f + aimPower * 0.1f, 1f);
            Color c = controller.Physics.Profile.color;
            band.color = Color.Lerp(new Color(1, 1, 1, 0.7f), new Color(c.r, c.g, c.b, 1f), aimPower);
        }

        void HidePreview()
        {
            if (band != null) band.enabled = false;
            for (int i = 0; i < dots.Count; i++) dots[i].enabled = false;
        }

        /// <summary>
        /// Simulates the shot step by step with the same bounce rules the worm uses (profile restitution,
        /// surface grip, brittle breaking). Static snapshot: moving mechanisms are not predicted.
        /// </summary>
        void DrawPreview()
        {
            WormPhysics ph = controller.Physics;
            WormMaterialProfile p = ph.Profile;
            Vector2 pos = ph.Body.position;
            Vector2 v = ph.PredictLaunchVelocity(aimDir, aimPower, launchForceMultiplier);
            Vector2 g = Physics2D.gravity * ph.Body.gravityScale;
            float r = ph.Collider.radius * 0.95f;

            path.Clear();
            ignored.Clear();
            path.Add(pos);
            float travelled = 0f;
            int bouncesLeft = previewBounces;

            for (int i = 0; i < 900 && travelled < trajectoryPreviewLength; i++)
            {
                v += g * previewStep;
                Vector2 step = v * previewStep;
                float dist = step.magnitude;
                if (dist < 1e-5f) break;
                Vector2 dir = step / dist;

                RaycastHit2D hit;
                if (CastFirst(ph, pos, r, dir, dist, out hit))
                {
                    Vector2 n = hit.normal;
                    float ns = -Vector2.Dot(v, n);
                    pos = hit.centroid;
                    path.Add(pos);
                    travelled += hit.distance;
                    if (ns <= 0.01f) { pos += step * 0.5f; continue; }

                    BreakableObject br = hit.collider.GetComponentInParent<BreakableObject>();
                    if (br != null && !br.IsBroken && br.WouldBreak(ph.Mass * ns, ph.Mass))
                    {
                        ignored.Add(hit.collider);
                        v *= p.breakSpeedKeep;
                        continue;
                    }

                    SurfaceProfile sp = Surface.ProfileOf(hit.collider);
                    float outSpeed = ns * p.GetRestitution(sp.type) * sp.restitutionMultiplier;
                    if (outSpeed < p.stopBounceSpeed) break; // it would land here
                    Vector2 tangent = new Vector2(-n.y, n.x);
                    float vt = Vector2.Dot(v, tangent);
                    if (ns > p.stopBounceSpeed && n.y > 0.5f) vt *= Mathf.Lerp(1f, p.landingTangentialKeep, sp.landingGrip);
                    v = tangent * vt + n * outSpeed;
                    pos += n * 0.02f;
                    if (--bouncesLeft < 0) break;
                }
                else
                {
                    pos += step;
                    travelled += dist;
                    if ((i & 1) == 0) path.Add(pos);
                }
            }

            // Lay dots at equal spacing along the polyline.
            int used = 0;
            float carry = 0f;
            Color c = p.color;
            for (int s = 1; s < path.Count; s++)
            {
                Vector2 a = path[s - 1];
                Vector2 b = path[s];
                float seg = Vector2.Distance(a, b);
                float d = dotSpacing - carry;
                while (d <= seg)
                {
                    Vector2 pt = Vector2.Lerp(a, b, d / seg);
                    SpriteRenderer dot = GetDot(used++);
                    dot.enabled = true;
                    dot.transform.position = new Vector3(pt.x, pt.y, 0f);
                    float fade = 1f - (float)used / 70f;
                    dot.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(fade) * 0.9f);
                    d += dotSpacing;
                    if (used > 90) break;
                }
                carry = seg - (d - dotSpacing);
                if (used > 90) break;
            }
            for (int i = used; i < dots.Count; i++) dots[i].enabled = false;
        }

        bool CastFirst(WormPhysics ph, Vector2 pos, float radius, Vector2 dir, float dist, out RaycastHit2D result)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = false;
            int n = Physics2D.CircleCast(pos, radius, dir, filter, castHits, dist);
            float best = float.MaxValue;
            result = default(RaycastHit2D);
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                Collider2D c = castHits[i].collider;
                if (c == null || c == ph.Collider || ignored.Contains(c)) continue;
                if (castHits[i].distance < best) { best = castHits[i].distance; result = castHits[i]; found = true; }
            }
            return found;
        }

        SpriteRenderer GetDot(int i)
        {
            while (dots.Count <= i)
            {
                SpriteRenderer sr = PlaceholderSprites.Make("Dot", visualRoot, PlaceholderSprites.Circle, Color.white, Vector2.zero, new Vector2(dotSize, dotSize), 29);
                dots.Add(sr);
            }
            return dots[i];
        }
    }
}
