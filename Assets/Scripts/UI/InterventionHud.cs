using UnityEngine;
using UnityEngine.UI;

namespace MojiBattle
{
    /// <summary>
    /// 両陣営の必殺技・声援の状態を表示し、自分の陣営には操作ボタンも出す。
    /// キー: K = 必殺技 / L = 声援。必殺技の名前と掛け声は、装備した技（AI の技なら技の色）で告知する。
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class InterventionHud : MonoBehaviour
    {
        const float LocalWidth = 560, LocalHeight = 156, GaugeWidth = 330, BannerSeconds = 2.2f;

        MatchDirector director;
        CameraRig rig;
        BattleHudPresenter presenter;
        BattleIntervention iv;
        int localSide = -1;
        bool SelfControl => director != null && director.Context.PlayerSide >= 0;
        string CheerKey => SelfControl ? "D" : "L";
        readonly RectTransform[] gaugeFill = new RectTransform[2];
        readonly RectTransform[] cheerFill = new RectTransform[2];
        readonly Text[] gaugeState = new Text[2], cheerTag = new Text[2];
        readonly Text[] cheerState = new Text[2];
        UguiKit.ChoiceButton special, cheer;
        RectTransform banner;
        Image bannerFill;
        Text missText, bannerText, bannerCallout;
        float missLife, bannerLife;

        public int LocalSide => localSide;

        public void Bind(MatchDirector d, CameraRig cameraRig, BattleHudPresenter hud)
        {
            director = d; rig = cameraRig; presenter = hud; iv = d.Context.Interventions;
            for (int i = 0; i < 2; i++) if (iv.Side(i).controller == InterventionController.Local) localSide = i;
            var arena = hud.Arena;
            for (int i = 0; i < 2; i++) BuildSide(arena, i);
            BuildBanner(arena);
            gameObject.AddComponent<SpecialMoveVfx>().Bind(d, cameraRig);
            gameObject.AddComponent<SpecialCutIn>().Bind(d, cameraRig, (RectTransform)arena.parent);
            missText = RetroUi.Label(arena, "SpecialMiss", "", 0, 0, 220, 40, 26, RetroUi.Muted, TextAnchor.MiddleCenter);
            missText.gameObject.SetActive(false);
            d.Events.Intervention += OnIntervention;
        }

        void BuildSide(RectTransform arena, int side)
        {
            bool local = side == localSide;
            var team = RetroUi.Team(side);
            float w = LocalWidth, h = local ? LocalHeight : 84;
            float x = side == 0 ? 16 : BattleHudPresenter.ArenaWidth - 16 - w;
            var panel = RetroUi.Group(arena, "Intervention" + side, x, 12, w, h);
            RetroUi.Block(panel, "Paper", 0, 0, w, h, new Color(RetroUi.Paper.r, RetroUi.Paper.g, RetroUi.Paper.b, .92f));
            RetroUi.Frame(panel, 0, 0, w, h, local ? RetroUi.Ink : RetroUi.Rule, local ? 2 : 1);
            RetroUi.Label(panel, "GaugeCaption", "必殺", 12, 7, 80, 32, 24, team);
            RetroUi.Frame(panel, 92, 12, GaugeWidth + 4, 24, RetroUi.Ink, 2);
            RetroUi.Block(panel, "GaugeWash", 94, 14, GaugeWidth, 20, RetroUi.Wash);
            gaugeFill[side] = RetroUi.Block(panel, "GaugeFill", 94, 14, 0, 20, team).rectTransform;
            string who = iv.Side(side).controller == InterventionController.Com ? "COM" : local ? "" : "相手";
            gaugeState[side] = RetroUi.Label(panel, "GaugeState", who, 436, 6, w - 446, 34, 22, null, TextAnchor.MiddleRight);
            RetroUi.Label(panel, "CheerGaugeCaption", "声援", 12, 45, 80, 32, 24, team);
            RetroUi.Frame(panel, 92, 50, GaugeWidth + 4, 24, RetroUi.Ink, 2);
            RetroUi.Block(panel, "CheerGaugeWash", 94, 52, GaugeWidth, 20, RetroUi.Wash);
            cheerFill[side] = RetroUi.Block(panel, "CheerGaugeFill", 94, 52, GaugeWidth, 20, team).rectTransform;
            cheerState[side] = RetroUi.Label(panel, "CheerGaugeState", "使用可", 436, 44, w - 446, 34, 20, null, TextAnchor.MiddleRight);
            cheerTag[side] = RetroUi.Label(arena, "CheerTag" + side, "声援中！", 0, 0, 180, 36, 26, team, TextAnchor.MiddleCenter);
            cheerTag[side].gameObject.SetActive(false);
            if (!local) return;
            special = RetroUi.Button(panel, "SpecialButton", "", 12, 88, 360, 58, 24, () => iv.Press(side, InterventionKind.Special), shadow: false);
            cheer = RetroUi.Button(panel, "CheerButton", SelfControl ? "声援  D" : "声援  L", 382, 88, 166, 58, 24, () => iv.Press(side, InterventionKind.Cheer), shadow: false);
        }

        /// <summary>必殺の告知。戦っている所を隠さないよう、アリーナ上部の中央に短く出す。</summary>
        void BuildBanner(RectTransform arena)
        {
            float w = 560, x = (BattleHudPresenter.ArenaWidth - w) / 2;
            banner = RetroUi.Group(arena, "SpecialBanner", x, 14, w, 120);
            bannerFill = RetroUi.Block(banner, "BannerFill", 0, 0, w, 120, RetroUi.Red);
            RetroUi.Frame(banner, 0, 0, w, 120, RetroUi.Ink, 3);
            bannerText = RetroUi.Label(banner, "SpecialBannerText", "", 8, 4, w - 16, 76, 50, RetroUi.Paper, TextAnchor.MiddleCenter);
            bannerText.resizeTextForBestFit = true; bannerText.resizeTextMinSize = 26; bannerText.resizeTextMaxSize = 50;
            bannerCallout = RetroUi.Label(banner, "SpecialBannerCallout", "", 8, 78, w - 16, 36, 26, RetroUi.Paper, TextAnchor.MiddleCenter);
            banner.gameObject.SetActive(false);
        }

        void OnIntervention(InterventionEvent e)
        {
            var f = director.Fighters[e.fighter];
            if (e.kind == InterventionKind.Special && e.success && e.step == 0)
            {
                var plan = iv.Side(e.fighter).plan;
                bannerText.text = $"必殺「{plan.name}」";
                bannerCallout.text = plan.callout;
                bannerCallout.gameObject.SetActive(!string.IsNullOrEmpty(plan.callout));
                Color fill = plan.HasAppearance ? plan.move.PrimaryColor : RetroUi.Team(e.fighter);
                bannerFill.color = fill;
                bannerText.color = bannerCallout.color = fill.r * .3f + fill.g * .59f + fill.b * .11f > .6f ? RetroUi.Ink : RetroUi.Paper;
                // カットインが終わってから技の間ずっと出す
                bannerLife = SpecialCutIn.Duration + BannerSeconds; banner.gameObject.SetActive(false);
            }
            else if (e.kind == InterventionKind.Special && e.success) { } // 二段目以降は告知しない
            else if (e.kind == InterventionKind.Cheer)
                presenter.ShowTechnique(e.brace ? "踏ん張り" : "声援", e.brace ? "BRACE" : "CHEER", e.fighter, f.HeadWorld);
            else if (!e.success && e.kind != InterventionKind.Cheer && iv.Side(e.fighter).controller != InterventionController.Com)
            {
                missText.text = "必殺 届かず…"; missText.color = RetroUi.Team(e.fighter);
                missLife = .7f; missText.gameObject.SetActive(true);
                Place(missText.rectTransform, f.HeadWorld + Vector2.up * .9f, 110, 20);
            }
        }

        void Update()
        {
            if (SettingsScreen.BlocksInput || ControlsGuide.BlocksInput) return;
            if (localSide < 0 || director.Ended) return;
            // 自操作は S・D（K・L も使える）。観戦は K・L
            if (Input.GetKeyDown(KeyCode.K) || SelfControl && Input.GetKeyDown(KeyCode.S)) iv.Press(localSide, InterventionKind.Special);
            if (Input.GetKeyDown(KeyCode.L) || SelfControl && Input.GetKeyDown(KeyCode.D)) iv.Press(localSide, InterventionKind.Cheer);
        }

        void LateUpdate()
        {
            if (director == null || rig == null || rig.Cam == null) return;
            float now = iv.Now;
            for (int i = 0; i < 2; i++)
            {
                var s = iv.Side(i);
                gaugeFill[i].sizeDelta = new Vector2(GaugeWidth * Mathf.Clamp01(s.gauge), 20);
                bool full = s.gauge >= 1f;
                if (i != localSide)
                {
                    string who = s.controller == InterventionController.Com ? "COM" : "相手";
                    gaugeState[i].text = s.SpecialPending(now) ? "必殺！" : full ? who + "  MAX" : who;
                    gaugeState[i].color = full ? RetroUi.Team(i) : RetroUi.Ink;
                }
                bool cheering = (now <= s.cheerUntil || s.braced) && !director.Ended;
                float wait = Mathf.Max(0, s.cheerReadyAt - now);
                float cooldown = Mathf.Max(.001f, director.Context.Balance.intervention.cheerCooldown/s.cheerRecoveryScale);
                cheerFill[i].sizeDelta = new Vector2(GaugeWidth * Mathf.Clamp01(1f - wait / cooldown), 20);
                cheerState[i].text = director.Ended ? "終了" : s.braced ? "踏ん張り" : cheering
                    ? $"効果 {Mathf.CeilToInt(Mathf.Max(0, s.cheerUntil - now))}秒"
                    : wait > 0 ? $"あと {Mathf.CeilToInt(wait)}秒" : "使用可";
                cheerState[i].color = cheering || wait <= 0 ? RetroUi.Team(i) : RetroUi.Ink;
                cheerTag[i].text = s.braced ? "踏ん張り！" : "声援中！";
                cheerTag[i].gameObject.SetActive(cheering);
                if (cheering) Place(cheerTag[i].rectTransform, director.Fighters[i].HeadWorld + Vector2.up * .55f, 90, 18);
            }
            if (localSide >= 0) UpdateLocal(now);
            if (missLife > 0) { missLife -= Time.unscaledDeltaTime; if (missLife <= 0) missText.gameObject.SetActive(false); }
            if (bannerLife > 0 && !TimeController.Paused)
            {
                bannerLife -= Time.unscaledDeltaTime;
                bool show = bannerLife > 0 && bannerLife <= BannerSeconds;
                if (show && !banner.gameObject.activeSelf) { banner.gameObject.SetActive(true); RetroUi.Enter(banner.gameObject, 0, 12); }
                if (!show) banner.gameObject.SetActive(false);
            }
        }

        void UpdateLocal(float now)
        {
            var s = iv.Side(localSide);
            bool live = director.Fighting && !TimeController.Paused;
            bool pending = s.SpecialPending(now);
            gaugeState[localSide].text = pending ? "間合いを詰める" : s.gauge >= 1f ? "MAX！" : Mathf.FloorToInt(s.gauge * 100) + "%";
            gaugeState[localSide].color = s.gauge >= 1f ? RetroUi.Team(localSide) : RetroUi.Ink;

            special.button.interactable = live && s.gauge >= 1f && !pending;
            RetroUi.Selected(special, special.button.interactable, RetroUi.Team(localSide));
            special.label.text = pending ? "必殺 準備中" : s.chainActive ? "必殺 発動中" : $"必殺「{s.plan.name}」  {(SelfControl ? "S" : "K")}";

            float cheerWait = s.cheerReadyAt - now;
            bool canBrace = iv.Side(1 - localSide).chainActive && !s.braced;
            cheer.button.interactable = live && s.CheerReady(now) && !s.braced;
            RetroUi.Selected(cheer, now <= s.cheerUntil || s.braced || (canBrace && s.CheerReady(now)), canBrace ? RetroUi.Red : RetroUi.Team(localSide));
            cheer.label.text = s.braced ? "踏ん張り中" : cheerWait > 0 ? $"声援  {Mathf.CeilToInt(cheerWait)}" : canBrace ? $"踏ん張る  {CheerKey}" : $"声援  {CheerKey}";
        }

        /// <summary>ワールド座標をアリーナ内の位置へ（左上が原点）。</summary>
        void Place(RectTransform rect, Vector2 world, float halfWidth, float halfHeight)
        {
            Vector3 p = rig.Cam.WorldToViewportPoint(world);
            float x = Mathf.Clamp(p.x * BattleHudPresenter.ArenaWidth, 60, BattleHudPresenter.ArenaWidth - 60);
            float y = Mathf.Clamp((1 - p.y) * BattleHudPresenter.ArenaHeight,
                LocalHeight + 12 + halfHeight + 8, BattleHudPresenter.ArenaHeight - 60);
            rect.anchoredPosition = new Vector2(x - halfWidth, -(y - halfHeight));
        }

        void OnDestroy()
        {
            if (director != null) director.Events.Intervention -= OnIntervention;
        }
    }
}
