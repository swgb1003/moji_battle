using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MojiBattle.Tests
{
    // Footage for the PR video. Run explicitly: -testFilter MojiBattle.Tests.PRVideoShots
    // Each test writes PRVideo/footage/<name>.mp4 and <name>.events.tsv (frame number and event).
    [Explicit, Category("PRVideo"), Timeout(7200000)]
    public sealed class PRVideoShots
    {
        int reduced; FighterBuildData left, right; string worldCupSave; bool hadWorldCupSave;
        [SetUp] public void Setup()
        {
            reduced = PlayerPrefs.GetInt("MenuReducedMotion", 0); PlayerPrefs.SetInt("MenuReducedMotion", 0);
            left = BuildRuntimeStore.Get(0).Clone(); right = BuildRuntimeStore.Get(1).Clone();
            hadWorldCupSave = PlayerPrefs.HasKey(WorldCupRuntimeStore.PrefKey); worldCupSave = PlayerPrefs.GetString(WorldCupRuntimeStore.PrefKey);
            TimeController.ResetAll(); PRVideoRecorder.UseMenuResolution();
        }
        [TearDown] public void Cleanup()
        {
            PRVideoRecorder.End(); GameFlow.ToTitle(); WorldCupRuntimeStore.ClearSession();
            BuildRuntimeStore.Set(0, left); BuildRuntimeStore.Set(1, right); BuildRuntimeStore.Active = false;
            if (hadWorldCupSave) PlayerPrefs.SetString(WorldCupRuntimeStore.PrefKey, worldCupSave); else PlayerPrefs.DeleteKey(WorldCupRuntimeStore.PrefKey);
            PlayerPrefs.SetInt("MenuReducedMotion", reduced); PlayerPrefs.Save(); TimeController.ResetAll();
        }
        static IEnumerator Hold(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds * PRVideoRecorder.Fps);
            for (int i = 0; i < frames; i++) yield return null;
        }
        static IEnumerator Settle() { for (int i = 0; i < 4; i++) yield return null; }
        static void Click(string name)
        {
            var b = Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(b, name); b.onClick.Invoke(); PRVideoRecorder.Mark("click " + name);
        }

        [UnityTest] public IEnumerator Title()
        {
            GameFlow.ToTitle(); yield return null;
            PRVideoRecorder.Begin("title"); yield return Hold(6);
        }

        [UnityTest] public IEnumerator Customize()
        {
            BuildRuntimeStore.ResetToDefaults(); GameFlow.ToCustomize(); yield return Settle();
            var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var c = screen.Customizer;
            c.SelectSide(0); c.SetCharacter("一"); c.SetFont(FontStyleId.Gothic); c.SetSize(WeaponSize.M); c.SetGrip(GripType.TwoHanded);
            yield return Settle();
            PRVideoRecorder.Begin("customize"); yield return Hold(1.2f);
            foreach (string ch in new[] { "龍", "鬱", "A", "斬", "火" })
            { c.SetCharacter(ch); PRVideoRecorder.Mark("char " + ch); yield return Hold(1.0f); }
            foreach (var size in new[] { WeaponSize.S, WeaponSize.M, WeaponSize.L, WeaponSize.XL })
            { c.SetSize(size); PRVideoRecorder.Mark("size " + size); yield return Hold(.8f); }
            foreach (var font in new[] { FontStyleId.Mincho, FontStyleId.RoundedGothic, FontStyleId.Brush })
            { c.SetFont(font); PRVideoRecorder.Mark("font " + font); yield return Hold(.8f); }
            foreach (var grip in new[] { GripType.OneHanded, GripType.Reverse, GripType.Horizontal, GripType.TwoHanded })
            { c.SetGrip(grip); PRVideoRecorder.Mark("grip " + grip); yield return Hold(.8f); }
            yield return Hold(1.5f);
        }

        [UnityTest] public IEnumerator Fusion()
        {
            GameFlow.ToModeSelect(); yield return Settle();
            Click("FusionMode"); yield return Settle(); Click("FusionNew"); yield return Settle();
            var screen = Object.FindAnyObjectByType<FusionScreen>();
            PRVideoRecorder.Begin("fusion"); yield return Hold(1.2f);
            screen.SelectSource("畑"); PRVideoRecorder.Mark("left 畑"); yield return Hold(1.4f);
            Click("ChooseRight"); yield return Hold(.6f);
            screen.SelectSource("忍"); yield return Hold(.4f); Click("SourceFilter_4");
            PRVideoRecorder.Mark("right 忍"); yield return Hold(1.6f);
            Click("ToLayout"); yield return Hold(1.4f);
            var width = GameObject.Find("FusionWidth").GetComponent<Slider>();
            for (int i = 0; i <= 15; i++) { width.value = Mathf.Lerp(1f, 1.15f, i / 15f); yield return null; }
            Click("FusionFont3"); yield return Hold(1.2f);
            Click("FusionConfirm"); PRVideoRecorder.Mark("birth");
            while (screen.BirthPlaying) yield return null;
            PRVideoRecorder.Mark("finish"); yield return Hold(.8f);
            var input = GameObject.Find("FusionNameInput").GetComponent<WebCharacterInput>();
            foreach (string name in new[] { "炎", "炎刃" }) { input.ReceiveText(name); yield return Hold(.5f); }
            yield return Hold(2f);
        }

        [UnityTest] public IEnumerator Modes()
        {
            GameFlow.ToModeSelect(); yield return null;
            PRVideoRecorder.Begin("mode_select"); yield return Hold(5);
            PRVideoRecorder.End();

            BuildRuntimeStore.ResetToDefaults(); GameFlow.ToCustomize(); yield return Settle();
            var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var c = screen.Customizer;
            c.SelectSide(0); c.SetCharacter("火"); c.SetFont(FontStyleId.Brush); c.SetSize(WeaponSize.L);
            c.SelectSide(1); c.SetCharacter("水"); c.SetFont(FontStyleId.Brush); c.SetSize(WeaponSize.L);
            screen.ShowVs(); yield return null;
            PRVideoRecorder.Begin("mode_battle"); yield return Hold(4); PRVideoRecorder.End();

            GameFlow.ToTournamentSelect(); yield return null;
            PRVideoRecorder.Begin("mode_tournament"); yield return Hold(4); PRVideoRecorder.End();
            GameFlow.ToChallenges(); yield return null;
            PRVideoRecorder.Begin("mode_challenge"); yield return Hold(4); PRVideoRecorder.End();
            GameFlow.ToDraftIntro(); yield return null;
            PRVideoRecorder.Begin("mode_draft"); yield return Hold(4); PRVideoRecorder.End();
            GameFlow.ToRunIntro(); yield return null;
            PRVideoRecorder.Begin("mode_run"); yield return Hold(4); PRVideoRecorder.End();
            GameFlow.ToPractice(); yield return null;
            PRVideoRecorder.Begin("mode_practice"); yield return Hold(4); PRVideoRecorder.End();
            GameFlow.ToFriendRoom(); yield return null;
            PRVideoRecorder.Begin("mode_friend"); yield return Hold(4); PRVideoRecorder.End();
            GameFlow.ToAchievements(); yield return null;
            PRVideoRecorder.Begin("mode_achievements"); yield return Hold(4);
        }

        static FighterBuildData Build(string ch, WeaponSize size, FontStyleId font, GripType grip, BattleStyle style)
            => new FighterBuildData { character = ch, weaponSize = size, fontType = font, gripType = grip, battleStyle = style };

        static IEnumerator RecordBattle(string name, FighterBuildData a, FighterBuildData b, int seed, float maxSeconds, float specialAt = -1)
        {
            GameFlow.ToCustomize(); yield return Settle();
            BuildRuntimeStore.Set(0, a); BuildRuntimeStore.Set(1, b);
            GameFlow.ToBattle(); yield return Settle();
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            boot.useCountdown = true;
            PRVideoRecorder.Begin(name);
            boot.StartMatch(seed); yield return null;
            var battle = boot.Battle; var ev = battle.Context.Events;
            ev.Hit += e => PRVideoRecorder.Mark($"hit\t{e.attacker}\t{e.damage:0.0}\t{e.style}\t{(e.critical ? "crit" : "")}\t{(e.knockdown ? "down" : "")}\t{(e.launch ? "launch" : "")}\t{(e.combo ? "combo" : "")}");
            ev.KO += e => PRVideoRecorder.Mark($"ko\t{e.winner}\t{e.finish}\t{e.style}");
            ev.Clash += e => PRVideoRecorder.Mark("clash");
            ev.Guard += e => PRVideoRecorder.Mark("guard");
            ev.EnvImpact += e => PRVideoRecorder.Mark("env");
            ev.Motion += e => PRVideoRecorder.Mark("motion\t" + e.kind);
            ev.Intervention += e => { if (e.success) PRVideoRecorder.Mark("intervention\t" + e.kind); };
            bool special = false; float endedAt = -1;
            while (PRVideoRecorder.Seconds < maxSeconds)
            {
                if (battle == null || battle.Root == null) break;
                if (!special && specialAt >= 0 && battle.Director.Fighting && battle.Director.Clock >= specialAt)
                {
                    special = true; var iv = battle.Context.Interventions;
                    iv.Side(0).gauge = 1f; iv.Request(0, InterventionKind.Special, false);
                }
                if (battle.Director.Ended && endedAt < 0) { endedAt = PRVideoRecorder.Seconds; PRVideoRecorder.Mark("ended"); }
                if (endedAt >= 0 && PRVideoRecorder.Seconds - endedAt > 7) break;
                yield return null;
            }
        }

        [UnityTest] public IEnumerator BattleDragonVsBlade() => RecordBattle("battle_dragon_blade",
            Build("龍", WeaponSize.L, FontStyleId.Brush, GripType.TwoHanded, BattleStyle.Aggressive),
            Build("刀", WeaponSize.M, FontStyleId.Brush, GripType.OneHanded, BattleStyle.Counter), 7301, 70);
        [UnityTest] public IEnumerator BattleFireVsWater() => RecordBattle("battle_fire_water",
            Build("火", WeaponSize.L, FontStyleId.Brush, GripType.TwoHanded, BattleStyle.Aggressive),
            Build("水", WeaponSize.L, FontStyleId.Brush, GripType.TwoHanded, BattleStyle.HitAndAway), 4127, 70);
        [UnityTest] public IEnumerator BattleHeavy() => RecordBattle("battle_heavy",
            Build("鬱", WeaponSize.XL, FontStyleId.Gothic, GripType.TwoHanded, BattleStyle.Aggressive),
            Build("斬", WeaponSize.L, FontStyleId.Mincho, GripType.TwoHanded, BattleStyle.Aggressive), 801942925, 70);
        [UnityTest] public IEnumerator BattleSpecial() => RecordBattle("battle_special",
            Build("火", WeaponSize.M, FontStyleId.Brush, GripType.TwoHanded, BattleStyle.Aggressive),
            Build("闘", WeaponSize.M, FontStyleId.Gothic, GripType.TwoHanded, BattleStyle.Aggressive), 2219, 40, 4f);

        // ---------------------------------------------------------------- 一 (attacker) vs 木 (receiver), both S
        static FighterBuildData Ichi() => Build("一", WeaponSize.M, FontStyleId.Brush, GripType.TwoHanded, BattleStyle.Aggressive);
        static FighterBuildData Ki() => Build("木", WeaponSize.M, FontStyleId.Brush, GripType.OneHanded, BattleStyle.HitAndAway);
        static readonly ClassTendency Still = new ClassTendency { reactionChance = 0f, proactiveGuard = 0f, guardBias = 0f,
            evadeBias = 0f, counterBias = 0f, retreatAfterAttack = 0f, attackWillingness = 0f };

        static void Place(Fighter f, float x)
        {
            var delta = new Vector2(x - f.Body.position.x, 0f);
            f.Body.position += delta; f.WeaponBody.position += delta;
            f.Body.transform.position += (Vector3)delta; f.WeaponBody.transform.position += (Vector3)delta;
            f.Body.linearVelocity = f.WeaponBody.linearVelocity = Vector2.zero;
        }

        static IEnumerator StartIchiVsKi(BattleStageId stage, bool countdown, int seed = 5150)
        {
            StageSession.Selected = stage;
            GameFlow.ToCustomize(); yield return Settle();
            BuildRuntimeStore.Set(0, Ichi()); BuildRuntimeStore.Set(1, Ki());
            GameFlow.ToBattle(); yield return Settle();
            var boot = Object.FindAnyObjectByType<BattleBootstrap>();
            boot.useCountdown = countdown; boot.StartMatch(seed); yield return null;
        }

        /// <summary>Freezes both AIs: only scripted techniques of 一 happen; 木 stands and takes them.</summary>
        static void Script(BattleInstance battle)
        {
            foreach (var f in battle.Director.Fighters)
            { f.SetTendency(Still); f.Runtime.attackReadyAt = 1e6f; f.Runtime.evadeReadyAt = 1e6f; f.WalkSpeed = 0f; }
            var iv = battle.Context.Interventions; iv.Side(1).cheerReadyAt = 1e6f; iv.Side(1).gauge = 0f;
        }

        static void Log(BattleInstance battle)
        {
            var ev = battle.Context.Events; var cam = battle.CameraRig.Cam;
            string P(Vector2 w) { var p = PRVideoRecorder.BattlePixel(cam, w); return $"{p.x:0}\t{p.y:0}"; }
            ev.Hit += e => PRVideoRecorder.Mark($"hit\t{e.attacker}\t{e.damage:0.0}\t{e.style}\t{(e.critical ? "crit" : "")}\t{(e.knockdown ? "down" : "")}\t{(e.launch ? "launch" : "")}\t{(e.combo ? "combo" : "")}\t{e.time:0.000}\t{P(e.point)}");
            ev.KO += e => PRVideoRecorder.Mark($"ko\t{e.winner}\t{e.finish}\t{e.style}\t{e.time:0.000}\t{P(e.point)}");
            ev.EnvImpact += e => { if (e.slam || e.smash || e.damage > 0) PRVideoRecorder.Mark($"env\t{e.fighter}\t{e.damage:0.0}\t{(e.slam ? "slam" : e.smash ? "smash" : "")}\t{e.time:0.000}\t{P(e.point)}"); };
            ev.Guard += e => PRVideoRecorder.Mark($"guard\t{e.defender}\t{(e.broke ? "broke" : e.parried ? "parried" : "")}\t{e.time:0.000}");
            ev.Evaded += e => PRVideoRecorder.Mark($"evade\t{e.fighter}\t{e.kind}\t{e.time:0.000}");
            ev.Intervention += e => { if (e.success) PRVideoRecorder.Mark("intervention\t" + e.kind); };
        }

        static float Gap(Fighter a, AttackStyle style)
        {
            switch (style)
            {
                case AttackStyle.DashSlash: return a.AttackRange + .8f;
                case AttackStyle.Leap: return a.AttackRange + 1.1f;
                case AttackStyle.StrokeShot: case AttackStyle.GroundWave: return 3.6f;
                case AttackStyle.Throw: case AttackStyle.FrontKick: case AttackStyle.Bash: return a.AttackRange * .6f;
                default: return a.AttackRange * .85f;
            }
        }

        /// <summary>一 performs one technique on 木 (reset to full health and placed at the right gap).</summary>
        static IEnumerator Perform(BattleInstance battle, AttackStyle style, float kiHp = -1)
        {
            var a = battle.Left; var d = battle.Right;
            Script(battle);
            d.Runtime.hp = kiHp > 0 ? kiHp : d.Runtime.maxHp;
            float x0 = Mathf.Clamp(a.X, -2.4f, 0f) - .6f;
            Place(a, x0); Place(d, x0 + Gap(a, style));
            for (int i = 0; i < 10; i++) { Script(battle); yield return null; }
            PRVideoRecorder.Mark("tech\t" + style);
            a.StartAttack(battle.Context.SimTime, style);
            float until = PRVideoRecorder.Seconds + 2.4f; bool hit = false;
            System.Action<HitEvent> onHit = e => { if (e.attacker == 0) hit = true; };
            battle.Context.Events.Hit += onHit;
            while (PRVideoRecorder.Seconds < until && !battle.Director.Ended)
            {
                Script(battle); yield return null;
                if (hit && a.Runtime.state != FighterState.AttackWindup && a.Runtime.state != FighterState.AttackActive && until - PRVideoRecorder.Seconds > .7f)
                    until = PRVideoRecorder.Seconds + .7f;
            }
            battle.Context.Events.Hit -= onHit;
            PRVideoRecorder.Mark((hit ? "landed\t" : "missed\t") + style);
        }

        static IEnumerator TechniqueReel(string name, BattleStageId stage, bool countdown, params AttackStyle[] styles)
        {
            yield return StartIchiVsKi(stage, countdown);
            var boot = Object.FindAnyObjectByType<BattleBootstrap>(); var battle = boot.Battle;
            PRVideoRecorder.Begin(name); Log(battle);
            while (!battle.Director.Fighting) { Script(battle); yield return null; }
            PRVideoRecorder.Mark("fighting");
            foreach (var style in styles) yield return Perform(battle, style);
        }

        [UnityTest] public IEnumerator TechDojo() => TechniqueReel("tech_dojo", BattleStageId.Dojo, true,
            AttackStyle.DashSlash, AttackStyle.Thrust, AttackStyle.Overhead, AttackStyle.Rising);
        [UnityTest] public IEnumerator TechCalligraphy() => TechniqueReel("tech_calligraphy", BattleStageId.Calligraphy, false,
            AttackStyle.Sweep, AttackStyle.LowSweep, AttackStyle.Spin);
        [UnityTest] public IEnumerator TechLetterpress() => TechniqueReel("tech_letterpress", BattleStageId.Letterpress, false,
            AttackStyle.Launch, AttackStyle.FrontKick, AttackStyle.Throw);
        [UnityTest] public IEnumerator TechExam() => TechniqueReel("tech_exam", BattleStageId.Exam, false,
            AttackStyle.StrokeShot, AttackStyle.GroundWave, AttackStyle.Leap);
        [UnityTest] public IEnumerator TechEraser() => TechniqueReel("tech_eraser", BattleStageId.EraserFactory, false,
            AttackStyle.Whirl, AttackStyle.Bash, AttackStyle.DashSlash);
        [UnityTest] public IEnumerator TechDictionary() => TechniqueReel("tech_dictionary", BattleStageId.Dictionary, false,
            AttackStyle.Overhead, AttackStyle.Thrust, AttackStyle.Leap);

        [UnityTest] public IEnumerator KnockOut()
        {
            yield return StartIchiVsKi(BattleStageId.Letterpress, false);
            var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle;
            PRVideoRecorder.Begin("tech_ko"); Log(battle);
            while (!battle.Director.Fighting) { Script(battle); yield return null; }
            yield return Perform(battle, AttackStyle.Thrust);
            yield return Perform(battle, AttackStyle.Overhead, 6f);
            float endAt = -1;
            while (PRVideoRecorder.Seconds < 40)
            {
                if (battle.Director.Ended && endAt < 0) { endAt = PRVideoRecorder.Seconds; PRVideoRecorder.Mark("ended"); }
                if (endAt >= 0 && PRVideoRecorder.Seconds - endAt > 6) break;
                yield return null;
            }
        }

        [UnityTest] public IEnumerator Stages()
        {
            var saved = StageSession.Selected;
            BuildRuntimeStore.Set(0, Ichi()); BuildRuntimeStore.Set(1, Ki());
            GameFlow.ToModeSelect(); yield return Settle();
            Click("BattleModeStart"); yield return Settle();
            BuildRuntimeStore.Set(0, Ichi()); BuildRuntimeStore.Set(1, Ki());
            Click("Battle"); yield return null;
            PRVideoRecorder.Begin("stage_select"); yield return Hold(1.0f);
            for (int i = 1; i < 6; i++) { Click("StageCard" + i); yield return Hold(.85f); }
            Click("StageCard0"); yield return Hold(1f);
            StageSession.Selected = saved;
        }

        [UnityTest] public IEnumerator AiSpecial()
        {
            string saved = PlayerPrefs.GetString("MojiBattle.Specials.v1", "");
            try
            {
                SpecialMoveLibrary.ClearAll();
                StageSession.Selected = BattleStageId.Dojo;
                GameFlow.ToCustomize(); yield return Settle();
                var screen = Object.FindAnyObjectByType<CustomizeScreen>(); var c = screen.Customizer;
                c.SelectSide(1); c.SetCharacter("木"); c.SetSize(WeaponSize.M); c.SetFont(FontStyleId.Brush); c.SetStyle(BattleStyle.HitAndAway);
                c.SelectSide(0); c.SetCharacter("一"); c.SetSize(WeaponSize.M); c.SetFont(FontStyleId.Brush); c.SetGrip(GripType.TwoHanded); c.SetSpecial(null);
                yield return Settle();
                PRVideoRecorder.Begin("ai_special"); yield return Hold(.8f);
                Click("SpecialMoves"); yield return Hold(1.2f);
                var panel = screen.SpecialPanel; string name = "一閃轟雷";
                for (int i = 1; i <= name.Length; i++) { panel.NameInput.text = name.Substring(0, i); yield return Hold(.35f); }
                yield return Hold(.5f);
                panel.GenerateButton.button.onClick.Invoke(); PRVideoRecorder.Mark("generate");
                while (panel.Busy && PRVideoRecorder.Seconds < 90) yield return null;
                PRVideoRecorder.Mark("generated\t" + panel.Status);
                Debug.Log("[MojiBattle] PRVideo special: " + panel.Status + " / " + JsonUtility.ToJson(c.Current.EquippedSpecial));
                yield return Hold(4f);
                panel.Hide(); yield return Hold(1f);
                PRVideoRecorder.End();

                GameFlow.ToBattle(); yield return Settle();
                var boot = Object.FindAnyObjectByType<BattleBootstrap>(); var battle = boot.Battle;
                PRVideoRecorder.UseBattleResolution(); PRVideoRecorder.BattleZoom = .72f;
                PRVideoRecorder.Begin("battle_special"); Log(battle);
                PRVideoRecorder.BattleZoom = .72f;
                while (!battle.Director.Fighting) yield return null;
                yield return Hold(1.5f);
                var iv = battle.Context.Interventions; iv.Side(0).gauge = 1f; iv.Request(0, InterventionKind.Special, false);
                PRVideoRecorder.Mark("special");
                float until = PRVideoRecorder.Seconds + 10;
                while (PRVideoRecorder.Seconds < until)
                {
                    iv.Side(1).gauge = Mathf.Min(iv.Side(1).gauge, .9f);
                    if (battle.Right.Runtime.hp < 25) battle.Right.Runtime.hp = 25;
                    yield return null;
                }
                PRVideoRecorder.End(); PRVideoRecorder.BattleZoom = 1f;
            }
            finally
            {
                SpecialMoveLibrary.ClearAll();
                if (!string.IsNullOrEmpty(saved)) PlayerPrefs.SetString("MojiBattle.Specials.v1", saved);
            }
        }

        // ---------------------------------------------------------------- real 一 vs 木 matches (both AI)
        static readonly BattleStageId[] AllStages = { BattleStageId.Dojo, BattleStageId.Calligraphy, BattleStageId.EraserFactory,
            BattleStageId.Dictionary, BattleStageId.Letterpress, BattleStageId.Exam };

        /// <summary>
        /// Plays many unrecorded 一 vs 木 matches at high speed and scores how spectacular 一's attacks are
        /// (damage, criticals, knockdowns, launches, slams, KO). Writes PRVideo/footage/harvest.tsv.
        /// </summary>
        [UnityTest] public IEnumerator Harvest()
        {
            int seeds = int.TryParse(System.Environment.GetEnvironmentVariable("PR_HARVEST_SEEDS"), out int n) ? n : 24;
            var rows = new System.Collections.Generic.List<string> { "score\tstage\tseed\twinner\tichiHits\tkiHits\tkiGuards\tkiEvades\tstyles\tbig" };
            var saved = StageSession.Selected; bool koReplay = KoReplay.Enabled;
            TimeController.PresentationEffectsEnabled = false; KoReplay.Enabled = false;
            System.IO.File.WriteAllText(System.IO.Path.Combine(PRVideoRecorder.Root, "footage", "harvest.tsv"), rows[0] + "\n");
            try
            {
                foreach (var stage in AllStages)
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        yield return StartIchiVsKi(stage, true, seed);
                        var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle; var ev = battle.Context.Events;
                        float score = 0; int ichiHits = 0, kiHits = 0, guards = 0, evades = 0, winner = -1;
                        var styles = new System.Collections.Generic.HashSet<AttackStyle>(); var big = new System.Collections.Generic.List<string>();
                        ev.Hit += e =>
                        {
                            if (e.attacker != 0) { kiHits++; return; }
                            ichiHits++; styles.Add(e.style);
                            float v = e.damage + (e.critical ? 12 : 0) + (e.knockdown ? 15 : 0) + (e.launch ? 15 : 0) + (e.combo ? 20 : 0);
                            score += v; if (v >= 25) big.Add($"{e.time:0.0}:{e.style}{(e.critical ? "!" : "")}");
                        };
                        ev.EnvImpact += e => { if (e.fighter == 1 && (e.slam || e.smash)) { score += 20; big.Add($"{e.time:0.0}:slam"); } };
                        ev.Guard += e => { if (e.defender == 1) guards++; };
                        ev.Evaded += e => { if (e.fighter == 1) evades++; };
                        ev.KO += e => { winner = e.winner; if (e.winner == 0) { score += 40; big.Add($"{e.time:0.0}:KO-{e.style}"); } };
                        TimeController.SetSpectatorSpeed(24f);
                        double deadline = Time.realtimeSinceStartupAsDouble + 60;
                        while (!battle.Director.Ended && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                        if (winner < 0) winner = battle.Left.Runtime.hp >= battle.Right.Runtime.hp ? 0 : 1;
                        score += styles.Count * 6 + Mathf.Min(guards + evades, 10) * 2 - kiHits * 4;
                        if (winner != 0) score *= .3f;
                        string row = $"{score:0}\t{stage}\t{seed}\t{winner}\t{ichiHits}\t{kiHits}\t{guards}\t{evades}\t{string.Join(",", styles)}\t{string.Join(" ", big)}";
                        rows.Add(row);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(PRVideoRecorder.Root, "footage", "harvest.tsv"), row + "\n");
                        TimeController.ResetAll();
                    }
            }
            finally
            {
                TimeController.PresentationEffectsEnabled = true; KoReplay.Enabled = koReplay; StageSession.Selected = saved;
            }
        }

        /// <summary>Records each match listed in PRVideo/footage/picks.tsv ("stage seed") at normal and half speed.</summary>
        [UnityTest] public IEnumerator Picks()
        {
            var saved = StageSession.Selected;
            try
            {
                foreach (var line in System.IO.File.ReadAllLines(System.IO.Path.Combine(PRVideoRecorder.Root, "footage", "picks.tsv")))
                {
                    var f = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (f.Length < 2 || line.StartsWith("#")) continue;
                    var stage = (BattleStageId)System.Enum.Parse(typeof(BattleStageId), f[0]); int seed = int.Parse(f[1]);
                    foreach (float speed in new[] { 1f, .5f })
                    {
                        yield return StartIchiVsKi(stage, true, seed);
                        var battle = Object.FindAnyObjectByType<BattleBootstrap>().Battle;
                        TimeController.SetSpectatorSpeed(speed); PRVideoRecorder.BattleZoom = .72f; PRVideoRecorder.UseBattleResolution();
                        PRVideoRecorder.Begin($"pick_{stage}_{seed}{(speed < 1 ? "_slow" : "")}"); Log(battle);
                        float endAt = -1, cap = 80 / speed;
                        while (PRVideoRecorder.Seconds < cap)
                        {
                            if (battle.Director.Ended && endAt < 0) { endAt = PRVideoRecorder.Seconds; PRVideoRecorder.Mark("ended"); }
                            if (endAt >= 0 && PRVideoRecorder.Seconds - endAt > 6) break;
                            yield return null;
                        }
                        PRVideoRecorder.End(); PRVideoRecorder.BattleZoom = 1f; TimeController.ResetAll();
                    }
                }
            }
            finally { StageSession.Selected = saved; PRVideoRecorder.BattleZoom = 1f; }
        }

        static WorldCupScreen Cup => Object.FindAnyObjectByType<WorldCupScreen>();
        static IEnumerator CupReady()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 180;
            while (Time.realtimeSinceStartupAsDouble < deadline && (Cup == null || Cup.Busy)) yield return null;
            yield return Settle();
        }
        static void Finish(WorldCupSession s, WorldCupMatch m, int winner)
        {
            s.BeginMatch(m.Id, 60);
            WorldCupRuntimeStore.Record(m.Id, new MatchResult { seed = m.Seed, winner = winner, finishReason = "KO", elapsedSeconds = 20,
                hpRemaining = winner == 0 ? new[] { 50f, 0f } : new[] { 0f, 50f }, maxHp = new[] { 100f, 100f } }, false);
        }
        static void ComputersExceptBronze(WorldCupSession s)
        { for (int n = 0; n < 104; n++) { var m = s.NextComputerMatch; if (m == null || m.Bronze) break; Finish(s, m, 0); } }

        [UnityTest] public IEnumerator WorldCupFinal()
        {
            WorldCupRuntimeStore.ClearSession(); TimeController.PresentationEffectsEnabled = false;
            GameFlow.ToTournamentSelect(); yield return Settle();
            Click("TournamentChoiceStart1"); yield return Settle(); Click("WorldCupBegin"); yield return Settle();
            var wc = Object.FindAnyObjectByType<CustomizeScreen>().Customizer; wc.SetCharacter("一"); wc.SetFont(FontStyleId.Brush);
            Click("Battle"); yield return Settle(); var s = WorldCupRuntimeStore.Current;
            foreach (var m in s.Matches.Where(m => !m.IncludesPlayer).ToArray()) Finish(s, m, 0);
            foreach (var m in s.Matches.Take(72).Where(m => m.Ready).ToArray()) Finish(s, m, m.IncludesPlayer ? (m.LeftId == 0 ? 0 : 1) : 0);
            for (int round = 0; round < 4; round++) { ComputersExceptBronze(s); var m = s.NextPlayerMatch; Finish(s, m, m.LeftId == 0 ? 0 : 1); }
            ComputersExceptBronze(s); Finish(s, s.Match(103), 0);
            TimeController.PresentationEffectsEnabled = true;
            Cup.Open(WorldCupPage.Bracket); yield return CupReady();
            PRVideoRecorder.Begin("worldcup_bracket"); yield return Hold(4); PRVideoRecorder.End();
            Click("WorldCupNext"); yield return null;
            PRVideoRecorder.Begin("worldcup_final_vs"); yield return Hold(5); PRVideoRecorder.End();
            var final = s.NextPlayerMatch; Finish(s, final, final.LeftId == 0 ? 0 : 1);
            Cup.Open(WorldCupPage.Ceremony); yield return null;
            PRVideoRecorder.Begin("worldcup_ceremony"); yield return Hold(14);
        }
    }
}
