# MOJI Fighter X Web公開

- 公開URL: https://moji-fighter-x.vercel.app
- Vercel team: `team_T9S6jm2dWj9hQHWiULpbSwxx` (`swgb1003-5441s-projects`)
- Project: `moji-fighter-x` / `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`
- Production deployment: `dpl_74rv4cWdS4dxNYx2vqgcdURm3Vzi` / READY
- Unity: 6000.0.61f1 / WebGL / release build
- 出力: `Builds/WebGL`（約56.8MiB）

## 実施した変更と検証

Web Build Supportを追加。OSフォントに依存するHUD・英数字のUIをWeb版では同梱フォントに変更。Web用テンプレートに読み込み表示、音声を開始するクリック、全画面ボタンを追加。ゲーム内の画面遷移・戦闘処理・BGM選択・ゴング後の再生タイミングは既存コードを使用。

Webビルドが成功し、音声ブリッジとAudioContextのresume処理が出力に含まれることを確認。既存設定を保持するため、ビルドは隔離したプロジェクトコピーから行った。元のプロジェクトのWindows用設定は変更していない。

ローカルHTTPサーバーと公開URLの両方で10ファイルを検証。HTML、ローダー、ゲームデータ、Wasm、JavaScript、4字体のOFL通知の内容をハッシュ付きレポートに保存。gzipの整合性、Content-Encoding、Wasm/JSのContent-Typeを確認。Vercelに未ログインのHTTPクライアントから公開URLを取得できた。

CLIのlinkコマンドが作成した`.env.local`は削除し、`.vercelignore`で認証・接続ファイルを配信対象から除外。Vercelに送信したのはビルド結果と配信設定のみ。Unityソース、テスト、ログ、認証情報は送信していない。

## 未確認の範囲

ブラウザ操作ツールに接続されたブラウザがなく、実ブラウザ上のタイトル→カスタマイズ→戦闘→トーナメントの操作テストは未実施。Playwrightによる代替テストについてユーザーへの確認が未回答。スマートフォン実機も未確認。配信の成功と実際の操作確認は区別する。

## 再現用のファイル

- ビルド: `Tools/BuildWeb.ps1`, `Assets/Scripts/Editor/WebBuild.cs`
- 配信プレビュー: `Tools/serve_web.py`
- 配信検証: `Tools/verify_web.py`
- 検証結果: `Reports/web-local-delivery.json`, `Reports/web-production-delivery.json`
- 手順: `Assets/Documentation/WebDeployment.md`
- ビルドログ: `Logs/web-build.log`, `Logs/web-rebuild.log`
