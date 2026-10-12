using System;
using System.Collections.Generic;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>
    /// UI からの変更を BuildData へ反映する（カスタマイズ仕様 15）。左右 2 体の設定を持ち、変更のたびに保存して Changed を通知する。
    /// </summary>
    public sealed class FighterCustomizer
    {
        public const string GlyphOrder = "一口山火鬱AIOX";

        readonly FighterBuildData[] builds = new FighterBuildData[2];
        readonly bool tournament;
        readonly bool friend;
        readonly bool challenge;
        readonly bool draft;
        readonly bool run;
        readonly bool worldCup;
        public int Side { get; private set; }
        public FighterBuildData Current => builds[Side];
        public bool Editable => worldCup ? Side==0 && WorldCupRuntimeStore.Preparing : run ? Side == 0 && RunRuntimeStore.Current?.stage == RunStage.Build : !draft || (Side == 0 && DraftRuntimeStore.Current?.stage == DraftStage.Build);
        public FighterBuildData Get(int side) => builds[side];
        /// <summary>最後に入力された文字が使えなかった理由（使えれば null）。</summary>
        public string CharacterError { get; private set; }
        public event Action Changed;

        public FighterCustomizer(bool tournament = false)
        {
            this.tournament = tournament;
            challenge = GameFlow.ChallengeMode;
            draft = GameFlow.DraftMode;
            run = GameFlow.RunMode;
            worldCup=GameFlow.WorldCupMode;
            friend = GameFlow.FriendMode && FriendRoomSession.Instance?.Draft != null;
            builds[0] = tournament ? TournamentRuntimeStore.Draft : BuildRuntimeStore.Get(0).Clone();
            if (friend) builds[0] = FriendRoomSession.Instance.Draft.Clone();
            if (challenge) builds[0] = ChallengeRuntimeStore.Draft;
            builds[1] = BuildRuntimeStore.Get(1).Clone();
            if (challenge) builds[1] = ChallengeRuntimeStore.Current.Opponent;
            if (draft) { builds[0] = DraftRuntimeStore.Current.player.Clone(); builds[1] = DraftRuntimeStore.Current.com.Clone(); }
            if(run) { builds[0]=RunRuntimeStore.Current.player.Clone(); builds[1]=RunRuntimeStore.Current.Opponent.Build.Clone(); }
            if(worldCup)builds[0]=WorldCupRuntimeStore.Preparing?WorldCupRuntimeStore.Draft:WorldCupRuntimeStore.Current.Entrants[0].Build;
            foreach (var b in builds)
                if (!IsAvailable(b.character, b.fontType, out _)) b.character = "一";
            if(challenge)ChallengeRuntimeStore.SetDraft(builds[0]);
        }

        public static List<string> AvailableCharacters(FontStyleId font)
        {
            var list = GlyphCatalog.AvailableGraphemes(font);
            list.Sort((a, b) => GlyphOrder.IndexOf(a, StringComparison.Ordinal).CompareTo(GlyphOrder.IndexOf(b, StringComparison.Ordinal)));
            return list;
        }

        public static bool IsAvailable(string ch, FontStyleId font, out string reason) =>
            GlyphCatalog.TryGet(ch, font, CombatBalance.Default, GlyphCalibration.Default, out _, out reason);

        public void SelectSide(int side)
        {
            Side = worldCup || tournament || friend || challenge ? 0 : Mathf.Clamp(side, 0, 1);
            CharacterError = null;
            Changed?.Invoke();
        }

        /// <summary>文字を入力。ベイク済みでない文字は理由を残して変更しない。</summary>
        public bool SetCharacter(string ch)
        {
            if (!Editable) return false;
            if(run && !string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph) && ch?.Trim()!=RunRuntimeStore.Current.lockedGlyph)
            { CharacterError="挑戦中の文字は変更できません。"; Changed?.Invoke(); return false; }
            if (draft && ch?.Trim() != DraftRuntimeStore.Current.player.character)
            { CharacterError = "ドラフトで選んだ文字は変更できません。"; Changed?.Invoke(); return false; }
            if (challenge && ChallengeRuntimeStore.Current.FixedCharacter != null
                && ch?.Trim() != ChallengeRuntimeStore.Current.FixedCharacter)
            { CharacterError = "このお題では文字「" + ChallengeRuntimeStore.Current.FixedCharacter + "」を使います。"; Changed?.Invoke(); return false; }
            if (!IsAvailable(ch, Current.fontType, out var reason))
            {
                CharacterError = reason;
                Changed?.Invoke();
                return false;
            }
            CharacterError = null;
            Current.character = ch.Trim();
            // 必殺技は文字ごと。別の文字に変えたら、その文字で最初に登録した技を装備する（無ければ標準）
            if (Current.special?.character != Current.character)
            {
                var moves = SpecialMoveLibrary.For(Current.character);
                Current.special = moves.Count > 0 ? moves[0].Clone() : null;
            }
            Commit();
            return true;
        }

        public void SetFont(FontStyleId font)
        {
            if (!Editable) return;
            if (!IsAvailable(Current.character, font, out var reason)) { CharacterError = reason; Changed?.Invoke(); return; }
            CharacterError = null;
            Current.fontType = font;
            Commit();
        }

        public void SetSize(WeaponSize s) { if (!Editable) return; Current.weaponSize = challenge && ChallengeRuntimeStore.Current.FixedSize.HasValue
            ? ChallengeRuntimeStore.Current.FixedSize.Value : worldCup ? WeaponSize.M : tournament ? WeaponSize.S : s; Commit(); }
        public void SetGrip(GripType g) { if (!Editable) return; Current.gripType = g; Commit(); }
        public void SetGripPosition(float p) { if (!Editable) return; Current.gripPosition = Mathf.Clamp01(p); Current.customGrip = false; Commit(); }

        /// <summary>プレビューで選んだ場所を握る（外接矩形内の正規化座標）。</summary>
        public void SetGripPoint(Vector2 normalized)
        {
            if (!Editable) return;
            Current.gripPoint = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
            Current.customGrip = true;
            Commit();
        }

        /// <summary>握る場所を中央（長軸の中央）に戻す。</summary>
        public void ResetGrip() => SetGripPosition(0.5f);
        public void SetStyle(BattleStyle s) { if (!Editable) return; Current.battleStyle = s; Commit(); }
        public void SetFlip(WeaponFlip f) { if (!Editable) return; Current.weaponFlip = f; Commit(); }
        /// <summary>必殺技を装備する（null で標準の必殺技に戻す）。</summary>
        public void SetSpecial(SpecialMoveData move) { if (!Editable) return; Current.special = move?.character == Current.character ? move.Clone() : null; Commit(); }

        void Commit()
        {
            if(worldCup) WorldCupRuntimeStore.SetDraft(Current);
            else if(run) RunRuntimeStore.SetBuild(Current);
            else if (draft) DraftRuntimeStore.SetBuild(Current);
            else if (challenge) ChallengeRuntimeStore.SetDraft(Current);
            else if (friend) FriendRoomSession.Instance.SaveDraft(Current);
            else if (tournament) TournamentRuntimeStore.SetDraft(Current);
            else BuildRuntimeStore.Set(Side, Current);
            AchievementStore.Customized();
            Changed?.Invoke();
        }
    }
}
