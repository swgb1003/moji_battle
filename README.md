# 文字武器オートバトル — P2 字形パイプライン＋カスタマイズ版

仕様書「文字武器オートバトル — プロトタイプ開発仕様書 v1.0」の P0（物理箱庭）・P1（戦闘の芯）・P2（字形パイプライン）と、
「カスタマイズ機能①〜④ 実装仕様書」（持ち方・戦闘スタイル・握る位置・文字サイズ）を実装した Unity プロジェクト。
好きな 1 文字（同梱のゴシック体フォントにある漢字・かな・英数字・記号）から武器を生成し、カスタマイズして完全オートの戦闘を観戦できる。
ゴシックの 9 文字（一 口 山 火 鬱 A I O X）はベイク済みアセット、それ以外は実行時に同じ処理で字形化する。

- Unity 6000.0.61f1（`ProjectSettings/ProjectVersion.txt`）、Built-in レンダーパイプライン、Unity 2D Physics
- 確認結果: [P1](Assets/Documentation/P1_Verification.md) / [P2](Assets/Documentation/P2_Verification.md) / [カスタマイズ](Assets/Documentation/Customize_Verification.md)
- 調整値と経緯: [Assets/Documentation/BalanceNotes.md](Assets/Documentation/BalanceNotes.md)

## 動かし方

Unity Hub でこのフォルダを開き、`Assets/Scenes/Title.unity` を開いて Play。
START → カスタマイズ（P1/P2 それぞれ、好きな文字を 1 つ入力・サイズ・持ち方・握る位置・戦闘スタイル）→ BATTLE → VS 確認 → 観戦。
入力した文字はその場で同梱フォントから字形化される（フォントに字形の無い文字・絵文字・空白は使えない）。
リザルトで「同じ設定でもう一度」/「カスタマイズへ戻る」。設定は次回起動時も残る。

`Assets/Scenes/Battle.unity` を直接 Play すると、カスタマイズ無し（P1/P2 検証どおり）の一 vs 鬱 になる。

| キー | 操作（観戦のみ。AI への操作はない） |
|---|---|
| Space | 一時停止 / 再開 |
| 1 / 2 / 3 | 観戦速度 0.5× / 1× / 2× |
| R / T | 新しい seed で再戦 / 同じ seed で再戦 |
| Z / X | 左の文字を前 / 次へ（9 文字を順に） |
| N / M | 右の文字を前 / 次へ |
| C / D | Collider・握り点・重心の表示 / AI の状態表示（スタイル・質量・leverArm・距離・射程） |
| B / Esc | カスタマイズへ戻る |

`BattleBootstrap`（シーン内）でも文字・字体・seed を指定できる（字形があるのはゴシックの 9 文字）。

## 字形のベイク

メニュー **MojiBattle → Glyphs → Glyph Bake Window** →「すべてベイク」。フォントや解析設定を変えたら再ベイクする。
実行時の字形化に使うフォント一覧（`Resources/Glyphs/GlyphFontLibrary.asset`、cmap の範囲を含む）は ProjectSetup が作る。
結果の能力一覧は `Reports/glyph_stats.md`。

## テスト

```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Reports/editmode.xml
```

```bash
"/c/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Reports/playmode.xml
```

PlayMode の seed 掃引は `Reports/p1_seed_sweep.md/.json`、撮影画像は `Reports/Screenshots/` に出力される。
調整用: `Tools/sweep.sh`（`SWEEP_SEEDS=40` で試合数変更）、`TRACE_SEED=<seed>` を付けて `-testFilter MojiBattle.Tests.TraceDebug` で 1 試合のトレース。

## 構成

