using UnityEditor;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    /// <summary>Audio components must not overlay speaker icons on the battle view.</summary>
    public static class CombatAudioEditorSettings
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            // Native audio annotations may not exist until the first battle is drawn.
            // Keep checking after scene changes and dynamically created audio sources.
            EditorApplication.update += HideAudioIcons;
        }

        public static void HideAudioIcons()
        {
            Hide(typeof(AudioSource));
            Hide(typeof(AudioListener));
        }

        static void Hide(System.Type type)
        {
            if (!GizmoUtility.TryGetGizmoInfo(type, out var info)) return;
            if (!info.iconEnabled && !info.gizmoEnabled) return;
            info.iconEnabled = false;
            info.gizmoEnabled = false;
            GizmoUtility.ApplyGizmoInfo(info, false);
        }
    }
}
