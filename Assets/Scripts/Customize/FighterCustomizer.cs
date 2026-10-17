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
        public bool CanUseFusion => Editable && !draft && !(run && !string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph))
            && !(challenge && ChallengeRuntimeStore.Current.FixedCharacter != null);
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
            {
                b.EnsureIndependentGrips();
                if (!IsAvailable(b, b.fontType, out _)) { b.fusion = null; b.fusionEnabled = false; b.character = "一"; b.splitMing = false; }
                if (!string.IsNullOrEmpty(b.offhandCharacter) && !IsAvailable(b.offhandCharacter, b.fontType, out _)) b.offhandCharacter = "";
                if (b.gripType == GripType.DualWield && !FighterBuildData.DualWieldAllowed(b.weaponSize)) b.gripType = GripType.OneHanded;
            }
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
        public static bool IsAvailable(FighterBuildData build, FontStyleId font, out string reason) => build.fusionEnabled
            ? FusionGlyphs.TryGet(build.fusion, font, CombatBalance.Default, GlyphCalibration.Default, -1, out _, out reason)
            : IsAvailable(build.character, font, out reason);
        public bool SetFusion(FusionData recipe, FontStyleId font)
        {
            if (!CanUseFusion || recipe == null) return false;
            if (!FusionGlyphs.TryGet(recipe, font, CombatBalance.Default, GlyphCalibration.Default, -1, out _, out var reason))
            { CharacterError = reason; Changed?.Invoke(); return false; }
            Current.fusion = recipe.Clone(); Current.character = recipe.Character; Current.fontType = font;
            Current.fusionEnabled = true;
            Current.splitMing = Current.splitSwapped = false; Current.special = null;
            var moves = SpecialMoveLibrary.For(Current); Current.special = moves.Count > 0 ? moves[0].Clone() : null;
            CharacterError = null; Commit(); return true;
        }

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
            if (Current.fusionEnabled) { Current.fusion = null; Current.fusionEnabled = false; Current.splitMing = Current.splitSwapped = false; }
            if (Current.character != ch.Trim()) { Current.splitMing = false; Current.splitSwapped = false; }
            Current.character = ch.Trim();
            // 必殺技は文字ごと。別の文字に変えたら、その文字で最初に登録した技を装備する（無ければ標準）
            if (Current.special != null && !Current.special.SameWeapon(Current.character, null)) Current.special = null;
            if (Current.special == null)
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
            if (!IsAvailable(Current, font, out var reason)) { CharacterError = reason; Changed?.Invoke(); return; }
            if (Current.IsDualWield && !Current.IsSplitMing)
            {
                bool ok = Current.OffhandFusion != null
                    ? FusionGlyphs.TryGet(Current.OffhandFusion, font, CombatBalance.Default, GlyphCalibration.Default, -1, out _, out reason)
                    : IsAvailable(Current.OffhandGlyph, font, out reason);
                if (!ok) { CharacterError = "左手の文字: " + reason; Changed?.Invoke(); return; }
            }
            if (Current.IsSplitMing)
            {
                var probe = Current.Clone(); probe.fontType = font;
                if (!SplitMingGlyphs.TryGet(probe, CombatBalance.Default, GlyphCalibration.Default, false, out _, out reason))
                { CharacterError = reason; Changed?.Invoke(); return; }
            }
            CharacterError = null;
            Current.fontType = font;
            Commit();
        }

        /// <summary>二刀流を L・XL に変えて外した時の知らせ（次の変更で消える）。</summary>
        public string GripNotice { get; private set; }

        public void SetSize(WeaponSize s)
        {
            if (!Editable) return;
            Current.weaponSize = challenge && ChallengeRuntimeStore.Current.FixedSize.HasValue
                ? ChallengeRuntimeStore.Current.FixedSize.Value : worldCup ? WeaponSize.M : tournament ? WeaponSize.S : s;
            GripNotice = null;
            // 二刀流は S・M のみ。大きいサイズにしたら片手に戻す
            if (Current.gripType == GripType.DualWield && !FighterBuildData.DualWieldAllowed(Current.weaponSize))
            { Current.gripType = GripType.OneHanded; GripNotice = "二刀流は S・M サイズのみ。片手に戻しました。"; }
            Commit();
        }
        public void SetGrip(GripType g)
        {
            if (!Editable) return;
            if (g == GripType.DualWield && !FighterBuildData.DualWieldAllowed(Current.weaponSize)) return;
            GripNotice = null;
            Current.EnsureIndependentGrips();
            Current.gripType = g;
            Commit();
        }

        /// <summary>二刀流の副武器（反対の手）の文字。ベイク済みでない文字は理由を残して変更しない。</summary>
        public bool SetOffhandCharacter(string ch)
        {
            if (!Editable || Current.IsSplitMing) return false;
            if (string.IsNullOrWhiteSpace(ch)) { CharacterError = null; Current.offhandCharacter = ""; Current.offhandFusion = null; Current.offhandFusionEnabled = false; Commit(); return true; }
            if (!IsAvailable(ch, Current.fontType, out var reason)) { CharacterError = "左手の文字: " + reason; Changed?.Invoke(); return false; }
            CharacterError = null;
            Current.offhandCharacter = ch.Trim();
            Current.offhandFusion = null;
            Current.offhandFusionEnabled = false;
            Commit();
            return true;
        }
        public void SetGripPosition(float p) { if (!Editable) return; Current.EnsureIndependentGrips(); Current.gripPosition = Mathf.Clamp01(p); Current.customGrip = false; Commit(); }

        /// <summary>プレビューで選んだ場所を握る（外接矩形内の正規化座標）。</summary>
        public void SetGripPoint(Vector2 normalized)
        {
            if (!Editable) return;
            Current.EnsureIndependentGrips();
            Current.gripPoint = new Vector2(Mathf.Clamp01(normalized.x), Mathf.Clamp01(normalized.y));
            Current.customGrip = true;
            Commit();
        }

        /// <summary>握る場所を中央（長軸の中央）に戻す。</summary>
        public void ResetGrip() => SetGripPosition(0.5f);
        public void SetOffhandGripPoint(Vector2 point)
        {
            if (!Editable || !Current.IsDualWield) return;
            Current.EnsureIndependentGrips();
            Current.offhandGripPoint = new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
            Current.offhandCustomGrip = true;
            Commit();
        }
        public void ResetOffhandGrip()
        {
            if (!Editable || !Current.IsDualWield) return;
            Current.EnsureIndependentGrips(); Current.offhandCustomGrip = false; Current.offhandGripPosition = .5f; Commit();
        }
        public bool SetSplitMing(bool enabled)
        {
            if (!Editable || (!Current.HasFusion && !SplitKanjiCatalog.Supports(Current.character)) || !FighterBuildData.DualWieldAllowed(Current.weaponSize)) return false;
            var probe = Current.Clone(); probe.gripType = GripType.DualWield; probe.splitMing = enabled;
            if (enabled && !SplitMingGlyphs.TryGet(probe, CombatBalance.Default, GlyphCalibration.Default, false, out _, out var reason))
            { CharacterError = reason; Changed?.Invoke(); return false; }
            Current.EnsureIndependentGrips();
            if (enabled) Current.gripType = GripType.DualWield;
            Current.splitMing = enabled;
            CharacterError = null; GripNotice = enabled ? $"「{Current.character}」を{Current.MainWeaponLabel}・{Current.OffhandWeaponLabel}に分離。左右の握りを別々に設定できます。" : null;
            Commit(); return true;
        }
        public void SwapHands()
        {
            if (!Editable || !Current.IsDualWield) return;
            if (!Current.IsSplitMing && (draft || (run && !string.IsNullOrEmpty(RunRuntimeStore.Current.lockedGlyph))
                || (challenge && ChallengeRuntimeStore.Current.FixedCharacter != null))) return;
            Current.EnsureIndependentGrips();
            if (Current.IsSplitMing) Current.splitSwapped = !Current.splitSwapped;
            else
            {
                var previousFusion = Current.HasFusion ? Current.fusion.Clone() : null; var nextFusion = Current.OffhandFusion?.Clone();
                string main = Current.character; Current.character = Current.OffhandGlyph; Current.offhandCharacter = main;
                Current.fusion = nextFusion; Current.offhandFusion = previousFusion;
                Current.fusionEnabled = nextFusion != null; Current.offhandFusionEnabled = previousFusion != null;
                if (Current.EquippedSpecial == null || !Current.special.SameWeapon(Current.character, Current.HasFusion ? Current.fusion : null))
                { var moves = SpecialMoveLibrary.For(Current); Current.special = moves.Count > 0 ? moves[0].Clone() : null; }
            }
            (Current.customGrip, Current.offhandCustomGrip) = (Current.offhandCustomGrip, Current.customGrip);
            (Current.gripPoint, Current.offhandGripPoint) = (Current.offhandGripPoint, Current.gripPoint);
            (Current.gripPosition, Current.offhandGripPosition) = (Current.offhandGripPosition, Current.gripPosition);
            CharacterError = null; Commit();
        }
        public void SetStyle(BattleStyle s) { if (!Editable) return; Current.battleStyle = s; Commit(); }
        public void SetFlip(WeaponFlip f) { if (!Editable) return; Current.weaponFlip = f; Commit(); }
        /// <summary>必殺技を装備する（null で標準の必殺技に戻す）。</summary>
        public void SetSpecial(SpecialMoveData move) { if (!Editable) return; Current.special = SpecialMoveRules.Sanitize(move, Current.character, Current.HasFusion ? Current.fusion : null); Commit(); }

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
