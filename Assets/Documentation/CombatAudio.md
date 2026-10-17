# 戦闘効果音（2026-10-06）

Runway Sound Effects で生成した38素材を `Resources/Audio/Combat` に収録（追加技用8素材を含む）。
生成プロンプトとタスクIDは `Tools/combat_audio_sources.json`、長さ・ピーク等は
`Reports/combat_audio_assets.json` に記録。44.1kHz / モノラル / PCM16。
冒頭の無音を除き、端に短いフェードを付けてピークを0.85以下に調整。
UnityではPCM・事前読み込みで取り込む。

## 再生する場面

|場面|素材|
|---|---|
|カウントダウン終了 / 戦闘終了|gong_start / gong_end|
|跳び斬りの離陸・跳び越し回避|jump|
|空中から着地|land_light / land_heavy|
|攻撃の振り開始|swing_light_01–02 / swing_heavy_01–02|
|通常打撃 / 強い打撃|hit_01–03 / hit_heavy_01–02|
|クリティカル|critical|
|ガード / 武器同士の衝突 / ガードブレイク|guard / clash / guard_break|
|壁に激突 / 転倒して床に接触|wall / fall|
|打ち上げ / 叩き落とし・叩きつけ|launch / slam|
|バックステップ|evade|
|投げで武器を手放す / 拾ってヒンジを戻す|throw / pickup|
|攻撃前の溜め / 接地して歩く / 起き上がり|charge / step_01–02 / recover|
|KOの決着|ko|
|踏み込み斬りの前進 / 振り / 命中|dash_step / dash_swing / dash_hit|
|受け流し成功 / 斬り返し / 反撃命中|parry / counter_swing / counter_hit|
|全身ぶん回しの回転 / 命中|whirl_swing / whirl_hit|

`BattleBuilder` が観戦表示を有効にした試合に `CombatAudioDirector` を追加する。
シミュレーション専用試合には追加しない。既存のAudioListenerがない場合はカメラに追加する。

効果音用12個とBGM専用1個の音源は `CombatAudioVoices` 子オブジェクトに配置する。2D音源のため位置は音量に影響しない。
カメラ後方に配置し、エディターのスピーカーアイコンが初期化直後にも戦闘画面に重ならないようにする。
エディターでは音源の注釈が登録されてからアイコン／ギズモを無効にする。
起動直後だけの設定では `Annotation not found` となる場合があるため、動的生成後にも適用する。

## 再生調整

- 同時再生は最大12音。ゴング・KO・命中・衝突を足音や溜めより優先。
- 足音と溜め音は小さくし、武器サイズ／重量で振り音を使い分ける。
- 通常打撃3種、重い打撃2種、振り各2種、足音2種を順番に切り替える。
- 音の高さも少し変える。戦闘の乱数を消費せず、物理・AIには影響しない。
- わずかな左右定位を付け、ゴングは中央で再生。
- 着地は空中に上がった後に限定し、接地中の跳ね・微小接触では鳴らさない。
- 環境ダメージの叩きつけ音と通常着地を同じ接触で重ねない。
- 一時停止で再生中の音も停止し、再開時に続きから再生。開始／終了ゴングは各1回。
- KO演出後に終了ゴング。終了後の新しい移動音は抑える。
- 音量は `CombatAudioDirector.MasterVolume`（既定0.8）で調整可能。

## 戦闘BGM（2026-10-07追加）

通常戦・大会1回戦・準決勝では、`Assets/Audio/battle.mp3`（1曲目）・`battle2.mp3`・`battle3.mp3` から試合ごとに各1/3の確率で抽選する。
同じ曲が連続することもある。戦闘用の乱数とは独立しており、曲の選択は試合結果に影響しない。
大会決勝は `Assets/Audio/final2.mp3` に切り替える（引き分け再試合も同じ決勝BGM）。
カウントダウン中は再生せず、開始ゴング（約2.48秒）が鳴り終わった直後にループ再生を始める。
一時停止時はゴング待ち／BGMの再生位置を保ち、再開時に続きから再生する。
観戦速度やヒットストップでBGMの速さ・音程は変わらない。
試合終了時にBGMを止めて終了ゴングを鳴らし、再戦では新しい開始ゴングからやり直す。

効果音用の音源と分け、BGMはステレオ・Vorbisストリーミングで取り込む。
`Resources/Audio/BattleMusic.asset` が元の音源を参照するため、プレイヤービルドにも含まれる。
BGM音量は `CombatAudioDirector.MusicVolume`（既定0.35）で調整でき、
実際の音量は `MasterVolume × MusicVolume`（既定0.28）。

## 画面BGM・ボタンSE（2026-10-07追加）

|画面|BGM（Assets/Audio）|
|---|---|
|タイトル|title.mp3|
|メニュー（モード選択）|menu.mp3|
|トーナメント表|taunament.mp3|
|カスタマイズ・VS確認・大会準備|custermize.mp3|
|通常戦・大会1回戦・準決勝|battle.mp3 / battle2.mp3 / battle3.mp3 からランダム|
|大会決勝|final2.mp3|

`ScreenAudioDirector` が起動時に作られ、画面切り替えを検知して2D・ループ再生する。
戦闘シーンへ移った時点で画面BGMを停止し、戦闘BGMは `CombatAudioDirector` が開始ゴング後に再生する。
戻る操作でも該当する画面BGMへ切り替え、カスタマイズ内のVS確認では音楽を継続する。
画面側のBGM音量は `ScreenAudioDirector.MusicVolume`（既定0.28）。

タイトルのSTART（Enter/Space含む）で金属音「キーン」、VS確認のBATTLE開始（Ctrl+Enter含む）で和太鼓「ドンドンッ」。
大会エントリーとトーナメント表の試合開始にも同じ和太鼓を使用する。
SE用音源は画面切り替えで破棄せず、余韻や2打目が途切れないようにする。
音量は `ScreenAudioDirector.ButtonVolume`（既定0.8）。

追加SEは Runway Sound Effects で生成し、`Assets/Audio/UI/title_start.wav` と `battle_start.wav` に収録。
冒頭の無音を除き、PCM16・44.1kHz・モノラル・ピーク0.85で事前読み込みする。
生成記録は `Tools/ui_audio_sources.json`、音量・長さの記録は `Reports/ui_audio_assets.json`。

## 試聴

`Reports/combat_audio_preview.html` をブラウザーで開くと各素材を個別／連続試聴できる。
`Reports/combat_audio_preview.wav` は表の順番で、素材間に0.5秒の間隔を置いた試聴用音声。
試聴ページを作り直す場合は `python Tools/build_audio_preview.py`。
