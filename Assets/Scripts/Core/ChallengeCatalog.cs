using System;

namespace MojiBattle
{
    public enum ChallengeGoal { Victory, SpecialFinish, CheerVictory }

    public sealed class ChallengeDefinition
    {
        public readonly int Id, Difficulty, RewardId;
        public readonly string Name, Summary, Condition, Hint, PlayerGlyph, OpponentGlyph, FixedCharacter;
        public readonly WeaponSize? FixedSize;
        public readonly ChallengeGoal Goal;
        readonly WeaponSize opponentSize;
        readonly BattleStyle opponentStyle;
        public ChallengeDefinition(int id, string name, int difficulty, string player, string opponent,
            string summary, string condition, string hint, string fixedCharacter = null, WeaponSize? fixedSize = null,
            WeaponSize opponentSize = WeaponSize.M, BattleStyle opponentStyle = BattleStyle.Counter,
            ChallengeGoal goal = ChallengeGoal.Victory)
        {
            Id = id; RewardId = 51 + id; Name = name; Difficulty = difficulty;
            PlayerGlyph = player; OpponentGlyph = opponent; Summary = summary; Condition = condition; Hint = hint;
            FixedCharacter = fixedCharacter; FixedSize = fixedSize; this.opponentSize = opponentSize;
            this.opponentStyle = opponentStyle; Goal = goal;
        }
        public FighterBuildData Opponent => new FighterBuildData { character = OpponentGlyph,
            fontType = FontStyleId.Brush, weaponSize = opponentSize, gripType = GripType.TwoHanded,
            battleStyle = opponentStyle };
        public FighterBuildData DefaultPlayer => Constrain(new FighterBuildData { character = PlayerGlyph,
            gripType = GripType.TwoHanded, battleStyle = BattleStyle.Counter });
        public FighterBuildData Constrain(FighterBuildData build)
        {
            var copy = build?.Clone() ?? FighterBuildData.Default(PlayerGlyph);
            if (FixedCharacter != null && copy.character != FixedCharacter) { copy.character = FixedCharacter; copy.special = null; }
            if (FixedSize.HasValue) copy.weaponSize = FixedSize.Value;
            if (!Enum.IsDefined(typeof(FontStyleId), copy.fontType)) copy.fontType = FontStyleId.Gothic;
            if (!Enum.IsDefined(typeof(WeaponSize), copy.weaponSize)) copy.weaponSize = WeaponSize.M;
            if (!Enum.IsDefined(typeof(GripType), copy.gripType)) copy.gripType = GripType.TwoHanded;
            if (!Enum.IsDefined(typeof(BattleStyle), copy.battleStyle)) copy.battleStyle = BattleStyle.Counter;
            if (!Enum.IsDefined(typeof(WeaponFlip), copy.weaponFlip)) copy.weaponFlip = WeaponFlip.None;
            if (string.IsNullOrWhiteSpace(copy.character)) copy.character = PlayerGlyph;
            return copy;
        }
        public bool BuildAllowed(FighterBuildData build) => build != null
            && (FixedCharacter == null || build.character == FixedCharacter)
            && (!FixedSize.HasValue || build.weaponSize == FixedSize.Value);
        public bool GoalMet(AchievementMatchStats stats) => Goal == ChallengeGoal.Victory
            || (stats != null && (Goal == ChallengeGoal.SpecialFinish ? stats.specialFinish : stats.totals[5] > 0));
        public string Restriction => FixedCharacter != null ? "使用文字　「" + FixedCharacter + "」固定"
            : FixedSize.HasValue ? "サイズ　" + FixedSize.Value + "固定" : "文字・構成の制限なし";
        public string Editable => FixedCharacter != null ? "字体・サイズ・持ち方・戦い方・必殺技は変更できます。"
            : FixedSize.HasValue ? "文字・字体・持ち方・戦い方・必殺技は変更できます。" : "好きな文字と構成で挑戦できます。";
    }

    public static class ChallengeCatalog
    {
        public static readonly ChallengeDefinition[] All = {
            new ChallengeDefinition(0,"一文字の可能性",1,"一","刀","文字「一」で勝利する","文字「一」で相手に勝利する",
                "シンプルな形を活かして、握る位置や持ち方を試そう。",fixedCharacter:"一"),
            new ChallengeDefinition(1,"小よく大を制す",1,"小","大","SサイズでXLに挑む","SサイズでXLの相手に勝利する",
                "小さな武器の速さを活かして、間合いと戦い方を工夫しよう。",fixedSize:WeaponSize.S,
                opponentSize:WeaponSize.XL,opponentStyle:BattleStyle.Aggressive),
            new ChallengeDefinition(2,"鉄壁突破",2,"破","盾","守りの堅い相手を倒す","鉄壁の相手「盾」に勝利する",
                "守りを崩す必殺技や、届く位置を変える持ち方を試そう。",opponentStyle:BattleStyle.Defensive),
            new ChallengeDefinition(3,"一撃の美学",2,"撃","刀","必殺技でとどめを刺す","必殺技でとどめを刺して勝利する",
                "相手の残りHPを見て、必殺技を発動するタイミングを選ぼう。",goal:ChallengeGoal.SpecialFinish),
            new ChallengeDefinition(4,"声援の力",2,"声","闘","声援を使って勝利する","声援を使った試合で勝利する",
                "声援ゲージが満タンになったら、Lキーやボタンで力を届けよう。",opponentStyle:BattleStyle.Aggressive,
                goal:ChallengeGoal.CheerVictory),
            new ChallengeDefinition(5,"挑戦者の証",3,"挑","龍","強敵「龍」を倒す","強敵「龍」に勝利する",
                "相手の必殺技には声援で踏ん張り、自分の切り札で反撃しよう。",opponentSize:WeaponSize.L,
                opponentStyle:BattleStyle.Aggressive)
        };
        public static ChallengeDefinition Get(int id) => id >= 0 && id < All.Length ? All[id] : null;
    }
}
