#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectJuice.Editor
{
    /// <summary>One-time SFX setup for the existing scene AudioManager.</summary>
    public static class AudioManagerSetup
    {
        const string LaunchClipPath = "Assets/Audio/SFX/Player/launching-the-ball.wav";
        const string SquishyImpactA = "Assets/Audio/SFX/Player/object-ball-rubber-ball-bounce-short-01.wav";
        const string SquishyImpactB = "Assets/Audio/SFX/Player/object-ball-rubber-ball-bounce-tile-01.wav";
        const string HeavyImpactA = "Assets/Audio/SFX/Player/impact-metal-generic-metal-impact-short-02.wav";
        const string HeavyImpactB = "Assets/Audio/SFX/Player/object-metal-medium-impact-04.wav";
        const string HeavyImpactC = "Assets/Audio/SFX/Player/object-metal-medium-impact-05.wav";
        const string RockDestructionA = "Assets/Audio/SFX/Player/debris-rock-large-sandstone-smash-01.wav";
        const string RockDestructionB = "Assets/Audio/SFX/Player/debris-rock-large-sandstone-smash-05.wav";
        const string RockDestructionC = "Assets/Audio/SFX/Player/debris-rock-large-sandstone-smash-07.wav";
        const string RockDestructionD = "Assets/Audio/SFX/Player/destruction-debris-pickaxe-crush-rubble-hard-resonance-06.wav";

        [MenuItem("Project Juice/Audio/Assign Gameplay SFX")]
        public static void AssignGameplaySfx()
        {
            AudioManager manager = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogError("No AudioManager exists in the active scene. Add the existing project AudioManager before assigning SFX.");
                return;
            }

            SerializedObject serializedManager = new SerializedObject(manager);
            AssignIfEmpty(serializedManager.FindProperty("launchSfx"), LaunchClipPath);
            AssignClipsIfEmpty(serializedManager.FindProperty("squishyImpactSfx"), SquishyImpactA, SquishyImpactB);
            AssignClipsIfEmpty(serializedManager.FindProperty("heavyImpactSfx"), HeavyImpactA, HeavyImpactB, HeavyImpactC);
            serializedManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("Filled only empty gameplay SFX fields on the existing AudioManager. Existing assignments and settings were preserved. Save the scene to keep any new assignments.");
        }

        [MenuItem("Project Juice/Audio/Assign Rock Destruction SFX")]
        public static void AssignRockDestructionSfx()
        {
            AudioManager manager = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogError("No AudioManager exists in the active scene. Add the existing project AudioManager before assigning SFX.");
                return;
            }

            SerializedObject serializedManager = new SerializedObject(manager);
            AssignClipsIfEmpty(serializedManager.FindProperty("rockDestructionSfx"), RockDestructionA, RockDestructionB, RockDestructionC, RockDestructionD);
            serializedManager.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log("Filled Rock Destruction SFX only when its clip list was empty. Existing audio settings were preserved. Save the scene to keep any new assignments.");
        }

        static void AssignIfEmpty(SerializedProperty property, string path)
        {
            if (property.objectReferenceValue == null)
                property.objectReferenceValue = LoadClip(path);
        }

        static void AssignClipsIfEmpty(SerializedProperty property, params string[] paths)
        {
            if (HasAssignedClip(property)) return;
            AudioClip[] clips = LoadClips(paths);
            if (clips == null) return;
            property.arraySize = paths.Length;
            for (int i = 0; i < paths.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        static bool HasAssignedClip(SerializedProperty property)
        {
            for (int i = 0; i < property.arraySize; i++)
                if (property.GetArrayElementAtIndex(i).objectReferenceValue != null) return true;
            return false;
        }

        static AudioClip[] LoadClips(string[] paths)
        {
            AudioClip[] clips = new AudioClip[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                clips[i] = LoadClip(paths[i]);
                if (clips[i] == null) return null;
            }
            return clips;
        }

        static AudioClip LoadClip(string path)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogError("Audio clip was not found at " + path);
            return clip;
        }
    }
}
#endif
