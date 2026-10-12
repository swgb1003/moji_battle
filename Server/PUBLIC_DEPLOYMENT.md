# ルームサーバーの公開とゲームへの組み込み

公開先はまだ作成していません。`fly.toml` はFly.io東京リージョンの共有CPU 1個・256MB・常時起動1台用です。
部屋情報をメモリで共有するため、必ず `--ha=false` でデプロイし、台数を増やさないでください。再起動・再デプロイでは部屋が消えます。

## 費用とログイン

Fly.ioアカウントと支払い方法の設定が必要です。VM、通信、ビルダー等の利用料が発生します。
2026年10月1日改定の料金表では最小VMの参考価格は月$2.19で、地域と通信量によって総額が変わります。
専用IPv4・ボリューム・データベースはこの構成では追加しません。
[料金表](https://fly.io/pricing-update/)

ターミナルで `flyctl auth login` を実行し、ブラウザで本人がログインします。認証トークンをチャットに送る必要はありません。

## 公開

以下の `<app-name>` はFly.ioで取得できる一意の名前に置き換えてください。実行には有料公開の承認が必要です。

```powershell
cd Server
flyctl apps create <app-name>
flyctl ips allocate-v6 --app <app-name>
flyctl ips allocate-v4 --shared --app <app-name>
flyctl deploy --app <app-name> --config fly.toml --ha=false
flyctl status --app <app-name>
```

コンテナに送るのは `package.json` と `server.mjs` だけです。Unityソースやローカルの認証情報は送りません。
HTTPSを強制し、`/health` を監視します。WebGLの許可元は `https://moji-fighter-x.vercel.app`。
ゲーム本体のドメインを変える場合は `fly.toml` の `ALLOWED_ORIGIN` も合わせます。

## ゲームに設定

プロジェクトルートで実行します。

```powershell
./Tools/SetRoomServer.ps1 -Url https://<app-name>.fly.dev
```

HTTPSと `/health` のゲーム用バージョンを確認してから `Assets/Resources/Online/RoomServer.txt` にURLを書き込みます。
ゲームは同梱URLを優先し、以前保存したローカル接続先を使わず、入室画面の接続設定を表示しません。
URL未設定の場合は従来のローカル接続・設定画面を維持します。
Editor／Development Buildでは `FriendRoomSession.DevelopmentServerOverride` で一時的にローカルテストができます。通常リリースにはこの機能を含みません。

## 最後の検証と配布

公開先で2クライアントの作成・参加・準備・対戦開始・同期・退出を確認します。
その後Unityのゲームを再ビルドしてください。すでに公開中のWebゲームにも変更を届けるには、WebGL再ビルドとゲーム本体の再公開が必要です。
