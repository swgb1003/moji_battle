# Web版のビルドとVercel公開

Unity 6000.0.61f1 の Web Build Support を使用します。ブラウザでは同梱フォントを使い、読み込み完了後の「クリックしてはじめる」で音声を有効にします。戦闘のゴングとBGMのタイミングは通常版と共通です。

Web版の武器の文字入力には、Unity標準のInputFieldではなくブラウザのHTML inputを重ねて使います。Unity Webの標準IMEは未対応のためです。変換中は文字数で切り詰めず、compositionend以降の確定した値をゲームの字形検証へ渡します。入力中だけUnityのキーボード取得を解除し、入力終了・画面移動で復元します。P1/P2切り替え、対戦確認画面での非表示、画面移動時の削除、画面サイズ変更にも対応します。全画面表示の対象をゲームと入力欄の親要素にして、全画面でもブラウザ入力欄を表示します。

IMEの回帰テストは `Tools/test_web_ime.cjs` （jsdomをインストールしたnode_modulesのパスを引数で指定）と `MojiBattle.Tests.WebCharacterInputTests` です。前者はDOMの変換イベントと送信処理、後者は確定文字からUnityの装備設定までを確認します。OSの実際の変換候補ウィンドウやモバイルキーボードの実機確認とは区別してください。

友人対戦のルームコードにもブラウザ標準の入力欄を使います。表示はUnityの6マス、入力・選択・削除は透明なHTML inputが担当します。小文字は大文字に正規化し、6文字の有効なコードでEnterを押すと参加します。通信中やロビーでは入力欄を隠します。

## ビルド

Unityで `MojiBattle > Build Web (Vercel)` を選択するか、Unityを閉じてプロジェクトルートで以下を実行します。

```powershell
./Tools/BuildWeb.ps1
```

出力は `Builds/WebGL`、ログは `Logs/web-build.log` です。このコマンドはWebプラットフォームへ切り替え、Web用のPlayer Settingsを設定します。既に生成済みのシーンと装備・バランス設定を使い、Setupコマンドによる再生成は行いません。

## ローカル確認

```powershell
python Tools/serve_web.py --directory Builds/WebGL --port 8015
```

`http://127.0.0.1:8015` を開きます。HTMLを直接開くと起動できません。フォント・文字入力・持ち手のドラッグ・画面ごとのBGM・ゴング後の戦闘BGM・トーナメント表とCOM試合結果を確認してください。

## Vercel公開

Unityのソース全体ではなく、**Builds/WebGLの内容だけ**を公開します。ビルド環境・認証情報・テストログは含めません。生成される `vercel.json` は圧縮ファイルにgzipヘッダー、WasmにMIMEタイプ、ハッシュ名のデータにキャッシュを指定します。Framework PresetはOther、Build/Install Commandは空欄です。

Vercelにログイン済みのCLIから、接続先チームを明示して公開できます。

```powershell
npx vercel@63.0.0 --scope swgb1003-5441s-projects deploy Builds/WebGL --prod
```

更新時もWebビルドを作り直して同じプロジェクトへデプロイします。生成する `.vercelignore` で、認証・接続管理用の `.env*` と `.vercel` を公開対象から除外します。

## 配信の検証

```powershell
python Tools/verify_web.py https://moji-fighter-x.vercel.app --report Reports/web-production-delivery.json
```

公開先のHTML・ローダー・圧縮データ・Wasm・JavaScript・フォントライセンスがローカルの成果物と一致することをチェックします。gzipの整合性と配信ヘッダー、WasmのMIMEタイプも確認します。この検証は配信ファイルを確認するもので、実際のゲーム操作の確認はブラウザで別に行います。

初回公開先： https://moji-fighter-x.vercel.app

Web版は16:9の画面で、全画面ボタンを用意しています。初回はゲームデータのダウンロードが必要です。スマートフォンは横向きが適しています。PCブラウザでの確認と、実機でのスマートフォン確認は別に行ってください。
