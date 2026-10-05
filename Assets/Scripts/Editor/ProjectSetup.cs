using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    /// <summary>
    /// プロジェクト資産を生成する: CombatBalance / CustomizeBalance / GlyphCalibration アセット、
    /// Title / Customize / Battle シーン、Build Settings（Title → Customize → Battle）。
    /// バッチ: Unity -batchmode -executeMethod MojiBattle.EditorTools.ProjectSetup.Run -quit
    /// </summary>
    public static class ProjectSetup
    {
        const string BalancePath = "Assets/Resources/Balance/CombatBalance.asset";
        const string CalibrationPath = "Assets/Resources/Glyphs/GlyphCalibration.asset";
        const string CustomizeBalancePath = "Assets/Resources/Balance/CustomizeBalance.asset";
        const string ScenePath = "Assets/Scenes/Battle.unity";
        const string TitleScenePath = "Assets/Scenes/Title.unity";
        const string CustomizeScenePath = "Assets/Scenes/Customize.unity";

        [MenuItem("MojiBattle/Setup P1 Battle Scene")]
        public static void Run()
        {
            var balance = EnsureAsset<CombatBalance>(BalancePath);
            var calibration = EnsureAsset<GlyphCalibration>(CalibrationPath);
            EnsureAsset<CustomizeBalance>(CustomizeBalancePath);
            Directory.CreateDirectory("Assets/Scenes");
            CreateUiScene<TitleScreen>(TitleScenePath, "TitleScreen");
            CreateUiScene<CustomizeScreen>(CustomizeScenePath, "CustomizeScreen");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FighterFactory.Paper;
            camGo.transform.position = new Vector3(0f, 3.85f, -10f);
            var boot = new GameObject("BattleBootstrap").AddComponent<BattleBootstrap>();
            boot.balance = balance;
            boot.calibration = calibration;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(CustomizeScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.productName = "文字武器オートバトル";
            Time.fixedDeltaTime = TimeController.BaseFixedDelta;
            AssetDatabase.SaveAssets();
            Debug.Log("[MojiBattle] Setup complete: " + ScenePath);
        }

        /// <summary>カメラと画面コンポーネント 1 つだけのシーン（UI は実行時にコードで組み立てる）。</summary>
        static void CreateUiScene<T>(string path, string name) where T : MonoBehaviour
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FighterFactory.Paper;
            camGo.transform.position = new Vector3(0f, 2.5f, -10f);
            new GameObject(name).AddComponent<T>();
            EditorSceneManager.SaveScene(scene, path);
        }

        static T EnsureAsset<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
