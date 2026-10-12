using UnityEngine;
using UnityEngine.SceneManagement;

namespace MojiBattle
{
    public sealed class BattleInstance
    {
        public Scene Scene;
        public GameObject Root;
        public MatchDirector Director;
        public MatchContext Context;
        public Fighter Left, Right;
        public CameraRig CameraRig;

        public void Destroy()
        {
            if (Root != null) { Root.SetActive(false); Object.Destroy(Root); }
            if (Scene.IsValid() && Scene.isLoaded) SceneManager.UnloadSceneAsync(Scene);
        }
    }

    /// <summary>MatchConfig から戦闘一式（アリーナ・2体・進行・観戦表示）を組み立てる。</summary>
    public static class BattleBuilder
    {
        static int sceneCounter;

        /// <summary>二刀流の副武器の字形（同じ字体・反転）。使えない文字なら主武器と同じ字形。</summary>
        public static GlyphDefinitionRuntime OffhandGlyph(FighterLoadout loadout, GlyphDefinitionRuntime main, CombatBalance balance, GlyphCalibration calibration)
        {
            var build = loadout.build;
            if (build == null || !build.IsDualWield) return null;
            string ch = build.OffhandGlyph;
            if (ch == build.character) return main;
            if (!GlyphCatalog.TryGet(ch, loadout.font, balance, calibration, out var g, out _)) return main;
            return GlyphFlip.Apply(g, build.weaponFlip);
        }

        public static BattleInstance Build(MatchConfig config, CombatBalance balance, GlyphCalibration calibration,
            bool presentation, float countdown, out string error, bool tournamentFinal = false)
        {
            error = null;
            if (!GlyphCatalog.TryGet(config.left.grapheme, config.left.font, balance, calibration, out var gl, out var reasonL))
            { error = "左: " + reasonL; return null; }
            if (!GlyphCatalog.TryGet(config.right.grapheme, config.right.font, balance, calibration, out var gr, out var reasonR))
            { error = "右: " + reasonR; return null; }
            gl = GlyphFlip.Apply(gl, config.left.build?.weaponFlip ?? WeaponFlip.None);
            gr = GlyphFlip.Apply(gr, config.right.build?.weaponFlip ?? WeaponFlip.None);
            var ol = OffhandGlyph(config.left, gl, balance, calibration);
            var or = OffhandGlyph(config.right, gr, balance, calibration);

            Time.fixedDeltaTime = TimeController.BaseFixedDelta;
            // 重い武器とヒンジの伸びを抑えるため反復回数を増やす
            Physics2D.velocityIterations = 20;
            Physics2D.positionIterations = 10;
            // 試合ごとに専用の 2D 物理ワールドを作る。前の試合の物理内部状態に影響されず、同じ seed は同じ経過になる。
            var scene = SceneManager.CreateScene($"Battle_{config.seed}_{++sceneCounter}", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var prevActive = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Battle");
            var ctx = new MatchContext
            {
                Balance = balance,
                Events = new CombatEvents(),
                ArenaHalfWidth = balance.arenaHalfWidth,
                Customize = CustomizeBalance.Default,
                MatchSeed = config.seed,
            };
            ctx.Hits = new HitResolver(ctx);
            ctx.Env = new EnvironmentImpactResolver(ctx);

            ArenaBuilder.Build(root.transform, balance);
            var rng = new MatchRandom(config.seed);
            var left = FighterFactory.Create(0, config.left, gl, ctx, rng.Fork(1), root.transform, ol);
            var right = FighterFactory.Create(1, config.right, gr, ctx, rng.Fork(2), root.transform, or);
            left.Opponent = right;
            right.Opponent = left;
            // Heavy resets and the return from ranged spacing load the hinge in close contact.
            // Give those fights extra solver passes instead of moving the weapon
            // back to the hand by changing its position.
            if (left.HeavyTempoEnabled || right.HeavyTempoEnabled)
            {
                Physics2D.velocityIterations = balance.rangedSpacing ? 40 : 28;
                Physics2D.positionIterations = balance.rangedSpacing ? 20 : 14;
            }

            var director = root.AddComponent<MatchDirector>();
            director.Init(config, ctx, left, right, countdown, scene.GetPhysicsScene2D());
            SceneManager.SetActiveScene(prevActive);

            var instance = new BattleInstance { Scene = scene, Root = root, Director = director, Context = ctx, Left = left, Right = right };
            if (presentation)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                    cam = camGo.AddComponent<Camera>();
                }
                cam.orthographic = true;
                if (Object.FindFirstObjectByType<AudioListener>() == null)
                    cam.gameObject.AddComponent<AudioListener>();
                cam.backgroundColor = FighterFactory.Paper;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.transform.position = new Vector3(0f, 3.5f, -10f);
                var rig = cam.GetComponent<CameraRig>();
                if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
                rig.Bind(director);
                instance.CameraRig = rig;
                var hud = root.AddComponent<BattleHudPresenter>();
                hud.Bind(director, rig);
                var vfx = root.AddComponent<CombatVfxDirector>();
                vfx.Bind(director, rig);
                root.AddComponent<CombatInkEffects>().Bind(director);
                root.AddComponent<RangedProjectileView>().Bind(director);
                root.AddComponent<CombatAudioDirector>().Bind(director, tournamentFinal);
                root.AddComponent<ColliderDebugView>().Bind(director);
            }
            return instance;
        }
    }
}
