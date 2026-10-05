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

## 明朝・丸ゴシック・筆文字（P3 で追加予定）

未選定。再配布可能で「鬱」を含む初期 9 文字を収録していることを `GlyphBaker` の cmap 検査で確認してから同梱する。

## HUD 表示用フォント

P1/P2 の HUD・擬音は実行時に OS フォント（Noto Sans JP → Yu Gothic UI → Meiryo → Arial の順で存在するもの）を
`Font.CreateDynamicFontFromOSFont` で参照している。P4 で TextMesh Pro 用の同梱フォントへ置き換える。