```text
Assets/
  Scripts/Core      進行(MatchDirector)・組み立て(BattleBuilder/FighterFactory/ArenaBuilder)・時間管理・イベント・計測
  Scripts/Glyphs    字形マスク解析(GlyphMaskAnalyzer)・キャリブレーション・GlyphCatalog
  Scripts/Stats     CombatBalance（全調整値）・StatCalculator
  Scripts/AI        FighterBrain（0.15 秒周期の判断）・Styles（戦闘スタイルの Strategy）
  Scripts/Customize FighterBuildData・BuildRuntimeStore・CustomizeBalance・FighterModifiers・握り/サイズの計算
  Scripts/Physics   Fighter・FighterMotor2D・WeaponMotor2D（ヒンジ＋トルク制御）・KnockdownController・接触報告
  Scripts/Combat    HitResolver（1ステップ蓄積→解決・attackId 台帳）・EnvironmentImpactResolver・DamageMath
  Scripts/UI        HUD・演出(CombatVfxDirector)・カメラ・棒人間表示・Collider デバッグ表示・タイトル
  Scripts/UI/Customize カスタマイズ画面（uGUI をコードで組み立て）・リアルタイムプレビュー
  Scripts/Editor    GlyphBaker / GlyphBakeWindow（字形ベイク）・cmap 検査・ProjectSetup（アセット/シーン生成）
  Art/Fonts/        同梱フォント（Gothic: Noto Sans JP Bold + OFL.txt）
  Art/Glyphs/Baked/ ベイク済みマスク PNG・解析 JSON
  Resources/        CombatBalance.asset・CustomizeBalance.asset・GlyphCalibration.asset・Glyphs/Definitions（GlyphDefinition）
  Tests/            EditMode / PlayMode
```

## 仕様との差分

- 字体はゴシックのみ（明朝・丸ゴシック・筆文字は P3）。キャリブレーションはゴシック 9 文字からの暫定値。
- 字形の描画は TextMesh Pro ではなく Unity の動的フォントのアトラスを使う（同じ em 枠・基準線・256×256 は仕様どおり）。
- Collider が 64 個を超える場合、解像度を下げる代わりに「画線からはみ出す面積が最小の矩形同士」を統合して 64 個に収める。
- 試合ごとに専用の 2D 物理シーンを作り手動で Simulate（同 seed の再現性のため）。表示は前後ステップを描画側で補間。
- ラグドールは簡易版（本体の回転を解放して転がす）。HUD は TextMesh/IMGUI の簡易版、音なし。
- 戦闘を成立させるために追加した要素: 突き・斬り上げの攻撃種別、上段/下段ガード、重い武器による叩き落とし、
  重なり・密着の自動解消、AI の反応率など（詳細は BalanceNotes）。
- 試合規則の変更: 時間切れで残 HP 割合が同じなら延長戦（先にダメージを入れた方の勝ち、最大 30 秒）。
- 戦闘テンポの見直し（仕様外の追加）: 構えているだけの字形は盾にならない（字形どうしの衝突はガード中・攻撃どうしの時だけ）、
  横振り（奥を回り込む薙ぎ払い）、火力・HP の見直し、重量級の攻撃中のスーパーアーマー。詳細は BalanceNotes。
- 技の追加（仕様外）: 刺す（腕を伸ばして先端で突く）・足払い・盾当て・打ち上げ（浮かせて追撃）・回転斬り。詳細は BalanceNotes。
- 武器の最大辺は 2.2 → 1.8（字形が棒人間より大きく弾き合い・引き分けが多かったため。検証は BalanceNotes）。
- カスタマイズの仕様との差分（握る位置を長軸に沿わせる、攻撃の段階を武器の実際の角度で進める等）は
  [Customize_Verification](Assets/Documentation/Customize_Verification.md) の 4 章。
- 8.3 の拡張「叩きつけ」: 相手の武器で持ち上げられ（高さ 1.0 以上）、離れて 1.5 秒以内に床・壁へ速度 5.5 以上で落ちると
  `clamp((vN-5.5)×2, 0, 20)` のダメージ（持ち上げ 1 回につき 1 回、持ち上げた側の得点）。
