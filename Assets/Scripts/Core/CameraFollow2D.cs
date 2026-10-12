using System.Collections.Generic;
using UnityEngine;

namespace ProjectJuice
{
    /// <summary>
    /// Smooth 2D follow camera. Lives on the Main Camera of every level; every value is per scene, so
    /// each level can have its own zoom/follow. Select Main Camera > Camera Follow 2D:
    ///   Orthographic Size  = zoom (half the visible height in units). Bigger = wider view, smaller = closer.
    ///   Offset             = where the worm sits on screen (x, y units added to its position).
    ///   Follow Speed       = horizontal / vertical catch-up (higher = snappier, lower = floatier).
    ///   Dead Zone          = the worm can move this far from the centre before the camera follows.
    ///   Look Ahead         = shifts the view in the direction the worm is flying.
    ///   Bounds             = optional limits so the camera never shows outside the level.
    /// CameraZone triggers can temporarily override zoom/offset in parts of a level.
    /// Runs in LateUpdate after the worm's Rigidbody2D interpolation, so there is no jitter.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("What to follow. Empty = the worm, found automatically.")]
        public Transform target;

        [Header("Zoom")]
        [Tooltip("Half the visible height in world units. 10 = 20 units tall. Bigger = see more of the level.")]
        [Min(1f)] public float orthographicSize = 14f;
        [Tooltip("Seconds to blend between zoom levels (CameraZones). 0 = instant.")]
        [Min(0f)] public float zoomSmoothTime = 0.6f;

        [Header("Follow")]
        [Tooltip("Added to the target position (units). (0, 2) keeps the worm a bit below the centre so you see more above.")]
        public Vector2 offset = new Vector2(0f, 2f);
        [Tooltip("Horizontal catch-up. Higher = snappier, lower = more lag. 4-8 feels good.")]
        [Min(0.1f)] public float followSpeed = 6f;
        [Tooltip("Vertical catch-up. Slightly lower than horizontal avoids bobbing on small bounces.")]
        [Min(0.1f)] public float verticalFollowSpeed = 4.5f;
        [Tooltip("The worm can move this far (units) from the camera's aim point before the camera starts moving.")]
        public Vector2 deadZone = new Vector2(0.4f, 0.6f);

        [Header("Look ahead")]
        [Tooltip("Seconds of the worm's velocity to look ahead (0 = off).")]
        [Min(0f)] public float lookAhead = 0.12f;
        [Tooltip("Largest look-ahead shift (units).")]
        [Min(0f)] public float maxLookAhead = 3f;

        [Header("Bounds (optional)")]
        [Tooltip("Keep the view inside the box below (or inside Bounds Collider if set).")]
        public bool useBounds = false;
        [Tooltip("Optional: any Collider2D (e.g. a BoxCollider2D set to trigger) whose bounds are the level limits.")]
        public Collider2D boundsCollider;
        public Vector2 boundsMin = new Vector2(-50f, -50f);
        public Vector2 boundsMax = new Vector2(50f, 50f);

        Camera cam;
        Vector2 aim;          // dead-zone filtered target point
        Vector2 velocity;     // SmoothDamp state
        Vector2 look;
        Vector2 lookVel;
        float zoomVel;
        bool hasAim;
        Rigidbody2D targetBody;

