using UnityEngine;

namespace MojiBattle
{
    public enum BattleStageId { Dojo, Calligraphy, EraserFactory, Dictionary, Letterpress, Exam }

    public sealed class BattleStageDefinition
    {
        public readonly BattleStageId Id;
        public readonly string Name, Description, Traits, Art;
        public readonly Color Floor;
        public BattleStageDefinition(BattleStageId id, string name, string description, string traits, string art, Color floor)
        { Id=id; Name=name; Description=description; Traits=traits; Art=art; Floor=floor; }
        public Texture2D Texture => Resources.Load<Texture2D>(Art);
    }

    public static class StageCatalog
    {
        public static readonly BattleStageDefinition[] All = {
            new BattleStageDefinition(BattleStageId.Dojo,"基本の道場","仕掛けなし。文字と腕で、真っ向勝負。","基本ルール","UI/BattlePaper",new Color32(210,198,172,255)),
            new BattleStageDefinition(BattleStageId.Calligraphy,"傾く書道机","机が傾く。墨だまりは滑る。","傾斜 ／ 滑る床","UI/Stages/calligraphy",new Color32(139,96,57,255)),
            new BattleStageDefinition(BattleStageId.EraserFactory,"消しゴム工場","流れる床と、落ちてくる消しゴム。","ベルト ／ 落下予告","UI/Stages/eraser",new Color32(87,83,73,255)),
            new BattleStageDefinition(BattleStageId.Dictionary,"辞書の谷","ページがめくれ、二人を空中へ。","ページめくり ／ 空中戦","UI/Stages/dictionary",new Color32(229,213,178,255)),
            new BattleStageDefinition(BattleStageId.Letterpress,"活版印刷所","落ちる活字が、戦場の形を変える。","活字落下 ／ 障害物","UI/Stages/letterpress",new Color32(110,104,89,255)),
            new BattleStageDefinition(BattleStageId.Exam,"赤ペン先生の答案用紙","○で回復。×には赤ペンの一撃。","○ 回復 ／ × 斬撃","UI/Stages/exam",new Color32(238,227,203,255))
        };
        public static BattleStageDefinition Get(BattleStageId id) => All[Mathf.Clamp((int)id,0,All.Length-1)];
        public static BattleStageId ForSeed(int seed, bool final=false) => final ? BattleStageId.Letterpress : (BattleStageId)((uint)seed % (uint)All.Length);
    }

    public static class StageSession
    {
        public const string PrefKey="MojiBattle.Stage";
        public static BattleStageId Selected
        {
            get => (BattleStageId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey,0),0,StageCatalog.All.Length-1);
            set { PlayerPrefs.SetInt(PrefKey,Mathf.Clamp((int)value,0,StageCatalog.All.Length-1)); PlayerPrefs.Save(); }
        }
        public static bool OpenOnCustomize;
    }
}
