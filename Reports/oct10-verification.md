# 2026-10-10 称号・説明・COM声援・初期必殺技の修正

## 変更

- 称号画像の行ごとに異なる横幅を測り直して切り出しを修正。全50個の一覧と詳細、全9ページ、880×450を描画確認。
- 隣の斜めの筆が触れる28・34番はUIの描画範囲を絞り、残る小さな画像片も除去。
- 10番「大逆転」は元画像に隣の余分な拳が重なるため、独立した透過PNGを使用。既存アトラスは維持。
- 戦闘開始時の説明とヘルプからジャストガードとJキー・クリックの案内を除去。現在のK必殺・L声援／踏ん張りを表示。
- COMは満タンのHPでも声援を使用し、声援ゲージが回復すると再使用。必殺入力のタイマーと声援のタイマーを独立させた。
- 声援も踏ん張りも声援ゲージを消費し、再使用待ち中は使用不可。COM・ローカル・友人対戦の受付処理と表示を統一。効果中の声援による自動踏ん張りは、その声援の支払い済みゲージで軽減する。
- 既存の一閃に加えて初期必殺技を30種類追加。各漢字は意味の近い技へ割り当て、それ以外の文字にも端末間で同じ技を固定割り当て。技名・30種類の連携・11属性・6演出・能力配分が異なる。自作技を優先し、初期技は登録枠を消費しない。

30種類の割り当て表は [BuiltinSpecialMoves.md](../Assets/Documentation/BuiltinSpecialMoves.md)。

## 自動・描画確認

- `oct10-final-edit.xml`: EditMode 34件成功（初期技の名前・全30連携の違い・能力上限・固定割り当て・コピーの独立性、自作技の保存、称号、装備、大会）。
- `oct10-final-play.xml`: PlayMode 44件成功（全30種類の実戦での全段発動とダメージ、COMの満タンHP時の声援と再使用、踏ん張りのゲージ消費と未回復時の拒否、友人同期、通常戦、大会、友人対戦、メニュー、称号）。
- `oct10-badge-com.xml`: 最終画像差し替え後の描画・COM・ゲージ・必殺カットインの6件成功。
- COM同士の実戦ログ: `specials=2 cheers=4 result=KO`。声援の成功が必殺技の成功と独立して記録された。
- [全50個の確認画像](UI/Achievements/fixed_all_50_badges.png)。
- [添付画像と同じ3ページ目](UI/Achievements/fixed_page_3.png)、[880×450](UI/Achievements/fixed_page_3_880x450.png)。
- 各ページは `UI/Achievements/fixed_page_1.png` ～ `fixed_page_9.png`。

## 画像生成

利用: built-in `image_gen`（imagegenスキル）。保存先: `Assets/Resources/UI/Achievements/Badge10.png`。RGBA、1254×1254、透明部分のalpha=0を確認。

入力: `Assets/Resources/UI/Achievements/Badges.png`。最終プロンプト:

> Use case: background-extraction / precise-object-edit. Asset type: one transparent game achievement badge. The input is an atlas edit target. Extract ONLY the red circular medal with the upward-turning black curved arrow, the TENTH badge in the FIRST row (near the upper-right, immediately left of the extra clenched-fist medal). Produce this SINGLE medal by itself, centered in a square canvas, on genuinely transparent background. Preserve its original paper-colored circle, red printed accents, cream/white arrow outline, black scalloped medallion rim, red ribbon tails, slightly distressed ink and paper illustration style. Restore the full smooth rounded right rim and the complete curved arrow tail that were crowded by the adjacent fist. Remove EVERY fragment of the neighboring fist or any other badge. Full medal must fit comfortably within canvas with 6% clear margin all around; no cropped edges, no text, no additional symbols, no sheet, no other badges. Keep the original symbol and palette very close to the reference.

## Web版

Unity 6000.0.61f1 のWebGL本番ビルド成功。ビルド日時（UTC）は `2026-10-10T07:27:50.9897601Z`。出力は `Builds/WebGL`。

ローカル確認先: http://127.0.0.1:8017 。`oct10-web-local-delivery.json` で11ファイルのSHA256一致、gzip整合性、Wasm/JavaScriptのMIME、フォントとUnicodeライセンスを検証済み。

Web版の境界確認には `vercel:verification` スキルを参照。称号画面 → 画像、漢字入力 → 初期技の選択 → 実戦の各段、COM判断 → 声援／踏ん張り受付 → ゲージ消費 → HUDと友人同期は上記のUnityテストで検証した。自作技のサーバー連携の回帰確認にはローカルの `SPECIAL_FAKE=1` を使い、公開サーバーの設定は変更していない。

ブラウザ自動操作の起動コマンドは実行環境から `blocked by policy` として拒否された。今回のWebビルドの実ブラウザ操作は未確認であり、配信整合性の検証とは区別する。公開サイトへのデプロイは行っていない。