        static readonly List<CameraZone> zones = new List<CameraZone>();

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }

        void OnValidate()
        {
            // Lets you see the zoom change in the editor straight away.
            Camera c = GetComponent<Camera>();
            if (c != null && c.orthographic && !Application.isPlaying) c.orthographicSize = orthographicSize;
        }

        bool FindTarget()
        {
            if (target == null)
            {
                if (WormController.Instance == null) return false;
                target = WormController.Instance.transform;
            }
            if (targetBody == null || targetBody.transform != target) targetBody = target.GetComponent<Rigidbody2D>();
            return true;
        }

        void LateUpdate()
        {
            if (!FindTarget()) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            CameraZone zone = ActiveZone();
            float size = zone != null && zone.overrideSize ? zone.orthographicSize : orthographicSize;
            Vector2 off = zone != null && zone.overrideOffset ? zone.offset : offset;

            cam.orthographicSize = zoomSmoothTime > 0f
                ? Mathf.SmoothDamp(cam.orthographicSize, size, ref zoomVel, zoomSmoothTime)
                : size;

            Vector2 p = target.position;
            if (!hasAim) { aim = p; hasAim = true; }
            // Dead zone: only move the aim point when the target leaves the box around it.
            aim.x = DeadZone(aim.x, p.x, deadZone.x);
            aim.y = DeadZone(aim.y, p.y, deadZone.y);

            Vector2 wantLook = Vector2.zero;
            if (lookAhead > 0f && targetBody != null)
                wantLook = Vector2.ClampMagnitude(targetBody.GetVelocity() * lookAhead, maxLookAhead);
            look = Vector2.SmoothDamp(look, wantLook, ref lookVel, 0.5f, Mathf.Infinity, dt);

            Vector2 goal = Clamp(aim + off + look);
            Vector2 cur = transform.position;
            float nx = Mathf.SmoothDamp(cur.x, goal.x, ref velocity.x, 1f / followSpeed, Mathf.Infinity, dt);
            float ny = Mathf.SmoothDamp(cur.y, goal.y, ref velocity.y, 1f / verticalFollowSpeed, Mathf.Infinity, dt);
            Vector2 next = Clamp(new Vector2(nx, ny));
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        static float DeadZone(float aim, float p, float half)
        {
            if (half <= 0f) return p;
            if (p > aim + half) return p - half;
            if (p < aim - half) return p + half;
            return aim;
        }

        Vector2 Clamp(Vector2 p)
        {
            if (!useBounds || cam == null) return p;
            Vector2 min = boundsMin, max = boundsMax;
            if (boundsCollider != null) { Bounds b = boundsCollider.bounds; min = b.min; max = b.max; }
            float h = cam.orthographicSize, w = h * cam.aspect;
            p.x = (max.x - min.x) <= 2f * w ? (min.x + max.x) * 0.5f : Mathf.Clamp(p.x, min.x + w, max.x - w);
            p.y = (max.y - min.y) <= 2f * h ? (min.y + max.y) * 0.5f : Mathf.Clamp(p.y, min.y + h, max.y - h);
            return p;
        }

        /// <summary>Jump straight to the target (used after respawn / area jumps).</summary>
        public void Snap()
        {
            if (!FindTarget()) return;
            if (cam == null) cam = GetComponent<Camera>();
            CameraZone zone = ActiveZone();
            cam.orthographicSize = zone != null && zone.overrideSize ? zone.orthographicSize : orthographicSize;
            Vector2 off = zone != null && zone.overrideOffset ? zone.offset : offset;
            aim = target.position;
            hasAim = true;
            look = lookVel = velocity = Vector2.zero;
            zoomVel = 0f;
            Vector2 p = Clamp(aim + off);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }

        // ------------------------------------------------------------------ zones

        internal static void Enter(CameraZone z) { if (!zones.Contains(z)) zones.Add(z); }
        internal static void Exit(CameraZone z) { zones.Remove(z); }

        static CameraZone ActiveZone()
        {
            CameraZone best = null;
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                CameraZone z = zones[i];
                if (z == null || !z.isActiveAndEnabled) { zones.RemoveAt(i); continue; }
                if (best == null || z.priority > best.priority) best = z;
            }
            return best;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (!useBounds) return;
            Vector2 min = boundsMin, max = boundsMax;
            if (boundsCollider != null) { Bounds b = boundsCollider.bounds; min = b.min; max = b.max; }
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube((min + max) * 0.5f, max - min);
        }
#endif
    }
}
