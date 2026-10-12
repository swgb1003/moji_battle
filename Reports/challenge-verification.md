# お題バトルの検証

2026-10-10 / Unity 6000.0.61f1。

実装：6お題、6画面の遷移、個別構成の保存、クリア判定、再挑戦、同じ構成での再戦、次のお題、初回称号6個。
承認された和紙・墨・朱色のデザインをRetroUiと既存の対戦・カスタマイズ表示で実装。
設定・通常対戦・大会・友人対戦は既存の仕組みを利用し、従来の装備保存を上書きしない。

## テスト

- EditMode：22 / 22成功（ChallengeTests / AchievementTests / CustomizeDataTests）。最終実行：challenge-edit-final.xml。
- PlayMode：71 / 71成功（ChallengeFlowTests / CustomizeFlowTests / MenuUiTests / SettingsFlowTests / BattleUiTests / AchievementFlowTests / TournamentFlowTests / CombatAudioTests / ScreenAudioTests / InterventionTests / FriendRoomTests）。
- 重複を除く合計：93件。
- ChallengeFlowTestsは6件。全6相手の組み立て、条件固定、通常装備の保存維持、声援の成功、実際の必殺ダメージによるKO、再戦・再編集・次のお題、未達成・敗北・引き分け、報酬と称号カテゴリを検証。
- データの再読み込み、称号の重複防止、必殺KO以外でクリアしないこと、相手の声援を誤って数えない条件を検証。
- テストはユーザーの設定・装備・称号・お題進行を退避して復元。
- Fake backendはSPECIAL_FAKE=1のローカルテスト用。公開用の生成サービスは変更していない。

## 実画面

[デザインと実装の比較ギャラリー](UI/Challenges/index.html)。各画面を1920×1080と880×450で撮影し、操作ボタンが画面内に収まることをチェック。実画像は以下。

| 画面 | 画像 |
|---|---|
| メニュー | [01_menu.png](UI/Challenges/01_menu.png) |
| お題一覧 | [02_list.png](UI/Challenges/02_list.png) |
| 詳細 | [03_detail.png](UI/Challenges/03_detail.png) |
| 準備 | [04_prepare.png](UI/Challenges/04_prepare.png) |
| 対戦 | [05_battle.png](UI/Challenges/05_battle.png) |
| 結果 | [06_result.png](UI/Challenges/06_result.png) |
| クリア済みフィルター | [07_cleared_filter.png](UI/Challenges/07_cleared_filter.png) |
| 条件未達成 | [08_goal_not_met.png](UI/Challenges/08_goal_not_met.png) |
| 称号 | [09_rewards.png](UI/Challenges/09_rewards.png) |
| 敗北 | [10_defeat.png](UI/Challenges/10_defeat.png) |
| 引き分け | [11_draw.png](UI/Challenges/11_draw.png) |

HP・字体・握る位置・能力値・必殺名・進行状況は、画像案の仮の数値を固定せず実データを表示。
動いている棒人間・文字の姿勢は試合や構成に応じて変わる。デザイン画像とすべての画素が一致するという意味での保証はしていない。

## 公開対象

既存のVercelチーム swgb1003-5441s-projects / プロジェクト moji-fighter-x / productionを使用。
orgId: team_T9S6jm2dWj9hQHWiULpbSwxx。
projectId: prj_dysF5ncQzt0MeAgRoHEDSazVQ3eG。
Git基準コミット: e2b60e25a4a4c7b34759b032832b2829f079bfc5。以前からの未コミット変更と今回の変更を含むローカルWebビルドが対象。commit/pushはしていない。
公開成果物のみをBuilds/WebGLから配置し、ソース・Reports・.env・ローカルテスト用サーバーは配置しない。
WebGLビルド成功：2026-10-10T10:21:00.1552130Z（19:21 JST）。成果物66,056,388 bytes。
challenge-web-build.logに完了記録。ローカルと公開先のそれぞれで11ファイルのHTTP 200・内容一致・gzip・Wasm/JS MIME・フォントライセンスを確認。
配信記録：challenge-local-delivery.json / challenge-production-delivery.json。

## Deploy Result

- URL: https://moji-fighter-x.vercel.app
- Deployment: https://moji-fighter-5p1g529rw-swgb1003-5441s-projects.vercel.app
- ID: dpl_7Csss5GppzJdVzdkPcSH67XDJ44J
- Target: production
- Status: READY（CLI inspectでも確認）
- Commit: e2b60e2を基準とする未コミットのローカル変更を含む。
- Framework: Unity WebGL / static（Vercel Other）
- Vercel Build Duration: 7秒

## Post-Deploy Observability

- Error scan: 当該デプロイの直近1時間をerrorレベル・上限50件で確認。該当ログなし。
- Drains: 未確認。
- Monitoring: 静的ファイル配信を確認。クライアントのブラウザーコンソールと外部監視は確認対象外。

操作フローの検証と画面撮影はUnityの実動作で行った。Web版の実ブラウザー操作テストは未実施で、HTTP・MIME・gzip・ファイル内容の配信確認と区別する。
