using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    public enum CombatSound
    {
        StartGong, EndGong, Jump, LandLight, LandHeavy, SwingLight, SwingHeavy,
        Hit, HeavyHit, Critical, Guard, Clash, GuardBreak, Wall, Fall, Launch,
        Slam, Evade, Throw, Pickup, Charge, Step, Recover, KO,
        DashStep, DashSwing, DashHit, Parry, CounterSwing, CounterHit, WhirlSwing, WhirlHit,
        SpecialCutIn, SpecialImpact, Cheer
    }

    public struct CombatSoundPlayback
    {
        public CombatSound sound;
        public AudioClip clip;
        public float volume, pitch;
        public int fighter;
    }

    /// <summary>Presentation only. Sound variation never consumes the combat RNG.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CombatAudioDirector : MonoBehaviour
    {
        [Range(0f, 1f)] public float MasterVolume = .8f;
        [Range(0f, 1f)] public float MusicVolume = .35f;
        public AudioSource MusicSource { get; private set; }
        public event Action<CombatSoundPlayback> SoundPlayed;
        public int LoadedClipCount { get; private set; }
        public int PlayedCount { get; private set; }
        public const int VoiceLimit = 12;

        // Music draws are independent of both the match seed and Unity's global RNG.
        // Draw afresh for every match, allowing consecutive repeats.
        static readonly System.Random MusicRandom = new System.Random();

        static readonly string[][] Files =
        {
            new[] { "gong_start" }, new[] { "gong_end" }, new[] { "jump" },
            new[] { "land_light" }, new[] { "land_heavy" },
            new[] { "swing_light_01", "swing_light_02" },
            new[] { "swing_heavy_01", "swing_heavy_02" },
            new[] { "hit_01", "hit_02", "hit_03" },
            new[] { "hit_heavy_01", "hit_heavy_02" },
            new[] { "critical" }, new[] { "guard" }, new[] { "clash" },
            new[] { "guard_break" }, new[] { "wall" }, new[] { "fall" },
            new[] { "launch" }, new[] { "slam" }, new[] { "evade" },
            new[] { "throw" }, new[] { "pickup" }, new[] { "charge" },
            new[] { "step_01", "step_02" }, new[] { "recover" }, new[] { "ko" },
            new[] { "dash_step" }, new[] { "dash_swing" }, new[] { "dash_hit" }, new[] { "parry" },
            new[] { "counter_swing" }, new[] { "counter_hit" }, new[] { "whirl_swing" }, new[] { "whirl_hit" },
            new[] { "special_cutin" }, new[] { "special_impact" },
            new[] { "cheer_01", "cheer_02", "cheer_03", "cheer_04" },
        };
        readonly AudioSource[] voices = new AudioSource[VoiceLimit];
        readonly float[] voiceGain = new float[VoiceLimit];
        readonly int[] voicePriority = new int[VoiceLimit];
        readonly double[] voiceStarted = new double[VoiceLimit];
        readonly int[] variants = new int[Files.Length];
        readonly float[] footsteps = new float[2];
        readonly float[] lastStep = { -10f, -10f };
        readonly bool[] airborne = new bool[2];
        readonly float[] dashSwingAt = { -1f, -1f };
        readonly float[,] lastContact = { { -10f, -10f }, { -10f, -10f } };
        readonly List<SurfaceContactEvent> contacts = new List<SurfaceContactEvent>();
        AudioClip[][] clips;
        MatchDirector director;
        bool started, ended, paused;
        bool musicPending;
        AudioSource startGong;

        public void Bind(MatchDirector match, bool tournamentFinal = false)
        {
            director = match;
            clips = new AudioClip[Files.Length][];
            for (int i = 0; i < Files.Length; i++)
            {
                clips[i] = new AudioClip[Files[i].Length];
                for (int j = 0; j < Files[i].Length; j++)
                {
                    clips[i][j] = Resources.Load<AudioClip>("Audio/Combat/" + Files[i][j]);
                    if (clips[i][j] != null) LoadedClipCount++;
                    else Debug.LogError("Missing combat sound: " + Files[i][j]);
                }
            }
            // These are nonspatial (2D) voices. Keep their editor speaker gizmos
            // behind the camera, including the frame before annotations are registered.
            var audioRoot = new GameObject("CombatAudioVoices");
            audioRoot.transform.SetParent(transform, false);
            var camera = Camera.main;
            audioRoot.transform.position = camera != null
                ? camera.transform.position - camera.transform.forward * 100f
                : transform.position + Vector3.back * 100f;
            for (int i = 0; i < VoiceLimit; i++)
            {
                var source = audioRoot.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                voices[i] = source;
            }
            MusicSource = audioRoot.AddComponent<AudioSource>();
            MusicSource.playOnAwake = false;
            MusicSource.loop = true;
            MusicSource.spatialBlend = 0f;
            MusicSource.dopplerLevel = 0f;
            MusicSource.pitch = 1f;
            MusicSource.priority = 150;
            var music = Resources.Load<BattleMusicSettings>("Audio/BattleMusic");
            if (music != null)
            {
                if (tournamentFinal) MusicSource.clip = music.finalLoop;
                else
                {
                    var tracks = new[] { music.battleLoop, music.battleLoop2, music.battleLoop3 };
                    int selected = GameSettings.BattleMusic;
                    MusicSource.clip = tracks[selected == 0 ? MusicRandom.Next(tracks.Length) : selected - 1];
                }
            }
            if (MusicSource.clip == null) Debug.LogError("Missing battle music: Audio/BattleMusic");
            var e = match.Events;
            e.Hit += OnHit;
            e.Guard += OnGuard;
            e.Clash += OnClash;
            e.EnvImpact += OnEnvironment;
            e.SurfaceContact += OnSurface;
            e.StateChanged += OnState;
            e.Motion += OnMotion;
            e.RangedLaunch += OnRangedLaunch;
            e.Evaded += OnEvade;
            e.Recovered += OnRecover;
            e.KO += OnKo;
            e.MatchEnded += OnEnd;
            e.Intervention += OnIntervention;
        }

        void OnDestroy()
        {
            if (director == null || director.Context == null) return;
            var e = director.Events;
            e.Hit -= OnHit;
            e.Guard -= OnGuard;
            e.Clash -= OnClash;
            e.EnvImpact -= OnEnvironment;
            e.SurfaceContact -= OnSurface;
            e.StateChanged -= OnState;
            e.Motion -= OnMotion;
            e.RangedLaunch -= OnRangedLaunch;
            e.Evaded -= OnEvade;
            e.Recovered -= OnRecover;
            e.KO -= OnKo;
            e.MatchEnded -= OnEnd;
            e.Intervention -= OnIntervention;
        }

        /// <summary>必殺技: 発動のカットインで「シャキーン」、技の当たりごとに重い炸裂音。</summary>
        void OnIntervention(InterventionEvent e)
        {
            if (e.kind == InterventionKind.Special && e.success && e.step == 0) Play(CombatSound.SpecialCutIn, -1, 1f, 100);
            // 声援: 掛け声と歓声。効果の間にもう一声かける
            if (e.kind == InterventionKind.Cheer && e.success)
            {
                Play(CombatSound.Cheer, -1, .9f, 95);
                if (!e.brace) cheerEchoAt[Mathf.Clamp(e.fighter, 0, 1)] = Time.unscaledTime + 1.5f;
            }
        }

        readonly float[] cheerEchoAt = { -1f, -1f };

        void TickCheerEcho()
        {
            var iv = director.Context.Interventions;
            for (int i = 0; i < 2; i++)
            {
                if (cheerEchoAt[i] < 0f || Time.unscaledTime < cheerEchoAt[i] || TimeController.Paused) continue;
                cheerEchoAt[i] = -1f;
                if (iv != null && iv.Now <= iv.Side(i).cheerUntil && !director.Ended) Play(CombatSound.Cheer, -1, .75f, 90);
            }
        }

        bool Live => director != null && !director.Context.MatchOver && director.CountdownRemaining <= 0f;
        Fighter Fighter(int id) => director.Fighters[Mathf.Clamp(id, 0, 1)];
        bool Heavy(Fighter f) => f.WeightClass == WeightClass.Heavy || f.Weapon.maxSide >= 2.5f;

        void Update()
        {
            if (director == null) return;
            if (paused != TimeController.Paused)
            {
                paused = TimeController.Paused;
                foreach (var source in voices)
                    if (paused) source.Pause(); else source.UnPause();
                if (paused)
                {
                    MusicSource.Pause();
                }
                else
                {
                    MusicSource.UnPause();
                }
            }
            TickCheerEcho();
            MusicSource.volume = MasterVolume * MusicVolume * GameSettings.MusicGain;
            MusicSource.mute = !TimeController.PresentationEffectsEnabled || SettingsScreen.Previewing;
            for (int i = 0; i < voices.Length; i++)
                voices[i].volume = voiceGain[i] * MasterVolume * GameSettings.EffectsGain;
            // Read the actual voice rather than clip.length: output buffering can
            // keep the gong playing a little longer than its nominal duration.
            if (musicPending && !paused && !ended && startGong != null
                && (startGong.clip != clips[(int)CombatSound.StartGong][0] || !startGong.isPlaying))
            {
                musicPending = false;
                if (MusicSource.clip != null) MusicSource.Play();
            }
        }

        void OnDisable()
        {
            musicPending = false;
            if (MusicSource != null) MusicSource.Stop();
        }

        void FixedUpdate()
        {
            if (director == null) return;
            if (!started && !ended && Live)
            {
                started = true;
                startGong = Play(CombatSound.StartGong, -1, 1f, 100);
                musicPending = startGong != null && MusicSource.clip != null;
            }
            // Wait until the resolver has processed this physics step. Damaging slams
            // replace ordinary landing sounds at the same contact.
            for (int i = contacts.Count - 1; i >= 0; i--)
            {
                var c = contacts[i];
                if (c.time >= director.Context.SimTime) continue;
                contacts.RemoveAt(i);
                if (!c.isWall && !c.down)
                {
                    if (!airborne[c.fighter]) continue;
                    airborne[c.fighter] = false;
                }
                if (!Live || c.speed < (c.isWall ? 2f : 1.2f)) continue;
                int surface = c.isWall ? 1 : 0;
                if (c.time - lastContact[c.fighter, surface] < .22f) continue;
                lastContact[c.fighter, surface] = c.time;
                var sound = c.isWall ? CombatSound.Wall : c.down ? CombatSound.Fall
                    : c.speed > 4.5f || Heavy(Fighter(c.fighter)) ? CombatSound.LandHeavy : CombatSound.LandLight;
                Play(sound, c.fighter, Mathf.Clamp(c.speed / 6f, .45f, 1f), 60);
            }
            foreach (var f in director.Fighters)
            {
                if (dashSwingAt[f.Id] >= 0f && director.Context.SimTime >= dashSwingAt[f.Id])
                {
                    dashSwingAt[f.Id] = -1f;
                    if (Live && f.Runtime.attackStyle == AttackStyle.DashSlash && f.Runtime.state == FighterState.AttackActive)
                        Play(CombatSound.DashSwing, f.Id, .65f, 45);
                }
                if (Live && f.Body.position.y > .4f && !f.Runtime.IsDown) airborne[f.Id] = true;
                if (!Live || !f.IsGrounded || f.Runtime.state != FighterState.Approach)
                { footsteps[f.Id] = 0f; continue; }
                float speed = Mathf.Abs(f.PreBodyVelocity.x);
                if (speed < .5f) { footsteps[f.Id] = 0f; continue; }
                footsteps[f.Id] += speed * Time.fixedDeltaTime;
                if (footsteps[f.Id] < .7f || Time.unscaledTime - lastStep[f.Id] < .16f) continue;
                footsteps[f.Id] %= .7f;
                lastStep[f.Id] = Time.unscaledTime;
                Play(CombatSound.Step, f.Id, .22f, 10);
            }
        }

        void OnMotion(FighterMotionEvent e)
        {
            if (!Live) return;
            switch (e.kind)
            {
                case FighterMotionKind.AerialChase: Play(CombatSound.Jump, e.fighter, .65f, 40); return;
                case FighterMotionKind.AerialSlash:
                case FighterMotionKind.AerialSmash:
                case FighterMotionKind.BindCounter: return; // Attack state owns the swing; never use the pickup fallback.
                case FighterMotionKind.Duck:
                case FighterMotionKind.WallJump:
                case FighterMotionKind.RecoverRoll: return; // EvadeEvent owns these sounds.
                case FighterMotionKind.DuckCounter: return; // The rising attack owns its swing.
                case FighterMotionKind.Feint: Play(CombatSound.SwingLight, e.fighter, .25f, 30); return;
                case FighterMotionKind.Bind: return; // ClashEvent owns the collision sound.
                case FighterMotionKind.BindPush:
                case FighterMotionKind.HookPull:
                case FighterMotionKind.HookReversed: Play(CombatSound.GuardBreak, e.fighter, .55f, 45); return;
                case FighterMotionKind.BindEscape: Play(CombatSound.Evade, e.fighter, .4f, 35); return;
                case FighterMotionKind.FrontKick:
                case FighterMotionKind.RecoverKick: Play(CombatSound.SwingLight, e.fighter, .4f, 40); return;
                case FighterMotionKind.PickupSlash: return; // PickupEvent and attack state own the two sounds.
                case FighterMotionKind.GroundWave:
                case FighterMotionKind.StrokeShot: return; // The release event owns the sound.
            }
            if (e.kind == FighterMotionKind.Dash)
            {
                Play(CombatSound.DashStep, e.fighter, .55f, 45);
                dashSwingAt[e.fighter] = e.time + director.Context.Balance.dashSlashDelay;
                return;
            }
            if (e.kind == FighterMotionKind.Riposte) { Play(CombatSound.CounterSwing, e.fighter, .65f, 50); return; }
            if (e.kind == FighterMotionKind.Whirl) { Play(CombatSound.WhirlSwing, e.fighter, .65f, 50); return; }
            var sound = e.kind == FighterMotionKind.Jump ? CombatSound.Jump
                : e.kind == FighterMotionKind.Throw ? CombatSound.Throw : CombatSound.Pickup;
            Play(sound, e.fighter, .65f, 40);
        }

        void OnRangedLaunch(RangedLaunchEvent e)
        {
            if (Live) Play(e.style == AttackStyle.GroundWave ? CombatSound.Slam : CombatSound.CounterSwing,
                e.fighter, e.style == AttackStyle.GroundWave ? .75f : .6f, 60);
        }

        void OnState(StateChangeEvent e)
        {
            if (!Live) return;
            var f = Fighter(e.fighter);
            if (e.to == FighterState.AttackWindup && f.Runtime.attackStyle != AttackStyle.Riposte) Play(CombatSound.Charge, e.fighter, .18f, 15);
            if (e.to == FighterState.AttackActive && f.Runtime.attackStyle != AttackStyle.Throw
                && !AttackTechniques.IsKick(f.Runtime.attackStyle)
                && !AttackTechniques.IsRanged(f.Runtime.attackStyle)
                && f.Runtime.attackStyle != AttackStyle.DashSlash && f.Runtime.attackStyle != AttackStyle.Riposte && f.Runtime.attackStyle != AttackStyle.Whirl)
                Play(Heavy(f) ? CombatSound.SwingHeavy : CombatSound.SwingLight, e.fighter, .5f, 40);
        }

        void OnHit(HitEvent e)
        {
            if (ended || e.damage <= 0f) return;
            var iv = director.Context.Interventions;
            if (iv != null && (iv.Side(e.attacker).OwnsAttack(e.attackId) || iv.InSpecial(e.attacker)))
                Play(CombatSound.SpecialImpact, -1, 1f, 90);
            var sound = e.style == AttackStyle.DashSlash ? CombatSound.DashHit
                : e.style == AttackStyle.Riposte ? CombatSound.CounterHit : e.style == AttackStyle.Whirl ? CombatSound.WhirlHit
                : e.launch ? CombatSound.Launch : e.combo || e.style == AttackStyle.Smash ? CombatSound.Slam
                : e.critical ? CombatSound.Critical : e.damage >= director.Context.Balance.heavyHitDamage
                    || e.knockdown ? CombatSound.HeavyHit : CombatSound.Hit;
            Play(sound, e.defender, e.quality == HitQuality.Graze ? .4f : .9f, 80);
        }
        void OnGuard(GuardEvent e)
        {
            if (!ended) Play(e.parried ? CombatSound.Parry : e.broke ? CombatSound.GuardBreak : CombatSound.Guard,
                e.defender, e.parried || e.broke ? .9f : .6f, 75);
        }
        void OnClash(ClashEvent e) { if (!ended) Play(CombatSound.Clash, e.attacker, .65f, 70); }
        void OnSurface(SurfaceContactEvent e) { if (Live) contacts.Add(e); }
        void OnEnvironment(EnvImpactEvent e)
        {
            if (ended) return;
            lastContact[e.fighter, e.isWall ? 1 : 0] = e.time;
            Play(e.slam || e.smash ? CombatSound.Slam : e.isWall ? CombatSound.Wall : CombatSound.Fall,
                e.fighter, .9f, 85);
        }
        void OnEvade(EvadeEvent e)
        {
            if (Live) Play(e.kind == EvadeKind.HopOver || e.kind == EvadeKind.WallJump ? CombatSound.Jump : CombatSound.Evade, e.fighter, .5f, 40);
        }
        void OnRecover(RecoverEvent e) { if (Live) Play(CombatSound.Recover, e.fighter, .45f, 30); }
        void OnKo(KoEvent e) { Play(CombatSound.KO, e.winner < 0 ? -1 : 1 - e.winner, .9f, 95); }
        void OnEnd(MatchResult result)
        {
            if (ended) return;
            ended = true;
            musicPending = false;
            MusicSource.Stop();
            contacts.Clear();
            for (int i = 0; i < VoiceLimit; i++) if (voicePriority[i] < 60) voices[i].Stop();
            Play(CombatSound.EndGong, -1, 1f, 100);
        }

        AudioSource Play(CombatSound sound, int fighter, float volume, int priority)
        {
            if (!TimeController.PresentationEffectsEnabled || MasterVolume <= 0f || TimeController.Paused) return null;
            int kind = (int)sound;
            int variation = variants[kind]++;
            var clip = clips[kind][variation % clips[kind].Length];
            if (clip == null) return null;
            int slot = -1;
            for (int i = 0; i < VoiceLimit; i++)
            {
                if (!voices[i].isPlaying) { slot = i; break; }
                if (voicePriority[i] > priority) continue;
                if (slot < 0 || voicePriority[i] < voicePriority[slot]
                    || voicePriority[i] == voicePriority[slot] && voiceStarted[i] < voiceStarted[slot]) slot = i;
            }
            if (slot < 0) return null;
            var source = voices[slot];
            source.Stop();
            source.clip = clip;
            voiceGain[slot] = volume;
            source.volume = volume * MasterVolume * GameSettings.EffectsGain;
            // Keep gongs stable; all other variation comes from a local counter.
            source.pitch = fighter < 0 ? 1f : Mathf.Lerp(1.08f, .9f,
                Mathf.InverseLerp(1f, 3.5f, Fighter(fighter).Weapon.maxSide)) + (variation % 3 - 1) * .025f;
            source.panStereo = fighter < 0 ? 0f : Mathf.Clamp(Fighter(fighter).X / director.Context.ArenaHalfWidth, -1f, 1f) * .25f;
            source.priority = 128 - priority;
            voicePriority[slot] = priority;
            voiceStarted[slot] = AudioSettings.dspTime;
            source.Play();
            PlayedCount++;
            SoundPlayed?.Invoke(new CombatSoundPlayback { sound = sound, clip = clip, volume = source.volume,
                pitch = source.pitch, fighter = fighter });
            return source;
        }
    }
}
