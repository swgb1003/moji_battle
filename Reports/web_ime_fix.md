# Web版の日本語IME入力修正

公開URL: https://moji-fighter-x.vercel.app

Production: `dpl_C6AXpUwTx1bSvBfksY8pKLpPkjyD` / READY

## 原因

Unity Webは標準のIME入力に未対応。カスタマイズのUGUI InputFieldをそのままWebでも使用していたため、日本語の変換ができなかった。加えて一文字制限とonValueChangedからの再描画を変換途中の文字に適用すると、ローマ字・かなの変換を妨げる。

参考: https://docs.unity.com/ja-jp/engine/6000.0/manual/unity-editor/editor-features/imeinput

## 修正

Web版のみ、文字欄にブラウザ標準のinputを重ねる。変換中はUnityへ送信・文字数の切り詰め・Unityからの値の上書きを行わず、確定した文字を既存の字形検証と装備設定へ渡す。変換確定のEnterと入力終了のEnterを区別し、貼り付けも同じ検証へ通す。複数文字は従来どおりエラー表示し、勝手に先頭文字へ置き換えない。

入力中はUnityのキーボード取得を解除し、入力終了・画面移動時に復元。P1/P2の切り替え、確認画面での非表示、画面移動時の削除、画面サイズ変更に対応。全画面の対象をcanvasとHTML入力欄の親要素へ変更。

## 検証

- DOMのIMEイベント回帰テスト: 8件成功。ローマ字→かな→漢字、ひらがな・カタカナ、Unicode貼り付け、候補確定Enter、変換途中のblur、P1/P2変更、非表示・削除、キャンバスへの位置合わせを検証。
- Unity EditMode: 7件成功。ブラウザからの確定値が「あ」「カ」「鬱」「龍」「→」の字形検証と装備設定まで届くこと、および複数文字・空白付き入力を拒否することを確認。
- Unity Webビルド成功。HTMLのコールバックと日本語IME用JSブリッジが成果物に含まれることを確認。
- 配信検証: `Reports/web-ime-production-delivery.json`。
- OSの実際の変換候補ウィンドウとスマートフォンキーボードは未実機確認。DOM回帰テストと実機操作は区別する。

テスト結果: `Reports/web-ime-dom-tests.txt`, `Reports/web-ime-unity.xml`

更新後はページを再読み込みする。既に起動中のUnityプレイヤーは更新前のままなので、再起動が必要。
