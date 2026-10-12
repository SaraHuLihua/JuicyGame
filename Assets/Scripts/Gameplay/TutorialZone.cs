using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectJuice
{
    public enum TutorialHint
    {
        /// <summary>Mouse + launch circle pulse while the worm stands here and can launch.</summary>
        Launch,
        /// <summary>Space bar + both circles pulse while the worm is in the air here (switch before landing).</summary>
        Switch,
        /// <summary>Space bar + Rocky circle pulse while the worm is Squishy here.</summary>
        BeHeavy,
        /// <summary>Space bar + Squishy circle pulse while the worm is Rocky here.</summary>
        BeSquishy
    }

    /// <summary>
    /// Teaches inside the level, without text: while the worm is in this box and has not done what the box is
    /// about, the matching icon of the controls (bottom left) pulses. Once the player has done it here, the box
    /// stays quiet, also after a death or a restart (until the game is closed).
    /// Place one on an empty GameObject and set Size; the box is drawn in the Scene view (blue).
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialZone : MonoBehaviour
    {
        public TutorialHint hint;
        [Tooltip("Size of the box (world units), centred on this object.")]
        public Vector2 size = new Vector2(10f, 6f);
        [Tooltip("Seconds in the box before the hint starts, so a player who already knows is not nagged.")]
        [Min(0f)] public float delay = 0.8f;
        [Tooltip("On = once the player has done it in this box, the box never hints again. Off = it hints every time it is needed.")]
        public bool onlyUntilDone = true;

        static readonly HashSet<string> doneZones = new HashSet<string>();

        string key;
        bool inside;
        float enteredAt;
        int launchesAtEnter;
        WormMaterial materialAtEnter;
        bool wasWrong;

        void Start()
        {
            Vector3 p = transform.position;
            key = SceneManager.GetActiveScene().name + "/" + name + "/" + Mathf.RoundToInt(p.x * 10f) + "," + Mathf.RoundToInt(p.y * 10f);
        }

        public bool Contains(Vector2 point)
        {
            Vector2 c = transform.position;
            Vector2 half = Vector2.Scale(size, (Vector2)transform.lossyScale) * 0.5f;
            half = new Vector2(Mathf.Abs(half.x), Mathf.Abs(half.y));
            return Mathf.Abs(point.x - c.x) <= half.x && Mathf.Abs(point.y - c.y) <= half.y;
        }

        void Update()
        {
            WormController w = WormController.Instance;
            if (w == null || w.Physics == null || w.IsDead || LevelFlow.IsComplete) { inside = false; return; }
            if (onlyUntilDone && doneZones.Contains(key)) return;

            bool now = Contains(w.Physics.Body.position);
            if (now && !inside)
            {
                enteredAt = Time.time;
                launchesAtEnter = w.LaunchCount;
                materialAtEnter = w.Material;
                wasWrong = false;
            }
            inside = now;
            if (!inside) return;

            bool grounded = w.CanLaunch;
            bool done = false, needed = false;
            switch (hint)
            {
                case TutorialHint.Launch:
                    done = w.LaunchCount > launchesAtEnter;
                    needed = grounded;
                    break;
                case TutorialHint.Switch:
                    done = w.Material != materialAtEnter;
                    needed = !grounded;
                    break;
                case TutorialHint.BeHeavy:
                case TutorialHint.BeSquishy:
                    WormMaterial want = hint == TutorialHint.BeHeavy ? WormMaterial.Heavy : WormMaterial.Squishy;
                    needed = w.Material != want;
                    if (needed) wasWrong = true;
                    done = wasWrong && !needed; // switched to the right one here
                    break;
            }
            if (done && onlyUntilDone) { doneZones.Add(key); return; }
            if (needed && Time.time - enteredAt >= delay) ControlsHud.ShowHint(hint);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one);
            Vector3 s = Vector2.Scale(size, (Vector2)transform.lossyScale);
            Gizmos.color = new Color(0.35f, 0.6f, 1f, 0.15f);
            Gizmos.DrawCube(Vector3.zero, s);
            Gizmos.color = new Color(0.35f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireCube(Vector3.zero, s);
        }
#endif
    }
}
