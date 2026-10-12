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
        const string ModeSelectScenePath = "Assets/Scenes/ModeSelect.unity";
        const string TournamentScenePath = "Assets/Scenes/Tournament.unity";
        const string FriendRoomScenePath = "Assets/Scenes/FriendRoom.unity";
        const string ChallengeScenePath = "Assets/Scenes/Challenges.unity";
        const string DraftScenePath = "Assets/Scenes/Draft.unity";
        const string RunScenePath = "Assets/Scenes/Run.unity";
        const string WorldCupScenePath="Assets/Scenes/WorldCup.unity";

        [MenuItem("MojiBattle/Setup P1 Battle Scene")]
        public static void Run()
        {
            var balance = EnsureAsset<CombatBalance>(BalancePath);
            var calibration = EnsureAsset<GlyphCalibration>(CalibrationPath);
            EnsureAsset<CustomizeBalance>(CustomizeBalancePath);
            GlyphBaker.BuildFontLibrary(); // 実行時に任意の文字を字形化するためのフォント一覧
            Directory.CreateDirectory("Assets/Scenes");
            CreateUiScene<TitleScreen>(TitleScenePath, "TitleScreen");
            CreateUiScene<CustomizeScreen>(CustomizeScenePath, "CustomizeScreen");
            CreateUiScene<ModeSelectScreen>(ModeSelectScenePath, "ModeSelectScreen");
            CreateUiScene<TournamentScreen>(TournamentScenePath, "TournamentScreen");
            CreateUiScene<FriendRoomScreen>(FriendRoomScenePath, "FriendRoomScreen");
            CreateUiScene<ChallengeScreen>(ChallengeScenePath, "ChallengeScreen");
            CreateUiScene<DraftScreen>(DraftScenePath, "DraftScreen");
            CreateUiScene<RunScreen>(RunScenePath, "RunScreen");
            CreateUiScene<WorldCupScreen>(WorldCupScenePath,"WorldCupScreen");

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
                new EditorBuildSettingsScene(ModeSelectScenePath, true),
                new EditorBuildSettingsScene(CustomizeScenePath, true),
                new EditorBuildSettingsScene(TournamentScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(FriendRoomScenePath, true),
                new EditorBuildSettingsScene(ChallengeScenePath, true),
                new EditorBuildSettingsScene(DraftScenePath, true),
                new EditorBuildSettingsScene(RunScenePath, true),
                new EditorBuildSettingsScene(WorldCupScenePath,true),
            };

            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.productName = "MOJI Fighter X";
            Time.fixedDeltaTime = TimeController.BaseFixedDelta;
            AssetDatabase.SaveAssets();
            AchievementSetup.Prepare();
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

        /// <summary>Add the new mode scenes without regenerating existing scenes or balance assets.</summary>
        [MenuItem("MojiBattle/Setup Tournament Scenes")]
        public static void SetupTournamentScenes()
        {
            if (!File.Exists(ModeSelectScenePath)) CreateUiScene<ModeSelectScreen>(ModeSelectScenePath, "ModeSelectScreen");
            if (!File.Exists(TournamentScenePath)) CreateUiScene<TournamentScreen>(TournamentScenePath, "TournamentScreen");
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in new[] { ModeSelectScenePath, TournamentScenePath })
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[MojiBattle] Tournament scenes ready.");
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
        [MenuItem("MojiBattle/Setup Challenge Scene")]
        public static void SetupChallengeScene()
        {
            if (!File.Exists(ChallengeScenePath)) CreateUiScene<ChallengeScreen>(ChallengeScenePath,"ChallengeScreen");
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s=>s.path==ChallengeScenePath)) scenes.Add(new EditorBuildSettingsScene(ChallengeScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets();
        }
        [MenuItem("MojiBattle/Setup Draft Scene")]
        public static void SetupDraftScene()
        {
            GlyphBaker.BakeDraft();
            if (!File.Exists(DraftScenePath)) CreateUiScene<DraftScreen>(DraftScenePath,"DraftScreen");
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s=>s.path==DraftScenePath)) scenes.Add(new EditorBuildSettingsScene(DraftScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray(); AssetDatabase.SaveAssets();
        }
        [MenuItem("MojiBattle/Setup Run Scene")]
        public static void SetupRunScene()
        {
            if(!File.Exists(RunScenePath))CreateUiScene<RunScreen>(RunScenePath,"RunScreen");
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if(!scenes.Exists(s=>s.path==RunScenePath))scenes.Add(new EditorBuildSettingsScene(RunScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
        }
        [MenuItem("MojiBattle/Setup World Cup Scene")]
        public static void SetupWorldCupScene()
        {
            if(!File.Exists(WorldCupScenePath))CreateUiScene<WorldCupScreen>(WorldCupScenePath,"WorldCupScreen");
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if(!scenes.Exists(s=>s.path==WorldCupScenePath))scenes.Add(new EditorBuildSettingsScene(WorldCupScenePath,true));
            EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
        }
        [MenuItem("MojiBattle/Setup Friend Room Scene")]
        public static void SetupFriendRoomScene()
        {
            if (!File.Exists(FriendRoomScenePath)) CreateUiScene<FriendRoomScreen>(FriendRoomScenePath, "FriendRoomScreen");
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == FriendRoomScenePath)) scenes.Add(new EditorBuildSettingsScene(FriendRoomScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            // HTTP is available only in development players; public release endpoints should use HTTPS.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            AssetDatabase.SaveAssets();
        }
    }
}
