# 設定画面のWeb公開（2026-10-10）

タイトル画面の設定ボタンを削除した。設定画面はモード選択メニューと対戦中から開く。

## デプロイ結果

- 公開URL: https://moji-fighter-x.vercel.app
- Target: production
- Status: READY
- Project: `moji-fighter-x` / `prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG`
- Team: `swgb1003-5441s-projects` / `team_T9S6jm2dWj9hQHWiULpbSwxx`
- Deployment: `dpl_42F7JpgrUmpB3o78njqy5zjMSRoD`
- Deployment URL: https://moji-fighter-p7qw507xs-swgb1003-5441s-projects.vercel.app
- Source: `e2b60e2` を基点とする作業中のローカルソース。未コミットの修正を含む最新WebGL成果物を公開し、Gitのコミットやpushは行っていない。
- Framework: Unity WebGL（VercelのOther / static）
- Local build time: `2026-10-10T08:19:01.7399736Z`
- Vercel build duration: 6秒、post-build 6秒。

公開内容は今回の設定画面、タイトルの設定ボタン削除、および前回依頼の称号アイコン・戦闘開始説明・COMの声援と踏ん張り・文字別の初期必殺技を含む。

## 検証

- タイトル画面を描画して設定ボタンがないことを確認。[実画面](UI/01_title.png)。
- 設定・メニューのPlayModeテスト9件成功（`settings-release-final-tests.xml`）。初回のテストでは必殺技生成用のローカルサーバーが停止していたため1件失敗し、検証用サーバーを起動した最終実行で全件成功。検証後にそのサーバーを停止した。
- 設定の実装時にはEditMode 23件、PlayMode 64件、計87件成功。[設定画面の検証](settings-verification.md)。
- 最新WebGLビルド成功（`settings-release-web-build.log`）。
- ローカルと公開サイトの両方で11ファイルの取得・内容一致・gzip整合性・Wasm／JS MIME・ライセンス表記を確認（`settings-release-local-delivery.json`、`settings-release-production-delivery.json`）。
- 未認証のHTTPクライアントで公開サイトの `build-info.json` がHTTP 200を返し、上記の最新ビルド日時に一致した。
- `vercel inspect` で本番READYと公開ドメインへの割り当てを確認。
- `vercel logs` で対象デプロイの直近1時間のerrorログを検索し、該当ログなし。
- Drainsの設定は未調査。監視確認は本番状態・配信ファイル・エラーログの照会に限る。
- ゲーム操作はUnityのPlayModeで検証し、今回ブラウザ上での操作は検証していない。

Vercel MCPでは対象プロジェクトが404だったため、既存の認証済みCLIでチーム・プロジェクトを確認し、その同じプロジェクトへ公開した。CLIは63.0.0を指定し、公開対象は `Builds/WebGL` の17ファイルのみ。`.env*` と `.vercel` は既存の除外設定を使用した。
