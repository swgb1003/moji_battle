# MOJI Fighter X ルームサーバー

Node.js 20以上で動作します。AI の必殺技づくりに `@anthropic-ai/sdk` を使います。

```powershell
cd Server
npm ci
node server.mjs
```

AI の必殺技づくり（`POST /specials`）には `ANTHROPIC_API_KEY` が必要です。無ければ部屋の機能だけ動き、技づくりは「準備中」と返します。
生成は 1 回ごとに料金がかかるため、接続元ごとに 1 時間 15 回・1 日合計 `SPECIAL_DAILY_LIMIT`（既定 500）回までに制限します。モデルは `SPECIAL_MODEL`（既定 `claude-opus-5-5`）で変えられます。
ローカル確認では `SPECIAL_FAKE=1` で AI の代わりにキーワードで技を決めます（公開サーバーでは使わないこと）。

既定ポートは8787。`PORT`、`HOST`、WebGL用の`ALLOWED_ORIGIN`、プロキシ配下で`X-Forwarded-For`を使う`TRUST_PROXY=1`で変更可能です。

公開サーバー：https://moji-fighter-x-rooms.onrender.com （Render無料枠。[手順](FREE_DEPLOYMENT.md)）
Unityの「友人と対戦 → 接続設定」にサーバーURLを設定します。初期値は`http://127.0.0.1:8787`。
2台を同じLANで接続する場合、両方にサーバーPCのLANアドレス（例：`http://192.168.1.10:8787`）を設定します。
ファイアウォールでは必要な範囲だけ接続を許可してください。

インターネット越しの対戦には、このサーバーを公開ホストに配置し、HTTPSのURLを両方のゲームに設定します。
公開デプロイはこの実装には含みません。Dockerfileを利用できます。通常リリースビルドはHTTPSを利用し、HTTPはEditor・Development Buildでの検証用です。
既定URLを配布用に変更する場合は`Assets/Resources/Online/RoomServer.txt`にHTTPS URLだけを記載してください。
公開先の疎通確認と書き込みには `Tools/SetRoomServer.ps1 -Url <HTTPS URL>` を使用できます。同梱URLを設定すると以前の保存済み接続先より優先し、入室画面の接続設定は自動で非表示になります。
Fly.io向けの公開設定を用意しています。[公開と組み込み手順](PUBLIC_DEPLOYMENT.md)を参照してください。アカウント・有料運用の承認が必要なため、公開先はまだ作成していません。

部屋は2人まで。ホストが作成し、友人が6桁コードで参加します。それぞれ装備を決めて準備完了にするとホストが対戦を開始できます。
装備変更で本人の準備が解除されます。対戦後・中断時は双方をロビーに戻して再準備します。
ホストの物理演算・HP・技・飛び道具・結果をゲストへ同期し、ゲスト側では勝敗を計算しません。

部屋情報はメモリ内で保持するため、サーバー再起動で部屋は消えます。単一プロセスで実行してください。
部屋ごとに推測困難な参加者トークンを発行し、他の参加者には公開しません。ホスト以外の開始・試合同期は禁止です。
通信が20秒途絶えると退出扱いになります。人数・装備・ペイロードサイズ・リクエスト頻度・試合IDを検証します。
対戦ホストを信頼する友人向けのモードであり、ランク戦や不正対策付きの競技サーバーではありません。

```powershell
npm test
```

通信実装参考：[UnityWebRequest](https://docs.unity3d.com/ja/2023.2/ScriptReference/Networking.UnityWebRequest.html)。
