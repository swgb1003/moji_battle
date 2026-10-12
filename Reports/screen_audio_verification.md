# 画面BGM・ボタンSEの実装確認（2026-10-07）

指定された `Assets/Audio` 内のMP3を、そのまま参照してループ再生する。

|画面|音源|
|---|---|
|タイトル|title.mp3|
|モード選択|menu.mp3|
|トーナメント表|taunament.mp3|
|カスタマイズ・VS確認・大会準備|custermize.mp3|
|通常戦・大会1回戦・準決勝|battle.mp3 / battle2.mp3 / battle3.mp3 からランダム|
|大会決勝|final.mp3|

タイトルSTARTには金属音、通常戦の開始・大会エントリー・大会各試合の開始には和太鼓2連打。
SEを保持する音源はシーンをまたいで存続し、余韻や2打目が途切れない。
メニューBGMは戦闘シーンへ入ると停止。通常戦・決勝とも開始ゴングが鳴り終わってからBGMを開始する。
戦闘中の一時停止・再開・試合終了・再戦は従来の音声制御を維持する。

## 検証

Unity 6000.0.61f1の作業コピーでPlayModeテストを実行。
完成した和太鼓音源での最終確認は **18件成功、失敗0件**。
[結果XML](screen-audio-final.xml)

- 画面音声3件：タイトル → メニュー → カスタマイズ → VS、戻る操作、戦闘開始、大会エントリー。
- 戦闘音声10件：通常戦と決勝のゴング待ち、ループ、停止、一時停止の位置保持、再戦、既存効果音。
- 大会フロー5件：3連勝の各試合で通常BGM／決勝BGMを選択、引き分け、敗退、COMの非表示・無音集計。
- 画面切り替え後のSE再生継続、単一の画面音源管理・AudioListener、戦闘カウントダウン中のBGM停止を確認。

和太鼓は0.24秒間隔に2つの打音を配置し、2打目をやや強く調整。
2素材ともPCM16・44.1kHz・モノラル・ピーク0.85。
[音源の長さ・音量](ui_audio_assets.json)／[ボタンSE試聴](ui_audio_preview.html)

[実装・音量調整の説明](../Assets/Documentation/CombatAudio.md)

## タイトルの初回再生修正

シーン読み込みイベントに加え、`ScreenAudioDirector.Start` で現在のシーンのBGMとAudioListenerを初期化する。
タイトルが既に読み込まれている状態でも、画面遷移なしでタイトルBGMを開始する。
同じBGMが既に再生中の場合は再スタートしない。

Unity 6000.0.61f1の作業コピーで **15件成功、失敗0件**。

- タイトルシーンを開いてEditorのPlayへ移行：BGM再生、ミュート解除、再生位置の進行、AudioListenerと音声管理が各1つであることを確認。[直接起動の結果](title-startup-tests.xml)
- 読み込み済みタイトルで音声管理が生成されるケース1件、画面遷移の既存3件、戦闘音声10件を確認。[PlayModeの結果](title-audio-tests.xml)

## トーナメント表のBGM変更

`BattleMusicSettings.tournamentLoop` に `Assets/Audio/taunament.mp3` を設定し、トーナメント表で専用曲をループ再生する。
Unity 6000.0.61f1の作業コピーで画面音声の **4件成功、失敗0件**。
大会エントリー後の `taunament` のループ再生、開始SEの継続、モード選択へ戻った際の `menu` への切り替えを確認。
[検証結果](tournament-music-tests.xml)
