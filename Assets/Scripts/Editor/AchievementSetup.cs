using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    public static class AchievementSetup
    {
        [MenuItem("MojiBattle/Setup Achievement Scene")]
        public static void Prepare()
        {
            const string path="Assets/Scenes/Achievements.unity";
            if(!File.Exists(path))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var cam=new GameObject("Main Camera"){ tag="MainCamera" }.AddComponent<Camera>();
                cam.orthographic=true; cam.orthographicSize=5.2f; cam.backgroundColor=RetroUi.Paper;
                cam.transform.position=new Vector3(0,2.5f,-10);
                new GameObject("AchievementsScreen").AddComponent<AchievementsScreen>();
                EditorSceneManager.SaveScene(scene,path);
            }
            if(!EditorBuildSettings.scenes.Any(s=>s.path==path))
                EditorBuildSettings.scenes=EditorBuildSettings.scenes.Concat(new[]{new EditorBuildSettingsScene(path,true)}).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[MojiBattle] Achievement scene and {AchievementCatalog.All.Length} badges ready.");
        }
    }
}
