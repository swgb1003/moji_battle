# MOJI Fighter X — 戦闘UI

承認画像 `battle-approved-reference.png` に合わせて、薄いヘッダー・HP帯、広い戦闘エリア、細い観戦操作バーへ変更。
1920×1080基準。戦闘領域は (42,185)、1836×786。メニューの紙・墨・朱色・群青、二重枠、隅飾りを共通化。

## 表示と操作

- BattleHudPresenterをTextMesh/IMGUIからuGUIへ移行。上部に文字・スタイル・左右のHP・残り時間・AUTO。
- 下部に一時停止/再開、0.5×/1×/2×、詳細、もう一度。既存のキーボード操作も維持。
- 詳細は開閉式。字体、能力値、重量、サイズ、持ち方、握る位置、スタイル、seed、デバッグ操作を表示。
- 戦闘映像は透明な専用RenderTextureをRawImageへ合成。戦闘エリア内でクリップし、HP帯や操作バーへはみ出さない。
- 画面用のBattleDisplayCameraを追加。戦闘カメラがRenderTextureに描画していても、Display 1へ描くカメラが存在し、Unity Gameビューの「No cameras rendering」の原因を解消。
- カメラは両者の体と文字武器の実際の墨の範囲を収める画角へズームし、左右・上下に追従。近接時の最小orthographicSizeは2.6（以前は4.6）。両者が離れると素早く引き、接近すると滑らかに寄る。
- 地面線もカメラに映る地面の位置へ追従。空中戦を追って地面が画角の外へ出ると、地面線と床の帯を隠す。
- 棒人間の頭を黒い円に変更。文字武器・軌道・衝突の色も共通の朱色・群青へ変更。
- 技名は斜めの紙札、陣営色の罫線、英語名を組み合わせる。既存の衝突・墨飛沫・ヒットストップを維持。
- HPの遅れて減る帯、登場演出、操作ボタンのホバー・押し込み・選択の動きはunscaled時間で動く。
- 大会中の再戦は無効。勝敗・大会進行・再戦時の設定保持を維持。
- AI、物理、当たり判定、能力値の計算は変更していない。生成画像の棒人間のポーズや衝突位置は固定せず、実際の対戦状態を描画する。

## 検証

Unity 6000.0.61f1、PlayMode 11件成功（`battle-final.xml`）。
BattleUiTests 2件、MenuUiTests 3件、BattleSceneSmokeTest 1件、TournamentFlowTests 5件。
停止と再開、3段階の速度変更、選択表示、詳細の開閉、停止中のHPアニメーション、再戦時の古いCanvas/RenderTexture破棄、カスタマイズへの復帰、既存の大会勝利・敗北・引き分けを確認。
1920×1080、880×450、1280×960の実描画を撮影。ボタンが表示領域内に収まることを検査。
開いているUnityを閉じずに、ワークスペース内の一時的な検証用コピーで実行し、終了後にコピーを削除。

カメラ修正後の再検証: PlayMode 10件成功（`camera-tests.xml`）。BattleUiTests 3件、リザルト・再戦フロー1件、BattleSceneSmokeTest 1件、TournamentFlowTests 5件。
画面用カメラの有効状態、再戦・画面遷移時の破棄、接近時の拡大、戦闘位置への横追従、離れた両者の画角への収まり、空中追従、地面線の追従/非表示を確認。カメラの速度・揺れも試合開始時にリセット。
追加画像は `battle_camera_close.png` と `battle_camera_airborne.png`。

スクリーンショット:

- `battle_01_live.png`
- `battle_01_880x450.png`
- `battle_01_1280x960.png`
- `battle_02_details.png`
- `battle_03_result.png`

## 画像素材

内蔵image_genツールで作成し、`Assets/Resources/UI/BattlePaper.png` に保存。
承認画像を背景の参照として使用。文字・棒人間・操作・枠は素材に含めず、実際のゲームで描画。

最終プロンプト:

> Create a reusable background-only texture for the combat arena of this exact reference game UI. Reproduce just the central wide paper arena with the extremely faint pale gray-beige circular dry-brush ink trace sweeping around the far left and right edges, subtle warm ivory handmade paper fibers. Landscape wide 2.4:1 full bleed flat texture. The brush circle's center is around the middle-lower portion, leaving the entire center very light clean blank paper for live characters. Soft understated #F2EBDD paper and very light beige ink, no dark strokes or dramatic stains. REMOVE ALL foreground and UI: absolutely NO text, NO glyphs, NO fighters, NO particles, NO ground line, NO floor strip, NO outer border, NO corners, NO buttons, NO HUD, NO symbols or red/blue marks. Only light paper and the pale dry-brush circle at outer edges, faithfully match reference arena background, no photograph or perspective.
