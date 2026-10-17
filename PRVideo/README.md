# PR 動画（90 秒）

ゲームクリエイター甲子園向けの PR 動画。完成品は `out/MOJI_Fighter_X_PR.mp4`（2560×1440・30fps・90 秒）。

## 1. ゲーム映像の撮影

`Assets/Tests/PlayMode/PRVideo/PRVideoShots.cs` が各場面を自動で再現して録画する（通常のテストでは実行されない）。

```bash
F=MojiBattle.Tests.PRVideoShots
# 1) 一（筆文字・M・両手・猛攻）vs 木（筆文字・M・片手・H&A）を各ステージ 40 試合ずつ高速で回し、見せ場を採点する
PR_HARVEST_SEEDS=40 Tools/unity.sh prv_h -runTests -testPlatform PlayMode -testFilter "$F.Harvest" -testResults "$(pwd)/Reports/prv.xml"
#    → PRVideo/footage/harvest.tsv（点数順に並べ、使う試合を picks.tsv に「ステージ 番号」で書く）
# 2) picks.tsv の試合を等倍と 0.5 倍速で録画し、ほかの場面も撮る
Tools/unity.sh prv -runTests -testPlatform PlayMode -testFilter "$F.Picks;$F.Title;$F.Customize;$F.Fusion;$F.Modes;$F.Stages;$F.AiSpecial;$F.WorldCupFinal" -testResults "$(pwd)/Reports/prv.xml"
```

- 出力: `PRVideo/footage/<場面>.mp4` と `<場面>.events.tsv`（フレームごとの当たり・KO・試合内時刻・画面上の当たり位置など）
- 試合は 2560×1440、メニューは 1920×1080 で録画する。録画は実時間に合わせて 30fps で進む（メニューや演出が実時間で動くため）。
- 試合の録画ではゲーム内カメラを寄せる（`PRVideoRecorder.BattleZoom`）。
- `AiSpecial` は公開中のサーバーで AI の必殺技づくりを 1 回実行する（料金と回数上限を消費する）。
- ffmpeg は `PRVideo/tools/ffmpeg.exe` に置く。

## 2. 当たりの抽出

```bash
python PRVideo/scripts/moments.py   # → remotion/src/moments.ts
```

等倍と 0.5 倍速の録画の当たりを試合内時刻で突き合わせ、点数（ダメージ・クリティカル・KO など）を付ける。編集側はここから KO・山場の 3 発・技の紹介 8 種を自動で選ぶ。

## 3. 編集（Remotion）

```bash
cd PRVideo/remotion
npm install
cp ../footage/*.mp4 public/footage/
npx remotion studio          # ブラウザでプレビュー・調整
npm run render               # ../out/MOJI_Fighter_X_PR.mp4（2560×1440）を書き出す
```

- 構成とテロップ: `src/PRVideo.tsx`（場面ごとのコンポーネント。切り替えは曲の小節 `db(k)` と拍 `bt(i)` に合わせてある）
- 当たりの演出（直前まで等倍 → 一時停止と白黒反転のコマ → 0.5 倍速 → 等倍、墨しぶき、寄り、揺れ）: `src/hit.tsx`
- 演出部品（筆の帯・判子・墨のワイプ・集中線）: `src/fx.tsx`
- 曲: `public/music.wav` は `Assets/Audio/title.mp3` を 2.996 秒から使い、8 小節（13.607 秒）を切って 90 秒に収めたもの。切れ目は動画の 64.18 秒（元の曲の 67.16 秒 → 80.77 秒）で、クライマックス前のブレイクに入るフィルの直前（36 小節目の 3 拍目）につないでいる。拍の位置は `src/beats.ts`。
- 効果音はゲームの `Assets/Resources/Audio/Combat` から、フォントは同梱の Yuji Boku（筆文字）と Noto Sans JP Bold を使用。
