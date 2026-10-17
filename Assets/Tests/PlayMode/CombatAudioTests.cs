using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace MojiBattle.Tests
{
    public class CombatAudioTests
    {
        BattleInstance battle;
        readonly List<CombatSoundPlayback> played = new List<CombatSoundPlayback>();

        [TearDown]
        public void Cleanup()
        {
            battle?.Destroy();
            played.Clear();
            SimHarness.End();
        }

        CombatAudioDirector Audio(bool presentation = false, float countdown = 0f)
        {
            SimHarness.Begin(true);
            battle = SimHarness.Build(13, presentation, countdown, left: "火", right: "山");
            var audio = battle.Root.GetComponent<CombatAudioDirector>();
            if (audio == null) { audio = battle.Root.AddComponent<CombatAudioDirector>(); audio.Bind(battle.Director); }
            if (Object.FindFirstObjectByType<AudioListener>() == null) battle.Root.AddComponent<AudioListener>();
            audio.SoundPlayed += e => played.Add(e);
            return audio;
        }

        [UnityTest]
        public IEnumerator Presentation_LoadsAllClips_CountdownAndPauseDoNotRepeatGong()
        {
            var audio = Audio(true, .2f);
            Assert.AreEqual(44, audio.LoadedClipCount);
            Assert.IsNotNull(Object.FindFirstObjectByType<AudioListener>());
            var sources = audio.GetComponentsInChildren<AudioSource>();
            Assert.AreEqual(CombatAudioDirector.VoiceLimit + 1, sources.Length);
            // Icons must stay outside the rendered view even before Unity registers
            // their annotation settings or when Game View gizmos are enabled.
            foreach (var source in sources)
            {
                var cam = battle.CameraRig.Cam;
                Assert.Less(cam.WorldToViewportPoint(source.transform.position).z, cam.nearClipPlane);
                Assert.AreEqual(0f, source.spatialBlend);
                Assert.IsFalse(source.playOnAwake);
            }
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(played.Any(e => e.sound == CombatSound.StartGong));
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(1, played.Count(e => e.sound == CombatSound.StartGong));
            TimeController.SetPaused(true);
            int count = audio.PlayedCount;
            battle.Context.Events.Raise(new FighterMotionEvent { fighter = 0, kind = FighterMotionKind.Jump });
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(count, audio.PlayedCount);
            TimeController.SetPaused(false);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(1, played.Count(e => e.sound == CombatSound.StartGong));
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio/Combat"))
            {
                Assert.Greater(clip.samples, 1000, clip.name);
                Assert.AreEqual(1, clip.channels, clip.name);
                var data = new float[clip.samples];
                Assert.IsTrue(clip.GetData(data, 0), clip.name);
                Assert.Greater(data.Max(v => Mathf.Abs(v)), .01f, clip.name + " is silent");
                Assert.Less(data.Max(v => Mathf.Abs(v)), .99f, clip.name + " clips");
            }
        }

        [UnityTest]
        public IEnumerator CombatEvents_SelectEverySound_AndSuppressDuplicateLandings()
        {
            var audio = Audio();
            battle.Director.enabled = false;
            yield return null;
            var e = battle.Context.Events;
            // Verify rare gameplay outcomes without relying on an AI seed to produce them.
            foreach (var kind in new[] { FighterMotionKind.Jump, FighterMotionKind.Throw, FighterMotionKind.Pickup })
            { e.Raise(new FighterMotionEvent { fighter = 0, kind = kind }); yield return null; }
            e.Raise(new EvadeEvent { fighter = 0, kind = EvadeKind.Backstep }); yield return null;
            e.Raise(new RecoverEvent { fighter = 0 }); yield return null;
            e.Raise(new FighterMotionEvent { fighter = 0, kind = FighterMotionKind.Riposte }); yield return null;
            e.Raise(new FighterMotionEvent { fighter = 0, kind = FighterMotionKind.Whirl }); yield return null;
            battle.Left.Runtime.attackStyle = AttackStyle.DashSlash;
            battle.Left.SetState(FighterState.AttackActive);
            e.Raise(new FighterMotionEvent { fighter = 0, kind = FighterMotionKind.Dash, time = battle.Context.SimTime });
            battle.Context.SimTime += .1f;
            yield return null;
            e.Raise(new StateChangeEvent { fighter = 0, to = FighterState.AttackWindup }); yield return null;
            battle.Left.Runtime.attackStyle = AttackStyle.Overhead;
            battle.Left.WeightClass = WeightClass.Light;
            battle.Left.Weapon.maxSide = 1f;
            e.Raise(new StateChangeEvent { fighter = 0, to = FighterState.AttackActive }); yield return null;
            battle.Left.WeightClass = WeightClass.Heavy;
            e.Raise(new StateChangeEvent { fighter = 0, to = FighterState.AttackActive }); yield return null;
            for (int i = 0; i < 3; i++)
            { e.Raise(new HitEvent { defender = 1, damage = 3, quality = HitQuality.Normal }); yield return null; }
            e.Raise(new HitEvent { defender = 1, damage = 30 }); yield return null;
            e.Raise(new HitEvent { defender = 1, damage = 4, critical = true }); yield return null;
            e.Raise(new HitEvent { defender = 1, damage = 4, launch = true }); yield return null;
            e.Raise(new HitEvent { defender = 1, damage = 4, combo = true }); yield return null;
            foreach (var style in new[] { AttackStyle.DashSlash, AttackStyle.Riposte, AttackStyle.Whirl })
            { e.Raise(new HitEvent { defender = 1, damage = 4, style = style }); yield return null; }
            e.Raise(new GuardEvent { defender = 1 }); yield return null;
            e.Raise(new GuardEvent { defender = 1, broke = true }); yield return null;
            e.Raise(new GuardEvent { defender = 1, parried = true }); yield return null;
            // 必殺技: 発動のカットイン（シャキーン）と、技の当たりの炸裂音
            var iv = new BattleIntervention(battle.Director, InterventionController.Local, InterventionController.Com);
            battle.Context.Interventions = iv;
            e.Raise(new InterventionEvent { fighter = 0, kind = InterventionKind.Special, success = true }); yield return null;
            e.Raise(new InterventionEvent { fighter = 1, kind = InterventionKind.Cheer, success = true }); yield return null;
            iv.Side(0).chainActive = true;
            e.Raise(new HitEvent { attacker = 0, defender = 1, damage = 9 }); yield return null;
            iv.Side(0).chainActive = false; battle.Context.Interventions = null;
            // Let high-priority impacts finish before checking lower-priority surfaces.
            yield return new WaitForSecondsRealtime(1f);
            e.Raise(new ClashEvent { attacker = 0 }); yield return null;
            foreach (var sound in new[] { CombatSound.LandLight, CombatSound.LandHeavy, CombatSound.Wall, CombatSound.Fall })
            {
                battle.Context.SimTime += 1f;
                if (sound == CombatSound.LandLight || sound == CombatSound.LandHeavy)
                {
                    // Establish a real airborne presentation frame before the contact.
                    battle.Right.Body.position = new Vector2(battle.Right.X, .8f);
                    yield return null;
                    battle.Right.Body.position = new Vector2(battle.Right.X, .02f);
                }
                e.Raise(new SurfaceContactEvent { fighter = 1, time = battle.Context.SimTime - .01f,
                    speed = sound == CombatSound.LandLight ? 2f : 7f,
                    isWall = sound == CombatSound.Wall, down = sound == CombatSound.Fall });
                yield return null;
            }
            int lands = played.Count(p => p.sound == CombatSound.LandHeavy);
            battle.Context.SimTime += 1f;
            e.Raise(new SurfaceContactEvent { fighter = 1, time = battle.Context.SimTime - .01f, speed = 8f });
            e.Raise(new EnvImpactEvent { fighter = 1, time = battle.Context.SimTime, smash = true });
            yield return null;
            Assert.AreEqual(lands, played.Count(p => p.sound == CombatSound.LandHeavy));
            e.Raise(new KoEvent { winner = 0 }); yield return null;
            e.Raise(new MatchResult { winner = 0 }); yield return null;
            e.Raise(new MatchResult { winner = 0 }); yield return null;
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.EndGong));
            var expected = (CombatSound[])System.Enum.GetValues(typeof(CombatSound));
            foreach (var sound in expected.Where(s => s != CombatSound.Step))
                Assert.IsTrue(played.Any(p => p.sound == sound), "Missing event: " + sound);
            Assert.AreEqual(3, played.Where(p => p.sound == CombatSound.Hit).Select(p => p.clip.name).Distinct().Count());
            Assert.IsTrue(played.All(p => p.clip != null && p.volume > 0f && p.volume <= 1f && p.pitch > 0f));
            Assert.AreEqual(44, audio.LoadedClipCount);
        }

        IEnumerator Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var ctx = battle.Context;
                ctx.SimTime += TimeController.BaseFixedDelta;
                foreach (var f in battle.Director.Fighters)
                { f.Tick(ctx.SimTime, TimeController.BaseFixedDelta); f.CachePreStep(); }
                battle.Scene.GetPhysicsScene2D().Simulate(TimeController.BaseFixedDelta);
                ctx.Hits.ResolveStep(ctx.SimTime);
                ctx.Env.ResolveStep(ctx.SimTime);
                yield return null;
            }
        }

        IEnumerator WaitForMusic(CombatAudioDirector audio)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5f;
            while (!audio.MusicSource.isPlaying && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.IsTrue(audio.MusicSource.isPlaying, "BGM did not start after the gong.");
        }

        [UnityTest]
        public IEnumerator RandomBattleMusic_AllThreePlay_FinalIsFixed_AndCombatRngIsUnchanged()
        {
            var initialAudio = Audio(countdown: 1000f);
            battle.Director.enabled = false;
            initialAudio.enabled = false;
            Time.captureDeltaTime = 0f;
            battle.Left.Brain = new FighterBrain(battle.Left, new MatchRandom(123));
            battle.Right.Brain = new FighterBrain(battle.Right, new MatchRandom(456));
            var expectedLeft = new MatchRandom(123);
            var expectedRight = new MatchRandom(456);
            var unityRandom = UnityEngine.Random.state;
            var tracks = new HashSet<string>();
            var counts = new Dictionary<string, int>();
            // Every bind uses the same match/seed; music still varies, while the
            // final bypasses the regular music pool.
            for (int i = 0; i < 100; i++)
            {
                bool final = i % 10 == 0;
                var host = new GameObject("MusicSelectionTest");
                host.transform.SetParent(battle.Root.transform);
                var audio = host.AddComponent<CombatAudioDirector>();
                audio.enabled = false;
                audio.Bind(battle.Director, final);
                var clip = audio.MusicSource.clip;
                Assert.IsNotNull(clip);
                if (final) Assert.AreEqual("final2", clip.name);
                else
                {
                    CollectionAssert.Contains(new[] { "battle", "battle2", "battle3" }, clip.name);
                    counts[clip.name] = counts.TryGetValue(clip.name, out int count) ? count + 1 : 1;
                    if (tracks.Add(clip.name))
                    {
                        Assert.AreEqual(AudioClipLoadType.Streaming, clip.loadType);
                        Assert.IsTrue(audio.MusicSource.loop);
                        audio.MusicSource.volume = 0f;
                        audio.MusicSource.Play();
                        yield return new WaitForSecondsRealtime(.2f);
                        Assert.IsTrue(audio.MusicSource.isPlaying, clip.name);
                        Assert.Greater(audio.MusicSource.timeSamples, 0, clip.name + " must decode and progress.");
                    }
                }
                Object.Destroy(host);
                yield return null;
            }
            CollectionAssert.AreEquivalent(new[] { "battle", "battle2", "battle3" }, tracks);
            Assert.AreEqual(unityRandom, UnityEngine.Random.state, "Music must not consume the shared Unity RNG.");
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(expectedLeft.NextUInt(), battle.Left.Brain.Rng.NextUInt());
                Assert.AreEqual(expectedRight.NextUInt(), battle.Right.Brain.Rng.NextUInt());
            }
            Debug.Log("[BGM RANDOM] " + string.Join(", ", counts.Select(pair => pair.Key + "=" + pair.Value)) + "; final=10/10");
        }

        [UnityTest]
        public IEnumerator BattleMusic_WaitsForGong_LoopsAndStopsAtMatchEnd()
        {
            var audio = Audio(true, .2f);
            CollectionAssert.Contains(new[] { "battle", "battle2", "battle3" }, audio.MusicSource.clip.name);
            Assert.AreEqual(2, audio.MusicSource.clip.channels);
            Assert.Greater(audio.MusicSource.clip.length, 50f);
            Assert.IsTrue(audio.MusicSource.loop);
            Assert.IsFalse(audio.MusicSource.isPlaying);
            Assert.AreEqual(AudioClipLoadType.Streaming, audio.MusicSource.clip.loadType);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(audio.MusicSource.isPlaying, "BGM must stay silent during countdown.");
            while (!played.Any(e => e.sound == CombatSound.StartGong)) yield return null;
            battle.Director.enabled = false;
            Time.captureDeltaTime = 0f;
            var gong = audio.GetComponentsInChildren<AudioSource>()
                .Single(s => s.clip != null && s.clip.name == "gong_start");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(gong.isPlaying);
            Assert.IsFalse(audio.MusicSource.isPlaying, "BGM must wait for the gong to finish.");
            yield return WaitForMusic(audio);
            Assert.IsFalse(gong.isPlaying, "BGM must start after the gong.");
            Assert.AreEqual(audio.MasterVolume * audio.MusicVolume * GameSettings.MusicGain, audio.MusicSource.volume, .001f);
            Assert.AreEqual(1f, audio.MusicSource.pitch);
            TimeController.SetSpectatorSpeed(3f);
            yield return null;
            Assert.AreEqual(1f, audio.MusicSource.pitch, "Spectator speed must not change music pitch.");
            battle.Context.Events.Raise(new MatchResult { winner = 0 });
            Assert.IsFalse(audio.MusicSource.isPlaying);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsFalse(audio.MusicSource.isPlaying);
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.EndGong));
        }

        [UnityTest]
        public IEnumerator FinalMusic_WaitsForGong_AndStopsAtMatchEnd()
        {
            SimHarness.Begin(true);
            battle = BattleBuilder.Build(new MatchConfig
            {
                left = new FighterLoadout("火", FontStyleId.Gothic),
                right = new FighterLoadout("山", FontStyleId.Gothic),
                seed = 13, durationSeconds = 60f,
            }, CombatBalance.Default, GlyphCalibration.Default, true, .2f, out var error, tournamentFinal: true);
            Assert.IsNotNull(battle, error);
            var audio = battle.Root.GetComponent<CombatAudioDirector>();
            audio.SoundPlayed += e => played.Add(e);
            Assert.AreEqual("final2", audio.MusicSource.clip.name);
            Assert.AreEqual(AudioClipLoadType.Streaming, audio.MusicSource.clip.loadType);
            Assert.IsTrue(audio.MusicSource.loop);
            Assert.IsFalse(audio.MusicSource.isPlaying);
            while (!played.Any(e => e.sound == CombatSound.StartGong)) yield return null;
            battle.Director.enabled = false;
            Time.captureDeltaTime = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsFalse(audio.MusicSource.isPlaying);
            yield return WaitForMusic(audio);
            var gong = audio.GetComponentsInChildren<AudioSource>().Single(s => s.clip != null && s.clip.name == "gong_start");
            Assert.IsFalse(gong.isPlaying);
            battle.Context.Events.Raise(new MatchResult { winner = 0 });
            Assert.IsFalse(audio.MusicSource.isPlaying);
        }

        [UnityTest]
        public IEnumerator BattleMusic_PausePreservesGongDelayAndPlaybackPosition()
        {
            var audio = Audio();
            battle.Director.enabled = false;
            Time.captureDeltaTime = 0f;
            while (!played.Any(e => e.sound == CombatSound.StartGong)) yield return null;
            TimeController.SetPaused(true);
            yield return null;
            yield return new WaitForSecondsRealtime(2.7f);
            Assert.IsFalse(audio.MusicSource.isPlaying);
            TimeController.SetPaused(false);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsFalse(audio.MusicSource.isPlaying, "Pausing must preserve the remaining gong delay.");
            yield return WaitForMusic(audio);
            yield return new WaitForSecondsRealtime(.15f);
            TimeController.SetPaused(true);
            yield return null;
            yield return null;
            int position = audio.MusicSource.timeSamples;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual(position, audio.MusicSource.timeSamples);
            Assert.IsFalse(audio.MusicSource.isPlaying);
            TimeController.SetPaused(false);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.IsTrue(audio.MusicSource.isPlaying);
            Assert.Greater(audio.MusicSource.timeSamples, position, "Resume must continue rather than restart.");
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.StartGong));
            audio.enabled = false;
            Assert.IsFalse(audio.MusicSource.isPlaying);
        }

        [UnityTest]
        public IEnumerator BattleMusic_EndDuringGongCancelsPendingStart_AndRematchStartsSilent()
        {
            var audio = Audio();
            battle.Director.enabled = false;
            Time.captureDeltaTime = 0f;
            while (!played.Any(e => e.sound == CombatSound.StartGong)) yield return null;
            battle.Context.Events.Raise(new MatchResult { winner = 0 });
            yield return new WaitForSecondsRealtime(2.7f);
            Assert.IsFalse(audio.MusicSource.isPlaying, "An ended match must not start its pending music.");
            battle.Destroy();
            yield return null;
            Assert.IsTrue(audio == null, "The previous match's music source must be destroyed.");
            played.Clear();
            audio = Audio(true, .2f);
            yield return null;
            Assert.IsFalse(audio.MusicSource.isPlaying);
            Assert.IsFalse(played.Any(e => e.sound == CombatSound.StartGong));
        }

        [UnityTest]
        public IEnumerator BattleMusic_DisabledPresentationDoesNotStartAudio()
        {
            var audio = Audio();
            TimeController.PresentationEffectsEnabled = false;
            battle.Director.enabled = false;
            yield return null;
            Assert.AreEqual(0, audio.PlayedCount);
            Assert.IsFalse(audio.MusicSource.isPlaying);
        }

        [UnityTest]
        public IEnumerator BattleMusic_ReusedGongVoiceDoesNotDelayStart()
        {
            var audio = Audio();
            battle.Director.enabled = false;
            while (!played.Any(e => e.sound == CombatSound.StartGong)) yield return null;
            var gong = audio.GetComponentsInChildren<AudioSource>()
                .Single(s => s.clip != null && s.clip.name == "gong_start");
            // Reproduce the frame where a completed gong's free voice is used
            // by another combat sound before the presentation Update runs.
            gong.Stop();
            battle.Context.Events.Raise(new FighterMotionEvent { fighter = 0, kind = FighterMotionKind.Jump });
            Assert.AreEqual("jump", gong.clip.name);
            Assert.IsTrue(gong.isPlaying);
            yield return null;
            Assert.IsTrue(audio.MusicSource.isPlaying, "A reused effect voice must not block the BGM.");
        }

        [UnityTest]
        public IEnumerator ActualLeapThrowAndPickup_EmitAtPhysicalMotion()
        {
            Audio();
            battle.Director.enabled = false;
            battle.Context.AiEnabled = false;
            yield return Step(2);
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.Leap);
            yield return Step(100);
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.Jump));
            Assert.IsTrue(played.Any(p => p.sound == CombatSound.SwingLight || p.sound == CombatSound.SwingHeavy));
            Assert.IsTrue(played.Any(p => p.sound == CombatSound.LandLight || p.sound == CombatSound.LandHeavy));
            battle.Left.SetState(FighterState.Approach);
            battle.Left.StartAttack(battle.Context.SimTime, AttackStyle.Throw);
            int steps = 0;
            while (!played.Any(p => p.sound == CombatSound.Throw) && steps++ < 300) yield return Step(1);
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.Throw));
            yield return Step(600);
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.Pickup));
        }

        [UnityTest]
        public IEnumerator RealFight_PlaysFootstepsHitsAndFinishGong()
        {
            Audio(true);
            yield return SimHarness.RunToEnd(battle, 3f);
            Assert.IsTrue(battle.Director.Ended);
            Assert.IsTrue(played.Any(p => p.sound == CombatSound.Step));
            Assert.IsTrue(played.Any(p => p.sound == CombatSound.Hit || p.sound == CombatSound.HeavyHit || p.sound == CombatSound.Critical));
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.StartGong));
            Assert.AreEqual(1, played.Count(p => p.sound == CombatSound.EndGong));
            Debug.Log("[AUDIO] Real fight: " + string.Join(", ", played.GroupBy(p => p.sound).Select(g => g.Key + "=" + g.Count())));
        }
    }
}
