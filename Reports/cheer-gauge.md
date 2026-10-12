# 両陣営の声援ゲージ

バトルHUDの必殺ゲージの下に声援の再使用ゲージを追加。自分と相手の両方に表示し、陣営の赤・青に合わせる。満タンで「使用可」、声援の効果中は「効果 ○秒」、再使用待ちは「あと ○秒」、踏ん張り中は「踏ん張り」、試合終了時は「終了」と表示する。

ゲージはゲーム内時間で回復する。一時停止中は残り時間を維持し、観戦速度に追従する。既存の友人対戦フレームに含まれる声援の残り効果時間・再使用待ち・踏ん張りを参照する。戦闘ルールや通信形式の変更はない。

操作ボタンを1行下へ移動し、頭上の声援表示が拡大したパネルに重ならないよう位置の上限を調整した。

PlayModeテスト4件成功（`cheer-gauge-tests.xml`）。相手への声援使用で効果表示とゲージ消費を確認し、友人対戦の受信フレーム適用による待機・半分回復・使用可能・踏ん張りの表示、停止中の時間維持を検証。既存の声援効果・自動踏ん張り・必殺操作も成功。880×450のUI画像を確認済み。

検証画像: `UI/battle_cheer_opponent_effect.png`、`UI/battle_cheer_opponent_cooldown.png`、`UI/battle_cheer_880x450.png`。

最終WebGLビルド成功（UTC `2026-10-09T02:56:48.1136917Z`）。ローカルの実バトルで両側の声援ゲージと声援ボタンを確認。ローカル・本番とも最終ビルドの11ファイルの一致、gzip、MIMEの確認成功（`cheer-gauge-local-delivery.json`、`cheer-gauge-production-delivery.json`）。

公開先: https://moji-fighter-x.vercel.app

本番確認中、相手の必殺演出で既存の文字描画エラーを検出。`SpecialCutIn` が4方向の縁取りを持つ `Outline` を4個と影を1個重ね、頂点数が乗算されて65000の描画上限を超えていた。`Outline` 1個と影1個に整理した。相手の必殺イベントを受け、踏ん張りの合図のメッシュが実際に描画される回帰テストを追加。修正後の表示・必殺操作・声援ゲージの3テスト成功（`cheer-gauge-render-tests.xml`）。テスト合計は5種類成功。

必殺演出の検証画像: `UI/battle_opponent_special_brace_prompt.png`。

最終デプロイ: `https://moji-fighter-o0h6f2n0x-swgb1003-5441s-projects.vercel.app`（Ready）。

最終公開版で実バトルを開始し、相手の必殺が発動してゲージを消費するところまで確認。再読み込み以降のエラーレベルのブラウザログ0件。実画面: `UI/cheer-gauge-local.jpg`、`UI/cheer-gauge-production.jpg`。
