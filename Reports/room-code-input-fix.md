# 友人対戦のルームコード手入力

## 修正

ルームコード欄に、既存のWeb入力ブリッジを接続した。Webではブラウザ標準の入力欄でキー入力を受け、既存の正規化と6マス表示へ渡す。ブラウザ入力欄は透明にして承認済みの6マスを維持し、フォーカス枠とキャレットを表示する。

手入力、Backspace、小文字の大文字化、貼り付けの区切り除去に対応。入力中はUnityのキーボード取得を解除する。Enterは入力確定後に有効なコードで参加し、IME候補選択のEnterでは参加しない。通信中とロビーでは入力を隠し、画面を離れると削除する。正規化後も編集位置を保つ。

## 検証

- DOM回帰テスト11件が成功（既存の武器・必殺名IME入力9件、コード入力2件）。
- Unity PlayModeのコード入力テスト1件が成功。段階的な入力、正規化、6マス、参加可能状態、Backspace、通信中の遅れた入力拒否を確認。
- WebGLビルド成功（UTC `2026-10-09T02:05:04.5002672Z`）。
- ローカルブラウザで `pressSequentially` による1文字ずつのキー入力、Backspace、再入力を実操作。大文字化、フォーカス枠、6マス表示を確認。
- 公開版でも検証用ホストのコードを1文字ずつ小文字で入力し、Enterでゲストとして参加できた。ロビーへの移動、入力欄の非表示、退出後の復帰を確認。検証用の両者は退出済み。
- 本番ブラウザのエラーレベルのコンソールログは0件。
- ローカル・本番とも11件の配信ファイル一致とgzip/MIMEを検証（`room-code-web-local-delivery.json`、`room-code-web-production-delivery.json`）。

## 公開

https://moji-fighter-x.vercel.app

既存プロジェクト `moji-fighter-x`、scope `swgb1003-5441s-projects` にWebGL成果物のみを公開。

Production `dpl_CqhaAVqJnChiaiXAiLoFeX6eZLEi` / READY

画像: `UI/room-code-local-typed.jpg`、`UI/room-code-production-typed.jpg`、`UI/room-code-production-joined.jpg`。

スマートフォンのソフトキーボードとOSのIME候補ウィンドウは今回の実機確認には含めていない。
