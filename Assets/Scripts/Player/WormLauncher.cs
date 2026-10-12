using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    public enum AimRangeMode { Full, Upward, Custom }
    public enum AimArcPlacement { Behind, Under, Off }

    /// <summary>
    /// Slingshot aiming: press anywhere on the screen, drag back, release. Shows a white chalk trajectory
    /// (dots + a ring where it lands), a slingshot pocket behind the worm and an arrow in the launch direction
    /// (sprites from Art/UI/Untitled_Artwork 14).
    /// Only launches while the worm CanLaunch (grounded and settled), so there is no mid-air relaunch;
    /// a press made while it is still landing is kept and the aim starts as soon as it can launch.
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
        [Tooltip("Most physics casts the preview may do in one update. Lower = cheaper, shorter preview on fast shots.")]
        [Min(10)] public int maxPreviewCasts = 240;
        [Tooltip("Seconds between preview re-simulations while the aim does not change (moving mechanisms).")]
        [Min(0f)] public float previewRefresh = 0.1f;
        public float dotSpacing = 0.55f;
        [Tooltip("Size of the first dot (units). The dots shrink toward the end of the path.")]
        public float dotSize = 0.16f;
        [Tooltip("Size of the last dot compared to the first.")]
        [Range(0.1f, 1f)] public float dotEndScale = 0.45f;
        [Tooltip("Chalk dots used in turn along the path (Untitled_Artwork 14_0 and _1). Empty = plain circles.")]
        public Sprite[] trajectoryDotSprites;
        [Tooltip("Chalk ring drawn where the worm will land (Untitled_Artwork 14_3). Empty = none.")]
        public Sprite landingMarkerSprite;
        [Tooltip("Ring size compared to the worm's diameter.")]
        public float landingMarkerSize = 1.6f;
        public Color trajectoryColor = new Color(1f, 1f, 1f, 0.95f);

        [Header("Aim feedback (arc + arrow)")]
        [Tooltip("Half-arc drawn while aiming (Untitled_Artwork 14_4). Empty = the old white band.")]
        public Sprite aimArcSprite;
        [Tooltip("Arrow pointing where the worm will go (Untitled_Artwork 14_5).")]
        public Sprite aimArrowSprite;
        public Color aimFeedbackColor = Color.white;
        [Tooltip("Arc width (units) at no power and at full power.")]
        public Vector2 arcWidth = new Vector2(1.6f, 2.4f);
        [Tooltip("Behind = slingshot pocket on the pull side, open toward the launch, pulled back with power. Under = cradle under the worm. Off = no arc.")]
        public AimArcPlacement arcPlacement = AimArcPlacement.Behind;
        [Tooltip("Distance of the arc from the worm's centre (units), behind it or under it.")]
        public float arcDrop = 0.2f;
        [Tooltip("Behind only: how much further back the pocket is pulled at full power (units).")]
        public float arcPullBack = 0.5f;
        [Tooltip("Behind only: how much deeper the pocket stretches at full power (0.6 = 60%).")]
        [Range(0f, 1.5f)] public float arcStretch = 0.6f;
        [Tooltip("Arrow length (units) at no power and at full power.")]
        public Vector2 arrowLength = new Vector2(0.7f, 1.9f);
        [Tooltip("Gap between the worm's centre and the arrow's tail (units).")]
        public float arrowGap = 0.75f;
        [Tooltip("Arrow thickness compared to the drawn arrow (1 = as drawn).")]
        public float arrowWidth = 1.3f;
        [Tooltip("Pulse of the arc and arrow at full power (0 = none).")]
        [Range(0f, 0.3f)] public float fullPowerPulse = 0.08f;
        public int feedbackSortingOrder = 31;

        WormController controller;
        bool aiming;
        Vector2 startPx;
        Vector2 currentPx;
        bool aimValid;
        Vector2 aimDir;
        float aimPower;
        int cancelFrame = -10;
        bool pressHeld;
        Vector2 pressPx;
        SpriteRenderer arc;
        SpriteRenderer arrow;
        SpriteRenderer landing;
        bool lands;
        Vector2 landPoint;
        // Last simulated aim: the preview is only re-simulated when it changes (or every Preview Refresh).
        Vector2 simDir;
        float simPower = -1f;
        Vector2 simPos;
        float simTime;

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
            arc = MakeFeedback("AimArc", aimArcSprite);
            arrow = MakeFeedback("AimArrow", aimArrowSprite);
            landing = MakeFeedback("AimLanding", landingMarkerSprite);
            if (landing != null) landing.sortingOrder = feedbackSortingOrder - 2;
        }

        SpriteRenderer MakeFeedback(string n, Sprite s)
        {
            if (s == null) return null;
            GameObject go = new GameObject(n);
            go.transform.SetParent(visualRoot, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = aimFeedbackColor;
            sr.sortingOrder = feedbackSortingOrder;
            sr.enabled = false;
            return sr;
        }

        void Update()
        {
            if (!aiming)
            {
                // The press can happen anywhere and a little early (still landing): it is kept while held.
                if (GameInput.LeftDown) { pressHeld = true; pressPx = GameInput.PointerPosition; }
                if (GameInput.RightDown || GameInput.EscapeDown) pressHeld = false;
                if (pressHeld && controller.CanLaunch) { Begin(pressPx); pressHeld = false; }
                if (GameInput.LeftUp) pressHeld = false;
                if (aiming && GameInput.LeftUp) Cancel(); // pressed and let go before it could launch
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

        void Begin(Vector2 fromPx)
        {
            aiming = true;
            startPx = fromPx;
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

            if (arc != null || arrow != null) DrawFeedback(); else DrawBand();
            if (showPreview) DrawPreview();
            else { for (int i = 0; i < dots.Count; i++) dots[i].enabled = false; if (landing != null) landing.enabled = false; }
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

        void DrawFeedback()
        {
            Vector2 origin = controller.Physics.Body.position;
            float pulse = aimPower >= 0.999f && fullPowerPulse > 0f ? 1f + fullPowerPulse * Mathf.Sin(Time.time * 18f) : 1f;
            if (arc != null && arcPlacement != AimArcPlacement.Off)
            {
                float w = Mathf.Lerp(arcWidth.x, arcWidth.y, aimPower) * pulse;
                float k = w / Mathf.Max(0.01f, arc.sprite.bounds.size.x);
                arc.enabled = true;
                arc.color = aimFeedbackColor;
                if (arcPlacement == AimArcPlacement.Behind)
                {
                    // Slingshot pocket: hugs the back of the worm on the pull side, open toward the launch,
                    // and is pulled back and stretched deeper as the power grows (like the rubber of a slingshot).
                    arc.transform.localScale = new Vector3(k, k * (1f + arcStretch * aimPower), 1f);
                    arc.transform.position = origin - aimDir * (arcDrop + arcPullBack * aimPower);
                    arc.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aimDir.x, -aimDir.y) * Mathf.Rad2Deg); // dome top away from the aim
                }
                else
                {
                    arc.transform.localScale = new Vector3(k, k * (1f + 0.25f * aimPower), 1f);
                    arc.transform.position = origin + Vector2.down * arcDrop;
                    arc.transform.rotation = Quaternion.Euler(0f, 0f, 180f); // drawn as a dome: flipped into a cradle
                }
            }
            else if (arc != null) arc.enabled = false;
            if (arrow != null)
            {
                float len = Mathf.Lerp(arrowLength.x, arrowLength.y, aimPower) * pulse;
                Vector2 size = arrow.sprite.bounds.size;
                arrow.enabled = true;
                arrow.color = aimFeedbackColor;
                arrow.transform.localScale = new Vector3(arrowWidth, len / Mathf.Max(0.01f, size.y), 1f);
                arrow.transform.position = origin + aimDir * (arrowGap + len * 0.5f);
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-aimDir.x, aimDir.y) * Mathf.Rad2Deg);
            }
        }

        void HidePreview()
        {
            simPower = -1f;
            if (band != null) band.enabled = false;
            if (arc != null) arc.enabled = false;
            if (arrow != null) arrow.enabled = false;
            if (landing != null) landing.enabled = false;
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
            bool same = simPower >= 0f && Mathf.Abs(aimPower - simPower) < 0.002f && Vector2.Dot(aimDir, simDir) > 0.99999f
                        && (pos - simPos).sqrMagnitude < 0.0004f && Time.time - simTime < previewRefresh;
            if (same) { PlaceLanding(ph); return; } // nothing changed: the dots already show this shot
            simDir = aimDir; simPower = aimPower; simPos = pos; simTime = Time.time;
            lands = false;
            Vector2 v = ph.PredictLaunchVelocity(aimDir, aimPower, launchForceMultiplier);
            Vector2 g = Physics2D.gravity * ph.Body.gravityScale;
            float r = ph.Collider.radius * 0.95f;

            path.Clear();
            ignored.Clear();
            path.Add(pos);
            float travelled = 0f;
            int bouncesLeft = previewBounces;

            for (int i = 0; i < maxPreviewCasts && travelled < trajectoryPreviewLength; i++)
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
                    if (outSpeed < p.stopBounceSpeed) { lands = true; landPoint = pos; break; } // it would land here
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

            // Lay chalk dots at equal spacing along the polyline, shrinking and fading toward the end.
            float total = 0f;
            for (int s = 1; s < path.Count; s++) total += Vector2.Distance(path[s - 1], path[s]);
            int expected = Mathf.Clamp(Mathf.FloorToInt(total / Mathf.Max(0.05f, dotSpacing)), 1, 91);
            int used = 0;
            float carry = 0f;
            Color c = trajectoryColor;
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
                    float t = Mathf.Clamp01((used - 1) / (float)expected);
                    float size = dotSize * Mathf.Lerp(1f, dotEndScale, t);
                    Vector2 sb = dot.sprite != null ? (Vector2)dot.sprite.bounds.size : Vector2.one;
                    float k = size / Mathf.Max(0.001f, Mathf.Max(sb.x, sb.y));
                    dot.transform.localScale = new Vector3(k, k, 1f);
                    dot.color = new Color(c.r, c.g, c.b, c.a * Mathf.Lerp(1f, 0.25f, t));
                    d += dotSpacing;
                    if (used > 90) break;
                }
                carry = seg - (d - dotSpacing);
                if (used > 90) break;
            }
            for (int i = used; i < dots.Count; i++) dots[i].enabled = false;
            PlaceLanding(ph);
        }

        void PlaceLanding(WormPhysics ph)
        {
            if (landing == null) return;
            landing.enabled = lands;
            if (!lands) return;
            float d = ph.Collider.radius * 2f * landingMarkerSize * (1f + 0.06f * Mathf.Sin(Time.time * 10f));
            float k = d / Mathf.Max(0.01f, landing.sprite.bounds.size.x);
            landing.transform.position = new Vector3(landPoint.x, landPoint.y, 0f);
            landing.transform.localScale = new Vector3(k, k, 1f);
            landing.color = new Color(trajectoryColor.r, trajectoryColor.g, trajectoryColor.b, trajectoryColor.a * 0.6f);
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
                int n = dots.Count;
                Sprite chalk = trajectoryDotSprites != null && trajectoryDotSprites.Length > 0 ? trajectoryDotSprites[n % trajectoryDotSprites.Length] : null;
                SpriteRenderer sr = PlaceholderSprites.Make("Dot", visualRoot, chalk != null ? chalk : PlaceholderSprites.Circle, Color.white, Vector2.zero, new Vector2(dotSize, dotSize), 29);
                if (chalk != null) sr.transform.localRotation = Quaternion.Euler(0f, 0f, (n * 47) % 360); // hand-drawn: no two dots alike
                dots.Add(sr);
            }
            return dots[i];
        }
    }
}
