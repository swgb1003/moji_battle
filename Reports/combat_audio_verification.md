# 戦闘効果音の確認（2026-10-06）

## 素材

- Runway Sound Effectsで30個生成し、Unity用の44.1kHz・モノラルPCM WAVに変換。
- 全30ファイルについてPCMデータを読み込み、無音・クリッピングがないことを検証。
- 全24再生種別に素材を割り当て。振り・通常打撃・強打・足音には複数バリエーション。
- 試聴ページ: `combat_audio_preview.html`。試聴音声: `combat_audio_preview.wav`。

## Unity PlayModeテスト

Unity 6000.0.61f1。開いているエディターに影響を与えないよう、Assets・Packages・ProjectSettingsを一時フォルダーへコピーして検証。
**20件成功、失敗0件**。結果XML: `combat_audio_tests.xml`。

- 効果音4件: 全素材の読み込み／カウントダウンと一時停止／レアな戦闘イベントの音の選択／
  着地と環境ダメージの重複抑制／実際の跳び斬り・投げ・拾い／実戦の足音・命中・終了ゴング。
- 戦闘15件: 武器保持・攻撃・跳び斬り・連携・投げ・XL壁越え復帰などの回帰テスト。
- seed再現性1件。

実戦の再生記録:

```
StartGong=1, Step=4, Charge=6, Evade=4, SwingLight=13,
LandLight=12, Critical=5, Slam=2, LandHeavy=18, Launch=3,
Fall=2, Hit=2, Guard=1, Jump=1, Wall=1, EndGong=1
```

音声データとイベント連携を自動確認した記録。素材の音色は試聴ページで確認できる。

## スピーカーアイコンの再修正

エディター実行ログで、起動時のアイコン非表示処理が `Annotation not found` により適用されていなかったことを確認。
登録後にも適用するよう変更し、2D音源をカメラ後方の子オブジェクトへ移動。
全12音源がカメラの後方にあり、2D再生設定を保つことを確認する項目を追加。
効果音の4テストすべて成功。再修正のログには `Annotation not found` 警告なし。
結果XML: `combat_audio_icons_tests.xml`。
