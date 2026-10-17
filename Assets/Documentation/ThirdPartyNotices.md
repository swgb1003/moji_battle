# Third Party Notices

## Noto Sans JP Bold（字体「ゴシック」）

- 同梱ファイル: `Assets/Art/Fonts/Gothic/NotoSansJP-Bold.ttf`（ライセンス全文: `Assets/Art/Fonts/Gothic/OFL.txt`）
- 用途: `GlyphBakeWindow` / `GlyphBaker` で字形マスクをベイクする（実行時にフォントは読まない）
- バージョン: Version 2.004-H2
- 著作権: (c) 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'.
- ライセンス: SIL Open Font License 1.1 — https://openfontlicense.org/
- 出典: https://fonts.google.com/noto/specimen/Noto+Sans+JP（このプロジェクトでは Windows にインストール済みのファイルを複製）
- SHA-256: 9e4e354e728cfc32e04bbc0bf686c7285a1cf47f64ce2ac27c1d813ad1dc9d63
- ベイク生成物（`Assets/Art/Glyphs/Baked/gothic/*.png/.json`、`Assets/Resources/Glyphs/Definitions/*.asset`）にも同じハッシュを記録している

## しっぽり明朝 Bold（字体「明朝」）

- 同梱ファイル: `Assets/Art/Fonts/Mincho/ShipporiMincho-Bold.ttf`（ライセンス全文: `Assets/Art/Fonts/Mincho/OFL.txt`）
- 用途: 字形マスクのベイクと、入力された文字の実行時の字形化
- バージョン: Version 3.110
- 著作権: Copyright 2021 The Shippori Mincho Project Authors (https://github.com/fontdasu/ShipporiMincho)
- ライセンス: SIL Open Font License 1.1 — https://openfontlicense.org/
- 出典: https://github.com/google/fonts/tree/main/ofl/shipporimincho
- SHA-256: 63bc4eddc74793f671c3ab827c5175e773ffbe569d0bf50ee65375ea9e3bc286

## Zen Maru Gothic Bold（字体「丸ゴシック」）

- 同梱ファイル: `Assets/Art/Fonts/RoundedGothic/ZenMaruGothic-Bold.ttf`（ライセンス全文: `Assets/Art/Fonts/RoundedGothic/OFL.txt`）
- 用途: 字形マスクのベイクと、入力された文字の実行時の字形化
- バージョン: Version 1.001
- 著作権: Copyright 2021 The Zen Maru Gothic Authors (https://github.com/googlefonts/zen-marugothic)
- ライセンス: SIL Open Font License 1.1 — https://openfontlicense.org/
- 出典: https://github.com/google/fonts/tree/main/ofl/zenmarugothic
- SHA-256: fe24426b9c8b5523a0146a8235c8674eccf0493af354a53ec895c3596d9eb745

## 佑字 朴 Regular（Yuji Boku、字体「筆文字」）

- 同梱ファイル: `Assets/Art/Fonts/Brush/YujiBoku-Regular.ttf`（ライセンス全文: `Assets/Art/Fonts/Brush/OFL.txt`）
- 用途: 字形マスクのベイクと、入力された文字の実行時の字形化
- バージョン: Version 3.002
- 著作権: Copyright 2021 The Yuji Project Authors (https://github.com/Kinutafontfactory/Yuji)
- ライセンス: SIL Open Font License 1.1 — https://openfontlicense.org/
- 出典: https://github.com/google/fonts/tree/main/ofl/yujiboku
- SHA-256: 94fda16384f3bdac24376a000c57e99abfa314961bd89ef27badfb7410322003

いずれも初期 9 文字（一口山火鬱AIOX）を自前の字形として収録していることを cmap で確認済み。

## Noto Sans CJK JP Black（ワールド文字カップの見出し）

- 同梱ファイル: `Assets/Resources/UI/WorldCup/Fonts/NotoSansCJKjp-Black.otf`（ライセンス全文: `Assets/Resources/UI/WorldCup/Fonts/OFL.txt`）
- 用途: ワールド文字カップ画面の見出し（`WorldCupDesign.HeadingFont`）
- 著作権: Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'.
- ライセンス: SIL Open Font License 1.1 — https://openfontlicense.org/
- 出典: https://github.com/notofonts/noto-cjk
- SHA-256: cd66686594a861a42fe752990a406fcedafb51b3a91d6639f30f8948f7a10b4d

# 生成AIで作成した素材

ゲーム内の曲・効果音・画像は、以下の生成AIサービスで作者が作成したもの。各サービスの利用規約に従って使用している。

## BGM（Suno）

- 作成: Suno（https://suno.com/）、作成者アカウント sw_gb_1003
- 収録先と使用場面（`Assets/Resources/Audio/BattleMusic.asset` で割り当て）

| ファイル | 曲名 | 場面 | 作成日（UTC） |
|---|---|---|---|
| `Assets/Audio/title.mp3` | Adventure Awaits | タイトル | 2026-10-07 |
| `Assets/Audio/menu.mp3` | 勇者の冒険 | メニュー | 2026-10-07 |
| `Assets/Audio/custermize.mp3` | 装備選択画面 | カスタマイズ | 2026-10-07 |
| `Assets/Audio/draft.mp3` | 冒险选择画面 | 文字ドラフト | 2026-10-11 |
| `Assets/Audio/taunament.mp3` | 決戦の瞬間 | トーナメント | 2026-10-07 |
| `Assets/Audio/battle.mp3` | Adventure Awaits | バトル 1 | 2026-10-07 |
| `Assets/Audio/battle2.mp3` | 神の決闘 | バトル 2 | 2026-10-07 |
| `Assets/Audio/battle3.mp3` | Pixel Showdown | バトル 3 | 2026-10-07 |
| `Assets/Audio/final2.mp3` | 決戦の鼓動 | 決勝 | 2026-10-17 |

## 効果音

- Runway Sound Effects（https://runwayml.com/）で作成: `Assets/Resources/Audio/Combat/` の 38 素材と、
  `Assets/Audio/UI/title_start.wav`・`battle_start.wav`。生成プロンプトとタスク ID は `Tools/combat_audio_sources.json`・`Tools/ui_audio_sources.json`。
- 自作（プログラムで合成）: `special_cutin.wav`・`special_impact.wav`（numpy で合成）。
- 声援 `cheer_01〜04.wav`: Windows の音声合成（Microsoft Haruka・Ichiro）の声を重ね、歓声と反響を合成したもの。

## 画像

- OpenAI の画像生成（ChatGPT / Codex の画像生成機能）で作成: `Assets/Resources/UI/` 以下の PNG 34 点
  （ロゴ `MojiFighterXLogo.png`、紙の背景、ステージ背景と小物 `Stages/`、称号バッジ `Achievements/`、ワールド文字カップの背景・トロフィー・図解 `WorldCup/`、タイトル背景 `Title/`）。
- `Design/` 以下の画像はデザイン検討用の案で、ゲームには含まれない（生成時の指示文を同じフォルダに保存）。
- 文字の武器（`Assets/Art/Glyphs/Baked/`）は上記フォントからプログラムで生成したもの。

## ゲーム内で使う生成AI

- AI 必殺技の生成: Anthropic の Claude API（`Server/specials.mjs`、既定モデル `claude-opus-5-5`）。技名から動きと見た目のデータを作る。

## HUD 表示用フォント

P1/P2 の HUD・擬音は実行時に OS フォント（Noto Sans JP → Yu Gothic UI → Meiryo → Arial の順で存在するもの）を
`Font.CreateDynamicFontFromOSFont` で参照している。P4 で TextMesh Pro 用の同梱フォントへ置き換える。
