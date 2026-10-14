using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    public sealed partial class CustomizeScreen
    {
        static string DraftSummary(FighterBuildData b) => $"{b.character} / {GlyphCatalog.FontDisplayName(b.fontType)} / {b.weaponSize}\n{CustomizeLabels.Grip(b)} / {CustomizeLabels.StyleShort(b.battleStyle)}";
        void BuildDraftVs(Transform root)
        {
            var bg=UguiKit.Fill(root,"VsPanel");vsPanel=bg.gameObject;
            bg.gameObject.AddComponent<Image>().color=RetroUi.Paper;
            RetroUi.PaperSurface(bg,0,0,1920,1080);RetroUi.Frame(bg,24,24,1872,1032,RetroUi.Ink,3);RetroUi.Frame(bg,39,39,1842,1002,RetroUi.Ink,1);
            foreach(float x in new[]{24f,1872f})foreach(float y in new[]{24f,1032f})
            {
                RetroUi.Block(bg,"Corner",x-8,y+3,16,16,RetroUi.Ink);RetroUi.Block(bg,"Corner",x+8,y-13,16,16,RetroUi.Ink);
                RetroUi.Block(bg,"Corner",x+8,y+19,16,16,RetroUi.Ink);RetroUi.Block(bg,"Corner",x+24,y+3,16,16,RetroUi.Ink);
            }
            RetroUi.Group(bg,"DraftVersusInkRing",680,244,560,470).gameObject.AddComponent<DraftInkRing>();
            DraftUi.Header(bg,"DRAFT / VS",HideVs,"← 構成調整");
            bg.Find("Back").name="DraftVsBack";
            DraftUi.Score(bg,578,147,764);
            var l=customizer.Get(0);var r=customizer.Get(1);
            RetroUi.Fighter(bg,100,244,1.05f,l.character,RetroUi.Red);
            RetroUi.Fighter(bg,1380,244,1.05f,r.character,RetroUi.Blue,true);
            // The actual weapon glyphs, using the selected font, occupy the large art area.
            vsLeftGlyph=bg.Find("GlyphFighter/GlyphWeapon").GetComponent<Text>();
            var fighters=bg.GetComponentsInChildren<RetroFighterArt>();
            foreach(var art in fighters)if(art.mirrored)vsRightGlyph=art.transform.parent.Find("GlyphWeapon").GetComponent<Text>();
            DraftUi.Title(bg,"DraftVersus","VS",715,310,490,300,240,RetroUi.Ink);
            DraftUi.Pips(bg,350,713,DraftRuntimeStore.Current.playerWins,RetroUi.Red);
            DraftUi.Pips(bg,1480,713,DraftRuntimeStore.Current.comWins,RetroUi.Blue);
            RetroUi.Block(bg,"VsYouPlate",190,777,440,82,RetroUi.Red);RetroUi.Label(bg,"VsYou","あなた",190,777,440,82,51,RetroUi.Paper,TextAnchor.MiddleCenter);
            RetroUi.Block(bg,"VsComPlate",1320,777,440,82,RetroUi.Blue);RetroUi.Label(bg,"VsCom","COM",1320,777,440,82,51,RetroUi.Paper,TextAnchor.MiddleCenter);
            DraftUi.Panel(bg,"VsYouSummary",190,875,440,116);DraftUi.Panel(bg,"VsComSummary",1320,875,440,116);
            vsLeft=RetroUi.Label(bg,"VsLeft","",200,883,420,99,31,null,TextAnchor.MiddleCenter);
            vsRight=RetroUi.Label(bg,"VsRight","",1330,883,420,99,31,null,TextAnchor.MiddleCenter);
            RetroUi.Label(bg,"VsCaption","選んだ一文字で、勝負。",640,796,640,60,36,null,TextAnchor.MiddleCenter);
            RetroUi.Button(bg,"Start","対戦開始 →",680,875,560,106,52,StartBattle,RetroUi.Red);
            PlayModePicker.Build(bg,660,726);
            vsPanel.SetActive(false);
        }
    }
}
