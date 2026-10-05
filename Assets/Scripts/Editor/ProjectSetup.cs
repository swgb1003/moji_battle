using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MojiBattle.EditorTools
{
    /// <summary>
    /// P0/P1 のプロジェクト資産を生成する: CombatBalance / GlyphCalibration アセット、Battle シーン、Build Settings。
    /// バッチ: Unity -batchmode -executeMethod MojiBattle.EditorTools.ProjectSetup.Run -quit
    /// </summary>
    public static class ProjectSetup
    {
        const string BalancePath = "Assets/Resources/Balance/CombatBalance.asset";
        const string CalibrationPath = "Assets/Resources/Glyphs/GlyphCalibration.asset";
        const string ScenePath = "Assets/Scenes/Battle.unity";

        [MenuItem("MojiBattle/Setup P1 Battle Scene")]
        public static void Run()
        {
            var balance = EnsureAsset<CombatBalance>(BalancePath);
            var calibration = EnsureAsset<GlyphCalibration>(CalibrationPath);

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
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.productName = "文字武器オートバトル P1";
            Time.fixedDeltaTime = TimeController.BaseFixedDelta;
            AssetDatabase.SaveAssets();
            Debug.Log("[MojiBattle] Setup complete: " + ScenePath);
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
