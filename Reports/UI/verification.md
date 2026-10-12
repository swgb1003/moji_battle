# MOJI Fighter X — メニューUI更新

承認された画像 `approved-reference.png` を基準に、生成りの紙、墨色、朱色、群青、二重枠とピクセル状の隅飾りを実装。
タイトル・モード選択・カスタマイズ・トーナメントを共通の1920×1080座標系に統一し、対戦確認と試合終了後の操作にも同じ部品を適用。
大会表は実際の8人制トーナメントを正しく表示するため、8→4→2→1の接続を維持。

## 表示と操作

- Unityで `Assets/Scenes/Title.unity` を開いてPlay。
- プレビューは専用カメラとRenderTextureで表示。文字サイズや握る位置を変えても設定欄にはみ出さない。
- 字形のクリック・ドラッグで握る場所を設定。ガイド表示で当たり判定・重心を確認できる。
- 選択中の項目はP1が朱、P2が群青。入力不可の文字や空欄では対戦確認を無効化。
- 画面の縦横比が異なる場合も画面全体が収まるよう余白を取り、操作を欠けさせない。
- Enter / Spaceでタイトルから開始。Ctrl+Enterで対戦確認・開始。Escで戻る。
- F6で動きを抑える設定を切り替え、次回起動にも保存。

## アニメーション

ロゴ・パネル・説明・ボタンの時間差による登場、枠線や大会の接続線を描く演出、ボタンのホバー・キーボード選択・押し込み、設定選択時の弾み、タイトルの武器の揺れ、カスタマイズの試し振り、VSとリザルト操作の登場演出。
UIのアニメーションはunscaledDeltaTimeで実行し、観戦の一時停止・速度変更から独立。

## 検証

Unity 6000.0.61f1、PlayMode。
`MenuUiTests` / `CustomizeFlowTests` / `TournamentFlowTests`: 9件成功（menu-tests.xml）。
画面遷移、P1/P2設定、XL・反転、文字入力、ガイド表示、大会の勝利・敗北・引き分け、再戦と設定保持、一時停止中のUIアニメーション、動きの抑制を確認。
1920×1080、880×450、1280×960の実描画を撮影。各ボタンが表示領域内に収まることも検査。
最終整理後のUI再検証はmenu-final.xmlに保存。

対戦確認画面の武器文字とパラメータの中央軸を修正。文字とVSをひとつの文字列にする配置をやめ、P1・P2それぞれ共通の列に武器文字と設定を配置。既存のメニュー表示・操作テスト1件成功（vs-alignment-tests.xml）。修正後の実描画は05_vs.png。

2026-10-08：トーナメント表の選手枠を修正。通常枠を1から2.5基準ピクセルへ変更し、縮小表示で四辺が消えることを防止。枠線を背景・番号・文字の後に生成し、内容に隠れないようにした。Unityの作業コピーで1920×1080・1230×690・880×450を描画し、未確定枠、準決勝、決勝、優勝後の表示を目視確認。撮影用実行は成功（tournament-border-capture.xml）。実描画：tournament_border_empty_1230.png / tournament_border_empty_880.png / tournament_border_round_1.png / tournament_border_round_2.png / tournament_border_round_3.png。

スクリーンショット: 01_title.png / 02_mode_select.png / 03_customize_p1.png / 04_customize_p2_xl.png / 05_vs.png / 06_tournament.png / 07_result.png。
戦闘HUD・戦闘描画・AI・物理・バランスは今回変更していない。BattleBootstrapの変更は試合終了後のボタン部分のみ。

## 画像素材

生成は内蔵image_genツールを使用。素材はプロジェクトのResources内へコピーし、ビルドから参照できる状態にしている。

- `Assets/Resources/UI/MojiFighterXLogo.png`
- `Assets/Resources/UI/PrintedPaper.png`

ロゴの最終プロンプト:

> Create a reusable transparent-background game logo asset from ONLY the logo in the upper-left screen of this reference board. Reproduce the same exact heavy vintage printed lettering, same slight dynamic tilt, same proportion and letter shapes: top line "MOJI" in near-black #20211F, second line "Fighter X" with Fighter in near-black and the large X in vermilion #BA3C2D, with the small red square「闘」seal immediately beside X. The exact game title is "MOJI Fighter X". Subtle printed ink wear contained inside the letters, opaque solid ink shapes with transparent surrounding background. The logo should tightly fill a wide horizontal canvas, centered with small clean transparent padding. Remove every other thing: NO paper background, NO border, NO subtitle, NO other headers, NO UI, NO buttons, NO glyph weapons, NO stick figures. This is a faithful extraction/recreation of the approved reference logo as a standalone RGBA UI asset.

紙の最終プロンプト:

> Create a plain blank paper texture asset for the game UI in this reference image. Match the warm creamy off-white #F2EBDD aged printing paper of the reference: quiet subtle mottled fibers and very fine natural tan speckles, slight irregular warmth, restrained old paper texture. Landscape 16:9 flat front-facing full bleed texture. Uniformly light so black game text stays perfectly readable. Only blank paper: remove all letters, all logos, all illustrations, all borders, all UI, all stains, all creases, all torn edges and all objects. No vignette or dramatic lighting. The entire result must be an unobtrusive seamless-looking blank paper surface, similar to the reference's calm spaces.
