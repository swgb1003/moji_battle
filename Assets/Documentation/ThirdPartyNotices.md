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

## HUD 表示用フォント

P1/P2 の HUD・擬音は実行時に OS フォント（Noto Sans JP → Yu Gothic UI → Meiryo → Arial の順で存在するもの）を
`Font.CreateDynamicFontFromOSFont` で参照している。P4 で TextMesh Pro 用の同梱フォントへ置き換える。
