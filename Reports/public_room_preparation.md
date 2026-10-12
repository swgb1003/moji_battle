# 公開ルームサーバーの準備

2026-10-08 / Unity 6000.0.61f1

## 完了した準備

- Fly.io東京リージョンの共有CPU 1個・256MB、HTTPS、常時起動、ヘルスチェック、WebGLの許可元を `Server/fly.toml` に設定。
- `--ha=false` で1台だけ公開する手順を `Server/PUBLIC_DEPLOYMENT.md` に記載。メモリ内の部屋情報が別VMに分散する構成は使用しない。
- HTTPSの `/health` と通信バージョンを確認してからURLを書き込む `Tools/SetRoomServer.ps1` を追加。
- 同梱された公開URLを保存済みローカルURLより優先し、設定済みなら入室画面の接続設定を非表示にする。
- Editor／Development Buildの自動テストは非永続の明示的な接続先上書きで実施可能。

## 検証

- Unity EditMode 9件合格：URLの優先順位、HTTPSと認証・クエリ・フラグメントの拒否、未設定時のローカル接続 (`public-room-config-tests.xml`)。
- Unity PlayMode 2件合格：公開URLありの入室画面で設定ボタンがないこと、2クライアントの装備・準備・試合開始・同期・ロビー帰還 (`public-room-ui-tests.xml`)。
- サーバーHTTPテスト6件合格。
- Fly設定をPythonのtomllibで解析してHTTPS、ポート、256MB、自動停止なしを確認。Fly側の構成検証はログイン未済のため未実施。
- URL設定スクリプトのPowerShell構文確認とHTTP URLの書き込み拒否を確認。
- `UI/friend_room_entry_bundled_server.png` を撮影して設定ボタンの非表示を確認。

## 未完了

Fly.io CLIは未ログイン。公開先アプリは作成しておらず、有料運用の確認とアカウントへのログインが必要。
実際の公開URLはまだないため、元プロジェクトの `Assets/Resources/Online/RoomServer.txt` は未作成。
画面テストでは隔離コピーに `https://rooms.example.com` を設定し、ネットワークテストは明示的にローカルサーバーへ接続した。公開URLでの接続を検証したものではない。
公開サーバーの動作確認後、URLを書き込み、ゲームの再ビルドと公開版の更新を行う必要がある。
