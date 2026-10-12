# UIの位置・枠線重なり修正

2026-10-10 / Unity 6000.0.61f1。

## 修正

- お題結果の未達成見出し「再び、挑め。」を中央へ。末尾の句点の空白による見た目の偏りも補正。クリア時はCLEAR印のスペースを維持。
- モード選択の3つの開始ボタンを上げ、説明欄の高さを調整。黒い影の下端と内側の枠線の間に余白を確保。
- 通常・大会・友人対戦の共通構成画面の下部ボタンを10px上げる。
- お題一覧の最初の開始ボタンは内枠と押下アニメーション分の余白を確保。
- お題詳細の開始ボタン、大会と対戦の下部ボタンの高さを調整し、影が画面の内枠に重ならないようにする。
- 友人対戦のフッターを上げ、開発用の接続設定ボタンも内枠内に収める。

## 画面確認

タイトル、モード選択、通常の構成画面、VS確認、必殺技、大会、お題一覧・詳細・構成・対戦・結果、称号、設定の基本・音楽・表示・データ・確認ダイアログ、友人対戦の入口・ロビー・対戦・結果を、座標と実画面で確認。

修正箇所は1920×1080、880×450の実行画面で確認。友人対戦は1230×690と880×450。
画像は Reports/UI と Reports/UI/Challenges。画面の固定値ではなく既存の実行フローで撮影。

## 検証

ui-frame-fixes.xml、ui-frame-friend-final.xml、ui-frame-challenge-final.xml。
初回の友人対戦3件はローカル検証サーバー停止で失敗。起動後に友人対戦6件を再実行。
最終結果：重複を除いた20件すべて成功。友人対戦は6/6、お題の再確認は2/2。
「再び、挑め。」の朱色の描画領域の中心は960.5px、1920px画面の中央は960px。差は0.5px。
git diff --checkも成功。

## Deploy Result

- URL: https://moji-fighter-x.vercel.app
- Deployment: https://moji-fighter-cw4f62lyl-swgb1003-5441s-projects.vercel.app
- ID: dpl_2JsmLnNZay6fQ4umJbEsjmd8ckSV
- Target: production
- Status: READY（inspectで確認）
- Commit: e2b60e25a4a4c7b34759b032832b2829f079bfc5を基準とするローカル変更を含む。
- Framework: Unity WebGL / static
- Build Duration: Vercel 4秒
- BuiltAtUtc: 2026-10-10T11:12:22.9613057Z
- Team: swgb1003-5441s-projects / team_T9S6jm2dWj9hQHWiULpbSwxx
- Project: moji-fighter-x / prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG

ローカル・公開URLの各11ファイルについて内容一致、gzipの整合性、Wasm/JS MIME、ライセンス配信を確認。
ui-frame-final-local-delivery.json / ui-frame-final-production-delivery.jsonに記録。

## Post-Deploy Observability

- Error scan: 当該デプロイの直近1時間・errorレベル・上限50件。該当ログなし。
- Drains: 未確認。
- Monitoring: HTTPの配信確認。Webブラウザーのコンソールは未確認。

UIの操作と撮影はUnity上で確認。Web版は実ブラウザーでの操作検証と区別し、公開ファイルの内容・HTTP・gzip・MIMEを確認する。
