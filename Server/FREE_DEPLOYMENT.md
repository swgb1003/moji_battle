# Render無料枠での公開

`render.yaml` は無料Webサービス1台用です。有料VM、データベース、ディスクは追加しません。
支払い方法を登録せずに利用する場合、無料枠超過時は自動課金ではなくサービス停止・ビルド停止になります。
[無料枠の仕様](https://render.com/docs/free)

15分アクセスがないと休止し、次のアクセスで起動まで約1分かかります。月750時間はワークスペース全体で共有します。
サーバーが休止・再起動するとメモリ内の部屋情報は消えます。友人対戦中は両者から継続的に通信しますが、プラットフォームによる再起動は起こりえます。

## 公開準備

1. Renderに本人のアカウントでログインする。
2. このディレクトリの `server.mjs`、`specials.mjs`、`package.json`、`package-lock.json`、`Dockerfile`、`.dockerignore`、`render.yaml` だけを公開用Gitリポジトリに配置する。
   AI の必殺技づくりを使う場合は、Render の Environment に `ANTHROPIC_API_KEY` を本人が設定する（リポジトリには書かない）。
3. RenderでそのリポジトリからBlueprintを作成し、サービスのプランが **Free** になっていることを確認して公開する。
4. Renderが割り当てたHTTPS URLの `/health` で `moji-fighter-x-rooms-v3` の応答を確認する。
5. プロジェクトルートで `Tools/SetRoomServer.ps1 -Url <HTTPS URL>` を実行し、ゲームを再ビルドする。

Blueprintは公開用リポジトリのルートに `render.yaml` がある構成です。Unityプロジェクト全体をアップロードする必要はありません。
## 公開済みの構成（2026-10-08）

- URL: https://moji-fighter-x-rooms.onrender.com （`/health` で `moji-fighter-x-rooms-v3`）
- Renderサービス: `srv-db3mkam0tbcc73858i50`（Free・Singapore・Docker・自動デプロイ無効）
- ソース: https://github.com/swgb1003/moji-fighter-x-rooms （このディレクトリのサーバーファイルのみ）
- `TRUST_PROXY=1`: Renderのプロキシ越しでも、`X-Forwarded-For` の接続元ごとに回数制限する。

サーバーを更新したら公開リポジトリへpushし、Renderの画面の Manual Deploy か API で再デプロイする。

## クライアントの対応

接続開始時に最大90秒の起動待ちを行い、起動待ち中の案内を表示します。
部屋作成のPOSTを繰り返すと複数の部屋ができる場合があるため、起動待ちはGET `/health` で行い、準備できてから作成・参加します。
起動待ち中に画面を離れた場合は、応答待ちを中断し部屋を作成しません。
