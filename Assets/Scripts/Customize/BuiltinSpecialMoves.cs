using System;
using UnityEngine;

namespace MojiBattle
{
    /// <summary>通信や保存を必要としない初期必殺。意味が近い文字を割り当て、その他の文字も固定の技を持つ。</summary>
    public static class BuiltinSpecialMoves
    {
        sealed class Preset
        {
            public string characters, suffix, reading, element, effect, callout, description;
            public int power, speed, reach;
            public AttackStyle[] steps;
        }

        static Preset P(string chars, string suffix, string reading, string element, string effect,
            int power, int speed, int reach, string callout, string description, params AttackStyle[] steps) =>
            new Preset { characters=chars, suffix=suffix, reading=reading, element=element, effect=effect,
                power=power, speed=speed, reach=reach, callout=callout, description=description, steps=steps };

        static readonly Preset[] Presets =
        {
            P("火炎燃灼熱", "炎連斬", "かえんれんざん", "fire", "pillar", 4,3,2, "燃え上がれ！", "踏み込みから振り下ろす、炎の二連斬。", AttackStyle.DashSlash, AttackStyle.Overhead),
            P("水海河湖泉流", "流穿ち", "すいりゅううがち", "water", "rain", 2,3,4, "流れを貫け！", "一画を飛ばし、流れるように突きを重ねる。", AttackStyle.StrokeShot, AttackStyle.Thrust),
            P("雷電鳴轟", "迅三連", "らいじんさんれん", "thunder", "slash", 2,5,2, "閃け！", "踏み込み、突き、斬り上げを素早くつなぐ。", AttackStyle.DashSlash, AttackStyle.Thrust, AttackStyle.Rising),
            P("風嵐旋吹空", "旋風舞", "ふうせんぷうまい", "wind", "spiral", 2,4,3, "巻き起これ！", "回転斬りから全身で旋風を巻き起こす。", AttackStyle.Spin, AttackStyle.Whirl),
            P("氷雪冷凍霜", "凍牙突", "ひょうとうがとつ", "ice", "pillar", 4,2,3, "凍てつけ！", "足元を払ってから氷の牙を突き立てる。", AttackStyle.LowSweep, AttackStyle.Thrust),
            P("土地岩石山岳", "裂震撃", "どれっしんげき", "earth", "ring", 5,1,3, "大地よ響け！", "体当たりから大地を揺らす衝撃波へ。", AttackStyle.Bash, AttackStyle.GroundWave),
            P("木林森枝樹葉", "葉乱舞", "もくようらんぶ", "wind", "rain", 3,4,2, "枝葉よ舞え！", "斬り上げ、横薙ぎ、回転斬りで乱れ舞う。", AttackStyle.Rising, AttackStyle.Sweep, AttackStyle.Spin),
            P("光輝耀明晶", "輝閃撃", "こうきせんげき", "light", "slash", 3,4,2, "輝け！", "光を纏って踏み込み、斬り上げる。", AttackStyle.DashSlash, AttackStyle.Rising),
            P("闇暗影夜黒", "影縛斬", "あんえいばくざん", "dark", "spiral", 4,3,2, "影に沈め！", "低く払い、闇を纏う斬撃で打ち上げる。", AttackStyle.LowSweep, AttackStyle.Launch),
            P("花桜梅桃華", "吹雪", "はなふぶき", "flower", "rain", 2,4,3, "咲き誇れ！", "一画の花びらから回転斬りへつなぐ。", AttackStyle.StrokeShot, AttackStyle.Spin),
            P("毒蛇蠍菌", "牙襲", "どくがしゅう", "poison", "burst", 3,4,2, "牙を受けろ！", "突きから低い払いへ、毒色の飛沫を散らす。", AttackStyle.Thrust, AttackStyle.LowSweep),
            P("龍竜昇天雲", "昇撃", "りゅうしょうげき", "light", "pillar", 4,2,3, "天まで昇れ！", "打ち上げてから跳び斬りで追撃する。", AttackStyle.Launch, AttackStyle.Leap),
            P("虎獅牙獣猫", "猛爪", "こもうそう", "earth", "slash", 5,2,2, "食らいつけ！", "力強い横薙ぎから重い振り下ろしへ。", AttackStyle.Sweep, AttackStyle.Overhead),
            P("狼犬狐走迅", "疾走牙", "ろうしっそうが", "wind", "slash", 2,5,2, "追いつめろ！", "踏み込み斬りから蹴りで追い込む。", AttackStyle.DashSlash, AttackStyle.FrontKick),
            P("鳥鷹隼翼飛羽", "飛翔斬", "ちょうひしょうざん", "wind", "spiral", 2,4,3, "飛び立て！", "跳び斬りから斬り上げへ、翼のように舞う。", AttackStyle.Leap, AttackStyle.Rising),
            P("月夢幻兎", "輪斬", "げつりんざん", "ice", "ring", 3,3,3, "月よ照らせ！", "横薙ぎと回転斬りで月輪を描く。", AttackStyle.Sweep, AttackStyle.Spin),
            P("日陽晴暁朝", "陽炎撃", "にちようえんげき", "fire", "burst", 4,2,3, "照らし尽くせ！", "振り下ろしから一画を放ち、陽炎を散らす。", AttackStyle.Overhead, AttackStyle.StrokeShot),
            P("星宙宇銀", "雨陣", "せいうじん", "light", "rain", 2,3,4, "星よ降れ！", "二つの一画と跳び斬りが星の雨を描く。", AttackStyle.StrokeShot, AttackStyle.StrokeShot, AttackStyle.Leap),
            P("金鋼鉄銅硬", "鋼砕打", "きんこうさいだ", "earth", "burst", 5,2,2, "打ち砕け！", "二度の体当たりから振り下ろす重い三連撃。", AttackStyle.Bash, AttackStyle.Bash, AttackStyle.Overhead),
            P("剣刀刃斬武", "舞陣", "けんぶじん", "none", "slash", 3,4,2, "刃よ踊れ！", "踏み込み、横薙ぎ、斬り上げ、振り下ろしの四連斬。", AttackStyle.DashSlash, AttackStyle.Sweep, AttackStyle.Rising, AttackStyle.Overhead),
            P("拳腕力打剛", "剛撃", "けんごうげき", "earth", "burst", 5,3,1, "押し切れ！", "体当たり、前蹴り、振り下ろしで力を叩き込む。", AttackStyle.Bash, AttackStyle.FrontKick, AttackStyle.Overhead),
            P("脚足蹴跳歩", "烈舞", "きゃくれつぶ", "fire", "spiral", 3,5,1, "蹴り抜け！", "前蹴り、足払い、前蹴りを連ねる。", AttackStyle.FrontKick, AttackStyle.LowSweep, AttackStyle.FrontKick),
            P("心魂愛命勇", "魂烈斬", "しんこんれつざん", "light", "burst", 4,3,2, "魂を込めろ！", "斬り上げから渾身の振り下ろしへ。", AttackStyle.Rising, AttackStyle.Overhead),
            P("音歌声響奏", "響波撃", "おんきょうはげき", "water", "ring", 2,3,4, "響き渡れ！", "一画と衝撃波が重なる波紋を広げる。", AttackStyle.StrokeShot, AttackStyle.GroundWave),
            P("墨筆書文字画", "流舞", "ぼくりゅうぶ", "none", "rain", 3,3,3, "描き切れ！", "一画、横薙ぎ、回転斬りで墨の軌跡を描く。", AttackStyle.StrokeShot, AttackStyle.Sweep, AttackStyle.Spin),
            P("王皇帝将覇冠", "覇撃", "おうはげき", "light", "pillar", 5,1,3, "ひれ伏せ！", "全身ぶん回しから重い振り下ろしを放つ。", AttackStyle.Whirl, AttackStyle.Overhead),
            P("零無虚穴口", "虚空突", "れいこくうとつ", "dark", "ring", 2,4,3, "虚空を穿て！", "二段の突きで静かな空間を貫く。", AttackStyle.Thrust, AttackStyle.Thrust),
            P("砂塵黄乾沙", "塵旋", "さじんせん", "earth", "spiral", 3,2,4, "砂塵よ舞え！", "足払いから衝撃波、全身ぶん回しへ。", AttackStyle.LowSweep, AttackStyle.GroundWave, AttackStyle.Whirl),
            P("雨霧露滴湿", "霧穿", "うむせん", "water", "rain", 2,4,3, "降り注げ！", "一画から二段の突きを霧雨のように重ねる。", AttackStyle.StrokeShot, AttackStyle.Thrust, AttackStyle.Thrust),
            P("鬱怒恨魔鬼憂", "怨旋陣", "うつおんせんじん", "dark", "spiral", 4,2,3, "解き放て！", "全身ぶん回しから打ち上げ、回転斬りへ。", AttackStyle.Whirl, AttackStyle.Launch, AttackStyle.Spin),
        };

        public static int Count => Presets.Length;
        public static string Representative(int index) => Presets[index].characters.Substring(0,1);

        public static SpecialMoveData For(FighterBuildData build)
        {
            var move = For(build?.character);
            if (build != null && build.HasFusion)
            {
                move.name = "創字" + move.name.Substring(build.character.Length); move.reading = "";
                move.fusionEnabled = true; move.fusion = build.fusion.Clone();
            }
            return move;
        }

        public static SpecialMoveData For(string character)
        {
            if (string.IsNullOrEmpty(character)) character="一";
            if (character=="一") return new SpecialMoveData { character=character, name="一閃", reading="いっせん",
                steps=new[]{"DashSlash"}, power=3, speed=4, reach=2, element="light", effect="slash",
                callout="一撃に込める！", description="光を纏う、一撃の踏み込み斬り。" };
            int index=-1;
            for (int i=0;i<Presets.Length;i++)
                if (SpecialMoveRules.Graphemes(Presets[i].characters).Contains(character)) { index=i; break; }
            if (index<0)
            {
                // .NET の文字列ハッシュは使用せず、端末・保存・友人対戦をまたいで割り当てを固定する。
                uint hash=2166136261;
                foreach (char c in character) hash=unchecked((hash ^ c)*16777619);
                index=(int)(hash % (uint)Presets.Length);
            }
            var p=Presets[index];
            return new SpecialMoveData { character=character, name=character+p.suffix, reading=character==Representative(index) ? p.reading : "",
                steps=Array.ConvertAll(p.steps,s=>s.ToString()), power=p.power, speed=p.speed, reach=p.reach,
                element=p.element, effect=p.effect, callout=p.callout, description=p.description };
        }
    }
}
